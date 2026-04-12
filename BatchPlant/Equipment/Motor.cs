using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// VFD-driven motor with speed, torque, power, current, vibration, and winding temperature.
    /// Vibration increases gradually with wear (run hours since service).
    /// </summary>
    public class Motor : EquipmentBase
    {
        // Process values
        public double SpeedSetpoint { get; set; }  // RPM or %
        public double Speed { get; private set; }
        public double Torque { get; private set; }
        public double Power { get; private set; }   // kW
        public double Current { get; private set; } // A
        public double Vibration { get; private set; } // mm/s
        public double WindingTemp { get; private set; } = 25.0; // degC

        // Configuration
        public double MaxSpeed { get; set; } = 1500; // RPM
        public double RatedPower { get; set; } = 15;  // kW
        public double RatedCurrent { get; set; } = 30; // A
        public double RampRate { get; set; } = 500;    // RPM/sec (VFD accel)
        public double BaseVibration { get; set; } = 0.5; // mm/s baseline
        public double AmbientTemp { get; set; } = 25.0;

        // Tags
        private string _tSpeed = "";
        private string _tTorque = "";
        private string _tPower = "";
        private string _tCurrent = "";
        private string _tVibration = "";
        private string _tRun = "";
        private string _tFault = "";

        public Motor(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        }

        public override void RegisterTags(string folderPath)
        {
            _tSpeed = $"{TagPrefix}_SPD";
            _tTorque = $"{TagPrefix}_TRQ";
            _tPower = $"{TagPrefix}_PWR";
            _tCurrent = $"{TagPrefix}_AMPS";
            _tVibration = $"{TagPrefix}_VIB";
            _tRun = $"{TagPrefix}_RUN";
            _tFault = $"{TagPrefix}_FAULT";

            Ctx.Tags.Define(_tSpeed, BuiltInType.Double, folderPath, "RPM", $"{TagPrefix} Speed");
            Ctx.Tags.Define(_tTorque, BuiltInType.Double, folderPath, "Nm", $"{TagPrefix} Torque");
            Ctx.Tags.Define(_tPower, BuiltInType.Double, folderPath, "kW", $"{TagPrefix} Power");
            Ctx.Tags.Define(_tCurrent, BuiltInType.Double, folderPath, "A", $"{TagPrefix} Current");
            Ctx.Tags.Define(_tVibration, BuiltInType.Double, folderPath, "mm/s", $"{TagPrefix} Vibration");
            Ctx.Tags.Define(_tRun, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Running");
            Ctx.Tags.Define(_tFault, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Fault");
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Speed ramp toward setpoint (VFD acceleration)
            double speedError = SpeedSetpoint - Speed;
            double maxChange = RampRate * dtSec;
            Speed += Math.Clamp(speedError, -maxChange, maxChange);
            Speed = Math.Clamp(Speed, 0, MaxSpeed);

            // Torque (proportional to speed, with load factor)
            double speedFraction = Speed / MaxSpeed;
            Torque = RatedPower * 1000 / (2 * Math.PI * MaxSpeed / 60.0) * speedFraction;
            Torque = Ctx.AddNoise(Torque, Torque * 0.02);

            // Power
            Power = Torque * 2 * Math.PI * Speed / 60000.0;
            Power = Math.Max(0, Ctx.AddNoise(Power, 0.05));

            // Current (3-phase, power factor 0.85)
            Current = Power * 1000 / (3 * 400 * 0.85);
            Current = Ctx.AddNoise(Current, 0.1);

            // Vibration (increases with wear)
            double wearFactor = 1.0 + RunHoursSinceService / 5000.0;
            Vibration = BaseVibration * speedFraction * wearFactor;
            Vibration = Ctx.AddNoise(Vibration, 0.05);
            Vibration = Math.Max(0, Vibration);

            // Winding temperature (rises with load, cools toward ambient)
            double heatInput = Power * 0.05; // 5% of power as heat
            double cooling = (WindingTemp - AmbientTemp) * 0.01 * dtSec;
            WindingTemp += (heatInput - cooling) * dtSec;
            WindingTemp = Math.Clamp(WindingTemp, AmbientTemp, 120);
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            Speed = 0;
            Torque = 0;
            Power = 0;
            Current = 0;
            Vibration = Ctx.AddNoise(0.02, 0.01); // Ambient vibration
            Vibration = Math.Max(0, Vibration);

            // Cool down
            WindingTemp += (AmbientTemp - WindingTemp) * 0.005 * dt.TotalSeconds;
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tSpeed, Math.Round(Speed, 1));
            Ctx.Tags.Write(_tTorque, Math.Round(Torque, 2));
            Ctx.Tags.Write(_tPower, Math.Round(Power, 2));
            Ctx.Tags.Write(_tCurrent, Math.Round(Current, 2));
            Ctx.Tags.Write(_tVibration, Math.Round(Vibration, 3));
            Ctx.Tags.Write(_tRun, State == EquipmentState.Running);
            Ctx.Tags.Write(_tFault, State == EquipmentState.Faulted);
        }
    }
}
