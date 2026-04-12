using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// High-pressure homogenizer (e.g., APV Gaulin, GEA Niro Soavi).
    /// 150-250 bar operating pressure. Valve wear causes gradual pressure loss.
    /// Very high maintenance item - valve set replacement every 500-1000 run hours.
    /// </summary>
    public class Homogenizer : EquipmentBase
    {
        // Process values
        public double Pressure { get; private set; }         // bar (outlet)
        public double PressureSetpoint { get; set; } = 200;  // bar
        public double Current { get; private set; }          // A
        public double FlowRate { get; private set; }         // L/min
        public double ValveHours { get; private set; }       // Run hours on current valve set

        // Configuration
        public double MaxPressure { get; set; } = 250;
        public double RatedCurrent { get; set; } = 50;      // A (large motor)
        public double NominalFlow { get; set; } = 80;       // L/min
        public double ValveWearRate { get; set; } = 0.02;   // bar loss per run hour

        // Tags
        private string _tPress = "";
        private string _tAmps = "";
        private string _tRun = "";
        private string _tFault = "";
        private string _tValveHrs = "";

        public Homogenizer(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
        }

        public override void RegisterTags(string folderPath)
        {
            _tPress = $"{TagPrefix}_PRESS";
            _tAmps = $"{TagPrefix}_AMPS";
            _tRun = $"{TagPrefix}_RUN";
            _tFault = $"{TagPrefix}_FAULT";
            _tValveHrs = $"{TagPrefix}_VLV_HRS";

            Ctx.Tags.Define(_tPress, BuiltInType.Double, folderPath, "bar", $"{TagPrefix} Homogenizer Pressure");
            Ctx.Tags.Define(_tAmps, BuiltInType.Double, folderPath, "A", $"{TagPrefix} Motor Current");
            Ctx.Tags.Define(_tRun, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Running");
            Ctx.Tags.Define(_tFault, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Fault");
            Ctx.Tags.Define(_tValveHrs, BuiltInType.Double, folderPath, "hrs", $"{TagPrefix} Valve Run Hours");
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtHours = dt.TotalHours;
            ValveHours += dtHours;

            // Pressure decays with valve wear
            double wearLoss = ValveHours * ValveWearRate;
            Pressure = PressureSetpoint - wearLoss;
            Pressure = Math.Max(100, Pressure); // Can't drop below minimum
            Pressure = Ctx.AddNoise(Pressure, 2.0); // ±2 bar fluctuation (reciprocating)

            // Flow (relatively constant for PD homogenizer)
            FlowRate = NominalFlow;
            FlowRate = Ctx.AddNoise(FlowRate, 0.5);

            // Current increases slightly with pressure
            double loadFrac = Pressure / MaxPressure;
            Current = RatedCurrent * loadFrac;
            Current = Ctx.AddNoise(Current, 0.5);
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            Pressure = Ctx.AddNoise(0, 0.5);
            Pressure = Math.Max(0, Pressure);
            FlowRate = 0;
            Current = 0;
        }

        /// <summary>
        /// Reset valve hours after maintenance (valve set replacement).
        /// </summary>
        public void ReplaceValveSet()
        {
            ValveHours = 0;
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tPress, Math.Round(Pressure, 1));
            Ctx.Tags.Write(_tAmps, Math.Round(Current, 1));
            Ctx.Tags.Write(_tRun, State == EquipmentState.Running);
            Ctx.Tags.Write(_tFault, State == EquipmentState.Faulted);
            Ctx.Tags.Write(_tValveHrs, Math.Round(ValveHours, 1));
        }
    }
}
