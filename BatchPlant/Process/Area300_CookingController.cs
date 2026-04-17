using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 300 - Cooking / Pasteurization (Honeywell Experion DCS, ~2005).
    ///
    /// Jacketed steam kettle KT-301 with scraped-surface agitator. PID-controlled steam
    /// valve TCV-301 modulates jacket heating to reach 90°C setpoint and hold for
    /// pasteurization.
    ///
    /// Phase 3 changes:
    ///   - Filling phase now pulls product from Area 200 via IProductSource (no fake fill).
    ///   - Discharging phase acts as an IProductSource for Area 400.
    ///   - Waits for upstream/downstream handshakes between batches.
    ///
    /// Cross-area coupling: draws steam from Area 800 via ctx.Utilities.
    /// </summary>
    public class Area300_CookingController : ISimulatable, IProductSource
    {
        private readonly PlantContext _ctx;
        private readonly Area800_UtilitiesController _utilities;
        private readonly IProductSource _upstream;

        public Vessel Kettle { get; }
        public Motor Agitator { get; }
        public Valve SteamValve { get; }
        public Valve SteamIsolation { get; }

        private readonly PidController _tempPid;

        public int Priority => 40;

        // Process constants
        private const double TargetTempC = 90.0;
        private const double MinHoldTempC = 88.0;
        private const double HoldTimeSec = 300;
        private const double MaxSteamFlowKgH = 300;
        private const double LatentHeatKjPerKg = 2163;
        private const double HeatTransferEfficiency = 0.75;
        private const double FillRateLpm = 250;
        private const double MinFillLevelL = 1500;  // minimum product to proceed to heating

        private enum CycleState
        {
            Idle = 0,
            Filling = 1,
            HeatingUp = 2,
            Holding = 3,
            Cooling = 4,
            Discharging = 5
        }

        private CycleState _cycle = CycleState.Idle;
        private DateTime _cycleEntered;
        private bool _initialized;
        private double _holdTimer;
        private double _steamFlowKgH;
        private DateTime _lastUpstreamDrawTime;

        public Area300_CookingController(PlantContext ctx, Area800_UtilitiesController utilities, IProductSource upstream)
        {
            _ctx = ctx;
            _utilities = utilities;
            _upstream = upstream;

            Kettle = new Vessel(ctx, "KT-301")
            {
                Capacity = 3300,
                Temperature = 22.0,
                AmbientTemp = 22.0,
                Density = 1.08,
                HeatLossCoefficient = 0.000005,
                IsJacketed = false
            }.Bind(new VesselTagMap
            {
                Level = "LIT-301",
                LevelPct = "LIT-301_PCT",
                Temperature = "TIT-301",
                Pressure = "PIT-301"
            });

            Agitator = new Motor(ctx, "AGIT-301")
            {
                MaxSpeed = 60,
                RatedPower = 15,
                RatedCurrent = 30,
                RampRate = 10,
                BaseVibration = 0.4,
                AmbientTemp = 22.0
            }.Bind(new MotorTagMap
            {
                Speed = "AGIT301_SPD",
                Current = "AGIT301_AMPS",
                Run = "AGIT301_RUN"
            });

            SteamValve = new Valve(ctx, "TCV-301") { IsOnOff = false, TravelTimeSeconds = 3.0 }
                .Bind(new ValveTagMap { Position = "TCV-301" });

            SteamIsolation = new Valve(ctx, "SV301") { IsOnOff = true, TravelTimeSeconds = 2.0 }
                .Bind(new ValveTagMap { Open = "SV301" });

            _tempPid = new PidController(kp: 5.0, ki: 0.2, kd: 0.1)
            {
                OutputMin = 0,
                OutputMax = 100,
                IntegralWindupLimit = 50,
                Setpoint = TargetTempC
            };
        }

        // ---- IProductSource implementation - available only during Discharging ----

        public bool HasProductAvailable => _cycle == CycleState.Discharging && Kettle.Level > 0.5;
        public double ProductTempC => Kettle.Temperature;

        public double DrawForTransfer(double litersRequested)
        {
            if (!HasProductAvailable) return 0;
            return Kettle.RemoveMaterial(litersRequested);
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            if (!_initialized)
            {
                _cycleEntered = simNow;
                _initialized = true;
            }

            // 1. Run state machine
            RunTestCycle(simNow, dt);

            // 2. During Filling, pull from Area 200 upstream
            double dtSec = dt.TotalSeconds;
            if (_cycle == CycleState.Filling && _upstream.HasProductAvailable)
            {
                double desiredL = FillRateLpm * dtSec / 60.0;
                double actualL = _upstream.DrawForTransfer(desiredL);
                if (actualL > 0)
                {
                    Kettle.AddMaterial(actualL, _upstream.ProductTempC);
                    _lastUpstreamDrawTime = simNow;
                }
            }

            // 3. PID temperature control during HeatingUp and Holding
            bool pidActive = _cycle == CycleState.HeatingUp || _cycle == CycleState.Holding;
            if (pidActive)
            {
                _tempPid.AutoMode = true;
                _tempPid.ProcessVariable = Kettle.Temperature;
                double pidOut = _tempPid.Update(dt.TotalSeconds);
                SteamValve.SetPosition(pidOut);
            }
            else
            {
                _tempPid.AutoMode = false;
                _tempPid.Reset();
                SteamValve.SetPosition(0);
            }

            // 4. Steam flow from valve position × header pressure
            double valveFrac = SteamValve.ActualPosition / 100.0;
            double pressureFactor = _utilities.SteamHeaderPressureBar / 6.0;
            _steamFlowKgH = MaxSteamFlowKgH * valveFrac * pressureFactor;

            if (SteamIsolation.IsOpen)
            {
                _utilities.RequestSteam("KT-301", _steamFlowKgH);
            }
            else
            {
                _utilities.RequestSteam("KT-301", 0);
                _steamFlowKgH = 0;
            }

            // 5. Apply heat input to kettle
            double heatKw = (_steamFlowKgH * LatentHeatKjPerKg / 3600.0) * HeatTransferEfficiency;
            Kettle.JacketTemp = _utilities.SteamHeaderTempC;

            if (Kettle.Level > 10 && SteamIsolation.IsOpen)
            {
                Kettle.ExternalHeatInputKw = heatKw;
            }

            // 6. Cooling phase
            if (_cycle == CycleState.Cooling)
            {
                _utilities.RequestCooling("KT-301", 15.0);
                Kettle.ExternalHeatInputKw = -12.0;
            }
            else
            {
                _utilities.RequestCooling("KT-301", 0);
            }

            // 7. Agitator load
            double fillFrac = Math.Min(1.0, Kettle.Level / Kettle.Capacity);
            Agitator.LoadFactor = 0.2 + fillFrac * 0.7;

            // 8. Tick equipment
            Kettle.Tick(dt, simNow);
            Agitator.Tick(dt, simNow);
            SteamValve.Tick(dt, simNow);
            SteamIsolation.Tick(dt, simNow);

            // 9. Publish additional Area 300 tags
            var t = _ctx.Tags;
            double bottomTemp = Kettle.Temperature - 0.5;
            t.Write("TIT-302", Math.Round(_ctx.AddNoise(bottomTemp, 0.2), 1));
            double jktPress = _utilities.SteamHeaderPressureBar * valveFrac;
            t.Write("PT-301_JKT", Math.Round(_ctx.AddNoise(jktPress, 0.05), 2));
            t.Write("FIT-302", Math.Round(_steamFlowKgH, 1));

            if (Kettle.Level > 100)
            {
                double ph = 3.72 + Math.Sin(simNow.Ticks * 1e-10) * 0.03;
                t.Write("AIT-301", Math.Round(_ctx.AddNoise(ph, 0.01), 2));
            }
            else
            {
                t.Write("AIT-301", 0.0);
            }

            t.Write("PID_301_SP", Math.Round(_tempPid.Setpoint, 1));
            t.Write("PID_301_PV", Math.Round(_tempPid.ProcessVariable, 1));
            t.Write("PID_301_OP", Math.Round(_tempPid.Output, 1));
            t.Write("PID_301_MODE", _tempPid.AutoMode ? 1 : 0);
            t.Write("KT301_STATE", (int)_cycle);
            t.Write("KT301_HOLD_TMR", Math.Round(_holdTimer, 0));
        }

        private void RunTestCycle(DateTime simNow, TimeSpan dt)
        {
            TimeSpan elapsed = simNow - _cycleEntered;

            switch (_cycle)
            {
                case CycleState.Idle:
                    // Ready to receive a new batch - transition to Filling as soon as upstream has product
                    if (elapsed.TotalSeconds >= 15 && _upstream.HasProductAvailable)
                    {
                        Transition(CycleState.Filling, simNow);
                    }
                    break;

                case CycleState.Filling:
                    // Transition to heating when upstream is drained AND we have enough product
                    // (upstream going not-available means it's empty or finished discharging)
                    bool upstreamDone = !_upstream.HasProductAvailable;
                    bool upstreamIdle = (simNow - _lastUpstreamDrawTime).TotalSeconds > 5;
                    if (Kettle.Level >= MinFillLevelL && upstreamDone && upstreamIdle)
                    {
                        Transition(CycleState.HeatingUp, simNow);
                    }
                    break;

                case CycleState.HeatingUp:
                    SteamIsolation.Open();
                    _tempPid.Setpoint = TargetTempC;
                    if (Kettle.Temperature >= MinHoldTempC)
                    {
                        _holdTimer = 0;
                        Transition(CycleState.Holding, simNow);
                    }
                    break;

                case CycleState.Holding:
                    if (Kettle.Temperature >= MinHoldTempC)
                    {
                        _holdTimer += dt.TotalSeconds;
                    }
                    if (_holdTimer >= HoldTimeSec)
                    {
                        SteamIsolation.Close();
                        Transition(CycleState.Cooling, simNow);
                    }
                    break;

                case CycleState.Cooling:
                    if (Kettle.Temperature <= 85.0)
                    {
                        Transition(CycleState.Discharging, simNow);
                    }
                    break;

                case CycleState.Discharging:
                    // Stay in Discharging until Area 400 has drained us
                    if (Kettle.Level < 1.0)
                    {
                        Transition(CycleState.Idle, simNow);
                    }
                    break;
            }
        }

        private void Transition(CycleState next, DateTime simNow)
        {
            Console.WriteLine($"[{simNow:HH:mm:ss}] Area 300 Cook: {_cycle} -> {next} " +
                              $"(kettle={Kettle.Level:F0}L, T={Kettle.Temperature:F1}C)");
            _cycle = next;
            _cycleEntered = simNow;
        }
    }
}
