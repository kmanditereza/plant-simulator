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
        public string? Fault { get; set; }
        public string? FaultCode { get; set; }
        public string? FailureCount { get; set; }
        public string? Vibration { get; set; }
        public string? BearingTemp { get; set; }
        public string? Mcsa { get; set; }
        public string? SpeedLossPct { get; set; }
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

        /// <summary>
        /// Speed loss from mechanical wear: percent of nameplate rate lost per RUN hour since the
        /// last service. This is what makes actual cycle time drift away from ideal, so OEE
        /// Performance can fall below 100%. 0 = no speed degradation.
        /// </summary>
        public double SpeedLossPctPerRunHour { get; set; }

        /// <summary>Ceiling on the wear-induced speed loss, percent.</summary>
        public double MaxSpeedLossPct { get; set; } = 15.0;

        /// <summary>Current speed loss (%), growing with RunHoursSinceService, reset by servicing.</summary>
        public double SpeedLossPct =>
            Math.Min(MaxSpeedLossPct, SpeedLossPctPerRunHour * RunHoursSinceService);

        /// <summary>Nameplate rate reduced by the current wear-induced speed loss.</summary>
        public double EffectiveFillRate => FillRate * (1.0 - SpeedLossPct / 100.0);

        public double ProductTemp { get; set; } = 85.0;

        /// <summary>Report the filler's true mechanical load so vibration tracks actual speed.</summary>
        protected override double ConditionLoad =>
            State == EquipmentState.Running
                ? Math.Clamp(ActualRate / Math.Max(1.0, FillRate), 0.0, 1.5)
                : 0.0;

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
            IdentifyIfMonitored(tags.Vibration);
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

            // Wear slows the filler: the achievable rate drifts below nameplate, which shows up as
            // a longer actual cycle time and drags OEE Performance below 100%.
            double rate = EffectiveFillRate;
            ActualRate = Ctx.AddNoise(rate, rate * 0.005);
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

            // Fault + predictive-maintenance signals (previously computed but never published).
            if (_tags.Fault != null) Ctx.Tags.Write(_tags.Fault, IsFaulted);
            if (_tags.FaultCode != null) Ctx.Tags.Write(_tags.FaultCode, FaultCode);
            if (_tags.FailureCount != null) Ctx.Tags.Write(_tags.FailureCount, FailureCount);
            if (_tags.Vibration != null) Ctx.Tags.Write(_tags.Vibration, Math.Round(Condition.Vibration, 3));
            if (_tags.BearingTemp != null) Ctx.Tags.Write(_tags.BearingTemp, Math.Round(Condition.BearingTemp, 1));
            if (_tags.Mcsa != null) Ctx.Tags.Write(_tags.Mcsa, Math.Round(Condition.McsaIndex, 1));
            if (_tags.SpeedLossPct != null) Ctx.Tags.Write(_tags.SpeedLossPct, Math.Round(SpeedLossPct, 2));
        }
    }
}
