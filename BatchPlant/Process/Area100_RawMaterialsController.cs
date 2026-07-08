using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 100 - Raw Material Storage (legacy Foxboro DCS, ~1995).
    ///
    /// Bulk storage tanks, silos, and the transfer pumps/feeders that deliver raw materials
    /// to Area 200 Pre-Mix. Since Phase 3, this area no longer runs an independent test cycle;
    /// instead it exposes public Draw*() methods that Area 200 calls when its intake valves
    /// are open. Status tags (pump run, feeder run, flow) reflect ongoing draw activity and
    /// time out a few seconds after draw stops.
    ///
    /// Tank levels deplete as product is drawn. Tanks auto-refill when depleted (simulated
    /// tanker delivery) so the plant can run indefinitely.
    ///
    /// Tag naming is legacy Foxboro style: UPPERCASE_WITH_UNDERSCORES, no hyphens.
    /// </summary>
    public class Area100_RawMaterialsController : ISimulatable
    {
        private readonly PlantContext _ctx;
        private readonly Pump _p101;
        private readonly Pump _p102;

        // Condition monitoring for screw-feeder drives
        private readonly ConditionMonitor _cmSugarFeeder;
        private readonly ConditionMonitor _cmSaltFeeder;

        public int Priority => 35; // slightly before Area 200 so draw activity is visible same tick

        // Tank contents (current state)
        private double _pasteTk1Kg = 14400;
        private double _pasteTk2Kg = 17000;
        private double _vinegarLvlPct = 65.0;
        private const double VinegarTankCapacityL = 10000;
        private double _sugarKg = 8500;
        private double _saltKg = 2200;
        private double _spiceKg = 450;
        private double _h2oTotalizerL = 0;

        // Draw activity timers - tag indicators stay true for a short "tail" after drawing stops
        // so brief idle frames between dosing steps don't flicker
        private double _pasteDrawTailSec;
        private double _vinegarDrawTailSec;
        private double _waterDrawTailSec;
        private double _sugarDrawTailSec;
        private double _saltDrawTailSec;
        private double _spiceDrawTailSec;
        private const double StatusTailSec = 2.0;

        // Current instantaneous draw rates (for flow tags)
        private double _pasteDrawRateKgPerMin;
        private double _vinegarDrawRateLpm;
        private double _waterDrawRateLpm;
        private double _sugarDrawRateKgH;
        private double _saltDrawRateKgH;

        public Area100_RawMaterialsController(PlantContext ctx)
        {
            _ctx = ctx;

            _p101 = new Pump(ctx, "P-101")
            {
                NominalFlow = 80,  // kg/min at 100% (matches paste dosing rate in Area 200)
                NominalPressure = 4.0,
                RatedCurrent = 18,
                IsPositiveDisplacement = true,
                SpeedPercent = 80
            }.Bind(new PumpTagMap
            {
                Flow = "P101_FLOW",
                Current = "P101_AMPS",
                Status = "P101_STS",
                Fault = "P101_FAULT",
                FailureCount = "P101_FAILCNT",
                Vibration = "P101_VIB",
                BearingTemp = "P101_BRGTEMP",
                Mcsa = "P101_MCSA"
            });

            _p102 = new Pump(ctx, "P-102")
            {
                NominalFlow = 80,
                NominalPressure = 4.0,
                RatedCurrent = 18,
                IsPositiveDisplacement = true,
                SpeedPercent = 80
            }.Bind(new PumpTagMap
            {
                Status = "P102_STS",
                Fault = "P102_FAULT",
                FailureCount = "P102_FAILCNT",
                Vibration = "P102_VIB",
                BearingTemp = "P102_BRGTEMP",
                Mcsa = "P102_MCSA"
            });

            _cmSugarFeeder = new ConditionMonitor(_ctx) { BaseVibration = 1.0, BearingRise = 22 };
            _cmSaltFeeder = new ConditionMonitor(_ctx) { BaseVibration = 1.0, BearingRise = 22 };
        }

        // ---- Public draw API called by Area 200 each tick while its intake valves are open ----

        /// <summary>Draw water (returns actual L, temp always ~15°C).</summary>
        public double DrawWater(double litersRequested)
        {
            if (litersRequested <= 0) return 0;
            _waterDrawRateLpm = litersRequested * 60.0; // convert to per-minute for status tag
            _waterDrawTailSec = StatusTailSec;
            _h2oTotalizerL += litersRequested;
            return litersRequested; // municipal supply is unlimited
        }

        /// <summary>Draw paste. Returns actual kg drawn (limited by tank contents).</summary>
        public double DrawPaste(double kgRequested)
        {
            if (kgRequested <= 0 || _pasteTk1Kg <= 0) return 0;
            double taken = Math.Min(kgRequested, _pasteTk1Kg);
            _pasteTk1Kg -= taken;
            _pasteDrawRateKgPerMin = taken * 60.0;
            _pasteDrawTailSec = StatusTailSec;
            return taken;
        }

        /// <summary>Draw vinegar. Returns actual L drawn.</summary>
        public double DrawVinegar(double litersRequested)
        {
            if (litersRequested <= 0) return 0;
            double availableL = _vinegarLvlPct / 100.0 * VinegarTankCapacityL;
            double taken = Math.Min(litersRequested, availableL);
            _vinegarLvlPct = Math.Max(0, (availableL - taken) / VinegarTankCapacityL * 100.0);
            _vinegarDrawRateLpm = taken * 60.0;
            _vinegarDrawTailSec = StatusTailSec;
            return taken;
        }

        /// <summary>Draw sugar from silo. Returns actual kg drawn.</summary>
        public double DrawSugar(double kgRequested)
        {
            if (kgRequested <= 0 || _sugarKg <= 0) return 0;
            double taken = Math.Min(kgRequested, _sugarKg);
            _sugarKg -= taken;
            _sugarDrawRateKgH = taken * 3600.0;
            _sugarDrawTailSec = StatusTailSec;
            return taken;
        }

        /// <summary>Draw salt from bin. Returns actual kg drawn.</summary>
        public double DrawSalt(double kgRequested)
        {
            if (kgRequested <= 0 || _saltKg <= 0) return 0;
            double taken = Math.Min(kgRequested, _saltKg);
            _saltKg -= taken;
            _saltDrawRateKgH = taken * 3600.0;
            _saltDrawTailSec = StatusTailSec;
            return taken;
        }

        /// <summary>Draw spice from hopper. Returns actual kg drawn.</summary>
        public double DrawSpice(double kgRequested)
        {
            if (kgRequested <= 0 || _spiceKg <= 0) return 0;
            double taken = Math.Min(kgRequested, _spiceKg);
            _spiceKg -= taken;
            _spiceDrawTailSec = StatusTailSec;
            return taken;
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Decay status tail timers
            _pasteDrawTailSec = Math.Max(0, _pasteDrawTailSec - dtSec);
            _vinegarDrawTailSec = Math.Max(0, _vinegarDrawTailSec - dtSec);
            _waterDrawTailSec = Math.Max(0, _waterDrawTailSec - dtSec);
            _sugarDrawTailSec = Math.Max(0, _sugarDrawTailSec - dtSec);
            _saltDrawTailSec = Math.Max(0, _saltDrawTailSec - dtSec);
            _spiceDrawTailSec = Math.Max(0, _spiceDrawTailSec - dtSec);

            // If the draw tail has expired, rate readings drop to 0
            if (_pasteDrawTailSec <= 0) _pasteDrawRateKgPerMin = 0;
            if (_vinegarDrawTailSec <= 0) _vinegarDrawRateLpm = 0;
            if (_waterDrawTailSec <= 0) _waterDrawRateLpm = 0;
            if (_sugarDrawTailSec <= 0) _sugarDrawRateKgH = 0;
            if (_saltDrawTailSec <= 0) _saltDrawRateKgH = 0;

            // Run the paste pump only while paste is actively being drawn
            bool pasteRunning = _pasteDrawTailSec > 0;
            if (pasteRunning && _p101.State != EquipmentState.Running && _p101.State != EquipmentState.Starting)
                _p101.Start();
            else if (!pasteRunning && _p101.State == EquipmentState.Running)
                _p101.Stop();

            _p101.Tick(dt, simNow);
            _p102.Tick(dt, simNow);

            // Auto-refill tanks when depleted (simulated deliveries)
            if (_pasteTk1Kg < 2000) _pasteTk1Kg = 14400;
            if (_vinegarLvlPct < 15) _vinegarLvlPct = 85;
            if (_sugarKg < 1000) _sugarKg = 8500;
            if (_saltKg < 200) _saltKg = 2200;
            if (_spiceKg < 50) _spiceKg = 450;

            // Publish tags
            var t = _ctx.Tags;

            double tk1LvlPct = _pasteTk1Kg / (20000 * 1.1) * 100;
            t.Write("PASTE_TK1_LVL", Math.Round(tk1LvlPct, 1));
            t.Write("PASTE_TK1_TEMP", Math.Round(_ctx.AddNoise(18.0, 0.3), 1));
            t.Write("PASTE_TK1_WT", Math.Round(_pasteTk1Kg, 0));
            t.Write("P101_CMD", pasteRunning);
            // Override the pump's own P101_FLOW write with our draw rate
            t.Write("P101_FLOW", Math.Round(_pasteDrawRateKgPerMin, 1));

            t.Write("PASTE_TK2_LVL", Math.Round(_pasteTk2Kg / (20000 * 1.1) * 100, 1));
            t.Write("PASTE_TK2_TEMP", Math.Round(_ctx.AddNoise(18.0, 0.3), 1));
            t.Write("PASTE_TK2_WT", Math.Round(_pasteTk2Kg, 0));
            t.Write("P102_CMD", false);

            t.Write("VIN_TK_LVL", Math.Round(_vinegarLvlPct, 1));
            t.Write("VIN_TK_TEMP", Math.Round(_ctx.AddNoise(20.0, 0.2), 1));
            t.Write("VIN_TK_FLOW", Math.Round(_vinegarDrawRateLpm, 1));
            t.Write("VIN_VLV_STS", _vinegarDrawTailSec > 0);

            t.Write("SUGAR_BIN_WT", Math.Round(_sugarKg, 0));
            t.Write("SUGAR_FEEDER_SPD", Math.Round(_sugarDrawRateKgH, 1));
            t.Write("SUGAR_FEEDER_RUN", _sugarDrawTailSec > 0);
            _cmSugarFeeder.Update(_sugarDrawTailSec > 0, 0.6, false, dt.TotalSeconds);
            t.Write("SUGAR_FEEDER_FAULT", false);
            _cmSugarFeeder.Publish(t, vibration: "SUGAR_FEEDER_VIB", bearingTemp: "SUGAR_FEEDER_BRGTEMP",
                mcsa: "SUGAR_FEEDER_MCSA", failureCount: "SUGAR_FEEDER_FAILCNT");

            t.Write("SALT_BIN_WT", Math.Round(_saltKg, 0));
            t.Write("SALT_FEEDER_SPD", Math.Round(_saltDrawRateKgH, 1));
            t.Write("SALT_FEEDER_RUN", _saltDrawTailSec > 0);
            _cmSaltFeeder.Update(_saltDrawTailSec > 0, 0.6, false, dt.TotalSeconds);
            t.Write("SALT_FEEDER_FAULT", false);
            _cmSaltFeeder.Publish(t, vibration: "SALT_FEEDER_VIB", bearingTemp: "SALT_FEEDER_BRGTEMP",
                mcsa: "SALT_FEEDER_MCSA", failureCount: "SALT_FEEDER_FAILCNT");

            t.Write("SPICE_BIN_WT", Math.Round(_spiceKg, 1));
            t.Write("SPICE_FEEDER_RUN", _spiceDrawTailSec > 0);

            t.Write("H2O_FLOW", Math.Round(_waterDrawRateLpm, 1));
            t.Write("H2O_PRESS", Math.Round(_ctx.AddNoise(4.2, 0.05), 2));
            t.Write("H2O_TEMP", Math.Round(_ctx.AddNoise(15.0, 0.3), 1));
            t.Write("H2O_VLV_STS", _waterDrawTailSec > 0);
            t.Write("H2O_TOTALIZER", Math.Round(_h2oTotalizerL, 0));
        }
    }
}
