namespace BatchPlant.Equipment
{
    /// <summary>
    /// Reusable PID controller with anti-windup, output limits, and manual/auto mode.
    /// Used for temperature control (steam valves), pressure control, etc.
    /// </summary>
    public class PidController
    {
        public double Kp { get; set; }
        public double Ki { get; set; }
        public double Kd { get; set; }
        public double Setpoint { get; set; }
        public double ProcessVariable { get; set; }
        public double Output { get; private set; }
        public double OutputMin { get; set; } = 0.0;
        public double OutputMax { get; set; } = 100.0;
        public double IntegralWindupLimit { get; set; } = 50.0;
        public bool AutoMode { get; set; } = true;
        public double ManualOutput { get; set; }

        private double _integral;
        private double _previousError;
        private bool _firstUpdate = true;

        public PidController(double kp, double ki, double kd)
        {
            Kp = kp;
            Ki = ki;
            Kd = kd;
        }

        /// <summary>
        /// Update the PID output. Call once per tick.
        /// </summary>
        /// <param name="dt">Time step in seconds.</param>
        /// <returns>Controller output (0-100% typically).</returns>
        public double Update(double dt)
        {
            if (!AutoMode)
            {
                Output = Math.Clamp(ManualOutput, OutputMin, OutputMax);
                return Output;
            }

            if (dt <= 0) return Output;

            double error = Setpoint - ProcessVariable;

            // Proportional
            double p = Kp * error;

            // Integral with anti-windup
            _integral += error * dt;
            _integral = Math.Clamp(_integral, -IntegralWindupLimit, IntegralWindupLimit);
            double i = Ki * _integral;

            // Derivative (skip on first update to avoid spike)
            double d = 0;
            if (!_firstUpdate)
            {
                d = Kd * (error - _previousError) / dt;
            }
            _firstUpdate = false;
            _previousError = error;

            // Sum and clamp
            Output = Math.Clamp(p + i + d, OutputMin, OutputMax);
            return Output;
        }

        /// <summary>
        /// Reset the controller state (e.g., when switching to auto or changing setpoint).
        /// </summary>
        public void Reset()
        {
            _integral = 0;
            _previousError = 0;
            _firstUpdate = true;
        }
    }
}
