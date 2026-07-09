using System.Collections.Concurrent;

namespace BatchPlant.Api
{
    /// <summary>
    /// A packaging work order dispatched to the pack line. The make/pack split:
    /// a Process Order cooks bulk into the buffer tanks; a separate Packaging Order
    /// draws that buffered bulk and packs it into finished cases of a distinct SKU.
    /// Gates Area 500 — the line stays idle until one of these is dequeued.
    /// </summary>
    public class PackagingInstruction
    {
        public string PackagingOrderNumber { get; set; } = "";
        public string WorkOrderId { get; set; } = "";
        public string ControlRecipeNumber { get; set; } = "";
        public string Sku { get; set; } = "";
        public string SourceBulkMaterial { get; set; } = "";
        public string UnitOfMeasure { get; set; } = "CASE";

        public double TargetCases { get; set; }
        public int BottlesPerCase { get; set; } = 24;
        public int CasesPerPallet { get; set; } = 60;

        // Customer
        public string CustomerId { get; set; } = "";
        public string CustomerName { get; set; } = "";

        // ISA-95 context (drives the UNS topic — PackagingArea / Line1)
        public string Enterprise { get; set; } = "GlobalIndustries";
        public string Site { get; set; } = "Munich";
        public string Area { get; set; } = "PackagingArea";
        public string ProcessCell { get; set; } = "Line1";

        public DateTime ReceivedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// One line of material genealogy: a bulk batch (identified by its produced batch
    /// number) that a packaging order drew from, with how many bottles/cases it yielded.
    /// </summary>
    public class ConsumedBatch
    {
        public string ProducedBatch { get; set; } = "";
        public string BulkMaterial { get; set; } = "";
        public double BottlesDrawn { get; set; }
        public double CasesProduced { get; set; }
    }

    /// <summary>
    /// Thread-safe queue of pending packaging instructions. Area 500 dequeues one at a
    /// time and runs it to completion before pulling the next.
    /// </summary>
    public class PackagingOrderQueue
    {
        private readonly ConcurrentQueue<PackagingInstruction> _queue = new();
        private PackagingInstruction? _current;

        public int PendingCount => _queue.Count;
        public PackagingInstruction? Current => _current;

        public void Enqueue(PackagingInstruction instruction) => _queue.Enqueue(instruction);

        public PackagingInstruction? TryDequeue()
        {
            if (_queue.TryDequeue(out var instruction))
            {
                _current = instruction;
                return instruction;
            }
            return null;
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
