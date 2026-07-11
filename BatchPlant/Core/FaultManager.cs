using BatchPlant.Api;

namespace BatchPlant.Core
{
    /// <summary>
    /// Edge-detects equipment faults across both lines and latches a FailureOccurred event into the
    /// API server's queue, exactly as DowntimeManager does for line downtime.
    ///
    /// Why latch instead of letting the UNS bridge poll each fault flag: a filler breakdown's MTTR is
    /// a few plant-minutes, which at SIM_SPEED=60 is a few WALL seconds. A 5-second poll of the fault
    /// booleans would miss short faults entirely. Detecting here -- on the simulation tick -- means no
    /// fault onset is ever missed, and the event carries the plant-time timestamp to the tick.
    ///
    /// Runs at Priority 62: after every area controller and equipment has published this tick's
    /// _FAULT tags, after DowntimeManager (60), and before the tag flusher (90).
    ///
    /// Scope: one FailureOccurred per fault ONSET (false -> true edge). Recovery is not published here
    /// (the line-level DowntimeEnded already carries the resume); the Maintenance topic the user asked
    /// for is FailureOccurred only.
    ///
    /// Registry: every _FAULT boolean that belongs to one of the two production lines. Shared utilities
    /// (Area 700 CIP, Area 800) are intentionally excluded -- they serve the whole plant, not a single
    /// line, so they have no place on a line's Maintenance topic. Today only the filler runs a failure
    /// model, so only FL-501 actually edges; the rest are registered so that enabling a failure model
    /// on any listed unit surfaces its faults with no further wiring.
    /// </summary>
    public class FaultManager : ISimulatable
    {
        private readonly PlantContext _ctx;
        private ApiServer? _apiServer;

        public int Priority => 62;

        public FaultManager(PlantContext ctx) { _ctx = ctx; }

        public void ConnectApiServer(ApiServer apiServer) => _apiServer = apiServer;

        private sealed class Machine
        {
            public string FaultTag = "";     // the boolean OPC UA tag to edge-detect
            public string MachineId = "";     // ISA-95 unit id, used as the topic's <machine_name>
            public string MachineName = "";   // human-readable equipment name
            public string Area = "";          // ISA-95 area -> UNS topic segment
            public string LineId = "";        // process cell / work centre
            public string? FaultCodeTag;      // optional companion fault-code tag
            public bool Open;                 // currently latched faulted (for edge detection)
            public DateTime StartedAt;        // onset time, so the resolution can report duration
            public string StartFaultCode = ""; // fault code seen at onset, carried into the resolution
        }

        // Batch line = ProductionArea / WC-MUC-200-L1; Pack line = PackagingArea / WC-MUC-500-L1.
        private const string ProdArea = "ProductionArea";
        private const string ProdLine = "WC-MUC-200-L1";
        private const string PackArea = "PackagingArea";
        private const string PackLine = "WC-MUC-500-L1";

        private readonly Machine[] _machines =
        {
            // --- Production line (Area 100-450) --------------------------------------
            new Machine { FaultTag = "P101_FAULT",       MachineId = "P-101",      MachineName = "Paste Tank Pump 101",  Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "P102_FAULT",       MachineId = "P-102",      MachineName = "Paste Tank Pump 102",  Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "SUGAR_FEEDER_FAULT", MachineId = "SUGAR-FEEDER", MachineName = "Sugar Feeder",     Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "SALT_FEEDER_FAULT",  MachineId = "SALT-FEEDER",  MachineName = "Salt Feeder",      Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "AGIT201_FAULT",    MachineId = "MX-201",     MachineName = "High Shear Mixer 201", Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "AGIT301_FAULT",    MachineId = "KT-301",     MachineName = "Steam Kettle 301",     Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "401-VP-001_FAULT", MachineId = "401-VP-001", MachineName = "Deaerator Vacuum Pump", Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "HOMOG_401_FAULT",  MachineId = "HG-401",     MachineName = "Homogenizer 401",      Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "401-P-001_FAULT",  MachineId = "401-P-001",  MachineName = "Transfer Pump 401",    Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "BT451_AGIT_FAULT", MachineId = "BT-451",     MachineName = "Buffer Tank 451",      Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "BT452_AGIT_FAULT", MachineId = "BT-452",     MachineName = "Buffer Tank 452",      Area = ProdArea, LineId = ProdLine },
            new Machine { FaultTag = "BT453_AGIT_FAULT", MachineId = "BT-453",     MachineName = "Buffer Tank 453",      Area = ProdArea, LineId = ProdLine },

            // --- Packaging line (Area 500-600) ---------------------------------------
            new Machine { FaultTag = "FL_501_FAULT",   MachineId = "FL-501",   MachineName = "Piston Filler 501", Area = PackArea, LineId = PackLine, FaultCodeTag = "FL_501_FAULT_CODE" },
            new Machine { FaultTag = "CONV_501_FAULT", MachineId = "CONV-501", MachineName = "Filling Conveyor 501", Area = PackArea, LineId = PackLine },
            new Machine { FaultTag = "LBL_601_FAULT",  MachineId = "LBL-601",  MachineName = "Labeler 601",       Area = PackArea, LineId = PackLine },
            new Machine { FaultTag = "CP_601_FAULT",   MachineId = "CP-601",   MachineName = "Case Packer 601",   Area = PackArea, LineId = PackLine },
            new Machine { FaultTag = "PAL_601_FAULT",  MachineId = "PAL-601",  MachineName = "Palletizer 601",    Area = PackArea, LineId = PackLine },
        };

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            foreach (var m in _machines)
            {
                bool faulted = _ctx.Tags.Read<bool>(m.FaultTag, false);

                if (faulted && !m.Open)
                {
                    m.Open = true;
                    m.StartedAt = simNow;
                    string faultCode = string.IsNullOrEmpty(m.FaultCodeTag)
                        ? "" : (_ctx.Tags.Read<string>(m.FaultCodeTag, "") ?? "");
                    m.StartFaultCode = faultCode;
                    _apiServer?.EnqueueFaultEvent(BuildFailure(m, simNow, faultCode));
                    Console.WriteLine($"[{simNow:HH:mm:ss}] [FAULT] {m.MachineId} ({m.Area}) FAILURE" +
                                      (string.IsNullOrEmpty(faultCode) ? "" : $" '{faultCode}'"));
                }
                else if (!faulted && m.Open)
                {
                    m.Open = false;
                    double durationSec = (simNow - m.StartedAt).TotalSeconds;
                    _apiServer?.EnqueueFaultEvent(BuildResolved(m, simNow, durationSec));
                    Console.WriteLine($"[{simNow:HH:mm:ss}] [FAULT] {m.MachineId} RESOLVED after " +
                                      $"{durationSec:F0}s plant time (was '{m.StartFaultCode}')");
                }
            }
        }

        private object Isa95(Machine m) => new
        {
            enterprise = "GlobalIndustries",
            site = "Munich",
            area = m.Area,
            processCell = "Line1"
        };

        private static object Source() => new
        {
            origin = "MES-LineMonitor",
            uri = "opc.tcp://localhost:26543/BatchPlantServer",
            gateway = "Ignition-Gateway-Munich-Line1",
            originSystemId = 1030
        };

        private object BuildFailure(Machine m, DateTime simNow, string faultCode) => new
        {
            eventType = "FailureOccurred",
            eventId = $"EVT-FAIL-{m.MachineId}-{simNow.Ticks}",
            eventVersion = "2.0",
            timestamp = simNow.ToString("o"),
            source = Source(),
            correlationId = $"failure-{m.MachineId}-{simNow.Ticks}",
            data = new
            {
                machineId = m.MachineId,
                machineName = m.MachineName,
                lineId = m.LineId,
                isa95Context = Isa95(m),
                state = "Faulted",
                category = "EquipmentFailure",
                faultCode,
                plannedDowntime = false,
                occurredAt = simNow.ToString("o"),
                comment = "Equipment tripped to a faulted state and stopped. The failure is reported at the moment " +
                          "the fault was detected; the machine remains down until the fault clears and the unit is " +
                          "restarted. Any resulting line stoppage is carried separately as a downtime event."
            }
        };

        // Paired with the FailureOccurred by correlationId (both key on the onset ticks), so a
        // maintenance consumer can open a work order on the failure and close it on the resolution
        // without holding state. Emitted on the clear edge; the OT bridge publishes it to
        // .../Maintenance/Events/FailureResolved/<machine> exactly as it does FailureOccurred.
        private object BuildResolved(Machine m, DateTime simNow, double durationSec) => new
        {
            eventType = "FailureResolved",
            eventId = $"EVT-FAILRES-{m.MachineId}-{m.StartedAt.Ticks}",
            eventVersion = "2.0",
            timestamp = simNow.ToString("o"),
            source = Source(),
            correlationId = $"failure-{m.MachineId}-{m.StartedAt.Ticks}",   // pairs with the FailureOccurred
            data = new
            {
                machineId = m.MachineId,
                machineName = m.MachineName,
                lineId = m.LineId,
                isa95Context = Isa95(m),
                state = "Running",
                category = "EquipmentFailure",
                faultCode = m.StartFaultCode,   // the fault that took it down
                plannedDowntime = false,
                occurredAt = m.StartedAt.ToString("o"),
                resolvedAt = simNow.ToString("o"),
                durationSeconds = Math.Round(durationSec, 1),   // plant seconds, onset to recovery
                comment = "Equipment fault cleared and the machine returned to service. Duration is measured from the " +
                          "moment the fault was detected to the moment it recovered."
            }
        };
    }
}
