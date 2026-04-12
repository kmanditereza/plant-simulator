using BatchPlant;
using BatchPlant.Helpers;
using BatchPlant.Alarms;

namespace BatchPlant.Operations
{
    /// <summary>
    /// Controls Stage 1: Initial Mixing operation.
    /// Integrates with OPC UA Alarms &amp; Conditions (Part 9).
    /// </summary>
    public class MixerController
    {
        private readonly Random _rand = new();

        // Mixing parameters (read from OPC UA nodes at runtime)
        private double InitialSpeedRpm => Convert.ToDouble(BatchPlantNodeManager.SP_MX_INITIAL_SPEED.Value);
        private double FinalSpeedRpm => Convert.ToDouble(BatchPlantNodeManager.SP_MX_FINAL_SPEED.Value);
        private double AmbientTemperature => Convert.ToDouble(BatchPlantNodeManager.SP_MX_AMBIENT_TEMP.Value);
        private double InitialViscosity => Convert.ToDouble(BatchPlantNodeManager.SP_MX_INITIAL_VISC.Value);
        private double TargetViscosity => Convert.ToDouble(BatchPlantNodeManager.SP_MX_TARGET_VISC.Value);

        /// <summary>
        /// Execute the mixing operation.
        /// </summary>
        public async Task ExecuteAsync()
        {
            Console.WriteLine("=== Stage 1: Initial Mixing Started ===");

            InitializeMixerState();

            int mixingDurationSeconds = Convert.ToInt32(BatchPlantNodeManager.mixing_time.Value);
            if (mixingDurationSeconds <= 0) mixingDurationSeconds = 900;

            int updateIntervalMs = 2000;
            int totalSteps = (mixingDurationSeconds * 1000) / updateIntervalMs;

            double currentViscosity = InitialViscosity;
            double currentHomogeneity = 0.0;
            double currentTemperature = AmbientTemperature;
            double energyConsumed = Convert.ToDouble(BatchPlantNodeManager.mixer_energy_consumed.Value);

            // Report accumulators
            double sumSpeed = 0.0;
            double sumPower = 0.0;
            double maxPower = 0.0;
            int stepCount = 0;
            var mixStartTime = DateTime.UtcNow;

            // Write initial viscosity report value
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_INITIAL_VISCOSITY, Math.Round(InitialViscosity, 1));

            Console.WriteLine($"Mixing Duration: {mixingDurationSeconds} seconds ({totalSteps} steps)");
            Console.WriteLine($"Initial Viscosity: {InitialViscosity} cP");
            Console.WriteLine($"Mixer Level: {BatchPlantNodeManager.LT201.Value} L");

            for (int step = 0; step <= totalSteps; step++)
            {
                double progress = (double)step / totalSteps;
                double elapsedMinutes = (progress * mixingDurationSeconds) / 60.0;

                double currentSpeed = CalculateSpeed(progress);
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_RPM, Math.Round(currentSpeed, 1));

                currentViscosity = CalculateViscosity(progress, currentSpeed, currentViscosity);
                UaWriteHelpers.Write(BatchPlantNodeManager.VISC201, Math.Round(currentViscosity, 1));

                currentHomogeneity = CalculateHomogeneity(progress, currentSpeed);
                UaWriteHelpers.Write(BatchPlantNodeManager.TURB201, Math.Round(currentHomogeneity, 1));

                currentTemperature = CalculateTemperature(progress, currentSpeed);
                UaWriteHelpers.Write(BatchPlantNodeManager.TT201, Math.Round(currentTemperature, 2));

                var motorParams = CalculateMotorParameters(currentSpeed, currentViscosity, currentHomogeneity);
                UpdateMotorParameters(motorParams);

                energyConsumed += (motorParams.Power * updateIntervalMs / 1000.0) / 3600.0;
                UaWriteHelpers.Write(BatchPlantNodeManager.mixer_energy_consumed, Math.Round(energyConsumed, 4));

                // Accumulate report statistics
                sumSpeed += currentSpeed;
                sumPower += motorParams.Power;
                if (motorParams.Power > maxPower) maxPower = motorParams.Power;
                stepCount++;

                // Check alarms - both OPC UA and console logging
                CheckAlarms(motorParams.Amperage, currentViscosity, motorParams.Vibration, progress);

                LogProgress(step, totalSteps, elapsedMinutes, currentSpeed, currentViscosity, currentHomogeneity, currentTemperature, motorParams.Power);

                await Task.Delay(updateIntervalMs);
            }

            double actualDurationSeconds = (DateTime.UtcNow - mixStartTime).TotalSeconds;
            double avgSpeed = stepCount > 0 ? sumSpeed / stepCount : 0.0;
            double avgPower = stepCount > 0 ? sumPower / stepCount : 0.0;

            FinalizeMixerState(currentViscosity, currentHomogeneity, energyConsumed,
                currentTemperature, avgSpeed, avgPower, maxPower, actualDurationSeconds);
        }

        private void InitializeMixerState()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Mixing");
            UaWriteHelpers.Write(BatchPlantNodeManager.mixer_status, "Running");
            UaWriteHelpers.Write(BatchPlantNodeManager.MX101_STATE, 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.MX101_MODE, 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_RUN, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_FAULT, false);
        }

        private double CalculateSpeed(double progress)
        {
            double sCurveProgress = 0.5 * (1 - Math.Cos(Math.PI * progress));
            double speed = InitialSpeedRpm + (FinalSpeedRpm - InitialSpeedRpm) * sCurveProgress;
            speed += (_rand.NextDouble() - 0.5) * 1.0;
            return speed;
        }

        private double CalculateViscosity(double progress, double currentSpeed, double currentViscosity)
        {
            double mixingEnergy = currentSpeed / FinalSpeedRpm;
            double decayRate = 2.5 * mixingEnergy;
            double viscosityDecay = 1 - Math.Exp(-decayRate * progress);
            double viscosity = InitialViscosity - (InitialViscosity - TargetViscosity) * viscosityDecay;

            if (progress > 0.2 && progress < 0.7)
            {
                viscosity += (_rand.NextDouble() - 0.5) * 300;
            }
            viscosity += (_rand.NextDouble() - 0.5) * 50;

            return viscosity;
        }

        private double CalculateHomogeneity(double progress, double currentSpeed)
        {
            double speedFactor = currentSpeed / FinalSpeedRpm;
            double midpoint = 0.4 - (0.1 * speedFactor);
            double steepness = 6 + (4 * speedFactor);
            double sigmoid = 1.0 / (1.0 + Math.Exp(-steepness * (progress - midpoint)));
            double homogeneity = sigmoid * 98.0;
            homogeneity += (_rand.NextDouble() - 0.5) * 1.0;
            return Math.Max(0, Math.Min(100, homogeneity));
        }

        private double CalculateTemperature(double progress, double currentSpeed)
        {
            double speedRatio = currentSpeed / FinalSpeedRpm;
            double maxTempRise = 1.5 * speedRatio;
            double tempRise = maxTempRise * (1 - Math.Exp(-3 * progress));
            double temperature = AmbientTemperature + tempRise;
            temperature += (_rand.NextDouble() - 0.5) * 0.2;
            return temperature;
        }

        private (double Torque, double Power, double Amperage, double Vibration) CalculateMotorParameters(
            double currentSpeed, double currentViscosity, double currentHomogeneity)
        {
            double mixerLevel = Convert.ToDouble(BatchPlantNodeManager.LT201.Value);
            double viscosityFactor = currentViscosity / 10000.0;
            double volumeFactor = mixerLevel / 500.0;
            double normalizedSpeed = currentSpeed / 150.0;

            double baseTorque = 50.0;
            double torque = baseTorque * (1 + viscosityFactor) * (0.5 + 0.5 * volumeFactor) * (0.3 + 0.7 * normalizedSpeed);
            torque += (_rand.NextDouble() - 0.5) * 2.0;

            double angularVelocity = currentSpeed * 2 * Math.PI / 60.0;
            double power = (torque * angularVelocity) / 1000.0;
            power += (_rand.NextDouble() - 0.5) * 0.1;

            double voltage = 400.0;
            double powerFactor = 0.85;
            double amperage = (power * 1000) / (Math.Sqrt(3) * voltage * powerFactor);
            amperage += (_rand.NextDouble() - 0.5) * 0.5;

            double baseVibration = 2.0;
            double inhomogeneityFactor = 1 - (currentHomogeneity / 100.0);
            double vibration = baseVibration * normalizedSpeed * (0.5 + 0.5 * inhomogeneityFactor);
            vibration += (_rand.NextDouble() - 0.5) * 0.2;

            return (torque, power, amperage, vibration);
        }

        private void UpdateMotorParameters((double Torque, double Power, double Amperage, double Vibration) motorParams)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_TRQ, Math.Round(motorParams.Torque, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_PWR, Math.Round(motorParams.Power, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_AMP, Math.Round(motorParams.Amperage, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_VIB, Math.Round(motorParams.Vibration, 3));
        }

        private void CheckAlarms(double amperage, double currentViscosity, double vibration, double progress)
        {
            // ═══════════════════════════════════════════════════════════════════════
            // OPC UA ALARM CHECKS (Part 9 Alarms & Conditions)
            // These trigger proper OPC UA alarm events that clients can subscribe to
            // ═══════════════════════════════════════════════════════════════════════

            // Check motor current for overload conditions (ALM-001, ALM-002)
            AlarmHelper.CheckMixerMotorCurrent(amperage);

            // Check vibration levels (ALM-004, ALM-005)
            AlarmHelper.CheckMixerVibration(vibration);

            // Check viscosity deviation after 67% of mixing cycle (ALM-003)
            if (progress > 0.67)
            {
                AlarmHelper.CheckViscosityDeviation(currentViscosity, TargetViscosity);
            }

            // ═══════════════════════════════════════════════════════════════════════
            // CONSOLE LOGGING (kept for operator visibility)
            // ═══════════════════════════════════════════════════════════════════════

            // Motor overload warning (console only - OPC UA alarm handled above)
            if (amperage > 150)
            {
                Console.WriteLine($"[ALARM ALM-001] Mixer motor overload: {amperage:F1} A");
            }
            if (amperage > 175)
            {
                Console.WriteLine($"[ALARM ALM-002] CRITICAL: Mixer motor critical overload: {amperage:F1} A");
            }

            // Viscosity deviation warning
            if (progress > 0.67)
            {
                double viscosityDeviation = Math.Abs(currentViscosity - TargetViscosity) / TargetViscosity * 100;
                if (viscosityDeviation > 15)
                {
                    Console.WriteLine($"[ALARM ALM-003] Viscosity deviation: {viscosityDeviation:F1}%");
                }
            }

            // Vibration warnings
            if (vibration > 5.0 && vibration <= 8.0)
            {
                Console.WriteLine($"[WARNING ALM-004] High mixer vibration: {vibration:F2} mm/s");
            }
            else if (vibration > 8.0)
            {
                Console.WriteLine($"[ALARM ALM-005] CRITICAL: Mixer critical vibration: {vibration:F2} mm/s");
            }
        }

        private void LogProgress(int step, int totalSteps, double elapsedMinutes, double currentSpeed,
            double currentViscosity, double currentHomogeneity, double currentTemperature, double power)
        {
            if (step % 10 == 0 || step == totalSteps)
            {
                Console.WriteLine($"[{elapsedMinutes:F1} min] RPM: {currentSpeed:F0} | " +
                                $"Visc: {currentViscosity:F0} cP | " +
                                $"Homog: {currentHomogeneity:F1}% | " +
                                $"Temp: {currentTemperature:F1}°C | " +
                                $"Power: {power:F2} kW");
            }

            if (step == totalSteps / 2)
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.mixer_status, "Mixing - Midpoint");
                Console.WriteLine(">>> Mixing midpoint reached");
            }
            else if (step == (int)(totalSteps * 0.8))
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.mixer_status, "Mixing - Final Stage");
                Console.WriteLine(">>> Entering final mixing stage");
            }
        }

        private void FinalizeMixerState(double currentViscosity, double currentHomogeneity, double energyConsumed,
            double finalTemperature, double avgSpeed, double avgPower, double maxPower, double mixDurationSeconds)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_RPM, 30.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.MX101_STATE, 1);
            UaWriteHelpers.Write(BatchPlantNodeManager.mixer_status, "Mixing Complete");
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Mixing Complete - Ready for Heating");

            // Write report parameters
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_AVG_AGITATOR_SPEED, Math.Round(avgSpeed, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_AVG_MOTOR_POWER, Math.Round(avgPower, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_FINAL_HOMOGENEITY, Math.Round(currentHomogeneity, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_FINAL_TEMPERATURE, Math.Round(finalTemperature, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_FINAL_TURBIDITY, Math.Round(currentHomogeneity, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_FINAL_VISCOSITY, Math.Round(currentViscosity, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_MAX_MOTOR_POWER, Math.Round(maxPower, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.MX_RPT_MIX_DURATION, Math.Round(mixDurationSeconds, 1));

            Console.WriteLine("=== Stage 1: Initial Mixing Complete ===");
            Console.WriteLine($"Final Viscosity: {currentViscosity:F0} cP");
            Console.WriteLine($"Final Homogeneity: {currentHomogeneity:F1}%");
            Console.WriteLine($"Final Temperature: {finalTemperature:F1}°C");
            Console.WriteLine($"Avg Speed: {avgSpeed:F1} RPM | Avg Power: {avgPower:F2} kW | Max Power: {maxPower:F2} kW");
            Console.WriteLine($"Mix Duration: {mixDurationSeconds:F0}s | Energy Consumed: {energyConsumed:F3} kWh");

            // Print alarm summary
            var (total, active, unacked, unconfirmed) = AlarmHelper.GetSummary();
            if (active > 0)
            {
                Console.WriteLine($">>> Active Alarms: {active} ({unacked} unacknowledged)");
            }

            Console.WriteLine(">>> Proceeding to Stage 2: Heating");
        }
    }
}