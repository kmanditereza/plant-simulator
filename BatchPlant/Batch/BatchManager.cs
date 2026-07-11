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
        private ApiServer? _apiServer;
        private Process.Area450_BufferTanksController? _buffers;
        private WorkOrderQueue? _workOrderQueue;

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
        private int _batchesThisShift;
        private int _goodBatchesThisShift;
        private int _scrapBatchesThisShift;
        private int _lastShift;
        private bool _shiftInit;
        private double _runtimeMinShift;
        private double _downtimeMinShift;

        // Last completed batch's outcome. The "actual" tags hold these while the line is idle,
        // so a dashboard still shows the finished batch rather than snapping to zero.
        private double _lastCycleMin;
        private double _lastYieldPct;
        private double _lastTargetKg;
        private double _lastActualKg;
        private string _lastBatchId = "";
        private readonly List<double> _recentCycleMin = new();
        private const int CycleWindow = 10;

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

        /// <summary>
        /// Connect to the API server so we can publish WorkOrderActivated events.
        /// </summary>
        public void ConnectApiServer(ApiServer server)
        {
            _apiServer = server;
        }

        /// <summary>
        /// Connect the Area 450 buffer tanks so the batch line can report the buffer level it
        /// feeds, and how much good product the current order has delivered into it.
        /// </summary>
        public void ConnectBuffers(Process.Area450_BufferTanksController buffers)
        {
            _buffers = buffers;
        }

        /// <summary>
        /// Connect the work order queue so the batch line can distinguish "blocked" (work is
        /// waiting but the buffers are full) from "idle" (nothing scheduled).
        /// </summary>
        public void ConnectWorkOrderQueue(WorkOrderQueue queue)
        {
            _workOrderQueue = queue;
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

            // Reset the per-shift batch counters at each shift change (incl. a SHIFT_RESET).
            int shift = _ctx.Clock.CurrentShift;
            if (!_shiftInit || shift != _lastShift)
            {
                _shiftInit = true;
                _lastShift = shift;
                _batchesThisShift = 0;
                _goodBatchesThisShift = 0;
                _scrapBatchesThisShift = 0;
                _runtimeMinShift = 0;
                _downtimeMinShift = 0;
            }

            // Observe each area's state and handle transitions
            ObserveArea(Area200, "MX201_STATE", Area200StateNames, simNow, dt);
            ObserveArea(Area300, "KT301_STATE", Area300StateNames, simNow, dt);
            ObserveArea(Area400, "DA401_STATE", Area400StateNames, simNow, dt);

            // Batch line runtime/downtime for this shift: the line is "running" whenever a
            // batch is in flight anywhere across Area 200/300/400, otherwise it is idle.
            // Sampled after ObserveArea so _byArea reflects this tick's transitions.
            double dtWallMin = _ctx.Clock.SimToWall(dt).TotalMinutes;   // real minutes, matching the shift window
            if (_byArea.Count > 0) _runtimeMinShift += dtWallMin;
            else                   _downtimeMinShift += dtWallMin;

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

                    // Emit BatchPhaseCompleted event
                    var instruction = _getActiveInstruction?.Invoke();
                    if (instruction != null && _apiServer != null)
                    {
                        _ctx.Tags.Write("PHASE_COMPLETED", true);
                        _apiServer.SetPhaseCompletedEvent(instruction, finalized, simNow);
                    }
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

                // Emit BatchPhaseStarted event
                var instruction = _getActiveInstruction?.Invoke();
                if (instruction != null && _apiServer != null)
                {
                    _ctx.Tags.Write("PHASE_STARTED", true);
                    _apiServer.SetPhaseStartedEvent(
                        instruction, obs.PhaseLabel, obs.UnitName,
                        tracker.Record.ReportParameters.ToDictionary(kv => kv.Key, kv => kv.Value),
                        simNow);
                }
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

            // Work-order-assigned batch IDs repeat when the order is re-dispatched, so the buffer's
            // per-batch delivery ledger must start empty for this run rather than resume the last one.
            _buffers?.BeginBatch(batchId);

            var record = new BatchRecord
            {
                BatchId = batchId,
                StartTime = simNow,
                Status = BatchStatus.Running,
                CurrentStage = BatchStage.PreMixing
            };

            var batch = new ActiveBatch { Record = record, Instruction = instruction };

            if (!string.IsNullOrEmpty(workOrderId))
            {
                Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batchId}: START (stage=PreMixing, workOrder={workOrderId})");

                // Set event tags and publish events
                if (instruction != null && _apiServer != null)
                {
                    string shift = _ctx.Clock.CurrentShift.ToString();
                    _ctx.Tags.Write("WO_ACTIVATED", true);
                    _ctx.Tags.Write("BATCH_STARTED", true);
                    _apiServer.ResetPhaseSequence();
                    _apiServer.SetActivatedEvent(instruction, simNow, shift);
                    _apiServer.SetBatchStartedEvent(instruction, simNow, shift);
                    Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] WO_ACTIVATED=true, BATCH_STARTED=true");
                }
            }
            else
            {
                Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batchId}: START (stage=PreMixing)");
            }

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

                // Order identity of the batch on the line. Instruction is null for a batch
                // started without a work order (legacy auto-start), so fall back to "".
                t.Write("BATCH_LINE_WORK_ORDER_ID", primary.Instruction?.WorkOrderId ?? "");
                t.Write("BATCH_LINE_PROCESS_ORDER", primary.Instruction?.ProcessOrderNumber ?? "");

                // Order progress, in kg. "Good qty completed" is the product this batch has
                // actually delivered into the buffer (cumulative), not the current tank level.
                t.Write("BATCH_LINE_ORDER_QTY_TARGET", Math.Round(primary.Instruction?.BatchSizeKg ?? 0.0, 2));
                t.Write("BATCH_LINE_ORDER_QTY_GOOD", Math.Round(_buffers?.DeliveredKgFor(primary.Record.BatchId) ?? 0.0, 2));

                // Recipe identity of the batch on the line.
                t.Write("BATCH_LINE_RECIPE_ID", primary.Instruction?.MasterRecipeId ?? "");
                t.Write("BATCH_LINE_RECIPE_VERSION", primary.Instruction?.MasterRecipeVersion ?? "");

                // Actual cycle time so far, and yield achieved so far, for the batch on the line.
                double targetKg = primary.Instruction?.BatchSizeKg ?? 0.0;
                double deliveredKg = _buffers?.DeliveredKgFor(primary.Record.BatchId) ?? 0.0;
                t.Write("BATCH_LINE_CYCLE_TIME_ACTUAL_MIN", Math.Round((simNow - primary.Record.StartTime).TotalMinutes, 2));
                t.Write("BATCH_LINE_BATCH_YIELD_PCT", Math.Round(targetKg > 0 ? deliveredKg / targetKg * 100.0 : 0.0, 2));

                // Batch outcome, live. BATCH_LINE_BATCH_COMPLETE (below) is false here, so a
                // consumer knows ACTUAL is still accumulating and will fall short of TARGET.
                t.Write("BATCH_LINE_BATCH_ID", primary.Record.BatchId);
                t.Write("BATCH_LINE_BATCH_TARGET_KG", Math.Round(targetKg, 2));
                t.Write("BATCH_LINE_BATCH_ACTUAL_KG", Math.Round(deliveredKg, 2));

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

                double phaseElapsedSec = phaseStart.HasValue ? (simNow - phaseStart.Value).TotalSeconds : 0.0;

                t.Write("BATCH_PHASE", phaseLabel);
                if (phaseStart.HasValue)
                {
                    t.Write("BATCH_PHASE_TMR", Math.Round(phaseElapsedSec, 0));
                }
                t.Write("BATCH_START_TIME", primary.Record.StartTime);

                // BatchLine-folder view of the active phase
                t.Write("BATCH_LINE_CURRENT_PHASE", phaseLabel);
                t.Write("BATCH_LINE_PHASE_ELAPSED", Math.Round(phaseElapsedSec, 0));
                t.Write("BATCH_LINE_PHASE_TARGET_DURATION", PhaseTargetDurationSec(primary.Instruction, phaseLabel));
            }
            else
            {
                t.Write("BATCH_ID", "");
                t.Write("BATCH_STATUS", "IDLE");
                t.Write("CURRENT_BATCH_ID", "");
                t.Write("BATCH_LINE_WORK_ORDER_ID", "");
                t.Write("BATCH_LINE_PROCESS_ORDER", "");
                t.Write("BATCH_LINE_ORDER_QTY_TARGET", 0.0);
                t.Write("BATCH_LINE_ORDER_QTY_GOOD", 0.0);
                t.Write("BATCH_LINE_RECIPE_ID", "");
                t.Write("BATCH_LINE_RECIPE_VERSION", "");
                t.Write("BATCH_LINE_PHASE_TARGET_DURATION", 0.0);

                // Idle: hold the last completed batch's outcome rather than snapping to zero.
                t.Write("BATCH_LINE_CYCLE_TIME_ACTUAL_MIN", Math.Round(_lastCycleMin, 2));
                t.Write("BATCH_LINE_BATCH_YIELD_PCT", Math.Round(_lastYieldPct, 2));
                t.Write("BATCH_LINE_BATCH_ID", _lastBatchId);
                t.Write("BATCH_LINE_BATCH_TARGET_KG", Math.Round(_lastTargetKg, 2));
                t.Write("BATCH_LINE_BATCH_ACTUAL_KG", Math.Round(_lastActualKg, 2));
                t.Write("BATCH_PHASE", "");
                t.Write("BATCH_PHASE_TMR", 0.0);

                t.Write("BATCH_LINE_CURRENT_PHASE", "");
                t.Write("BATCH_LINE_PHASE_ELAPSED", 0.0);
            }

            // Per-area BATCH_ID tags
            t.Write("MX201_BATCH_ID", _byArea.TryGetValue(Area200, out var mx) ? mx.Record.BatchId : "");
            t.Write("KT301_BATCH_ID", _byArea.TryGetValue(Area300, out var kt) ? kt.Record.BatchId : "");
            t.Write("DA401_BATCH_ID", _byArea.TryGetValue(Area400, out var da) ? da.Record.BatchId : "");

            // Buffer the batch line feeds (Area 450, all three tanks), in kg -- same units as
            // batch size. Published whether or not a batch is currently running.
            t.Write("BATCH_LINE_UPSTREAM_BUFFER_LEVEL", Math.Round(_buffers?.TotalProductKg() ?? 0.0, 1));

            // Line state + the single downtime flag and its one reason.
            // Blocked = work orders are waiting but every buffer tank is full, so no batch can start.
            // Idle    = nothing scheduled; not a loss, so not flagged as downtime.
            bool running = _byArea.Count > 0;
            bool ordersPending = (_workOrderQueue?.PendingCount ?? 0) > 0;
            bool bufferFull = _buffers != null && !_buffers.HasCapacityForNewBatch();

            string reason;
            bool downtime;
            if (running)                            { reason = "Running"; downtime = false; }
            else if (ordersPending && bufferFull)   { reason = "Blocked"; downtime = true; }
            else                                    { reason = "Idle";    downtime = false; }

            t.Write("BATCH_LINE_RUNNING", running);
            t.Write("BATCH_LINE_STATE", running ? "Producing" : (downtime ? "Blocked" : "Idle"));
            t.Write("BATCH_LINE_DOWNTIME", downtime);
            t.Write("BATCH_LINE_STATE_REASON", reason);

            // Completed-batch cycle times (previously defined but never written).
            // Are the batch outcome tags final? They are exactly when no batch is on the line and
            // at least one has completed. Derived rather than latched, so it needs no acknowledge
            // and cannot be raced by the BatchControl/BATCH_COMPLETED ack that Node-RED issues.
            t.Write("BATCH_LINE_BATCH_COMPLETE", _byArea.Count == 0 && !string.IsNullOrEmpty(_lastBatchId));

            t.Write("LAST_BATCH_CYCLE_MIN", Math.Round(_lastCycleMin, 2));
            t.Write("AVG_BATCH_CYCLE_MIN", _recentCycleMin.Count > 0 ? Math.Round(_recentCycleMin.Average(), 2) : 0.0);

            t.Write("BATCHES_TODAY", _batchesToday);
            t.Write("BATCHES_SHIFT", _batchesThisShift);
            t.Write("BATCH_LINE_GOOD_BATCHES_SHIFT", _goodBatchesThisShift);
            t.Write("BATCH_LINE_SCRAP_BATCHES_SHIFT", _scrapBatchesThisShift);
            t.Write("BATCH_LINE_RUNTIME_MIN_SHIFT", Math.Round(_runtimeMinShift, 4));
            t.Write("BATCH_LINE_DOWNTIME_MIN_SHIFT", Math.Round(_downtimeMinShift, 4));
        }

        /// <summary>
        /// Called externally when we detect a batch has been fully processed through the
        /// batch side (Area 400 completed). Writes the record and archives it.
        /// </summary>
        /// <summary>
        /// The control recipe's target duration for a phase, in seconds. Only the time-driven
        /// phases have one: the dose phases run to a charge quantity, heat/cool run to a
        /// temperature setpoint, and homogenise runs until the vessel is empty -- those return 0.
        /// </summary>
        private static double PhaseTargetDurationSec(BatchInstruction? instr, string phaseLabel)
        {
            if (instr == null) return 0.0;
            return phaseLabel switch
            {
                "PreMix.HighShearMix"     => instr.MixDurationSec,
                "Cook.PasteurizationHold" => instr.HoldDurationSec,
                "Process.Deaerate"        => instr.DeaerateTimeSec,
                _                         => 0.0
            };
        }

        private void CompleteBatch(ActiveBatch batch, DateTime simNow)
        {
            batch.Record.EndTime = simNow;
            batch.Record.Status = BatchStatus.Complete;
            batch.Record.CurrentStage = BatchStage.Complete;

            // Freeze this batch's outcome. Yield is the bulk actually delivered into the buffer
            // against the order's target quantity -- Area 400 has finished transferring by now,
            // so the delivered figure is final.
            _lastCycleMin = (simNow - batch.Record.StartTime).TotalMinutes;
            _recentCycleMin.Add(_lastCycleMin);
            if (_recentCycleMin.Count > CycleWindow) _recentCycleMin.RemoveAt(0);

            double targetKg = batch.Instruction?.BatchSizeKg ?? 0.0;
            double deliveredKg = _buffers?.DeliveredKgFor(batch.Record.BatchId) ?? 0.0;
            _lastYieldPct = targetKg > 0 ? deliveredKg / targetKg * 100.0 : 0.0;
            _lastTargetKg = targetKg;
            _lastActualKg = deliveredKg;
            _lastBatchId = batch.Record.BatchId;

            // Edge-only. The yield KPI bridge polls this and writes false once it has published.
            _ctx.Tags.Write("BATCH_LINE_YIELD_PENDING", true);

            _batchesToday++;
            _batchesThisShift++;

            // Good vs scrap: a batch is good if it completed cleanly with no recorded
            // deviations or alarms; otherwise it counts as scrap/rework.
            bool good = batch.Record.Status == BatchStatus.Complete
                        && batch.Record.Deviations.Count == 0
                        && batch.Record.Alarms.Count == 0;
            if (good) _goodBatchesThisShift++;
            else _scrapBatchesThisShift++;

            _completedRecords.Add(batch.Record);
            if (_completedRecords.Count > MaxRetainedRecords)
                _completedRecords.RemoveAt(0);

            WriteBatchRecord(batch.Record);
            Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] {batch.Record.BatchId}: COMPLETE " +
                              $"(duration={batch.Record.TotalDurationSec / 60:F1}min, phases={batch.Record.Phases.Count})");

            // Emit completion events
            if (batch.Instruction != null && _apiServer != null)
            {
                _ctx.Tags.Write("BATCH_COMPLETED", true);
                _ctx.Tags.Write("WO_COMPLETED", true);
                _ctx.Tags.Write("BATCH_RELEASED", true);
                _apiServer.SetBatchCompletedEvent(batch.Instruction, batch.Record, simNow);
                _apiServer.SetWorkOrderCompletedEvent(batch.Instruction, batch.Record, simNow);
                _apiServer.SetBatchReleasedEvent(batch.Instruction, batch.Record, simNow);
                Console.WriteLine($"[{simNow:HH:mm:ss}] [BATCH] BATCH_COMPLETED=true, WO_COMPLETED=true, BATCH_RELEASED=true");
            }
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
        public BatchInstruction? Instruction { get; set; }
    }
}
