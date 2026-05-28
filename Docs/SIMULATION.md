# Ketchup Batch Plant Simulator v2.0

A .NET 8 console application that simulates a realistic ketchup manufacturing plant. All process data is exposed via an OPC UA server on port 26543, mimicking how a DCS (Distributed Control System) would present data in a real facility.

## Purpose

This simulator creates a realistic 24/7 production environment for a ketchup manufacturing plant. The goal is to produce data that mirrors what you would find in a traditional food manufacturing facility, suitable for building data collection pipelines, contextualisation layers, analytics dashboards, and digital transformation demonstrations.

The simulation deliberately models:
- **What data IS available** from a DCS (transmitter readings, valve positions, motor states, batch status)
- **What data IS NOT available** electronically (lab measurements like Brix, viscosity, color — these are taken manually and recorded on paper or in a LIMS system)

---

## Plant Overview

The simulated plant produces 14oz (397g) squeeze bottles of ketchup. The process follows real ketchup manufacturing practices validated against food engineering references and equipment manufacturer documentation.

### Process Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        BATCH SIDE (ISA-88)                          │
│                                                                     │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌────────────────┐  │
│  │ Area 100 │──▶│ Area 200 │──▶│ Area 300 │──▶│   Area 400     │  │
│  │Raw Matl  │   │ Pre-Mix  │   │ Cooking  │   │Deaerate + Homog│  │
│  │ Storage  │   │(Breddo)  │   │(Kettle)  │   │                │  │
│  └──────────┘   └──────────┘   └──────────┘   └───────┬────────┘  │
│                                                        │           │
│                                              ┌─────────▼─────────┐ │
│                                              │    Area 450       │ │
│                                              │  Buffer Tanks     │ │
│                                              │  (BT451/452/453)  │ │
│                                              └─────────┬─────────┘ │
└────────────────────────────────────────────────────────┼───────────┘
                                                         │
                    ┌────────────────────────────────────┘
                    │ BATCH ↔ CONTINUOUS DECOUPLING POINT
                    │
┌───────────────────▼─────────────────────────────────────────────────┐
│                   DISCRETE / CONTINUOUS SIDE                        │
│                                                                     │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌────────────────┐  │
│  │ Area 500 │──▶│ Area 500 │──▶│ Area 600 │──▶│   Area 600     │  │
│  │ Filling  │   │ Cooling  │   │ Labeling │   │Case Pack + Pal │  │
│  │(Hot Fill)│   │ Tunnel   │   │+Inspect  │   │                │  │
│  └──────────┘   └──────────┘   └──────────┘   └────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘

Support Systems:
  Area 700: CIP (Clean-in-Place)
  Area 800: Utilities (Steam, Cooling Water, Compressed Air, Hot Water)
```

### Production Parameters

| Parameter | Value | Notes |
|-----------|-------|-------|
| Batch size | 3,000 L (~3,150 kg) | Per cook kettle batch |
| Bottles per batch | ~7,900 | 397g per bottle |
| Filling speed | 200 bottles/min | 12-head rotary piston filler |
| Batch cycle time | 45–60 min | Dose through buffer tank fill |
| Batches per day | 8–10 | Depends on CIP schedule |
| Hot fill temperature | >82°C | FDA food safety requirement |
| Finished pH | 3.5–3.9 | Acid food (measured in lab, not on DCS) |
| Finished Brix | 28–33° | Measured in lab, not on DCS |

### Recipe (per 3,150 kg batch)

| Ingredient | % | Amount | Source |
|-----------|---|--------|--------|
| Tomato paste (28-30 Brix) | 30% | 945 kg | Paste Tank 1 or 2 |
| Sugar | 22% | 693 kg | Sugar Silo |
| Water | 20% | 630 kg | Municipal supply |
| Vinegar (120-grain) | 12% | 378 kg | Vinegar Tank |
| Salt | 2.5% | 79 kg | Salt Bin |
| Onion/Spice blend | 1.5% | 47 kg | Spice Hopper |
| Modified starch / Xanthan | 1% | 32 kg | Spice Hopper |
| Other | 11% | balance | — |

---

## Plant Areas

### Area 100 — Raw Material Storage

20,000L paste tanks, 10,000L vinegar tank, sugar silo, salt bin, spice hopper, and municipal water supply. Progressive cavity pumps transfer tomato paste (highly viscous, abrasive). Loss-in-weight and flow metering control ingredient dosing.

**Equipment**: 2 paste tanks, 1 vinegar tank, 1 sugar silo, 1 salt bin, 1 spice hopper, 2 paste pumps (Seepex-type progressive cavity), flow meters, weigh feeders.

### Area 200 — Pre-Mixing

3,500L high-shear batch mixer (Breddo Likwifier type) with anchor agitator and high-shear head. Ingredients are dosed in sequence: water first (heated to ~60°C to dissolve dry ingredients), then dry ingredients (sugar, salt, spices), then tomato paste, then vinegar last (acid added after hydration to prevent protein coagulation).

**Equipment**: Jacketed mixing vessel with VFD-driven agitator (0–1500 RPM), inlet valves (XV-201 through XV-204), flow control valve (FCV-201), discharge valve.

### Area 300 — Cooking / Pasteurization

3,000L jacketed steam kettle with scraped-surface agitation. Product is heated to 90°C via steam jacket (6 bar saturated steam at ~165°C) and held for 5 minutes for pasteurization. PID-controlled steam valve modulates jacket temperature. Inline pH transmitter provides continuous process monitoring.

**Equipment**: Jacketed cook kettle (KT-301), scraped-surface agitator, steam control valve (TCV-301), steam isolation valve (SV301), inline pH analyzer (AIT-301).

### Area 400 — Deaeration & Homogenization

**Vacuum Deaerator (DA-401)**: Removes dissolved oxygen to prevent oxidative browning and extend shelf life. Operates at 680–720 mmHg vacuum. This step is critical for ketchup quality — without it, the product darkens rapidly.

**Homogenizer (HG-401)**: High-pressure homogenizer (200 bar, two-stage) breaks down tomato fiber for uniform consistency and smooth mouthfeel. The valve set is a high-wear item that degrades over ~1,000 run hours, causing gradual pressure loss.

### Area 450 — Buffer / Holding Tanks

Three 5,000L insulated agitated tanks that decouple batch production from continuous filling. This is the critical batch-to-continuous interface:
- Batch side fills whichever tank is available
- Filling line draws from any tank with level >20% and temperature >82°C
- Slow-speed anchor agitator prevents settling without shearing the product
- If all tanks drop below 5%, the filling line starves and stops

### Area 500 — Filling Line

**Hot Fill**: Product is filled at >82°C — the hot product sterilizes the container and headspace (FDA requirement for acid foods). 12-head rotary piston filler at 200 bottles/min.

**Capping**: Rotary capper with torque control (18–22 in-lb). Cap feeder is a common source of micro-stops (jams).

**Inversion Tunnel**: Bottles travel cap-down for ~10 seconds immediately after capping. The hot product pasteurizes the closure and headspace.

**Cooling Tunnel**: Spray cooling takes bottles from 85°C down to 35°C over ~20 minutes through 3 temperature zones. Controlled cooling rate prevents thermal shock.

### Area 600 — Packaging Line

Continuous line downstream of filling:
- **Labeler**: Pressure-sensitive label applicator (200 BPM)
- **Date Coder**: Inkjet printer (batch ID, best-by date, line ID)
- **X-Ray Inspector**: Checks fill level and foreign objects (100% inspection)
- **Metal Detector**: Ferrous/non-ferrous detection
- **Case Packer**: 24 bottles per case (~8.3 cases/min)
- **Palletizer**: 5 layers × 12 cases = 60 cases per pallet (~1 pallet every 7.2 min)

### Area 700 — CIP (Clean-in-Place)

Automated cleaning system that runs every 8–12 hours or between product changeovers. A full CIP cycle takes ~75 minutes and ties up the batch-side equipment (Areas 200–400).

| Step | Duration | Solution | Temperature |
|------|----------|----------|-------------|
| Pre-Rinse | 10 min | Water | 40°C |
| Caustic Wash | 20 min | 2% NaOH | 75°C |
| Intermediate Rinse | 10 min | Water (until conductivity <200 µS/cm) | 40°C |
| Acid Wash | 15 min | 1% Phosphoric acid | 60°C |
| Final Rinse | 10 min | Water (until conductivity <100 µS/cm) | 25°C |
| Sanitize | 5 min | 200 ppm Peracetic acid | Ambient |
| Drain | 5 min | Air blow | — |

### Area 800 — Utilities

Plant utility systems that feed all production areas:
- **Steam**: 7 bar boiler, 6 bar header, supplies kettle jackets and CIP heating
- **Cooling Water**: 8°C supply, cooling tower with 2 pumps and 2 fans
- **Compressed Air**: 6.5 bar, instrument-quality, for valve actuators and pneumatic conveyors
- **Hot Water**: 82°C supply for CIP make-up

---

## Tag Naming Conventions

Tags are intentionally named inconsistently across areas. This reflects reality — different parts of a plant are installed by different contractors and equipment vendors at different times, each with their own naming preferences.

| Area | Era | Convention | Examples |
|------|-----|-----------|----------|
| 100 | Legacy Foxboro (~1995) | No hyphens, uppercase, abbreviations | `PASTE_TK1_LVL`, `P101_STS`, `H2O_FLOW` |
| 200–300 | Honeywell Experion (~2005) | ISA-5.1 with inconsistencies | `TIT-201`, `AGIT201_SPD` (motor lost its hyphen), `SV301` |
| 400 | 2012 upgrade | Area-first numbering | `401-TI-001`, `401-VP-001_RUN`, `HOMOG_401_PRESS` |
| 500–600 | Krones OEM (~2018) | Mixed case, underscores, PascalCase leaks | `FL_501_WEIGHT`, `FILLER_HeadPress_01`, `CAP_501_JAM` |
| 700 | Integrated with DCS (~2005) | CIP prefix convention | `CIP_TIT_701`, `CIP_CIT_701`, `CIP_STEP` |
| 800 | Schneider BMS (~2010) | Abbreviated underscore | `STM_HDR_PRESS`, `CW_SUP_TEMP`, `BOILER_EFF` |

---

## OPC UA Address Space

Connect to: `opc.tcp://localhost:26543/BatchPlantServer`

```
Objects/
  Area100_RawMaterials/
    PasteTank1/           PASTE_TK1_LVL, PASTE_TK1_TEMP, PASTE_TK1_WT, P101_STS, P101_CMD...
    PasteTank2/           PASTE_TK2_LVL, PASTE_TK2_TEMP, P102_STS...
    VinegarTank/          VIN_TK_LVL, VIN_TK_TEMP, VIN_TK_FLOW...
    SugarSilo/            SUGAR_BIN_WT, SUGAR_FEEDER_SPD, SUGAR_FEEDER_RUN
    SaltBin/              SALT_BIN_WT, SALT_FEEDER_SPD
    SpiceHopper/          SPICE_BIN_WT, SPICE_FEEDER_RUN
    WaterSupply/          H2O_FLOW, H2O_PRESS, H2O_TEMP, H2O_TOTALIZER
  Area200_PreMix/
    Mixer_MX201/          TIT-201, LIT-201, AGIT201_SPD, AGIT201_AMPS, AGIT201_VIB...
    Valves/               XV-201, XV-202, XV-203, XV-204, FCV-201
    PID/                  PID_201_SP, PID_201_PV, PID_201_OP
  Area300_Cooking/
    Kettle_KT301/         TIT-301, TIT-302, PIT-301, LIT-301, AIT-301, AGIT301_SPD...
    SteamControl/         TCV-301, SV301, FIT-302
    PID/                  PID_301_SP, PID_301_PV, PID_301_OP, PID_301_MODE
  Area400_Process/
    Deaerator_DA401/      401-TI-001, 401-PI-001, 401-VP-001_RUN...
    Homogenizer_HG401/    HOMOG_401_PRESS, HOMOG_401_AMPS, HOMOG_401_VLV_HRS...
  Area450_Buffer/
    BT451/                BT451_LVL, BT451_TEMP, BT451_AGIT_RUN...
    BT452/                BT452_LVL, BT452_TEMP...
    BT453/                BT453_LVL, BT453_TEMP...
  Area500_Filling/
    Filler_FL501/         FL_501_WEIGHT, FL_501_FILL_SPD, FL_501_GOOD_CNT, FL_501_YIELD...
    Capper_CAP501/        CAP_501_STS, CAP_501_JAM, CAP_501_TORQUE...
    InversionTunnel/      INV_TUNNEL_TEMP_1, INV_TUNNEL_TEMP_2...
    CoolingTunnel/        COOL_TUN_TEMP_IN, COOL_TUN_TEMP_OUT, COOL_TUN_SPRAY_FLOW...
    Conveyor/             CONV_501_SPD, CONV_501_RUN, CONV_501_FAULT
  Area600_Packaging/
    Labeler_LBL601/       LBL_601_STATUS, LBL_601_SPEED...
    DateCoder_DC601/      DC_601_STATUS, DC_601_PRINT_CNT
    Inspector_XR601/      XRAY_601_TOTAL, XRAY_601_REJECT, MD_601_TRIP...
    CasePacker_CP601/     CP_601_CASES_CNT, CP_601_SPEED...
    Palletizer_PAL601/    PAL_601_LAYERS, PAL_601_PALLETS_CNT...
  Area700_CIP/
    Circuit1/             CIP_TIT_701, CIP_CIT_701, CIP_STEP, CIP_TIMER, CIP_PHASE...
  Area800_Utilities/
    Steam/                STM_HDR_PRESS, STM_HDR_TEMP, BOILER_STS, BOILER_EFF...
    CoolingWater/         CW_SUP_TEMP, CW_RET_TEMP, CW_BASIN_LVL, CW_COND...
    CompressedAir/        COMP_AIR_PRESS, COMP_AIR_FLOW...
    HotWater/             HW_SUP_TEMP, HW_RET_TEMP...
  BatchControl/           BATCH_START, BATCH_ID, BATCH_STATUS, BATCH_PHASE...
  PlantStatus/            PLANT_MODE, SHIFT_ID, OEE_OVERALL, SIM_TIME, SIM_SPEED...
```

---

## Data Availability — What a Real Plant Exposes

### Available on OPC UA (what the DCS provides)

These are the measurements that come from hardwired transmitters, PLCs, and control systems:

- Temperature transmitters (RTDs, thermocouples) — `TIT-xxx`, `TT-xxx`
- Pressure transmitters — `PIT-xxx`, `PT-xxx`
- Level transmitters (radar, guided wave, load cell) — `LIT-xxx`
- Flow transmitters (Coriolis, mag-flow) — `FIT-xxx`
- Valve positions and feedback — `TCV-xxx`, `XV-xxx`, `FCV-xxx`
- Motor status (run/fault), speed, current — `AGIT201_RUN`, `P101_STS`
- PID controller outputs, setpoints, modes — `PID_xxx_OP/SP/PV`
- Inline pH (on cooking kettle) — `AIT-301`
- Conductivity (CIP system) — `CIP_CIT_701`
- Batch status, phase, timers — `BATCH_STATUS`, `BATCH_PHASE`
- Fill counts, reject counts, case counts — `FL_501_GOOD_CNT`, `CP_601_CASES_CNT`
- Equipment states and fault codes
- Utility readings (steam pressure, cooling water temperature, etc.)

### NOT available on OPC UA (separate systems in a real plant)

| Data | Where it lives in a real plant |
|------|-------------------------------|
| Brix (concentration) | QC Lab — manual refractometer reading, recorded on paper or LIMS |
| Lab pH | QC Lab — benchtop pH meter, separate from inline pH |
| Viscosity (Bostwick/Brookfield) | QC Lab — manual test, results in LIMS |
| Color (L*a*b*) | QC Lab — spectrophotometer, LIMS |
| Sensory evaluation | QC Lab — trained panel, paper records |
| Microbiology results | QC Lab — 48-72 hour incubation, LIMS |
| ATP swab results | Sanitation crew — handheld luminometer |
| Operator names | HR/MES system |
| Recipe details | MES or batch management system (Wonderware InBatch, PlantPAx) |
| Maintenance work orders | CMMS (SAP PM, Maximo) |
| Inventory / raw material lots | ERP (SAP, Oracle) |
| Cost data | ERP financial module |
| Shipping / logistics | WMS (Warehouse Management System) |

This distinction is important: in a real digitalisation project, integrating these disparate data sources is one of the biggest challenges.

---

## Simulation Architecture

### Tick-Based Engine

The simulator uses a synchronous tick-based engine rather than independent async loops. Every 100ms of wall-clock time, all components are updated in deterministic order:

```
1. SimClock          — advance simulation time
2. Utilities         — steam/cooling/air availability
3. FaultEngine       — probabilistic equipment failures
4. Equipment         — pumps, motors, valves (physics)
5. Process Areas     — area controllers (100–800)
6. BatchManager      — batch orchestration (ISA-88)
7. FillingLine       — discrete bottle production
8. PackagingLine     — labeling, inspection, casing
9. ShiftManager      — shift transitions, CIP scheduling
10. TagFlusher       — push all changed values to OPC UA nodes
```

### Time Acceleration

The `SIM_SPEED` environment variable controls simulation speed:

| SIM_SPEED | Effect | Use Case |
|-----------|--------|----------|
| 1 | Real-time (1 sec = 1 sec) | Production monitoring demo |
| 10 | 10x (1 hour = 6 min) | Shift-level observation |
| 60 | 60x (1 day = 24 min) | Multi-day trend analysis |

### Equipment Models

Each piece of equipment is modeled with:
- **State machine**: Off → Starting → Running → Stopping → Faulted → Maintenance
- **Physics**: Speed ramps, PID control, heat transfer, mass balance
- **Wear tracking**: Run hours since service, gradual degradation
- **Sensor noise**: Realistic measurement noise (Gaussian, not uniform)

---

## Shift Pattern

The plant operates 24/7 with three 8-hour shifts:

| Shift | Hours | Typical Activity |
|-------|-------|------------------|
| Day | 06:00–14:00 | Full production, most staffing, QC sampling |
| Swing | 14:00–22:00 | Full production, reduced management presence |
| Night | 22:00–06:00 | Full production, minimal support staff |

Each shift transition includes a 15-minute handover period where batch production pauses but the filling line continues (draining buffer tanks).

### Weekly Pattern

- **Monday–Friday**: Full 3-shift production
- **Saturday**: Day shift only (reduced production)
- **Sunday**: Maintenance window (06:00–18:00), no production

---

## Fault Simulation

Equipment failures are modeled using MTBF (Mean Time Between Failures) and MTTR (Mean Time To Repair) with Gaussian distributions. Faults auto-resolve after the repair time elapses (simulating maintenance crew response).

| Equipment | Fault | MTBF | MTTR | Type |
|-----------|-------|------|------|------|
| Paste pump (P-101) | Seal failure | 500 hrs | 3 hrs | Sudden stop |
| Transfer pump (P-201) | Cavitation | 800 hrs | 1.5 hrs | Sudden stop |
| Homogenizer (HG-401) | Valve wear | 1000 hrs | 4 hrs | Gradual pressure loss |
| Cook kettle (KT-301) | Steam trap fail | 2000 hrs | 2 hrs | Efficiency drop |
| Filler (FL-501) | Piston wear | 800 hrs | 2 hrs | Gradual weight drift |
| Conveyor (CONV-501) | Jam | 8 hrs | 9 min | Micro-stop |
| Capper (CAP-501) | Feeder jam | 12 hrs | 3 min | Micro-stop |
| Labeler (LBL-601) | Misfeed | 20 hrs | 2 min | Micro-stop |
| Boiler | Low water | 3000 hrs | 1 hr | Sudden stop |
| CIP system | ATP swab fail | N/A | 20 min | 15% per cycle |

### Gradual Degradation

Some equipment degrades gradually rather than failing suddenly:
- **Homogenizer valve wear**: Operating pressure drops ~0.02 bar per run hour. At ~160 bar (vs. 200 bar setpoint), an alarm triggers and the valve set must be replaced.
- **Filler piston wear**: Fill weight standard deviation increases 0.5g per 100 run hours. SPC charts would show the process going out of control before any single bottle fails specification.
- **Heat exchanger fouling**: Pressure drop increases with cook time. Fouling recovers partially during CIP.

---

## Running the Simulator

### Local

```bash
cd BatchPlant
dotnet run
```

With time acceleration:
```bash
SIM_SPEED=10 dotnet run
```

### Docker

```bash
docker compose up --build
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `SIM_SPEED` | `1` | Time acceleration factor (1–60) |
| `SIM_START` | Next Monday 06:00 UTC | Override simulation start time (ISO 8601) |

### Ports

| Port | Protocol | Description |
|------|----------|-------------|
| 26543 | OPC UA TCP | OPC UA server endpoint |

---

## Tech Stack

- **Runtime**: .NET 8 (C#, console application)
- **OPC UA**: OPCFoundation.NetStandard.Opc.Ua v1.5.375.457
- **Modbus**: FluentModbus v5.3.2 (optional, for utilities)
- **Container**: Docker (Linux), docker-compose
