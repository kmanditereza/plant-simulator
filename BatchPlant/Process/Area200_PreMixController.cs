using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 200 - Pre-Mixing (Honeywell Experion DCS, ~2005).
    ///
    /// High-shear mixer MX-201 that doses raw ingredients from Area 100 and prepares a
    /// homogenized slurry for the cook kettle. Since Phase 3, ingredients are actually
    /// pulled from Area 100 (paste pump runs, silo levels drop, water totalizer increments)
    /// instead of appearing from nothing. The mixer also acts as a product source for
    /// Area 300 — it stays in the Discharging state until downstream has fully drained it.
    /// </summary>
    public class Area200_PreMixController : ISimulatable, IProductSource
    {
        private readonly PlantContext _ctx;
        private readonly Area100_RawMaterialsController _rawMaterials;

        public Vessel Mixer { get; }
        public Motor Agitator { get; }
        public Valve WaterInlet { get; }
        public Valve PasteInlet { get; }
        public Valve VinegarInlet { get; }
        public Valve Discharge { get; }
        public Valve FlowControl { get; }

        public int Priority => 40;

        private enum CycleState
        {
            Idle = 0,
            FillingWater = 1,
            DosingPaste = 2,
            DosingVinegar = 3,
            RampingAgitator = 4,
            Mixing = 5,
            RampingDown = 6,
            Discharging = 7
        }

        private CycleState _cycle = CycleState.Idle;
        private DateTime _cycleEntered;
        private bool _initialized;

        // Dosing rates (L/min or kg/min as appropriate)
        private const double WaterFlowLpm = 150;
        private const double PasteFlowKgPerMin = 80;
        private const double VinegarFlowLpm = 60;

        // Targets (volumes for liquid phase dosing)
        private const double WaterTargetL = 630;
        private const double PasteTargetKg = 945;
        private const double VinegarTargetL = 378;

        // Accumulated doses (reset on each cycle start)
        private double _pasteDosedKg;
        private double _waterDosedL;
        private double _vinegarDosedL;

        public Area200_PreMixController(PlantContext ctx, Area100_RawMaterialsController rawMaterials)
        {
            _ctx = ctx;
            _rawMaterials = rawMaterials;

            Mixer = new Vessel(ctx, "MX-201")
            {
                Capacity = 3500,
                Temperature = 22.0,
                AmbientTemp = 22.0,
                Density = 1.08,
                HeatLossCoefficient = 0.00002,
                IsJacketed = false
            }.Bind(new VesselTagMap
            {
                Level = "LIT-201",
                LevelPct = "LIT-201_PCT",
                Temperature = "TIT-201",
                Weight = "WIT-201"
            });

            Agitator = new Motor(ctx, "AGIT-201")
            {
                MaxSpeed = 1500,
                RatedPower = 75,
                RatedCurrent = 130,
                RampRate = 150,
                BaseVibration = 0.8,
                AmbientTemp = 22.0
            }.Bind(new MotorTagMap
            {
                Speed = "AGIT201_SPD",
                Torque = "AGIT201_TRQ",
                Power = "AGIT201_PWR",
                Current = "AGIT201_AMPS",
                Vibration = "AGIT201_VIB",
                Run = "AGIT201_RUN",
                Fault = "AGIT201_FAULT"
            });

            WaterInlet = new Valve(ctx, "XV-201") { IsOnOff = true, TravelTimeSeconds = 2.0 }
                .Bind(new ValveTagMap { Open = "XV-201" });
            PasteInlet = new Valve(ctx, "XV-202") { IsOnOff = true, TravelTimeSeconds = 2.0 }
                .Bind(new ValveTagMap { Open = "XV-202" });
            VinegarInlet = new Valve(ctx, "XV-203") { IsOnOff = true, TravelTimeSeconds = 2.0 }
                .Bind(new ValveTagMap { Open = "XV-203" });
            Discharge = new Valve(ctx, "XV-204") { IsOnOff = true, TravelTimeSeconds = 3.0 }
                .Bind(new ValveTagMap { Open = "XV-204" });
            FlowControl = new Valve(ctx, "FCV-201") { IsOnOff = false, TravelTimeSeconds = 5.0 }
                .Bind(new ValveTagMap { Position = "FCV-201" });
        }

        // ---- IProductSource implementation - available only during Discharging ----

        public bool HasProductAvailable => _cycle == CycleState.Discharging && Mixer.Level > 0.5;
        public double ProductTempC => Mixer.Temperature;

        public double DrawForTransfer(double litersRequested)
        {
            if (!HasProductAvailable) return 0;
            return Mixer.RemoveMaterial(litersRequested);
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            if (!_initialized)
            {
                _cycleEntered = simNow;
                _initialized = true;
            }

            // 1. Run state machine
            RunTestCycle(simNow);

            // 2. Draw from Area 100 based on which inlet valve is open
            double dtSec = dt.TotalSeconds;

            if (WaterInlet.IsOpen)
            {
                double desiredL = WaterFlowLpm * dtSec / 60.0;
                double actualL = _rawMaterials.DrawWater(desiredL);
                Mixer.AddMaterial(actualL, 15.0);
                _waterDosedL += actualL;
            }
            if (PasteInlet.IsOpen)
            {
                double desiredKg = PasteFlowKgPerMin * dtSec / 60.0;
                double actualKg = _rawMaterials.DrawPaste(desiredKg);
                // Paste ~1.1 kg/L → convert kg to L for vessel level
                double volumeL = actualKg / 1.1;
                Mixer.AddMaterial(volumeL, 18.0);
                _pasteDosedKg += actualKg;
            }
            if (VinegarInlet.IsOpen)
            {
                double desiredL = VinegarFlowLpm * dtSec / 60.0;
                double actualL = _rawMaterials.DrawVinegar(desiredL);
                Mixer.AddMaterial(actualL, 20.0);
                _vinegarDosedL += actualL;
            }

            // 3. Agitator load scales with mixer fill
            double fillFraction = Math.Min(1.0, Mixer.Level / Mixer.Capacity);
            Agitator.LoadFactor = 0.1 + fillFraction * 1.1;

            // 4. Agitator viscous heat dissipation
            if (Agitator.State == EquipmentState.Running && Mixer.Level > 100)
            {
                Mixer.ExternalHeatInputKw = Agitator.Power * 0.30;
            }

            // 5. Tick equipment
            Mixer.Tick(dt, simNow);
            Agitator.Tick(dt, simNow);
            WaterInlet.Tick(dt, simNow);
            PasteInlet.Tick(dt, simNow);
            VinegarInlet.Tick(dt, simNow);
            Discharge.Tick(dt, simNow);
            FlowControl.Tick(dt, simNow);

            // 6. Publish area-level tags
            double totalInflowLpm = 0;
            if (WaterInlet.IsOpen) totalInflowLpm += WaterFlowLpm;
            if (PasteInlet.IsOpen) totalInflowLpm += PasteFlowKgPerMin / 1.1; // kg/min -> L/min
            if (VinegarInlet.IsOpen) totalInflowLpm += VinegarFlowLpm;

            _ctx.Tags.Write("FIT-201", Math.Round(totalInflowLpm, 1));
            _ctx.Tags.Write("MX201_STATE", (int)_cycle);
            _ctx.Tags.Write("MX201_MODE", 2);
            _ctx.Tags.Write("PID_201_SP", 22.0);
            _ctx.Tags.Write("PID_201_PV", Math.Round(Mixer.Temperature, 1));
            _ctx.Tags.Write("PID_201_OP", 0.0);
        }

        private void RunTestCycle(DateTime simNow)
        {
            TimeSpan elapsed = simNow - _cycleEntered;

            switch (_cycle)
            {
                case CycleState.Idle:
                    if (elapsed.TotalSeconds >= 20)
                    {
                        _pasteDosedKg = 0;
                        _waterDosedL = 0;
                        _vinegarDosedL = 0;
                        Transition(CycleState.FillingWater, simNow);
                    }
                    break;

                case CycleState.FillingWater:
                    WaterInlet.Open();
                    if (_waterDosedL >= WaterTargetL)
                    {
                        WaterInlet.Close();
                        Transition(CycleState.DosingPaste, simNow);
                    }
                    break;

                case CycleState.DosingPaste:
                    PasteInlet.Open();
                    if (_pasteDosedKg >= PasteTargetKg)
                    {
                        PasteInlet.Close();
                        Transition(CycleState.DosingVinegar, simNow);
                    }
                    break;

                case CycleState.DosingVinegar:
                    VinegarInlet.Open();
                    if (_vinegarDosedL >= VinegarTargetL)
                    {
                        VinegarInlet.Close();
                        Transition(CycleState.RampingAgitator, simNow);
                    }
                    break;

                case CycleState.RampingAgitator:
                    Agitator.SpeedSetpoint = 1500;
                    if (Agitator.State != EquipmentState.Running && Agitator.State != EquipmentState.Starting)
                    {
                        Agitator.Start();
                    }
                    if (Agitator.Speed >= 1450)
                    {
                        Transition(CycleState.Mixing, simNow);
                    }
                    break;

                case CycleState.Mixing:
                    if (elapsed.TotalMinutes >= 3)
                    {
                        Transition(CycleState.RampingDown, simNow);
                    }
                    break;

                case CycleState.RampingDown:
                    Agitator.SpeedSetpoint = 0;
                    if (Agitator.Speed < 50)
                    {
                        Agitator.Stop();
                        Discharge.Open();
                        Transition(CycleState.Discharging, simNow);
                    }
                    break;

                case CycleState.Discharging:
                    // Stay in Discharging until Area 300 has drained us
                    // (Area 300 pulls from us via DrawForTransfer during its Fill phase)
                    if (Mixer.Level < 1.0)
                    {
                        Discharge.Close();
                        Transition(CycleState.Idle, simNow);
                    }
                    break;
            }
        }

        private void Transition(CycleState next, DateTime simNow)
        {
            Console.WriteLine($"[{simNow:HH:mm:ss}] Area 200 Pre-Mix: {_cycle} -> {next} (mixer={Mixer.Level:F0}L)");
            _cycle = next;
            _cycleEntered = simNow;
        }
    }
}
