namespace BatchPlant.OpcUa
{
    /// <summary>
    /// PackML / Weihenstephan Standard operational MODE values.
    /// Used by packaging-line equipment (Areas 400/500/600) to expose the operating regime.
    ///
    /// Mode differs from State: Mode is what the machine is SET UP to do (deliberately chosen
    /// by operator/MES/automation, changes infrequently), while State is what it's actually
    /// DOING right now (computed from conditions, changes continuously).
    ///
    /// The combination Mode×State is what enables meaningful operational reporting:
    /// e.g. Mode=Production + State=Starved is completely different from
    /// Mode=Cleaning + State=Running, even though both are "not producing product."
    ///
    /// Reference: Weihenstephan Standard (WS Food/Pack), ISA-TR88.00.02 (PackML).
    /// </summary>
    public enum OperationalMode
    {
        /// <summary>Powered off, locked out, decommissioned. Excluded from scheduling.</summary>
        Disabled = 0,

        /// <summary>Powered and ready, but no command/mode selected yet. Availability loss.</summary>
        Idle = 1,

        /// <summary>Running a product recipe. Denominator for OEE calculation.</summary>
        Production = 2,

        /// <summary>SKU change, format change, recipe swap. Planned downtime (tracked for SMED).</summary>
        Changeover = 3,

        /// <summary>Work order open, mechanic on equipment. Planned downtime.</summary>
        Maintenance = 4,

        /// <summary>CIP/sanitation cycle. Planned downtime (tracked for compliance).</summary>
        Cleaning = 5,

        /// <summary>Catch-all: safety drill, audit, training, other non-production activity.</summary>
        Other = 6
    }

    public static class OperationalModeExtensions
    {
        public static string ToText(this OperationalMode mode) => mode switch
        {
            OperationalMode.Disabled => "Disabled",
            OperationalMode.Idle => "Idle",
            OperationalMode.Production => "Production",
            OperationalMode.Changeover => "Changeover",
            OperationalMode.Maintenance => "Maintenance",
            OperationalMode.Cleaning => "Cleaning",
            OperationalMode.Other => "Other",
            _ => "Unknown"
        };
    }
}
