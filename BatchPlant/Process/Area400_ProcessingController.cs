using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 400 - Deaeration and Homogenization (2012 upgrade).
    ///
    /// Phase 3 flow:
    ///   - Filling phase: pulls cooked product from Area 300 (IProductSource)
    ///   - PullingVacuum / Deaerating: closed vessel under vacuum, no mass transfer
    ///   - Homogenizing phase: pumps product out through the high-pressure homogenizer
    ///     into whichever Area 450 buffer tank is accepting material
    /// </summary>
    public class Area400_ProcessingController : ISimulatable
    {
        private readonly PlantContext _ctx;
        private readonly IProductSource _upstream;
        private readonly Area450_BufferTanksController _buffers;

        public Vessel Deaerator { get; }
        public VacuumSystem VacuumPump { get; }
        public Pump TransferPump { get; }
        public Homogenizer HomogenizerUnit { get; }

        public int Priority => 40;

        private const double FillRateLpm = 250;
        private const double VacuumTargetMmHg = 700;
        private const double DeaerationHoldSec = 180;
        private const double HomogTargetBar = 200;
        private const double HomogFlowLpm = 80;
        private const double MinFillLevelL = 1400;

        private enum CycleState
        {
            Idle = 0,
            Filling = 1,
            PullingVacuum = 2,
            Deaerating = 3,
            Homogenizing = 4,
            VentingVacuum = 5,
            Draining = 6
        }

        private CycleState _cycle = CycleState.Idle;
        private DateTime _cycleEntered;
        private bool _initialized;
        private double _deaerationTimer;
        private DateTime _lastUpstreamDrawTime;

        public Area400_ProcessingController(PlantContext ctx, IProductSource upstream, Area450_BufferTanksController buffers)
        {
            _ctx = ctx;
            _upstream = upstream;
            _buffers = buffers;

            Deaerator = new Vessel(ctx, "DA-401")
            {
                Capacity = 2500,
                Temperature = 22.0,
                AmbientTemp = 22.0,
                Density = 1.08,
                HeatLossCoefficient = 0.00001,
                IsJacketed = false
            }.Bind(new VesselTagMap
            {
                Level = "401-LI-001",
                Temperature = "401-TI-001"
            });

            VacuumPump = new VacuumSystem(ctx, "401-VP-001")
            {
                VacuumSetpoint = VacuumTargetMmHg,
                MaxVacuum = 740,
                PumpDownRate = 20,
                LeakRate = 1.5,
                RatedCurrent = 18
            }.Bind(new VacuumSystemTagMap
            {
                VacuumLevel = "401-PI-001",
                Run = "401-VP-001_RUN",
                Fault = "401-VP-001_FAULT",
                Current = "401-VP-001_AMPS",
                Vibration = "401-VP-001_VIB",
                BearingTemp = "401-VP-001_BRGTEMP",
                Mcsa = "401-VP-001_MCSA",
                FailureCount = "401-VP-001_FAILCNT"
            });

            TransferPump = new Pump(ctx, "401-P-001")
            {
                NominalFlow = HomogFlowLpm,
                NominalPressure = 3.5,
                RatedCurrent = 12,
                IsPositiveDisplacement = false
            }.Bind(new PumpTagMap
            {
                Flow = "401-P-001_FLOW",
                Status = "401-P-001_STS",
                Fault = "401-P-001_FAULT",
                FailureCount = "401-P-001_FAILCNT",
                Vibration = "401-P-001_VIB",
                BearingTemp = "401-P-001_BRGTEMP",
                Mcsa = "401-P-001_MCSA"
            });

            HomogenizerUnit = new Homogenizer(ctx, "HG-401")
            {
                PressureSetpoint = HomogTargetBar,
                MaxPressure = 250,
                Stage2Fraction = 0.25,
                NominalFlow = HomogFlowLpm,
                RatedCurrent = 65,
                ValveWearRate = 0.015
            }.Bind(new HomogenizerTagMap
            {
                Pressure = "HOMOG_401_PRESS",
                Pressure2 = "HOMOG_401_PRESS2",
                Current = "HOMOG_401_AMPS",
                Run = "HOMOG_401_RUN",
                Fault = "HOMOG_401_FAULT",
                ValveHours = "HOMOG_401_VLV_HRS",
                Vibration = "HOMOG_401_VIB",
                BearingTemp = "HOMOG_401_BRGTEMP",
                Mcsa = "HOMOG_401_MCSA",
                FailureCount = "HOMOG_401_FAILCNT",
                HydPressure = "HOMOG_401_HYD_PRESS",
                HydTemp = "HOMOG_401_HYD_TEMP",
                FilterDp = "HOMOG_401_FILTER_DP",
                OilQuality = "HOMOG_401_OIL_QUAL"
            });
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            if (!_initialized)
            {
                _cycleEntered = simNow;
                _initialized = true;
            }

            RunTestCycle(simNow, dt);

            double dtSec = dt.TotalSeconds;
            double inflowLpm = 0;
            double outflowLpm = 0;

            // --- Filling: pull from Area 300 ---
            if (_cycle == CycleState.Filling && _upstream.HasProductAvailable)
            {
                double desired = FillRateLpm * dtSec / 60.0;
                double actual = _upstream.DrawForTransfer(desired);
                if (actual > 0)
                {
                    Deaerator.AddMaterial(actual, _upstream.ProductTempC);
                    _lastUpstreamDrawTime = simNow;
                    inflowLpm = FillRateLpm;
                }
            }

            // --- Homogenizing: push to Area 450 buffer tanks ---
            if (_cycle == CycleState.Homogenizing && Deaerator.Level > 10)
            {
                double desired = HomogFlowLpm * dtSec / 60.0;
                // Tag the buffered product with the batch currently in DA-401 (published by
                // BatchManager) so the buffer tank carries the bulk-batch genealogy forward.
                string batchId = _ctx.Tags.Read<string>("DA401_BATCH_ID") ?? "";
                double accepted = _buffers.AcceptProduct(desired, Deaerator.Temperature, batchId);
                if (accepted > 0)
                {
                    Deaerator.RemoveMaterial(accepted);
                    outflowLpm = HomogFlowLpm;
                }
            }
            else if (_cycle == CycleState.Draining && Deaerator.Level > 0)
            {
                // Drain any residue directly (lost - not routed to buffers)
                double volume = 60.0 * dtSec / 60.0;
                Deaerator.RemoveMaterial(volume);
            }

            // Tick equipment
            Deaerator.Tick(dt, simNow);
            VacuumPump.Tick(dt, simNow);
            TransferPump.Tick(dt, simNow);
            HomogenizerUnit.Tick(dt, simNow);

            _ctx.Tags.Write("401-FI-001", Math.Round(inflowLpm + outflowLpm, 1));
            _ctx.Tags.Write("DA401_STATE", (int)_cycle);
        }

        private void RunTestCycle(DateTime simNow, TimeSpan dt)
        {
            TimeSpan elapsed = simNow - _cycleEntered;

            switch (_cycle)
            {
                case CycleState.Idle:
                    if (elapsed.TotalSeconds >= 15 && _upstream.HasProductAvailable)
                    {
                        Transition(CycleState.Filling, simNow);
                    }
                    break;

                case CycleState.Filling:
                    bool upstreamDone = !_upstream.HasProductAvailable;
                    bool upstreamIdle = (simNow - _lastUpstreamDrawTime).TotalSeconds > 5;
                    if (Deaerator.Level >= MinFillLevelL && upstreamDone && upstreamIdle)
                    {
                        Transition(CycleState.PullingVacuum, simNow);
                    }
                    break;

                case CycleState.PullingVacuum:
                    if (VacuumPump.State != EquipmentState.Running &&
                        VacuumPump.State != EquipmentState.Starting)
                    {
                        VacuumPump.Start();
                    }
                    if (VacuumPump.VacuumLevel >= VacuumTargetMmHg * 0.98)
                    {
                        _deaerationTimer = 0;
                        Transition(CycleState.Deaerating, simNow);
                    }
                    break;

                case CycleState.Deaerating:
                    if (VacuumPump.VacuumLevel >= VacuumTargetMmHg * 0.95)
                    {
                        _deaerationTimer += dt.TotalSeconds;
                    }
                    if (_deaerationTimer >= DeaerationHoldSec)
                    {
                        Transition(CycleState.Homogenizing, simNow);
                    }
                    break;

                case CycleState.Homogenizing:
                    if (HomogenizerUnit.State != EquipmentState.Running &&
                        HomogenizerUnit.State != EquipmentState.Starting)
                    {
                        HomogenizerUnit.Start();
                    }
                    if (HomogenizerUnit.State == EquipmentState.Running &&
                        TransferPump.State != EquipmentState.Running &&
                        TransferPump.State != EquipmentState.Starting)
                    {
                        TransferPump.Start();
                    }
                    if (Deaerator.Level < 20)
                    {
                        TransferPump.Stop();
                        HomogenizerUnit.Stop();
                        Transition(CycleState.VentingVacuum, simNow);
                    }
                    break;

                case CycleState.VentingVacuum:
                    VacuumPump.Stop();
                    if (VacuumPump.VacuumLevel < 50)
                    {
                        Transition(CycleState.Draining, simNow);
                    }
                    break;

                case CycleState.Draining:
                    if (Deaerator.Level < 1.0)
                    {
                        Transition(CycleState.Idle, simNow);
                    }
                    break;
            }
        }

        private void Transition(CycleState next, DateTime simNow)
        {
            Console.WriteLine($"[{simNow:HH:mm:ss}] Area 400 Process: {_cycle} -> {next} " +
                              $"(DA-401={Deaerator.Level:F0}L, vac={VacuumPump.VacuumLevel:F0}mmHg, homog={HomogenizerUnit.Pressure:F0}bar)");
            _cycle = next;
            _cycleEntered = simNow;
        }
    }
}
