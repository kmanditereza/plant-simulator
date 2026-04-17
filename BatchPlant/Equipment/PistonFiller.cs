using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Maps piston filler outputs to OPC UA tag names. Null entries are not published.
    /// </summary>
    public class PistonFillerTagMap
    {
        public string? CurrentWeight { get; set; }
        public string? Rate { get; set; }
        public string? GoodCount { get; set; }
        public string? RejectCount { get; set; }
        public string? TotalCount { get; set; }
        public string? WeightAvg { get; set; }
        public string? WeightStdDev { get; set; }
        public string? Yield { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>
    /// Piston filler head model for hot-fill ketchup bottles.
    /// Tracks fill weight accuracy with piston wear degradation.
    /// Weight standard deviation increases with run hours (piston seal wear).
    /// </summary>
    public class PistonFiller : EquipmentBase
    {
        // Process values
        public double CurrentWeight { get; private set; }
        public double FillRate { get; set; }           // bottles/min setpoint
        public double ActualRate { get; private set; }
        public long GoodCount { get; set; }
        public long RejectCount { get; set; }
        public long TotalCount => GoodCount + RejectCount;
        public double WeightAvg { get; private set; }
        public double WeightStdDev { get; private set; }
        public double FirstPassYield => TotalCount > 0 ? (double)GoodCount / TotalCount * 100.0 : 100.0;

        // Configuration
        public double TargetWeight { get; set; } = 397;
        public double BaseStdDev { get; set; } = 3.0;
        public double WearDegradation { get; set; } = 0.5;
        public double UnderweightLimit { get; set; } = 388;
        public double OverweightLimit { get; set; } = 406;
        public double MinFillTemp { get; set; } = 82.0;

        public double ProductTemp { get; set; } = 85.0;

        private double _fillAccumulator;
        private readonly List<double> _recentWeights = new();
        private const int RecentWindowSize = 50;

        private PistonFillerTagMap _tags = new();

        public PistonFiller(PlantContext ctx, string name) : base(ctx, name)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
        }

        public PistonFiller Bind(PistonFillerTagMap tags)
        {
            _tags = tags;
            return this;
        }

        /// <summary>
        /// Fill one or more bottles this tick. Returns number of bottles actually filled.
        /// Called by the line controller when the line is running and product is available.
        /// </summary>
        public int FillBottles(TimeSpan dt)
        {
            if (State != EquipmentState.Running) return 0;

            double currentStdDev = BaseStdDev + (RunHoursSinceService / 100.0) * WearDegradation;

            ActualRate = Ctx.AddNoise(FillRate, FillRate * 0.005);
            _fillAccumulator += ActualRate * dt.TotalMinutes;

            int filled = 0;
            while (_fillAccumulator >= 1.0)
            {
                _fillAccumulator -= 1.0;
                FillOneBottle(currentStdDev);
                filled++;
            }
            return filled;
        }

        private void FillOneBottle(double stdDev)
        {
            CurrentWeight = Ctx.NextGaussian(TargetWeight, stdDev);

            if (ProductTemp < MinFillTemp)
            {
                RejectCount++;
                UpdateStats(CurrentWeight);
                return;
            }

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

            if (_tags.CurrentWeight != null) Ctx.Tags.Write(_tags.CurrentWeight, Math.Round(CurrentWeight, 1));
            if (_tags.Rate != null) Ctx.Tags.Write(_tags.Rate, Math.Round(ActualRate, 1));
            if (_tags.GoodCount != null) Ctx.Tags.Write(_tags.GoodCount, (int)GoodCount);
            if (_tags.RejectCount != null) Ctx.Tags.Write(_tags.RejectCount, (int)RejectCount);
            if (_tags.TotalCount != null) Ctx.Tags.Write(_tags.TotalCount, (int)TotalCount);
            if (_tags.WeightAvg != null) Ctx.Tags.Write(_tags.WeightAvg, Math.Round(WeightAvg, 1));
            if (_tags.WeightStdDev != null) Ctx.Tags.Write(_tags.WeightStdDev, Math.Round(WeightStdDev, 2));
            if (_tags.Yield != null) Ctx.Tags.Write(_tags.Yield, Math.Round(FirstPassYield, 1));
            if (_tags.Status != null) Ctx.Tags.Write(_tags.Status, status);
        }
    }
}
