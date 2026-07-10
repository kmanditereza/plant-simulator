using BatchPlant.Api;

namespace BatchPlant.Core
{
    /// <summary>
    /// Detects downtime edges on each line and latches the resulting events into the API server's
    /// queue, exactly as the batch lifecycle events are latched.
    ///
    /// Why latch instead of letting the UNS bridge poll the flag: a breakdown's MTTR is a few
    /// plant-minutes, which at SIM_SPEED=60 is a few WALL seconds. A 5-second poll of the boolean
    /// flag would miss short downtimes entirely and quantise every duration to the poll interval.
    /// Detecting here -- on the simulation tick -- means no edge is ever missed, and durations are
    /// measured in plant time to the tick.
    ///
    /// Runs at Priority 60: after Area 500 (40) and BatchManager (50) have published this tick's
    /// flag and reason, and before the tag flusher (90).
    ///
    /// One open downtime per line: a second DowntimeStarted cannot be raised until the first closes.
    /// </summary>
    public class DowntimeManager : ISimulatable
    {
        private readonly PlantContext _ctx;
        private ApiServer? _apiServer;

        public int Priority => 60;

        public DowntimeManager(PlantContext ctx) { _ctx = ctx; }

        public void ConnectApiServer(ApiServer apiServer) => _apiServer = apiServer;

        private sealed class LineState
        {
            public string LineId = "";
            public string Area = "";
            public string FlagTag = "";
            public string ReasonTag = "";
            public bool Open;
            public DateTime StartedAt;
            public string StartReason = "";
            public string LastReason = "";
        }

        private readonly LineState[] _lines =
        {
            new LineState { LineId = "PackLine",  Area = "PackagingArea",  FlagTag = "PACK_LINE_DOWNTIME",  ReasonTag = "PACK_LINE_STATE_REASON" },
            new LineState { LineId = "BatchLine", Area = "ProductionArea", FlagTag = "BATCH_LINE_DOWNTIME", ReasonTag = "BATCH_LINE_STATE_REASON" },
        };

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            foreach (var line in _lines)
            {
                bool down = _ctx.Tags.Read<bool>(line.FlagTag, false);
                string reason = _ctx.Tags.Read<string>(line.ReasonTag, "") ?? "";

                if (down && !line.Open)
                {
                    line.Open = true;
                    line.StartedAt = simNow;
                    line.StartReason = reason;
                    line.LastReason = reason;
                    _apiServer?.EnqueueDowntimeEvent(BuildStarted(line, simNow));
                    Console.WriteLine($"[{simNow:HH:mm:ss}] [DOWNTIME] {line.LineId} DOWN ({reason})");
                }
                else if (!down && line.Open)
                {
                    line.Open = false;
                    double durationSec = (simNow - line.StartedAt).TotalSeconds;
                    _apiServer?.EnqueueDowntimeEvent(BuildEnded(line, simNow, durationSec, reason));
                    Console.WriteLine($"[{simNow:HH:mm:ss}] [DOWNTIME] {line.LineId} UP after " +
                                      $"{durationSec:F0}s plant time (cause: {line.StartReason})");
                }
                else if (down && line.Open)
                {
                    line.LastReason = reason;   // cause can evolve while down; publish nothing
                }
            }
        }

        private static string CategoryOf(string reason) => reason switch
        {
            "MachineFault" => "UnplannedDowntime",
            "Starved"      => "Starved",
            "Blocked"      => "Blocked",
            _              => "Other"
        };

        private object Isa95(LineState line) => new
        {
            enterprise = "GlobalIndustries",
            site = "Munich",
            area = line.Area,
            processCell = "Line1"
        };

        private static object Source() => new
        {
            origin = "MES-LineMonitor",
            uri = "opc.tcp://localhost:26543/BatchPlantServer",
            gateway = "Ignition-Gateway-Munich-Line1",
            originSystemId = 1030
        };

        private string CorrelationId(LineState line) =>
            $"downtime-{line.LineId}-{line.StartedAt.Ticks}";

        private object BuildStarted(LineState line, DateTime simNow) => new
        {
            eventType = "DowntimeStarted",
            eventId = $"EVT-DT-START-{line.LineId}-{line.StartedAt.Ticks}",
            eventVersion = "2.0",
            timestamp = simNow.ToString("o"),
            source = Source(),
            correlationId = CorrelationId(line),
            data = new
            {
                lineId = line.LineId,
                isa95Context = Isa95(line),
                state = "Down",
                reason = line.StartReason,
                category = CategoryOf(line.StartReason),
                plannedDowntime = false,
                startedAt = simNow.ToString("o"),
                comment = "Line stopped producing. The reason carried here is the line's reported state at the moment " +
                          "of the stop and is provisional; root cause has not been confirmed by the operator. The confirmed " +
                          "cause and the total duration follow in DowntimeEnded, which shares this correlationId."
            }
        };

        private object BuildEnded(LineState line, DateTime simNow, double durationSec, string resumedReason) => new
        {
            eventType = "DowntimeEnded",
            eventId = $"EVT-DT-END-{line.LineId}-{line.StartedAt.Ticks}",
            eventVersion = "2.0",
            timestamp = simNow.ToString("o"),
            source = Source(),
            correlationId = CorrelationId(line),   // pairs with the DowntimeStarted
            data = new
            {
                lineId = line.LineId,
                isa95Context = Isa95(line),
                state = "Running",
                reason = line.StartReason,     // what took the line down
                lastReason = line.LastReason,  // last reason seen before it recovered
                category = CategoryOf(line.StartReason),
                plannedDowntime = false,
                startedAt = line.StartedAt.ToString("o"),
                endedAt = simNow.ToString("o"),
                durationSeconds = Math.Round(durationSec, 1),   // plant seconds, measured to the tick
                resumedReason,
                comment = "Line resumed production. Duration is measured from the moment production stopped to the moment it " +
                          "resumed. 'reason' is the condition that took the line down; 'lastReason' is the condition observed " +
                          "immediately before recovery, which may differ where one stoppage cascaded into another."
            }
        };
    }
}
