using System.Collections.Concurrent;

namespace BatchPlant.Api
{
    /// <summary>
    /// Internal instruction produced by marrying a Control Recipe with a Work Order Dispatch.
    /// This is what the area controllers consume to execute a batch.
    /// </summary>
    public class BatchInstruction
    {
        public string BatchId { get; set; } = "";
        public string WorkOrderId { get; set; } = "";
        public string ControlRecipeNumber { get; set; } = "";
        public string ProcessOrderNumber { get; set; } = "";
        public string ProductCode { get; set; } = "";
        public double BatchSizeKg { get; set; }
        public double ScaleFactor { get; set; } = 1.0;

        // Customer
        public string CustomerId { get; set; } = "";
        public string CustomerName { get; set; } = "";

        // ISA-95 context
        public string Enterprise { get; set; } = "";
        public string Site { get; set; } = "";
        public string Area { get; set; } = "";
        public string ProcessCell { get; set; } = "";

        // Master recipe reference
        public string MasterRecipeId { get; set; } = "";
        public string MasterRecipeVersion { get; set; } = "";

        // Scheduling
        public DateTime? ScheduledAt { get; set; }
        public string BatchRecordId { get; set; } = "";

        // Equipment binding
        public string PreMixUnit { get; set; } = "MX-201";
        public string CookUnit { get; set; } = "KT-301";
        public string DeaerateUnit { get; set; } = "DA-401";
        public string HomogenizeUnit { get; set; } = "HG-401";
        public string BufferUnit { get; set; } = "BT-452";

        // PreMix parameters
        public double WaterTargetKg { get; set; } = 630;
        public double PasteTargetKg { get; set; } = 945;
        public double VinegarTargetKg { get; set; } = 378;
        public double SugarKg { get; set; } = 693;
        public double SaltKg { get; set; } = 78.75;
        public double SpiceKg { get; set; } = 47.25;
        public double StabilizerKg { get; set; } = 31.5;
        public double MixSpeedRpm { get; set; } = 1450;
        public double MixDurationSec { get; set; } = 180;

        // Cook parameters
        public double CookTargetTempC { get; set; } = 90.0;
        public double CookRampRateCPerMin { get; set; } = 2.5;
        public double HoldDurationSec { get; set; } = 300;
        public double HoldMinTempC { get; set; } = 88.0;
        public double CoolTargetTempC { get; set; } = 45.0;

        // Processing parameters
        public double VacuumTargetMbar { get; set; } = 150;
        public double DeaerateTimeSec { get; set; } = 180;
        public double HomogPressureBar { get; set; } = 200;
        public int HomogPassCount { get; set; } = 1;

        // Buffer parameters
        public double MinTransferTempC { get; set; } = 40.0;
        public double MaxBufferHoldTimeMin { get; set; } = 120;

        // BOM with lot tracking
        public List<BomLine> Bom { get; set; } = new();

        // Timestamps
        public DateTime ReceivedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class BomLine
    {
        public string Ingredient { get; set; } = "";
        public string Lot { get; set; } = "";
        public double QtyKg { get; set; }
        public string SourceUnit { get; set; } = "";
    }

    /// <summary>
    /// Thread-safe queue of pending batch instructions.
    /// Area controllers dequeue when ready to start a new batch.
    /// </summary>
    public class WorkOrderQueue
    {
        private readonly ConcurrentQueue<BatchInstruction> _queue = new();
        private BatchInstruction? _current;

        public int PendingCount => _queue.Count;
        public BatchInstruction? Current => _current;

        public void Enqueue(BatchInstruction instruction)
        {
            _queue.Enqueue(instruction);
        }

        public BatchInstruction? TryDequeue()
        {
            if (_queue.TryDequeue(out var instruction))
            {
                _current = instruction;
                return instruction;
            }
            return null;
        }

        public BatchInstruction? Peek()
        {
            _queue.TryPeek(out var instruction);
            return instruction;
        }

        public void MarkComplete(DateTime simNow)
        {
            if (_current != null)
            {
                _current.CompletedAt = simNow;
                _current = null;
            }
        }
    }
}
