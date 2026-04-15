using Opc.Ua;
using Opc.Ua.Configuration;
using BatchPlant.Core;
using BatchPlant.OpcUa;
using BatchPlant.Process;

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

                // 1. Create core infrastructure
                var clock = new SimClock(simStart, simSpeed);
                var tags = new TagRegistry();
                var ctx = new PlantContext(clock, tags);
                var engine = new SimEngine(clock);

                Console.WriteLine($"[CLOCK] Simulation time: {clock.Now:yyyy-MM-dd HH:mm:ss dddd}");
                Console.WriteLine($"[CLOCK] Speed: {simSpeed}x (1 real second = {simSpeed} sim seconds)");
                Console.WriteLine($"[CLOCK] Shift: {clock.ShiftLabel}");
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
                //    For now: TagFlusher (pushes values to OPC UA) + idle sensor simulation
                var flusher = new TagFlusher(tags);
                engine.Register(flusher);

                // Register a simple idle simulator to show tags are alive
                var idleSim = new IdleSimulator(ctx);
                engine.Register(idleSim);

                // 5. Start the simulation engine
                engine.Start();

                // Write initial sim metadata
                tags.Write("SIM_SPEED", simSpeed);
                tags.Write("SIM_TIME", clock.Now);
                tags.Write("PLANT_MODE", "Idle");
                tags.Write("SHIFT_ID", clock.CurrentShift.ToString());

                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine("  Server is running. Connect with any OPC UA client to:");
                Console.WriteLine("  opc.tcp://localhost:26543/BatchPlantServer");
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
            tags.Write("SHIFT_ID", _ctx.Clock.CurrentShift.ToString());

            // Area 100: Raw material storage - slow ambient drift
            tags.Write("PASTE_TK1_TEMP", Math.Round(_ctx.AddNoise(18.0, 0.3), 1));
            tags.Write("PASTE_TK2_TEMP", Math.Round(_ctx.AddNoise(18.0, 0.3), 1));
            tags.Write("VIN_TK_TEMP", Math.Round(_ctx.AddNoise(20.0, 0.2), 1));
            tags.Write("H2O_PRESS", Math.Round(_ctx.AddNoise(4.2, 0.05), 2));
            tags.Write("H2O_TEMP", Math.Round(_ctx.AddNoise(15.0, 0.3), 1));

            // Area 200: Premix idle
            tags.Write("TIT-201", Math.Round(_ctx.AddNoise(22.0, 0.15), 1));

            // Area 300: Kettle idle
            tags.Write("TIT-301", Math.Round(_ctx.AddNoise(22.0, 0.15), 1));
            tags.Write("TIT-302", Math.Round(_ctx.AddNoise(22.0, 0.15), 1));

            // Area 400: idle
            tags.Write("401-TI-001", Math.Round(_ctx.AddNoise(22.0, 0.2), 1));

            // Area 450: Buffer tanks idle
            tags.Write("BT451_TEMP", Math.Round(_ctx.AddNoise(22.0, 0.15), 1));
            tags.Write("BT452_TEMP", Math.Round(_ctx.AddNoise(22.0, 0.15), 1));
            tags.Write("BT453_TEMP", Math.Round(_ctx.AddNoise(22.0, 0.15), 1));

            // Area 800: Utilities always running
            tags.Write("STM_HDR_PRESS", Math.Round(_ctx.AddNoise(6.0, 0.05), 2));
            tags.Write("STM_HDR_TEMP", Math.Round(_ctx.AddNoise(165.0, 0.5), 1));
            tags.Write("BOILER_PRESS", Math.Round(_ctx.AddNoise(7.0, 0.03), 2));
            tags.Write("BOILER_EFF", Math.Round(_ctx.AddNoise(92.0, 0.3), 1));
            tags.Write("COMP_AIR_PRESS", Math.Round(_ctx.AddNoise(6.5, 0.1), 2));
            tags.Write("CW_SUP_TEMP", Math.Round(_ctx.AddNoise(8.0, 0.3), 1));
            tags.Write("CW_RET_TEMP", Math.Round(_ctx.AddNoise(14.0, 0.5), 1));
            tags.Write("CW_BASIN_LVL", Math.Round(_ctx.AddNoise(85.0, 0.2), 1));
            tags.Write("CW_COND", Math.Round(_ctx.AddNoise(350.0, 5.0), 0));

            // Periodic console log
            _logCounter++;
            if (_logCounter % 100 == 0) // Every 10 seconds at 100ms tick rate
            {
                Console.WriteLine($"[{simNow:HH:mm:ss}] {_ctx.Clock.CurrentShift} shift | {tags.TagCount} tags | Idle");
            }
        }
    }
}
