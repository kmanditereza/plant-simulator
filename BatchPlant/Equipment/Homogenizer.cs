using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Maps homogenizer outputs to OPC UA tag names. Null entries are not published.
    /// </summary>
    public class HomogenizerTagMap
    {
        public string? Pressure { get; set; }
        public string? Pressure2 { get; set; }
        public string? Current { get; set; }
        public string? Run { get; set; }
        public string? Fault { get; set; }
        public string? ValveHours { get; set; }
        // Condition monitoring
        public string? Vibration { get; set; }
        public string? BearingTemp { get; set; }
        public string? Mcsa { get; set; }             // motor current signature anomaly index
        public string? FailureCount { get; set; }
        // Hydraulic / lube health
        public string? HydPressure { get; set; }
        public string? HydTemp { get; set; }
        public string? FilterDp { get; set; }
        public string? OilQuality { get; set; }
    }

    /// <summary>
    /// High-pressure homogenizer (APV Gaulin, GEA Niro Soavi style).
    /// Two-stage, 150-250 bar operating pressure. Valve set is a high-wear item that
    /// causes gradual pressure decay — replace every ~1000 run hours.
    /// </summary>
    public class Homogenizer : EquipmentBase
    {
        // Process values
        public double Pressure { get; private set; }         // 1st stage (bar)
        public double Pressure2 { get; private set; }        // 2nd stage (bar)
        public double PressureSetpoint { get; set; } = 200;
        public double Current { get; private set; }
        public double FlowRate { get; private set; }
        public double ValveHours { get; private set; }

        // Configuration
        public double MaxPressure { get; set; } = 250;
        public double Stage2Fraction { get; set; } = 0.25;   // 2nd stage ~50 bar when 1st is 200
        public double RatedCurrent { get; set; } = 50;
        public double NominalFlow { get; set; } = 80;
        public double ValveWearRate { get; set; } = 0.02;    // bar loss per run hour

        private HomogenizerTagMap _tags = new();

        protected override double ConditionLoad =>
            State == EquipmentState.Running ? Math.Clamp(Pressure / MaxPressure, 0, 1.5) : 0.0;

        public Homogenizer(PlantContext ctx, string name) : base(ctx, name)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));

            // Reciprocating HP unit: higher baseline vibration, hydraulic drive + lube oil.
            Condition.BaseVibration = 3.0;
            Condition.BearingRise = 45.0;
            Condition.HydNominalPressure = 180.0;
            Condition.HydFullTemp = 55.0;
            Condition.FilterDpClean = 0.35;
        }

        public Homogenizer Bind(HomogenizerTagMap tags)
        {
            _tags = tags;
            return this;
        }

        /// <summary>
        /// Reset valve hours after valve set replacement (maintenance).
        /// </summary>
        public void ReplaceValveSet()
        {
            ValveHours = 0;
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtHours = dt.TotalHours;
            ValveHours += dtHours;

            // 1st stage pressure decays with valve wear
            double wearLoss = ValveHours * ValveWearRate;
            Pressure = PressureSetpoint - wearLoss;
            Pressure = Math.Max(100, Pressure);
            Pressure = Ctx.AddNoise(Pressure, 2.0); // reciprocating piston noise

            // 2nd stage pressure
            Pressure2 = Pressure * Stage2Fraction;
            Pressure2 = Ctx.AddNoise(Pressure2, 0.5);

            // Flow is roughly constant for PD homogenizer
            FlowRate = NominalFlow;
            FlowRate = Ctx.AddNoise(FlowRate, 0.5);

            // Current scales with pressure
            double loadFrac = Pressure / MaxPressure;
            Current = RatedCurrent * loadFrac;
            Current = Ctx.AddNoise(Current, 0.5);
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            Pressure = Math.Max(0, Ctx.AddNoise(0, 0.5));
            Pressure2 = 0;
            FlowRate = 0;
            Current = 0;
        }

        protected override void WriteTags()
        {
            if (_tags.Pressure != null) Ctx.Tags.Write(_tags.Pressure, Math.Round(Pressure, 1));
            if (_tags.Pressure2 != null) Ctx.Tags.Write(_tags.Pressure2, Math.Round(Pressure2, 1));
            if (_tags.Current != null) Ctx.Tags.Write(_tags.Current, Math.Round(Current, 1));
            if (_tags.Run != null) Ctx.Tags.Write(_tags.Run, State == EquipmentState.Running);
            if (_tags.Fault != null) Ctx.Tags.Write(_tags.Fault, State == EquipmentState.Faulted);
            if (_tags.ValveHours != null) Ctx.Tags.Write(_tags.ValveHours, Math.Round(ValveHours, 2));
            // Condition monitoring
            if (_tags.Vibration != null) Ctx.Tags.Write(_tags.Vibration, Math.Round(Condition.Vibration, 3));
            if (_tags.BearingTemp != null) Ctx.Tags.Write(_tags.BearingTemp, Math.Round(Condition.BearingTemp, 1));
            if (_tags.Mcsa != null) Ctx.Tags.Write(_tags.Mcsa, Math.Round(Condition.McsaIndex, 1));
            if (_tags.FailureCount != null) Ctx.Tags.Write(_tags.FailureCount, FailureCount);
            // Hydraulic / lube
            if (_tags.HydPressure != null) Ctx.Tags.Write(_tags.HydPressure, Math.Round(Condition.HydraulicPressure, 1));
            if (_tags.HydTemp != null) Ctx.Tags.Write(_tags.HydTemp, Math.Round(Condition.HydraulicTemp, 1));
            if (_tags.FilterDp != null) Ctx.Tags.Write(_tags.FilterDp, Math.Round(Condition.FilterDp, 3));
            if (_tags.OilQuality != null) Ctx.Tags.Write(_tags.OilQuality, Math.Round(Condition.OilQuality, 1));
        }
    }
}
