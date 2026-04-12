using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Pump model (centrifugal or positive displacement).
    /// Tracks flow rate, discharge pressure, current draw, and seal wear.
    /// Progressive cavity pumps (for tomato paste) wear faster.
    /// </summary>
    public class Pump : EquipmentBase
    {
        // Process values
        public double SpeedPercent { get; set; } = 100.0; // VFD speed 0-100%
        public double FlowRate { get; private set; }       // L/min
        public double DischargePressure { get; private set; } // bar
        public double Current { get; private set; }        // A
        public double SealTemp { get; private set; } = 25.0;

        // Configuration
        public double NominalFlow { get; set; } = 100.0;  // L/min at 100%
        public double NominalPressure { get; set; } = 3.0; // bar at 100%
        public double RatedCurrent { get; set; } = 15.0;  // A
        public double AmbientTemp { get; set; } = 25.0;
        public bool IsPositiveDisplacement { get; set; }   // PD pump = linear flow vs speed

        // Tags
        private string _tFlow = "";
        private string _tPress = "";
        private string _tCurrent = "";
        private string _tSts = "";
        private string _tCmd = "";

        public Pump(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        }

        public override void RegisterTags(string folderPath)
        {
            _tFlow = $"{TagPrefix}_FLOW";
            _tPress = $"{TagPrefix}_PRESS";
            _tCurrent = $"{TagPrefix}_AMPS";
            _tSts = $"{TagPrefix}_STS";
            _tCmd = $"{TagPrefix}_CMD";

            Ctx.Tags.Define(_tFlow, BuiltInType.Double, folderPath, "L/min", $"{TagPrefix} Flow Rate");
            Ctx.Tags.Define(_tPress, BuiltInType.Double, folderPath, "bar", $"{TagPrefix} Discharge Pressure");
            Ctx.Tags.Define(_tCurrent, BuiltInType.Double, folderPath, "A", $"{TagPrefix} Motor Current");
            Ctx.Tags.Define(_tSts, BuiltInType.Int32, folderPath, description: $"{TagPrefix} Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            Ctx.Tags.Define(_tCmd, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Start Command", writable: true);
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double speedFrac = SpeedPercent / 100.0;

            // Flow: PD pump is linear with speed, centrifugal follows affinity laws
            if (IsPositiveDisplacement)
            {
                FlowRate = NominalFlow * speedFrac;
            }
            else
            {
                FlowRate = NominalFlow * speedFrac; // Simplified affinity law
            }

            // Wear reduces efficiency slightly
            double wearFactor = 1.0 - (RunHoursSinceService / 10000.0) * 0.1;
            wearFactor = Math.Max(0.85, wearFactor);
            FlowRate *= wearFactor;
            FlowRate = Ctx.AddNoise(FlowRate, FlowRate * 0.01);
            FlowRate = Math.Max(0, FlowRate);

            // Discharge pressure
            DischargePressure = NominalPressure * speedFrac * speedFrac; // Affinity law
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
            DischargePressure = Ctx.AddNoise(0.05, 0.01); // residual
            Current = 0;
            SealTemp += (AmbientTemp - SealTemp) * 0.01 * dt.TotalSeconds;
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tFlow, Math.Round(FlowRate, 1));
            Ctx.Tags.Write(_tPress, Math.Round(DischargePressure, 2));
            Ctx.Tags.Write(_tCurrent, Math.Round(Current, 1));
            Ctx.Tags.Write(_tSts, State == EquipmentState.Faulted ? 2 : (State == EquipmentState.Running ? 1 : 0));
        }
    }
}
