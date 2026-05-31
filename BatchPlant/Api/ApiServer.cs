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
        private readonly List<string> _eventLog = new();
        private readonly object _lock = new();

        public ApiServer(WorkOrderQueue queue)
        {
            _queue = queue;
        }

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/control-recipe", HandleControlRecipe);
            app.MapPost("/api/work-order", HandleWorkOrder);
            app.MapGet("/api/status", HandleStatus);
            app.MapGet("/api/recipes", HandleListRecipes);
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

            lock (_lock)
            {
                LogEvent($"Work order dispatched: {workOrderId}, batch={batchId}, recipe={crNumber}");
            }

            var customerName = instruction.CustomerName;
            Console.WriteLine($"[API] Work order dispatched: {workOrderId} -> batch {batchId}" +
                (string.IsNullOrEmpty(customerName) ? "" : $" [{customerName}]") +
                $" (queue={_queue.PendingCount})");

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

        private void LogEvent(string message)
        {
            _eventLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
            if (_eventLog.Count > 100) _eventLog.RemoveAt(0);
        }
    }
}
