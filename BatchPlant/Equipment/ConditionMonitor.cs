using BatchPlant.Core;
using BatchPlant.OpcUa;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Reusable condition-monitoring / predictive-maintenance signal generator.
    ///
    /// Produces realistic, wear-driven values for the sensors added across the plant:
    ///   Rotating machine health : Vibration, BearingTemp, McsaIndex (motor current signature)
    ///   Hydraulic / lube health : HydraulicPressure, HydraulicTemp, FilterDp, OilQuality
    ///
    /// A single instance is owned by each monitored asset. It is self-contained (tracks its own
    /// accumulated wear/service hours) so it can be driven from both the equipment classes and the
    /// inline area controllers. Values evolve with run time and load:
    ///   - vibration and bearing temperature climb with load and accumulated wear (and spike on fault),
    ///   - the motor-current-signature index rises as electrical/mechanical asymmetry develops,
    ///   - filter differential pressure rises as the element clogs,
    ///   - oil quality decays with service hours.
    ///
    /// Wear accumulates in simulated hours, consistent with the existing wear models
    /// (e.g. Motor uses RunHoursSinceService/5000, Homogenizer accumulates ValveHours).
    /// </summary>
    public class ConditionMonitor
    {
        private readonly PlantContext _ctx;

        // ---- Configuration (sensible defaults; override per asset) ----
        /// <summary>Healthy vibration at full load, mm/s RMS.</summary>
        public double BaseVibration { get; set; } = 1.5;
        /// <summary>Ambient/reference temperature, degC.</summary>
        public double Ambient { get; set; } = 25.0;
        /// <summary>Bearing temperature rise above ambient at full load, degC.</summary>
        public double BearingRise { get; set; } = 35.0;
        /// <summary>Nominal hydraulic system pressure at load, bar.</summary>
        public double HydNominalPressure { get; set; } = 150.0;
        /// <summary>Steady hydraulic oil temperature at full load, degC.</summary>
        public double HydFullTemp { get; set; } = 50.0;
        /// <summary>Filter differential pressure when clean, bar.</summary>
        public double FilterDpClean { get; set; } = 0.20;
        /// <summary>Run hours over which a filter element clogs from clean to alarm.</summary>
        public double FilterServiceHours { get; set; } = 1500.0;
        /// <summary>Run hours over which oil quality decays appreciably.</summary>
        public double OilServiceHours { get; set; } = 3000.0;
        /// <summary>Run hours scaling for general mechanical wear (vibration/MCSA growth).</summary>
        public double WearHours { get; set; } = 5000.0;

        // ---- Accumulated state ----
        private double _wearHrs;
        private double _oilHrs;
        private double _filterHrs;
        private bool _prevFault;

        // ---- Outputs ----
        public int FailureCount { get; private set; }
        public double Vibration { get; private set; }
        public double BearingTemp { get; private set; }
        public double McsaIndex { get; private set; }
        public double HydraulicPressure { get; private set; }
        public double HydraulicTemp { get; private set; }
        public double FilterDp { get; private set; }
        public double OilQuality { get; private set; } = 100.0;

        public ConditionMonitor(PlantContext ctx)
        {
            _ctx = ctx;
            BearingTemp = Ambient;
            HydraulicTemp = Ambient;
            FilterDp = FilterDpClean;
        }

        /// <summary>Seed accumulated hours so assets don't all start pristine (optional).</summary>
        public void SeedHours(double hours)
        {
            _wearHrs = hours;
            _oilHrs = hours;
            _filterHrs = hours;
        }

        /// <summary>
        /// Maintenance: reset accumulated wear/oil/filter hours so the degradation signals return
        /// to their as-new baseline. Called after a condition-based fault is repaired (bearing
        /// replaced, filter changed). FailureCount is cumulative and deliberately not reset.
        /// </summary>
        public void Service()
        {
            _wearHrs = 0;
            _oilHrs = 0;
            _filterHrs = 0;
            FilterDp = FilterDpClean;
            OilQuality = 100.0;
        }

        /// <summary>
        /// Advance the model one tick.
        /// </summary>
        /// <param name="running">Whether the asset is running this tick.</param>
        /// <param name="load">Mechanical load fraction (0..1.5+, 1.0 = rated).</param>
        /// <param name="faulted">Whether the asset is currently faulted.</param>
        /// <param name="dtSec">Simulated seconds elapsed this tick.</param>
        public void Update(bool running, double load, bool faulted, double dtSec)
        {
            // Failure count increments on the rising edge of a fault.
            if (faulted && !_prevFault) FailureCount++;
            _prevFault = faulted;

            double dtHours = dtSec / 3600.0;
            if (running)
            {
                _wearHrs += dtHours;
                _oilHrs += dtHours;
                _filterHrs += dtHours;
            }

            double wearFactor = 1.0 + _wearHrs / WearHours;      // ~1.0 new, grows slowly
            double lf = Math.Clamp(load, 0.0, 1.5);

            if (running)
            {
                // Vibration grows with load and wear; roughly doubles under fault.
                Vibration = BaseVibration * (0.4 + 0.6 * lf) * wearFactor;
                if (faulted) Vibration *= 2.2;
                Vibration = Math.Max(0, _ctx.AddNoise(Vibration, BaseVibration * 0.05));

                // Bearing temperature: first-order approach to a load/wear dependent target.
                double brgTarget = Ambient + BearingRise * (0.3 + 0.7 * lf) * (0.95 + 0.05 * wearFactor);
                if (faulted) brgTarget += 15.0;
                BearingTemp += (brgTarget - BearingTemp) * Math.Min(1.0, dtSec * 0.02);
                BearingTemp = _ctx.AddNoise(BearingTemp, 0.2);

                // Motor current signature analysis anomaly index (0-100%). Baseline low,
                // rises with mechanical wear and any fault (broken bar/eccentricity proxy).
                double mcsa = 4.0 + (wearFactor - 1.0) * 120.0 + (faulted ? 45.0 : 0.0);
                McsaIndex = Math.Clamp(_ctx.AddNoise(mcsa, 1.0), 0, 100);

                // Hydraulic system: pressure near nominal (scaled by load), oil temp approaches steady.
                HydraulicPressure = _ctx.AddNoise(HydNominalPressure * (0.85 + 0.15 * lf), HydNominalPressure * 0.01);
                HydraulicPressure = Math.Max(0, HydraulicPressure);
                double hydTarget = Ambient + (HydFullTemp - Ambient) * (0.4 + 0.6 * lf);
                HydraulicTemp += (hydTarget - HydraulicTemp) * Math.Min(1.0, dtSec * 0.01);
                HydraulicTemp = _ctx.AddNoise(HydraulicTemp, 0.2);
            }
            else
            {
                Vibration = Math.Max(0, _ctx.AddNoise(0.05, 0.02));
                BearingTemp += (Ambient - BearingTemp) * Math.Min(1.0, dtSec * 0.01);
                BearingTemp = _ctx.AddNoise(BearingTemp, 0.1);
                McsaIndex = Math.Clamp(_ctx.AddNoise(2.0, 0.5), 0, 100);
                HydraulicPressure = Math.Max(0, _ctx.AddNoise(0.0, 0.5));
                HydraulicTemp += (Ambient - HydraulicTemp) * Math.Min(1.0, dtSec * 0.005);
            }

            // Filter differential pressure rises as the element clogs with service hours.
            double clogFrac = _filterHrs / FilterServiceHours;
            FilterDp = FilterDpClean * (1.0 + clogFrac * 4.0);
            FilterDp = Math.Max(0, _ctx.AddNoise(FilterDp, FilterDpClean * 0.03));

            // Oil quality (%) decays exponentially with oil-in-service hours.
            OilQuality = 100.0 * Math.Exp(-_oilHrs / (OilServiceHours * 1.5));
            OilQuality = Math.Clamp(_ctx.AddNoise(OilQuality, 0.2), 0, 100);
        }

        /// <summary>
        /// Publish the monitored signals to the given tag names. Null names are skipped.
        /// Used by inline area controllers (equipment classes publish via their own WriteTags).
        /// </summary>
        public void Publish(
            TagRegistry t,
            string? vibration = null,
            string? bearingTemp = null,
            string? mcsa = null,
            string? failureCount = null,
            string? hydPressure = null,
            string? hydTemp = null,
            string? filterDp = null,
            string? oilQuality = null)
        {
            if (vibration != null) t.Write(vibration, Math.Round(Vibration, 3));
            if (bearingTemp != null) t.Write(bearingTemp, Math.Round(BearingTemp, 1));
            if (mcsa != null) t.Write(mcsa, Math.Round(McsaIndex, 1));
            if (failureCount != null) t.Write(failureCount, FailureCount);
            if (hydPressure != null) t.Write(hydPressure, Math.Round(HydraulicPressure, 1));
            if (hydTemp != null) t.Write(hydTemp, Math.Round(HydraulicTemp, 1));
            if (filterDp != null) t.Write(filterDp, Math.Round(FilterDp, 3));
            if (oilQuality != null) t.Write(oilQuality, Math.Round(OilQuality, 1));
        }
    }
}
