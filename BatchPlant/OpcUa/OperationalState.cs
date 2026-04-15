namespace BatchPlant.OpcUa
{
    /// <summary>
    /// PackML / Weihenstephan Standard operational state values.
    /// Used by packaging-line equipment (Areas 400/500/600) to expose OEE-relevant state.
    ///
    /// These states answer "WHY is this machine not producing?" which is essential for
    /// availability/OEE calculations and bottleneck analysis. Starved and Blocked are
    /// particularly important because they distinguish equipment faults from upstream/
    /// downstream constraints.
    ///
    /// Reference: ISA-TR88.00.02 (PackML), Weihenstephan Standard for packaging machines.
    /// </summary>
    public enum OperationalState
    {
        /// <summary>Equipment is powered off or out of service. Excluded from planned production time.</summary>
        Disabled = 0,

        /// <summary>Equipment is healthy, ready, but has no command to run. Counts as availability loss.</summary>
        Idle = 1,

        /// <summary>Equipment is producing normally. Counts as uptime.</summary>
        Running = 2,

        /// <summary>No upstream material available (e.g., filler waiting for buffer tank). External downtime.</summary>
        Starved = 3,

        /// <summary>Downstream is full (e.g., labeler blocked by jammed case packer). External downtime.</summary>
        Blocked = 4,

        /// <summary>Scheduled maintenance, CIP, changeover, planned stop. Planned downtime.</summary>
        PlannedDowntime = 5,

        /// <summary>Equipment fault, jam, breakdown. Unplanned downtime.</summary>
        UnplannedDowntime = 6,

        /// <summary>Catch-all for states that don't fit the other categories.</summary>
        Other = 7
    }

    public static class OperationalStateExtensions
    {
        public static string ToText(this OperationalState state) => state switch
        {
            OperationalState.Disabled => "Disabled",
            OperationalState.Idle => "Idle",
            OperationalState.Running => "Running",
            OperationalState.Starved => "Starved",
            OperationalState.Blocked => "Blocked",
            OperationalState.PlannedDowntime => "Planned Downtime",
            OperationalState.UnplannedDowntime => "Unplanned Downtime",
            OperationalState.Other => "Other",
            _ => "Unknown"
        };
    }
}
