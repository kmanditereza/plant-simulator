using Opc.Ua;

namespace BatchPlant.Alarms
{
    /// <summary>
    /// Defines the type of limit alarm.
    /// </summary>
    public enum LimitAlarmType
    {
        /// <summary>High limit only (value > limit triggers alarm)</summary>
        High,
        /// <summary>Low limit only (value &lt; limit triggers alarm)</summary>
        Low,
        /// <summary>High-High limit (critical high)</summary>
        HighHigh,
        /// <summary>Low-Low limit (critical low)</summary>
        LowLow,
        /// <summary>Deviation from setpoint</summary>
        Deviation,
        /// <summary>Rate of change exceeded</summary>
        RateOfChange,
        /// <summary>Discrete/off-normal condition</summary>
        OffNormal
    }

    /// <summary>
    /// Defines severity levels for alarms (OPC UA range: 1-1000).
    /// </summary>
    public static class AlarmSeverity
    {
        public const ushort Low = 200;
        public const ushort Medium = 500;
        public const ushort MediumHigh = 700;
        public const ushort High = 850;
        public const ushort Critical = 1000;
    }

    /// <summary>
    /// Defines an alarm configuration for the batch plant.
    /// </summary>
    public class AlarmDefinition
    {
        /// <summary>
        /// Unique identifier for this alarm (e.g., "ALM-001").
        /// </summary>
        public string AlarmId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the alarm.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Detailed description of the alarm condition.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The type of limit alarm.
        /// </summary>
        public LimitAlarmType AlarmType { get; set; } = LimitAlarmType.High;

        /// <summary>
        /// The source variable node ID (symbolic name in BatchPlantNodeManager).
        /// </summary>
        public string SourceNodeName { get; set; } = string.Empty;

        /// <summary>
        /// High-High limit value (if applicable).
        /// </summary>
        public double? HighHighLimit { get; set; }

        /// <summary>
        /// High limit value (if applicable).
        /// </summary>
        public double? HighLimit { get; set; }

        /// <summary>
        /// Low limit value (if applicable).
        /// </summary>
        public double? LowLimit { get; set; }

        /// <summary>
        /// Low-Low limit value (if applicable).
        /// </summary>
        public double? LowLowLimit { get; set; }

        /// <summary>
        /// Setpoint for deviation alarms.
        /// </summary>
        public double? Setpoint { get; set; }

        /// <summary>
        /// Deviation limit (percentage or absolute).
        /// </summary>
        public double? DeviationLimit { get; set; }

        /// <summary>
        /// Whether deviation is calculated as percentage (true) or absolute (false).
        /// </summary>
        public bool DeviationIsPercent { get; set; } = true;

        /// <summary>
        /// Hysteresis value to prevent alarm chatter.
        /// </summary>
        public double Hysteresis { get; set; } = 0.0;

        /// <summary>
        /// Alarm severity (1-1000, where 1000 is most severe).
        /// </summary>
        public ushort Severity { get; set; } = AlarmSeverity.Medium;

        /// <summary>
        /// The plant area this alarm belongs to.
        /// </summary>
        public string Area { get; set; } = string.Empty;

        /// <summary>
        /// The equipment unit this alarm belongs to.
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Engineering units for the source value.
        /// </summary>
        public string EngineeringUnits { get; set; } = string.Empty;

        /// <summary>
        /// Whether this alarm requires acknowledgment.
        /// </summary>
        public bool RequiresAcknowledgment { get; set; } = true;

        /// <summary>
        /// Whether this alarm requires confirmation after acknowledgment.
        /// </summary>
        public bool RequiresConfirmation { get; set; } = false;

        /// <summary>
        /// Time delay before alarm activates (seconds).
        /// </summary>
        public double OnDelay { get; set; } = 0.0;

        /// <summary>
        /// Time delay before alarm clears (seconds).
        /// </summary>
        public double OffDelay { get; set; } = 0.0;

        /// <summary>
        /// Whether this alarm is currently enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Message template for when alarm is active.
        /// Use {value}, {limit}, {unit} as placeholders.
        /// </summary>
        public string MessageTemplate { get; set; } = "{name} is in alarm state";

        /// <summary>
        /// Creates a formatted message for this alarm.
        /// </summary>
        public string FormatMessage(double currentValue, double? limitValue = null)
        {
            string message = MessageTemplate
                .Replace("{name}", Name)
                .Replace("{value}", currentValue.ToString("F2"))
                .Replace("{unit}", EngineeringUnits)
                .Replace("{limit}", limitValue?.ToString("F2") ?? "N/A")
                .Replace("{alarmId}", AlarmId);

            return message;
        }
    }
}