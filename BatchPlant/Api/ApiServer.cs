using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BatchPlant.Api
{
    /// <summary>
    /// Lightweight HTTP API for receiving production instructions.
    ///
    /// Endpoints:
    ///   POST /api/control-recipe    — Download a control recipe (stores for later execution)
    ///   POST /api/work-order        — Dispatch a work order (triggers batch start)
    ///   GET  /api/status            — Current queue and batch status
    ///   GET  /api/recipes           — List stored control recipes
    ///
    /// The two-step flow:
    ///   1. MES posts the control recipe (with all parameters)
    ///   2. MES posts the work order dispatch (the "GO" signal)
    ///   3. BatchManager dequeues the instruction and starts the batch
    /// </summary>
    public class ApiServer
    {
        private readonly Dictionary<string, JsonObject> _recipes = new();
        private readonly WorkOrderQueue _queue;
        private readonly OpcUa.TagRegistry? _tags;
        private readonly List<string> _eventLog = new();
        private readonly object _lock = new();

        public ApiServer(WorkOrderQueue queue, OpcUa.TagRegistry? tags = null)
        {
            _queue = queue;
            _tags = tags;
        }

        // Event storage (set by processing, read/ack by external systems)
        private object? _activatedEvent;
        private object? _batchScheduledEvent;
        private object? _batchStartedEvent;
        private object? _phaseStartedEvent;
        private object? _phaseCompletedEvent;
        private object? _batchCompletedEvent;
        private object? _woCompletedEvent;
        private object? _batchReleasedEvent;
        private readonly object _eventLock = new();

        // Phase sequence counter per batch
        private int _phaseSequenceNumber;

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/control-recipe", HandleControlRecipe);
            app.MapPost("/api/work-order", HandleWorkOrder);
            app.MapGet("/api/status", HandleStatus);
            app.MapGet("/api/recipes", HandleListRecipes);
            app.MapGet("/api/work-order/activated", HandleGetActivated);
            app.MapPost("/api/work-order/activated/ack", HandleAckActivated);
            app.MapGet("/api/batch/scheduled", HandleGetBatchScheduled);
            app.MapPost("/api/batch/scheduled/ack", HandleAckBatchScheduled);
            app.MapGet("/api/batch/started", HandleGetBatchStarted);
            app.MapPost("/api/batch/started/ack", HandleAckBatchStarted);
            app.MapGet("/api/batch/phase-started", HandleGetPhaseStarted);
            app.MapPost("/api/batch/phase-started/ack", HandleAckPhaseStarted);
            app.MapGet("/api/batch/phase-completed", HandleGetPhaseCompleted);
            app.MapPost("/api/batch/phase-completed/ack", HandleAckPhaseCompleted);
            app.MapGet("/api/batch/completed", HandleGetBatchCompleted);
            app.MapPost("/api/batch/completed/ack", HandleAckBatchCompleted);
            app.MapGet("/api/work-order/completed", HandleGetWoCompleted);
            app.MapPost("/api/work-order/completed/ack", HandleAckWoCompleted);
            app.MapGet("/api/batch/released", HandleGetBatchReleased);
            app.MapPost("/api/batch/released/ack", HandleAckBatchReleased);
        }

        // ---- Helper: common event envelope fields ----

        private static object BuildIsa95Context(BatchInstruction instr) => new
        {
            enterprise = instr.Enterprise,
            site = instr.Site,
            area = instr.Area,
            processCell = instr.ProcessCell
        };

        private static object BuildCustomer(BatchInstruction instr) => new
        {
            customerId = instr.CustomerId,
            customerName = instr.CustomerName
        };

        private static object BuildMasterRecipe(BatchInstruction instr) => new
        {
            id = instr.MasterRecipeId,
            version = instr.MasterRecipeVersion
        };

        private static object BuildSource() => new
        {
            origin = "BatchPlantSimulator_OPCUA",
            uri = "opc.tcp://localhost:26543/BatchPlantServer"
        };

        private static object BuildOperator() => new
        {
            userId = "operator-4412",
            name = "J. Smith",
            role = "BatchOperator"
        };

        // ---- WorkOrderActivated ----

        public void SetActivatedEvent(BatchInstruction instruction, DateTime simNow, string shift)
        {
            lock (_eventLock)
            {
                _activatedEvent = new
                {
                    eventType = "WorkOrderActivated",
                    eventId = $"EVT-WO-ACT-{instruction.WorkOrderId}",
                    eventVersion = "2.0",
                    timestamp = simNow.ToString("o"),
                    source = BuildSource(),
                    correlationId = $"workorder-{instruction.WorkOrderId}",
                    data = new
                    {
                        workOrderId = instruction.WorkOrderId,
                        processOrderNumber = instruction.ProcessOrderNumber,
                        controlRecipeNumber = instruction.ControlRecipeNumber,
                        batchRecordId = instruction.BatchRecordId,
                        producedBatch = instruction.BatchId,
                        materialNumber = instruction.ProductCode,
                        customer = BuildCustomer(instruction),
                        isa95Context = BuildIsa95Context(instruction),
                        state = "ACTIVE",
                        stateTimestamp = simNow.ToString("o"),
                        activatedAt = simNow.ToString("o"),
                        startedBy = BuildOperator(),
                        shift,
                        comment = "Batch manager started executing the control recipe; the BatchRecord now tracks per-operation / per-phase actuals."
                    }
                };
            }
        }

        private IResult HandleGetActivated(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_activatedEvent == null)
                    return Results.Ok(new { activated = false });
                return Results.Ok(_activatedEvent);
            }
        }

        private IResult HandleAckActivated(HttpContext ctx)
        {
            lock (_eventLock) { _activatedEvent = null; }
            _tags?.Write("WO_ACTIVATED", false);
            Console.WriteLine("[API] WO_ACTIVATED acknowledged and reset");
            return Results.Ok(new { status = "acknowledged", woActivated = false });
        }

        // ---- BatchScheduled ----

        private void SetBatchScheduledEvent(BatchInstruction instruction)
        {
            var now = DateTime.UtcNow;
            lock (_eventLock)
            {
                _batchScheduledEvent = new
                {
                    eventType = "BatchScheduled",
                    eventId = $"EVT-SCHED-{instruction.WorkOrderId}",
                    eventVersion = "2.0",
                    timestamp = now.ToString("o"),
                    source = new
                    {
                        origin = "MES-BatchManager",
                        uri = "http://localhost:5050"
                    },
                    correlationId = $"batch-{instruction.BatchRecordId}",
                    data = new
                    {
                        batchRecordId = instruction.BatchRecordId,
                        producedBatch = instruction.BatchId,
                        workOrderId = instruction.WorkOrderId,
                        controlRecipeNumber = instruction.ControlRecipeNumber,
                        processOrderNumber = instruction.ProcessOrderNumber,
                        materialNumber = instruction.ProductCode,
                        masterRecipe = BuildMasterRecipe(instruction),
                        isa95Context = BuildIsa95Context(instruction),
                        scheduledStart = now.ToString("o"),
                        scheduledBy = "scheduler-auto",
                        scheduledAt = now.ToString("o"),
                        targetQuantity = instruction.BatchSizeKg,
                        unitOfMeasure = "KG",
                        priority = "Normal",
                        equipmentAllocation = new object[]
                        {
                            new { role = "PreMix", unit = instruction.PreMixUnit },
                            new { role = "Cook", unit = instruction.CookUnit },
                            new { role = "DeaerateAndHomogenize", unit = instruction.DeaerateUnit },
                            new { role = "DeaerateAndHomogenize", unit = instruction.HomogenizeUnit },
                            new { role = "BufferTransfer", unit = instruction.BufferUnit }
                        },
                        materialReservations = instruction.Bom.Select(b => new
                        {
                            ingredientClass = b.Ingredient,
                            targetKg = b.QtyKg,
                            lot = b.Lot,
                            sourceEquipment = b.SourceUnit
                        }).ToArray(),
                        status = "Scheduled"
                    }
                };
            }
        }

        private IResult HandleGetBatchScheduled(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_batchScheduledEvent == null)
                    return Results.Ok(new { scheduled = false });
                return Results.Ok(_batchScheduledEvent);
            }
        }

        private IResult HandleAckBatchScheduled(HttpContext ctx)
        {
            lock (_eventLock) { _batchScheduledEvent = null; }
            _tags?.Write("BATCH_SCHEDULED", false);
            Console.WriteLine("[API] BATCH_SCHEDULED acknowledged and reset");
            return Results.Ok(new { status = "acknowledged", batchScheduled = false });
        }

        // ---- BatchStarted ----

        public void SetBatchStartedEvent(BatchInstruction instruction, DateTime simNow, string shift)
        {
            // Read initial plant state from tags
            double steamPress = _tags?.Read<double>("STM_HDR_PRESS") ?? 0;
            double steamTemp = _tags?.Read<double>("STM_HDR_TEMP") ?? 0;
            double coolingTemp = _tags?.Read<double>("CW_SUP_TEMP") ?? 0;
            double airPress = _tags?.Read<double>("COMP_AIR_PRESS") ?? 0;

            lock (_eventLock)
            {
                _batchStartedEvent = new
                {
                    eventType = "BatchStarted",
                    eventId = $"EVT-START-{instruction.WorkOrderId}",
                    eventVersion = "2.0",
                    timestamp = simNow.ToString("o"),
                    source = BuildSource(),
                    correlationId = $"batch-{instruction.BatchRecordId}",
                    data = new
                    {
                        batchRecordId = instruction.BatchRecordId,
                        producedBatch = instruction.BatchId,
                        workOrderId = instruction.WorkOrderId,
                        controlRecipeNumber = instruction.ControlRecipeNumber,
                        processOrderNumber = instruction.ProcessOrderNumber,
                        materialNumber = instruction.ProductCode,
                        masterRecipe = BuildMasterRecipe(instruction),
                        isa95Context = BuildIsa95Context(instruction),
                        actualStart = simNow.ToString("o"),
                        scheduledStart = instruction.ScheduledAt?.ToString("o") ?? simNow.ToString("o"),
                        startDelaySec = instruction.ScheduledAt.HasValue
                            ? (int)(simNow - instruction.ScheduledAt.Value).TotalSeconds
                            : 0,
                        startedBy = BuildOperator(),
                        shift,
                        targetQuantity = instruction.BatchSizeKg,
                        unitOfMeasure = "KG",
                        scaleFactor = instruction.ScaleFactor,
                        parameterScaling = new
                        {
                            standardBatchSizeKg = instruction.BatchSizeKg,
                            actualBatchSizeKg = instruction.BatchSizeKg,
                            scaleFactor = instruction.ScaleFactor
                        },
                        initialPlantState = new
                        {
                            steamHeaderPressBar = Math.Round(steamPress, 1),
                            steamHeaderTempC = Math.Round(steamTemp, 1),
                            coolingSupplyTempC = Math.Round(coolingTemp, 1),
                            compressedAirPressBar = Math.Round(airPress, 1)
                        },
                        status = "Running",
                        currentOperation = "0010",
                        currentOperationName = "PreMix"
                    }
                };
            }
        }

        private IResult HandleGetBatchStarted(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_batchStartedEvent == null)
                    return Results.Ok(new { started = false });
                return Results.Ok(_batchStartedEvent);
            }
        }

        private IResult HandleAckBatchStarted(HttpContext ctx)
        {
            lock (_eventLock) { _batchStartedEvent = null; }
            _tags?.Write("BATCH_STARTED", false);
            Console.WriteLine("[API] BATCH_STARTED acknowledged and reset");
            return Results.Ok(new { status = "acknowledged", batchStarted = false });
        }

        private IResult HandleControlRecipe(HttpContext ctx)
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = reader.ReadToEndAsync().Result;

            JsonObject? doc;
            try
            {
                doc = JsonNode.Parse(body)?.AsObject();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON", detail = ex.Message });
            }

            if (doc == null)
                return Results.BadRequest(new { error = "Empty or invalid JSON body" });

            // Extract the control recipe object (may be nested under "controlRecipe" key or at root)
            var recipe = doc["controlRecipe"]?.AsObject() ?? doc;
            var crNumber = recipe["controlRecipeNumber"]?.GetValue<string>();

            if (string.IsNullOrEmpty(crNumber))
                return Results.BadRequest(new { error = "Missing controlRecipeNumber" });

            lock (_lock)
            {
                _recipes[crNumber] = recipe;
                LogEvent($"Control recipe downloaded: {crNumber}");
            }

            Console.WriteLine($"[API] Control recipe received: {crNumber}");

            return Results.Ok(new
            {
                status = "stored",
                controlRecipeNumber = crNumber,
                storedRecipes = _recipes.Count
            });
        }

        private IResult HandleWorkOrder(HttpContext ctx)
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = reader.ReadToEndAsync().Result;

            JsonObject? doc;
            try
            {
                doc = JsonNode.Parse(body)?.AsObject();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = "Invalid JSON", detail = ex.Message });
            }

            if (doc == null)
                return Results.BadRequest(new { error = "Empty or invalid JSON body" });

            // Navigate to the data payload
            var data = doc["data"]?.AsObject() ?? doc;

            var crNumber = data["controlRecipeNumber"]?.GetValue<string>();
            var workOrderId = data["workOrderId"]?.GetValue<string>();
            var batchId = data["producedBatch"]?.GetValue<string>();
            var state = data["state"]?.GetValue<string>();

            if (string.IsNullOrEmpty(crNumber))
                return Results.BadRequest(new { error = "Missing controlRecipeNumber in data" });
            if (string.IsNullOrEmpty(workOrderId))
                return Results.BadRequest(new { error = "Missing workOrderId in data" });
            if (state != "DISPATCHED")
                return Results.BadRequest(new { error = $"Work order state must be DISPATCHED, got: {state}" });

            // Look up the control recipe
            JsonObject? recipe;
            lock (_lock)
            {
                if (!_recipes.TryGetValue(crNumber, out recipe))
                {
                    return Results.NotFound(new
                    {
                        error = $"Control recipe '{crNumber}' not found. Post it to /api/control-recipe first.",
                        storedRecipes = _recipes.Keys.ToList()
                    });
                }
            }

            // Build the batch instruction from the recipe
            var instruction = BuildInstruction(recipe, data, workOrderId, batchId ?? "", crNumber);

            _queue.Enqueue(instruction);

            // Set BatchScheduled event and tag
            SetBatchScheduledEvent(instruction);
            _tags?.Write("BATCH_SCHEDULED", true);

            lock (_lock)
            {
                LogEvent($"Work order dispatched: {workOrderId}, batch={batchId}, recipe={crNumber}");
            }

            var customerName = instruction.CustomerName;
            Console.WriteLine($"[API] Work order dispatched: {workOrderId} -> batch {batchId}" +
                (string.IsNullOrEmpty(customerName) ? "" : $" [{customerName}]") +
                $" (queue={_queue.PendingCount})");
            Console.WriteLine($"[API] BATCH_SCHEDULED=true (GET /api/batch/scheduled)");

            return Results.Ok(new
            {
                status = "queued",
                workOrderId,
                batchId,
                controlRecipeNumber = crNumber,
                queueDepth = _queue.PendingCount
            });
        }

        private IResult HandleStatus(HttpContext ctx)
        {
            lock (_lock)
            {
                return Results.Ok(new
                {
                    queueDepth = _queue.PendingCount,
                    currentBatch = _queue.Current?.BatchId,
                    currentWorkOrder = _queue.Current?.WorkOrderId,
                    currentCustomer = _queue.Current?.CustomerName,
                    currentProduct = _queue.Current?.ProductCode,
                    storedRecipes = _recipes.Count,
                    recentEvents = _eventLog.TakeLast(20).ToList()
                });
            }
        }

        private IResult HandleListRecipes(HttpContext ctx)
        {
            lock (_lock)
            {
                var summaries = _recipes.Select(kv => new
                {
                    controlRecipeNumber = kv.Key,
                    product = kv.Value["product"]?["materialNumber"]?.GetValue<string>() ?? "unknown",
                    targetKg = kv.Value["product"]?["targetQuantity"]?.GetValue<double>() ?? 0,
                    customer = kv.Value["customer"]?["customerName"]?.GetValue<string>() ?? "",
                    batch = kv.Value["product"]?["producedBatch"]?.GetValue<string>() ?? ""
                }).ToList();

                return Results.Ok(new { recipes = summaries });
            }
        }

        private BatchInstruction BuildInstruction(JsonObject recipe, JsonObject workOrder, string workOrderId, string batchId, string crNumber)
        {
            var instruction = new BatchInstruction
            {
                BatchId = batchId,
                WorkOrderId = workOrderId,
                ControlRecipeNumber = crNumber,
                ProcessOrderNumber = workOrder["processOrderNumber"]?.GetValue<string>()
                    ?? recipe["source"]?["processOrderNumber"]?.GetValue<string>() ?? "",
                ProductCode = recipe["product"]?["materialNumber"]?.GetValue<string>() ?? "",
                BatchSizeKg = recipe["product"]?["targetQuantity"]?.GetValue<double>() ?? 2000,
                ScaleFactor = recipe["scaleFactor"]?.GetValue<double>() ?? 1.0,
                ReceivedAt = DateTime.UtcNow
            };

            // Customer (from work order or recipe — work order takes precedence)
            var customer = workOrder["customer"]?.AsObject()
                ?? recipe["customer"]?.AsObject();
            if (customer != null)
            {
                instruction.CustomerId = customer["customerId"]?.GetValue<string>() ?? "";
                instruction.CustomerName = customer["customerName"]?.GetValue<string>() ?? "";
            }

            // ISA-95 context (from work order or recipe)
            var isa95 = workOrder["isa95Context"]?.AsObject()
                ?? recipe["isa95Context"]?.AsObject();
            if (isa95 != null)
            {
                instruction.Enterprise = isa95["enterprise"]?.GetValue<string>() ?? "";
                instruction.Site = isa95["site"]?.GetValue<string>() ?? "";
                instruction.Area = isa95["area"]?.GetValue<string>() ?? "";
                instruction.ProcessCell = isa95["processCell"]?.GetValue<string>() ?? "";
            }

            // Master recipe reference
            var masterRecipe = recipe["source"]?["masterRecipe"]?.AsObject();
            if (masterRecipe != null)
            {
                instruction.MasterRecipeId = masterRecipe["id"]?.GetValue<string>() ?? "";
                instruction.MasterRecipeVersion = masterRecipe["version"]?.GetValue<string>() ?? "";
            }

            // Generate batch record ID and scheduling timestamp
            var now = DateTime.UtcNow;
            instruction.BatchRecordId = $"BR-{instruction.Site}-{instruction.ProcessCell}-{now:yyMMdd}-{now:HHmm}";
            instruction.ScheduledAt = now;

            // Equipment binding (from work order or recipe)
            var boundUnits = workOrder["boundUnits"]?.AsObject()
                ?? recipe["equipmentBinding"]?.AsObject();
            if (boundUnits != null)
            {
                instruction.PreMixUnit = boundUnits["PreMix"]?.GetValue<string>() ?? "MX-201";
                instruction.CookUnit = boundUnits["Cook"]?.GetValue<string>() ?? "KT-301";
                instruction.BufferUnit = boundUnits["BufferTransfer"]?.GetValue<string>() ?? "BT-452";

                var deaerNode = boundUnits["DeaerateAndHomogenize"];
                if (deaerNode is JsonArray arr && arr.Count >= 2)
                {
                    instruction.DeaerateUnit = arr[0]?.GetValue<string>() ?? "DA-401";
                    instruction.HomogenizeUnit = arr[1]?.GetValue<string>() ?? "HG-401";
                }
            }

            // Extract parameters from procedure
            var operations = recipe["procedure"]?["operations"]?.AsArray();
            if (operations != null)
            {
                foreach (var op in operations)
                {
                    var opName = op?["name"]?.GetValue<string>();
                    var phases = op?["phases"]?.AsArray();
                    if (phases == null) continue;

                    switch (opName)
                    {
                        case "PreMix":
                            ExtractPreMixParams(instruction, phases);
                            break;
                        case "Cook":
                            ExtractCookParams(instruction, phases);
                            break;
                        case "DeaerateAndHomogenize":
                            ExtractProcessingParams(instruction, phases);
                            break;
                        case "BufferTransfer":
                            ExtractBufferParams(instruction, phases);
                            break;
                    }
                }
            }

            // BOM with lot tracking
            var bom = recipe["bom"]?.AsArray();
            if (bom != null)
            {
                foreach (var item in bom)
                {
                    instruction.Bom.Add(new BomLine
                    {
                        Ingredient = item?["ingredient"]?.GetValue<string>() ?? "",
                        Lot = item?["lot"]?.GetValue<string>() ?? "",
                        QtyKg = item?["qtyKg"]?.GetValue<double>() ?? 0,
                        SourceUnit = item?["sourceUnit"]?.GetValue<string>() ?? ""
                    });
                }
            }

            return instruction;
        }

        private void ExtractPreMixParams(BatchInstruction instr, JsonArray phases)
        {
            foreach (var phase in phases)
            {
                var name = phase?["name"]?.GetValue<string>();
                var parms = phase?["parameters"]?.AsArray();
                if (parms == null) continue;

                switch (name)
                {
                    case "DoseWater":
                        instr.WaterTargetKg = GetParam(parms, "chargeQuantity", instr.WaterTargetKg);
                        break;
                    case "DosePaste":
                        instr.PasteTargetKg = GetParam(parms, "chargeQuantity", instr.PasteTargetKg);
                        break;
                    case "DoseVinegar":
                        instr.VinegarTargetKg = GetParam(parms, "chargeQuantity", instr.VinegarTargetKg);
                        break;
                    case "HighShearMix":
                        instr.SugarKg = GetParam(parms, "sugarKg", instr.SugarKg);
                        instr.SaltKg = GetParam(parms, "saltKg", instr.SaltKg);
                        instr.SpiceKg = GetParam(parms, "spiceKg", instr.SpiceKg);
                        instr.StabilizerKg = GetParam(parms, "stabilizerKg", instr.StabilizerKg);
                        instr.MixSpeedRpm = GetParam(parms, "mixSpeedRpm", instr.MixSpeedRpm);
                        instr.MixDurationSec = GetParam(parms, "mixDurationSec", instr.MixDurationSec);
                        break;
                }
            }
        }

        private void ExtractCookParams(BatchInstruction instr, JsonArray phases)
        {
            foreach (var phase in phases)
            {
                var name = phase?["name"]?.GetValue<string>();
                var parms = phase?["parameters"]?.AsArray();
                if (parms == null) continue;

                switch (name)
                {
                    case "HeatToTarget":
                        instr.CookTargetTempC = GetParam(parms, "targetTemperature", instr.CookTargetTempC);
                        instr.CookRampRateCPerMin = GetParam(parms, "rampRate", instr.CookRampRateCPerMin);
                        break;
                    case "PasteurizationHold":
                        instr.HoldDurationSec = GetParam(parms, "holdDuration", instr.HoldDurationSec);
                        // holdDuration might be in MIN
                        var uom = GetParamUom(parms, "holdDuration");
                        if (uom == "MIN") instr.HoldDurationSec *= 60;
                        instr.HoldMinTempC = instr.CookTargetTempC - 2.0; // 2°C tolerance below setpoint
                        break;
                    case "Cool":
                        instr.CoolTargetTempC = GetParam(parms, "coolTargetTemperature", instr.CoolTargetTempC);
                        break;
                }
            }
        }

        private void ExtractProcessingParams(BatchInstruction instr, JsonArray phases)
        {
            foreach (var phase in phases)
            {
                var name = phase?["name"]?.GetValue<string>();
                var parms = phase?["parameters"]?.AsArray();
                if (parms == null) continue;

                switch (name)
                {
                    case "PullVacuum":
                        instr.VacuumTargetMbar = GetParam(parms, "vacuumTargetMbar", instr.VacuumTargetMbar);
                        break;
                    case "Deaerate":
                        instr.DeaerateTimeSec = GetParam(parms, "deaerateTimeSec", instr.DeaerateTimeSec);
                        break;
                    case "Homogenize":
                        instr.HomogPressureBar = GetParam(parms, "homogenizationPressure", instr.HomogPressureBar);
                        instr.HomogPassCount = (int)GetParam(parms, "homogPassCount", instr.HomogPassCount);
                        break;
                }
            }
        }

        private void ExtractBufferParams(BatchInstruction instr, JsonArray phases)
        {
            foreach (var phase in phases)
            {
                var name = phase?["name"]?.GetValue<string>();
                var parms = phase?["parameters"]?.AsArray();
                if (parms == null) continue;

                switch (name)
                {
                    case "Transfer":
                        instr.MinTransferTempC = GetParam(parms, "minTransferTemperature", instr.MinTransferTempC);
                        break;
                    case "Hold":
                        instr.MaxBufferHoldTimeMin = GetParam(parms, "maxBufferHoldTimeMin", instr.MaxBufferHoldTimeMin);
                        break;
                }
            }
        }

        private static double GetParam(JsonArray parms, string id, double defaultVal)
        {
            foreach (var p in parms)
            {
                if (p?["id"]?.GetValue<string>() == id)
                    return p?["value"]?.GetValue<double>() ?? defaultVal;
            }
            return defaultVal;
        }

        private static string? GetParamUom(JsonArray parms, string id)
        {
            foreach (var p in parms)
            {
                if (p?["id"]?.GetValue<string>() == id)
                    return p?["uom"]?.GetValue<string>();
            }
            return null;
        }

        // ---- BatchPhaseStarted ----

        private static readonly Dictionary<string, (string opNum, string opName, string phNum, string phName)> PhaseMetadata = new()
        {
            ["PreMix.DoseWater"]             = ("0010", "PreMix",                "0011", "DoseWater"),
            ["PreMix.DosePaste"]             = ("0010", "PreMix",                "0012", "DosePaste"),
            ["PreMix.DoseVinegar"]           = ("0010", "PreMix",                "0013", "DoseVinegar"),
            ["PreMix.HighShearMix"]          = ("0010", "PreMix",                "0014", "HighShearMix"),
            ["PreMix.Discharge"]             = ("0010", "PreMix",                "0015", "Discharge"),
            ["Cook.Fill"]                    = ("0020", "Cook",                  "0021", "Fill"),
            ["Cook.HeatToTarget"]            = ("0020", "Cook",                  "0022", "HeatToTarget"),
            ["Cook.PasteurizationHold"]      = ("0020", "Cook",                  "0023", "PasteurizationHold"),
            ["Cook.Cool"]                    = ("0020", "Cook",                  "0024", "Cool"),
            ["Cook.Discharge"]               = ("0020", "Cook",                  "0025", "Discharge"),
            ["Process.Fill"]                 = ("0030", "DeaerateAndHomogenize", "0031", "Fill"),
            ["Process.PullVacuum"]           = ("0030", "DeaerateAndHomogenize", "0032", "PullVacuum"),
            ["Process.Deaerate"]             = ("0030", "DeaerateAndHomogenize", "0033", "Deaerate"),
            ["Process.Homogenize"]           = ("0030", "DeaerateAndHomogenize", "0034", "Homogenize"),
            ["Process.VentVacuum"]           = ("0030", "DeaerateAndHomogenize", "0035", "VentVacuum"),
        };

        public void ResetPhaseSequence() => _phaseSequenceNumber = 0;

        public void SetPhaseStartedEvent(
            BatchInstruction instruction, string phaseLabel, string unitName,
            Dictionary<string, double> entrySnapshot, DateTime simNow)
        {
            _phaseSequenceNumber++;
            var meta = PhaseMetadata.GetValueOrDefault(phaseLabel);
            var controlParams = GetControlParameters(instruction, phaseLabel);
            var expectedParams = GetExpectedReportParameters(phaseLabel);

            lock (_eventLock)
            {
                _phaseStartedEvent = new
                {
                    eventType = "BatchPhaseStarted",
                    eventId = $"EVT-PHASE-START-{instruction.WorkOrderId}-{meta.phNum ?? _phaseSequenceNumber.ToString("D4")}",
                    eventVersion = "2.0",
                    timestamp = simNow.ToString("o"),
                    source = BuildSource(),
                    correlationId = $"batch-{instruction.BatchRecordId}",
                    data = new
                    {
                        batchRecordId = instruction.BatchRecordId,
                        producedBatch = instruction.BatchId,
                        workOrderId = instruction.WorkOrderId,
                        controlRecipeNumber = instruction.ControlRecipeNumber,
                        processOrderNumber = instruction.ProcessOrderNumber,
                        operationNumber = meta.opNum ?? "",
                        operationName = meta.opName ?? "",
                        phaseNumber = meta.phNum ?? "",
                        phaseName = meta.phName ?? phaseLabel,
                        phaseSequenceNumber = _phaseSequenceNumber,
                        unitName,
                        isa95Context = BuildIsa95Context(instruction),
                        startTime = simNow.ToString("o"),
                        controlParameters = controlParams,
                        entrySnapshot,
                        expectedReportParameters = expectedParams
                    }
                };
            }
        }

        public void SetPhaseCompletedEvent(
            BatchInstruction instruction, Batch.PhaseRecord record, DateTime simNow)
        {
            var meta = PhaseMetadata.GetValueOrDefault(record.PhaseLabel);
            var exitSnapshot = record.ReportParameters
                .Where(kv => kv.Key.StartsWith("end") || kv.Key.StartsWith("final"))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            var reportParams = record.ReportParameters
                .Where(kv => !kv.Key.StartsWith("start") && !kv.Key.StartsWith("end") && !kv.Key.StartsWith("final"))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            lock (_eventLock)
            {
                _phaseCompletedEvent = new
                {
                    eventType = "BatchPhaseCompleted",
                    eventId = $"EVT-PHASE-DONE-{instruction.WorkOrderId}-{meta.phNum ?? "0000"}",
                    eventVersion = "2.0",
                    timestamp = simNow.ToString("o"),
                    source = BuildSource(),
                    correlationId = $"batch-{instruction.BatchRecordId}",
                    data = new
                    {
                        batchRecordId = instruction.BatchRecordId,
                        producedBatch = instruction.BatchId,
                        workOrderId = instruction.WorkOrderId,
                        controlRecipeNumber = instruction.ControlRecipeNumber,
                        processOrderNumber = instruction.ProcessOrderNumber,
                        operationNumber = meta.opNum ?? "",
                        operationName = meta.opName ?? "",
                        phaseNumber = meta.phNum ?? "",
                        phaseName = meta.phName ?? record.PhaseLabel,
                        unitName = record.UnitName,
                        isa95Context = BuildIsa95Context(instruction),
                        startTime = record.StartTime.ToString("o"),
                        endTime = simNow.ToString("o"),
                        durationSec = record.DurationSec,
                        status = "Complete",
                        completionReason = "Normal",
                        reportParameters = reportParams,
                        exitSnapshot,
                        phaseSignature = new
                        {
                            signedBy = "system",
                            signedAt = simNow.ToString("o"),
                            reason = "Auto-sign on phase completion"
                        }
                    }
                };
            }
        }

        private IResult HandleGetPhaseStarted(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_phaseStartedEvent == null)
                    return Results.Ok(new { phaseStarted = false });
                return Results.Ok(_phaseStartedEvent);
            }
        }

        private IResult HandleAckPhaseStarted(HttpContext ctx)
        {
            lock (_eventLock) { _phaseStartedEvent = null; }
            _tags?.Write("PHASE_STARTED", false);
            return Results.Ok(new { status = "acknowledged", phaseStarted = false });
        }

        private IResult HandleGetPhaseCompleted(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_phaseCompletedEvent == null)
                    return Results.Ok(new { phaseCompleted = false });
                return Results.Ok(_phaseCompletedEvent);
            }
        }

        private IResult HandleAckPhaseCompleted(HttpContext ctx)
        {
            lock (_eventLock) { _phaseCompletedEvent = null; }
            _tags?.Write("PHASE_COMPLETED", false);
            return Results.Ok(new { status = "acknowledged", phaseCompleted = false });
        }

        private Dictionary<string, object> GetControlParameters(BatchInstruction instr, string phaseLabel)
        {
            var p = new Dictionary<string, object>();
            switch (phaseLabel)
            {
                case "PreMix.DoseWater":
                    p["chargeQuantityKg"] = instr.WaterTargetKg;
                    break;
                case "PreMix.DosePaste":
                    p["chargeQuantityKg"] = instr.PasteTargetKg;
                    break;
                case "PreMix.DoseVinegar":
                    p["chargeQuantityKg"] = instr.VinegarTargetKg;
                    break;
                case "PreMix.HighShearMix":
                    p["sugarKg"] = instr.SugarKg;
                    p["saltKg"] = instr.SaltKg;
                    p["spiceKg"] = instr.SpiceKg;
                    p["stabilizerKg"] = instr.StabilizerKg;
                    p["mixSpeedRpm"] = instr.MixSpeedRpm;
                    p["mixDurationSec"] = instr.MixDurationSec;
                    break;
                case "Cook.HeatToTarget":
                    p["targetTemperatureC"] = instr.CookTargetTempC;
                    p["rampRateCPerMin"] = instr.CookRampRateCPerMin;
                    break;
                case "Cook.PasteurizationHold":
                    p["holdTemperatureC"] = instr.CookTargetTempC;
                    p["holdDurationSec"] = instr.HoldDurationSec;
                    p["holdMinTempC"] = instr.HoldMinTempC;
                    break;
                case "Cook.Cool":
                    p["coolTargetTempC"] = instr.CoolTargetTempC;
                    break;
                case "Process.PullVacuum":
                    p["vacuumTargetMbar"] = instr.VacuumTargetMbar;
                    break;
                case "Process.Deaerate":
                    p["deaerateTimeSec"] = instr.DeaerateTimeSec;
                    break;
                case "Process.Homogenize":
                    p["homogPressureBar"] = instr.HomogPressureBar;
                    p["homogPassCount"] = instr.HomogPassCount;
                    break;
            }
            return p;
        }

        private static string[] GetExpectedReportParameters(string phaseLabel)
        {
            return phaseLabel switch
            {
                "PreMix.DoseWater" => new[] { "avgInflowLpm", "totalWaterL" },
                "PreMix.DosePaste" => new[] { "avgPumpCurrentA", "totalPasteKg" },
                "PreMix.DoseVinegar" => new[] { "totalVinegarL" },
                "PreMix.HighShearMix" => new[] { "avgPowerKw", "maxPowerKw", "avgTorqueNm", "avgCurrentA", "maxVibrationMmS" },
                "Cook.HeatToTarget" => new[] { "peakTempC", "avgSteamValvePct", "avgPidOutputPct", "steamUsedKg" },
                "Cook.PasteurizationHold" => new[] { "minTempC", "maxTempC", "avgTempC", "avgPhInline", "steamDuringHoldKg" },
                "Cook.Cool" => new[] { "startTempC", "endTempC" },
                "Process.PullVacuum" => new[] { "peakVacuumMmHg", "finalVacuumMmHg" },
                "Process.Deaerate" => new[] { "minVacuumMmHg", "avgVacuumMmHg" },
                "Process.Homogenize" => new[] { "avgPressureBar", "minPressureBar", "maxPressureBar", "avgMotorCurrentA" },
                _ => Array.Empty<string>()
            };
        }

        // ---- BatchCompleted ----

        public void SetBatchCompletedEvent(BatchInstruction instr, Batch.BatchRecord record, DateTime simNow)
        {
            var phaseSummary = record.Phases.Select(p => new
            {
                phaseLabel = p.PhaseLabel,
                durationSec = (int)p.DurationSec,
                status = "Complete"
            }).ToArray();

            // Aggregate consumptions from phase report parameters
            double steamKg = record.Phases
                .Sum(p => p.ReportParameters.GetValueOrDefault("steamUsedKg") + p.ReportParameters.GetValueOrDefault("steamDuringHoldKg"));

            // Equipment durations by area
            var areaDurations = new Dictionary<string, double>();
            foreach (var phase in record.Phases)
            {
                if (!areaDurations.ContainsKey(phase.AreaName)) areaDurations[phase.AreaName] = 0;
                areaDurations[phase.AreaName] += phase.DurationSec;
            }

            lock (_eventLock)
            {
                _batchCompletedEvent = new
                {
                    eventType = "BatchCompleted",
                    eventId = $"EVT-DONE-{instr.WorkOrderId}",
                    eventVersion = "2.0",
                    timestamp = simNow.ToString("o"),
                    source = BuildSource(),
                    correlationId = $"batch-{instr.BatchRecordId}",
                    data = new
                    {
                        batchRecordId = instr.BatchRecordId,
                        producedBatch = instr.BatchId,
                        workOrderId = instr.WorkOrderId,
                        controlRecipeNumber = instr.ControlRecipeNumber,
                        processOrderNumber = instr.ProcessOrderNumber,
                        materialNumber = instr.ProductCode,
                        masterRecipe = BuildMasterRecipe(instr),
                        isa95Context = BuildIsa95Context(instr),
                        startTime = record.StartTime.ToString("o"),
                        endTime = simNow.ToString("o"),
                        totalDurationSec = (int)record.TotalDurationSec,
                        status = "Complete",
                        pendingQAReview = true,
                        equipmentUsed = new object[]
                        {
                            new { role = "PreMix", unit = instr.PreMixUnit, durationSec = (int)areaDurations.GetValueOrDefault("Area 200") },
                            new { role = "Cook", unit = instr.CookUnit, durationSec = (int)areaDurations.GetValueOrDefault("Area 300") },
                            new { role = "DeaerateAndHomogenize", unit = instr.DeaerateUnit, durationSec = (int)areaDurations.GetValueOrDefault("Area 400") },
                            new { role = "DeaerateAndHomogenize", unit = instr.HomogenizeUnit, durationSec = (int)areaDurations.GetValueOrDefault("Area 400") },
                            new { role = "BufferTransfer", unit = instr.BufferUnit, durationSec = 0 }
                        },
                        phasesCompleted = record.Phases.Count,
                        phasesWithDeviations = 0,
                        phaseSummary,
                        aggregateConsumptions = new
                        {
                            steamKg = Math.Round(steamKg, 1),
                            electricalEnergyKwh = 0.0
                        },
                        aggregateProduction = new
                        {
                            productWeightKg = instr.BatchSizeKg,
                            bufferTankAssigned = instr.BufferUnit
                        },
                        deviationCount = 0,
                        deviationSummary = Array.Empty<object>(),
                        alarmCount = 0,
                        batchRecordLocation = $"ebr://batch-records/{instr.BatchId}.json",
                        batchRecordFormat = "OrderCascade-JSON-v1"
                    }
                };
            }
        }

        private IResult HandleGetBatchCompleted(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_batchCompletedEvent == null) return Results.Ok(new { completed = false });
                return Results.Ok(_batchCompletedEvent);
            }
        }

        private IResult HandleAckBatchCompleted(HttpContext ctx)
        {
            lock (_eventLock) { _batchCompletedEvent = null; }
            _tags?.Write("BATCH_COMPLETED", false);
            Console.WriteLine("[API] BATCH_COMPLETED acknowledged and reset");
            return Results.Ok(new { status = "acknowledged", batchCompleted = false });
        }

        // ---- WorkOrderCompleted ----

        public void SetWorkOrderCompletedEvent(BatchInstruction instr, Batch.BatchRecord record, DateTime simNow)
        {
            lock (_eventLock)
            {
                _woCompletedEvent = new
                {
                    eventType = "WorkOrderCompleted",
                    eventId = $"EVT-WO-COMP-{instr.WorkOrderId}",
                    eventVersion = "2.0",
                    timestamp = simNow.ToString("o"),
                    source = BuildSource(),
                    correlationId = $"workorder-{instr.WorkOrderId}",
                    data = new
                    {
                        workOrderId = instr.WorkOrderId,
                        processOrderNumber = instr.ProcessOrderNumber,
                        controlRecipeNumber = instr.ControlRecipeNumber,
                        batchRecordId = instr.BatchRecordId,
                        producedBatch = instr.BatchId,
                        materialNumber = instr.ProductCode,
                        customer = BuildCustomer(instr),
                        isa95Context = BuildIsa95Context(instr),
                        state = "COMPLETED",
                        stateTimestamp = simNow.ToString("o"),
                        completedAt = simNow.ToString("o"),
                        actualStart = record.StartTime.ToString("o"),
                        actualEnd = simNow.ToString("o"),
                        totalDurationSec = (int)record.TotalDurationSec,
                        yield = new
                        {
                            expectedQuantity = instr.BatchSizeKg,
                            actualQuantity = instr.BatchSizeKg,
                            unitOfMeasure = "KG",
                            variancePct = 0.0
                        },
                        deviationCount = 0,
                        alarmCount = 0,
                        pendingQAReview = true,
                        ebrId = instr.BatchRecordId
                    }
                };
            }
        }

        private IResult HandleGetWoCompleted(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_woCompletedEvent == null) return Results.Ok(new { completed = false });
                return Results.Ok(_woCompletedEvent);
            }
        }

        private IResult HandleAckWoCompleted(HttpContext ctx)
        {
            lock (_eventLock) { _woCompletedEvent = null; }
            _tags?.Write("WO_COMPLETED", false);
            Console.WriteLine("[API] WO_COMPLETED acknowledged and reset");
            return Results.Ok(new { status = "acknowledged", woCompleted = false });
        }

        // ---- BatchReleased (auto-released with simulated lab results) ----

        public void SetBatchReleasedEvent(BatchInstruction instr, Batch.BatchRecord record, DateTime simNow)
        {
            var rng = new Random((int)(simNow.Ticks % int.MaxValue));
            double ph = Math.Round(3.65 + rng.NextDouble() * 0.15, 2);
            double brix = Math.Round(30.2 + rng.NextDouble() * 1.6, 1);
            double visc = Math.Round(4600 + rng.NextDouble() * 800, 0);

            lock (_eventLock)
            {
                _batchReleasedEvent = new
                {
                    eventType = "BatchReleased",
                    eventId = $"EVT-REL-{instr.WorkOrderId}",
                    eventVersion = "2.0",
                    timestamp = simNow.ToString("o"),
                    source = new
                    {
                        origin = "QM-LabSystem",
                        uri = "https://qm.globalindustries.com/lab"
                    },
                    correlationId = $"batch-{instr.BatchRecordId}",
                    data = new
                    {
                        batchRecordId = instr.BatchRecordId,
                        producedBatch = instr.BatchId,
                        workOrderId = instr.WorkOrderId,
                        controlRecipeNumber = instr.ControlRecipeNumber,
                        processOrderNumber = instr.ProcessOrderNumber,
                        materialNumber = instr.ProductCode,
                        isa95Context = BuildIsa95Context(instr),
                        batchCompletedAt = simNow.ToString("o"),
                        releasedAt = simNow.ToString("o"),
                        disposition = "Released",
                        releaseType = "Full",
                        releasedBy = new
                        {
                            userId = "qa-7741",
                            name = "M. Mueller",
                            role = "QAReleaseAuthority",
                            qualifications = new[] { "L3-QARelease", "FoodSafety-Adv" }
                        },
                        electronicSignature = new
                        {
                            userId = "qa-7741",
                            timestamp = simNow.ToString("o"),
                            signatureAlgorithm = "SHA-256",
                            reason = "Final QA release after lab pass"
                        },
                        labResults = new
                        {
                            samplesTestedAt = simNow.ToString("o"),
                            performedBy = "lab-tech-3201",
                            results = new
                            {
                                pH = new { value = ph, spec = "3.6-3.8", unit = "PH", status = ph >= 3.6 && ph <= 3.8 ? "Pass" : "Fail" },
                                brix = new { value = brix, spec = "30-32", unit = "DEGBX", status = brix >= 30 && brix <= 32 ? "Pass" : "Fail" },
                                viscosity = new { value = visc, spec = "4500-5500", unit = "CP", status = visc >= 4500 && visc <= 5500 ? "Pass" : "Fail" }
                            },
                            sensory = new
                            {
                                evaluatedBy = "panel-A",
                                appearance = "Pass",
                                flavor = "Pass",
                                texture = "Pass",
                                overallAcceptability = "Pass"
                            }
                        },
                        deviationResolutions = Array.Empty<object>(),
                        finalProduct = new
                        {
                            bufferTank = instr.BufferUnit,
                            comment = "Semi-finished bulk; downstream packaging work order will fill bottles."
                        },
                        genealogy = new
                        {
                            consumedMaterialLots = instr.Bom.Select(b => b.Lot).Where(l => !string.IsNullOrEmpty(l)).ToArray(),
                            producedLots = new[] { instr.BatchId }
                        },
                        inventoryStatus = "AvailableForShipment",
                        inventoryLocation = instr.BufferUnit
                    }
                };
            }
        }

        private IResult HandleGetBatchReleased(HttpContext ctx)
        {
            lock (_eventLock)
            {
                if (_batchReleasedEvent == null) return Results.Ok(new { released = false });
                return Results.Ok(_batchReleasedEvent);
            }
        }

        private IResult HandleAckBatchReleased(HttpContext ctx)
        {
            lock (_eventLock) { _batchReleasedEvent = null; }
            _tags?.Write("BATCH_RELEASED", false);
            Console.WriteLine("[API] BATCH_RELEASED acknowledged and reset");
            return Results.Ok(new { status = "acknowledged", batchReleased = false });
        }

        private void LogEvent(string message)
        {
            _eventLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
            if (_eventLog.Count > 100) _eventLog.RemoveAt(0);
        }
    }
}
