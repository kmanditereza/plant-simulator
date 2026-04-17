using BatchPlant.OpcUa;
using Opc.Ua;

namespace BatchPlant.Process
{
    /// <summary>
    /// Defines all ~300 OPC UA tags for the ketchup plant.
    /// Tag naming intentionally varies by area to reflect different contractors/eras.
    /// Area 100: Legacy Foxboro (~1995) - no hyphens, uppercase, abbreviations
    /// Area 200-300: Honeywell Experion (~2005) - ISA-like with inconsistencies
    /// Area 400: 2012 upgrade - area-first numbering
    /// Area 500-600: Krones OEM (~2018) - mixed case, underscores
    /// Area 700: CIP integrated with DCS (~2005)
    /// Area 800: Schneider BMS (~2010) - underscore-separated abbreviations
    /// </summary>
    public static class TagCatalog
    {
        public static void RegisterAll(TagRegistry tags)
        {
            RegisterArea100(tags);
            RegisterArea200(tags);
            RegisterArea300(tags);
            RegisterArea400(tags);
            RegisterArea450(tags);
            RegisterArea500(tags);
            RegisterArea600(tags);
            RegisterArea700(tags);
            RegisterArea800(tags);
            RegisterBatchControl(tags);
            RegisterPlantStatus(tags);
            RegisterBatchLine(tags);
            RegisterPackLine(tags);
        }

        private static void RegisterArea100(TagRegistry tags)
        {
            // === AREA 100: Raw Materials (Legacy Foxboro ~1995) ===
            // No hyphens, uppercase, abbreviations. Status as integer words.
            var p = "Area100_RawMaterials";

            // Paste Tank 1 (20,000L tomato paste storage)
            var pt1 = $"{p}/PasteTank1";
            tags.Define("PASTE_TK1_LVL", BuiltInType.Double, pt1, "%", "Paste Tank 1 Level", 72.0);
            tags.Define("PASTE_TK1_TEMP", BuiltInType.Double, pt1, "degC", "Paste Tank 1 Temperature", 18.0);
            tags.Define("PASTE_TK1_WT", BuiltInType.Double, pt1, "kg", "Paste Tank 1 Weight", 14400.0);
            tags.Define("P101_STS", BuiltInType.Int32, pt1, description: "Pump 101 Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            tags.Define("P101_CMD", BuiltInType.Boolean, pt1, description: "Pump 101 Start Command", writable: true);
            tags.Define("P101_AMPS", BuiltInType.Double, pt1, "A", "Pump 101 Motor Current", 0.0);
            tags.Define("P101_FLOW", BuiltInType.Double, pt1, "kg/min", "Pump 101 Flow Rate", 0.0);

            // Paste Tank 2
            var pt2 = $"{p}/PasteTank2";
            tags.Define("PASTE_TK2_LVL", BuiltInType.Double, pt2, "%", "Paste Tank 2 Level", 85.0);
            tags.Define("PASTE_TK2_TEMP", BuiltInType.Double, pt2, "degC", "Paste Tank 2 Temperature", 18.0);
            tags.Define("PASTE_TK2_WT", BuiltInType.Double, pt2, "kg", "Paste Tank 2 Weight", 17000.0);
            tags.Define("P102_STS", BuiltInType.Int32, pt2, description: "Pump 102 Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            tags.Define("P102_CMD", BuiltInType.Boolean, pt2, description: "Pump 102 Start Command", writable: true);

            // Vinegar Tank (10,000L)
            var vt = $"{p}/VinegarTank";
            tags.Define("VIN_TK_LVL", BuiltInType.Double, vt, "%", "Vinegar Tank Level", 65.0);
            tags.Define("VIN_TK_TEMP", BuiltInType.Double, vt, "degC", "Vinegar Tank Temperature", 20.0);
            tags.Define("VIN_TK_FLOW", BuiltInType.Double, vt, "L/min", "Vinegar Flow Rate", 0.0);
            tags.Define("VIN_VLV_STS", BuiltInType.Boolean, vt, description: "Vinegar Valve Open", initialValue: false);

            // Sugar Silo
            var ss = $"{p}/SugarSilo";
            tags.Define("SUGAR_BIN_WT", BuiltInType.Double, ss, "kg", "Sugar Silo Weight", 8500.0);
            tags.Define("SUGAR_FEEDER_SPD", BuiltInType.Double, ss, "kg/hr", "Sugar Feeder Speed", 0.0);
            tags.Define("SUGAR_FEEDER_RUN", BuiltInType.Boolean, ss, description: "Sugar Feeder Running");

            // Salt Bin
            var sb = $"{p}/SaltBin";
            tags.Define("SALT_BIN_WT", BuiltInType.Double, sb, "kg", "Salt Bin Weight", 2200.0);
            tags.Define("SALT_FEEDER_SPD", BuiltInType.Double, sb, "kg/hr", "Salt Feeder Speed", 0.0);
            tags.Define("SALT_FEEDER_RUN", BuiltInType.Boolean, sb, description: "Salt Feeder Running");

            // Spice Hopper
            var sh = $"{p}/SpiceHopper";
            tags.Define("SPICE_BIN_WT", BuiltInType.Double, sh, "kg", "Spice Hopper Weight", 450.0);
            tags.Define("SPICE_FEEDER_RUN", BuiltInType.Boolean, sh, description: "Spice Feeder Running");

            // Water Supply
            var ws = $"{p}/WaterSupply";
            tags.Define("H2O_FLOW", BuiltInType.Double, ws, "L/min", "Water Flow Rate", 0.0);
            tags.Define("H2O_PRESS", BuiltInType.Double, ws, "bar", "Water Supply Pressure", 4.2);
            tags.Define("H2O_TEMP", BuiltInType.Double, ws, "degC", "Water Temperature", 15.0);
            tags.Define("H2O_VLV_STS", BuiltInType.Boolean, ws, description: "Water Valve Open");
            tags.Define("H2O_TOTALIZER", BuiltInType.Double, ws, "L", "Water Totalizer", 0.0);
        }

        private static void RegisterArea200(TagRegistry tags)
        {
            // === AREA 200: Pre-Mixing (Honeywell Experion ~2005) ===
            // ISA-like with hyphens, but inconsistencies (some missing hyphens on motor tags)
            var p = "Area200_PreMix";

            // Mixer MX-201 (3,500L high-shear Breddo Likwifier)
            var mx = $"{p}/Mixer_MX201";
            tags.Define("TIT-201", BuiltInType.Double, mx, "degC", "Premix Tank Temperature", 22.0);
            tags.Define("LIT-201", BuiltInType.Double, mx, "L", "Premix Tank Level", 0.0);
            tags.Define("LIT-201_PCT", BuiltInType.Double, mx, "%", "Premix Tank Level Percent", 0.0);
            tags.Define("FIT-201", BuiltInType.Double, mx, "L/min", "Premix Inlet Flow", 0.0);
            tags.Define("WIT-201", BuiltInType.Double, mx, "kg", "Premix Tank Weight", 0.0);

            // Agitator (no hyphen - inconsistency from motor OEM)
            tags.Define("AGIT201_SPD", BuiltInType.Double, mx, "RPM", "Agitator Speed", 0.0);
            tags.Define("AGIT201_AMPS", BuiltInType.Double, mx, "A", "Agitator Motor Current", 0.0);
            tags.Define("AGIT201_RUN", BuiltInType.Boolean, mx, description: "Agitator Running");
            tags.Define("AGIT201_FAULT", BuiltInType.Boolean, mx, description: "Agitator Fault");
            tags.Define("AGIT201_VIB", BuiltInType.Double, mx, "mm/s", "Agitator Vibration", 0.0);
            tags.Define("AGIT201_TRQ", BuiltInType.Double, mx, "Nm", "Agitator Torque", 0.0);
            tags.Define("AGIT201_PWR", BuiltInType.Double, mx, "kW", "Agitator Power", 0.0);

            // Valves
            var vlv = $"{p}/Valves";
            tags.Define("XV-201", BuiltInType.Boolean, vlv, description: "Water Inlet Valve Open");
            tags.Define("XV-202", BuiltInType.Boolean, vlv, description: "Paste Inlet Valve Open");
            tags.Define("XV-203", BuiltInType.Boolean, vlv, description: "Vinegar Inlet Valve Open");
            tags.Define("XV-204", BuiltInType.Boolean, vlv, description: "Discharge Valve Open");
            tags.Define("FCV-201", BuiltInType.Double, vlv, "%", "Flow Control Valve Position", 0.0);

            // PID
            var pid = $"{p}/PID";
            tags.Define("PID_201_SP", BuiltInType.Double, pid, "degC", "Premix PID Setpoint", 0.0);
            tags.Define("PID_201_PV", BuiltInType.Double, pid, "degC", "Premix PID Process Variable", 0.0);
            tags.Define("PID_201_OP", BuiltInType.Double, pid, "%", "Premix PID Output", 0.0);

            // Mixer state
            tags.Define("MX201_STATE", BuiltInType.Int32, mx, description: "Mixer State (0=Idle,1=Dosing,2=Mixing,3=Discharging,4=Fault)", initialValue: 0);
            tags.Define("MX201_MODE", BuiltInType.Int32, mx, description: "Mixer Mode (0=Manual,1=Auto)", initialValue: 1);
            tags.Define("MX201_BATCH_ID", BuiltInType.String, mx, description: "Batch ID currently in MX-201", initialValue: "");
        }

        private static void RegisterArea300(TagRegistry tags)
        {
            // === AREA 300: Cooking (Same DCS as 200, ~2005) ===
            // ISA-like but some tags lose hyphens (human inconsistency)
            var p = "Area300_Cooking";

            // Kettle KT-301 (3,000L jacketed steam kettle)
            var kt = $"{p}/Kettle_KT301";
            tags.Define("TIT-301", BuiltInType.Double, kt, "degC", "Kettle Product Temperature", 22.0);
            tags.Define("TIT-302", BuiltInType.Double, kt, "degC", "Kettle Bottom Temperature", 22.0);
            tags.Define("PIT-301", BuiltInType.Double, kt, "bar", "Kettle Jacket Pressure", 0.0);
            tags.Define("LIT-301", BuiltInType.Double, kt, "L", "Kettle Level", 0.0);
            tags.Define("LIT-301_PCT", BuiltInType.Double, kt, "%", "Kettle Level Percent", 0.0);
            // Note: PT-301_JKT uses non-standard suffix (common in real plants)
            tags.Define("PT-301_JKT", BuiltInType.Double, kt, "bar", "Jacket Steam Pressure", 0.0);

            // Inline pH (one of few analytical instruments on DCS)
            tags.Define("AIT-301", BuiltInType.Double, kt, "pH", "Kettle Inline pH", 0.0);

            // Agitator (scraper type, low speed)
            tags.Define("AGIT301_SPD", BuiltInType.Double, kt, "RPM", "Kettle Agitator Speed", 0.0);
            tags.Define("AGIT301_RUN", BuiltInType.Boolean, kt, description: "Kettle Agitator Running");
            tags.Define("AGIT301_AMPS", BuiltInType.Double, kt, "A", "Kettle Agitator Current", 0.0);

            // Steam control
            var stm = $"{p}/SteamControl";
            // TCV-301 is correct ISA, but SV301 lost its hyphen (different tech wrote the code)
            tags.Define("TCV-301", BuiltInType.Double, stm, "%", "Steam Temperature Control Valve", 0.0);
            tags.Define("SV301", BuiltInType.Boolean, stm, description: "Steam Isolation Valve Open");
            tags.Define("FIT-302", BuiltInType.Double, stm, "kg/h", "Steam Flow Rate", 0.0);

            // PID
            var pid = $"{p}/PID";
            tags.Define("PID_301_SP", BuiltInType.Double, pid, "degC", "Cook PID Setpoint", 90.0);
            tags.Define("PID_301_PV", BuiltInType.Double, pid, "degC", "Cook PID Process Variable", 22.0);
            tags.Define("PID_301_OP", BuiltInType.Double, pid, "%", "Cook PID Output", 0.0);
            tags.Define("PID_301_MODE", BuiltInType.Int32, pid, description: "Cook PID Mode (0=Manual,1=Auto)", initialValue: 1);

            // Kettle state
            tags.Define("KT301_STATE", BuiltInType.Int32, kt, description: "Kettle State (0=Idle,1=Filling,2=Heating,3=HoldingTemp,4=Discharging,5=Fault)", initialValue: 0);
            tags.Define("KT301_HOLD_TMR", BuiltInType.Double, kt, "sec", "Pasteurization Hold Timer", 0.0);
            tags.Define("KT301_BATCH_ID", BuiltInType.String, kt, description: "Batch ID currently in KT-301", initialValue: "");
        }

        private static void RegisterArea400(TagRegistry tags)
        {
            // === AREA 400: Deaeration & Homogenization (2012 upgrade) ===
            // Area-first numbering: 401-XX-NNN (different contractor convention)
            var p = "Area400_Process";

            // Deaerator DA-401
            var da = $"{p}/Deaerator_DA401";
            tags.Define("401-TI-001", BuiltInType.Double, da, "degC", "Deaerator Temperature", 22.0);
            tags.Define("401-PI-001", BuiltInType.Double, da, "mmHg", "Deaerator Vacuum Level", 0.0);
            tags.Define("401-FI-001", BuiltInType.Double, da, "L/min", "Deaerator Product Flow", 0.0);
            tags.Define("401-LI-001", BuiltInType.Double, da, "%", "Deaerator Level", 0.0);
            tags.Define("401-VP-001_RUN", BuiltInType.Boolean, da, description: "Vacuum Pump Running");
            tags.Define("401-VP-001_FAULT", BuiltInType.Boolean, da, description: "Vacuum Pump Fault");
            tags.Define("401-VP-001_AMPS", BuiltInType.Double, da, "A", "Vacuum Pump Current", 0.0);

            // Homogenizer HG-401 (note: OEM tags mixed in with plant numbering)
            var hg = $"{p}/Homogenizer_HG401";
            tags.Define("HOMOG_401_PRESS", BuiltInType.Double, hg, "bar", "Homogenizer Pressure", 0.0);
            tags.Define("HOMOG_401_PRESS2", BuiltInType.Double, hg, "bar", "Homogenizer 2nd Stage Pressure", 0.0);
            tags.Define("HOMOG_401_AMPS", BuiltInType.Double, hg, "A", "Homogenizer Motor Current", 0.0);
            tags.Define("HOMOG_401_RUN", BuiltInType.Boolean, hg, description: "Homogenizer Running");
            tags.Define("HOMOG_401_FAULT", BuiltInType.Boolean, hg, description: "Homogenizer Fault");
            tags.Define("HOMOG_401_VLV_HRS", BuiltInType.Double, hg, "hrs", "Homogenizer Valve Run Hours", 0.0);
            // PackML state (GEA-class equipment from 2012 upgrade exposes this)
            tags.Define("HOMOG_401_OP_STATE", BuiltInType.Int32, hg, description: "PackML Operational State (0=Disabled,1=Idle,2=Running,3=Starved,4=Blocked,5=PlannedDT,6=UnplannedDT,7=Other)", initialValue: 1);
            tags.Define("HOMOG_401_OP_STATE_TXT", BuiltInType.String, hg, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("HOMOG_401_STATE_DUR", BuiltInType.Double, hg, "sec", "Time in Current State", 0.0);
            // PackML operational mode
            tags.Define("HOMOG_401_OP_MODE", BuiltInType.Int32, hg, description: "PackML Operational Mode (0=Disabled,1=Idle,2=Production,3=Changeover,4=Maintenance,5=Cleaning,6=Other)", initialValue: 1);
            tags.Define("HOMOG_401_OP_MODE_TXT", BuiltInType.String, hg, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("HOMOG_401_MODE_DUR", BuiltInType.Double, hg, "sec", "Time in Current Mode", 0.0);

            // Transfer pump
            tags.Define("401-P-001_STS", BuiltInType.Int32, da, description: "Transfer Pump Status", initialValue: 0);
            tags.Define("401-P-001_FLOW", BuiltInType.Double, da, "L/min", "Transfer Pump Flow", 0.0);
            tags.Define("DA401_STATE", BuiltInType.Int32, da, description: "Deaerator State (0=Idle,1=Filling,2=PullingVacuum,3=Deaerating,4=Homogenizing,5=VentingVacuum,6=Draining)", initialValue: 0);

            // Batch ID tags for genealogy (published by BatchManager)
            tags.Define("DA401_BATCH_ID", BuiltInType.String, da, description: "Batch ID currently in DA-401", initialValue: "");
        }

        private static void RegisterArea450(TagRegistry tags)
        {
            // === AREA 450: Buffer/Holding Tanks ===
            // Mixed convention - some tags from 2005 DCS, some from 2012 addition
            var p = "Area450_Buffer";

            // Buffer Tank 1 (5,000L insulated)
            var bt1 = $"{p}/BT451";
            tags.Define("BT451_LVL", BuiltInType.Double, bt1, "%", "Buffer Tank 1 Level", 0.0);
            tags.Define("BT451_TEMP", BuiltInType.Double, bt1, "degC", "Buffer Tank 1 Temperature", 22.0);
            tags.Define("BT451_AGIT_RUN", BuiltInType.Boolean, bt1, description: "BT451 Agitator Running");
            tags.Define("BT451_VLV_IN", BuiltInType.Boolean, bt1, description: "BT451 Inlet Valve");
            tags.Define("BT451_VLV_OUT", BuiltInType.Boolean, bt1, description: "BT451 Outlet Valve");
            tags.Define("BT451_LVL_L", BuiltInType.Double, bt1, "L", "Buffer Tank 1 Level Liters", 0.0);

            // Buffer Tank 2
            var bt2 = $"{p}/BT452";
            tags.Define("BT452_LVL", BuiltInType.Double, bt2, "%", "Buffer Tank 2 Level", 0.0);
            tags.Define("BT452_TEMP", BuiltInType.Double, bt2, "degC", "Buffer Tank 2 Temperature", 22.0);
            tags.Define("BT452_AGIT_RUN", BuiltInType.Boolean, bt2, description: "BT452 Agitator Running");
            tags.Define("BT452_VLV_IN", BuiltInType.Boolean, bt2, description: "BT452 Inlet Valve");
            tags.Define("BT452_VLV_OUT", BuiltInType.Boolean, bt2, description: "BT452 Outlet Valve");
            tags.Define("BT452_LVL_L", BuiltInType.Double, bt2, "L", "Buffer Tank 2 Level Liters", 0.0);

            // Buffer Tank 3
            var bt3 = $"{p}/BT453";
            tags.Define("BT453_LVL", BuiltInType.Double, bt3, "%", "Buffer Tank 3 Level", 0.0);
            tags.Define("BT453_TEMP", BuiltInType.Double, bt3, "degC", "Buffer Tank 3 Temperature", 22.0);
            tags.Define("BT453_AGIT_RUN", BuiltInType.Boolean, bt3, description: "BT453 Agitator Running");
            tags.Define("BT453_VLV_IN", BuiltInType.Boolean, bt3, description: "BT453 Inlet Valve");
            tags.Define("BT453_VLV_OUT", BuiltInType.Boolean, bt3, description: "BT453 Outlet Valve");
            tags.Define("BT453_LVL_L", BuiltInType.Double, bt3, "L", "Buffer Tank 3 Level Liters", 0.0);
        }

        private static void RegisterArea500(TagRegistry tags)
        {
            // === AREA 500: Filling Line (Krones OEM ~2018) ===
            // OEM-style: underscores, mixed case, some PascalCase leaks
            var p = "Area500_Filling";

            // Filler FL-501 (12-head rotary piston)
            var fl = $"{p}/Filler_FL501";
            tags.Define("FL_501_WEIGHT", BuiltInType.Double, fl, "g", "Current Fill Weight", 0.0);
            tags.Define("FL_501_FILL_SPD", BuiltInType.Double, fl, "BPM", "Filler Speed (Bottles/Min)", 0.0);
            tags.Define("FL_501_STATUS", BuiltInType.Int32, fl, description: "Filler Status (0=Off,1=Run,2=Fault,3=TempLow)", initialValue: 0);
            tags.Define("FL_501_GOOD_CNT", BuiltInType.Int32, fl, description: "Good Bottle Count", initialValue: 0);
            tags.Define("FL_501_REJECT_CNT", BuiltInType.Int32, fl, description: "Reject Count", initialValue: 0);
            tags.Define("FL_501_TOTAL_CNT", BuiltInType.Int32, fl, description: "Total Bottle Count", initialValue: 0);
            tags.Define("FL_501_WT_AVG", BuiltInType.Double, fl, "g", "Fill Weight Average", 0.0);
            tags.Define("FL_501_WT_STDDEV", BuiltInType.Double, fl, "g", "Fill Weight Std Dev", 0.0);
            tags.Define("FL_501_YIELD", BuiltInType.Double, fl, "%", "First Pass Yield", 100.0);
            // PascalCase leak from Krones HMI configuration
            tags.Define("FILLER_HeadPress_01", BuiltInType.Double, fl, "bar", "Filler Head Pressure", 0.0);
            tags.Define("FL_501_PROD_TEMP", BuiltInType.Double, fl, "degC", "Product Temperature at Filler", 22.0);
            // PackML state (Krones standard)
            tags.Define("FL_501_OP_STATE", BuiltInType.Int32, fl, description: "PackML Operational State (0=Disabled,1=Idle,2=Running,3=Starved,4=Blocked,5=PlannedDT,6=UnplannedDT,7=Other)", initialValue: 1);
            tags.Define("FL_501_OP_STATE_TXT", BuiltInType.String, fl, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("FL_501_STATE_DUR", BuiltInType.Double, fl, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("FL_501_OP_MODE", BuiltInType.Int32, fl, description: "PackML Operational Mode (0=Disabled,1=Idle,2=Production,3=Changeover,4=Maintenance,5=Cleaning,6=Other)", initialValue: 1);
            tags.Define("FL_501_OP_MODE_TXT", BuiltInType.String, fl, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("FL_501_MODE_DUR", BuiltInType.Double, fl, "sec", "Time in Current Mode", 0.0);

            // Capper CAP-501
            var cap = $"{p}/Capper_CAP501";
            tags.Define("CAP_501_STS", BuiltInType.Int32, cap, description: "Capper Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            tags.Define("CAP_501_JAM", BuiltInType.Boolean, cap, description: "Cap Feeder Jam");
            tags.Define("CAP_501_TORQUE", BuiltInType.Double, cap, "in-lb", "Cap Torque", 0.0);
            tags.Define("CAP_501_CNT", BuiltInType.Int32, cap, description: "Caps Applied Count", initialValue: 0);
            // PackML state
            tags.Define("CAP_501_OP_STATE", BuiltInType.Int32, cap, description: "PackML Operational State", initialValue: 1);
            tags.Define("CAP_501_OP_STATE_TXT", BuiltInType.String, cap, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("CAP_501_STATE_DUR", BuiltInType.Double, cap, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("CAP_501_OP_MODE", BuiltInType.Int32, cap, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("CAP_501_OP_MODE_TXT", BuiltInType.String, cap, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("CAP_501_MODE_DUR", BuiltInType.Double, cap, "sec", "Time in Current Mode", 0.0);

            // Inversion Tunnel
            var inv = $"{p}/InversionTunnel";
            tags.Define("INV_TUNNEL_TEMP_1", BuiltInType.Double, inv, "degC", "Inversion Tunnel Temp Zone 1", 22.0);
            tags.Define("INV_TUNNEL_TEMP_2", BuiltInType.Double, inv, "degC", "Inversion Tunnel Temp Zone 2", 22.0);
            tags.Define("INV_TUNNEL_SPD", BuiltInType.Double, inv, "m/min", "Inversion Tunnel Speed", 0.0);
            tags.Define("INV_TUNNEL_RUN", BuiltInType.Boolean, inv, description: "Inversion Tunnel Running");
            // PackML state
            tags.Define("INV_TUNNEL_OP_STATE", BuiltInType.Int32, inv, description: "PackML Operational State", initialValue: 1);
            tags.Define("INV_TUNNEL_OP_STATE_TXT", BuiltInType.String, inv, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("INV_TUNNEL_STATE_DUR", BuiltInType.Double, inv, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("INV_TUNNEL_OP_MODE", BuiltInType.Int32, inv, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("INV_TUNNEL_OP_MODE_TXT", BuiltInType.String, inv, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("INV_TUNNEL_MODE_DUR", BuiltInType.Double, inv, "sec", "Time in Current Mode", 0.0);

            // Cooling Tunnel
            var cool = $"{p}/CoolingTunnel";
            tags.Define("COOL_TUN_TEMP_IN", BuiltInType.Double, cool, "degC", "Cooling Tunnel Inlet Temp", 22.0);
            tags.Define("COOL_TUN_TEMP_MID", BuiltInType.Double, cool, "degC", "Cooling Tunnel Mid Temp", 22.0);
            tags.Define("COOL_TUN_TEMP_OUT", BuiltInType.Double, cool, "degC", "Cooling Tunnel Outlet Temp", 22.0);
            tags.Define("COOL_TUN_SPRAY_FLOW", BuiltInType.Double, cool, "L/min", "Cooling Spray Flow", 0.0);
            tags.Define("COOL_TUN_SPRAY_TEMP", BuiltInType.Double, cool, "degC", "Cooling Spray Water Temp", 8.0);
            tags.Define("COOL_TUN_RUN", BuiltInType.Boolean, cool, description: "Cooling Tunnel Running");
            // PackML state
            tags.Define("COOL_TUN_OP_STATE", BuiltInType.Int32, cool, description: "PackML Operational State", initialValue: 1);
            tags.Define("COOL_TUN_OP_STATE_TXT", BuiltInType.String, cool, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("COOL_TUN_STATE_DUR", BuiltInType.Double, cool, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("COOL_TUN_OP_MODE", BuiltInType.Int32, cool, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("COOL_TUN_OP_MODE_TXT", BuiltInType.String, cool, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("COOL_TUN_MODE_DUR", BuiltInType.Double, cool, "sec", "Time in Current Mode", 0.0);

            // Conveyor
            var conv = $"{p}/Conveyor";
            tags.Define("CONV_501_SPD", BuiltInType.Double, conv, "BPM", "Conveyor Speed", 0.0);
            tags.Define("CONV_501_RUN", BuiltInType.Boolean, conv, description: "Conveyor Running");
            tags.Define("CONV_501_FAULT", BuiltInType.Boolean, conv, description: "Conveyor Fault/Jam");
            // PackML state
            tags.Define("CONV_501_OP_STATE", BuiltInType.Int32, conv, description: "PackML Operational State", initialValue: 1);
            tags.Define("CONV_501_OP_STATE_TXT", BuiltInType.String, conv, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("CONV_501_STATE_DUR", BuiltInType.Double, conv, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("CONV_501_OP_MODE", BuiltInType.Int32, conv, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("CONV_501_OP_MODE_TXT", BuiltInType.String, conv, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("CONV_501_MODE_DUR", BuiltInType.Double, conv, "sec", "Time in Current Mode", 0.0);
        }

        private static void RegisterArea600(TagRegistry tags)
        {
            // === AREA 600: Packaging Line (Same Krones OEM ~2018) ===
            var p = "Area600_Packaging";

            // Labeler
            var lbl = $"{p}/Labeler_LBL601";
            tags.Define("LBL_601_STATUS", BuiltInType.Int32, lbl, description: "Labeler Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            tags.Define("LBL_601_SPEED", BuiltInType.Double, lbl, "BPM", "Labeler Speed", 0.0);
            tags.Define("LBL_601_REJECT", BuiltInType.Int32, lbl, description: "Labeler Reject Count", initialValue: 0);
            tags.Define("LBL_601_FAULT", BuiltInType.Boolean, lbl, description: "Labeler Fault");
            // PackML state
            tags.Define("LBL_601_OP_STATE", BuiltInType.Int32, lbl, description: "PackML Operational State", initialValue: 1);
            tags.Define("LBL_601_OP_STATE_TXT", BuiltInType.String, lbl, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("LBL_601_STATE_DUR", BuiltInType.Double, lbl, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("LBL_601_OP_MODE", BuiltInType.Int32, lbl, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("LBL_601_OP_MODE_TXT", BuiltInType.String, lbl, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("LBL_601_MODE_DUR", BuiltInType.Double, lbl, "sec", "Time in Current Mode", 0.0);

            // Date Coder (inkjet)
            var dc = $"{p}/DateCoder_DC601";
            tags.Define("DC_601_STATUS", BuiltInType.Int32, dc, description: "Date Coder Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            tags.Define("DC_601_PRINT_CNT", BuiltInType.Int32, dc, description: "Print Count", initialValue: 0);
            // PackML state
            tags.Define("DC_601_OP_STATE", BuiltInType.Int32, dc, description: "PackML Operational State", initialValue: 1);
            tags.Define("DC_601_OP_STATE_TXT", BuiltInType.String, dc, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("DC_601_STATE_DUR", BuiltInType.Double, dc, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("DC_601_OP_MODE", BuiltInType.Int32, dc, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("DC_601_OP_MODE_TXT", BuiltInType.String, dc, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("DC_601_MODE_DUR", BuiltInType.Double, dc, "sec", "Time in Current Mode", 0.0);

            // X-Ray Inspector
            var xr = $"{p}/Inspector_XR601";
            tags.Define("XRAY_601_TOTAL", BuiltInType.Int32, xr, description: "X-Ray Total Inspected", initialValue: 0);
            tags.Define("XRAY_601_REJECT", BuiltInType.Int32, xr, description: "X-Ray Reject Count", initialValue: 0);
            tags.Define("XRAY_601_STATUS", BuiltInType.Int32, xr, description: "X-Ray Status", initialValue: 0);
            // PackML state
            tags.Define("XRAY_601_OP_STATE", BuiltInType.Int32, xr, description: "PackML Operational State", initialValue: 1);
            tags.Define("XRAY_601_OP_STATE_TXT", BuiltInType.String, xr, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("XRAY_601_STATE_DUR", BuiltInType.Double, xr, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("XRAY_601_OP_MODE", BuiltInType.Int32, xr, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("XRAY_601_OP_MODE_TXT", BuiltInType.String, xr, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("XRAY_601_MODE_DUR", BuiltInType.Double, xr, "sec", "Time in Current Mode", 0.0);

            // Metal Detector (typically integrated with X-ray inspector station)
            tags.Define("MD_601_TRIP", BuiltInType.Boolean, xr, description: "Metal Detector Trip");
            tags.Define("MD_601_CNT", BuiltInType.Int32, xr, description: "Metal Detector Trip Count", initialValue: 0);

            // Case Packer
            var cp = $"{p}/CasePacker_CP601";
            tags.Define("CP_601_STATUS", BuiltInType.Int32, cp, description: "Case Packer Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            tags.Define("CP_601_CASES_CNT", BuiltInType.Int32, cp, description: "Cases Packed Count", initialValue: 0);
            tags.Define("CP_601_SPEED", BuiltInType.Double, cp, "cases/min", "Case Packer Speed", 0.0);
            tags.Define("CP_601_FAULT", BuiltInType.Boolean, cp, description: "Case Packer Fault");
            // PackML state
            tags.Define("CP_601_OP_STATE", BuiltInType.Int32, cp, description: "PackML Operational State", initialValue: 1);
            tags.Define("CP_601_OP_STATE_TXT", BuiltInType.String, cp, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("CP_601_STATE_DUR", BuiltInType.Double, cp, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("CP_601_OP_MODE", BuiltInType.Int32, cp, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("CP_601_OP_MODE_TXT", BuiltInType.String, cp, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("CP_601_MODE_DUR", BuiltInType.Double, cp, "sec", "Time in Current Mode", 0.0);

            // Palletizer
            var pal = $"{p}/Palletizer_PAL601";
            tags.Define("PAL_601_STATUS", BuiltInType.Int32, pal, description: "Palletizer Status (0=Off,1=Run,2=Fault)", initialValue: 0);
            tags.Define("PAL_601_LAYERS", BuiltInType.Int32, pal, description: "Current Pallet Layers", initialValue: 0);
            tags.Define("PAL_601_PALLETS_CNT", BuiltInType.Int32, pal, description: "Pallets Complete Count", initialValue: 0);
            tags.Define("PAL_601_FAULT", BuiltInType.Boolean, pal, description: "Palletizer Fault");
            // PackML state
            tags.Define("PAL_601_OP_STATE", BuiltInType.Int32, pal, description: "PackML Operational State", initialValue: 1);
            tags.Define("PAL_601_OP_STATE_TXT", BuiltInType.String, pal, description: "PackML Operational State Text", initialValue: "Idle");
            tags.Define("PAL_601_STATE_DUR", BuiltInType.Double, pal, "sec", "Time in Current State", 0.0);
            // PackML mode
            tags.Define("PAL_601_OP_MODE", BuiltInType.Int32, pal, description: "PackML Operational Mode", initialValue: 1);
            tags.Define("PAL_601_OP_MODE_TXT", BuiltInType.String, pal, description: "PackML Operational Mode Text", initialValue: "Idle");
            tags.Define("PAL_601_MODE_DUR", BuiltInType.Double, pal, "sec", "Time in Current Mode", 0.0);
        }

        private static void RegisterArea700(TagRegistry tags)
        {
            // === AREA 700: CIP System (Integrated with DCS ~2005) ===
            var p = "Area700_CIP";

            var c1 = $"{p}/Circuit1";
            tags.Define("CIP_TIT_701", BuiltInType.Double, c1, "degC", "CIP Solution Temperature", 22.0);
            tags.Define("CIP_FIT_701", BuiltInType.Double, c1, "L/min", "CIP Flow Rate", 0.0);
            tags.Define("CIP_CIT_701", BuiltInType.Double, c1, "uS/cm", "CIP Conductivity", 0.0);
            tags.Define("CIP_PIT_701", BuiltInType.Double, c1, "bar", "CIP Supply Pressure", 0.0);
            tags.Define("CIP_V701_POS", BuiltInType.Double, c1, "%", "CIP Supply Valve Position", 0.0);
            tags.Define("CIP_V702_POS", BuiltInType.Double, c1, "%", "CIP Return Valve Position", 0.0);
            tags.Define("CIP_P701_RUN", BuiltInType.Boolean, c1, description: "CIP Supply Pump Running");
            tags.Define("CIP_P701_AMPS", BuiltInType.Double, c1, "A", "CIP Pump Current", 0.0);
            tags.Define("CIP_STEP", BuiltInType.Int32, c1, description: "CIP Step (0=Idle,1=PreRinse,2=Caustic,3=IntRinse,4=Acid,5=FinalRinse,6=Sanitize,7=Drain)", initialValue: 0);
            tags.Define("CIP_TIMER", BuiltInType.Double, c1, "sec", "CIP Step Timer", 0.0);
            tags.Define("CIP_PHASE", BuiltInType.String, c1, description: "CIP Phase Description", initialValue: "Idle");
            tags.Define("CIP_ACTIVE", BuiltInType.Boolean, c1, description: "CIP Cycle Active");
        }

        private static void RegisterArea800(TagRegistry tags)
        {
            // === AREA 800: Utilities (Schneider BMS ~2010) ===
            // Underscore-separated abbreviations
            var p = "Area800_Utilities";

            // Steam
            var stm = $"{p}/Steam";
            tags.Define("STM_HDR_PRESS", BuiltInType.Double, stm, "bar", "Steam Header Pressure", 6.0);
            tags.Define("STM_HDR_TEMP", BuiltInType.Double, stm, "degC", "Steam Header Temperature", 165.0);
            tags.Define("STM_FLOW", BuiltInType.Double, stm, "kg/h", "Total Steam Flow", 0.0);
            tags.Define("STM_COND_TEMP", BuiltInType.Double, stm, "degC", "Condensate Return Temperature", 80.0);
            tags.Define("BOILER_STS", BuiltInType.Int32, stm, description: "Boiler Status (0=Off,1=On,2=Fault)", initialValue: 1);
            tags.Define("BOILER_PRESS", BuiltInType.Double, stm, "bar", "Boiler Pressure", 7.0);
            tags.Define("BOILER_FWATER_TEMP", BuiltInType.Double, stm, "degC", "Boiler Feedwater Temperature", 60.0);
            tags.Define("BOILER_EFF", BuiltInType.Double, stm, "%", "Boiler Efficiency", 92.0);
            tags.Define("STM_DEMAND", BuiltInType.Double, stm, "%", "Steam Demand", 0.0);
            tags.Define("STM_ENERGY", BuiltInType.Double, stm, "kWh", "Steam Energy Consumed", 0.0);

            // Cooling Water
            var cw = $"{p}/CoolingWater";
            tags.Define("CW_SUP_TEMP", BuiltInType.Double, cw, "degC", "Cooling Water Supply Temperature", 8.0);
            tags.Define("CW_RET_TEMP", BuiltInType.Double, cw, "degC", "Cooling Water Return Temperature", 14.0);
            tags.Define("CW_FLOW", BuiltInType.Double, cw, "m3/h", "Cooling Water Flow", 0.0);
            tags.Define("CW_PUMP1_STS", BuiltInType.Int32, cw, description: "Cooling Pump 1 Status", initialValue: 1);
            tags.Define("CW_PUMP2_STS", BuiltInType.Int32, cw, description: "Cooling Pump 2 Status (Standby)", initialValue: 0);
            tags.Define("CW_TOWER_FAN1", BuiltInType.Boolean, cw, description: "Cooling Tower Fan 1 Running", initialValue: true);
            tags.Define("CW_TOWER_FAN2", BuiltInType.Boolean, cw, description: "Cooling Tower Fan 2 Running");
            tags.Define("CW_BASIN_LVL", BuiltInType.Double, cw, "%", "Cooling Tower Basin Level", 85.0);
            tags.Define("CW_COND", BuiltInType.Double, cw, "uS/cm", "Cooling Water Conductivity", 350.0);
            tags.Define("CW_DEMAND", BuiltInType.Double, cw, "%", "Cooling Demand", 0.0);

            // Compressed Air
            var air = $"{p}/CompressedAir";
            tags.Define("COMP_AIR_PRESS", BuiltInType.Double, air, "bar", "Compressed Air Header Pressure", 6.5);
            tags.Define("COMP_AIR_FLOW", BuiltInType.Double, air, "Nm3/h", "Compressed Air Flow", 0.0);
            tags.Define("COMP_AIR_DEW", BuiltInType.Double, air, "degC", "Air Dewpoint", -40.0);
            tags.Define("COMP_RUN", BuiltInType.Boolean, air, description: "Compressor Running", initialValue: true);

            // Hot Water (for CIP)
            var hw = $"{p}/HotWater";
            tags.Define("HW_SUP_TEMP", BuiltInType.Double, hw, "degC", "Hot Water Supply Temperature", 82.0);
            tags.Define("HW_RET_TEMP", BuiltInType.Double, hw, "degC", "Hot Water Return Temperature", 65.0);
            tags.Define("HW_FLOW", BuiltInType.Double, hw, "L/min", "Hot Water Flow", 0.0);
            tags.Define("HW_PUMP_STS", BuiltInType.Int32, hw, description: "Hot Water Pump Status", initialValue: 1);
        }

        private static void RegisterBatchControl(TagRegistry tags)
        {
            var p = "BatchControl";

            tags.Define("BATCH_START", BuiltInType.Boolean, p, description: "Batch Start Command", writable: true);
            tags.Define("BATCH_HOLD", BuiltInType.Boolean, p, description: "Batch Hold Command", writable: true);
            tags.Define("BATCH_ABORT", BuiltInType.Boolean, p, description: "Batch Abort Command", writable: true);
            tags.Define("BATCH_ID", BuiltInType.String, p, description: "Current Batch ID", initialValue: "");
            tags.Define("BATCH_STATUS", BuiltInType.String, p, description: "Batch Status", initialValue: "IDLE");
            tags.Define("BATCH_PHASE", BuiltInType.String, p, description: "Current Batch Phase", initialValue: "");
            tags.Define("BATCH_PHASE_TMR", BuiltInType.Double, p, "sec", "Batch Phase Timer", 0.0);
            tags.Define("BATCH_PCT", BuiltInType.Double, p, "%", "Batch Progress Percent", 0.0);
            tags.Define("BATCH_START_TIME", BuiltInType.DateTime, p, description: "Batch Start Time");
            tags.Define("BATCH_END_TIME", BuiltInType.DateTime, p, description: "Batch End Time");
            tags.Define("BATCH_SIZE_KG", BuiltInType.Double, p, "kg", "Batch Size", 3150.0);
        }

        private static void RegisterPlantStatus(TagRegistry tags)
        {
            // Truly plant-wide tags only. Per-line metrics live in BatchLine/ and PackLine/.
            var p = "PlantStatus";

            tags.Define("PLANT_MODE", BuiltInType.String, p, description: "Plant Mode (Production/CIP/Maintenance/Idle)", initialValue: "Idle");
            tags.Define("PLANT_RUNNING", BuiltInType.Boolean, p, description: "Plant Running (any line producing)");
            tags.Define("SHIFT_ID", BuiltInType.String, p, description: "Current Shift (Day/Swing/Night)", initialValue: "Day");
            tags.Define("SHIFT_CHANGE_ACTIVE", BuiltInType.Boolean, p, description: "Shift Changeover Active");
            tags.Define("PLANT_TOTAL_PRODUCTION_TODAY", BuiltInType.Int32, p, "cases", "Total Cases Produced Today (all lines)", 0);
            tags.Define("SIM_SPEED", BuiltInType.Double, p, "x", "Simulation Speed Multiplier", 1.0);
            tags.Define("SIM_TIME", BuiltInType.DateTime, p, description: "Current Simulation Time");
        }

        private static void RegisterBatchLine(TagRegistry tags)
        {
            // Batch production line metrics (Areas 100-450 - dose, mix, cook, deaerate, homogenize, buffer)
            // KPIs are batch-oriented: cycle time, batches per shift, recipe adherence
            var p = "BatchLine";

            tags.Define("BATCH_LINE_STATE", BuiltInType.String, p, description: "Batch Line State (Idle/Producing/CIP/Maintenance/Faulted)", initialValue: "Idle");
            tags.Define("BATCH_LINE_RUNNING", BuiltInType.Boolean, p, description: "Batch Line Producing");

            tags.Define("BATCHES_TODAY", BuiltInType.Int32, p, description: "Batches Completed Today", initialValue: 0);
            tags.Define("BATCHES_THIS_SHIFT", BuiltInType.Int32, p, description: "Batches Completed This Shift", initialValue: 0);
            tags.Define("CURRENT_BATCH_ID", BuiltInType.String, p, description: "Currently Running Batch ID", initialValue: "");

            tags.Define("AVG_BATCH_CYCLE_MIN", BuiltInType.Double, p, "min", "Rolling Average Batch Cycle Time", 0.0);
            tags.Define("LAST_BATCH_CYCLE_MIN", BuiltInType.Double, p, "min", "Last Batch Cycle Time", 0.0);

            tags.Define("BATCH_LINE_OEE_AVAIL", BuiltInType.Double, p, "%", "Batch Line Availability", 0.0);
            tags.Define("BATCH_LINE_OEE_PERF", BuiltInType.Double, p, "%", "Batch Line Performance", 0.0);
            tags.Define("BATCH_LINE_OEE_QUAL", BuiltInType.Double, p, "%", "Batch Line Quality (Cooks Within Spec)", 0.0);
            tags.Define("BATCH_LINE_OEE_OVERALL", BuiltInType.Double, p, "%", "Batch Line OEE Overall", 0.0);

            tags.Define("BATCH_LINE_RUNTIME_MIN", BuiltInType.Double, p, "min", "Batch Line Runtime Today", 0.0);
            tags.Define("BATCH_LINE_DOWNTIME_MIN", BuiltInType.Double, p, "min", "Batch Line Downtime Today", 0.0);
            tags.Define("BATCH_LINE_CIP_TIME_MIN", BuiltInType.Double, p, "min", "Batch Line CIP Time Today", 0.0);
        }

        private static void RegisterPackLine(TagRegistry tags)
        {
            // Packaging line metrics (Areas 500-600 - filling, capping, cooling, labeling, casing, palletizing)
            // KPIs are PackML-oriented: bottles/min, OEE, micro-stops, bottleneck identification
            var p = "PackLine";

            // Line-level PackML state (computed from constituent machine states - common in Krones systems)
            tags.Define("PACK_LINE_OP_STATE", BuiltInType.Int32, p, description: "Packaging Line PackML State (0=Disabled,1=Idle,2=Running,3=Starved,4=Blocked,5=PlannedDT,6=UnplannedDT,7=Other)", initialValue: 1);
            tags.Define("PACK_LINE_OP_STATE_TXT", BuiltInType.String, p, description: "Packaging Line State Text", initialValue: "Idle");
            tags.Define("PACK_LINE_BOTTLENECK", BuiltInType.String, p, description: "Current Bottleneck Equipment", initialValue: "");

            // Line-level PackML mode (set by operator/MES, applies to the whole line)
            tags.Define("PACK_LINE_OP_MODE", BuiltInType.Int32, p, description: "Packaging Line PackML Mode (0=Disabled,1=Idle,2=Production,3=Changeover,4=Maintenance,5=Cleaning,6=Other)", initialValue: 1);
            tags.Define("PACK_LINE_OP_MODE_TXT", BuiltInType.String, p, description: "Packaging Line Mode Text", initialValue: "Idle");
            tags.Define("PACK_LINE_MODE_DUR", BuiltInType.Double, p, "sec", "Time in Current Mode", 0.0);
            tags.Define("PACK_LINE_CURRENT_SKU", BuiltInType.String, p, description: "Currently Running SKU/Product Code", initialValue: "");
            tags.Define("PACK_LINE_NEXT_SKU", BuiltInType.String, p, description: "Next Scheduled SKU (for changeover planning)", initialValue: "");

            // Time-in-mode accumulators today (used for SMED and shift reporting)
            tags.Define("PACK_LINE_PRODUCTION_MIN", BuiltInType.Double, p, "min", "Time in Production Mode Today", 0.0);
            tags.Define("PACK_LINE_CHANGEOVER_MIN", BuiltInType.Double, p, "min", "Time in Changeover Mode Today", 0.0);
            tags.Define("PACK_LINE_CLEANING_MIN", BuiltInType.Double, p, "min", "Time in Cleaning Mode Today", 0.0);
            tags.Define("PACK_LINE_MAINTENANCE_MIN", BuiltInType.Double, p, "min", "Time in Maintenance Mode Today", 0.0);
            tags.Define("PACK_LINE_IDLE_MIN", BuiltInType.Double, p, "min", "Time in Idle Mode Today", 0.0);
            tags.Define("PACK_LINE_CHANGEOVERS_TODAY", BuiltInType.Int32, p, description: "Number of Changeovers Today", initialValue: 0);
            tags.Define("PACK_LINE_LAST_CHANGEOVER_MIN", BuiltInType.Double, p, "min", "Duration of Last Changeover", 0.0);

            tags.Define("BOTTLES_TODAY", BuiltInType.Int32, p, description: "Bottles Produced Today", initialValue: 0);
            tags.Define("BOTTLES_THIS_SHIFT", BuiltInType.Int32, p, description: "Bottles Produced This Shift", initialValue: 0);
            tags.Define("CASES_TODAY", BuiltInType.Int32, p, description: "Cases Packed Today", initialValue: 0);
            tags.Define("PALLETS_TODAY", BuiltInType.Int32, p, description: "Pallets Complete Today", initialValue: 0);

            tags.Define("PACK_LINE_SPEED", BuiltInType.Double, p, "BPM", "Current Line Speed", 0.0);
            tags.Define("PACK_LINE_TARGET_SPEED", BuiltInType.Double, p, "BPM", "Target Line Speed", 200.0);

            tags.Define("PACK_LINE_OEE_AVAIL", BuiltInType.Double, p, "%", "Packaging Line Availability", 0.0);
            tags.Define("PACK_LINE_OEE_PERF", BuiltInType.Double, p, "%", "Packaging Line Performance", 0.0);
            tags.Define("PACK_LINE_OEE_QUAL", BuiltInType.Double, p, "%", "Packaging Line Quality (First Pass Yield)", 0.0);
            tags.Define("PACK_LINE_OEE_OVERALL", BuiltInType.Double, p, "%", "Packaging Line OEE Overall", 0.0);

            tags.Define("PACK_LINE_RUNTIME_MIN", BuiltInType.Double, p, "min", "Packaging Line Runtime Today", 0.0);
            tags.Define("PACK_LINE_DOWNTIME_MIN", BuiltInType.Double, p, "min", "Packaging Line Downtime Today", 0.0);
            tags.Define("PACK_LINE_MICROSTOPS_TODAY", BuiltInType.Int32, p, description: "Micro-stops Today (<5 min)", initialValue: 0);
            tags.Define("PACK_LINE_MAJOR_STOPS_TODAY", BuiltInType.Int32, p, description: "Major Stops Today (>5 min)", initialValue: 0);
        }
    }
}
