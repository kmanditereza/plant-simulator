# Plant Simulator — OPC UA Tag Reference

Every variable the ketchup batch-plant simulator publishes on its OPC UA server: **600 tags** in **43 folders**. Generated from the running simulator's tag catalog, so it matches what a client browses; sample values are a snapshot, not limits.

*Generated 2026-10-13 · sample values at plant time 2027-09-17 09:36 UTC (simulation runs ×60).*

## Connecting

| | |
|---|---|
| **Endpoint (from another machine)** | `opc.tcp://<host>:26543/BatchPlantServer` |
| **Endpoint (inside the Docker stack)** | `opc.tcp://plant-simulator:26543/BatchPlantServer` |
| **Security** | None · anonymous login |
| **Namespace** | `http://opcfoundation.org/BatchPlant` — namespace index **2** |
| **NodeId format** | `ns=2;s=<Folder>/<Tag>` — e.g. `ns=2;s=Area100_RawMaterials/PasteTank1/P101_VIB` |
| **Browse path** | `Objects` → folder path → tag (the browse path mirrors the NodeId) |

The same values are also available over HTTP from the simulator: `GET http://<host>:5050/api/scada/catalog` (definitions) and `/api/scada/values` (live values).

## Conventions

- **Tag naming varies by area on purpose** — it models different control-system eras: Area 100 Foxboro style (`PASTE_TK1_LVL`), Areas 200–300 Experion/ISA style (`TIT-301`), Area 400 area-first (`401-TI-001`), Areas 500–600 OEM style (`FL_501_…`), Area 800 BMS style (`CW_SUP_TEMP`).
- **Running state comes in two shapes:** Int32 status words (`0`=Off, `1`=Run, `2`=Fault) on `*_STS` / `*_STATUS` tags, and Boolean flags on `*_RUN` tags.
- **PackML state** (`*_OP_STATE`, Int32): 0 Disabled · 1 Idle · 2 Running · 3 Starved · 4 Blocked · 5 Planned downtime · 6 Unplanned downtime · 7 Other. `*_OP_STATE_TXT` carries the same as text.
- **Condition monitoring** — every rotating asset has `*_VIB` (mm/s RMS), `*_BRGTEMP` (°C), `*_MCSA` (motor current signature index, %) and `*_FAILCNT`.
- **Writable** tags (marked **RW**) are commands and event-acknowledge flags; everything else is read-only.

## Contents

| Section | Belongs to | Folders | Tags |
|---|---|---|---|
| [Area 100 — Raw Materials](#area-100--raw-materials) | Raw Material Storage | 7 | 53 |
| [Area 200 — Pre-Mix](#area-200--pre-mix) | Production Area | 3 | 26 |
| [Area 300 — Cooking](#area-300--cooking) | Production Area | 3 | 25 |
| [Area 400 — Deaeration & Homogenization](#area-400--deaeration--homogenization) | Production Area | 2 | 40 |
| [Area 450 — Buffer Tanks](#area-450--buffer-tanks) | Production Area | 4 | 45 |
| [Area 500 — Filling](#area-500--filling) | Packaging Area | 5 | 81 |
| [Area 600 — Packaging](#area-600--packaging) | Packaging Area | 5 | 73 |
| [Area 700 — CIP](#area-700--cip) | Production Area (CIP) | 1 | 17 |
| [Area 800 — Utilities](#area-800--utilities) | Utilities | 4 | 64 |
| [Batch Control](#batch-control) | Production Area — batch line | 1 | 24 |
| [Batch Line (KPIs)](#batch-line-kpis) | Production Area — batch line | 1 | 39 |
| [Packaging Line (KPIs)](#packaging-line-kpis) | Packaging Area — pack line | 1 | 57 |
| [Plant Status](#plant-status) | Plant | 1 | 11 |
| [Power Metering](#power-metering) | Electrical feeders | 5 | 45 |

## Area 100 — Raw Materials

*Raw Material Storage*

### `Area100_RawMaterials/PasteTank1`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `P101_AMPS` | Double | A | R | Pump 101 Motor Current | 0 |
| `P101_BRGTEMP` | Double | °C | R | Pump 101 Bearing Temperature | 24.8 |
| `P101_CMD` | Boolean |  | RW | Pump 101 Start Command | false |
| `P101_FAILCNT` | Int32 |  | R | Pump 101 Cumulative Failure Count | 0 |
| `P101_FAULT` | Boolean |  | R | Pump 101 Fault | false |
| `P101_FLOW` | Double | kg/min | R | Pump 101 Flow Rate ⚠ *never updated by the simulator* | 0 |
| `P101_MCSA` | Double | % | R | Pump 101 Motor Current Signature Index | 2.1 |
| `P101_STS` | Int32 |  | R | Pump 101 Status (0=Off,1=Run,2=Fault) | 0 |
| `P101_VIB` | Double | mm/s | R | Pump 101 Vibration (RMS) | 0.052 |
| `PASTE_TK1_LVL` | Double | % | R | Paste Tank 1 Level | 62.7 |
| `PASTE_TK1_TEMP` | Double | °C | R | Paste Tank 1 Temperature | 17.8 |
| `PASTE_TK1_WT` | Double | kg | R | Paste Tank 1 Weight | 13792 |

### `Area100_RawMaterials/PasteTank2`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `P102_BRGTEMP` | Double | °C | R | Pump 102 Bearing Temperature | 25.1 |
| `P102_CMD` | Boolean |  | RW | Pump 102 Start Command | false |
| `P102_FAILCNT` | Int32 |  | R | Pump 102 Cumulative Failure Count | 0 |
| `P102_FAULT` | Boolean |  | R | Pump 102 Fault | false |
| `P102_MCSA` | Double | % | R | Pump 102 Motor Current Signature Index | 1.9 |
| `P102_STS` | Int32 |  | R | Pump 102 Status (0=Off,1=Run,2=Fault) | 0 |
| `P102_VIB` | Double | mm/s | R | Pump 102 Vibration (RMS) | 0.068 |
| `PASTE_TK2_LVL` | Double | % | R | Paste Tank 2 Level | 77.3 |
| `PASTE_TK2_TEMP` | Double | °C | R | Paste Tank 2 Temperature | 17.8 |
| `PASTE_TK2_WT` | Double | kg | R | Paste Tank 2 Weight | 17000 |

### `Area100_RawMaterials/SaltBin`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `SALT_BIN_WT` | Double | kg | R | Salt Bin Weight | 2200 |
| `SALT_FEEDER_BRGTEMP` | Double | °C | R | Salt Feeder Bearing Temperature | 24.6 |
| `SALT_FEEDER_FAILCNT` | Int32 |  | R | Salt Feeder Cumulative Failure Count | 0 |
| `SALT_FEEDER_FAULT` | Boolean |  | R | Salt Feeder Fault | false |
| `SALT_FEEDER_MCSA` | Double | % | R | Salt Feeder Motor Current Signature Index | 2.5 |
| `SALT_FEEDER_RUN` | Boolean |  | R | Salt Feeder Running ⚠ *no dry-dosing step by the simulator* | false |
| `SALT_FEEDER_SPD` | Double | kg/hr | R | Salt Feeder Speed ⚠ *no dry-dosing step by the simulator* | 0 |
| `SALT_FEEDER_VIB` | Double | mm/s | R | Salt Feeder Vibration (RMS) | 0.057 |

### `Area100_RawMaterials/SpiceHopper`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `SPICE_BIN_WT` | Double | kg | R | Spice Hopper Weight | 450 |
| `SPICE_FEEDER_BRGTEMP` | Double | °C | R | Spice Feeder Bearing Temperature | 24.9 |
| `SPICE_FEEDER_FAILCNT` | Int32 |  | R | Spice Feeder Cumulative Failure Count | 0 |
| `SPICE_FEEDER_MCSA` | Double | % | R | Spice Feeder Motor Current Signature Index | 1.8 |
| `SPICE_FEEDER_RUN` | Boolean |  | R | Spice Feeder Running ⚠ *no dry-dosing step by the simulator* | false |
| `SPICE_FEEDER_VIB` | Double | mm/s | R | Spice Feeder Vibration (RMS) | 0.033 |

### `Area100_RawMaterials/SugarSilo`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `SUGAR_BIN_WT` | Double | kg | R | Sugar Silo Weight | 8500 |
| `SUGAR_FEEDER_BRGTEMP` | Double | °C | R | Sugar Feeder Bearing Temperature | 25 |
| `SUGAR_FEEDER_FAILCNT` | Int32 |  | R | Sugar Feeder Cumulative Failure Count | 0 |
| `SUGAR_FEEDER_FAULT` | Boolean |  | R | Sugar Feeder Fault | false |
| `SUGAR_FEEDER_MCSA` | Double | % | R | Sugar Feeder Motor Current Signature Index | 2.5 |
| `SUGAR_FEEDER_RUN` | Boolean |  | R | Sugar Feeder Running ⚠ *no dry-dosing step by the simulator* | false |
| `SUGAR_FEEDER_SPD` | Double | kg/hr | R | Sugar Feeder Speed ⚠ *no dry-dosing step by the simulator* | 0 |
| `SUGAR_FEEDER_VIB` | Double | mm/s | R | Sugar Feeder Vibration (RMS) | 0.051 |

### `Area100_RawMaterials/VinegarTank`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `VIN_TK_FLOW` | Double | L/min | R | Vinegar Flow Rate ⚠ *never updated by the simulator* | 0 |
| `VIN_TK_LVL` | Double | % | R | Vinegar Tank Level | 63.4 |
| `VIN_TK_TEMP` | Double | °C | R | Vinegar Tank Temperature | 20.2 |
| `VIN_VLV_STS` | Boolean |  | R | Vinegar Valve Open ⚠ *never written by the simulator* | false |

### `Area100_RawMaterials/WaterSupply`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `H2O_FLOW` | Double | L/min | R | Water Flow Rate ⚠ *never updated by the simulator* | 0 |
| `H2O_PRESS` | Double | bar | R | Water Supply Pressure | 4.23 |
| `H2O_TEMP` | Double | °C | R | Water Temperature | 15 |
| `H2O_TOTALIZER` | Double | L | R | Water Totalizer | 900 |
| `H2O_VLV_STS` | Boolean |  | R | Water Valve Open ⚠ *never written by the simulator* | false |

## Area 200 — Pre-Mix

*Production Area*

### `Area200_PreMix/Mixer_MX201`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `AGIT201_AMPS` | Double | A | R | Agitator Motor Current | 0 |
| `AGIT201_BRGTEMP` | Double | °C | R | Agitator Bearing Temperature | 24.9 |
| `AGIT201_FAILCNT` | Int32 |  | R | Agitator Cumulative Failure Count | 0 |
| `AGIT201_FAULT` | Boolean |  | R | Agitator Fault | false |
| `AGIT201_MCSA` | Double | % | R | Agitator Motor Current Signature Index | 2 |
| `AGIT201_PWR` | Double | kW | R | Agitator Power | 0 |
| `AGIT201_RUN` | Boolean |  | R | Agitator Running | false |
| `AGIT201_SPD` | Double | RPM | R | Agitator Speed | 0 |
| `AGIT201_TRQ` | Double | Nm | R | Agitator Torque | 0 |
| `AGIT201_VIB` | Double | mm/s | R | Agitator Vibration | 0.018 |
| `FIT-201` | Double | L/min | R | Premix Inlet Flow | 0 |
| `LIT-201` | Double | L | R | Premix Tank Level | 0 |
| `LIT-201_PCT` | Double | % | R | Premix Tank Level Percent | 0 |
| `MX201_BATCH_ID` | String |  | R | Batch ID currently in MX-201 | "" |
| `MX201_MODE` | Int32 |  | R | Mixer Mode (0=Manual,1=Auto) | 2 |
| `MX201_STATE` | Int32 |  | R | Mixer State (0=Idle,1=FillingWater,2=DosingPaste,3=DosingVinegar,4=RampingAgitator,5=Mixing,6=RampingDown,7=Discharging) | 0 |
| `TIT-201` | Double | °C | R | Premix Tank Temperature | 17.1 |
| `WIT-201` | Double | kg | R | Premix Tank Weight | 0 |

### `Area200_PreMix/PID`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PID_201_OP` | Double | % | R | Premix PID Output | 0 |
| `PID_201_PV` | Double | °C | R | Premix PID Process Variable | 17.1 |
| `PID_201_SP` | Double | °C | R | Premix PID Setpoint | 22 |

### `Area200_PreMix/Valves`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `FCV-201` | Double | % | R | Flow Control Valve Position | 0 |
| `XV-201` | Boolean |  | R | Water Inlet Valve Open | false |
| `XV-202` | Boolean |  | R | Paste Inlet Valve Open | false |
| `XV-203` | Boolean |  | R | Vinegar Inlet Valve Open | false |
| `XV-204` | Boolean |  | R | Discharge Valve Open | false |

## Area 300 — Cooking

*Production Area*

### `Area300_Cooking/Kettle_KT301`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `AGIT301_AMPS` | Double | A | R | Kettle Agitator Current | 0 |
| `AGIT301_BRGTEMP` | Double | °C | R | Kettle Agitator Bearing Temperature | 25 |
| `AGIT301_FAILCNT` | Int32 |  | R | Kettle Agitator Cumulative Failure Count | 0 |
| `AGIT301_FAULT` | Boolean |  | R | Kettle Agitator Fault | false |
| `AGIT301_MCSA` | Double | % | R | Kettle Agitator Motor Current Signature Index | 1.6 |
| `AGIT301_RUN` | Boolean |  | R | Kettle Agitator Running ⚠ *motor is never started by the simulator* | false |
| `AGIT301_SPD` | Double | RPM | R | Kettle Agitator Speed | 0 |
| `AGIT301_VIB` | Double | mm/s | R | Kettle Agitator Vibration (RMS) | 0.011 |
| `AIT-301` | Double | pH | R | Kettle Inline pH | 0 |
| `KT301_BATCH_ID` | String |  | R | Batch ID currently in KT-301 | "" |
| `KT301_HOLD_TMR` | Double | sec | R | Pasteurization Hold Timer | 300 |
| `KT301_STATE` | Int32 |  | R | Kettle State (0=Idle,1=Filling,2=HeatingUp,3=Holding,4=Cooling,5=Discharging) | 0 |
| `LIT-301` | Double | L | R | Kettle Level | 0 |
| `LIT-301_PCT` | Double | % | R | Kettle Level Percent | 0 |
| `PIT-301` | Double | bar | R | Kettle Jacket Pressure | -0.001 |
| `PT-301_JKT` | Double | bar | R | Jacket Steam Pressure | -0.01 |
| `TIT-301` | Double | °C | R | Kettle Product Temperature | 84.9 |
| `TIT-302` | Double | °C | R | Kettle Bottom Temperature | 84.2 |

### `Area300_Cooking/PID`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PID_301_MODE` | Int32 |  | R | Cook PID Mode (0=Manual,1=Auto) | 0 |
| `PID_301_OP` | Double | % | R | Cook PID Output | 14.7 |
| `PID_301_PV` | Double | °C | R | Cook PID Process Variable | 89.1 |
| `PID_301_SP` | Double | °C | R | Cook PID Setpoint | 90 |

### `Area300_Cooking/SteamControl`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `FIT-302` | Double | kg/h | R | Steam Flow Rate | 0 |
| `SV301` | Boolean |  | R | Steam Isolation Valve Open | false |
| `TCV-301` | Double | % | R | Steam Temperature Control Valve | 0 |

## Area 400 — Deaeration & Homogenization

*Production Area*

### `Area400_Process/Deaerator_DA401`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `401-FI-001` | Double | L/min | R | Deaerator Product Flow | 0 |
| `401-LI-001` | Double | L | R | Deaerator Level (litres, 2,500 L vessel) | 0 |
| `401-P-001_BRGTEMP` | Double | °C | R | Transfer Pump Bearing Temperature | 25.1 |
| `401-P-001_FAILCNT` | Int32 |  | R | Transfer Pump Cumulative Failure Count | 0 |
| `401-P-001_FAULT` | Boolean |  | R | Transfer Pump Fault | false |
| `401-P-001_FLOW` | Double | L/min | R | Transfer Pump Flow | 0 |
| `401-P-001_MCSA` | Double | % | R | Transfer Pump Motor Current Signature Index | 1.9 |
| `401-P-001_STS` | Int32 |  | R | Transfer Pump Status | 0 |
| `401-P-001_VIB` | Double | mm/s | R | Transfer Pump Vibration (RMS) | 0.047 |
| `401-PI-001` | Double | mmHg | R | Deaerator Vacuum Level | 0 |
| `401-TI-001` | Double | °C | R | Deaerator Temperature | 83.7 |
| `401-VP-001_AMPS` | Double | A | R | Vacuum Pump Current | 0 |
| `401-VP-001_BRGTEMP` | Double | °C | R | Vacuum Pump Bearing Temperature | 24.7 |
| `401-VP-001_FAILCNT` | Int32 |  | R | Vacuum Pump Cumulative Failure Count | 0 |
| `401-VP-001_FAULT` | Boolean |  | R | Vacuum Pump Fault | false |
| `401-VP-001_MCSA` | Double | % | R | Vacuum Pump Motor Current Signature Index | 1.9 |
| `401-VP-001_RUN` | Boolean |  | R | Vacuum Pump Running | false |
| `401-VP-001_VIB` | Double | mm/s | R | Vacuum Pump Vibration (RMS) | 0.033 |
| `DA401_BATCH_ID` | String |  | R | Batch ID currently in DA-401 | "" |
| `DA401_STATE` | Int32 |  | R | Deaerator State (0=Idle,1=Filling,2=PullingVacuum,3=Deaerating,4=Homogenizing,5=VentingVacuum,6=Draining) | 0 |

### `Area400_Process/Homogenizer_HG401`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `HOMOG_401_AMPS` | Double | A | R | Homogenizer Motor Current | 0 |
| `HOMOG_401_BRGTEMP` | Double | °C | R | Homogenizer Bearing Temperature | 25.2 |
| `HOMOG_401_FAILCNT` | Int32 |  | R | Homogenizer Cumulative Failure Count | 0 |
| `HOMOG_401_FAULT` | Boolean |  | R | Homogenizer Fault | false |
| `HOMOG_401_FILTER_DP` | Double | bar | R | Homogenizer Oil Filter Differential Pressure | 0.35 |
| `HOMOG_401_HYD_PRESS` | Double | bar | R | Homogenizer Hydraulic Drive Pressure | 0 |
| `HOMOG_401_HYD_TEMP` | Double | °C | R | Homogenizer Hydraulic Oil Temperature | 25 |
| `HOMOG_401_MCSA` | Double | % | R | Homogenizer Motor Current Signature Index | 1.8 |
| `HOMOG_401_MODE_DUR` | Double | sec | R | Time in Current Mode | 0 |
| `HOMOG_401_OIL_QUAL` | Double | % | R | Homogenizer Lube Oil Quality | 100 |
| `HOMOG_401_OP_MODE` | Int32 |  | R | PackML Operational Mode (0=Disabled,1=Idle,2=Production,3=Changeover,4=Maintenance,5=Cleaning,6=Other) | 1 |
| `HOMOG_401_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Idle |
| `HOMOG_401_OP_STATE` | Int32 |  | R | PackML Operational State (0=Disabled,1=Idle,2=Running,3=Starved,4=Blocked,5=PlannedDT,6=UnplannedDT,7=Other) | 1 |
| `HOMOG_401_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Idle |
| `HOMOG_401_PRESS` | Double | bar | R | Homogenizer Pressure | 0.4 |
| `HOMOG_401_PRESS2` | Double | bar | R | Homogenizer 2nd Stage Pressure | 0 |
| `HOMOG_401_RUN` | Boolean |  | R | Homogenizer Running | false |
| `HOMOG_401_STATE_DUR` | Double | sec | R | Time in Current State | 0 |
| `HOMOG_401_VIB` | Double | mm/s | R | Homogenizer Vibration (RMS) | 0.041 |
| `HOMOG_401_VLV_HRS` | Double | hrs | R | Homogenizer Valve Run Hours | 0.33 |

## Area 450 — Buffer Tanks

*Production Area*

### `Area450_Buffer`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `BUFFER_DISPOSALS_SHIFT` | Int32 |  | R | Number of buffer tanks condemned and dumped this shift | 0 |
| `BUFFER_HOLDING_LOSS_KG_SHIFT` | Double | kg | R | Bulk condemned in the buffer and sent to waste this shift | 0 |
| `BUFFER_HOLDING_LOSS_KG_TOTAL` | Double | kg | R | Bulk condemned in the buffer and sent to waste since startup | 20.2 |

### `Area450_Buffer/BT451`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `BT451_AGIT_BRGTEMP` | Double | °C | R | BT451 Agitator Bearing Temperature | 24.5 |
| `BT451_AGIT_FAILCNT` | Int32 |  | R | BT451 Agitator Cumulative Failure Count | 0 |
| `BT451_AGIT_FAULT` | Boolean |  | R | BT451 Agitator Fault | false |
| `BT451_AGIT_MCSA` | Double | % | R | BT451 Agitator Motor Current Signature Index | 2.2 |
| `BT451_AGIT_RUN` | Boolean |  | R | BT451 Agitator Running | false |
| `BT451_AGIT_VIB` | Double | mm/s | R | BT451 Agitator Vibration (RMS) | 0.039 |
| `BT451_BATCH_ID` | String |  | R | BT451 Held Bulk Batch (produced batch no.) | "" |
| `BT451_HOLD_MIN` | Double | min | R | Plant minutes BT451 has been holding product (0 = empty) | 0 |
| `BT451_LVL` | Double | % | R | Buffer Tank 1 Level | 0 |
| `BT451_LVL_L` | Double | L | R | Buffer Tank 1 Level Liters | 0 |
| `BT451_STATE` | Int32 |  | R | BT451 State (0=Empty,1=Filling,2=Ready,3=Draining,4=Disposing) | 0 |
| `BT451_TEMP` | Double | °C | R | Buffer Tank 1 Temperature | 84.1 |
| `BT451_VLV_IN` | Boolean |  | R | BT451 Inlet Valve | false |
| `BT451_VLV_OUT` | Boolean |  | R | BT451 Outlet Valve | false |

### `Area450_Buffer/BT452`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `BT452_AGIT_BRGTEMP` | Double | °C | R | BT452 Agitator Bearing Temperature | 25.2 |
| `BT452_AGIT_FAILCNT` | Int32 |  | R | BT452 Agitator Cumulative Failure Count | 0 |
| `BT452_AGIT_FAULT` | Boolean |  | R | BT452 Agitator Fault | false |
| `BT452_AGIT_MCSA` | Double | % | R | BT452 Agitator Motor Current Signature Index | 2.4 |
| `BT452_AGIT_RUN` | Boolean |  | R | BT452 Agitator Running | false |
| `BT452_AGIT_VIB` | Double | mm/s | R | BT452 Agitator Vibration (RMS) | 0.035 |
| `BT452_BATCH_ID` | String |  | R | BT452 Held Bulk Batch (produced batch no.) | "" |
| `BT452_HOLD_MIN` | Double | min | R | Plant minutes BT452 has been holding product (0 = empty) | 0 |
| `BT452_LVL` | Double | % | R | Buffer Tank 2 Level | 0 |
| `BT452_LVL_L` | Double | L | R | Buffer Tank 2 Level Liters | 0 |
| `BT452_STATE` | Int32 |  | R | BT452 State (0=Empty,1=Filling,2=Ready,3=Draining,4=Disposing) | 0 |
| `BT452_TEMP` | Double | °C | R | Buffer Tank 2 Temperature | 22 |
| `BT452_VLV_IN` | Boolean |  | R | BT452 Inlet Valve | false |
| `BT452_VLV_OUT` | Boolean |  | R | BT452 Outlet Valve | false |

### `Area450_Buffer/BT453`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `BT453_AGIT_BRGTEMP` | Double | °C | R | BT453 Agitator Bearing Temperature | 25.3 |
| `BT453_AGIT_FAILCNT` | Int32 |  | R | BT453 Agitator Cumulative Failure Count | 0 |
| `BT453_AGIT_FAULT` | Boolean |  | R | BT453 Agitator Fault | false |
| `BT453_AGIT_MCSA` | Double | % | R | BT453 Agitator Motor Current Signature Index | 1.8 |
| `BT453_AGIT_RUN` | Boolean |  | R | BT453 Agitator Running | false |
| `BT453_AGIT_VIB` | Double | mm/s | R | BT453 Agitator Vibration (RMS) | 0.056 |
| `BT453_BATCH_ID` | String |  | R | BT453 Held Bulk Batch (produced batch no.) | "" |
| `BT453_HOLD_MIN` | Double | min | R | Plant minutes BT453 has been holding product (0 = empty) | 0 |
| `BT453_LVL` | Double | % | R | Buffer Tank 3 Level | 0 |
| `BT453_LVL_L` | Double | L | R | Buffer Tank 3 Level Liters | 0 |
| `BT453_STATE` | Int32 |  | R | BT453 State (0=Empty,1=Filling,2=Ready,3=Draining,4=Disposing) | 0 |
| `BT453_TEMP` | Double | °C | R | Buffer Tank 3 Temperature | 21.9 |
| `BT453_VLV_IN` | Boolean |  | R | BT453 Inlet Valve | false |
| `BT453_VLV_OUT` | Boolean |  | R | BT453 Outlet Valve | false |

## Area 500 — Filling

*Packaging Area*

### `Area500_Filling/Capper_CAP501`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `CAP_501_BRGTEMP` | Double | °C | R | Capper Bearing Temperature | 25 |
| `CAP_501_CNT` | Int32 |  | R | Caps Applied Count | 4200 |
| `CAP_501_FAILCNT` | Int32 |  | R | Capper Cumulative Failure Count | 0 |
| `CAP_501_JAM` | Boolean |  | R | Cap Feeder Jam | false |
| `CAP_501_MCSA` | Double | % | R | Capper Motor Current Signature Index | 2.3 |
| `CAP_501_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `CAP_501_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `CAP_501_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `CAP_501_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `CAP_501_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `CAP_501_STATE_DUR` | Double | sec | R | Time in Current State | 23382594 |
| `CAP_501_STS` | Int32 |  | R | Capper Status (0=Off,1=Run,2=Fault) | 0 |
| `CAP_501_TORQUE` | Double | in-lb | R | Cap Torque | 20.01 |
| `CAP_501_VIB` | Double | mm/s | R | Capper Vibration (RMS) | 0.031 |

### `Area500_Filling/Conveyor`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `CONV_501_BRGTEMP` | Double | °C | R | Conveyor Bearing Temperature | 25.3 |
| `CONV_501_FAILCNT` | Int32 |  | R | Conveyor Cumulative Failure Count | 0 |
| `CONV_501_FAULT` | Boolean |  | R | Conveyor Fault/Jam | false |
| `CONV_501_MCSA` | Double | % | R | Conveyor Motor Current Signature Index | 1.6 |
| `CONV_501_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `CONV_501_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `CONV_501_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `CONV_501_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `CONV_501_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `CONV_501_RUN` | Boolean |  | R | Conveyor Running | false |
| `CONV_501_SPD` | Double | BPM | R | Conveyor Speed | 0 |
| `CONV_501_STATE_DUR` | Double | sec | R | Time in Current State | 23382594 |
| `CONV_501_VIB` | Double | mm/s | R | Conveyor Vibration (RMS) | 0.05 |

### `Area500_Filling/CoolingTunnel`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `COOL_TUN_BRGTEMP` | Double | °C | R | Cooling Tunnel Drive Bearing Temperature | 25.1 |
| `COOL_TUN_FAILCNT` | Int32 |  | R | Cooling Tunnel Drive Cumulative Failure Count | 0 |
| `COOL_TUN_MCSA` | Double | % | R | Cooling Tunnel Drive Motor Current Signature Index | 2.3 |
| `COOL_TUN_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `COOL_TUN_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `COOL_TUN_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `COOL_TUN_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `COOL_TUN_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `COOL_TUN_RUN` | Boolean |  | R | Cooling Tunnel Running | false |
| `COOL_TUN_SPRAY_FLOW` | Double | L/min | R | Cooling Spray Flow | 0 |
| `COOL_TUN_SPRAY_TEMP` | Double | °C | R | Cooling Spray Water Temp | 7.9 |
| `COOL_TUN_STATE_DUR` | Double | sec | R | Time in Current State | 23382594 |
| `COOL_TUN_TEMP_IN` | Double | °C | R | Cooling Tunnel Inlet Temp | 22.2 |
| `COOL_TUN_TEMP_MID` | Double | °C | R | Cooling Tunnel Mid Temp | 21.7 |
| `COOL_TUN_TEMP_OUT` | Double | °C | R | Cooling Tunnel Outlet Temp | 21.9 |
| `COOL_TUN_VIB` | Double | mm/s | R | Cooling Tunnel Drive Vibration (RMS) | 0.056 |

### `Area500_Filling/Filler_FL501`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `FILLER_HeadPress_01` | Double | bar | R | Filler Head Pressure | 2.48 |
| `FL_501_BRGTEMP` | Double | °C | R | Filler Bearing Temperature | 25.1 |
| `FL_501_FAILCNT` | Int32 |  | R | Filler Cumulative Fault Trips | 0 |
| `FL_501_FAULT` | Boolean |  | R | Filler Faulted (line down) | false |
| `FL_501_FAULT_CODE` | String |  | R | Active Fault Code (BREAKDOWN / HIGH-VIBRATION / HIGH-BEARING-TEMP) | "" |
| `FL_501_FILL_SPD` | Double | BPM | R | Filler Speed (Bottles/Min) | 0 |
| `FL_501_GOOD_CNT` | Int32 |  | R | Good Bottle Count | 4200 |
| `FL_501_MCSA` | Double | % | R | Filler Motor Current Signature Index | 1.8 |
| `FL_501_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `FL_501_OP_MODE` | Int32 |  | R | PackML Operational Mode (0=Disabled,1=Idle,2=Production,3=Changeover,4=Maintenance,5=Cleaning,6=Other) | 2 |
| `FL_501_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `FL_501_OP_STATE` | Int32 |  | R | PackML Operational State (0=Disabled,1=Idle,2=Running,3=Starved,4=Blocked,5=PlannedDT,6=UnplannedDT,7=Other) | 3 |
| `FL_501_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `FL_501_PROD_TEMP` | Double | °C | R | Product Temperature at Filler | 83.4 |
| `FL_501_REJECT_CNT` | Int32 |  | R | Reject Count | 0 |
| `FL_501_SPEED_LOSS_PCT` | Double | % | R | Wear-induced speed loss vs nameplate (drives OEE Performance below 100%) | 1.97 |
| `FL_501_STATE_DUR` | Double | sec | R | Time in Current State | 23382594 |
| `FL_501_STATUS` | Int32 |  | R | Filler Status (0=Off,1=Run,2=Fault,3=TempLow) | 0 |
| `FL_501_TOTAL_CNT` | Int32 |  | R | Total Bottle Count | 4200 |
| `FL_501_VIB` | Double | mm/s | R | Filler Vibration (RMS) - trips a fault at 3.2 mm/s | 0.055 |
| `FL_501_WEIGHT` | Double | g | R | Current Fill Weight | 398.6 |
| `FL_501_WT_AVG` | Double | g | R | Fill Weight Average | 397.5 |
| `FL_501_WT_STDDEV` | Double | g | R | Fill Weight Std Dev | 2.78 |
| `FL_501_YIELD` | Double | % | R | First Pass Yield | 100 |

### `Area500_Filling/InversionTunnel`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `INV_TUNNEL_BRGTEMP` | Double | °C | R | Inversion Tunnel Drive Bearing Temperature | 25.3 |
| `INV_TUNNEL_FAILCNT` | Int32 |  | R | Inversion Tunnel Drive Cumulative Failure Count | 0 |
| `INV_TUNNEL_MCSA` | Double | % | R | Inversion Tunnel Drive Motor Current Signature Index | 1.7 |
| `INV_TUNNEL_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `INV_TUNNEL_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `INV_TUNNEL_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `INV_TUNNEL_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `INV_TUNNEL_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `INV_TUNNEL_RUN` | Boolean |  | R | Inversion Tunnel Running | false |
| `INV_TUNNEL_SPD` | Double | m/min | R | Inversion Tunnel Speed | 0 |
| `INV_TUNNEL_STATE_DUR` | Double | sec | R | Time in Current State | 23382594 |
| `INV_TUNNEL_TEMP_1` | Double | °C | R | Inversion Tunnel Temp Zone 1 | 22.2 |
| `INV_TUNNEL_TEMP_2` | Double | °C | R | Inversion Tunnel Temp Zone 2 | 20.7 |
| `INV_TUNNEL_VIB` | Double | mm/s | R | Inversion Tunnel Drive Vibration (RMS) | 0.053 |

## Area 600 — Packaging

*Packaging Area*

### `Area600_Packaging/CasePacker_CP601`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `CP_601_BRGTEMP` | Double | °C | R | Case Packer Bearing Temperature | 25 |
| `CP_601_CASES_CNT` | Int32 |  | R | Cases Packed Count | 175 |
| `CP_601_FAILCNT` | Int32 |  | R | Case Packer Cumulative Failure Count | 0 |
| `CP_601_FAULT` | Boolean |  | R | Case Packer Fault | false |
| `CP_601_FILTER_DP` | Double | bar | R | Case Packer Hydraulic Filter Differential Pressure | 0.453 |
| `CP_601_HYD_PRESS` | Double | bar | R | Case Packer Hydraulic Pressure | 0 |
| `CP_601_HYD_TEMP` | Double | °C | R | Case Packer Hydraulic Oil Temperature | 25 |
| `CP_601_MCSA` | Double | % | R | Case Packer Motor Current Signature Index | 2.4 |
| `CP_601_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `CP_601_OIL_QUAL` | Double | % | R | Case Packer Hydraulic Oil Quality | 93.4 |
| `CP_601_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `CP_601_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `CP_601_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `CP_601_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `CP_601_SPEED` | Double | cases/min | R | Case Packer Speed | 0 |
| `CP_601_STATE_DUR` | Double | sec | R | Time in Current State | 29389008 |
| `CP_601_STATUS` | Int32 |  | R | Case Packer Status (0=Off,1=Run,2=Fault) | 0 |
| `CP_601_VIB` | Double | mm/s | R | Case Packer Vibration (RMS) | 0.053 |

### `Area600_Packaging/DateCoder_DC601`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `DC_601_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `DC_601_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `DC_601_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `DC_601_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `DC_601_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `DC_601_PRINT_CNT` | Int32 |  | R | Print Count | 4200 |
| `DC_601_STATE_DUR` | Double | sec | R | Time in Current State | 29389008 |
| `DC_601_STATUS` | Int32 |  | R | Date Coder Status (0=Off,1=Run,2=Fault) | 0 |

### `Area600_Packaging/Inspector_XR601`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `MD_601_CNT` | Int32 |  | R | Metal Detector Trip Count | 0 |
| `MD_601_TRIP` | Boolean |  | R | Metal Detector Trip | false |
| `XRAY_601_BRGTEMP` | Double | °C | R | X-Ray Conveyor Bearing Temperature | 25.1 |
| `XRAY_601_FAILCNT` | Int32 |  | R | X-Ray Conveyor Cumulative Failure Count | 0 |
| `XRAY_601_MCSA` | Double | % | R | X-Ray Conveyor Motor Current Signature Index | 2.1 |
| `XRAY_601_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `XRAY_601_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `XRAY_601_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `XRAY_601_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `XRAY_601_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `XRAY_601_REJECT` | Int32 |  | R | X-Ray Reject Count | 4 |
| `XRAY_601_STATE_DUR` | Double | sec | R | Time in Current State | 29389008 |
| `XRAY_601_STATUS` | Int32 |  | R | X-Ray Status | 0 |
| `XRAY_601_TOTAL` | Int32 |  | R | X-Ray Total Inspected | 4200 |
| `XRAY_601_VIB` | Double | mm/s | R | X-Ray Conveyor Vibration (RMS) | 0.05 |

### `Area600_Packaging/Labeler_LBL601`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `LBL_601_BRGTEMP` | Double | °C | R | Labeler Bearing Temperature | 24.8 |
| `LBL_601_FAILCNT` | Int32 |  | R | Labeler Cumulative Failure Count | 0 |
| `LBL_601_FAULT` | Boolean |  | R | Labeler Fault | false |
| `LBL_601_MCSA` | Double | % | R | Labeler Motor Current Signature Index | 2.5 |
| `LBL_601_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `LBL_601_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `LBL_601_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `LBL_601_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `LBL_601_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `LBL_601_REJECT` | Int32 |  | R | Labeler Reject Count | 0 |
| `LBL_601_SPEED` | Double | BPM | R | Labeler Speed | 0 |
| `LBL_601_STATE_DUR` | Double | sec | R | Time in Current State | 29389008 |
| `LBL_601_STATUS` | Int32 |  | R | Labeler Status (0=Off,1=Run,2=Fault) | 0 |
| `LBL_601_VIB` | Double | mm/s | R | Labeler Vibration (RMS) | 0.054 |

### `Area600_Packaging/Palletizer_PAL601`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PAL_601_BRGTEMP` | Double | °C | R | Palletizer Bearing Temperature | 25.1 |
| `PAL_601_FAILCNT` | Int32 |  | R | Palletizer Cumulative Failure Count | 0 |
| `PAL_601_FAULT` | Boolean |  | R | Palletizer Fault | false |
| `PAL_601_FILTER_DP` | Double | bar | R | Palletizer Hydraulic Filter Differential Pressure | 0.658 |
| `PAL_601_HYD_PRESS` | Double | bar | R | Palletizer Hydraulic Pressure | 0 |
| `PAL_601_HYD_TEMP` | Double | °C | R | Palletizer Hydraulic Oil Temperature | 25 |
| `PAL_601_LAYERS` | Int32 |  | R | Current Pallet Layers | 5 |
| `PAL_601_MCSA` | Double | % | R | Palletizer Motor Current Signature Index | 1.6 |
| `PAL_601_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `PAL_601_OIL_QUAL` | Double | % | R | Palletizer Hydraulic Oil Quality | 90.5 |
| `PAL_601_OP_MODE` | Int32 |  | R | PackML Operational Mode | 2 |
| `PAL_601_OP_MODE_TXT` | String |  | R | PackML Operational Mode Text | Production |
| `PAL_601_OP_STATE` | Int32 |  | R | PackML Operational State | 3 |
| `PAL_601_OP_STATE_TXT` | String |  | R | PackML Operational State Text | Starved |
| `PAL_601_PALLETS_CNT` | Int32 |  | R | Pallets Complete Count | 2 |
| `PAL_601_STATE_DUR` | Double | sec | R | Time in Current State | 29389008 |
| `PAL_601_STATUS` | Int32 |  | R | Palletizer Status (0=Off,1=Run,2=Fault) | 0 |
| `PAL_601_VIB` | Double | mm/s | R | Palletizer Vibration (RMS) | 0.034 |

## Area 700 — CIP

*Production Area (CIP)*

### `Area700_CIP/Circuit1`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `CIP_ACTIVE` | Boolean |  | R | CIP Cycle Active | false |
| `CIP_CIT_701` | Double | µS/cm | R | CIP Conductivity | 0 |
| `CIP_FIT_701` | Double | L/min | R | CIP Flow Rate | 0 |
| `CIP_P701_AMPS` | Double | A | R | CIP Pump Current | 0 |
| `CIP_P701_BRGTEMP` | Double | °C | R | CIP Pump Bearing Temperature | 24.9 |
| `CIP_P701_FAILCNT` | Int32 |  | R | CIP Pump Cumulative Failure Count | 0 |
| `CIP_P701_FAULT` | Boolean |  | R | CIP Pump Fault | false |
| `CIP_P701_MCSA` | Double | % | R | CIP Pump Motor Current Signature Index | 1.9 |
| `CIP_P701_RUN` | Boolean |  | R | CIP Supply Pump Running | false |
| `CIP_P701_VIB` | Double | mm/s | R | CIP Pump Vibration (RMS) | 0.069 |
| `CIP_PHASE` | String |  | R | CIP Phase Description | Idle |
| `CIP_PIT_701` | Double | bar | R | CIP Supply Pressure | 0 |
| `CIP_STEP` | Int32 |  | R | CIP Step (0=Idle,1=PreRinse,2=Caustic,3=IntRinse,4=Acid,5=FinalRinse,6=Sanitize,7=Drain) | 0 |
| `CIP_TIMER` | Double | sec | R | CIP Step Timer | 0 |
| `CIP_TIT_701` | Double | °C | R | CIP Solution Temperature | 22 |
| `CIP_V701_POS` | Double | % | R | CIP Supply Valve Position | 0 |
| `CIP_V702_POS` | Double | % | R | CIP Return Valve Position | 0 |

## Area 800 — Utilities

*Utilities*

### `Area800_Utilities/CompressedAir`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `COMP_AIR_DEW` | Double | °C | R | Air Dewpoint | -39.7 |
| `COMP_AIR_FLOW` | Double | Nm³/h | R | Compressed Air Flow | 122.2 |
| `COMP_AIR_PRESS` | Double | bar | R | Compressed Air Header Pressure | 6.54 |
| `COMP_BRGTEMP` | Double | °C | R | Compressor Bearing Temperature | 59.9 |
| `COMP_FAILCNT` | Int32 |  | R | Compressor Cumulative Failure Count | 0 |
| `COMP_FAULT` | Boolean |  | R | Compressor Fault | false |
| `COMP_FILTER_DP` | Double | bar | R | Compressor Intake Filter Differential Pressure | 4.528 |
| `COMP_MCSA` | Double | % | R | Compressor Motor Current Signature Index | 100 |
| `COMP_OIL_QUAL` | Double | % | R | Compressor Lube Oil Quality | 9.7 |
| `COMP_RUN` | Boolean |  | R | Compressor Running | true |
| `COMP_VIB` | Double | mm/s | R | Compressor Vibration (RMS) | 4.877 |

### `Area800_Utilities/CoolingWater`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `CW_BASIN_LVL` | Double | % | R | Cooling Tower Basin Level | 85.1 |
| `CW_COND` | Double | µS/cm | R | Cooling Water Conductivity | 347 |
| `CW_DEMAND` | Double | % | R | Cooling Demand | 0 |
| `CW_FAN1_BRGTEMP` | Double | °C | R | Cooling Tower Fan 1 Bearing Temperature | 24.9 |
| `CW_FAN1_FAILCNT` | Int32 |  | R | Cooling Tower Fan 1 Cumulative Failure Count | 0 |
| `CW_FAN1_FAULT` | Boolean |  | R | Cooling Tower Fan 1 Fault | false |
| `CW_FAN1_MCSA` | Double | % | R | Cooling Tower Fan 1 Motor Current Signature Index | 2.1 |
| `CW_FAN1_VIB` | Double | mm/s | R | Cooling Tower Fan 1 Vibration (RMS) | 0.068 |
| `CW_FAN2_BRGTEMP` | Double | °C | R | Cooling Tower Fan 2 Bearing Temperature | 25 |
| `CW_FAN2_FAILCNT` | Int32 |  | R | Cooling Tower Fan 2 Cumulative Failure Count | 0 |
| `CW_FAN2_FAULT` | Boolean |  | R | Cooling Tower Fan 2 Fault | false |
| `CW_FAN2_MCSA` | Double | % | R | Cooling Tower Fan 2 Motor Current Signature Index | 2.3 |
| `CW_FAN2_VIB` | Double | mm/s | R | Cooling Tower Fan 2 Vibration (RMS) | 0.066 |
| `CW_FLOW` | Double | m³/h | R | Cooling Water Flow | 0 |
| `CW_PUMP1_BRGTEMP` | Double | °C | R | Cooling Pump 1 Bearing Temperature | 43.9 |
| `CW_PUMP1_FAILCNT` | Int32 |  | R | Cooling Pump 1 Cumulative Failure Count | 0 |
| `CW_PUMP1_FAULT` | Boolean |  | R | Cooling Pump 1 Fault | false |
| `CW_PUMP1_MCSA` | Double | % | R | Cooling Pump 1 Motor Current Signature Index | 100 |
| `CW_PUMP1_STS` | Int32 |  | R | Cooling Pump 1 Status | 1 |
| `CW_PUMP1_VIB` | Double | mm/s | R | Cooling Pump 1 Vibration (RMS) | 2.919 |
| `CW_PUMP2_BRGTEMP` | Double | °C | R | Cooling Pump 2 Bearing Temperature | 25 |
| `CW_PUMP2_FAILCNT` | Int32 |  | R | Cooling Pump 2 Cumulative Failure Count | 0 |
| `CW_PUMP2_FAULT` | Boolean |  | R | Cooling Pump 2 Fault | false |
| `CW_PUMP2_MCSA` | Double | % | R | Cooling Pump 2 Motor Current Signature Index | 2.3 |
| `CW_PUMP2_STS` | Int32 |  | R | Cooling Pump 2 Status (Standby) | 0 |
| `CW_PUMP2_VIB` | Double | mm/s | R | Cooling Pump 2 Vibration (RMS) | 0.046 |
| `CW_RET_TEMP` | Double | °C | R | Cooling Water Return Temperature | 8.3 |
| `CW_SUP_TEMP` | Double | °C | R | Cooling Water Supply Temperature | 8.1 |
| `CW_TOWER_FAN1` | Boolean |  | R | Cooling Tower Fan 1 Running | false |
| `CW_TOWER_FAN2` | Boolean |  | R | Cooling Tower Fan 2 Running | false |

### `Area800_Utilities/HotWater`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `HW_FLOW` | Double | L/min | R | Hot Water Flow | 0.3 |
| `HW_PUMP_BRGTEMP` | Double | °C | R | Hot Water Pump Bearing Temperature | 49.9 |
| `HW_PUMP_FAILCNT` | Int32 |  | R | Hot Water Pump Cumulative Failure Count | 0 |
| `HW_PUMP_FAULT` | Boolean |  | R | Hot Water Pump Fault | false |
| `HW_PUMP_MCSA` | Double | % | R | Hot Water Pump Motor Current Signature Index | 100 |
| `HW_PUMP_STS` | Int32 |  | R | Hot Water Pump Status | 1 |
| `HW_PUMP_VIB` | Double | mm/s | R | Hot Water Pump Vibration (RMS) | 2.769 |
| `HW_RET_TEMP` | Double | °C | R | Hot Water Return Temperature | 65.2 |
| `HW_SUP_TEMP` | Double | °C | R | Hot Water Supply Temperature | 81.9 |

### `Area800_Utilities/Steam`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `BOILER_EFF` | Double | % | R | Boiler Efficiency | 85 |
| `BOILER_FWATER_TEMP` | Double | °C | R | Boiler Feedwater Temperature | 55.1 |
| `BOILER_FW_PUMP_BRGTEMP` | Double | °C | R | Boiler Feedwater Pump Bearing Temperature | 57.7 |
| `BOILER_FW_PUMP_FAILCNT` | Int32 |  | R | Boiler Feedwater Pump Cumulative Failure Count | 0 |
| `BOILER_FW_PUMP_MCSA` | Double | % | R | Boiler Feedwater Pump Motor Current Signature Index | 100 |
| `BOILER_FW_PUMP_VIB` | Double | mm/s | R | Boiler Feedwater Pump Vibration (RMS) | 3.531 |
| `BOILER_PRESS` | Double | bar | R | Boiler Pressure | 7 |
| `BOILER_STS` | Int32 |  | R | Boiler Status (0=Off,1=On,2=Fault) | 1 |
| `STM_COND_TEMP` | Double | °C | R | Condensate Return Temperature | 69.5 |
| `STM_DEMAND` | Double | % | R | Steam Demand | 0 |
| `STM_ENERGY` | Double | kWh | R | Steam Energy Consumed | 694.9 |
| `STM_FLOW` | Double | kg/h | R | Total Steam Flow | 0 |
| `STM_HDR_PRESS` | Double | bar | R | Steam Header Pressure | 6.03 |
| `STM_HDR_TEMP` | Double | °C | R | Steam Header Temperature | 189.8 |

## Batch Control

*Production Area — batch line*

### `BatchControl`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `BATCH_ABORT` | Boolean |  | RW | Batch Abort Command | false |
| `BATCH_COMPLETED` | Boolean |  | RW | Batch Completed flag - true when batch finishes, reset via POST /api/batch/completed/ack | false |
| `BATCH_END_TIME` | DateTime |  | R | Batch End Time | 0001-01-01T00:00:00.0000000 |
| `BATCH_HOLD` | Boolean |  | RW | Batch Hold Command | false |
| `BATCH_ID` | String |  | R | Current Batch ID | "" |
| `BATCH_LINE_QUEUED` | Boolean |  | R | Batch line holding new-batch starts because all buffer tanks are full | false |
| `BATCH_PCT` | Double | % | R | Batch Progress Percent | 0 |
| `BATCH_PHASE` | String |  | R | Current Batch Phase | "" |
| `BATCH_PHASE_TMR` | Double | sec | R | Batch Phase Timer | 0 |
| `BATCH_RELEASED` | Boolean |  | RW | Batch Released flag - true when batch is QA released, reset via POST /api/batch/released/ack | false |
| `BATCH_SCHEDULED` | Boolean |  | RW | Batch Scheduled flag - true when work order is queued, reset via POST /api/batch/scheduled/ack | false |
| `BATCH_SIZE_KG` | Double | kg | R | Batch Size | 3150 |
| `BATCH_START` | Boolean |  | RW | Batch Start Command | false |
| `BATCH_STARTED` | Boolean |  | RW | Batch Started flag - true when batch execution begins, reset via POST /api/batch/started/ack | false |
| `BATCH_START_TIME` | DateTime |  | R | Batch Start Time | 2026-12-20T15:25:06.0000000Z |
| `BATCH_STATUS` | String |  | R | Batch Status | IDLE |
| `DISPOSAL_EVENT_PENDING` | Boolean |  | RW | One or more buffer holding-loss (ProductDisposed) events are queued - GET /api/disposal/events then POST /api/disposal/events/ack | false |
| `DOWNTIME_EVENT_PENDING` | Boolean |  | RW | One or more downtime events are queued - GET /api/downtime/events then POST /api/downtime/events/ack | false |
| `FAULT_EVENT_PENDING` | Boolean |  | RW | One or more equipment fault events are queued - GET /api/fault/events then POST /api/fault/events/ack | false |
| `PACK_ORDER_COMPLETED` | Boolean |  | RW | Packaging Run Completed flag - true when a packaging order finishes, reset via POST /api/packaging/completed/ack | false |
| `PHASE_COMPLETED` | Boolean |  | RW | Phase Completed flag - true when a phase finishes, reset via POST /api/batch/phase-completed/ack | false |
| `PHASE_STARTED` | Boolean |  | RW | Phase Started flag - true when a new phase begins, reset via POST /api/batch/phase-started/ack | false |
| `WO_ACTIVATED` | Boolean |  | RW | Work Order Activated flag - true when batch execution begins, reset via POST /api/work-order/activated/ack | false |
| `WO_COMPLETED` | Boolean |  | RW | Work Order Completed flag - true when batch finishes, reset via POST /api/work-order/completed/ack | false |

## Batch Line (KPIs)

*Production Area — batch line*

### `BatchLine`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `AVG_BATCH_CYCLE_MIN` | Double | min | R | Rolling Average Batch Cycle Time | 165.9 |
| `BATCHES_SHIFT` | Int32 |  | R | Batches Completed This Shift | 0 |
| `BATCHES_TODAY` | Int32 |  | R | Batches Completed Today | 0 |
| `BATCH_LINE_BATCH_ACTUAL_KG` | Double | kg | R | Actual output of BATCH_LINE_BATCH_ID: bulk delivered into the Area 450 buffer, cumulative. Holds while idle. | 1719 |
| `BATCH_LINE_BATCH_COMPLETE` | Boolean |  | R | True when BATCH_LINE_BATCH_ID has finished and the batch outcome tags (ACTUAL/TARGET/YIELD/CYCLE_TIME) are final. False while a batch is running and those values are still accumulating. | true |
| `BATCH_LINE_BATCH_ID` | String |  | R | Batch the outcome tags below describe: the batch on the line, or the last one completed while idle ('' = none yet) | B-20261009-001 |
| `BATCH_LINE_BATCH_TARGET_KG` | Double | kg | R | Theoretical output of BATCH_LINE_BATCH_ID: the control recipe's target quantity. Holds while idle. | 2000 |
| `BATCH_LINE_BATCH_YIELD_PCT` | Double | % | R | Yield of the batch on the line: bulk delivered to buffer vs order target. Holds the last completed batch's yield while idle. | 85.97 |
| `BATCH_LINE_CIP_TIME_MIN` | Double | min | R | Batch Line CIP Time Today | 0 |
| `BATCH_LINE_CURRENT_PHASE` | String |  | R | Current Phase of the Active Batch | "" |
| `BATCH_LINE_CYCLE_TIME_ACTUAL_MIN` | Double | min | R | Actual cycle time of the batch on the line, elapsed so far. Holds the last completed batch's cycle time while idle. | 165.9 |
| `BATCH_LINE_DOWNTIME` | Boolean |  | R | Batch Line is down (stopped for a loss reason). Idle = nothing scheduled, not downtime. | false |
| `BATCH_LINE_DOWNTIME_MIN_SHIFT` | Double | min | R | Batch Line Downtime This Shift, real minutes (no batch in progress) | 63.62 |
| `BATCH_LINE_GOOD_BATCHES_SHIFT` | Int32 |  | R | Good Batches Completed This Shift (no deviations/alarms) | 0 |
| `BATCH_LINE_OEE_AVAIL` | Double | % | R | Batch Line Availability | 0 |
| `BATCH_LINE_OEE_OVERALL` | Double | % | R | Batch Line OEE Overall | 0 |
| `BATCH_LINE_OEE_PERF` | Double | % | R | Batch Line Performance | 0 |
| `BATCH_LINE_OEE_QUAL` | Double | % | R | Batch Line Quality (Cooks Within Spec) | 0 |
| `BATCH_LINE_ORDER_QTY_GOOD` | Double | kg | R | Good quantity completed for the order: product delivered into the buffer so far (0 = idle) | 0 |
| `BATCH_LINE_ORDER_QTY_TARGET` | Double | kg | R | Target quantity of the work order on the line (0 = idle) | 0 |
| `BATCH_LINE_PHASE_ELAPSED` | Double | sec | R | Elapsed Time in Current Phase | 0 |
| `BATCH_LINE_PHASE_TARGET_DURATION` | Double | sec | R | Control recipe's target duration for the current phase; 0 where the phase is quantity- or setpoint-driven rather than timed. Compare with BATCH_LINE_PHASE_ELAPSED. | 0 |
| `BATCH_LINE_PROCESS_ORDER` | String |  | R | Process Order number the current batch fulfils ('' = idle) | "" |
| `BATCH_LINE_RECIPE_ID` | String |  | R | Master recipe of the batch on the line ('' = idle) | "" |
| `BATCH_LINE_RECIPE_VERSION` | String |  | R | Master recipe version of the batch on the line ('' = idle) | "" |
| `BATCH_LINE_RUNNING` | Boolean |  | R | Batch Line Producing | false |
| `BATCH_LINE_RUNTIME_MIN_SHIFT` | Double | min | R | Batch Line Runtime This Shift, real minutes (a batch is in Area 200/300/400) | 0 |
| `BATCH_LINE_SCRAP_BATCHES_SHIFT` | Int32 |  | R | Scrap/Rework Batches This Shift (deviated or aborted) | 0 |
| `BATCH_LINE_SHIFT_DURATION_MIN` | Double | min | R | Shift length in real (wall-clock) minutes | 90 |
| `BATCH_LINE_SHIFT_ELAPSED_MIN` | Double | min | R | Elapsed real minutes within the current shift | 63.61 |
| `BATCH_LINE_SHIFT_ID` | String |  | R | Current shift number (mirror of PlantStatus/SHIFT_ID) | 91 |
| `BATCH_LINE_SHIFT_NUMBER` | Int32 |  | R | Current shift number (mirror of PlantStatus/SHIFT_NUMBER) | 91 |
| `BATCH_LINE_STATE` | String |  | R | Batch Line State (Idle/Producing/CIP/Maintenance/Faulted) | Idle |
| `BATCH_LINE_STATE_REASON` | String |  | R | Why the line is in its current state (Running / Blocked / Idle) | Idle |
| `BATCH_LINE_UPSTREAM_BUFFER_LEVEL` | Double | kg | R | Product held across all three Area 450 buffer tanks (same units as batch size) | 0 |
| `BATCH_LINE_WORK_ORDER_ID` | String |  | R | Work Order Id driving the batch currently on the line ('' = idle) | "" |
| `BATCH_LINE_YIELD_PENDING` | Boolean |  | RW | Latched true when a batch completes and its yield KPI has not yet been published. The KPI consumer writes false to clear it. Independent of BatchControl/BATCH_COMPLETED. | false |
| `CURRENT_BATCH_ID` | String |  | R | Currently Running Batch ID | "" |
| `LAST_BATCH_CYCLE_MIN` | Double | min | R | Last Batch Cycle Time | 165.9 |

## Packaging Line (KPIs)

*Packaging Area — pack line*

### `PackLine`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `BOTTLES_SHIFT` | Int32 |  | R | Bottles Produced This Shift | 0 |
| `BOTTLES_TODAY` | Int32 |  | R | Bottles Produced Today | 0 |
| `CASES_TODAY` | Int32 |  | R | Cases Packed Today | 175 |
| `PACK_LINE_BOTTLENECK` | String |  | R | Current Bottleneck Equipment | Buffer Tanks (Area 450) |
| `PACK_LINE_CHANGEOVERS_TODAY` | Int32 |  | R | Number of Changeovers Today | 0 |
| `PACK_LINE_CHANGEOVER_MIN` | Double | min | R | Time in Changeover Mode Today | 0 |
| `PACK_LINE_CLEANING_MIN` | Double | min | R | Time in Cleaning Mode Today | 0 |
| `PACK_LINE_CURRENT_SKU` | String |  | R | Currently Running SKU/Product Code | FG-KETCHUP-14OZ-CASE |
| `PACK_LINE_CYCLE_TIME` | Double | sec | R | Current Cycle Time (per unit) | 0 |
| `PACK_LINE_DEFECT_COUNT` | Int32 | units | R | Defective (rejected) units this shift, all stations. Same as PACK_LINE_SCRAP_UNITS_SHIFT, named as the quality KPI. | 0 |
| `PACK_LINE_DEFECT_RATE_PPM` | Double | ppm | R | Defect rate this shift in parts per million: scrap / (good + scrap) * 1e6. 0 before any unit is produced. | 0 |
| `PACK_LINE_DOWNSTREAM_BUFFER_LEVEL` | Double | % | R | Fullness across all three Area 450 buffer tanks | 0 |
| `PACK_LINE_DOWNTIME` | Boolean |  | R | Pack Line is down (stopped for a loss reason). Idle/MicroStop are not downtime. | true |
| `PACK_LINE_DOWNTIME_MIN_SHIFT` | Double | min | R | Packaging Line Downtime This Shift, real minutes (order active but not running) | 63.62 |
| `PACK_LINE_FIRST_PASS_YIELD` | Double | % | R | First pass yield this shift: good / (good + scrap) * 100 across all stations. 100 before any unit is produced. | 100 |
| `PACK_LINE_GOOD_UNITS_SHIFT` | Int32 | units | R | Good Units Produced This Shift | 0 |
| `PACK_LINE_IDLE_MIN` | Double | min | R | Time in Idle Mode Today | 0 |
| `PACK_LINE_LAST_CHANGEOVER_MIN` | Double | min | R | Duration of Last Changeover | 0 |
| `PACK_LINE_MAINTENANCE_MIN` | Double | min | R | Time in Maintenance Mode Today | 0 |
| `PACK_LINE_MAJOR_STOPS_TODAY` | Int32 |  | R | Major Stops Today (>5 min) | 0 |
| `PACK_LINE_MICROSTOPS_TODAY` | Int32 |  | R | Micro-stops Today (<5 min) | 0 |
| `PACK_LINE_MODE_DUR` | Double | sec | R | Time in Current Mode | 29389008 |
| `PACK_LINE_NEXT_SKU` | String |  | R | Next Scheduled SKU (for changeover planning) | "" |
| `PACK_LINE_OEE_AVAIL` | Double | % | R | Packaging Line Availability | 0 |
| `PACK_LINE_OEE_OVERALL` | Double | % | R | Packaging Line OEE Overall | 0 |
| `PACK_LINE_OEE_PERF` | Double | % | R | Packaging Line Performance | 100 |
| `PACK_LINE_OEE_QUAL` | Double | % | R | Packaging Line Quality (First Pass Yield) | 100 |
| `PACK_LINE_OP_MODE` | Int32 |  | R | Packaging Line PackML Mode (0=Disabled,1=Idle,2=Production,3=Changeover,4=Maintenance,5=Cleaning,6=Other) | 2 |
| `PACK_LINE_OP_MODE_TXT` | String |  | R | Packaging Line Mode Text | Production |
| `PACK_LINE_OP_STATE` | Int32 |  | R | Packaging Line PackML State (0=Disabled,1=Idle,2=Running,3=Starved,4=Blocked,5=PlannedDT,6=UnplannedDT,7=Other) | 3 |
| `PACK_LINE_OP_STATE_TXT` | String |  | R | Packaging Line State Text | Starved |
| `PACK_LINE_ORDER_QTY_GOOD` | Double | cases | R | Good cases completed for the order (excludes filler-rejected bottles) | 175 |
| `PACK_LINE_ORDER_QTY_RATE` | Double | cases/min | R | Average good cases per plant minute for the current order (ORDER_QTY_GOOD / ORDER_RUNTIME_MIN); 0 before the line runs | 7.322 |
| `PACK_LINE_ORDER_QTY_TARGET` | Double | cases | R | Target quantity of the packaging order on the line (0 = idle) | 500 |
| `PACK_LINE_ORDER_RUNTIME_MIN` | Double | min | R | Plant minutes the current packaging order has actually been Running (0 = idle) | 23.9 |
| `PACK_LINE_PRODUCTION_MIN` | Double | min | R | Time in Production Mode Today | 0 |
| `PACK_LINE_RUNTIME_MIN_SHIFT` | Double | min | R | Packaging Line Runtime This Shift, real minutes | 0 |
| `PACK_LINE_RUNTIME_SINCE_FAILURE_MIN` | Double | min | R | Filler run time since its wear was last reset, in plant minutes. Accrues only while the filler is running. Resets to 0 when a wear-out (condition/high-vibration) fault is repaired or the unit is serviced; a random jam breakdown clears without resetting it. This is the wear clock behind speed loss and vibration, not a shift counter. | 23.7 |
| `PACK_LINE_SCRAP_UNITS_SHIFT` | Int32 | units | R | Scrap/Reject Units This Shift | 0 |
| `PACK_LINE_SHIFT_DURATION_MIN` | Double | min | R | Shift length in real (wall-clock) minutes | 90 |
| `PACK_LINE_SHIFT_ELAPSED_MIN` | Double | min | R | Elapsed real minutes within the current shift | 63.61 |
| `PACK_LINE_SHIFT_ID` | String |  | R | Current shift number (mirror of PlantStatus/SHIFT_ID) | 91 |
| `PACK_LINE_SHIFT_NUMBER` | Int32 |  | R | Current shift number (mirror of PlantStatus/SHIFT_NUMBER) | 91 |
| `PACK_LINE_SPEED` | Double | BPM | R | Current Line Speed | 0 |
| `PACK_LINE_STATE_REASON` | String |  | R | Why the line is in its current state (Running / MicroStop / Idle / Starved / Blocked / MachineFault) | Starved |
| `PACK_LINE_TARGET_SPEED` | Double | BPM | R | Target Line Speed | 200 |
| `PACK_ORD_BOTTLES_DONE` | Int32 | bottles | R | Bottles Produced for Active Packaging Order | 4200 |
| `PACK_ORD_CASES_DONE` | Int32 | cases | R | Cases Produced for Active Packaging Order | 175 |
| `PACK_ORD_ID` | String |  | R | Active Packaging Order Number ('' = idle, no order) | PKG-2000000044 |
| `PACK_ORD_PALLETS_DONE` | Int32 | pallets | R | Pallets Produced for Active Packaging Order | 2 |
| `PACK_ORD_PROGRESS_PCT` | Double | % | R | Active Packaging Order Progress | 35 |
| `PACK_ORD_SKU` | String |  | R | Active Packaging Order Finished-Good SKU | FG-KETCHUP-14OZ-CASE |
| `PACK_ORD_SOURCE_BULK` | String |  | R | Active Packaging Order Source Bulk Material | SF-KETCHUP-BULK-STD |
| `PACK_ORD_STATE` | Int32 |  | R | Packaging Order State (line PackML state; 2=Complete pulse on finish) | 3 |
| `PACK_ORD_TARGET_CASES` | Int32 | cases | R | Packaging Order Target (cases) | 500 |
| `PACK_ORD_WO` | String |  | R | Active Packaging Work Order Id | PWO-MUC-26282-0001 |
| `PALLETS_TODAY` | Int32 |  | R | Pallets Complete Today | 2 |

## Plant Status

*Plant*

### `PlantStatus`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PLANT_MODE` | String |  | R | Plant Mode (Production/CIP/Maintenance/Idle) | Idle |
| `PLANT_RUNNING` | Boolean |  | R | Plant Running (any line producing) | false |
| `PLANT_TOTAL_PRODUCTION_TODAY` | Int32 | cases | R | Total Cases Produced Today (all lines) | 175 |
| `SHIFT_CHANGE_ACTIVE` | Boolean |  | R | Shift Changeover Active | false |
| `SHIFT_DURATION_MIN` | Double | min | R | Shift length in real (wall-clock) minutes | 90 |
| `SHIFT_ELAPSED_MIN` | Double | min | R | Elapsed real minutes within the current shift | 63.61 |
| `SHIFT_ID` | String |  | R | Current shift number (since simulator start or last reset) | 91 |
| `SHIFT_NUMBER` | Int32 |  | R | Current shift number - 1 on boot, +1 every SHIFT_DURATION_MIN | 91 |
| `SHIFT_RESET` | Boolean |  | RW | Shift reset command. Normally true; write false to reset the shift counter back to shift 1 (the tag latches itself back to true). | true |
| `SIM_SPEED` | Double | x | R | Simulation Speed Multiplier | 60 |
| `SIM_TIME` | DateTime |  | R | Current Simulation Time | 2027-09-17T09:36:54.0000000Z |

## Power Metering

*Electrical feeders*

### `PowerMetering/PM-CIP`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PM_CIP_Freq` | Double | Hz | R | Production CIP Grid Frequency | 60 |
| `PM_CIP_I_Avg` | Double | A | R | Production CIP Current 3-Phase Avg | 0.92 |
| `PM_CIP_PF` | Double |  | R | Production CIP Power Factor | 0.702 |
| `PM_CIP_THD_I` | Double | % | R | Production CIP Current THD | 2.6 |
| `PM_CIP_V_LL` | Double | V | R | Production CIP Voltage Line-to-Line Avg | 480.1 |
| `PM_CIP_V_Unbal` | Double | % | R | Production CIP Voltage Unbalance | 0.72 |
| `PM_CIP_kVAR` | Double | kVAR | R | Production CIP Reactive Power | 0.55 |
| `PM_CIP_kW` | Double | kW | R | Production CIP Active Power | 0.54 |
| `PM_CIP_kWh` | Double | kWh | R | Production CIP Active Energy (cumulative) | 9.576e+04 |

### `PowerMetering/PM-PKG1`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PM_PKG1_Freq` | Double | Hz | R | Packaging Line 1 (Filling, Packaging) Grid Frequency | 60.01 |
| `PM_PKG1_I_Avg` | Double | A | R | Packaging Line 1 (Filling, Packaging) Current 3-Phase Avg | 58.34 |
| `PM_PKG1_PF` | Double |  | R | Packaging Line 1 (Filling, Packaging) Power Factor | 0.912 |
| `PM_PKG1_THD_I` | Double | % | R | Packaging Line 1 (Filling, Packaging) Current THD | 10.5 |
| `PM_PKG1_V_LL` | Double | V | R | Packaging Line 1 (Filling, Packaging) Voltage Line-to-Line Avg | 471.4 |
| `PM_PKG1_V_Unbal` | Double | % | R | Packaging Line 1 (Filling, Packaging) Voltage Unbalance | 1.11 |
| `PM_PKG1_kVAR` | Double | kVAR | R | Packaging Line 1 (Filling, Packaging) Reactive Power | 19.81 |
| `PM_PKG1_kW` | Double | kW | R | Packaging Line 1 (Filling, Packaging) Active Power | 43.46 |
| `PM_PKG1_kWh` | Double | kWh | R | Packaging Line 1 (Filling, Packaging) Active Energy (cumulative) | 2.535e+05 |

### `PowerMetering/PM-PL1`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PM_PL1_Freq` | Double | Hz | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Grid Frequency | 59.99 |
| `PM_PL1_I_Avg` | Double | A | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Current 3-Phase Avg | 8.86 |
| `PM_PL1_PF` | Double |  | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Power Factor | 0.751 |
| `PM_PL1_THD_I` | Double | % | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Current THD | 2.6 |
| `PM_PL1_V_LL` | Double | V | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Voltage Line-to-Line Avg | 479 |
| `PM_PL1_V_Unbal` | Double | % | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Voltage Unbalance | 0.78 |
| `PM_PL1_kVAR` | Double | kVAR | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Reactive Power | 4.86 |
| `PM_PL1_kW` | Double | kW | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Active Power | 5.54 |
| `PM_PL1_kWh` | Double | kWh | R | Production Line 1 (PreMix, Cooking, Processing, Buffer) Active Energy (cumulative) | 4.495e+04 |

### `PowerMetering/PM-RM`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PM_RM_Freq` | Double | Hz | R | Raw Materials Storage Grid Frequency | 60.01 |
| `PM_RM_I_Avg` | Double | A | R | Raw Materials Storage Current 3-Phase Avg | 6.7 |
| `PM_RM_PF` | Double |  | R | Raw Materials Storage Power Factor | 0.821 |
| `PM_RM_THD_I` | Double | % | R | Raw Materials Storage Current THD | 2.4 |
| `PM_RM_V_LL` | Double | V | R | Raw Materials Storage Voltage Line-to-Line Avg | 478.5 |
| `PM_RM_V_Unbal` | Double | % | R | Raw Materials Storage Voltage Unbalance | 0.95 |
| `PM_RM_kVAR` | Double | kVAR | R | Raw Materials Storage Reactive Power | 3.18 |
| `PM_RM_kW` | Double | kW | R | Raw Materials Storage Active Power | 4.54 |
| `PM_RM_kWh` | Double | kWh | R | Raw Materials Storage Active Energy (cumulative) | 2.449e+04 |

### `PowerMetering/PM-UTIL`

| Tag | Type | Unit | Access | Description | Sample |
|---|---|---|---|---|---|
| `PM_UTIL_Freq` | Double | Hz | R | Utilities (Steam, Cooling, Compressed Air) Grid Frequency | 60 |
| `PM_UTIL_I_Avg` | Double | A | R | Utilities (Steam, Cooling, Compressed Air) Current 3-Phase Avg | 66.63 |
| `PM_UTIL_PF` | Double |  | R | Utilities (Steam, Cooling, Compressed Air) Power Factor | 0.881 |
| `PM_UTIL_THD_I` | Double | % | R | Utilities (Steam, Cooling, Compressed Air) Current THD | 8 |
| `PM_UTIL_V_LL` | Double | V | R | Utilities (Steam, Cooling, Compressed Air) Voltage Line-to-Line Avg | 475.3 |
| `PM_UTIL_V_Unbal` | Double | % | R | Utilities (Steam, Cooling, Compressed Air) Voltage Unbalance | 0.94 |
| `PM_UTIL_kVAR` | Double | kVAR | R | Utilities (Steam, Cooling, Compressed Air) Reactive Power | 26.12 |
| `PM_UTIL_kW` | Double | kW | R | Utilities (Steam, Cooling, Compressed Air) Active Power | 48.36 |
| `PM_UTIL_kWh` | Double | kWh | R | Utilities (Steam, Cooling, Compressed Air) Active Energy (cumulative) | 3.359e+05 |

## Known gaps

11 tags (marked ⚠ above) are defined in the address space but not driven by the simulation: the raw-material flow and supply-valve tags are never written, the kettle agitator motor is never started (`AGIT301_RUN` stays false), and the sugar/salt/spice feeders never run because the pre-mix cycle has no dry-dosing step. A client reading them gets a valid but constant value.
