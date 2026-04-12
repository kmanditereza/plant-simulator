using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Belt conveyor with speed control and jam detection model.
    /// Used in filling and packaging lines.
    /// </summary>
    public class Conveyor : EquipmentBase
    {
        // Process values
        public double SpeedSetpoint { get; set; }   // items/min or m/min
        public double ActualSpeed { get; private set; }
        public double Current { get; private set; }
        public bool IsJammed { get; private set; }

        // Configuration
        public double MaxSpeed { get; set; } = 250;       // items/min
        public double RatedCurrent { get; set; } = 5.0;   // A
        public double RampRate { get; set; } = 100;        // items/min/sec

        // Tags
        private string _tSpeed = "";
        private string _tRun = "";
        private string _tFault = "";

        public Conveyor(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public override void RegisterTags(string folderPath)
        {
            _tSpeed = $"{TagPrefix}_SPD";
            _tRun = $"{TagPrefix}_RUN";
            _tFault = $"{TagPrefix}_FAULT";

            Ctx.Tags.Define(_tSpeed, BuiltInType.Double, folderPath, "items/min", $"{TagPrefix} Speed");
            Ctx.Tags.Define(_tRun, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Running");
            Ctx.Tags.Define(_tFault, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Fault/Jam");
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Speed ramp
            double error = SpeedSetpoint - ActualSpeed;
            double maxChange = RampRate * dtSec;
            ActualSpeed += Math.Clamp(error, -maxChange, maxChange);
            ActualSpeed = Math.Clamp(ActualSpeed, 0, MaxSpeed);
            ActualSpeed = Ctx.AddNoise(ActualSpeed, ActualSpeed * 0.005);

            // Current proportional to speed and load
            Current = RatedCurrent * (ActualSpeed / MaxSpeed) * 0.7;
            Current = Ctx.AddNoise(Current, 0.05);
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            ActualSpeed = 0;
            Current = 0;
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tSpeed, Math.Round(ActualSpeed, 1));
            Ctx.Tags.Write(_tRun, State == EquipmentState.Running);
            Ctx.Tags.Write(_tFault, State == EquipmentState.Faulted);
        }
    }
}
