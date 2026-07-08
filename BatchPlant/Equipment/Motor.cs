using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Maps motor outputs to OPC UA tag names. Null entries are not published.
    /// </summary>
    public class MotorTagMap
    {
        public string? Speed { get; set; }
        public string? Torque { get; set; }
        public string? Power { get; set; }
        public string? Current { get; set; }
        public string? Vibration { get; set; }
        public string? Run { get; set; }
        public string? Fault { get; set; }
        public string? BearingTemp { get; set; }
        public string? Mcsa { get; set; }         // motor current signature anomaly index
        public string? FailureCount { get; set; }
    }

    /// <summary>
    /// VFD-driven motor with speed, torque, power, current, vibration, and winding temperature.
    /// Vibration increases gradually with wear (run hours since service).
    /// LoadFactor is set by the area controller based on what the motor is driving.
    /// </summary>
    public class Motor : EquipmentBase
    {
        // Process values
        public double SpeedSetpoint { get; set; }
        public double Speed { get; private set; }
        public double Torque { get; private set; }
        public double Power { get; private set; }
        public double Current { get; private set; }
        public double Vibration { get; private set; }
        public double WindingTemp { get; private set; } = 25.0;

        // Configuration
        public double MaxSpeed { get; set; } = 1500;
        public double RatedPower { get; set; } = 15;
        public double RatedCurrent { get; set; } = 30;
        public double RampRate { get; set; } = 500;
        public double BaseVibration { get; set; } = 0.5;
        public double AmbientTemp { get; set; } = 25.0;

        /// <summary>
        /// Load factor (0.0–1.5+): set by area controller each tick.
        /// 0.0 = no load, 1.0 = rated load at full speed, &gt;1.0 = overload.
        /// For a mixer, this scales with product mass and viscosity.
        /// </summary>
        public double LoadFactor { get; set; } = 0.3;

        private MotorTagMap _tags = new();

        protected override double ConditionLoad =>
            State == EquipmentState.Running ? Math.Clamp(Speed / MaxSpeed, 0, 1.5) : 0.0;

        public Motor(PlantContext ctx, string name) : base(ctx, name)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// Bind this motor's outputs to OPC UA tag names. Fluent — returns this motor.
        /// </summary>
        public Motor Bind(MotorTagMap tags)
        {
            _tags = tags;
            return this;
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Speed ramp toward setpoint (VFD acceleration)
            double speedError = SpeedSetpoint - Speed;
            double maxChange = RampRate * dtSec;
            Speed += Math.Clamp(speedError, -maxChange, maxChange);
            Speed = Math.Clamp(Speed, 0, MaxSpeed);

            // Torque scales with load factor and speed^2 (viscous drag)
            double speedFraction = Speed / MaxSpeed;
            double ratedTorque = RatedPower * 1000 / (2 * Math.PI * MaxSpeed / 60.0);
            Torque = ratedTorque * LoadFactor * speedFraction * speedFraction;
            Torque = Ctx.AddNoise(Torque, Torque * 0.02);
            Torque = Math.Max(0, Torque);

            // Power = torque * angular velocity
            Power = Torque * 2 * Math.PI * Speed / 60000.0;
            Power = Math.Max(0, Ctx.AddNoise(Power, 0.05));

            // Current (3-phase, power factor 0.85)
            Current = Power * 1000 / (3 * 400 * 0.85);
            Current = Ctx.AddNoise(Current, 0.1);

            // Vibration increases with wear and speed
            double wearFactor = 1.0 + RunHoursSinceService / 5000.0;
            Vibration = BaseVibration * speedFraction * wearFactor;
            Vibration = Ctx.AddNoise(Vibration, 0.05);
            Vibration = Math.Max(0, Vibration);

            // Winding temperature rises with power, cools toward ambient
            double heatInput = Power * 0.05;
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
            Vibration = Math.Max(0, Ctx.AddNoise(0.02, 0.01));
            WindingTemp += (AmbientTemp - WindingTemp) * 0.005 * dt.TotalSeconds;
        }

        protected override void WriteTags()
        {
            if (_tags.Speed != null) Ctx.Tags.Write(_tags.Speed, Math.Round(Speed, 1));
            if (_tags.Torque != null) Ctx.Tags.Write(_tags.Torque, Math.Round(Torque, 2));
            if (_tags.Power != null) Ctx.Tags.Write(_tags.Power, Math.Round(Power, 2));
            if (_tags.Current != null) Ctx.Tags.Write(_tags.Current, Math.Round(Current, 2));
            if (_tags.Vibration != null) Ctx.Tags.Write(_tags.Vibration, Math.Round(Vibration, 3));
            if (_tags.Run != null) Ctx.Tags.Write(_tags.Run, State == EquipmentState.Running);
            if (_tags.Fault != null) Ctx.Tags.Write(_tags.Fault, State == EquipmentState.Faulted);
            if (_tags.BearingTemp != null) Ctx.Tags.Write(_tags.BearingTemp, Math.Round(Condition.BearingTemp, 1));
            if (_tags.Mcsa != null) Ctx.Tags.Write(_tags.Mcsa, Math.Round(Condition.McsaIndex, 1));
            if (_tags.FailureCount != null) Ctx.Tags.Write(_tags.FailureCount, FailureCount);
        }
    }
}
