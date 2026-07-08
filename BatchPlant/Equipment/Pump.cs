using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Maps pump outputs to OPC UA tag names. Null entries are not published.
    /// </summary>
    public class PumpTagMap
    {
        public string? Flow { get; set; }
        public string? Pressure { get; set; }
        public string? Current { get; set; }
        public string? Status { get; set; }   // 0=Off, 1=Run, 2=Fault (for older DCS status words)
        public string? Run { get; set; }       // Boolean running
        public string? Fault { get; set; }
        public string? Speed { get; set; }     // VFD speed feedback
        public string? Vibration { get; set; }
        public string? BearingTemp { get; set; }
        public string? Mcsa { get; set; }         // motor current signature anomaly index
        public string? FailureCount { get; set; }
    }

    /// <summary>
    /// Pump model (centrifugal or positive displacement).
    /// Tracks flow rate, discharge pressure, current draw, and seal wear.
    /// Progressive cavity pumps (for tomato paste) wear faster.
    /// </summary>
    public class Pump : EquipmentBase
    {
        // Process values
        public double SpeedPercent { get; set; } = 100.0;
        public double FlowRate { get; private set; }       // L/min
        public double DischargePressure { get; private set; }
        public double Current { get; private set; }
        public double SealTemp { get; private set; } = 25.0;

        // Configuration
        public double NominalFlow { get; set; } = 100.0;   // L/min at 100%
        public double NominalPressure { get; set; } = 3.0;  // bar at 100%
        public double RatedCurrent { get; set; } = 15.0;
        public double AmbientTemp { get; set; } = 25.0;
        public bool IsPositiveDisplacement { get; set; }

        private PumpTagMap _tags = new();

        protected override double ConditionLoad =>
            State == EquipmentState.Running ? Math.Clamp(SpeedPercent / 100.0, 0, 1.5) : 0.0;

        public Pump(PlantContext ctx, string name) : base(ctx, name)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        }

        public Pump Bind(PumpTagMap tags)
        {
            _tags = tags;
            return this;
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double speedFrac = SpeedPercent / 100.0;

            // Flow scales with speed
            FlowRate = NominalFlow * speedFrac;

            // Wear reduces flow slightly
            double wearFactor = 1.0 - (RunHoursSinceService / 10000.0) * 0.1;
            wearFactor = Math.Max(0.85, wearFactor);
            FlowRate *= wearFactor;
            FlowRate = Ctx.AddNoise(FlowRate, FlowRate * 0.01);
            FlowRate = Math.Max(0, FlowRate);

            // Discharge pressure (affinity law for centrifugal)
            DischargePressure = NominalPressure * speedFrac * speedFrac;
            DischargePressure = Ctx.AddNoise(DischargePressure, 0.02);

            // Current draw
            Current = RatedCurrent * speedFrac * (1.0 / wearFactor);
            Current = Ctx.AddNoise(Current, 0.1);

            // Seal temperature
            double heatGen = Current * 0.1;
            double cooling = (SealTemp - AmbientTemp) * 0.02 * dt.TotalSeconds;
            SealTemp += (heatGen - cooling) * dt.TotalSeconds;
            SealTemp = Math.Clamp(SealTemp, AmbientTemp, 100);
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            FlowRate = 0;
            DischargePressure = Ctx.AddNoise(0.05, 0.01);
            Current = 0;
            SealTemp += (AmbientTemp - SealTemp) * 0.01 * dt.TotalSeconds;
        }

        protected override void WriteTags()
        {
            int status = State switch
            {
                EquipmentState.Faulted => 2,
                EquipmentState.Running => 1,
                _ => 0
            };

            if (_tags.Flow != null) Ctx.Tags.Write(_tags.Flow, Math.Round(FlowRate, 1));
            if (_tags.Pressure != null) Ctx.Tags.Write(_tags.Pressure, Math.Round(DischargePressure, 2));
            if (_tags.Current != null) Ctx.Tags.Write(_tags.Current, Math.Round(Current, 1));
            if (_tags.Status != null) Ctx.Tags.Write(_tags.Status, status);
            if (_tags.Run != null) Ctx.Tags.Write(_tags.Run, State == EquipmentState.Running);
            if (_tags.Fault != null) Ctx.Tags.Write(_tags.Fault, State == EquipmentState.Faulted);
            if (_tags.Speed != null) Ctx.Tags.Write(_tags.Speed, Math.Round(SpeedPercent, 1));
            if (_tags.Vibration != null) Ctx.Tags.Write(_tags.Vibration, Math.Round(Condition.Vibration, 3));
            if (_tags.BearingTemp != null) Ctx.Tags.Write(_tags.BearingTemp, Math.Round(Condition.BearingTemp, 1));
            if (_tags.Mcsa != null) Ctx.Tags.Write(_tags.Mcsa, Math.Round(Condition.McsaIndex, 1));
            if (_tags.FailureCount != null) Ctx.Tags.Write(_tags.FailureCount, FailureCount);
        }
    }
}
