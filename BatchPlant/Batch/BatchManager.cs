using System.Text.Json;
using BatchPlant.Api;
using BatchPlant.Core;
using BatchPlant.OpcUa;

namespace BatchPlant.Batch
{
    /// <summary>
    /// Phase 4 Lite: Observational batch tracking.
    ///
    /// Does NOT drive the plant — the area controllers still run their own hardcoded state
    /// machines. Instead, this manager observes them, assigns batch IDs when new batches
    /// start, tracks them through the production chain, and collects report parameters via
    /// the PhaseObserverCatalog.
    ///
    /// On batch completion, writes a BatchRecord to console and (optionally) JSON file.
    ///
    /// Lifecycle:
    ///   1. Detect new batch: Area 200 transitions Idle -> FillingWater
    ///   2. Assign batch ID (B-YYYYMMDD-NNN based on sim date)
    ///   3. Track batch as it moves through Area 200, 300, 400 phases
    ///   4. For each phase transition: finalize prior PhaseRecord, start new one
    ///   5. On Area 400 completion (final Draining -> Idle): mark batch Complete, write record
    /// </summary>
    public class BatchManager : ISimulatable
    {
        private readonly PlantContext _ctx;
        private readonly string _outputDir;
        private Func<BatchInstruction?>? _getActiveInstruction;

        public int Priority => 50; // after area controllers, before tag flusher

        // Names of areas we're observing
        private const string Area200 = "Area 200";
        private const string Area300 = "Area 300";
        private const string Area400 = "Area 400";

        // Active batches, keyed by area (each area can host at most one batch at a time)
        private readonly Dictionary<string, ActiveBatch> _byArea = new();

        // Active phase trackers, keyed by (area, phase label)
        private readonly Dictionary<string, ActivePhaseTracker> _phaseTrackers = new();

        // Last known state per area (for transition detection)
        private readonly Dictionary<string, int> _lastAreaState = new()
        {
            [Area200] = 0,
            [Area300] = 0,
            [Area400] = 0
        };

        // State ID → name mapping (for observer catalog lookup)
        private static readonly Dictionary<int, string> Area200StateNames = new()
        {
            [0] = "Idle",
            [1] = "FillingWater",
            [2] = "DosingPaste",
            [3] = "DosingVinegar",
            [4] = "RampingAgitator",
            [5] = "Mixing",
            [6] = "RampingDown",
            [7] = "Discharging"
        };

        private static readonly Dictionary<int, string> Area300StateNames = new()
        {
            [0] = "Idle",
            [1] = "Filling",
            [2] = "HeatingUp",
            [3] = "Holding",
            [4] = "Cooling",
            [5] = "Discharging"
        };

        private static readonly Dictionary<int, string> Area400StateNames = new()
        {
            [0] = "Idle",
            [1] = "Filling",
            [2] = "PullingVacuum",
            [3] = "Deaerating",
            [4] = "Homogenizing",
            [5] = "VentingVacuum",
            [6] = "Draining"
        };

        // Batch counter for ID generation
        private int _batchCounterForDate;
        private DateTime _counterDate;
        private int _batchesToday;

        // Retain recent completed records for inspection
        private readonly List<BatchRecord> _completedRecords = new();
        private const int MaxRetainedRecords = 50;

        public BatchManager(PlantContext ctx, string? outputDir = null)
        {
            _ctx = ctx;
            _outputDir = outputDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "batch-records");
            try
            {
                Directory.CreateDirectory(_outputDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BATCH] Warning: could not create output dir {_outputDir}: {ex.Message}");
            }
        }

        /// <summary>
        /// Connect to the Area 200 controller's active instruction so we can use
        /// the work-order-assigned batch ID instead of auto-generating one.
        /// </summary>
        public void ConnectInstructionSource(Func<BatchInstruction?> getActiveInstruction)
        {
            _getActiveInstruction = getActiveInstruction;
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            // Reset daily counter on date rollover
            if (simNow.Date != _counterDate.Date)
            {
                _counterDate = simNow.Date;
                _batchCounterForDate = 0;
                _batchesToday = 0;
            }

            // Observe each area's state and handle transitions
            ObserveArea(Area200, "MX201_STATE", Area200StateNames, simNow, dt);
            ObserveArea(Area300, "KT301_STATE", Area300StateNames, simNow, dt);
            ObserveArea(Area400, "DA401_STATE", Area400StateNames, simNow, dt);

            // Sample active phase trackers
            foreach (var tracker in _phaseTrackers.Values)
            {
                tracker.Sample(simNow, ReadTagAsDouble);
            }

            PublishBatchTags(simNow);
        }

        private void ObserveArea(string areaName, string stateTag, Dictionary<int, string> nameMap, DateTime simNow, TimeSpan dt)
        {
            // Area 400 doesn't currently publish its state tag; fall back to 0 (Idle).
            // We still react when Area 400 is observed indirectly (see ProcessArea400 below).
            int current = _ctx.Tags.Read<int>(stateTag, 0);
            int last = _lastAreaState[areaName];

            if (current == last) return;
            _lastAreaState[areaName] = current;

            string prevName = nameMap.TryGetValue(last, out var pn) ? pn : "Unknown";
            string newName = nameMap.TryGetValue(current, out var nn) ? nn : "Unknown";

            // Finalize prior phase tracker for this area if present
            var priorKey = $"{areaName}|{prevName}";
            if (_phaseTrackers.TryGetValue(priorKey, out var priorTracker))
            {
                var finalized = priorTracker.Finalize(simNow, ReadTagAsDouble);
                if (_byArea.TryGetValue(areaName, out var batch))
                {
                    batch.Record.Phases.Add(finalized);
                }
                _phaseTrackers.Remove(priorKey);
            }

            // Handle specific area transitions
            if (areaName == Area200)
            {
                // New batch starts on Idle -> FillingWater
                if (prevName == "Idle" && newName == "FillingWater")
                {
                    var batch = StartNewBatch(simNow);
                    _byArea[Area200] = batch;
                    if (!batch.Record.EquipmentUsed.Contains("MX-201"))
                        batch.Record.EquipmentUsed.Add("MX-201");
                }

                // When Area 200 discharge completes, remove its association with the batch
                // (the batch has fully transferred to Area 300 by this point)
                if (prevName == "Discharging" && newName == "Idle")
                {
                    _byArea.Remove(Area200);
                }
            }
            else if (areaName == Area300)
            {
                // When Area 300 starts filling, the batch moves from Area 200 to Area 300
                if (prevName == "Idle" && newName == "Filling")
                {
                    if (_byArea.TryGetValue(Area200, out var batch))
                    {
                        _byArea[Area300] = batch;
                        batch.Record.CurrentStage = BatchStage.Cooking;
                        if (!batch.Record.EquipmentUsed.Contains("KT-301"))
                            batch.Record.EquipmentUsed.Add("KT-301");
                        Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batch.Record.BatchId}: MX-201 -> KT-301");
                    }
                }

                // When Area 300 discharge starts, batch will soon move to Area 400
                if (prevName == "Cooling" && newName == "Discharging")
                {
                    // Pre-announce - batch still associated with Area 300 until Area 400 picks it up
                }

                if (prevName == "Discharging" && newName == "Idle")
                {
                    _byArea.Remove(Area300);
                }
            }
            else if (areaName == Area400)
            {
                // When Area 400 starts filling, the batch moves from Area 300 to Area 400
                if (prevName == "Idle" && newName == "Filling")
                {
                    if (_byArea.TryGetValue(Area300, out var batch))
                    {
                        _byArea[Area400] = batch;
                        batch.Record.CurrentStage = BatchStage.Processing;
                        if (!batch.Record.EquipmentUsed.Contains("DA-401"))
                            batch.Record.EquipmentUsed.Add("DA-401");
                        if (!batch.Record.EquipmentUsed.Contains("HG-401"))
                            batch.Record.EquipmentUsed.Add("HG-401");
                        Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batch.Record.BatchId}: KT-301 -> DA-401/HG-401");
                    }
                }

                // Batch complete when Area 400 returns to Idle
                if (prevName == "Draining" && newName == "Idle")
                {
                    if (_byArea.TryGetValue(Area400, out var batch))
                    {
                        _byArea.Remove(Area400);
                        CompleteBatch(batch, simNow);
                    }
                }
            }

            // Start new phase tracker for this area's new state
            var obs = PhaseObserverCatalog.TryGet(areaName, newName);
            if (obs != null && _byArea.ContainsKey(areaName))
            {
                var tracker = new ActivePhaseTracker(obs, simNow);
                tracker.RecordEntrySnapshot(ReadTagAsDouble);
                _phaseTrackers[$"{areaName}|{newName}"] = tracker;
            }
        }

        private ActiveBatch StartNewBatch(DateTime simNow)
        {
            // Use work-order-assigned batch ID if available, otherwise auto-generate
            var instruction = _getActiveInstruction?.Invoke();
            string batchId;
            string workOrderId = "";

            if (instruction != null && !string.IsNullOrEmpty(instruction.BatchId))
            {
                batchId = instruction.BatchId;
                workOrderId = instruction.WorkOrderId;
            }
            else
            {
                _batchCounterForDate++;
                batchId = $"B-{simNow:yyyyMMdd}-{_batchCounterForDate:D3}";
            }

            var record = new BatchRecord
            {
                BatchId = batchId,
                StartTime = simNow,
                Status = BatchStatus.Running,
                CurrentStage = BatchStage.PreMixing
            };

            var batch = new ActiveBatch { Record = record };

            if (!string.IsNullOrEmpty(workOrderId))
                Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batchId}: START (stage=PreMixing, workOrder={workOrderId})");
            else
                Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batchId}: START (stage=PreMixing)");

            return batch;
        }

        private void PublishBatchTags(DateTime simNow)
        {
            // The "primary" batch for BATCH_ID is whichever one is furthest along
            ActiveBatch? primary = null;
            if (_byArea.TryGetValue(Area400, out var b400)) primary = b400;
            else if (_byArea.TryGetValue(Area300, out var b300)) primary = b300;
            else if (_byArea.TryGetValue(Area200, out var b200)) primary = b200;

            var t = _ctx.Tags;

            if (primary != null)
            {
                t.Write("BATCH_ID", primary.Record.BatchId);
                t.Write("BATCH_STATUS", primary.Record.Status.ToString());
                t.Write("CURRENT_BATCH_ID", primary.Record.BatchId);

                // Determine current phase from the most recent active tracker for the primary's area
                string currentArea = _byArea.FirstOrDefault(kv => kv.Value == primary).Key ?? "";
                string phaseLabel = "";
                DateTime? phaseStart = null;
                foreach (var (key, tracker) in _phaseTrackers)
                {
                    if (key.StartsWith(currentArea + "|"))
                    {
                        phaseLabel = tracker.Record.PhaseLabel;
                        phaseStart = tracker.Record.StartTime;
                        break;
                    }
                }

                t.Write("BATCH_PHASE", phaseLabel);
                if (phaseStart.HasValue)
                {
                    t.Write("BATCH_PHASE_TMR", Math.Round((simNow - phaseStart.Value).TotalSeconds, 0));
                }
                t.Write("BATCH_START_TIME", primary.Record.StartTime);
            }
            else
            {
                t.Write("BATCH_ID", "");
                t.Write("BATCH_STATUS", "IDLE");
                t.Write("CURRENT_BATCH_ID", "");
                t.Write("BATCH_PHASE", "");
                t.Write("BATCH_PHASE_TMR", 0.0);
            }

            // Per-area BATCH_ID tags
            t.Write("MX201_BATCH_ID", _byArea.TryGetValue(Area200, out var mx) ? mx.Record.BatchId : "");
            t.Write("KT301_BATCH_ID", _byArea.TryGetValue(Area300, out var kt) ? kt.Record.BatchId : "");
            t.Write("DA401_BATCH_ID", _byArea.TryGetValue(Area400, out var da) ? da.Record.BatchId : "");

            t.Write("BATCHES_TODAY", _batchesToday);
        }

        /// <summary>
        /// Called externally when we detect a batch has been fully processed through the
        /// batch side (Area 400 completed). Writes the record and archives it.
        /// </summary>
        private void CompleteBatch(ActiveBatch batch, DateTime simNow)
        {
            batch.Record.EndTime = simNow;
            batch.Record.Status = BatchStatus.Complete;
            batch.Record.CurrentStage = BatchStage.Complete;

            _batchesToday++;
            _completedRecords.Add(batch.Record);
            if (_completedRecords.Count > MaxRetainedRecords)
                _completedRecords.RemoveAt(0);

            WriteBatchRecord(batch.Record);
            Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batch.Record.BatchId}: COMPLETE " +
                              $"(duration={batch.Record.TotalDurationSec / 60:F1}min, phases={batch.Record.Phases.Count})");
        }

        private void WriteBatchRecord(BatchRecord record)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                string json = JsonSerializer.Serialize(record, options);
                string path = Path.Combine(_outputDir, $"{record.BatchId}.json");
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BATCH] Failed to write batch record {record.BatchId}: {ex.Message}");
            }
        }

        private double ReadTagAsDouble(string tagName)
        {
            var val = _ctx.Tags.Read(tagName);
            if (val == null) return 0;
            return val switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                bool b => b ? 1 : 0,
                _ => double.TryParse(val.ToString(), out var parsed) ? parsed : 0
            };
        }

    }

    /// <summary>
    /// Runtime tracking wrapper for an active batch.
    /// </summary>
    public class ActiveBatch
    {
        public BatchRecord Record { get; set; } = null!;
    }
}
