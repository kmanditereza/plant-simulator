namespace BatchPlant.Alarms
{
    /// <summary>
    /// Defines all alarms for the Ketchup Batch Plant.
    /// Alarms are organized by equipment/stage.
    /// </summary>
    public static class PlantAlarms
    {
        // ═══════════════════════════════════════════════════════════════════════
        // MIXER ALARMS (Stage 1) - ALM-001 to ALM-099
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ALM-001: Mixer motor overload - current exceeds 150A
        /// </summary>
        public static readonly AlarmDefinition MixerMotorOverload = new()
        {
            AlarmId = "ALM-001",
            Name = "Mixer Motor Overload",
            Description = "Mixer motor current has exceeded the maximum safe operating limit",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "MT201_AMP",
            HighLimit = 150.0,
            HighHighLimit = 175.0,
            Hysteresis = 5.0,
            Severity = AlarmSeverity.High,
            Area = "Production",
            Unit = "MX101",
            EngineeringUnits = "A",
            RequiresAcknowledgment = true,
            RequiresConfirmation = false,
            OnDelay = 2.0,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} exceeds limit of {limit} {unit}"
        };

        /// <summary>
        /// ALM-002: Mixer motor critical overload - current exceeds 175A
        /// </summary>
        public static readonly AlarmDefinition MixerMotorCriticalOverload = new()
        {
            AlarmId = "ALM-002",
            Name = "Mixer Motor Critical Overload",
            Description = "Mixer motor current has reached critical level - immediate action required",
            AlarmType = LimitAlarmType.HighHigh,
            SourceNodeName = "MT201_AMP",
            HighHighLimit = 175.0,
            Hysteresis = 5.0,
            Severity = AlarmSeverity.Critical,
            Area = "Production",
            Unit = "MX101",
            EngineeringUnits = "A",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            OnDelay = 0.0,
            MessageTemplate = "[{alarmId}] CRITICAL: {name}: {value} {unit} - IMMEDIATE ACTION REQUIRED"
        };

        /// <summary>
        /// ALM-003: Viscosity deviation from target
        /// </summary>
        public static readonly AlarmDefinition ViscosityDeviation = new()
        {
            AlarmId = "ALM-003",
            Name = "Viscosity Deviation",
            Description = "Product viscosity has deviated more than 15% from target value",
            AlarmType = LimitAlarmType.Deviation,
            SourceNodeName = "VISC201",
            Setpoint = 8000.0,
            DeviationLimit = 15.0,
            DeviationIsPercent = true,
            Hysteresis = 2.0,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "MX101",
            EngineeringUnits = "cP",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} deviates from target"
        };

        /// <summary>
        /// ALM-004: High mixer vibration
        /// </summary>
        public static readonly AlarmDefinition MixerHighVibration = new()
        {
            AlarmId = "ALM-004",
            Name = "Mixer High Vibration",
            Description = "Mixer vibration level exceeds acceptable limit",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "MT201_VIB",
            HighLimit = 5.0,
            HighHighLimit = 8.0,
            Hysteresis = 0.5,
            Severity = AlarmSeverity.MediumHigh,
            Area = "Production",
            Unit = "MX101",
            EngineeringUnits = "mm/s",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} exceeds limit of {limit} {unit}"
        };

        /// <summary>
        /// ALM-005: Critical mixer vibration
        /// </summary>
        public static readonly AlarmDefinition MixerCriticalVibration = new()
        {
            AlarmId = "ALM-005",
            Name = "Mixer Critical Vibration",
            Description = "Mixer vibration has reached critical level - mechanical failure risk",
            AlarmType = LimitAlarmType.HighHigh,
            SourceNodeName = "MT201_VIB",
            HighHighLimit = 8.0,
            Hysteresis = 0.5,
            Severity = AlarmSeverity.Critical,
            Area = "Production",
            Unit = "MX101",
            EngineeringUnits = "mm/s",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            MessageTemplate = "[{alarmId}] CRITICAL: {name}: {value} {unit} - STOP MIXER IMMEDIATELY"
        };

        // ═══════════════════════════════════════════════════════════════════════
        // HEAT EXCHANGER ALARMS (Stage 2) - ALM-100 to ALM-199
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ALM-100: Heat exchanger fouling detected
        /// </summary>
        public static readonly AlarmDefinition HeatExchangerFouling = new()
        {
            AlarmId = "ALM-100",
            Name = "Heat Exchanger Fouling",
            Description = "Pressure drop increase indicates heat exchanger fouling",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "HX_FOULING_INDEX",
            HighLimit = 30.0,
            HighHighLimit = 50.0,
            Hysteresis = 5.0,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "HX301",
            EngineeringUnits = "%",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: Fouling index at {value}% exceeds {limit}%"
        };

        /// <summary>
        /// ALM-101: Critical heat exchanger fouling
        /// </summary>
        public static readonly AlarmDefinition HeatExchangerCriticalFouling = new()
        {
            AlarmId = "ALM-101",
            Name = "Heat Exchanger Critical Fouling",
            Description = "Severe fouling detected - cleaning required",
            AlarmType = LimitAlarmType.HighHigh,
            SourceNodeName = "HX_FOULING_INDEX",
            HighHighLimit = 50.0,
            Hysteresis = 5.0,
            Severity = AlarmSeverity.High,
            Area = "Production",
            Unit = "HX301",
            EngineeringUnits = "%",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            MessageTemplate = "[{alarmId}] {name}: Fouling index at {value}% - CLEANING REQUIRED"
        };

        /// <summary>
        /// ALM-102: Low product flow rate through heat exchanger
        /// </summary>
        public static readonly AlarmDefinition LowProductFlow = new()
        {
            AlarmId = "ALM-102",
            Name = "Low Product Flow",
            Description = "Product flow rate below minimum safe limit - hot spot risk",
            AlarmType = LimitAlarmType.Low,
            SourceNodeName = "FT301",
            LowLimit = 18.0,
            LowLowLimit = 12.0,
            Hysteresis = 1.0,
            Severity = AlarmSeverity.High,
            Area = "Production",
            Unit = "HX301",
            EngineeringUnits = "L/min",
            RequiresAcknowledgment = true,
            OnDelay = 3.0,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} below minimum of {limit} {unit} - HOT SPOT RISK"
        };

        /// <summary>
        /// ALM-103: Critical low product flow
        /// </summary>
        public static readonly AlarmDefinition CriticalLowProductFlow = new()
        {
            AlarmId = "ALM-103",
            Name = "Critical Low Product Flow",
            Description = "Product flow critically low - reduce steam immediately",
            AlarmType = LimitAlarmType.LowLow,
            SourceNodeName = "FT301",
            LowLowLimit = 12.0,
            Hysteresis = 1.0,
            Severity = AlarmSeverity.Critical,
            Area = "Production",
            Unit = "HX301",
            EngineeringUnits = "L/min",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            MessageTemplate = "[{alarmId}] CRITICAL: {name}: {value} {unit} - REDUCE STEAM IMMEDIATELY"
        };

        /// <summary>
        /// ALM-104: Temperature overshoot
        /// </summary>
        public static readonly AlarmDefinition TemperatureOvershoot = new()
        {
            AlarmId = "ALM-104",
            Name = "Temperature Overshoot",
            Description = "Product temperature has exceeded target setpoint",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "TT201",
            HighLimit = 91.0,
            HighHighLimit = 95.0,
            Hysteresis = 0.5,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "HX301",
            EngineeringUnits = "°C",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} exceeds target"
        };

        /// <summary>
        /// ALM-105: Critical high temperature
        /// </summary>
        public static readonly AlarmDefinition CriticalHighTemperature = new()
        {
            AlarmId = "ALM-105",
            Name = "Critical High Temperature",
            Description = "Product temperature critically high - product damage risk",
            AlarmType = LimitAlarmType.HighHigh,
            SourceNodeName = "TT201",
            HighHighLimit = 95.0,
            Hysteresis = 0.5,
            Severity = AlarmSeverity.Critical,
            Area = "Production",
            Unit = "HX301",
            EngineeringUnits = "°C",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            MessageTemplate = "[{alarmId}] CRITICAL: {name}: {value} {unit} - PRODUCT DAMAGE RISK"
        };

        // ═══════════════════════════════════════════════════════════════════════
        // REACTOR ALARMS (Stage 3) - ALM-200 to ALM-299
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// ALM-200: Reactor overpressure warning
        /// </summary>
        public static readonly AlarmDefinition ReactorOverpressure = new()
        {
            AlarmId = "ALM-200",
            Name = "Reactor Overpressure",
            Description = "Reactor pressure exceeds normal operating limit",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "PT401",
            HighLimit = 1.2,
            HighHighLimit = 1.5,
            Hysteresis = 0.05,
            Severity = AlarmSeverity.High,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "bar",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} exceeds limit of {limit} {unit}"
        };

        /// <summary>
        /// ALM-201: Reactor critical overpressure
        /// </summary>
        public static readonly AlarmDefinition ReactorCriticalOverpressure = new()
        {
            AlarmId = "ALM-201",
            Name = "Reactor Critical Overpressure",
            Description = "Reactor pressure at critical level - safety risk",
            AlarmType = LimitAlarmType.HighHigh,
            SourceNodeName = "PT401",
            HighHighLimit = 1.5,
            Hysteresis = 0.05,
            Severity = AlarmSeverity.Critical,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "bar",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            OnDelay = 0.0,
            MessageTemplate = "[{alarmId}] CRITICAL: {name}: {value} {unit} - SAFETY RISK"
        };

        /// <summary>
        /// ALM-202: pH high - microbial safety risk
        /// </summary>
        public static readonly AlarmDefinition PHHigh = new()
        {
            AlarmId = "ALM-202",
            Name = "pH High",
            Description = "Product pH exceeds safe limit - microbial growth risk",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "pH401",
            HighLimit = 4.4,
            HighHighLimit = 4.6,
            Hysteresis = 0.05,
            Severity = AlarmSeverity.High,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "pH",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} exceeds {limit} - MICROBIAL RISK"
        };

        /// <summary>
        /// ALM-203: pH critical high - product unsafe
        /// </summary>
        public static readonly AlarmDefinition PHCriticalHigh = new()
        {
            AlarmId = "ALM-203",
            Name = "pH Critical High",
            Description = "Product pH at unsafe level - batch may require rejection",
            AlarmType = LimitAlarmType.HighHigh,
            SourceNodeName = "pH401",
            HighHighLimit = 4.6,
            Hysteresis = 0.05,
            Severity = AlarmSeverity.Critical,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "pH",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            MessageTemplate = "[{alarmId}] CRITICAL: {name}: {value} - PRODUCT MAY BE UNSAFE"
        };

        /// <summary>
        /// ALM-204: pH low - excessive acidity
        /// </summary>
        public static readonly AlarmDefinition PHLow = new()
        {
            AlarmId = "ALM-204",
            Name = "pH Low",
            Description = "Product pH below target - excessive acidity",
            AlarmType = LimitAlarmType.Low,
            SourceNodeName = "pH401",
            LowLimit = 3.8,
            LowLowLimit = 3.5,
            Hysteresis = 0.05,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "pH",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} below {limit} - EXCESSIVE ACIDITY"
        };

        /// <summary>
        /// ALM-205: Brix out of range high
        /// </summary>
        public static readonly AlarmDefinition BrixHigh = new()
        {
            AlarmId = "ALM-205",
            Name = "Brix High",
            Description = "Product Brix exceeds target range - over-concentration",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "BRIX401",
            HighLimit = 14.0,
            HighHighLimit = 16.0,
            Hysteresis = 0.3,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "°Bx",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} exceeds target range"
        };

        /// <summary>
        /// ALM-206: Brix out of range low
        /// </summary>
        public static readonly AlarmDefinition BrixLow = new()
        {
            AlarmId = "ALM-206",
            Name = "Brix Low",
            Description = "Product Brix below target range - under-concentration",
            AlarmType = LimitAlarmType.Low,
            SourceNodeName = "BRIX401",
            LowLimit = 10.0,
            LowLowLimit = 8.0,
            Hysteresis = 0.3,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "°Bx",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} below target range"
        };

        /// <summary>
        /// ALM-207: Color drift detected
        /// </summary>
        public static readonly AlarmDefinition ColorDrift = new()
        {
            AlarmId = "ALM-207",
            Name = "Color Drift",
            Description = "Product color has drifted from target (Delta-E > 3.0)",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "color_delta_e",
            HighLimit = 3.0,
            HighHighLimit = 5.0,
            Hysteresis = 0.3,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "ΔE",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: ΔE = {value} exceeds limit of {limit}"
        };

        /// <summary>
        /// ALM-208: Significant color drift
        /// </summary>
        public static readonly AlarmDefinition SignificantColorDrift = new()
        {
            AlarmId = "ALM-208",
            Name = "Significant Color Drift",
            Description = "Product color significantly different from target",
            AlarmType = LimitAlarmType.HighHigh,
            SourceNodeName = "color_delta_e",
            HighHighLimit = 5.0,
            Hysteresis = 0.3,
            Severity = AlarmSeverity.High,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "ΔE",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: ΔE = {value} - SIGNIFICANT DEVIATION"
        };

        /// <summary>
        /// ALM-209: Reactor high temperature
        /// </summary>
        public static readonly AlarmDefinition ReactorHighTemp = new()
        {
            AlarmId = "ALM-209",
            Name = "Reactor High Temperature",
            Description = "Reactor temperature exceeds setpoint",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "TT401",
            HighLimit = 92.0,
            HighHighLimit = 95.0,
            Hysteresis = 0.5,
            Severity = AlarmSeverity.MediumHigh,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "°C",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} exceeds setpoint"
        };

        /// <summary>
        /// ALM-210: Reactor low temperature
        /// </summary>
        public static readonly AlarmDefinition ReactorLowTemp = new()
        {
            AlarmId = "ALM-210",
            Name = "Reactor Low Temperature",
            Description = "Reactor temperature below required minimum for reaction",
            AlarmType = LimitAlarmType.Low,
            SourceNodeName = "TT401",
            LowLimit = 82.0,
            LowLowLimit = 75.0,
            Hysteresis = 0.5,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "°C",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: {value} {unit} below minimum for reaction"
        };

        /// <summary>
        /// ALM-211: Vacuum loss
        /// </summary>
        public static readonly AlarmDefinition VacuumLoss = new()
        {
            AlarmId = "ALM-211",
            Name = "Vacuum Loss",
            Description = "Reactor vacuum pressure above target - concentration affected",
            AlarmType = LimitAlarmType.High,
            SourceNodeName = "PT401",
            HighLimit = -0.1,
            Hysteresis = 0.02,
            Severity = AlarmSeverity.Medium,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "bar",
            RequiresAcknowledgment = true,
            MessageTemplate = "[{alarmId}] {name}: Pressure at {value} {unit} - vacuum insufficient"
        };

        /// <summary>
        /// ALM-212: Reactor agitator fault
        /// </summary>
        public static readonly AlarmDefinition ReactorAgitatorFault = new()
        {
            AlarmId = "ALM-212",
            Name = "Reactor Agitator Fault",
            Description = "Reactor agitator motor has reported a fault",
            AlarmType = LimitAlarmType.OffNormal,
            SourceNodeName = "MT401_FAULT",
            Severity = AlarmSeverity.High,
            Area = "Production",
            Unit = "RX401",
            EngineeringUnits = "",
            RequiresAcknowledgment = true,
            RequiresConfirmation = true,
            MessageTemplate = "[{alarmId}] {name} - Check motor and restart"
        };

        // ═══════════════════════════════════════════════════════════════════════
        // HELPER: Get all alarm definitions
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets all defined alarms organized by stage.
        /// </summary>
        public static IEnumerable<AlarmDefinition> GetAllAlarms()
        {
            return new[]
            {
                // Mixer alarms
                MixerMotorOverload,
                MixerMotorCriticalOverload,
                ViscosityDeviation,
                MixerHighVibration,
                MixerCriticalVibration,
                // Heat exchanger alarms
                HeatExchangerFouling,
                HeatExchangerCriticalFouling,
                LowProductFlow,
                CriticalLowProductFlow,
                TemperatureOvershoot,
                CriticalHighTemperature,
                // Reactor alarms
                ReactorOverpressure,
                ReactorCriticalOverpressure,
                PHHigh,
                PHCriticalHigh,
                PHLow,
                BrixHigh,
                BrixLow,
                ColorDrift,
                SignificantColorDrift,
                ReactorHighTemp,
                ReactorLowTemp,
                VacuumLoss,
                ReactorAgitatorFault
            };
        }

        /// <summary>
        /// Gets mixer alarms only.
        /// </summary>
        public static IEnumerable<AlarmDefinition> GetMixerAlarms()
        {
            return new[]
            {
                MixerMotorOverload,
                MixerMotorCriticalOverload,
                ViscosityDeviation,
                MixerHighVibration,
                MixerCriticalVibration
            };
        }

        /// <summary>
        /// Gets heat exchanger alarms only.
        /// </summary>
        public static IEnumerable<AlarmDefinition> GetHeatExchangerAlarms()
        {
            return new[]
            {
                HeatExchangerFouling,
                HeatExchangerCriticalFouling,
                LowProductFlow,
                CriticalLowProductFlow,
                TemperatureOvershoot,
                CriticalHighTemperature
            };
        }

        /// <summary>
        /// Gets reactor alarms only.
        /// </summary>
        public static IEnumerable<AlarmDefinition> GetReactorAlarms()
        {
            return new[]
            {
                ReactorOverpressure,
                ReactorCriticalOverpressure,
                PHHigh,
                PHCriticalHigh,
                PHLow,
                BrixHigh,
                BrixLow,
                ColorDrift,
                SignificantColorDrift,
                ReactorHighTemp,
                ReactorLowTemp,
                VacuumLoss,
                ReactorAgitatorFault
            };
        }
    }
}