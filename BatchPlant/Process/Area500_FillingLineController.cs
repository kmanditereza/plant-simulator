using BatchPlant.Api;
using BatchPlant.Core;
using BatchPlant.Equipment;
using BatchPlant.OpcUa;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 500 - Filling Line (Krones OEM, ~2018).
    ///
    /// Hot-fill 12-head rotary piston filler running at 200 BPM nominal. Downstream stations:
    /// capper → inversion tunnel (10s) → cooling tunnel (20 min, 85C → 35C across 3 zones) → conveyor.
    ///
    /// Unlike the batch areas, this line runs CONTINUOUSLY. It draws product from Area 450
    /// buffer tanks as long as hot product is available, and stops when starved.
    ///
    /// PackML state logic:
    ///   - Mode = Production (set once at startup)
    ///   - State = Running      when line is actively producing
    ///   - State = Starved      when buffer tanks have no hot product
    ///   - State = Blocked      when downstream Area 600 can't receive (set via SetDownstreamBlocked)
    ///   - State = UnplannedDT  when equipment is faulted
    /// </summary>
    public class Area500_FillingLineController : ISimulatable
    {
        private readonly PlantContext _ctx;
        private readonly Area450_BufferTanksController _buffers;
        private readonly PackagingOrderQueue? _packagingQueue;
        private ApiServer? _apiServer;

        // --- Active packaging order (gates the line) ---
        private PackagingInstruction? _order;
        private int _orderBottlesProduced;   // all bottles filled (good + rejected)
        private int _orderGoodBottles;       // good bottles only -- drives ORDER_QTY_GOOD
        private long _lastGoodCount;         // Filler.GoodCount at the previous tick, for the delta
        private double _orderRuntimeMin;     // plant (sim) minutes this order has actually been Running
        private DateTime? _orderStarted;
        // Material genealogy: bottles produced per bulk batch drawn from the buffer.
        private readonly Dictionary<string, double> _bottlesByBatch = new();

        // Equipment
        public PistonFiller Filler { get; }

        public int Priority => 40;

        // --- Line configuration ---
        private const double TargetBpm = 200.0;
        private const double BottleWeightG = 397.0;
        private const double ProductDensityKgL = 1.08;
        private const double InversionTunnelSec = 10.0;

        // --- Micro-stops: brief stalls (jam clearance, bottle fall) that occur INSIDE run time.
        // The line stays Running, so the lost output shows up as an OEE Performance loss, not
        // Availability. Breakdowns are the opposite: they stop the line and hit Availability.
        private const double MicroStopMtbfRunHours = 0.1;   // ~1 micro-stop per 6 plant-min of running
        private const double MicroStopMinSec = 10.0;        // plant seconds
        private const double MicroStopMaxSec = 40.0;

        // --- Starvation hysteresis (Schmitt trigger) ---
        // A single threshold makes the line chatter Running<->Starved when hot product hovers at
        // the limit, which in turn produces a storm of downtime events. Require a healthy amount
        // of product to (re)start, but only declare starvation once it has genuinely run out.
        private const double StarveResumeL = 150.0;   // need this much hot product to start filling
        private const double StarveTripL = 20.0;      // starve only once it falls below this
        private bool _productAvailable;

        // --- Line state ---
        private OperationalState _lineState = OperationalState.Idle;
        private OperationalMode _lineMode = OperationalMode.Production;
        private DateTime _stateEntered;
        private DateTime _modeEntered;
        private DateTime _lineStarted;
        private double _productTempC = 85.0;
        private double _lineSpeed;

        // Downstream feedback (set by Area 600 each tick)
        private bool _downstreamBlocked;
        public void SetDownstreamBlocked(bool blocked) => _downstreamBlocked = blocked;

        // Bottles-per-tick output stream consumed by Area 600
        public int BottlesProducedThisTick { get; private set; }

        // Tunnel temperature simulation state
        private double _invZone1Temp = 22;
        private double _invZone2Temp = 22;
        private double _coolZoneInTemp = 22;
        private double _coolZoneMidTemp = 22;
        private double _coolZoneOutTemp = 22;

        // Capper state (simple - no Equipment subclass)
        private long _capsApplied;

        // Condition monitoring for the capper head drive and discharge conveyor
        private readonly ConditionMonitor _cmCapper;
        private readonly ConditionMonitor _cmConveyor;

        // Stoppages
        private bool _microStopActive;          // true on the ticks a micro-stop is stalling output
        private double _microStopSecRemaining;   // plant seconds left in the current micro-stop
        private int _microStopsToday;
        private int _majorStopsToday;
        private bool _prevFaulted;
        private DateTime _counterDate;

        // Shift-local and daily counters for PackLine aggregations
        private int _bottlesTodayCounter;
        private int _bottlesThisShift;
        private int _lastShift;
        private bool _shiftInit;
        private double _runtimeMinShift;        // wall minutes -- shift KPI tags (90-min shift window)
        private double _downtimeMinShift;       // wall minutes
        private double _runtimePlantMinShift;   // plant minutes -- OEE performance denominator

        private bool _initialized;

        public Area500_FillingLineController(PlantContext ctx, Area450_BufferTanksController buffers,
                                             PackagingOrderQueue? packagingQueue = null)
        {
            _ctx = ctx;
            _buffers = buffers;
            _packagingQueue = packagingQueue;

            _cmCapper = new ConditionMonitor(ctx) { BaseVibration = 1.6, BearingRise = 30 };
            _cmCapper.SeedHours(250);
            _cmConveyor = new ConditionMonitor(ctx) { BaseVibration = 1.2, BearingRise = 25 };
            _cmConveyor.SeedHours(500);

            // FL-501: 12-head rotary piston filler
            Filler = new PistonFiller(ctx, "FL-501")
            {
                TargetWeight = BottleWeightG,
                BaseStdDev = 2.5,
                WearDegradation = 0.4,
                UnderweightLimit = 388,
                OverweightLimit = 406,
                MinFillTemp = 82.0,
                FillRate = TargetBpm,

                // Random breakdowns (Availability loss). Scaled to how little the line actually
                // runs: a packaging order is only ~20-40 plant-minutes of run time, so an MTBF of
                // 0.5 run-hours gives roughly one breakdown per order.
                MtbfRunHours = 0.5,
                MttrMinutesMin = 3.0,
                MttrMinutesMax = 10.0,

                // Wear-driven speed loss (Performance loss): actual cycle time drifts from ideal.
                // Accumulates across orders and only resets when the asset is serviced.
                SpeedLossPctPerRunHour = 5.0,
                MaxSpeedLossPct = 15.0,

                // Condition-based trip: vibration climbs with wear until it forces a stoppage.
                VibrationTripLimit = 3.2
            }.Bind(new PistonFillerTagMap
            {
                CurrentWeight = "FL_501_WEIGHT",
                Rate = "FL_501_FILL_SPD",
                GoodCount = "FL_501_GOOD_CNT",
                RejectCount = "FL_501_REJECT_CNT",
                TotalCount = "FL_501_TOTAL_CNT",
                WeightAvg = "FL_501_WT_AVG",
                WeightStdDev = "FL_501_WT_STDDEV",
                Yield = "FL_501_YIELD",
                Status = "FL_501_STATUS",
                Fault = "FL_501_FAULT",
                FaultCode = "FL_501_FAULT_CODE",
                FailureCount = "FL_501_FAILCNT",
                Vibration = "FL_501_VIB",
                BearingTemp = "FL_501_BRGTEMP",
                Mcsa = "FL_501_MCSA",
                SpeedLossPct = "FL_501_SPEED_LOSS_PCT"
            });

            // Wear reaches the 3.2 mm/s vibration trip after ~0.7 run-hours, i.e. within a long
            // packaging order. Vibration = BaseVibration * load * (1 + runHrs/WearHours).
            Filler.Condition.WearHours = 0.6;
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            if (!_initialized)
            {
                _stateEntered = simNow;
                _modeEntered = simNow;
                _lineStarted = simNow;
                _initialized = true;
            }

            // Reset the daily counters on sim-date rollover.
            if (simNow.Date != _counterDate.Date)
            {
                _counterDate = simNow.Date;
                _bottlesTodayCounter = 0;
                _microStopsToday = 0;
                _majorStopsToday = 0;
            }

            // Reset the per-shift counters at each shift change (incl. a SHIFT_RESET).
            int shift = _ctx.Clock.CurrentShift;
            if (!_shiftInit || shift != _lastShift)
            {
                _shiftInit = true;
                _lastShift = shift;
                _bottlesThisShift = 0;
                _runtimeMinShift = 0;
                _downtimeMinShift = 0;
                _runtimePlantMinShift = 0;
            }

            // Count breakdowns on the rising edge, and restart the filler once its repair completes
            // (ClearFault leaves equipment Off, so without this a single fault would kill the line).
            if (Filler.IsFaulted && !_prevFaulted)
            {
                _majorStopsToday++;
                Console.WriteLine($"[{simNow:HH:mm:ss}] Area 500: FL-501 FAULT '{Filler.FaultCode}' - line down");
            }
            _prevFaulted = Filler.IsFaulted;

            // 0. Gate on a packaging order. The pack line no longer runs continuously —
            //    it draws a dequeued packaging order to completion, then idles.
            if (_order == null && _packagingQueue != null)
            {
                var next = _packagingQueue.TryDequeue();
                if (next != null)
                {
                    _order = next;
                    _order.StartedAt = simNow;
                    _orderStarted = simNow;
                    _orderBottlesProduced = 0;
                    _orderGoodBottles = 0;
                    _orderRuntimeMin = 0;
                    _lastGoodCount = Filler.GoodCount;   // baseline: only count good bottles from here on
                    _bottlesByBatch.Clear();
                    Console.WriteLine($"[{simNow:HH:mm:ss}] Area 500: Starting packaging order {_order.PackagingOrderNumber} " +
                                      $"({_order.TargetCases} cases x {_order.BottlesPerCase} of {_order.Sku})");
                }
            }

            // 1. Determine what the line SHOULD be doing based on conditions.
            // Hysteresis: once running, keep going until the buffer is genuinely empty; once
            // starved, wait for a real refill rather than restarting on a few stray litres.
            double hotProductAvailableL = _buffers.TotalHotProductL();
            _productAvailable = _productAvailable
                ? hotProductAvailableL > StarveTripL
                : hotProductAvailableL >= StarveResumeL;
            bool productAvailable = _productAvailable;

            OperationalState desiredState;
            if (_order == null)
            {
                desiredState = OperationalState.Idle;   // no packaging order — line idle
            }
            else if (Filler.IsFaulted)
            {
                desiredState = OperationalState.UnplannedDowntime;
            }
            else if (!productAvailable)
            {
                desiredState = OperationalState.Starved;
            }
            else if (_downstreamBlocked)
            {
                desiredState = OperationalState.Blocked;
            }
            else
            {
                desiredState = OperationalState.Running;
            }

            // Transition state if it changed
            if (desiredState != _lineState)
            {
                Console.WriteLine($"[{simNow:HH:mm:ss}] Area 500 Line: {_lineState} -> {desiredState} " +
                                  $"(hot product = {hotProductAvailableL:F0}L)");
                _lineState = desiredState;
                _stateEntered = simNow;
            }

            // Keep the filler's equipment state in step with the line. It must only be Running while
            // the line actually produces, otherwise it accrues run hours, wear and failure hazard
            // while idle or starved -- and would break down without ever having made a bottle.
            // (A micro-stop deliberately leaves the filler Running: the stall is inside run time.)
            if (!Filler.IsFaulted)
            {
                bool producing = _lineState == OperationalState.Running;
                if (producing && (Filler.State == EquipmentState.Off || Filler.State == EquipmentState.Stopping))
                {
                    Filler.Start();
                }
                else if (!producing && (Filler.State == EquipmentState.Running || Filler.State == EquipmentState.Starting))
                {
                    Filler.Stop();
                }
            }

            // 2. If Running, produce bottles
            BottlesProducedThisTick = 0;
            double dtMin = dt.TotalMinutes;                                  // sim minutes: drives process physics
            double dtWallMin = _ctx.Clock.SimToWall(dt).TotalMinutes;        // real minutes: drives the shift-scoped counters

            if (_lineState == OperationalState.Running)
            {
                // Micro-stop: a brief stall inside run time. The line stays Running, so run time
                // keeps accruing while output does not -- which is exactly how a micro-stop shows
                // up as an OEE Performance loss rather than an Availability loss.
                if (_microStopSecRemaining > 0)
                {
                    _microStopSecRemaining -= dt.TotalSeconds;
                }
                else if (_ctx.Rng.NextDouble() < dt.TotalHours / MicroStopMtbfRunHours)
                {
                    _microStopSecRemaining = MicroStopMinSec + _ctx.Rng.NextDouble() * (MicroStopMaxSec - MicroStopMinSec);
                    _microStopsToday++;
                    Console.WriteLine($"[{simNow:HH:mm:ss}] Area 500: micro-stop ({_microStopSecRemaining:F0}s)");
                }
                bool stalled = _microStopSecRemaining > 0;
                _microStopActive = stalled;

                if (!stalled)
                {
                    // Draw product from ONE buffer tank (one bulk batch at a time) and record
                    // which batch it came from for the packaging order's material genealogy.
                    double consumptionLpm = TargetBpm * BottleWeightG / ProductDensityKgL / 1000.0;
                    double drawVolume = consumptionLpm * dtMin;
                    var (_, drawnBatchId) = _buffers.DrawFromOneBatch(drawVolume);

                    // Update product temperature from buffer average (accounting for slight cooling during transfer)
                    _productTempC = Math.Max(82, _buffers.AverageProductTempC() - 1.5);
                    Filler.ProductTemp = _productTempC;

                    // Fill bottles via the PistonFiller equipment class
                    int filled = Filler.FillBottles(dt);
                    BottlesProducedThisTick = filled;
                    _capsApplied += filled;
                    _bottlesTodayCounter += filled;
                    _bottlesThisShift += filled;

                    // Packaging-order counters + genealogy attribution.
                    // Filler.FillBottles() returns ALL bottles filled (good + rejected), so the good
                    // count is taken from the filler's own good tally as a per-tick delta.
                    long goodNow = Filler.GoodCount;
                    _orderGoodBottles += (int)Math.Max(0, goodNow - _lastGoodCount);
                    _lastGoodCount = goodNow;

                    _orderBottlesProduced += filled;
                    if (filled > 0 && !string.IsNullOrEmpty(drawnBatchId))
                    {
                        _bottlesByBatch.TryGetValue(drawnBatchId, out var cur);
                        _bottlesByBatch[drawnBatchId] = cur + filled;
                    }

                    _lineSpeed = Filler.ActualRate;
                }
                else
                {
                    _lineSpeed = 0;   // stalled: no output, but still inside run time
                }

                _runtimeMinShift += dtWallMin;      // shift KPIs are wall-clock (90-min shift window)
                _runtimePlantMinShift += dtMin;     // OEE performance: must match TargetBpm's plant-time base
                _orderRuntimeMin += dtMin;          // order ETA is plant time, matching PACK_LINE_SPEED (BPM)

                if (!stalled) CheckOrderCompletion(simNow);
            }
            else
            {
                _lineSpeed = 0;
                _microStopActive = false;
                // Only count downtime while an order is active (idle-with-no-order is not a loss).
                if (_order != null) _downtimeMinShift += dtWallMin;
            }

            // 3. Update tunnel temperature profiles
            UpdateTunnelTemperatures(dt, filling: _lineState == OperationalState.Running);

            // 4. Tick the filler equipment
            Filler.Tick(dt, simNow);

            // 5. Compute bottleneck (which station is currently the constraint)
            string bottleneck = _lineState switch
            {
                OperationalState.Starved => "Buffer Tanks (Area 450)",
                OperationalState.Blocked => "Packaging Line (Area 600)",
                OperationalState.UnplannedDowntime => "FL-501",
                OperationalState.Running => "FL-501", // filler is the pacing element in normal ops
                _ => ""
            };

            // 6. Publish Area 500 station tags
            var t = _ctx.Tags;

            // Filler head pressure (sanitary pressurized product supply, ~2.5 bar)
            t.Write("FILLER_HeadPress_01", Math.Round(_ctx.AddNoise(2.5, 0.05), 2));
            t.Write("FL_501_PROD_TEMP", Math.Round(_ctx.AddNoise(_productTempC, 0.1), 1));

            // Capper
            int capperStatus = Filler.IsFaulted ? 2 : (_lineState == OperationalState.Running ? 1 : 0);
            t.Write("CAP_501_STS", capperStatus);
            t.Write("CAP_501_JAM", false);
            t.Write("CAP_501_TORQUE", Math.Round(_ctx.AddNoise(20.0, 0.4), 2));
            t.Write("CAP_501_CNT", (int)_capsApplied);

            // Inversion tunnel (2 zones)
            t.Write("INV_TUNNEL_TEMP_1", Math.Round(_invZone1Temp, 1));
            t.Write("INV_TUNNEL_TEMP_2", Math.Round(_invZone2Temp, 1));
            t.Write("INV_TUNNEL_SPD", Math.Round(_lineSpeed / 20.0, 2)); // ~m/min
            t.Write("INV_TUNNEL_RUN", _lineState == OperationalState.Running);

            // Cooling tunnel (3 zones: in / mid / out)
            t.Write("COOL_TUN_TEMP_IN", Math.Round(_coolZoneInTemp, 1));
            t.Write("COOL_TUN_TEMP_MID", Math.Round(_coolZoneMidTemp, 1));
            t.Write("COOL_TUN_TEMP_OUT", Math.Round(_coolZoneOutTemp, 1));
            t.Write("COOL_TUN_SPRAY_FLOW", Math.Round(_lineState == OperationalState.Running ? _ctx.AddNoise(120.0, 2.0) : 0, 1));
            t.Write("COOL_TUN_SPRAY_TEMP", Math.Round(_ctx.AddNoise(8.0, 0.3), 1));
            t.Write("COOL_TUN_RUN", _lineState == OperationalState.Running);

            // Conveyor
            t.Write("CONV_501_SPD", Math.Round(_lineSpeed, 1));
            t.Write("CONV_501_RUN", _lineState == OperationalState.Running);
            t.Write("CONV_501_FAULT", false);

            // Condition monitoring for capper head drive and conveyor
            bool lineRunning = _lineState == OperationalState.Running;
            double packLoad = Math.Clamp(_lineSpeed / TargetBpm, 0, 1.2);
            _cmCapper.Update(lineRunning, packLoad, Filler.IsFaulted, dt.TotalSeconds);
            _cmCapper.Publish(t, vibration: "CAP_501_VIB", bearingTemp: "CAP_501_BRGTEMP", mcsa: "CAP_501_MCSA", failureCount: "CAP_501_FAILCNT");
            _cmConveyor.Update(lineRunning, packLoad, false, dt.TotalSeconds);
            _cmConveyor.Publish(t, vibration: "CONV_501_VIB", bearingTemp: "CONV_501_BRGTEMP", mcsa: "CONV_501_MCSA", failureCount: "CONV_501_FAILCNT");

            // PackML state/mode tags per station - for simplicity, all stations mirror the line state
            double stateDurSec = (simNow - _stateEntered).TotalSeconds;
            double modeDurSec = (simNow - _modeEntered).TotalSeconds;

            PublishStationPackML("FL_501", _lineState, _lineMode, stateDurSec, modeDurSec);
            PublishStationPackML("CAP_501", _lineState, _lineMode, stateDurSec, modeDurSec);
            PublishStationPackML("INV_TUNNEL", _lineState, _lineMode, stateDurSec, modeDurSec);
            PublishStationPackML("COOL_TUN", _lineState, _lineMode, stateDurSec, modeDurSec);
            PublishStationPackML("CONV_501", _lineState, _lineMode, stateDurSec, modeDurSec);

            // Publish PackLine/ aggregate tags
            t.Write("PACK_LINE_OP_STATE", (int)_lineState);
            t.Write("PACK_LINE_OP_STATE_TXT", _lineState.ToText());
            t.Write("PACK_LINE_OP_MODE", (int)_lineMode);
            t.Write("PACK_LINE_OP_MODE_TXT", _lineMode.ToText());
            t.Write("PACK_LINE_MODE_DUR", Math.Round(modeDurSec, 0));
            t.Write("PACK_LINE_BOTTLENECK", bottleneck);
            t.Write("PACK_LINE_SPEED", Math.Round(_lineSpeed, 1));
            t.Write("PACK_LINE_TARGET_SPEED", TargetBpm);
            t.Write("PACK_LINE_RUNTIME_MIN_SHIFT", Math.Round(_runtimeMinShift, 4));
            t.Write("PACK_LINE_DOWNTIME_MIN_SHIFT", Math.Round(_downtimeMinShift, 4));
            // Filler wear clock, plant minutes (RunHoursSinceService is in plant hours).
            t.Write("PACK_LINE_RUNTIME_SINCE_FAILURE_MIN", Math.Round(Filler.RunHoursSinceService * 60.0, 2));

            // OEE calculation (simplified). Availability and performance are scoped to the
            // current shift, so the bottle count in the performance ratio must be the
            // per-shift one -- pairing lifetime bottles with shift runtime would inflate it.
            //
            // Availability is a wall/wall ratio, so it uses the wall-clock counters.
            // Performance = actual output / theoretical output during run time. Bottles accrue
            // in PLANT time (TargetBpm is bottles per plant-minute), so the denominator must be
            // plant minutes too. Using the wall-clock runtime here would inflate the ratio by
            // SIM_SPEED (60x) and the Math.Min clamp would peg performance at 100% forever,
            // hiding every speed loss.
            double plannedMin = _runtimeMinShift + _downtimeMinShift;
            double availability = plannedMin > 0 ? _runtimeMinShift / plannedMin * 100.0 : 100.0;
            double performance = _runtimePlantMinShift > 0
                ? (_bottlesThisShift / (_runtimePlantMinShift * TargetBpm)) * 100.0 : 100.0;
            performance = Math.Min(100.0, performance);
            double quality = Filler.TotalCount > 0 ? Filler.FirstPassYield : 100.0;
            double oeeOverall = (availability / 100.0) * (performance / 100.0) * (quality / 100.0) * 100.0;

            t.Write("PACK_LINE_OEE_AVAIL", Math.Round(availability, 1));
            t.Write("PACK_LINE_OEE_PERF", Math.Round(performance, 1));
            t.Write("PACK_LINE_OEE_QUAL", Math.Round(quality, 1));
            t.Write("PACK_LINE_OEE_OVERALL", Math.Round(oeeOverall, 1));
            t.Write("BOTTLES_TODAY", _bottlesTodayCounter);
            t.Write("BOTTLES_SHIFT", _bottlesThisShift);

            // Stoppage counts: micro-stops sit inside run time (Performance loss); major stops are
            // breakdowns that take the line down (Availability loss).
            t.Write("PACK_LINE_MICROSTOPS_TODAY", _microStopsToday);
            t.Write("PACK_LINE_MAJOR_STOPS_TODAY", _majorStopsToday);

            // Single downtime flag + the one reason that explains the line's current state.
            // Precedence mirrors the state machine, so exactly one reason is reported at a time.
            // Idle (no order) is NOT downtime: nothing was scheduled, so nothing was lost.
            // A micro-stop is NOT downtime either: it sits inside run time and shows up in Performance.
            string reason;
            bool downtime;
            if (_order == null)                                     { reason = "Idle";         downtime = false; }
            else if (Filler.IsFaulted)                              { reason = "MachineFault"; downtime = true; }
            else if (_lineState == OperationalState.Starved)        { reason = "Starved";      downtime = true; }
            else if (_lineState == OperationalState.Blocked)        { reason = "Blocked";      downtime = true; }
            else if (_microStopActive)                              { reason = "MicroStop";    downtime = false; }
            else                                                    { reason = "Running";      downtime = false; }

            t.Write("PACK_LINE_DOWNTIME", downtime);
            t.Write("PACK_LINE_STATE_REASON", reason);

            // Active packaging order progress (cases as the ordering unit; bottles + pallets reported too)
            int casesDone = _order != null && _order.BottlesPerCase > 0 ? _orderBottlesProduced / _order.BottlesPerCase : 0;
            t.Write("PACK_ORD_ID", _order?.PackagingOrderNumber ?? "");
            t.Write("PACK_ORD_WO", _order?.WorkOrderId ?? "");
            t.Write("PACK_ORD_SKU", _order?.Sku ?? "");
            t.Write("PACK_ORD_SOURCE_BULK", _order?.SourceBulkMaterial ?? "");
            t.Write("PACK_ORD_STATE", (int)_lineState);
            t.Write("PACK_ORD_TARGET_CASES", _order != null ? (int)_order.TargetCases : 0);
            t.Write("PACK_ORD_CASES_DONE", casesDone);
            t.Write("PACK_ORD_BOTTLES_DONE", _orderBottlesProduced);
            t.Write("PACK_ORD_PALLETS_DONE", _order != null && _order.CasesPerPallet > 0 ? casesDone / _order.CasesPerPallet : 0);
            t.Write("PACK_ORD_PROGRESS_PCT", _order != null && _order.TargetCases > 0
                ? Math.Round(Math.Min(100.0, casesDone / _order.TargetCases * 100.0), 1) : 0.0);
            t.Write("PACK_LINE_CURRENT_SKU", _order?.Sku ?? "");

            // Order quantities, in cases. GOOD excludes bottles the filler rejected
            // (underweight/overweight/cold), unlike PACK_ORD_CASES_DONE.
            int goodCases = _order != null && _order.BottlesPerCase > 0 ? _orderGoodBottles / _order.BottlesPerCase : 0;
            t.Write("PACK_LINE_ORDER_QTY_TARGET", _order != null ? _order.TargetCases : 0.0);
            t.Write("PACK_LINE_ORDER_QTY_GOOD", (double)goodCases);

            // ETA support: rate = good cases per PLANT minute, averaged over the time this order
            // has actually been Running. Averaged (not instantaneous) so a Starved/Blocked blip
            // doesn't send the rate -- and therefore the ETA -- to zero/infinity.
            // Uses fractional cases so the rate doesn't quantise in steps of one case.
            //   ETA_plant_minutes = (ORDER_QTY_TARGET - ORDER_QTY_GOOD) / ORDER_QTY_RATE
            double goodCasesExact = _order != null && _order.BottlesPerCase > 0
                ? _orderGoodBottles / (double)_order.BottlesPerCase : 0.0;
            double orderRate = (_order != null && _orderRuntimeMin > 0)
                ? goodCasesExact / _orderRuntimeMin : 0.0;

            t.Write("PACK_LINE_ORDER_RUNTIME_MIN", _order != null ? Math.Round(_orderRuntimeMin, 2) : 0.0);
            t.Write("PACK_LINE_ORDER_QTY_RATE", Math.Round(orderRate, 3));

            // Buffer the pack line draws from (Area 450, all three tanks), as % fullness.
            t.Write("PACK_LINE_DOWNSTREAM_BUFFER_LEVEL", Math.Round(_buffers.BufferFullnessPct(), 1));

            // Cycle time (seconds per unit) derived from current line speed.
            // Line-global (whole line runs synchronized at this speed); good/scrap
            // unit aggregates are published by Area 600 once all stations are tallied.
            double cycleTimeSec = _lineSpeed > 0 ? 60.0 / _lineSpeed : 0.0;
            t.Write("PACK_LINE_CYCLE_TIME", Math.Round(cycleTimeSec, 2));
        }

        private void UpdateTunnelTemperatures(TimeSpan dt, bool filling)
        {
            double dtSec = dt.TotalSeconds;
            double blend = Math.Min(1.0, dtSec * 0.5); // first-order lag

            // Inversion tunnel - bottles stay hot (short transit)
            double invTarget = filling ? 84.0 : 22.0;
            _invZone1Temp += (invTarget - _invZone1Temp) * blend;
            _invZone2Temp += (invTarget - 1.5 - _invZone2Temp) * blend;

            // Cooling tunnel - zones progressively cooler
            // Inlet: hot from inversion (~82C)
            // Mid: cooling (~55C)
            // Outlet: near ambient target (~35C)
            double coolInTarget = filling ? 82.0 : 22.0;
            double coolMidTarget = filling ? 55.0 : 22.0;
            double coolOutTarget = filling ? 35.0 : 22.0;

            _coolZoneInTemp += (coolInTarget - _coolZoneInTemp) * blend;
            _coolZoneMidTemp += (coolMidTarget - _coolZoneMidTemp) * blend;
            _coolZoneOutTemp += (coolOutTarget - _coolZoneOutTemp) * blend;

            _invZone1Temp = _ctx.AddNoise(_invZone1Temp, 0.2);
            _invZone2Temp = _ctx.AddNoise(_invZone2Temp, 0.2);
            _coolZoneInTemp = _ctx.AddNoise(_coolZoneInTemp, 0.3);
            _coolZoneMidTemp = _ctx.AddNoise(_coolZoneMidTemp, 0.3);
            _coolZoneOutTemp = _ctx.AddNoise(_coolZoneOutTemp, 0.3);
        }

        /// <summary>Wire the API server so completed packaging runs can be served to the UNS.</summary>
        public void ConnectApiServer(ApiServer apiServer) => _apiServer = apiServer;

        /// <summary>
        /// Complete the packaging order once enough FULL cases have been made (a partial
        /// case in progress is overpacked, i.e. only whole cases count). Builds the
        /// PackagingRunCompleted event (with genealogy), latches the OPC UA flag the
        /// plant_ot_to_uns bridge polls, and releases the line to the next order.
        /// </summary>
        private void CheckOrderCompletion(DateTime simNow)
        {
            if (_order == null) return;
            int producedCases = _order.BottlesPerCase > 0 ? _orderBottlesProduced / _order.BottlesPerCase : 0;
            if (producedCases < _order.TargetCases) return;

            int producedBottles = producedCases * _order.BottlesPerCase;  // whole cases only
            int producedPallets = _order.CasesPerPallet > 0 ? producedCases / _order.CasesPerPallet : 0;

            var consumed = _bottlesByBatch.Select(kv => new ConsumedBatch
            {
                ProducedBatch = kv.Key,
                BulkMaterial  = _order.SourceBulkMaterial,
                BottlesDrawn  = kv.Value,
                CasesProduced = _order.BottlesPerCase > 0 ? Math.Floor(kv.Value / _order.BottlesPerCase) : 0
            }).ToList();

            _apiServer?.SetPackagingCompletedEvent(_order, producedBottles, producedCases, producedPallets,
                                                   consumed, _orderStarted, simNow);
            _ctx.Tags.Write("PACK_ORDER_COMPLETED", true);
            _ctx.Tags.Write("PACK_ORD_STATE", 2); // Complete

            Console.WriteLine($"[{simNow:HH:mm:ss}] Area 500: Packaging order {_order.PackagingOrderNumber} COMPLETE — " +
                              $"{producedCases} cases / {producedBottles} bottles / {producedPallets} pallets; " +
                              $"consumed batches: {string.Join(", ", _bottlesByBatch.Keys.Where(k => !string.IsNullOrEmpty(k)))}");

            _packagingQueue?.MarkComplete(simNow);
            _order = null;
            _orderBottlesProduced = 0;
            _orderGoodBottles = 0;
            _orderRuntimeMin = 0;
            _bottlesByBatch.Clear();
            _orderStarted = null;
        }

        private void PublishStationPackML(string prefix, OperationalState state, OperationalMode mode, double stateDurSec, double modeDurSec)
        {
            var t = _ctx.Tags;
            t.Write($"{prefix}_OP_STATE", (int)state);
            t.Write($"{prefix}_OP_STATE_TXT", state.ToText());
            t.Write($"{prefix}_STATE_DUR", Math.Round(stateDurSec, 0));
            t.Write($"{prefix}_OP_MODE", (int)mode);
            t.Write($"{prefix}_OP_MODE_TXT", mode.ToText());
            t.Write($"{prefix}_MODE_DUR", Math.Round(modeDurSec, 0));
        }
    }
}
