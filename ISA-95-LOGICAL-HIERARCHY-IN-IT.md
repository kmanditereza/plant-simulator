# The ISA-95/88 Logical Hierarchy in the IT Layer

This document goes deeper into how the ISA-95/88 logical hierarchy is actually **used** at the IT level — by MES, ERP, historian, and analytics platforms — to enable recipe management, batch genealogy, OEE rollups, and corporate reporting.

The OPC UA address space exposed by the simulator (organised by physical Area) is unchanged. The logical hierarchy described here lives **above** that, in IT systems that consume the OPC UA data and overlay it with structure.

---

## Where Does the Logical Hierarchy Actually Live?

The hierarchy is a **data model**, not a physical thing. It doesn't live in the DCS or PLC. It lives in one or more IT systems above the DCS:

```
┌──────────────────────────────────────────────────────────────────────┐
│  ISA-95 Level 4: Business Planning                                  │
│  ┌────────┐  ┌──────────┐  ┌──────────┐                             │
│  │  ERP   │  │   PLM    │  │  CMMS    │  ← Enterprise master data  │
│  │ (SAP)  │  │ (Windchill)│ │(Maximo) │                             │
│  └────────┘  └──────────┘  └──────────┘                             │
└──────────────────────────────────────────────────────────────────────┘
                              ▲
                              │   B2MML / REST / OData
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│  ISA-95 Level 3: Manufacturing Operations Management                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                │
│  │     MES      │  │   Historian  │  │     LIMS     │  ← THE HIERARCHY│
│  │(Wonderware,  │  │  (PI, Aspen  │  │ (LabWare,    │   LIVES HERE   │
│  │ FactoryTalk, │  │  IP21, etc.) │  │  STARLIMS)   │                │
│  │ SAP MII)     │  │              │  │              │                │
│  └──────────────┘  └──────────────┘  └──────────────┘                │
│  ┌──────────────────────────────────────────────────┐                │
│  │   Batch Management (InBatch, FT Batch, DeltaV)   │                │
│  └──────────────────────────────────────────────────┘                │
└──────────────────────────────────────────────────────────────────────┘
                              ▲
                              │   OPC UA / OPC DA / MQTT / S7 / Modbus
                              ▼
┌──────────────────────────────────────────────────────────────────────┐
│  ISA-95 Level 2: Plant Floor Control                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                │
│  │     DCS      │  │     PLC      │  │  OPC UA      │  ← Physical    │
│  │ (Honeywell,  │  │  (Rockwell,  │  │   Server     │   tags here    │
│  │  ABB, Emerson)│ │  Siemens)    │  │ (our sim)    │                │
│  └──────────────┘  └──────────────┘  └──────────────┘                │
└──────────────────────────────────────────────────────────────────────┘
```

The IT systems at Level 3 import a list of physical tags from the DCS (via OPC UA) and **assign them to logical equipment in the model**. The mapping table lives in the IT system, not in the DCS.

For example, in the historian (PI):
- The physical tag from OPC UA is `TIT-301` (just a string).
- In the PI Asset Framework (PI AF), you create an "Element" called `KetchupCo / Springfield / SauceProduction / BatchLine1 / KT-301 / VesselInstrumentation / ProductTemperature`
- That Element has an "Attribute" called `Value` that **points at** the physical PI Point `TIT-301`.

Now when an analytics tool queries "average product temperature for KT-301 during Batch B-20260420-001", it traverses the AF hierarchy and resolves to the physical PI Point automatically. The recipe knows nothing about `TIT-301` — it just asks for "the cook kettle's product temperature."

This indirection is the whole point. It lets you:
- Swap the physical instrument without changing recipes/reports
- Run the same recipe on multiple lines that use different vendors
- Compute the same KPI identically across multiple sites
- Trace a quality issue from a finished pallet back to the raw materials

---

## The Four Big Uses, Concretely

### 1. MES Recipe Management

ISA-88 defines four levels of recipes, each at a different point in the IT stack:

| Recipe Type | Lives in | Owns | Example for Ketchup |
|-------------|----------|------|---------------------|
| **General Recipe** | R&D / PLM | Process scientists | "Ketchup formula concept" — ingredients, ratios, target Brix/pH, no equipment specified |
| **Site Recipe** | Site MES | Plant engineering | "Springfield ketchup" — General recipe adapted to Springfield's ingredient suppliers, water chemistry, regulatory market |
| **Master Recipe** | Batch Management System | Production engineering | "Springfield Standard Ketchup 14oz on BatchLine1" — Site recipe bound to specific equipment (MX-201, KT-301, etc.), with PID setpoints, valve sequences, hold times |
| **Control Recipe** | Batch Engine + DCS | Operations | A specific instance: "Batch B-20260420-001, target 3,150 kg, scheduled for 09:30, allocated to BatchLine1" |

When an operator hits "Start Batch", the **Control Recipe** is created on the fly by combining the Master Recipe with order-specific parameters from the schedule (lot numbers, target size, customer assignment) and downloading parameters to the PLC.

#### Why the logical hierarchy matters here

A Master Recipe is written against the logical hierarchy, not against physical tags:

```
Recipe: "Springfield Standard Ketchup 14oz"
  Procedure: Make Ketchup
    Unit Procedure: Pre-Mix
      Equipment Class: HighShearMixer  ← logical, not "MX-201"
      Operation: Dose Water
        Parameter: WaterTarget = 630 kg
        Phase: Open inlet valve, totalize via flow meter until target reached
      Operation: Add Tomato Paste
        Parameter: PasteTarget = 945 kg
        Phase: Run paste pump, dose by loss-in-weight
      ...
    Unit Procedure: Cook
      Equipment Class: SteamKettle      ← logical, not "KT-301"
      Operation: Heat
        Parameter: Setpoint = 90 degC
        Phase: PID auto, ramp steam valve
      Operation: Pasteurization Hold
        Parameter: HoldTime = 300 sec
        Parameter: MinTemp = 88 degC
        Phase: Hold, monitor product temp
```

When the recipe runs, the Batch Engine queries the equipment hierarchy: "find a `HighShearMixer` in `BatchLine1` that is `Available`." It picks `MX-201`, binds the recipe to it, and downloads the actual tag-level commands.

If you build a second batch line at Springfield (`BatchLine2`) with a Breddo mixer instead of a different vendor's, the **same recipe** runs on it — the binding just picks `MX-202` instead. Without the logical hierarchy you'd have to re-author the recipe with new tag names.

#### Real products that do this

- **Wonderware InBatch** (now AVEVA Batch Management)
- **Rockwell FactoryTalk Batch**
- **Siemens SIMATIC Batch**
- **Emerson DeltaV Batch**
- **ABB 800xA Batch**

All of these maintain the equipment hierarchy internally and use it to manage recipe portability.

---

### 2. Batch Records and Genealogy

A **batch record** is the complete history of one batch — every parameter, every actual value, every operator action, every alarm, every deviation. In regulated industries (food safety, pharma) this becomes the **electronic batch record (EBR)** and has legal weight under 21 CFR Part 11.

Genealogy goes one step further: it links the batch record to **everything that touched it**, both upstream and downstream.

#### Forward genealogy (recall scenario)

You discover a quality issue with tomato paste lot `LOT-PASTE-2026-04-15-0042`. You need to know every finished product that contains it.

```
LOT-PASTE-2026-04-15-0042  (raw material lot)
  └── consumed by: Batch B-20260420-001 (Springfield, BatchLine1)
        ├── batch produced 7,910 bottles
        ├── filled at FL-501
        ├── cased into:
        │    ├── Case CASE-20260420-0312 (24 bottles)
        │    ├── Case CASE-20260420-0313
        │    └── ... (~329 cases total)
        ├── palletized into:
        │    ├── Pallet PAL-20260420-0023 (60 cases)
        │    ├── Pallet PAL-20260420-0024
        │    └── Pallet PAL-20260420-0025
        └── shipped to:
             ├── Customer A, Order SO-78451
             └── Customer B, Order SO-78462
```

Forward genealogy lets you issue a precise recall: only those specific pallets and customers. Without it, you might recall the entire week's production "to be safe."

#### Backward genealogy (root cause scenario)

A customer complains about a weird taste in bottle barcode `8-12345-67890`. You scan it and want to know everything about how it was made.

```
Bottle barcode: 8-12345-67890
  ├── Pallet PAL-20260420-0023 (palletizer PAL-601, layer 3, position 7)
  ├── Case CASE-20260420-0312 (case packer CP-601, position 14)
  ├── Filled by: FL-501, head 7, at 11:34:18 on 2026-04-20
  ├── Fill weight: 396.8 g (within spec)
  ├── Fill temperature: 84.2 degC (within spec)
  ├── From: Buffer Tank BT-452 (filling started 11:31, ended 11:58)
  ├── BT-452 was loaded by: Batch B-20260420-001
  │     ├── Master Recipe: Springfield Standard Ketchup 14oz v3.2
  │     ├── Operator on shift: Day shift, supervisor M. Chen
  │     ├── Cook kettle KT-301
  │     │     ├── Peak temperature: 91.1 degC
  │     │     ├── Hold time at >88degC: 312 sec  ← longer than recipe
  │     │     ├── Steam valve max: 87%
  │     │     └── Inline pH at end: 3.71
  │     ├── Homogenizer HG-401
  │     │     ├── Average pressure: 198 bar  ← below recipe target 200
  │     │     ├── Valve hours: 1,247  ← high, due for replacement
  │     ├── Raw material lots:
  │     │     ├── Tomato paste: LOT-PASTE-2026-04-15-0042
  │     │     ├── Vinegar: LOT-VIN-2026-04-12-0007
  │     │     └── Sugar: LOT-SUG-2026-04-18-0019
  │     ├── Lab tests on this batch:
  │     │     ├── pH: 3.71 (target 3.6-3.9)  ← from QC lab, not OPC UA
  │     │     ├── Brix: 31.2 (target 30-32)  ← from QC lab, not OPC UA
  │     │     ├── Viscosity: 5,200 cP        ← from QC lab, not OPC UA
  │     │     └── Color: Pass                 ← from QC lab, not OPC UA
  │     └── Alarms during batch: 1 (homogenizer pressure low warning)
```

That's a complete trace from the customer complaint to the root cause: a homogenizer with a worn valve set was running below pressure, possibly affecting texture and mouthfeel.

#### Why the logical hierarchy matters here

The genealogy graph **is** the logical hierarchy made temporal. Every node in the trace above corresponds to an entity in the ISA-95/88 model:
- "Pallet PAL-..." links to `Palletizer PAL-601` which is in `PackLine1` → `Packaging Area` → `Springfield` → `KetchupCo`
- "Cook kettle KT-301" links to `Unit KT-301` in `BatchLine1` → `SauceProduction Area` → `Springfield`
- "Raw material lots" link to `Work Units` in `RawMaterials Area`
- "Lab tests" link by Batch ID, not by equipment, because they live in **LIMS**, not the DCS
- "Operator on shift" comes from **HR/MES**, not the DCS

The MES is the system that **stitches all of this together** by using Batch ID as the join key across all the data sources. That's only possible if every system speaks the same hierarchy.

#### Real products that do this

- **AVEVA MES (Wonderware MES)**
- **SAP ME / SAP DM**
- **Rockwell FactoryTalk ProductionCentre**
- **Siemens Opcenter Execution**
- **Tulip** (newer, lighter weight)
- **GE Proficy Plant Applications**

---

### 3. OEE Rollups

OEE (Overall Equipment Effectiveness) is calculated as **Availability × Performance × Quality**. The interesting thing is that the formula is the same at every level of the hierarchy, but the **inputs change**.

#### Equipment-level OEE (Filler FL-501)

```
Time period: One 8-hour shift (480 min)
  Planned downtime: CIP allocation = 60 min
  Planned production time: 480 - 60 = 420 min

  Unplanned downtime:
    - Conveyor jam micro-stops: 8 events × 2 min = 16 min
    - Cap feeder jam: 3 events × 4 min = 12 min
    - Filler pressure low: 1 event × 8 min = 8 min
    Total: 36 min

  Run time: 420 - 36 = 384 min

Availability = Run / Planned = 384 / 420 = 91.4%

Theoretical rate: 200 BPM
Bottles produced: 71,500
Performance = (71,500 / 384) / 200 = 93.1%

Good bottles: 70,890
Quality = 70,890 / 71,500 = 99.1%

OEE = 0.914 × 0.931 × 0.991 = 84.4%
```

#### Line-level OEE (PackLine1)

You **cannot** simply average the equipment OEEs together. You have to recalculate at the line level using the **slowest constraint**:

```
Planned production time: 420 min (same as above)
Line downtime: time when ANY machine in the chain stopped the line
  - Includes propagated stops from upstream/downstream
  - Excludes brief stops absorbed by buffers between machines

Line run time: 372 min
Availability (line) = 372 / 420 = 88.6%

Performance is calculated against the line's theoretical rate (200 BPM)
using the actual output of the SLOWEST machine.

Quality is end-of-line good count vs total count, accounting for
rejects from any inspection station along the line.
```

This is why the **bottleneck identification** (`PACK_LINE_BOTTLENECK` tag) matters — it tells you which machine is currently constraining the line, and therefore which machine's OEE matters most.

#### Area-level OEE (Packaging Area)

If the area has multiple lines, the area-level OEE weights each line by its **planned production volume** or **capacity**:

```
PackLine1: OEE 84.4%, planned 84,000 bottles
PackLine2: OEE 78.2%, planned 60,000 bottles

Area OEE = (84.4 × 84,000 + 78.2 × 60,000) / (84,000 + 60,000) = 81.8%
```

#### Site-level OEE (Springfield Plant)

Roll up across all areas, weighted by some common metric (often gross margin contribution rather than volume, because some products are more profitable than others).

#### Enterprise-level OEE (KetchupCo)

Roll up across all sites. This is what gets reported to the board.

#### Why the logical hierarchy matters here

The rollups only work if every site computes OEE the same way. ISA-95 doesn't define the formula, but providing a **consistent equipment hierarchy** is the foundation. Without it, you can't compare:

- "Fill line OEE at Springfield" vs "Fill line OEE at Newark" — are they measuring the same thing?
- "Cook kettle utilisation" between sites that have different kettles
- "Raw material yield" when one site reports kg-in-kg-out and another reports cases-out-per-tonne-in

A common hierarchy plus a common KPI library (often called a **Common Information Model** or **CIM**) is what makes corporate benchmarking real instead of theatre.

#### Real products that do this

- **Sight Machine** (purpose-built for cross-plant OEE)
- **Seeq** (analytics with hierarchy support)
- **AVEVA PI Vision + PI AF** (PI AF holds the hierarchy)
- **TrendMiner**
- **Ignition with Sepasoft OEE module**
- **Datch / Augury** (newer plays)
- **Custom Power BI / Tableau / Looker** dashboards on top of a data warehouse

---

### 4. Corporate Reporting and Benchmarking

The endgame for many digitalisation programs is corporate-wide visibility:

> "Show me top-quartile vs bottom-quartile performing plants on cost-per-case, OEE, energy intensity, and labour productivity. Drill into the bottom plants by line, by shift, by product, and identify what they could learn from the top plants."

For this to be meaningful, every plant has to report the same things in the same shape. The logical hierarchy provides the **shape**, and a corporate KPI dictionary provides the **definitions**.

#### A typical corporate scorecard for the ketchup plant

```
KPI: Cases per Labour Hour (Springfield, Sauce + Packaging combined)
  Springfield ketchup line, week 16:    142 cases/hr
  Newark ketchup line, week 16:         158 cases/hr
  Fresno ketchup line, week 16:         166 cases/hr  ← top performer
  Phoenix ketchup line, week 16:        128 cases/hr  ← bottom
  Corporate average:                    149 cases/hr
  Corporate target:                     155 cases/hr
```

For Fresno's number to be comparable to Springfield's, both plants need to:
- Define "ketchup line" the same way (which work centers/work units are in scope?)
- Define "labour hour" the same way (only operators? or include maintenance?)
- Define "case" the same way (counted at palletizer? or at case packer?)
- Roll up to the same time boundary (calendar week ends Sunday? or shift week ends Friday?)
- Apply the same allocation logic for shared equipment (CIP labour split between products)

The ISA-95 hierarchy gives you **the same words for the same things**. Without it, every plant invents its own terminology and rollup gets impossible. With it, every plant publishes data shaped like:

```
{
  "Enterprise": "KetchupCo",
  "Site": "Springfield",
  "Area": "SauceProduction",
  "WorkCenter": "BatchLine1",
  "ReportingPeriod": "2026-W16",
  "CasesProduced": 23800,
  "LabourHours": 168,
  "CasesPerLabourHour": 141.7
}
```

A simple SQL query against the corporate data warehouse pivots this across all sites for the dashboard.

#### Real products that do this

- **SAP Analytics Cloud / SAP BW**
- **Oracle Manufacturing Cloud / Oracle Analytics**
- **Microsoft Fabric / Azure Synapse + Power BI**
- **Snowflake + Tableau / Looker / Sigma**
- **Databricks Lakehouse for Manufacturing**

These are general-purpose analytics platforms. The **value** they provide depends entirely on whether the data feeding them follows a consistent hierarchy. Without that, you get garbage-in-garbage-out at scale.

---

## End-to-End: One Batch Through the Whole Stack

Here's how a single batch of ketchup flows through every layer of the IT stack, using the logical hierarchy as the connecting thread:

```
1. Sales / Demand Planning (ERP)
   Customer order: 50 pallets of 14oz ketchup, deliver 2026-04-25
   ERP creates Production Order: PO-78451, qty 3,000 cases
   Allocates to: Site=Springfield (closest to customer)

2. Production Scheduling (MES)
   Schedules PO-78451 onto: Area=SauceProduction, WorkCenter=BatchLine1
   Calculates: 3,000 cases / 24 bottles per case / 7,910 bottles per batch
              = ~9 batches needed, ~9 hours production
   Adds CIP allocation: 1 hour
   Schedules: 2026-04-20 09:30 to 19:30

3. Batch Management (InBatch / FT Batch)
   Loads Master Recipe: "Springfield Standard Ketchup 14oz v3.2"
   For each scheduled batch:
     Creates Control Recipe: B-20260420-001, B-20260420-002, ...
     Allocates equipment: MX-201, KT-301, DA-401, HG-401, BT-451 (or BT-452, BT-453)
     Reserves raw material lots: queries WMS for available lots
       Tomato paste: LOT-PASTE-2026-04-15-0042 (FIFO)
       Vinegar:      LOT-VIN-2026-04-12-0007
       Sugar:        LOT-SUG-2026-04-18-0019

4. Execution (DCS / PLC)
   Operator clicks "Start" on B-20260420-001
   Batch engine downloads phase parameters to PLC:
     PreMix.WaterTarget = 630.0
     PreMix.PasteTarget = 945.0
     PreMix.AgitatorRPM = 1500
     Cook.Setpoint = 90.0
     Cook.HoldTime = 300
     ...
   PLC executes phases, writes report parameters back as it goes
   OPC UA tags update in real-time:
     TIT-201, TIT-301, AGIT201_SPD, HOMOG_401_PRESS, BT451_LVL, ...

5. Historian (PI / IP21)
   Captures every OPC UA tag at 1-second resolution
   PI AF resolves each tag into the logical hierarchy:
     TIT-301 → KetchupCo/Springfield/SauceProduction/BatchLine1/KT-301/VesselInstrumentation/ProductTemperature
   Tags are now queryable both physically AND logically.

6. Batch Engine (back at Level 3)
   On phase completion, reads report parameters:
     Cook.PeakTemp = 91.1
     Cook.HoldDuration = 312
     Cook.SteamValveMax = 87
     Homog.AvgPressure = 198
   Writes to Batch Record (electronic batch record):
     B-20260420-001
       ├── Recipe: Springfield Standard Ketchup 14oz v3.2
       ├── Equipment: BatchLine1 (MX-201, KT-301, DA-401, HG-401, BT-452)
       ├── Operator: J. Smith (from MES login)
       ├── Lots: LOT-PASTE-..., LOT-VIN-..., LOT-SUG-...
       ├── Phase Records: (one per phase, with all params)
       ├── Alarms: 1 (homogenizer pressure low warning)
       └── Status: Completed

7. LIMS (parallel, separate system)
   Lab tech takes a sample, runs Brix/pH/viscosity/color
   Records against Batch ID B-20260420-001:
     pH: 3.71 (target 3.6-3.9)  ✓
     Brix: 31.2 (target 30-32)  ✓
     Viscosity: 5,200 cP        ✓
     Color: Pass                ✓
   LIMS publishes lab results to MES/EBR via Batch ID join.

8. Filling and Packaging (Levels 2-3)
   BT-452 starts feeding FL-501 around 11:30
   Each bottle gets a barcode label with: Lot=B-20260420-001, BestBy=2027-04-20
   Cases are barcoded; pallets are SSCC-tagged
   PackLine1 reports OEE in real-time via PackML states
   Production counts roll up to BatchLine1's batch record

9. Warehouse Management (WMS)
   Pallets PAL-20260420-0023 through 0025 received
   Linked to PO-78451
   Held for QC release (waiting on lab final approval)
   Lab releases batch → pallets become available for picking
   Picked, loaded, shipped to Customer A and Customer B

10. Corporate Data Warehouse / Analytics
    Nightly ETL pulls:
      - Production data from MES (cases, OEE, downtime)
      - Quality data from LIMS (lab results, holds, releases)
      - Cost data from ERP (raw material costs, labour)
      - Energy data from utilities BMS (kWh, steam tonnes)
    All keyed by Batch ID and the logical hierarchy
    Dashboard shows: Springfield Sauce Production / Week 16:
      - 47 batches completed
      - 2 batches on hold (1 quality, 1 weight)
      - Average OEE: 82.4%
      - Cost per case: $4.18 (target $4.05)
      - Energy per case: 0.42 kWh (target 0.40)

11. Recall scenario (3 weeks later)
    Quality issue traced to LOT-PASTE-2026-04-15-0042
    Forward genealogy query in MES:
      "Show all batches that consumed LOT-PASTE-2026-04-15-0042"
      → Returns: B-20260420-001 through B-20260420-004
    "Show all pallets from those batches"
      → Returns: PAL-20260420-0023 through 0089
    "Show all customer shipments containing those pallets"
      → Returns: 7 customer orders
    Recall is precise to the customer order level.
```

The Batch ID `B-20260420-001` is the **join key** that connects every level of the stack. The logical hierarchy provides the **structure** that lets queries traverse equipment, time, lots, products, and orders without ambiguity.

---

## What This Means for Your Digitalisation Project

Practical implications when you start building data pipelines on top of the simulator:

### 1. Don't try to reorganise the OPC UA address space
You don't need to. Real plants don't. Keep it as the physical/historical view.

### 2. Build the logical hierarchy in your historian or data platform
Whether that's PI AF, Ignition Tag Provider with UDTs, an Ignition Sepasoft model, an Azure Industrial IoT data model, or a custom asset model in your data warehouse — that's where you implement the ISA-95/88 structure.

### 3. The mapping table is itself a deliverable
Document which OPC UA tags belong to which logical equipment. Treat it as code, version it, review changes. This is one of the most valuable artefacts of the project — and one of the easiest to let drift.

### 4. Use Batch ID as the universal join key
Every data source — DCS, MES, LIMS, ERP, WMS — should be able to attach a Batch ID to its records. That's what makes batch genealogy possible. The DCS publishes Batch ID via OPC UA (we have `BATCH_ID` in `BatchControl/`); other systems include it in their own native data formats.

### 5. Computed KPIs should be defined once, used everywhere
Don't compute "OEE" three different ways in three different dashboards. Define it once in the data platform layer, document the formula, and have all dashboards consume the canonical metric.

### 6. The historian is for time-series; the MES is for events
The historian stores `TIT-301 = 89.5 at 11:34:18`. The MES stores `Phase 'Cook' completed at 11:34:23 with PeakTemp=91.1`. They're complementary, not redundant. The logical hierarchy is what lets analytics queries join them on equipment + time + Batch ID.

### 7. Lab data is its own world
LIMS lives outside the DCS hierarchy. Integration is by Batch ID and sample ID, not by tag mapping. Plan for this — it's where many data projects underestimate complexity.

---

## Tools That Implement the Hierarchy at Level 3

| Layer | Open-source / Lightweight | Mid-market | Enterprise |
|-------|---------------------------|------------|------------|
| Historian | InfluxDB, TimescaleDB, QuestDB | Ignition Tag Historian, Canary Labs | OSIsoft PI System (now AVEVA), Aspen IP21, GE Proficy Historian |
| Asset Model | Custom in Postgres | Ignition with UDTs, Sepasoft OEE | PI AF, AVEVA System Platform, Honeywell Forge |
| Batch Mgmt | Custom (rare) | Ignition + Sepasoft Batch | Wonderware InBatch, FT Batch, SIMATIC Batch, DeltaV Batch |
| MES | Tulip, Apriso (Dassault) | Plex, IQMS | SAP ME/DM, Wonderware MES, FT ProductionCentre, Siemens Opcenter |
| Analytics | Grafana, Metabase | Seeq, TrendMiner, Sight Machine | Power BI, Tableau, Snowflake, Databricks |
| LIMS | Bika, OpenLIMS | LabWare, Thermo SampleManager | LabWare LIMS, STARLIMS, Thermo SampleManager |

The simulator's OPC UA server sits to the left of all of these — it's the **source of physical data**. Everything else is layered on top.

---

## Summary

The ISA-95/88 logical hierarchy is **not a thing you build into the OPC UA server**. It's a **data model you build into the IT systems above the OPC UA server**, using the OPC UA tags as raw inputs.

It enables four things:
1. **Recipe management** — write recipes once, run on any compatible equipment
2. **Batch genealogy** — trace any finished product back to raw materials, and vice versa
3. **OEE rollups** — equipment → line → area → site → enterprise, computed consistently
4. **Corporate benchmarking** — compare like with like across plants and time

The hierarchy is implemented in MES, historian asset frameworks, batch management systems, and corporate data warehouses. The OPC UA tag list is one input to that hierarchy, not the hierarchy itself.

The single most valuable artefact of a digitalisation project is often the **mapping table** that connects physical OPC UA tags to logical equipment in the hierarchy. Treat it as code. Version it. Test it. It is the spine of everything built above it.
