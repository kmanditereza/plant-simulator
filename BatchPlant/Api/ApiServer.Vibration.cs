using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Api
{
    /// <summary>
    /// Operator-facing vibration fault injection.
    ///
    /// The plant already models vibration honestly -- it grows with load and accumulated wear,
    /// and doubles under a genuine equipment fault. What it could not do was fail ON DEMAND,
    /// which is what you need to exercise a condition-monitoring pipeline: an alarm rule, a
    /// historian query, an anomaly detector, or an agent that is supposed to notice and act.
    /// Waiting for a modelled MTBF to fire is not a test strategy.
    ///
    /// Endpoints are deliberately small and idempotent-ish: injecting twice re-aims the same
    /// fault rather than stacking, and clearing something already healthy is not an error.
    /// </summary>
    public partial class ApiServer
    {
        private PlantContext? _plant;

        /// <summary>Give the API access to the plant, matching the ConnectApiServer pattern used elsewhere.</summary>
        public void ConnectPlant(PlantContext ctx) => _plant = ctx;

        private void MapVibrationEndpoints(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/vibration/assets", HandleVibrationAssets);
            app.MapPost("/api/vibration/anomaly", HandleInjectAnomaly);
            app.MapPost("/api/vibration/anomaly/clear", HandleClearAnomaly);
            app.MapGet("/sim", HandleSimUi);
            app.MapGet("/", HandleSimUi);
        }

        private static JsonObject Describe(ConditionMonitor c)
        {
            var o = new JsonObject
            {
                ["assetId"] = c.AssetId,
                ["name"] = string.IsNullOrWhiteSpace(c.AssetName) ? c.AssetId : c.AssetName,
                ["area"] = c.Area,
                ["vibrationTag"] = c.VibrationTag,
                ["vibration"] = Math.Round(c.Vibration, 3),
                ["healthyVibration"] = Math.Round(c.HealthyVibration, 3),
                ["factor"] = Math.Round(c.AnomalyFactor, 2),
                ["bearingTemp"] = Math.Round(c.BearingTemp, 1),
                ["mcsaIndex"] = Math.Round(c.McsaIndex, 1),
                ["failureCount"] = c.FailureCount,
            };
            var a = c.Anomaly;
            o["anomaly"] = a == null ? null : new JsonObject
            {
                ["mode"] = a.Mode.ToString(),
                ["severity"] = a.Severity,
                ["rampMinutes"] = a.RampMinutes,
                ["durationMinutes"] = a.DurationMinutes,
                ["elapsedMinutes"] = Math.Round(a.ElapsedMinutes, 2),
                ["reason"] = a.Reason,
                ["injectedAt"] = a.InjectedAt.ToString("o"),
            };
            return o;
        }

        private IResult HandleVibrationAssets()
        {
            if (_plant == null) return Results.Json(new { error = "plant not connected" }, statusCode: 503);
            var arr = new JsonArray();
            foreach (var c in _plant.ConditionMonitors.All) arr.Add(Describe(c));
            return Results.Content(new JsonObject
            {
                ["plantTime"] = _plant.Clock.Now.ToString("o"),
                ["count"] = arr.Count,
                ["assets"] = arr
            }.ToJsonString(), "application/json");
        }

        private async Task<IResult> HandleInjectAnomaly(HttpRequest req)
        {
            if (_plant == null) return Results.Json(new { error = "plant not connected" }, statusCode: 503);

            JsonObject? body;
            try { body = (await JsonNode.ParseAsync(req.Body)) as JsonObject; }
            catch (Exception ex) { return Results.Json(new { error = "invalid JSON: " + ex.Message }, statusCode: 400); }
            if (body == null) return Results.Json(new { error = "expected a JSON object" }, statusCode: 400);

            string assetId = body["assetId"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(assetId))
                return Results.Json(new { error = "assetId is required" }, statusCode: 400);
            if (!_plant.ConditionMonitors.TryGet(assetId, out var cm))
                return Results.Json(new { error = $"unknown assetId '{assetId}'" }, statusCode: 404);

            string modeStr = body["mode"]?.GetValue<string>() ?? "Step";
            if (!Enum.TryParse<VibrationAnomalyMode>(modeStr, ignoreCase: true, out var mode))
                return Results.Json(new { error = $"mode must be Step, Ramp or Spike (got '{modeStr}')" }, statusCode: 400);

            double Num(string k, double dflt)
            {
                var n = body[k];
                if (n == null) return dflt;
                try { return n.GetValue<double>(); } catch { return dflt; }
            }

            double severity = Num("severity", 3.0);
            if (severity < 1.0 || severity > 25.0)
                return Results.Json(new { error = "severity must be between 1.0 and 25.0" }, statusCode: 400);

            var anomaly = new VibrationAnomaly
            {
                Mode = mode,
                Severity = severity,
                RampMinutes = Math.Max(0, Num("rampMinutes", 30.0)),
                DurationMinutes = Math.Max(0, Num("durationMinutes", 0.0)),
                Reason = body["reason"]?.GetValue<string>() ?? "",
                InjectedAt = _plant.Clock.Now,
            };
            cm.InjectAnomaly(anomaly);

            Console.WriteLine($"[VIB] injected {mode} x{severity:F1} on {cm.AssetId} ({cm.AssetName})" +
                              (anomaly.DurationMinutes > 0 ? $" for {anomaly.DurationMinutes:F0} min" : " until cleared"));

            return Results.Content(Describe(cm).ToJsonString(), "application/json");
        }

        private async Task<IResult> HandleClearAnomaly(HttpRequest req)
        {
            if (_plant == null) return Results.Json(new { error = "plant not connected" }, statusCode: 503);

            JsonObject? body = null;
            try { body = (await JsonNode.ParseAsync(req.Body)) as JsonObject; } catch { /* empty body = clear all */ }

            bool all = body?["all"]?.GetValue<bool>() ?? false;
            string assetId = body?["assetId"]?.GetValue<string>() ?? "";

            if (all || string.IsNullOrWhiteSpace(assetId))
            {
                int n = _plant.ConditionMonitors.ClearAll();
                Console.WriteLine($"[VIB] cleared {n} injected fault(s)");
                return Results.Content(new JsonObject { ["cleared"] = n }.ToJsonString(), "application/json");
            }

            if (!_plant.ConditionMonitors.TryGet(assetId, out var cm))
                return Results.Json(new { error = $"unknown assetId '{assetId}'" }, statusCode: 404);

            bool had = cm.ClearAnomaly();
            Console.WriteLine($"[VIB] cleared {cm.AssetId} ({(had ? "was faulted" : "was already healthy")})");
            return Results.Content(Describe(cm).ToJsonString(), "application/json");
        }
    }
}
