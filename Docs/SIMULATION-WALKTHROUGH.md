# Ketchup Plant Simulation — Step-by-Step Walkthrough

A high-level narrative of what actually happens inside the simulator: how the plant runs 24/7, what each piece of equipment does, how a single batch flows end-to-end, and how batches are tracked and recorded.

## Overview

The simulator models a **ketchup manufacturing plant** consisting of:

- **One batch production line** (Areas 100–450): dose → pre-mix → cook → deaerate → homogenize → buffer
- **One continuous filling + packaging line** (Areas 500–600): fill → cap → cool → label → inspect → case → palletize
- **One CIP (Clean-in-Place) system** (Area 700): automated cleaning between production runs
- **Plant utilities** (Area 800): steam, cooling water, compressed air, hot water

Everything runs 24/7 at an accelerated clock speed (default 60x — 1 real second = 60 sim seconds, so 24 sim hours pass in 24 real minutes). All process data is exposed via an **OPC UA server** on port 26543, mimicking how a real DCS (Distributed Control System) would present data to upstream systems.

## The Plant Clock

The simulation uses a `SimClock` that starts at a configurable sim time (default: next Monday 06:00) and advances at `SIM_SPEED` times real time. Every 100 ms of real time, the engine ticks all components in deterministic priority order. At 60x:

- 1 tick (100 ms real) = 6 seconds sim time
- 1 minute real = 1 hour sim time
- 1 hour real = 60 hours sim time (2.5 sim days)

This is the master clock that drives every state machine, every physics update, every tag publication.

## What's Always Running (the background)

Before we trace a single batch, it helps to know what's active continuously:

### Area 800 — Utilities

The utility systems are **always running** and act as services to the rest of the plant:

- **Boiler** maintains steam header at ~6 bar, 165°C. Responds to demand from consumers. If heavy demand pushes it past 50% capacity, the header pressure droops slightly (realistic boiler behavior).
- **Cooling water** circulates at 8°C supply temperature. Pump 2 auto-stages on when demand exceeds 60%. Tower fans stage similarly.
- **Compressed air** stays at 6.5 bar (for valve actuators and pneumatic devices).
- **Hot water** at 82°C is available for CIP make-up.

### Area 700 — CIP

The CIP controller runs on a schedule — by default every 45 min sim time. When a cycle fires, it walks through a 7-step sequence: **Pre-Rinse → Caustic Wash → Intermediate Rinse → Acid Wash → Final Rinse → Sanitize → Drain**. Each step has its own duration, temperature target, and conductivity profile (the most important QC parameter in CIP). The full cycle takes ~75 min sim time.

### Area 100 — Raw Material Storage

Paste tanks, vinegar tank, sugar silo, salt bin, spice hopper, and municipal water supply. Nothing happens here on its own — Area 100 responds to draw requests from Area 200 when ingredient valves open. Tank levels drop as material is drawn. When a tank approaches empty, it auto-refills (simulating a tanker delivery).

### Area 450 — Buffer Tanks (3 of them)

Three insulated 5,000 L holding tanks (BT-451, BT-452, BT-453) sit between batch production and the filling line. Each tank has its own state — `Empty` / `Filling` / `Ready` / `Draining` — and transitions are driven by external activity:

- Area 400 pushes homogenized product in → a tank goes `Empty → Filling`
- Tank reaches 90% full or Area 400 stops pushing → `Filling → Ready`
- Area 500 starts drawing → `Ready → Draining`
- Tank empties → `Draining → Empty`

This is the **batch-to-continuous bridge**: the three tanks smooth out the batch-production rhythm into continuous filler supply.

## The Plant's 24/7 Rhythm

With no user intervention, the plant self-organizes into a repeating production rhythm:

```
┌─────────────────────────────────────────────────────────────────┐
│  Every 25-30 minutes: a new batch starts in Area 200           │
│  Every 2-3 hours:     a batch completes through Area 400       │
│  Every 45 minutes:    CIP fires                                 │
│  Every 100 ms real:   all physics + OPC UA tag updates         │
└─────────────────────────────────────────────────────────────────┘
```

Areas 500 and 600 (filling + packaging) run continuously whenever buffer tanks have product above 82°C. They stop (report `Starved`) when buffers are empty, and resume when fresh product arrives.

Because the cook kettle (Area 300) is the bottleneck — it takes ~2.5 hours per batch for fill → heat → hold → cool → discharge — Area 200 often holds in its `Discharging` state waiting for the kettle to clear. This is **realistic back-pressure**: when downstream can't receive, upstream waits.

---

# A Single Batch: Step-by-Step Walkthrough

Let's follow one batch end-to-end: **B-20260420-001**, starting at sim time 06:00:30 on Monday morning.

## Step 1 — New Batch Starts (Area 200 Pre-Mix)

**Equipment**: MX-201 (3,500 L high-shear Breddo Likwifier), AGIT-201 (75 kW VFD agitator), inlet valves XV-201/202/203/204

At sim time 06:00:30, the Area 200 state machine transitions from `Idle` to `FillingWater`. Simultaneously, the **BatchManager** detects this transition and:

1. Generates a new batch ID: `B-20260420-001`
2. Creates an in-memory `BatchRecord` object
3. Writes `BATCH_ID = "B-20260420-001"` to OPC UA
4. Starts a `PhaseRecord` for the first phase: `PreMix.DoseWater`
5. Begins collecting report parameters (sampling `FIT-201`, `H2O_FLOW`, etc. each tick)

## Step 2 — Ingredient Dosing (Area 200)

The state machine walks through four dosing phases, each pulling ingredients from Area 100:

### 2a. FillingWater (0:00 → 4:24 sim time, ~264 sec)
- Valve `XV-201` opens.
- Each tick, Area 200 calls `area100.DrawWater(volume)` with the tick's required volume.
- Area 100 increments its water totalizer (`H2O_TOTALIZER`) and reports the flow (`H2O_FLOW`).
- Area 200 adds the drawn water (at 15°C) to MX-201 — mixer level (`LIT-201`) climbs.
- Phase ends when 630 L cumulative has been dosed.

**Phase record captured**: `endLevelL`, `endTempC`, `avgInflowLpm`

### 2b. DosingPaste (4:24 → 16:30, ~726 sec)
- Valve `XV-202` opens, Area 100's paste pump P-101 starts running.
- Each tick, Area 200 calls `area100.DrawPaste(kg)`.
- Area 100 decrements `PASTE_TK1_WT` by the drawn mass; `P101_FLOW` reports rate.
- Paste (at 18°C) flows into mixer — level climbs, temperature shifts via mass-weighted mixing.
- Phase ends when 945 kg cumulative paste has been dosed.

**Phase record captured**: `avgPumpCurrentA`, `endLevelL`, `endWeightKg`

### 2c. DosingVinegar (16:30 → 23:00, ~390 sec)
- Valve `XV-203` opens, Area 100 reports vinegar flow.
- Ends when 378 L dosed.

**Phase record captured**: `endLevelL`

### 2d. RampingAgitator (23:00 → 23:24, ~24 sec)
- Agitator motor AGIT-201 starts and ramps speed 0 → 1500 RPM.
- Load factor (`LoadFactor`) computed from mixer fill fraction — a 95%-full mixer has a high load.
- Motor current (`AGIT201_AMPS`) climbs as speed × load.

## Step 3 — High-Shear Mixing (Area 200)

**Phase**: `Mixing` (23:24 → 26:24, 180 sec)

With the agitator at full speed, viscous dissipation heats the product (~30% of motor power converts to heat in the product). The product temperature creeps up a few degrees over the 3-minute mixing phase. This is exactly what happens in a real high-shear mixer.

**Phase record captured**:
- `avgPowerKw: 52.41`, `maxPowerKw: 53.4`
- `avgTorqueNm: 333.61`
- `avgCurrentA: 51.4`
- `maxVibrationMmS: 0.85`
- `finalTempC`, `finalLevelL`

## Step 4 — Discharge (Area 200 → Area 300)

**Phases**: `RampingDown` (24 sec) → `Discharging` (~462 sec)

Agitator ramps down to stop, then discharge valve `XV-204` opens. Area 200 enters the `Discharging` state and **stops the batch state machine there** — it waits passively.

Meanwhile, Area 300 (which has been in `Idle` state) is continuously checking whether upstream product is available via `area200.HasProductAvailable`. The moment Area 200 enters `Discharging`, Area 300 detects it and transitions its own state to `Filling`.

This is a **synchronized handoff**:

- Area 300 `Idle → Filling` at 06:27:12
- Area 200 `RampingDown → Discharging` at the same tick
- BatchManager writes console log: `[BATCH] B-20260420-001: MX-201 -> KT-301`
- BatchRecord adds KT-301 to `EquipmentUsed`

For the next ~8 minutes, product flows from the mixer to the kettle at ~250 L/min. Each tick, Area 300's filling logic calls `area200.DrawForTransfer(volume)`, which removes material from MX-201 and returns it with its temperature. Area 300 adds the received material to KT-301 with mass-weighted temperature mixing.

When MX-201's level drops to near zero, Area 200 transitions `Discharging → Idle` and can start the **next batch** — which is exactly what happens: at 06:35:18, Batch 2 starts in Area 200 while Batch 1 is still in Area 300.

## Step 5 — Heat to 90°C (Area 300 Cook, PID Controlled)

**Phase**: `HeatingUp` (06:34:54 → 07:52:54, ~78 min sim time)

**Equipment**: KT-301 (3,300 L jacketed cook kettle), AGIT-301 (15 kW scraped-surface agitator), steam control valve TCV-301, steam isolation SV301

This is the most interesting phase — **the first closed-loop PID control** in the simulation.

1. Steam isolation valve `SV301` opens.
2. The PID controller (Kp=5, Ki=0.2, Kd=0.1) compares the kettle temperature (`TIT-301`) to the setpoint (90°C). With 68°C of error at the start, the proportional term drives the output to 100% — the steam valve (`TCV-301`) opens fully.
3. Each tick, Area 300 computes the actual steam flow: `steam_kgh = MaxSteamFlow × valve_fraction × (header_pressure / 6)`. This is where the Area 300 ↔ Area 800 coupling matters: if the boiler is under heavy load, the header pressure drops slightly and less steam gets delivered.
4. Area 300 calls `utilities.RequestSteam("KT-301", steam_kgh)` — Area 800 sums this demand with any other consumers, computes total steam use, updates boiler firing and efficiency.
5. The steam flow is converted to heat input: `heat_kw = steam_kgh × 2163 kJ/kg ÷ 3600 × 0.75` (where 0.75 is heat transfer efficiency). For 300 kg/h at full valve, that's ~135 kW of heat into the kettle.
6. Each tick, the kettle's `ExternalHeatInputKw` is set, and in `Vessel.Tick()` the temperature rises by `heat × dt ÷ (mass × specific_heat)`. At 1902 L × 1.08 kg/L × 3.5 kJ/K/kg, that's ~0.086°C per tick at full steam.

As the temperature approaches the setpoint, the PID output tapers (proportional band narrows). Real kettles exhibit this exact "asymptotic approach" — heating gets slower as you approach target. Total heat-up time: ~78 min sim from 18°C to 88°C.

**Phase record captured**:
- `peakTempC: 88`
- `avgSteamValvePct: 80.76` (PID averaged 81% open during heat-up)
- `steamUsedKg: 314.37` (integrated from `FIT-302` over the phase — **real energy consumption**)
- `peakJacketPressBar: 6.05`

## Step 6 — Pasteurization Hold (Area 300)

**Phase**: `Holding` (07:52:54 → 07:57:54, exactly 300 sec)

Once the kettle reaches 88°C, the `Holding` phase starts. The PID continues to maintain temperature at 90°C by modulating the steam valve. The `KT301_HOLD_TMR` tag counts up from 0 to 300 seconds.

Critical detail: the hold timer **only accumulates while the temperature is actually above 88°C**. If the temperature dropped (e.g., if steam supply failed), the timer would pause — matching real pasteurization integrity rules. In this batch, it held fine.

**Phase record captured**:
- `maxTempC: 89, minTempC: 88, avgTempC: 88.49` — temperature stability during the hold
- `avgPhInline: 3.73` — inline pH measured during the hold (within the 3.6-3.8 target range)
- `holdTimerFinalSec: 300` — pasteurization integrity confirmed
- `steamDuringHoldKg: 4.32` — steam used to maintain hold temperature

## Step 7 — Cooling (Area 300)

**Phase**: `Cooling` (07:57:54 → 08:30:42, ~33 min sim time)

Steam isolation closes. Area 300 requests cooling water from Area 800 (`utilities.RequestCooling("KT-301", 15.0)`) and applies negative heat input to the kettle (`ExternalHeatInputKw = -12`). Temperature decays from 88.9°C toward the transfer target of 85°C.

**Phase record captured**: `startTempC: 88.9`, `endTempC: 85.1`

## Step 8 — Transfer to Deaerator (Area 300 → Area 400)

**Phase**: `Discharging` (08:30:42 → 08:38:24)

Same pattern as the Area 200 → Area 300 handoff:

- Area 300 enters `Discharging` and waits
- Area 400 (which was idle) detects Area 300 has product available, transitions `Idle → Filling`
- BatchManager logs: `[BATCH] B-20260420-001: KT-301 -> DA-401/HG-401`
- BatchRecord adds DA-401, HG-401 to `EquipmentUsed`

For the next 8 minutes, the transfer pump moves product from KT-301 (at 85°C) into DA-401 at 250 L/min. When the kettle empties, Area 300 returns to `Idle` and can accept Batch 2 — which by this point has finished cooking and is ready.

## Step 9 — Vacuum Deaeration (Area 400)

**Equipment**: DA-401 (2,500 L vacuum deaerator vessel), 401-VP-001 (liquid ring vacuum pump), HG-401 (high-pressure homogenizer)

Two sub-phases:

### 9a. PullingVacuum (08:38:24 → 08:39:18, 54 sec)
- Vacuum pump starts running, pulling the vessel down from atmospheric pressure (0 mmHg gauge) toward the 700 mmHg setpoint.
- Each tick, `VacuumSystem.UpdateRunning()` increases vacuum at the pump-down rate (20 mmHg/sec) minus the natural leak (1.5 mmHg/sec).
- Vacuum reaches 98% of setpoint in ~54 seconds.

**Phase record captured**: `peakVacuumMmHg: 741`, `finalVacuumMmHg: 732`

### 9b. Deaerating (08:39:18 → 08:42:18, 180 sec)
- The vessel holds under vacuum for 3 minutes. Flash evaporation removes dissolved oxygen from the product (not actually simulated in the chemistry sense, but the pressure dynamics are).

**Phase record captured**: `minVacuumMmHg: 698`, `avgVacuumMmHg: 713.73`

## Step 10 — Homogenization (Area 400)

**Phase**: `Homogenizing` (08:42:18 → 09:05:54, ~24 min sim time)

This is where the product physically moves out of Area 400:

1. HG-401 (high-pressure homogenizer) starts. Its `Homogenizer.UpdateRunning()` simulates the 200 bar operating pressure with realistic ±2 bar piston noise.
2. Transfer pump 401-P-001 starts, pumping product from DA-401 through the homogenizer at 80 L/min.
3. Each tick, Area 400 calls `area450.AcceptProduct(volume, temp)`. Area 450 picks a buffer tank:
   - Prefers a tank already in `Filling` state (to keep a single tank filling rather than spreading across multiple)
   - Otherwise picks an `Empty` tank and starts filling it
   - Returns 0 if all tanks are full/draining — in which case Area 400 waits
4. BT-451 (in this batch's case) transitions `Empty → Filling` the moment homogenization starts.

**Valve wear accumulates here**: `HOMOG_401_VLV_HRS` increases by `dt` while running. This is the key wear indicator — real homogenizer valve sets need replacement every ~1000 run hours, and the 200 bar pressure slowly degrades as hours accumulate.

**Phase record captured**:
- `avgPressureBar: 197.46`, `maxPressureBar: 202`, `minPressureBar: 0.1` (0 during brief startup)
- `avgMotorCurrentA: 51.37`
- `valveHoursAtStart: 0`, `valveHoursAtEnd: 0.39` — **real wear tracking captured in the batch record**

## Step 11 — Vent Vacuum and Complete (Area 400)

**Phase**: `VentingVacuum` (09:05:54 → 09:13:36, ~462 sec)

- Vacuum pump stops. Natural leak bleeds the vessel back toward atmospheric at 1.5 mmHg/sec.
- When vacuum drops below 50 mmHg, the area transitions to a brief `Draining` state, then back to `Idle`.
- **Critical moment**: Area 400 `Draining → Idle` triggers the BatchManager's **completion logic**.

At 09:13:54 sim time, the BatchManager:
1. Sets `batch.Record.EndTime = simNow`
2. Sets `batch.Record.Status = Complete`
3. Increments `BATCHES_TODAY`
4. Serializes the entire `BatchRecord` to JSON
5. Writes it to `/app/batch-records/B-20260420-001.json`
6. Logs: `[BATCH] B-20260420-001: COMPLETE (duration=193.4min, phases=15)`

**The batch record now exists as a persistent artifact** containing 15 phase records with all captured report parameters. This IS the Electronic Batch Record (EBR) — the kind of artifact a regulated food plant would keep for audit.

## Step 12 — Buffer Residency (Area 450)

At this point the product from Batch 1 is sitting in BT-451 as ~1902 L of homogenized ketchup at ~85°C. The tank transitioned `Filling → Ready` around the time homogenization completed.

**The batch is "complete" from a production standpoint** but its physical product is still in the plant, waiting to be filled into bottles. In real plants this buffer residency is often tracked separately ("hot hold time") for food safety reasons — the sim doesn't enforce a time limit but records show the buffer state.

## Step 13 — Filling Line (Area 500)

**Equipment**: FL-501 (12-head rotary piston filler), CAP-501 (rotary capper), inversion tunnel (2 temp zones), cooling tunnel (3 zones: 82°C → 55°C → 35°C), CONV-501 (conveyor)

The filling line runs **continuously** whenever any buffer tank has hot product. It's not driven by batch events — it's driven by the presence of product.

Each tick:
1. Area 500 checks `area450.TotalHotProductL()`. If < 50 L, transitions to `Starved` state and stops.
2. Otherwise, computes bottle production rate based on line speed (200 BPM target).
3. Draws the equivalent volume from Area 450 via `area450.DrawProduct(liters)`.
4. Calls `filler.FillBottles(dt)` which uses Gaussian random weights (mean 397g, stddev 2.5g + wear) to generate individual bottle fills.
5. Bottle weights below 388g or above 406g are rejected. Good bottles increment `FL_501_GOOD_CNT`; rejects increment `FL_501_REJECT_CNT`.

The filler computes and publishes:
- `FL_501_WEIGHT` — current fill weight (updates every bottle, ~3 Hz at 200 BPM)
- `FL_501_WT_AVG`, `FL_501_WT_STDDEV` — rolling 50-bottle SPC stats
- `FL_501_YIELD` — first-pass yield %
- PackML state tags: `FL_501_OP_STATE` (Running/Starved/Blocked), `FL_501_OP_MODE` (Production)

The inversion tunnel and cooling tunnel are simulated as temperature profiles that update based on whether product is flowing through them. No discrete bottle tracking — just the aggregate rate.

**Line-level tags** published:
- `PACK_LINE_OP_STATE` / `_TXT` — aggregate line state
- `PACK_LINE_BOTTLENECK` — which station is currently constraining throughput
- `BOTTLES_TODAY` — cumulative counter
- `PACK_LINE_SPEED` — current BPM
- `PACK_LINE_OEE_AVAIL` / `_PERF` / `_QUAL` / `_OVERALL` — **computed OEE** from actual runtime vs downtime and production vs theoretical

## Step 14 — Packaging Line (Area 600)

**Equipment**: LBL-601 (labeler), DC-601 (date coder), XR-601 (X-ray inspector), MD-601 (metal detector), CP-601 (case packer), PAL-601 (palletizer)

Area 600 runs in lock-step with Area 500. Each tick it consumes `area500.BottlesProducedThisTick` and processes them through the stations:

1. **Labeler** increments label count for each bottle.
2. **Date coder** prints on each bottle.
3. **X-ray inspector** inspects each bottle; 0.1% random reject rate.
4. **Metal detector** trips on ~0.01% of bottles.
5. **Case packer** accumulates bottles into cases of 24. Each complete case increments `CP_601_CASES_CNT` and `CASES_TODAY`.
6. **Palletizer** accumulates cases into pallets of 60 (5 layers × 12 cases). Each complete pallet increments `PAL_601_PALLETS_CNT` and `PALLETS_TODAY`.

All stations report their own PackML state (mirroring the line state from Area 500 currently) and mode. In a future enhancement, a faulted station could propagate `Blocked` state upstream via `area500.SetDownstreamBlocked(true)`.

---

# What Happens in Parallel

While Batch 1 was working its way from Area 200 through Area 400, **other things were happening simultaneously**:

| At sim time | Event |
|-------------|-------|
| 06:35:18 | Batch 2 started in Area 200 (Batch 1 was still in KT-301 heating) |
| 08:38:42 | Batch 2 handed off MX-201 → KT-301 (Batch 1 had just moved to DA-401) |
| 08:46:48 | Batch 3 started in Area 200 |
| 08:10:06 | CIP cycle 2 started (every 45 min) |
| Continuously | Area 500 was filling bottles whenever BT-451 had product |
| Continuously | Area 600 was counting cases/pallets as the filler produced |
| Continuously | Area 800 was adjusting boiler firing based on Area 300's steam demand |
| Continuously | Area 100 was responding to Area 200's ingredient valve openings |

At 08:46:48, **three batches were simultaneously active** — one in each batch-side area. The BatchManager was tracking all three, each with its own phase tracker, its own report parameters, its own growing BatchRecord.

This **pipelined production** is what real plants do. The bottleneck (cook kettle) determines overall throughput; upstream areas work on the next batch while downstream is still processing the previous one.

---

# The Key Cross-Area Couplings

These are the interfaces between areas that make the simulation behave coherently rather than as 9 independent simulators:

| Coupling | Mechanism | What it models |
|----------|-----------|----------------|
| Area 100 → Area 200 | Area 200 calls `area100.DrawWater/DrawPaste/DrawVinegar(qty)` each tick when a valve is open | Ingredients actually leave their tanks |
| Area 200 → Area 300 | Area 300 calls `area200.DrawForTransfer(L)` each tick during its Fill phase; Area 200 implements `IProductSource` | Product physically moves between vessels with mass-weighted temperature mixing |
| Area 300 → Area 400 | Area 400 calls `area300.DrawForTransfer(L)` during its Fill phase | Same pattern |
| Area 400 → Area 450 | Area 400 calls `area450.AcceptProduct(L, temp)` during Homogenizing; Area 450 routes to an available tank | Product transferred to buffer tanks via the push model |
| Area 450 → Area 500 | Area 500 calls `area450.DrawProduct(L)` continuously | Filler consumes product as it produces bottles |
| Area 500 → Area 600 | Area 500 exposes `BottlesProducedThisTick` as a property; Area 600 reads and processes | Discrete bottle handoff |
| Area 300 → Area 800 | Area 300 calls `utilities.RequestSteam(kgh)` each tick; Area 800 sums demand and updates header pressure | Boiler demand coupling — high demand makes heat-up slower |
| Area 400 → Area 800 | Area 400 requests cooling water during cooling phases | Cooling coupling |

Everything else (BatchManager observing, tag publishing) is tag-based, not direct method calls.

---

# How Batches Are Tracked (The BatchManager)

The **BatchManager** runs as a dedicated simulation component at priority 50 — after the area controllers (which publish state tags at priority 40) but before the tag flusher (priority 90). This means each tick the BatchManager sees the latest state tags from the areas.

Its job each tick:

1. **Reads area state tags**: `MX201_STATE`, `KT301_STATE`, `DA401_STATE`.
2. **Detects transitions** by comparing current to last-tick state.
3. On Area 200 `Idle → FillingWater`, it **creates a new batch** with ID `B-YYYYMMDD-NNN`.
4. On Area 300 `Idle → Filling`, it **transfers the active batch** from Area 200 to Area 300.
5. On Area 400 `Idle → Filling`, it **transfers the active batch** from Area 300 to Area 400.
6. On Area 400 `Draining → Idle`, it **completes the batch** and writes the record to disk.
7. **Finalizes phase records** on every state exit, calling `Finalize()` on the active tracker.
8. **Starts new phase records** on every state entry, looking up the observer from the catalog.
9. **Samples tracker values** each tick — the aggregators (Peak, Min, Average, Integrate) accumulate the data.
10. **Publishes plant-level batch tags**: `BATCH_ID`, `BATCH_STATUS`, `BATCH_PHASE`, `BATCH_PHASE_TMR`, `MX201_BATCH_ID`, `KT301_BATCH_ID`, `DA401_BATCH_ID`, `CURRENT_BATCH_ID`, `BATCHES_TODAY`.

The "primary" batch for the plant-level tags is whichever one is furthest along — if there's a batch in Area 400, that's the primary; else Area 300; else Area 200.

## Report Parameters: How They're Collected

This is the clever part of Phase 4 Lite. The `PhaseObserverCatalog` is a static dictionary mapping `(areaName, stateName) → PhaseObserver`. Each observer declares:

- **Sampled tags with aggregation** — e.g., `"peakTempC" = ("TIT-301", Peak)` means "during this phase, sample `TIT-301` each tick and store the maximum".
- **Snapshot on entry** — tags to capture at phase start.
- **Snapshot on exit** — tags to capture at phase end.

When a phase begins, an `ActivePhaseTracker` is instantiated with its observer and the batch context. Each tick:

- `tracker.Sample(simNow, readTag)` walks the observer's sampled tags, reads current values from OPC UA, and updates the appropriate aggregator:
  - **Peak**: `max(current_max, new_value)`
  - **Min**: `min(current_min, new_value)`
  - **Average**: running sum and count for later division
  - **IntegratePerHour / IntegratePerMinute**: `accumulator += value × dt_fraction`
  - **Final / Initial**: last value / first value

When the phase ends, `tracker.Finalize(endTime, readTag)` computes final values from all aggregators and adds the snapshot-on-exit tags. The result is a `PhaseRecord` appended to the batch's growing record.

---

# What the OPC UA Client Sees

Connect any OPC UA client to `opc.tcp://localhost:26543/BatchPlantServer` and browse `Objects/`. You'll see:

- **9 area folders** (`Area100_RawMaterials` through `Area800_Utilities`) with realistic tag naming that varies by era/contractor
- **`PackLine/`** — aggregated packaging line OEE, bottle/case/pallet counts, PackML state
- **`BatchLine/`** — aggregated batch line metrics
- **`PlantStatus/`** — plant-wide status (shift, plant mode, sim time/speed)
- **`BatchControl/`** — **the live batch tracking tags**: `BATCH_ID`, `BATCH_STATUS`, `BATCH_PHASE`, `BATCH_PHASE_TMR`, `CURRENT_BATCH_ID`, `BATCHES_TODAY`, `BATCH_START_TIME`

**Per-area batch IDs** (newly added): `MX201_BATCH_ID`, `KT301_BATCH_ID`, `DA401_BATCH_ID` — show which batch is currently in which unit, enabling "where is batch X right now?" queries.

---

# The Batch Record Output

When a batch completes, a JSON file is written to `/app/batch-records/B-YYYYMMDD-NNN.json` inside the container. The structure:

```
{
  "BatchId": "B-20260420-001",
  "ProductCode": "SKU-KETCHUP-14OZ",
  "RecipeName": "Standard Ketchup 14oz",
  "StartTime": "2026-04-20T06:00:30Z",
  "EndTime": "2026-04-20T09:13:54Z",
  "Status": "Complete",
  "EquipmentUsed": ["MX-201", "KT-301", "DA-401", "HG-401"],
  "Phases": [
    {
      "PhaseLabel": "PreMix.DoseWater",
      "AreaName": "Area 200",
      "UnitName": "MX-201",
      "DurationSec": 264,
      "ReportParameters": { "endLevelL": 645, "endTempC": 15.1, "avgInflowLpm": 146.59 }
    },
    ... 14 more phases ...
    {
      "PhaseLabel": "Process.Homogenize",
      "UnitName": "HG-401",
      "DurationSec": 1416,
      "ReportParameters": {
        "avgPressureBar": 197.46,
        "maxPressureBar": 202,
        "avgMotorCurrentA": 51.37,
        "valveHoursAtStart": 0,
        "valveHoursAtEnd": 0.39
      }
    }
  ],
  "TotalDurationSec": 11604
}
```

This is the **Electronic Batch Record** — the key artifact that could be fed into:
- An MES for genealogy ("what raw materials did this batch use?")
- A historian for trending
- A quality system for hold/release decisions
- An analytics platform for per-batch KPI computation

---

# Timeline Summary

A typical sequence of events as the simulator runs (sim times shown):

```
06:00:00  Engine starts. All areas initialize in Idle.
06:00:30  Batch 1 (B-20260420-001) starts in Area 200.
          ├─ Phase: PreMix.DoseWater
06:04:54  └─ Phase: PreMix.DosePaste (paste pump P-101 runs)
06:17:00  └─ Phase: PreMix.DoseVinegar
06:23:24  └─ Phase: PreMix.HighShearMix (agitator at 1500 RPM)
06:27:12  └─ Phase: PreMix.Discharge (XV-204 opens)
          ├─ Area 300 detects upstream available -> Fill
          ├─ BATCH ID moves from MX-201 -> KT-301
          └─ Transfer at 250 L/min begins
06:34:54  Area 200 drains empty, Area 300 Fill complete -> HeatingUp
06:35:18  Batch 2 (B-20260420-002) starts in Area 200
          (concurrent with Batch 1 heating)
07:52:54  Batch 1 reaches 88°C -> Holding (pasteurization)
07:57:54  Hold complete (5 min) -> Cooling
08:30:42  Cooled to 85°C -> Discharging (Area 400 begins Fill)
          ├─ BATCH ID moves from KT-301 -> DA-401
          └─ Transfer to deaerator
08:38:24  Area 300 drains -> Idle (Batch 2 can move in soon)
          Area 400: Fill complete -> PullingVacuum
08:38:42  Batch 2 KT-301 -> DA-401/HG-401 (handoff)
08:39:18  Vacuum at 740 mmHg -> Deaerating
08:42:18  Deaeration complete -> Homogenizing (HG-401 at 200 bar)
          BT-451 begins Filling with homogenized product
08:46:48  Batch 3 (B-20260420-003) starts in Area 200
          (now 3 batches simultaneously active!)
09:05:54  Homogenization complete -> VentingVacuum
09:13:54  Batch 1 COMPLETE
          └─ BatchRecord written to B-20260420-001.json
          └─ 15 phase records captured
          └─ BATCHES_TODAY increments to 1
```

Meanwhile the filler (Area 500) has been running continuously since BT-451 first had product, consuming batch 1's 1902 L at 73.5 L/min — turning it into ~4800 bottles over 26 minutes. The packaging line has been counting them into cases (200 cases) and pallets (~3 pallets).

This is the plant, always running, always producing, always recording.
