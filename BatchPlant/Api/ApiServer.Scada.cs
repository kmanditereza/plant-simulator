using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace BatchPlant.Api
{
    /// <summary>
    /// The plant SCADA overview: a process graphic of the whole plant driven by live tag values.
    ///
    /// Two read-only endpoints feed it. The catalog (every tag's folder, unit and description) is
    /// fetched once; the values are polled. Splitting them keeps the poll small -- the catalog is
    /// static for the life of the process, the values are what change.
    ///
    /// The page itself is an embedded resource rather than a wwwroot folder, for the same reason
    /// the vibration console is a const string: the container copies only the publish output, and
    /// a resource compiled into the assembly cannot be left behind by a forgotten COPY. Unlike a
    /// const string it stays a real .html file, which matters at this size.
    /// </summary>
    public partial class ApiServer
    {
        private const string ScadaResourceName = "BatchPlant.Scada.html";

        private static readonly Lazy<string> ScadaHtml = new(() =>
        {
            using var stream = typeof(ApiServer).Assembly.GetManifestResourceStream(ScadaResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{ScadaResourceName}' not found");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });

        private void MapScadaEndpoints(IEndpointRouteBuilder app)
        {
            app.MapGet("/", HandleScadaUi);
            app.MapGet("/scada", HandleScadaUi);
            app.MapGet("/api/scada/catalog", HandleScadaCatalog);
            app.MapGet("/api/scada/values", HandleScadaValues);
        }

        private IResult HandleScadaUi() => Results.Content(ScadaHtml.Value, "text/html; charset=utf-8");

        private IResult HandleScadaCatalog()
        {
            if (_tags == null) return Results.Json(new { error = "tag registry not connected" }, statusCode: 503);
            var arr = new JsonArray();
            foreach (var d in _tags.GetAllDefinitions().OrderBy(d => d.FolderPath).ThenBy(d => d.Name))
            {
                arr.Add(new JsonObject
                {
                    ["name"] = d.Name,
                    ["folder"] = d.FolderPath,
                    ["unit"] = d.Unit,
                    ["description"] = d.Description,
                    ["type"] = d.DataType.ToString(),
                    ["writable"] = d.Writable,
                });
            }
            return Results.Content(new JsonObject { ["count"] = arr.Count, ["tags"] = arr }.ToJsonString(),
                "application/json");
        }

        private IResult HandleScadaValues()
        {
            if (_tags == null) return Results.Json(new { error = "tag registry not connected" }, statusCode: 503);
            var values = new JsonObject();
            foreach (var d in _tags.GetAllDefinitions())
                values[d.Name] = ToJson(_tags.Read(d.Name));

            return Results.Content(new JsonObject
            {
                ["plantTime"] = _plant?.Clock.Now.ToString("o"),
                ["serverTime"] = DateTime.UtcNow.ToString("o"),
                ["values"] = values,
            }.ToJsonString(), "application/json");
        }

        /// <summary>
        /// Tag values are primitives, but a double can be NaN or infinite mid-calculation, and
        /// System.Text.Json refuses to write those. A non-finite reading has no meaningful value
        /// to show, so it goes out as null and the page renders it as bad quality.
        /// </summary>
        private static JsonNode? ToJson(object? v) => v switch
        {
            null => null,
            double d => double.IsFinite(d) ? JsonValue.Create(Math.Round(d, 4)) : null,
            float f => float.IsFinite(f) ? JsonValue.Create(Math.Round(f, 4)) : null,
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            long l => JsonValue.Create(l),
            uint u => JsonValue.Create(u),
            short s => JsonValue.Create(s),
            ushort us => JsonValue.Create(us),
            byte by => JsonValue.Create(by),
            string str => JsonValue.Create(str),
            DateTime dt => JsonValue.Create(dt.ToString("o")),
            _ => JsonValue.Create(v.ToString()),
        };
    }
}
