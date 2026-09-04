using BatchPlant.Core;
using BatchPlant.Equipment;
using BatchPlant.OpcUa;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 600 - Packaging Line (Krones OEM, ~2018).
    ///
    /// Discrete stations downstream of the filling line, in order:
    ///   Labeler (LBL-601) → Date Coder (DC-601) → X-Ray/Metal Detect (XR-601) → Case Packer (CP-601) → Palletizer (PAL-601)
    ///
    /// Consumes bottles from Area 500 at line rate. Counters roll up into cases (24 bottles/case)
    /// and pallets (60 cases/pallet = 5 layers × 12 cases).
    ///
    /// This controller doesn't usually block upstream (it runs at the same rate as the filler),
    /// but it CAN block when a station faults. We simplify: all stations mirror the line state
    /// reported by Area 500.
    /// </summary>
    public class Area600_PackagingLineController : ISimulatable
    {
        private readonly PlantContext _ctx;
        private readonly Area500_FillingLineController _fillingLine;

        public int Priority => 41; // after filling line so we see its BottlesProducedThisTick

        private const int BottlesPerCase = 24;
        private const int CasesPerPallet = 60; // 5 layers × 12 cases
        private const double XrayRejectRate = 0.001;       // 0.1% X-ray reject
        private const double MetalDetectRejectRate = 0.0001; // 0.01% metal detector trip

        // Running counters
        private int _labelCount;
        private int _labelRejectCount;
        private int _dateCodeCount;
        private int _xrayTotal;
        private int _xrayReject;
        private int _metalDetectTrips;
        private int _bottlesInCurrentCase;
        private int _caseCount;
        private int _casesInCurrentLayer;
        private int _currentLayerNumber = 1;
        private int _palletCount;

        // Metal detector trip flag (latches briefly on detection)
        private DateTime _lastMdTrip = DateTime.MinValue;

        // Today-counters (reset on shift change - for now they're cumulative since boot)
        private int _casesToday;
        private int _palletsToday;

        // Per-shift good/scrap tracking: baselines captured at each shift change so the
        // published counts reflect only the current shift's production.
        private int _lastShift;
        private bool _shiftInit;
        private long _shiftGoodBaseline;
        private long _shiftScrapBaseline;

        // Condition monitoring: labeler drive (rotating), case packer & palletizer (rotating + hydraulic)
        private readonly ConditionMonitor _cmLabeler;
        private readonly ConditionMonitor _cmXray;
        private readonly ConditionMonitor _cmCasePacker;
        private readonly ConditionMonitor _cmPalletizer;

        public Area600_PackagingLineController(PlantContext ctx, Area500_FillingLineController fillingLine)
        {
            _ctx = ctx;
            _fillingLine = fillingLine;

            _cmLabeler = new ConditionMonitor(ctx){ BaseVibration = 1.4, BearingRise = 28 }
                .Identify("EQ-MUC-LBL601", "Labeler LBL-601", "Packaging", "LBL_601_VIB");
            _cmXray = new ConditionMonitor(ctx){ BaseVibration = 0.9, BearingRise = 18 }
                .Identify("EQ-MUC-XR601", "X-Ray Inspector XR-601", "Packaging", "XRAY_601_VIB");
            _cmLabeler.SeedHours(350);
            // Hydraulic case packer / palletizer: HPU pressure ~120 bar, lube + return filter.
            _cmCasePacker = new ConditionMonitor(ctx){ BaseVibration = 2.0, BearingRise = 32, HydNominalPressure = 120, HydFullTemp = 48, FilterDpClean = 0.25 }
                .Identify("EQ-MUC-CP601", "Case Packer CP-601", "Packaging", "CP_601_VIB");
            _cmCasePacker.SeedHours(300);
            _cmPalletizer = new ConditionMonitor(ctx){ BaseVibration = 2.4, BearingRise = 35, HydNominalPressure = 140, HydFullTemp = 52, FilterDpClean = 0.30 }
                .Identify("EQ-MUC-PAL601", "Palletizer PAL-601", "Packaging", "PAL_601_VIB");
            _cmPalletizer.SeedHours(450);
        }

        public int Priority_Alt => 41;

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            // Consume whatever Area 500 produced this tick
            int bottlesThisTick = _fillingLine.BottlesProducedThisTick;

            // Determine our own state. For simplicity, we mirror the filling line state
            // because the stations are synchronized. In a more advanced model, CP-601 or
            // PAL-601 faulting would cause _downstreamBlocked to feed back to Area 500.
            int lineState = _ctx.Tags.Read<int>("PACK_LINE_OP_STATE", 1);
            var stateEnum = (OperationalState)lineState;

            // Process bottles through the stations
            if (bottlesThisTick > 0)
            {
                // Labeler: all bottles get a label (occasional reject for misfeed would be added with fault model)
                _labelCount += bottlesThisTick;

                // Date coder: prints on every bottle
                _dateCodeCount += bottlesThisTick;

                // X-ray inspector (total inspected)
                _xrayTotal += bottlesThisTick;

                // X-ray rejects (statistical)
                for (int i = 0; i < bottlesThisTick; i++)
                {
                    if (_ctx.Rng.NextDouble() < XrayRejectRate)
                    {
                        _xrayReject++;
                    }
                    // Metal detector trips (rare)
                    if (_ctx.Rng.NextDouble() < MetalDetectRejectRate)
                    {
                        _metalDetectTrips++;
                        _lastMdTrip = simNow;
                    }
                }

                // Case packer: pack good bottles (accepted by X-ray) into cases
                int acceptedBottles = bottlesThisTick; // simplified - rejects removed downstream
                _bottlesInCurrentCase += acceptedBottles;
                while (_bottlesInCurrentCase >= BottlesPerCase)
                {
                    _bottlesInCurrentCase -= BottlesPerCase;
                    _caseCount++;
                    _casesToday++;

                    // Palletizer: pack cases into pallets
                    _casesInCurrentLayer++;
                    if (_casesInCurrentLayer >= 12)
                    {
                        _casesInCurrentLayer = 0;
                        _currentLayerNumber++;
                    }
                    if (_currentLayerNumber > 5)
                    {
                        _currentLayerNumber = 1;
                        _palletCount++;
                        _palletsToday++;
                    }
                }
            }

            // Metal detector trip latches for 2 sim seconds
            bool mdTripActive = (simNow - _lastMdTrip).TotalSeconds < 2;

            // Publish Area 600 tags
            var t = _ctx.Tags;

            int stationStatus = stateEnum switch
            {
                OperationalState.Running => 1,
                OperationalState.UnplannedDowntime => 2,
                _ => 0
            };

            // Labeler
            t.Write("LBL_601_STATUS", stationStatus);
            t.Write("LBL_601_SPEED", _ctx.Tags.Read<double>("PACK_LINE_SPEED"));
            t.Write("LBL_601_REJECT", _labelRejectCount);
            t.Write("LBL_601_FAULT", false);

            // Date Coder
            t.Write("DC_601_STATUS", stationStatus);
            t.Write("DC_601_PRINT_CNT", _dateCodeCount);

            // X-Ray
            t.Write("XRAY_601_TOTAL", _xrayTotal);
            t.Write("XRAY_601_REJECT", _xrayReject);
            t.Write("XRAY_601_STATUS", stationStatus);
            // The inspector head has no moving part worth monitoring; its infeed conveyor does.
            _cmXray.Update(stationStatus == 1, 0.4, false, dt.TotalSeconds);
            _cmXray.Publish(t, vibration: "XRAY_601_VIB", bearingTemp: "XRAY_601_BRGTEMP",
                mcsa: "XRAY_601_MCSA", failureCount: "XRAY_601_FAILCNT");

            // Metal Detector
            t.Write("MD_601_TRIP", mdTripActive);
            t.Write("MD_601_CNT", _metalDetectTrips);

            // Case Packer
            t.Write("CP_601_STATUS", stationStatus);
            t.Write("CP_601_CASES_CNT", _caseCount);
            double caseRate = _ctx.Tags.Read<double>("PACK_LINE_SPEED") / BottlesPerCase;
            t.Write("CP_601_SPEED", Math.Round(caseRate, 2));
            t.Write("CP_601_FAULT", false);

            // Palletizer
            t.Write("PAL_601_STATUS", stationStatus);
            t.Write("PAL_601_LAYERS", _currentLayerNumber);
            t.Write("PAL_601_PALLETS_CNT", _palletCount);
            t.Write("PAL_601_FAULT", false);

            // ---- Condition monitoring for packaging machine drives ----
            bool running = stateEnum == OperationalState.Running;
            double packLoad = Math.Clamp(_ctx.Tags.Read<double>("PACK_LINE_SPEED") / 200.0, 0, 1.2);
            double dtSec = dt.TotalSeconds;

            _cmLabeler.Update(running, packLoad, false, dtSec);
            _cmLabeler.Publish(t, vibration: "LBL_601_VIB", bearingTemp: "LBL_601_BRGTEMP", mcsa: "LBL_601_MCSA", failureCount: "LBL_601_FAILCNT");

            _cmCasePacker.Update(running, packLoad, false, dtSec);
            _cmCasePacker.Publish(t, vibration: "CP_601_VIB", bearingTemp: "CP_601_BRGTEMP", mcsa: "CP_601_MCSA", failureCount: "CP_601_FAILCNT",
                hydPressure: "CP_601_HYD_PRESS", hydTemp: "CP_601_HYD_TEMP", filterDp: "CP_601_FILTER_DP", oilQuality: "CP_601_OIL_QUAL");

            _cmPalletizer.Update(running, packLoad, false, dtSec);
            _cmPalletizer.Publish(t, vibration: "PAL_601_VIB", bearingTemp: "PAL_601_BRGTEMP", mcsa: "PAL_601_MCSA", failureCount: "PAL_601_FAILCNT",
                hydPressure: "PAL_601_HYD_PRESS", hydTemp: "PAL_601_HYD_TEMP", filterDp: "PAL_601_FILTER_DP", oilQuality: "PAL_601_OIL_QUAL");

            // Station-level PackML state/mode (all stations mirror the line)
            double stateDur = _ctx.Tags.Read<double>("PACK_LINE_MODE_DUR"); // shortcut
            PublishStationPackML("LBL_601", stateEnum, OperationalMode.Production, stateDur, stateDur);
            PublishStationPackML("DC_601", stateEnum, OperationalMode.Production, stateDur, stateDur);
            PublishStationPackML("XRAY_601", stateEnum, OperationalMode.Production, stateDur, stateDur);
            PublishStationPackML("CP_601", stateEnum, OperationalMode.Production, stateDur, stateDur);
            PublishStationPackML("PAL_601", stateEnum, OperationalMode.Production, stateDur, stateDur);

            // PackLine aggregates (counters owned by Area 600)
            t.Write("CASES_TODAY", _casesToday);
            t.Write("PALLETS_TODAY", _palletsToday);

            // Line-global good/scrap unit counts. Scrap = filler weight rejects (Area 500)
            // + all downstream rejects (labeler, X-ray, metal detector). A unit is only
            // "good" once it clears every station on the line.
            long fillerGood = _fillingLine.Filler.GoodCount;
            long fillerReject = _fillingLine.Filler.RejectCount;
            long downstreamReject = _labelRejectCount + _xrayReject + _metalDetectTrips;
            long lineScrap = fillerReject + downstreamReject;
            long lineGood = Math.Max(0, fillerGood - downstreamReject);

            // Rebase good/scrap at each shift change so the published counts are per-shift.
            int shift = _ctx.Clock.CurrentShift;
            if (!_shiftInit || shift != _lastShift)
            {
                _shiftInit = true;
                _lastShift = shift;
                _shiftGoodBaseline = lineGood;
                _shiftScrapBaseline = lineScrap;
            }
            long shiftGood = Math.Max(0, lineGood - _shiftGoodBaseline);
            long shiftScrap = Math.Max(0, lineScrap - _shiftScrapBaseline);
            t.Write("PACK_LINE_GOOD_UNITS_SHIFT", (int)shiftGood);
            t.Write("PACK_LINE_SCRAP_UNITS_SHIFT", (int)shiftScrap);

            // Shift quality KPIs, all from the same good/scrap so they stay mutually consistent.
            long shiftTotal = shiftGood + shiftScrap;
            t.Write("PACK_LINE_DEFECT_COUNT", (int)shiftScrap);
            t.Write("PACK_LINE_DEFECT_RATE_PPM", shiftTotal > 0 ? Math.Round((double)shiftScrap / shiftTotal * 1_000_000.0, 1) : 0.0);
            t.Write("PACK_LINE_FIRST_PASS_YIELD", shiftTotal > 0 ? Math.Round((double)shiftGood / shiftTotal * 100.0, 3) : 100.0);

            // Plant total production (across both lines - for now just packaging)
            t.Write("PLANT_TOTAL_PRODUCTION_TODAY", _casesToday);

            // Keep downstream-blocked signal false (no upstream block yet)
            _fillingLine.SetDownstreamBlocked(false);
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
