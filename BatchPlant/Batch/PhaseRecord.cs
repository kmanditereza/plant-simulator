namespace BatchPlant.Batch
{
    /// <summary>
    /// A single phase execution record. Captures start/end times and all collected
    /// report parameters for one phase within a batch.
    /// </summary>
    public class PhaseRecord
    {
        public string PhaseLabel { get; set; } = "";        // "PreMix.DoseWater"
        public string AreaName { get; set; } = "";           // "Area 200"
        public string UnitName { get; set; } = "";           // "MX-201"
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double DurationSec { get; set; }
        public Dictionary<string, double> ReportParameters { get; } = new();

        public bool IsComplete => EndTime.HasValue;
    }
}
