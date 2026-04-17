namespace BatchPlant.Batch
{
    public enum BatchStage
    {
        PreMixing = 0,
        Cooking = 1,
        Processing = 2,
        Buffering = 3,
        Bottling = 4,
        Complete = 5
    }

    public enum BatchStatus
    {
        Running = 0,
        Complete = 1,
        Aborted = 2
    }

    /// <summary>
    /// A complete batch record. Accumulated throughout a batch's lifecycle and written out
    /// on completion. In a real plant this would be the Electronic Batch Record (EBR).
    /// </summary>
    public class BatchRecord
    {
        public string BatchId { get; set; } = "";
        public string ProductCode { get; set; } = "SKU-KETCHUP-14OZ";
        public string RecipeName { get; set; } = "Standard Ketchup 14oz";

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public BatchStatus Status { get; set; } = BatchStatus.Running;
        public BatchStage CurrentStage { get; set; } = BatchStage.PreMixing;

        // Equipment that processed this batch
        public List<string> EquipmentUsed { get; } = new();

        // All phase records accumulated as the batch progresses
        public List<PhaseRecord> Phases { get; } = new();

        // Aggregate fill statistics (populated during Bottling)
        public int GoodBottles { get; set; }
        public int RejectBottles { get; set; }
        public double AvgFillWeightG { get; set; }
        public double FillWeightStdDevG { get; set; }

        // Alarms and deviations recorded during this batch
        public List<string> Alarms { get; } = new();
        public List<string> Deviations { get; } = new();

        public double TotalDurationSec => EndTime.HasValue ? (EndTime.Value - StartTime).TotalSeconds : 0;
    }
}
