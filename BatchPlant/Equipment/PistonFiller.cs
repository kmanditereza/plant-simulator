using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Piston filler head model for hot-fill ketchup bottles.
    /// Tracks fill weight accuracy with piston wear degradation.
    /// Weight standard deviation increases with run hours (piston seal wear).
    /// </summary>
    public class PistonFiller : EquipmentBase
    {
        // Process values
        public double CurrentWeight { get; private set; }    // g (last filled)
        public double FillRate { get; set; }                 // bottles/min
        public double ActualRate { get; private set; }
        public long GoodCount { get; set; }
        public long RejectCount { get; set; }
        public long TotalCount => GoodCount + RejectCount;
        public double WeightAvg { get; private set; }
        public double WeightStdDev { get; private set; }
        public double FirstPassYield => TotalCount > 0 ? (double)GoodCount / TotalCount * 100.0 : 100.0;

        // Configuration
        public double TargetWeight { get; set; } = 397;       // g (14oz bottle)
        public double BaseStdDev { get; set; } = 3.0;         // g (new piston)
        public double WearDegradation { get; set; } = 0.5;    // g std dev increase per 100 run hours
        public double UnderweightLimit { get; set; } = 388;    // g (-2.3%)
        public double OverweightLimit { get; set; } = 406;     // g (+2.3%)
        public double MinFillTemp { get; set; } = 82.0;       // degC (food safety)

        // Fill temperature (set by process area)
        public double ProductTemp { get; set; } = 85.0;

        // Internal state for fill cycle timing
        private double _fillAccumulator;
        private readonly List<double> _recentWeights = new();
        private const int RecentWindowSize = 50;

        // Tags
        private string _tWeight = "";
        private string _tRate = "";
        private string _tGood = "";
        private string _tReject = "";
        private string _tTotal = "";
        private string _tAvg = "";
        private string _tStdDev = "";
        private string _tYield = "";
        private string _tStatus = "";

        public PistonFiller(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
        }

        public override void RegisterTags(string folderPath)
        {
            _tWeight = $"{TagPrefix}_WEIGHT";
            _tRate = $"{TagPrefix}_FILL_SPD";
            _tGood = $"{TagPrefix}_GOOD_CNT";
            _tReject = $"{TagPrefix}_REJECT_CNT";
            _tTotal = $"{TagPrefix}_TOTAL_CNT";
            _tAvg = $"{TagPrefix}_WT_AVG";
            _tStdDev = $"{TagPrefix}_WT_STDDEV";
            _tYield = $"{TagPrefix}_YIELD";
            _tStatus = $"{TagPrefix}_STATUS";

            Ctx.Tags.Define(_tWeight, BuiltInType.Double, folderPath, "g", $"{TagPrefix} Current Fill Weight");
            Ctx.Tags.Define(_tRate, BuiltInType.Double, folderPath, "BPM", $"{TagPrefix} Fill Speed");
            Ctx.Tags.Define(_tGood, BuiltInType.Int32, folderPath, description: $"{TagPrefix} Good Count", initialValue: 0);
            Ctx.Tags.Define(_tReject, BuiltInType.Int32, folderPath, description: $"{TagPrefix} Reject Count", initialValue: 0);
            Ctx.Tags.Define(_tTotal, BuiltInType.Int32, folderPath, description: $"{TagPrefix} Total Count", initialValue: 0);
            Ctx.Tags.Define(_tAvg, BuiltInType.Double, folderPath, "g", $"{TagPrefix} Weight Average");
            Ctx.Tags.Define(_tStdDev, BuiltInType.Double, folderPath, "g", $"{TagPrefix} Weight Std Dev");
            Ctx.Tags.Define(_tYield, BuiltInType.Double, folderPath, "%", $"{TagPrefix} First Pass Yield");
            Ctx.Tags.Define(_tStatus, BuiltInType.Int32, folderPath, description: $"{TagPrefix} Status (0=Off,1=Run,2=Fault,3=TempLow)", initialValue: 0);
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            // Piston wear increases fill weight variation
            double currentStdDev = BaseStdDev + (RunHoursSinceService / 100.0) * WearDegradation;

            // Accumulate fractional bottles based on rate
            ActualRate = Ctx.AddNoise(FillRate, FillRate * 0.01);
            _fillAccumulator += ActualRate * dt.TotalMinutes;

            // Fill whole bottles
            while (_fillAccumulator >= 1.0)
            {
                _fillAccumulator -= 1.0;
                FillOneBottle(currentStdDev);
            }
        }

        private void FillOneBottle(double stdDev)
        {
            // Generate fill weight with Gaussian distribution
            CurrentWeight = Ctx.NextGaussian(TargetWeight, stdDev);

            // Temperature check
            if (ProductTemp < MinFillTemp)
            {
                RejectCount++;
                UpdateStats(CurrentWeight);
                return;
            }

            // Weight classification
            if (CurrentWeight < UnderweightLimit || CurrentWeight > OverweightLimit)
            {
                RejectCount++;
            }
            else
            {
                GoodCount++;
            }

            UpdateStats(CurrentWeight);
        }

        private void UpdateStats(double weight)
        {
            _recentWeights.Add(weight);
            if (_recentWeights.Count > RecentWindowSize)
                _recentWeights.RemoveAt(0);

            if (_recentWeights.Count > 1)
            {
                WeightAvg = _recentWeights.Average();
                double variance = _recentWeights.Sum(w => (w - WeightAvg) * (w - WeightAvg)) / (_recentWeights.Count - 1);
                WeightStdDev = Math.Sqrt(variance);
            }
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            ActualRate = 0;
            _fillAccumulator = 0;
        }

        protected override void WriteTags()
        {
            int status = State switch
            {
                EquipmentState.Faulted => 2,
                EquipmentState.Running when ProductTemp < MinFillTemp => 3,
                EquipmentState.Running => 1,
                _ => 0
            };

            Ctx.Tags.Write(_tWeight, Math.Round(CurrentWeight, 1));
            Ctx.Tags.Write(_tRate, Math.Round(ActualRate, 1));
            Ctx.Tags.Write(_tGood, (int)GoodCount);
            Ctx.Tags.Write(_tReject, (int)RejectCount);
            Ctx.Tags.Write(_tTotal, (int)TotalCount);
            Ctx.Tags.Write(_tAvg, Math.Round(WeightAvg, 1));
            Ctx.Tags.Write(_tStdDev, Math.Round(WeightStdDev, 2));
            Ctx.Tags.Write(_tYield, Math.Round(FirstPassYield, 1));
            Ctx.Tags.Write(_tStatus, status);
        }
    }
}
