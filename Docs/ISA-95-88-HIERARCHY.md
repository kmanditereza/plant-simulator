# ISA-95 / ISA-88 Equipment Hierarchy for the Ketchup Plant

This document describes how the ketchup plant simulator maps to the **ISA-95 equipment hierarchy** and the **ISA-88 batch control standard**. These two standards are the most widely-used reference models for organising manufacturing operations and they work together.

## Why this matters

Most digital transformation projects in manufacturing eventually run into the question: "how do we organise our data?" The natural temptation is to organise by physical layout (which is what our current OPC UA address space does — by Area 100, 200, 300...). But for **MES integration, batch genealogy, OEE rollups, recipe management, and corporate-wide reporting**, you need a logical hierarchy that:

- Distinguishes enterprise/site/area/equipment levels cleanly
- Maps physical equipment to functional roles in the production process
- Supports recipe portability across similar equipment in different sites
- Enables consistent KPI rollups (equipment → line → area → site → enterprise)

ISA-95 and ISA-88 give you exactly that.

---

## The Two Standards in One Picture

```
┌────────────────────────────────────────────────────────────────────────┐
│                          ISA-95                                        │
│              (Enterprise / Manufacturing Integration)                  │
│                                                                        │
│   Enterprise ──► Site ──► Area ──► Work Center ──► Work Unit           │
│       │            │        │           │              │              │
│       │            │        │           │              │              │
│       └────────────┼────────┼───────────┼──────────────┼──────────────┐│
│                    │        │           │              │              ││
│  ┌─────────────────┼────────┼───────────┼──────────────┼─────────────┐││
│  │                 ▼        ▼           ▼              ▼             │││
│  │   Enterprise ──► Site ──► Area ──► Process Cell ──► Unit          │││
│  │                                                       │           │││
│  │                                                       ▼           │││
│  │                                              Equipment Module     │││
│  │                                                       │           │││
│  │                                                       ▼           │││
│  │                                                Control Module     │││
│  │                                                                   │││
│  │                       ISA-88                                      │││
│  │              (Batch Control Standard)                             │││
│  └───────────────────────────────────────────────────────────────────┘││
└────────────────────────────────────────────────────────────────────────┘
```

The top three levels (**Enterprise / Site / Area**) are identical between ISA-95 and ISA-88. They diverge below that:

| ISA-95 | ISA-88 | What it represents |
|--------|--------|-------------------|
| Enterprise | Enterprise | Legal entity / company |
| Site | Site | Physical facility / plant |
| Area | Area | Logical or physical zone within the site |
| **Work Center** | **Process Cell** (batch) / **Production Line** (discrete) / **Production Unit** (continuous) | Smallest grouping that can independently produce a product |
| **Work Unit** | **Unit** | Major processing equipment within a work center |
| — | **Equipment Module** | Functional grouping of control modules |
| — | **Control Module** | Smallest individually-controllable element (valve, motor, sensor) |

ISA-88 goes deeper into the equipment than ISA-95 does. In practice, you use ISA-95 for the upper layers and ISA-88 for the lower layers.

---

## The Ketchup Plant Mapped to the Hierarchy

```
Enterprise: KetchupCo Industrial
│
└── Site: Springfield Plant
    │
    ├── Area: Sauce Production
    │   │
    │   ├── Process Cell: Batch Production Line 1
    │   │   │   (the smallest grouping that produces one batch of ketchup)
    │   │   │
    │   │   ├── Unit: Premix Vessel MX-201
    │   │   │   ├── Equipment Module: Ingredient Dosing System
    │   │   │   │   ├── Control Module: Water Inlet Valve  XV-201
    │   │   │   │   ├── Control Module: Paste Inlet Valve  XV-202
    │   │   │   │   ├── Control Module: Vinegar Inlet Valve XV-203
    │   │   │   │   ├── Control Module: Flow Transmitter   FIT-201
    │   │   │   │   └── Control Module: Weight Transmitter WIT-201
    │   │   │   ├── Equipment Module: Agitator Drive AGIT-201
    │   │   │   │   ├── Control Module: Motor Run   AGIT201_RUN
    │   │   │   │   ├── Control Module: Speed       AGIT201_SPD
    │   │   │   │   ├── Control Module: Current     AGIT201_AMPS
    │   │   │   │   └── Control Module: Vibration   AGIT201_VIB
    │   │   │   ├── Equipment Module: Vessel Instrumentation
    │   │   │   │   ├── Control Module: Temperature TIT-201
    │   │   │   │   └── Control Module: Level       LIT-201
    │   │   │   └── Equipment Module: Discharge System
    │   │   │       └── Control Module: Discharge Valve XV-204
    │   │   │
    │   │   ├── Unit: Cook Kettle KT-301
    │   │   │   ├── Equipment Module: Steam Heating System
    │   │   │   │   ├── Control Module: Steam Control Valve TCV-301
    │   │   │   │   ├── Control Module: Steam Isolation     SV301
    │   │   │   │   ├── Control Module: Steam Flow          FIT-302
    │   │   │   │   └── Control Module: Jacket Pressure     PT-301_JKT
    │   │   │   ├── Equipment Module: Scraped-Surface Agitator
    │   │   │   │   ├── Control Module: Motor Run   AGIT301_RUN
    │   │   │   │   ├── Control Module: Speed       AGIT301_SPD
    │   │   │   │   └── Control Module: Current     AGIT301_AMPS
    │   │   │   ├── Equipment Module: Vessel Instrumentation
    │   │   │   │   ├── Control Module: Product Temperature  TIT-301
    │   │   │   │   ├── Control Module: Bottom Temperature   TIT-302
    │   │   │   │   ├── Control Module: Vessel Pressure      PIT-301
    │   │   │   │   ├── Control Module: Level                LIT-301
    │   │   │   │   └── Control Module: Inline pH            AIT-301
    │   │   │   └── Equipment Module: Pasteurization Hold Logic
    │   │   │       └── Control Module: Hold Timer KT301_HOLD_TMR
    │   │   │
    │   │   ├── Unit: Vacuum Deaerator DA-401
    │   │   │   ├── Equipment Module: Vacuum Pump Skid
    │   │   │   │   ├── Control Module: Pump Run    401-VP-001_RUN
    │   │   │   │   ├── Control Module: Pump Fault  401-VP-001_FAULT
    │   │   │   │   ├── Control Module: Pump Amps   401-VP-001_AMPS
    │   │   │   │   └── Control Module: Vacuum Level 401-PI-001
    │   │   │   ├── Equipment Module: Product Flow
    │   │   │   │   ├── Control Module: Flow         401-FI-001
    │   │   │   │   └── Control Module: Temperature  401-TI-001
    │   │   │   └── Equipment Module: Vessel Level
    │   │   │       └── Control Module: Level        401-LI-001
    │   │   │
    │   │   ├── Unit: Homogenizer HG-401
    │   │   │   ├── Equipment Module: High-Pressure Pump
    │   │   │   │   ├── Control Module: Run     HOMOG_401_RUN
    │   │   │   │   ├── Control Module: Fault   HOMOG_401_FAULT
    │   │   │   │   ├── Control Module: Amps    HOMOG_401_AMPS
    │   │   │   │   └── Control Module: PackML State HOMOG_401_OP_STATE
    │   │   │   └── Equipment Module: Homogenizing Valve
    │   │   │       ├── Control Module: 1st Stage Pressure HOMOG_401_PRESS
    │   │   │       ├── Control Module: 2nd Stage Pressure HOMOG_401_PRESS2
    │   │   │       └── Control Module: Valve Run Hours    HOMOG_401_VLV_HRS
    │   │   │
    │   │   ├── Unit: Buffer Tank BT-451
    │   │   │   ├── Equipment Module: Vessel Instrumentation
    │   │   │   │   ├── Control Module: Level       BT451_LVL
    │   │   │   │   └── Control Module: Temperature BT451_TEMP
    │   │   │   ├── Equipment Module: Slow Agitator
    │   │   │   │   └── Control Module: Run BT451_AGIT_RUN
    │   │   │   └── Equipment Module: Inlet/Outlet Valves
    │   │   │       ├── Control Module: Inlet Valve  BT451_VLV_IN
    │   │   │       └── Control Module: Outlet Valve BT451_VLV_OUT
    │   │   │
    │   │   ├── Unit: Buffer Tank BT-452  (same EM/CM structure as BT-451)
    │   │   └── Unit: Buffer Tank BT-453  (same EM/CM structure as BT-451)
    │   │
    │   └── Process Cell: CIP Station 1
    │       └── Unit: CIP Skid
    │           ├── Equipment Module: CIP Supply Pump
    │           │   ├── Control Module: Pump Run    CIP_P701_RUN
    │           │   └── Control Module: Pump Amps   CIP_P701_AMPS
    │           ├── Equipment Module: CIP Solution Conditioning
    │           │   ├── Control Module: Temperature CIP_TIT_701
    │           │   ├── Control Module: Conductivity CIP_CIT_701
    │           │   └── Control Module: Flow         CIP_FIT_701
    │           └── Equipment Module: CIP Sequence Logic
    │               ├── Control Module: Step    CIP_STEP
    │               ├── Control Module: Timer   CIP_TIMER
    │               └── Control Module: Phase   CIP_PHASE
    │
    ├── Area: Packaging
    │   │
    │   └── Production Line: Packaging Line 1
    │       │   (NOTE: ISA-88 calls this a "Production Line" not a "Process Cell"
    │       │    because the operation is discrete/repetitive, not batch)
    │       │
    │       ├── Work Unit: Filler FL-501
    │       │   ├── Equipment Module: Fill Heads (12-station rotary)
    │       │   │   ├── Control Module: Current Fill Weight FL_501_WEIGHT
    │       │   │   ├── Control Module: Head Pressure       FILLER_HeadPress_01
    │       │   │   └── Control Module: Product Temperature FL_501_PROD_TEMP
    │       │   ├── Equipment Module: Production Counters
    │       │   │   ├── Control Module: Good Count   FL_501_GOOD_CNT
    │       │   │   ├── Control Module: Reject Count FL_501_REJECT_CNT
    │       │   │   ├── Control Module: Total Count  FL_501_TOTAL_CNT
    │       │   │   └── Control Module: Yield        FL_501_YIELD
    │       │   ├── Equipment Module: SPC Statistics
    │       │   │   ├── Control Module: Weight Average FL_501_WT_AVG
    │       │   │   └── Control Module: Weight StdDev FL_501_WT_STDDEV
    │       │   └── Equipment Module: PackML State Machine
    │       │       ├── Control Module: Op State     FL_501_OP_STATE
    │       │       ├── Control Module: Op State Text FL_501_OP_STATE_TXT
    │       │       └── Control Module: State Duration FL_501_STATE_DUR
    │       │
    │       ├── Work Unit: Capper CAP-501
    │       │   ├── Equipment Module: Cap Application Head
    │       │   │   ├── Control Module: Status  CAP_501_STS
    │       │   │   ├── Control Module: Torque  CAP_501_TORQUE
    │       │   │   └── Control Module: Counter CAP_501_CNT
    │       │   ├── Equipment Module: Cap Feeder
    │       │   │   └── Control Module: Jam     CAP_501_JAM
    │       │   └── Equipment Module: PackML State Machine
    │       │       ├── Control Module: Op State     CAP_501_OP_STATE
    │       │       └── Control Module: State Duration CAP_501_STATE_DUR
    │       │
    │       ├── Work Unit: Inversion Tunnel
    │       ├── Work Unit: Cooling Tunnel
    │       ├── Work Unit: Conveyor CONV-501
    │       ├── Work Unit: Labeler LBL-601
    │       ├── Work Unit: Date Coder DC-601
    │       ├── Work Unit: Inspector XR-601
    │       ├── Work Unit: Case Packer CP-601
    │       └── Work Unit: Palletizer PAL-601
    │
    ├── Area: Raw Materials Storage
    │   │
    │   ├── Work Center: Liquid Ingredient Storage
    │   │   ├── Work Unit: Tomato Paste Tank 1 (TK-101)
    │   │   ├── Work Unit: Tomato Paste Tank 2 (TK-102)
    │   │   └── Work Unit: Vinegar Tank
    │   │
    │   └── Work Center: Dry Ingredient Storage
    │       ├── Work Unit: Sugar Silo
    │       ├── Work Unit: Salt Bin
    │       └── Work Unit: Spice Hopper
    │
    └── Area: Utilities
        │
        ├── Work Center: Steam System
        │   └── Work Unit: Boiler #1
        │
        ├── Work Center: Cooling Water System
        │   ├── Work Unit: Cooling Tower
        │   └── Work Unit: Cooling Water Pumps
        │
        ├── Work Center: Compressed Air System
        │   └── Work Unit: Air Compressor
        │
        └── Work Center: Hot Water System
            └── Work Unit: HW Heat Exchanger
```

---

## Key Modeling Decisions

### Process Cell vs Production Line vs Production Unit

ISA-88 distinguishes three types of "smallest production grouping":

| ISA-88 Type | Use For | Our Plant |
|-------------|---------|-----------|
| **Process Cell** | Batch manufacturing — product made in discrete batches | Areas 100–450 (Batch Production Line 1) |
| **Production Line** | Discrete / repetitive manufacturing — units of identical product | Areas 500–600 (Packaging Line 1) |
| **Production Unit** | Continuous manufacturing — steady-state flow | (None in this plant) |

**Why it matters**: A Process Cell uses ISA-88 batch recipes. A Production Line uses the **PackML state model** (which is exactly what we added — the `*_OP_STATE` tags). They are governed by different sub-standards (ISA-88.01 vs ISA-TR88.00.02).

In our plant, the **buffer tanks (BT-451/452/453)** sit at the boundary between the Process Cell (batch) and the Production Line (discrete). This is the famous "batch-to-continuous" interface and is why we modeled them as a key architectural element.

### Why CIP is its own Process Cell

CIP is modeled as a separate Process Cell, not as an Equipment Module of the cook kettle. This is correct because:
- CIP runs its own recipes (cleaning sequences) independent of product recipes
- CIP cycles tie up multiple Units (premix, kettle, deaerator, homogenizer, buffer tanks) at the same time
- CIP has its own batch records and validation requirements

### Why Raw Materials and Utilities use only Work Centers, not Process Cells

These areas don't produce product — they store/supply it. They have Work Units (tanks, silos, boilers) but no recipes execute against them. ISA-95 still applies (Area → Work Center → Work Unit) but ISA-88 does not.

### Tomato Paste Tanks — Area or Process Cell?

Could go either way. We model them as **Work Units in the Raw Materials Area**, not as Units inside Batch Production Line 1, because:
- They serve multiple downstream consumers (could feed multiple cook lines if there were more)
- They have their own management (inventory, lot tracking, FIFO)
- Their state is independent of any specific batch in flight

In a plant with multiple production lines, this distinction becomes critical for recipe portability.

---

## Three Different Hierarchies, Three Different Uses

Real plants typically maintain **three parallel hierarchies** because each serves a different audience:

### 1. Physical / Plant Layout Hierarchy

How the equipment is physically arranged. This is what our **current OPC UA address space** (Area100_RawMaterials, Area200_PreMix, etc.) reflects. It maps directly to:
- P&IDs and engineering drawings
- Plant walks and operator rounds
- Maintenance work orders ("the leak is in Area 300")
- DCS HMI screens organised by physical proximity

**Audience**: Operators, maintenance, plant engineering.

### 2. ISA-95 / ISA-88 Logical Hierarchy

How equipment relates functionally to the production process. This is what this document describes. It maps to:
- MES recipe management
- Batch records and genealogy
- OEE rollups (machine → line → area → site)
- Corporate reporting and benchmarking

**Audience**: MES, ERP, corporate IT, quality, production scheduling.

### 3. Network / IP Hierarchy

How equipment is connected on the OT network. PLCs, DCS controllers, switches, VLANs. This is what your network engineers care about, governed by:
- Purdue Reference Model levels (0-5)
- IEC 62443 zones and conduits
- Cybersecurity segmentation

**Audience**: OT cybersecurity, network engineering.

A digitalisation project usually has to **map between these three** somehow. The translation tables that connect physical tag names to logical equipment are themselves a deliverable.

---

## How to Implement This in OPC UA

There are three main approaches, each with trade-offs:

### Option A: Reorganise the Address Space to Match ISA-95/88

Rebuild the OPC UA folder structure to match the hierarchy in this document. Browse path becomes:

```
Objects/
  KetchupCo/
    Springfield/
      SauceProduction/
        BatchLine1/
          MX201/
            DosingSystem/
              XV-201
              XV-202
              ...
            AgitatorDrive/
              AGIT201_RUN
              AGIT201_SPD
              ...
```

**Pros**: Pure standards alignment. MES integration is straightforward. Recipe management can address equipment by logical role.

**Cons**: Loses the area-based view. Operators used to seeing "TIT-301 is in Area 300" have to hunt deeper. Tag browse paths get long.

### Option B: Keep Physical Layout, Add Logical Hierarchy as Aliases/References

Keep the current `Area100_RawMaterials/` etc. structure, and add a parallel `LogicalHierarchy/` folder that uses OPC UA References to point at the same underlying nodes.

```
Objects/
  Area100_RawMaterials/        (physical view)
    PasteTank1/
      PASTE_TK1_LVL
  Area200_PreMix/
    Mixer_MX201/
      TIT-201

  KetchupCo/                    (logical view - references only)
    Springfield/
      SauceProduction/
        BatchLine1/
          MX201/
            VesselInstrumentation/
              ─► (references TIT-201 in Area200_PreMix)
```

**Pros**: Best of both worlds. Same underlying data, different views for different audiences. This is what mature plants typically end up doing.

**Cons**: More complex address space. Requires OPC UA References (which not all clients display well).

### Option C: Keep Physical Layout, Map in MES

Don't change OPC UA at all. Do the physical-to-logical mapping in the MES / data platform layer (Ignition, AVEVA, AspenTech, custom).

**Pros**: Minimal changes to the OT system. Mapping can be edited freely in the IT layer without touching the DCS.

**Cons**: The mapping becomes a "shadow data model" maintained outside the source system. Two sources of truth. Drift risk.

In our simulator, **Option C is the most realistic** — it's how brownfield plants actually do this. The OPC UA server reflects the physical/historical layout, and the contextualisation happens upstream in the MES or analytics platform. The work product of digitalisation is often *exactly* this mapping layer.

---

## Naming Conventions

ISA-95 and ISA-88 don't mandate naming, but most implementations follow:

| Level | Convention | Example |
|-------|-----------|---------|
| Enterprise | Company name | `KetchupCo` |
| Site | Geographic identifier | `Springfield` |
| Area | Functional area name | `SauceProduction`, `Packaging` |
| Process Cell / Production Line | Line designation | `BatchLine1`, `PackLine1` |
| Unit / Work Unit | Equipment tag with role | `MX-201` (Mixer), `KT-301` (Kettle) |
| Equipment Module | Functional grouping | `DosingSystem`, `AgitatorDrive` |
| Control Module | Tag from physical instrument | `XV-201`, `TIT-201` |

**Key rule**: Equipment Module and Control Module names should describe **function**, not physical model number. The function ("pH control loop") doesn't change if you swap out the vendor; the model number does.

---

## Recipe Implications

Once you have the ISA-88 hierarchy in place, recipes can be written **portably**:

```
Master Recipe: Standard Ketchup 14oz
  Procedure: Make Ketchup
    Unit Procedure: Pre-Mix
      Operation: Dose Ingredients
        Phase: Add Water (target: WATER_TARGET kg)
        Phase: Add Paste (target: PASTE_TARGET kg)
        ...
      Operation: High-Shear Mix
        Phase: Mix at HIGH_SHEAR_RPM for MIX_DURATION
    Unit Procedure: Cook
      Operation: Heat to TEMP
        Phase: Ramp steam valve via PID
      Operation: Pasteurization Hold
        Phase: Hold at TEMP for HOLD_TIME
    ...
```

The recipe references logical concepts (`PreMix`, `Cook`, `Dose`) not physical tags (`TIT-301`). At execution time the Batch Engine maps these to the actual equipment available in the chosen Process Cell. If the plant has Batch Line 1 and Batch Line 2 with different equipment, the **same recipe runs on both** — just bound to different physical units.

This is why the ISA-88 hierarchy matters even if you never reorganise the OPC UA tags themselves: it's the layer that makes recipes portable, batches comparable, and OEE meaningful across sites.

---

## Quick Reference: Which Standard for What?

| Question | Standard |
|----------|----------|
| "How is the company organised?" | ISA-95 (Enterprise → Site → Area) |
| "How do I structure a batch recipe?" | ISA-88 (Procedure → Unit Procedure → Operation → Phase) |
| "How should the packaging machines report state?" | ISA-TR88.00.02 (PackML) |
| "How do I integrate MES with the plant floor?" | ISA-95 Part 2 (object models) and Part 5 (B2MML) |
| "What's the smallest controllable element?" | ISA-88 Control Module |
| "What's the smallest grouping that produces a batch?" | ISA-88 Process Cell |
| "How do I track a batch from raw material to finished pallet?" | ISA-88 batch records + ISA-95 material traceability |
| "How do I segment my OT network for security?" | ISA/IEC 62443 (separate from 95/88 but complementary) |

---

## Summary

For the ketchup plant simulator:

1. **Enterprise / Site / Area** levels are common to both standards and easy to apply.
2. **Sauce Production Area** contains a **Process Cell** (Batch Production Line 1) for batch ketchup making, plus a separate **CIP Process Cell**.
3. **Packaging Area** contains a **Production Line** (not a Process Cell — discrete, not batch). Its equipment use **PackML state**.
4. **Raw Materials and Utilities Areas** use ISA-95 Work Centers/Work Units only — no batch recipes execute there.
5. **Buffer tanks** are the architectural seam between the batch Process Cell and the discrete Production Line.
6. The current OPC UA address space is a **physical layout view**. The ISA-95/88 hierarchy is a **logical view**. Real plants typically have both, with the mapping done either in the MES or as references in the OPC UA address space.

The value of the standard is not in the diagram itself — it's in giving you a vocabulary that scales from one plant to a hundred plants, lets recipes move between sites, and makes KPI rollups comparable across the enterprise.
