namespace BatchPlant.Alarms
{
    /// <summary>
    /// Static helper class for triggering alarms from controllers.
    /// Provides a simple interface to the AlarmConditionManager.
    /// </summary>
    public static class AlarmHelper
    {
        /// <summary>
        /// Checks a limit alarm condition and triggers/clears as needed.
        /// </summary>
        /// <param name="alarmId">The alarm ID (e.g., "ALM-001").</param>
        /// <param name="currentValue">Current value of the monitored variable.</param>
        /// <param name="setpoint">Optional setpoint for deviation alarms.</param>
        /// <returns>True if alarm is active, false otherwise.</returns>
        public static bool Check(string alarmId, double currentValue, double? setpoint = null)
        {
            var manager = AlarmConditionManager.Instance;
            if (manager == null)
            {
                return false;
            }

            return manager.CheckAlarm(alarmId, currentValue, setpoint);
        }

        /// <summary>
        /// Raises a discrete/off-normal alarm.
        /// </summary>
        /// <param name="alarmId">The alarm ID.</param>
        /// <param name="message">Optional custom message.</param>
        public static void Raise(string alarmId, string message = "")
        {
            var manager = AlarmConditionManager.Instance;
            manager?.RaiseAlarm(alarmId, message);
        }

        /// <summary>
        /// Clears a discrete/off-normal alarm.
        /// </summary>
        /// <param name="alarmId">The alarm ID.</param>
        public static void Clear(string alarmId)
        {
            var manager = AlarmConditionManager.Instance;
            manager?.ClearAlarm(alarmId);
        }

        /// <summary>
        /// Checks if an alarm is currently active.
        /// </summary>
        public static bool IsActive(string alarmId)
        {
            var manager = AlarmConditionManager.Instance;
            var state = manager?.GetAlarmState(alarmId);
            return state?.IsActive ?? false;
        }

        /// <summary>
        /// Checks if an alarm is active and unacknowledged.
        /// </summary>
        public static bool IsUnacknowledged(string alarmId)
        {
            var manager = AlarmConditionManager.Instance;
            var state = manager?.GetAlarmState(alarmId);
            return state != null && state.IsActive && !state.IsAcknowledged;
        }

        /// <summary>
        /// Gets the count of active unacknowledged alarms.
        /// </summary>
        public static int GetUnacknowledgedCount()
        {
            var manager = AlarmConditionManager.Instance;
            return manager?.GetUnacknowledgedCount() ?? 0;
        }

        /// <summary>
        /// Gets alarm summary statistics.
        /// </summary>
        public static (int Total, int Active, int Unacknowledged, int Unconfirmed) GetSummary()
        {
            var manager = AlarmConditionManager.Instance;
            return manager?.GetAlarmSummary() ?? (0, 0, 0, 0);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MIXER ALARM SHORTCUTS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks mixer motor current for overload conditions.
        /// </summary>
        public static void CheckMixerMotorCurrent(double amperage)
        {
            Check(PlantAlarms.MixerMotorOverload.AlarmId, amperage);
            Check(PlantAlarms.MixerMotorCriticalOverload.AlarmId, amperage);
        }

        /// <summary>
        /// Checks mixer vibration levels.
        /// </summary>
        public static void CheckMixerVibration(double vibration)
        {
            Check(PlantAlarms.MixerHighVibration.AlarmId, vibration);
            Check(PlantAlarms.MixerCriticalVibration.AlarmId, vibration);
        }

        /// <summary>
        /// Checks viscosity deviation from target.
        /// </summary>
        public static void CheckViscosityDeviation(double viscosity, double target = 8000.0)
        {
            Check(PlantAlarms.ViscosityDeviation.AlarmId, viscosity, target);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HEAT EXCHANGER ALARM SHORTCUTS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks heat exchanger fouling index.
        /// </summary>
        public static void CheckHxFouling(double foulingIndex)
        {
            Check(PlantAlarms.HeatExchangerFouling.AlarmId, foulingIndex);
            Check(PlantAlarms.HeatExchangerCriticalFouling.AlarmId, foulingIndex);
        }

        /// <summary>
        /// Checks product flow rate through heat exchanger.
        /// </summary>
        public static void CheckProductFlow(double flowRate)
        {
            Check(PlantAlarms.LowProductFlow.AlarmId, flowRate);
            Check(PlantAlarms.CriticalLowProductFlow.AlarmId, flowRate);
        }

        /// <summary>
        /// Checks heat exchanger outlet temperature.
        /// </summary>
        public static void CheckHxTemperature(double temperature)
        {
            Check(PlantAlarms.TemperatureOvershoot.AlarmId, temperature);
            Check(PlantAlarms.CriticalHighTemperature.AlarmId, temperature);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // REACTOR ALARM SHORTCUTS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks reactor pressure.
        /// </summary>
        public static void CheckReactorPressure(double pressure)
        {
            Check(PlantAlarms.ReactorOverpressure.AlarmId, pressure);
            Check(PlantAlarms.ReactorCriticalOverpressure.AlarmId, pressure);
        }

        /// <summary>
        /// Checks reactor pH levels.
        /// </summary>
        public static void CheckReactorPH(double ph)
        {
            Check(PlantAlarms.PHHigh.AlarmId, ph);
            Check(PlantAlarms.PHCriticalHigh.AlarmId, ph);
            Check(PlantAlarms.PHLow.AlarmId, ph);
        }

        /// <summary>
        /// Checks reactor Brix levels.
        /// </summary>
        public static void CheckReactorBrix(double brix)
        {
            Check(PlantAlarms.BrixHigh.AlarmId, brix);
            Check(PlantAlarms.BrixLow.AlarmId, brix);
        }

        /// <summary>
        /// Checks color drift (Delta-E).
        /// </summary>
        public static void CheckColorDrift(double deltaE)
        {
            Check(PlantAlarms.ColorDrift.AlarmId, deltaE);
            Check(PlantAlarms.SignificantColorDrift.AlarmId, deltaE);
        }

        /// <summary>
        /// Checks reactor temperature.
        /// </summary>
        public static void CheckReactorTemperature(double temperature)
        {
            Check(PlantAlarms.ReactorHighTemp.AlarmId, temperature);
            Check(PlantAlarms.ReactorLowTemp.AlarmId, temperature);
        }

        /// <summary>
        /// Checks reactor vacuum level.
        /// </summary>
        public static void CheckReactorVacuum(double pressure)
        {
            // Only check during vacuum operation (negative pressure expected)
            if (pressure < 0)
            {
                Check(PlantAlarms.VacuumLoss.AlarmId, pressure);
            }
        }

        /// <summary>
        /// Reports reactor agitator fault state.
        /// </summary>
        public static void CheckReactorAgitatorFault(bool isFaulted)
        {
            if (isFaulted)
            {
                Raise(PlantAlarms.ReactorAgitatorFault.AlarmId);
            }
            else
            {
                Clear(PlantAlarms.ReactorAgitatorFault.AlarmId);
            }
        }
    }
}