using Opc.Ua;
using Opc.Ua.Configuration;
using BatchPlant.Api;
using BatchPlant.Core;
using BatchPlant.OpcUa;
using BatchPlant.Process;
using BatchPlant.Equipment;
using BatchPlant.Batch;

namespace BatchPlant
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  KETCHUP BATCH PLANT SIMULATOR v2.0                           ║");
            Console.WriteLine("║  OPC UA Process Data Server                                   ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                // Read simulation speed from environment (default: 1.0 = real-time)
                double simSpeed = 1.0;
                var speedEnv = Environment.GetEnvironmentVariable("SIM_SPEED");
                if (!string.IsNullOrEmpty(speedEnv) && double.TryParse(speedEnv, out var parsed))
                {
                    simSpeed = Math.Clamp(parsed, 0.1, 60.0);
                }

                // Optional: start time override (default: next Monday 06:00)
                DateTime? simStart = null;
                var startEnv = Environment.GetEnvironmentVariable("SIM_START");
                if (!string.IsNullOrEmpty(startEnv) && DateTime.TryParse(startEnv, out var parsedStart))
                {
                    simStart = parsedStart;
                }

                // Shift length in REAL (wall-clock) minutes (default 90), independent of SIM_SPEED.
                double shiftMinutes = 90.0;
                var shiftEnv = Environment.GetEnvironmentVariable("SHIFT_MINUTES");
                if (!string.IsNullOrEmpty(shiftEnv) && double.TryParse(shiftEnv, out var parsedShift) && parsedShift > 0)
                {
                    shiftMinutes = parsedShift;
                }

                // 1. Create core infrastructure
                var clock = new SimClock(simStart, simSpeed, shiftMinutes);
                var tags = new TagRegistry();
                var ctx = new PlantContext(clock, tags);
                var engine = new SimEngine(clock);

                Console.WriteLine($"[CLOCK] Simulation time: {clock.Now:yyyy-MM-dd HH:mm:ss dddd}");
                Console.WriteLine($"[CLOCK] Speed: {simSpeed}x (1 real second = {simSpeed} sim seconds)");
                Console.WriteLine($"[CLOCK] Shift: {clock.ShiftLabel} ({shiftMinutes} real-min per shift)");
                Console.WriteLine();

                // 2. Register all tags (this defines the OPC UA address space)
                TagCatalog.RegisterAll(tags);
                Console.WriteLine($"[TAGS] Registered {tags.TagCount} tags across 8 plant areas");

                // Handle --export-structure flag: dump address space to filesystem and exit
                if (args.Contains("--export-structure"))
                {
                    var exportRoot = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "..", "..", "..", "..", "Objects");
                    exportRoot = Path.GetFullPath(exportRoot);
                    TagExporter.ExportToDirectory(tags, exportRoot);
                    return;
                }

                // 3. Initialize and start OPC UA server
                var server = await StartOpcUaServerAsync(tags);
                Console.WriteLine("[OPCUA] Server started successfully");
                Console.WriteLine();

                // 4. Register simulation components
                var flusher = new TagFlusher(tags);
                engine.Register(flusher);

                // Shift window: publishes the numbered-shift tags and services SHIFT_RESET.
                // Priority 5 => runs before the line controllers, so a reset lands on the
                // same tick they roll their per-shift counters over.
                engine.Register(new ShiftManager(ctx));
                Console.WriteLine($"[SHIFT] Shift manager registered (shift 1 on boot, {shiftMinutes} real-min per shift, SHIFT_RESET tag)");

                // Area 800: Utilities (no dependencies)
                var area800 = new Area800_UtilitiesController(ctx);
                engine.Register(area800);
                Console.WriteLine("[AREA800] Utilities controller registered (steam, cooling, air, hot water)");

                // Area 100: Raw material storage (no dependencies, provides draw API)
                var area100 = new Area100_RawMaterialsController(ctx);
                engine.Register(area100);
                Console.WriteLine("[AREA100] Raw Materials controller registered (paste tanks, vinegar, silos, P-101/102)");

                // Work Order Queue - receives instructions from HTTP API
                var workOrderQueue = new WorkOrderQueue();
                // Packaging Order Queue - gates the pack line (make/pack split)
                var packagingQueue = new PackagingOrderQueue();

                // Area 200: Pre-Mix (depends on Area 100 for ingredients, IProductSource for Area 300)
                var area200 = new Area200_PreMixController(ctx, area100);
                area200.ConnectWorkOrderQueue(workOrderQueue);
                engine.Register(area200);
                Console.WriteLine("[AREA200] Pre-Mix controller registered (MX-201, AGIT-201, XV-201..204, FCV-201)");

                // Area 450: Buffer tanks (no inbound deps - Area 400 pushes here)
                var area450 = new Area450_BufferTanksController(ctx);
                engine.Register(area450);
                Console.WriteLine("[AREA450] Buffer tanks controller registered (BT-451, BT-452, BT-453)");

                // Gate the batch line: hold new-batch starts while all buffer tanks are full.
                area200.ConnectBufferGate(() => area450.HasCapacityForNewBatch());

                // Area 300: Cooking (depends on Area 800 for steam, Area 200 for upstream product)
                var area300 = new Area300_CookingController(ctx, area800, area200);
                engine.Register(area300);
                Console.WriteLine("[AREA300] Cooking controller registered (KT-301, AGIT-301, TCV-301, SV301, PID_301)");

                // Area 400: Deaeration and Homogenization (pulls from Area 300, pushes to Area 450)
                var area400 = new Area400_ProcessingController(ctx, area300, area450);
                engine.Register(area400);
                Console.WriteLine("[AREA400] Processing controller registered (DA-401 vacuum, HG-401 homogenizer)");

                // Area 500: Filling line (gated on a packaging order; draws from Area 450 buffers)
                var area500 = new Area500_FillingLineController(ctx, area450, packagingQueue);
                engine.Register(area500);
                Console.WriteLine("[AREA500] Filling line controller registered (FL-501, CAP-501, inv/cool tunnels, CONV-501)");

                // Area 600: Packaging line (consumes bottles from Area 500)
                var area600 = new Area600_PackagingLineController(ctx, area500);
                engine.Register(area600);
                Console.WriteLine("[AREA600] Packaging line controller registered (LBL-601, DC-601, XR-601, CP-601, PAL-601)");

                // Area 700: CIP (Clean-in-Place)
                var area700 = new Area700_CIPController(ctx);
                engine.Register(area700);
                Console.WriteLine("[AREA700] CIP controller registered (7-step sequence: pre-rinse, caustic, int-rinse, acid, final-rinse, sanitize, drain)");

                // Batch Manager - observes state transitions, assigns Batch IDs, collects report parameters
                var batchManager = new BatchManager(ctx);
                batchManager.ConnectInstructionSource(() => area200.ActiveInstruction);
                batchManager.ConnectBuffers(area450);   // buffer level + per-order delivered qty
                batchManager.ConnectWorkOrderQueue(workOrderQueue);   // distinguishes Blocked from Idle
                engine.Register(batchManager);
                Console.WriteLine("[BATCH] BatchManager registered - observing Area 200/300/400, writing records to ./batch-records/");

                // Downtime Manager - latches downtime edges for both lines into the API queue
                var downtimeManager = new DowntimeManager(ctx);
                engine.Register(downtimeManager);
                Console.WriteLine("[DOWNTIME] Downtime manager registered (latches DowntimeStarted/Ended for both lines)");

                // Fault Manager - latches equipment fault onsets (FailureOccurred) for both lines
                var faultManager = new FaultManager(ctx);
                engine.Register(faultManager);
                Console.WriteLine("[FAULT] Fault manager registered (latches FailureOccurred per machine for both lines)");

                // Power Metering - 5 meters at MCC level, one per work center
                var powerMetering = new PowerMeteringController(ctx);
                engine.Register(powerMetering);
                Console.WriteLine("[POWER] Power metering registered (PM-PL1, PM-CIP, PM-PKG1, PM-UTIL, PM-RM)");

                // Remaining areas: lightweight idle simulator until their controllers are built
                var idleSim = new IdleSimulator(ctx);
                engine.Register(idleSim);

                // 5. Start the simulation engine
                engine.Start();

                // 6. Start HTTP API server for work order dispatch
                int apiPort = 5000;
                var apiPortEnv = Environment.GetEnvironmentVariable("API_PORT");
                if (!string.IsNullOrEmpty(apiPortEnv) && int.TryParse(apiPortEnv, out var parsedPort))
                    apiPort = parsedPort;

                var apiServer = new ApiServer(workOrderQueue, tags, packagingQueue);
                batchManager.ConnectApiServer(apiServer);
                area500.ConnectApiServer(apiServer);
                downtimeManager.ConnectApiServer(apiServer);
                faultManager.ConnectApiServer(apiServer);
                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls($"http://0.0.0.0:{apiPort}");
                builder.Logging.ClearProviders(); // don't pollute console with ASP.NET logs
                var webapp = builder.Build();
                apiServer.MapEndpoints(webapp);
                _ = webapp.RunAsync(); // fire and forget — runs alongside OPC UA

                Console.WriteLine($"[API] HTTP API started on port {apiPort}");
                Console.WriteLine($"[API]   POST /api/control-recipe  - Download control recipe");
                Console.WriteLine($"[API]   POST /api/work-order      - Dispatch work order");
                Console.WriteLine($"[API]   POST /api/packaging-order - Dispatch packaging order (gates pack line)");
                Console.WriteLine($"[API]   GET  /api/packaging/completed - Poll completed packaging run");
                Console.WriteLine($"[API]   GET  /api/downtime/events     - Drain queued downtime events");
                Console.WriteLine($"[API]   GET  /api/status          - Queue and batch status");

                // Write initial sim metadata
                tags.Write("SIM_SPEED", simSpeed);
                tags.Write("SIM_TIME", clock.Now);
                tags.Write("PLANT_MODE", "Idle");

                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine("  Server is running. Connect with any OPC UA client to:");
                Console.WriteLine("  opc.tcp://localhost:26543/BatchPlantServer");
                Console.WriteLine($"  HTTP API: http://localhost:{apiPort}/api/status");
                Console.WriteLine("  Press Ctrl+C to exit.");
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine();

                // Handle graceful shutdown
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                // Keep alive until Ctrl+C
                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException) { }

                // Shutdown
                Console.WriteLine();
                Console.WriteLine("[SHUTDOWN] Stopping simulation...");
                engine.Stop();
                Console.WriteLine("[SHUTDOWN] Complete.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        static async Task<PlantServer> StartOpcUaServerAsync(TagRegistry tags)
        {
            Console.WriteLine("[OPCUA] Starting OPC UA server...");

            var application = new ApplicationInstance
            {
                ApplicationType = ApplicationType.Server,
                ConfigSectionName = "BatchPlantServer"
            };

            // Find config file
            var dockerConfigPath = "/config/BatchPlantServer.Config.xml";
            var localConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "BatchPlantServer.Config.xml");
            var configPath = File.Exists(dockerConfigPath) ? dockerConfigPath : localConfigPath;

            Console.WriteLine($"[OPCUA] Config: {configPath}");

            await application.LoadApplicationConfiguration(configPath, false);
            await application.CheckApplicationInstanceCertificate(false, 0);

            var server = new PlantServer(tags);
            await application.Start(server);

            foreach (var endpoint in application.Server.GetEndpoints())
            {
                Console.WriteLine($"[OPCUA] Endpoint: {endpoint.EndpointUrl}");
            }

            return server;
        }
    }

    /// <summary>
    /// Minimal idle-state simulator that keeps tags alive with realistic noise.
    /// Will be replaced by full process area controllers in later phases.
    /// </summary>
    internal class IdleSimulator : ISimulatable
    {
        private readonly PlantContext _ctx;
        private int _logCounter;

        public int Priority => 40;

        public IdleSimulator(PlantContext ctx) { _ctx = ctx; }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            var tags = _ctx.Tags;
            var rng = _ctx.Rng;

            // Update sim time tag
            tags.Write("SIM_TIME", simNow);

            // Area 100: owned by Area100_RawMaterialsController (not touched here)
            // Area 200: owned by Area200_PreMixController (not touched here)
            // Area 300: owned by Area300_CookingController (not touched here)
            // Area 400: owned by Area400_ProcessingController (not touched here)
            // Area 450: owned by Area450_BufferTanksController (not touched here)
            // Area 800: owned by Area800_UtilitiesController (not touched here)

            // Periodic console log
            _logCounter++;
            if (_logCounter % 100 == 0) // Every 10 seconds at 100ms tick rate
            {
                Console.WriteLine($"[{simNow:HH:mm:ss}] {_ctx.Clock.ShiftLabel} | {tags.TagCount} tags | Idle");
            }
        }
    }
}
