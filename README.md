# Ketchup Batch Plant Simulator

A .NET 8 simulator of a ketchup manufacturing plant, for building and demonstrating industrial data pipelines: OPC UA collection, Unified Namespace, contextualisation, MES/ERP integration and analytics.

It runs a plant with raw materials, pre-mix, cooking, deaeration and homogenisation, buffer tanks, filling, packaging, CIP and utilities, and exposes it the way a real DCS and batch manager would:

- **OPC UA** for process values, motor and valve states, alarms, batch status, line KPIs, and event flags.
- **An HTTP API** to dispatch batches and packaging orders, and to read the ISA-88/ISA-95 events they produce.
- **A SCADA console** in the browser to watch the plant and inject equipment faults.

Your own MES, scheduler, historian or integration platform can drive it and record what it does.

- [Run it](#run-it)
- [Interfaces at a glance](#interfaces-at-a-glance)
- [Start a batch](#start-a-batch)
- [Run a packaging order](#run-a-packaging-order)
- [Monitor production](#monitor-production)
- [Read events: flag, read, acknowledge](#read-events-flag-read-acknowledge)
- [Inject equipment faults](#inject-equipment-faults)
- [The SCADA console](#the-scada-console)
- [Documentation](#documentation)

## Run it

```bash
docker compose up --build
```

Or without Docker (needs the .NET 8 SDK):

```bash
cd BatchPlant
dotnet run
```

| Port | What |
|---|---|
| 26543 | OPC UA server: `opc.tcp://localhost:26543/BatchPlantServer` |
| 5050 | HTTP API, the SCADA console (`/` or `/scada`) and the vibration console (`/sim`) |

| Environment variable | Default | Meaning |
|---|---|---|
| `SIM_SPEED` | `1.0` | Simulation speed multiplier, 0.1 to 60. At 60, one plant hour passes in one real minute. The included `docker-compose.yml` sets 60. |
| `API_PORT` | `5050` | Port of the HTTP API |
| `SIM_START` | next Monday 06:00 | Plant clock at start-up |

**The plant starts idle.** The batch line runs only when it receives a work order, and the packaging line only when it receives a packaging order. Utilities, idle-state process values and condition monitoring run all the time.

**There is no authentication.** OPC UA uses security policy None with anonymous login, and the HTTP API has no login. Don't expose the ports to an untrusted network.

## Interfaces at a glance

| You want to… | Use |
|---|---|
| Start a batch | `POST /api/control-recipe`, then `POST /api/work-order` |
| Start a packaging run | `POST /api/packaging-order` |
| Follow a batch live | OPC UA folder `BatchControl` (`BATCH_STATUS`, `BATCH_ID`, `BATCH_PHASE`, `BATCH_PCT` …), or `GET /api/status` |
| Know that something happened | An OPC UA event flag in `BatchControl` turns `true` (for example `BATCH_COMPLETED`) |
| Get the details of what happened | `GET` the matching event endpoint (for example `GET /api/batch/completed`) |
| Reset the flag after reading | `POST` the matching `/ack` endpoint (for example `POST /api/batch/completed/ack`) |
| Read any process value | OPC UA, NodeId `ns=2;s=<Folder>/<Tag>`; see [OPCUA-TAG-REFERENCE.md](Docs/OPCUA-TAG-REFERENCE.md) |
| Read every value over HTTP | `GET /api/scada/catalog` (definitions) and `GET /api/scada/values` (live values) |
| Make a machine fail | `POST /api/vibration/anomaly` |

All HTTP bodies are JSON (`Content-Type: application/json`). Event timestamps are on the **plant clock**, which runs at `SIM_SPEED`, not on wall-clock time.

## Start a batch

A batch takes two calls, the same way a real MES works with a batch manager:

1. **Download the control recipe:** what to make and with which parameters. The simulator stores it.
2. **Dispatch the work order:** the "go" signal. It refers to the recipe by `controlRecipeNumber`.

### 1. `POST /api/control-recipe`

A minimal recipe looks like this. Every process parameter falls back to the standard recipe when it's left out:

```bash
curl -X POST http://localhost:5050/api/control-recipe \
  -H 'Content-Type: application/json' -d '{
  "controlRecipeNumber": "CR-1000000001-01",
  "source":  { "processOrderNumber": "1000000001",
               "masterRecipe": { "id": "MR-KETCHUP-BULK-STD", "version": "01" } },
  "product": { "materialNumber": "SF-KETCHUP-BULK-STD", "producedBatch": "B-20260101-001",
               "targetQuantity": 2000, "unitOfMeasure": "KG" },
  "isa95Context": { "enterprise": "GlobalIndustries", "site": "Munich",
                    "area": "ProductionArea", "processCell": "Line1" }
}'
```

```json
{ "status": "stored", "controlRecipeNumber": "CR-1000000001-01", "storedRecipes": 1 }
```

- **Required:** `controlRecipeNumber`, and nothing else.
- **Wrapping:** the recipe can be at the root or under a `"controlRecipe"` key.
- **Re-posting:** posting the same number again replaces the stored recipe. `GET /api/recipes` lists what's stored.

| Optional field | Used for |
|---|---|
| `customer` `{customerId, customerName}` | Carried into every event |
| `scaleFactor` | Recipe scale factor (default 1.0) |
| `equipmentBinding` | Which units run the batch. Default `{"PreMix":"MX-201","Cook":"KT-301","DeaerateAndHomogenize":["DA-401","HG-401"],"BufferTransfer":"BT-452"}` |
| `bom` | Bill of materials with lots: `[{"ingredient", "lot", "qtyKg", "sourceUnit"}]`, carried into the events |
| `procedure.operations[].phases[].parameters[]` | Process parameters, below |

**Process parameters** use the ISA-88 layout: an operation contains phases, and a phase has parameters in the form `{"id", "value", "uom"}`. These are the ones the simulator reads:

| Operation | Phase | Parameter `id` | Default |
|---|---|---|---|
| `PreMix` | `DoseWater` | `chargeQuantity` | 630 kg |
| `PreMix` | `DosePaste` | `chargeQuantity` | 945 kg |
| `PreMix` | `DoseVinegar` | `chargeQuantity` | 378 kg |
| `PreMix` | `HighShearMix` | `sugarKg`, `saltKg`, `spiceKg`, `stabilizerKg` | 693, 78.75, 47.25, 31.5 kg |
| `PreMix` | `HighShearMix` | `mixSpeedRpm`, `mixDurationSec` | 1450 rpm, 180 s |
| `Cook` | `HeatToTarget` | `targetTemperature`, `rampRate` | 90 °C, 2.5 °C/min |
| `Cook` | `PasteurizationHold` | `holdDuration` (seconds, or minutes with `"uom": "MIN"`) | 300 s |
| `Cook` | `Cool` | `coolTargetTemperature` | 45 °C |
| `DeaerateAndHomogenize` | `PullVacuum` | `vacuumTargetMbar` | 150 mbar |
| `DeaerateAndHomogenize` | `Deaerate` | `deaerateTimeSec` | 180 s |
| `DeaerateAndHomogenize` | `Homogenize` | `homogenizationPressure`, `homogPassCount` | 200 bar, 1 |
| `BufferTransfer` | `Transfer` | `minTransferTemperature` | 40 °C |
| `BufferTransfer` | `Hold` | `maxBufferHoldTimeMin` | 120 min |

For example, to cook at 92 °C:

```json
"procedure": { "operations": [
  { "name": "Cook", "phases": [
    { "name": "HeatToTarget", "parameters": [ { "id": "targetTemperature", "value": 92, "uom": "DEGC" } ] }
  ] }
] }
```

A complete recipe and work order, as an MES would send them, are in [Docs/nodered-batch-dispatch.json](Docs/nodered-batch-dispatch.json). It's a Node-RED flow you can import, or just read for the payloads.

### 2. `POST /api/work-order`

```bash
curl -X POST http://localhost:5050/api/work-order \
  -H 'Content-Type: application/json' -d '{
  "data": {
    "workOrderId": "WO-0001",
    "controlRecipeNumber": "CR-1000000001-01",
    "producedBatch": "B-20260101-001",
    "state": "DISPATCHED"
  }
}'
```

```json
{ "status": "queued", "workOrderId": "WO-0001", "batchId": "B-20260101-001",
  "controlRecipeNumber": "CR-1000000001-01", "queueDepth": 1 }
```

- **Required:** `workOrderId`, `controlRecipeNumber` and `state`, which must be `"DISPATCHED"`. The fields can sit under `"data"` (a full `WorkOrderDispatched` event) or at the root.
- **Overrides:** `processOrderNumber`, `customer`, `isa95Context`, and `boundUnits` (same shape as `equipmentBinding`) take precedence over the values in the recipe.
- **Errors:** an unknown `controlRecipeNumber` returns **404** with the list of stored recipes. A missing field or a wrong `state` returns **400**.

**What happens next**
1. The work order joins a FIFO queue, and `BATCH_SCHEDULED` turns `true` straight away.
2. When the pre-mix vessel is idle, the batch manager takes the next work order and starts the batch. `BATCH_STARTED` and `WO_ACTIVATED` turn `true`.
3. If all three buffer tanks are full, new batches wait and `BATCH_LINE_QUEUED` is `true`. Running a packaging order empties the tanks.
4. With more than one work order queued, batches overlap: while one is cooking, the next can already be in pre-mix.
5. A standard 2000 kg batch takes about 3 plant hours from scheduling to completion, about 3 real minutes at `SIM_SPEED=60`.

## Run a packaging order

Packaging is a separate order (make and pack are split). The filling and packaging line draws bulk product from whichever buffer tank is ready, until it has made the target number of cases, then goes idle.

```bash
curl -X POST http://localhost:5050/api/packaging-order \
  -H 'Content-Type: application/json' -d '{
  "packagingOrderNumber": "PKG-2000000001",
  "workOrderId": "PWO-0001",
  "sku": "FG-KETCHUP-14OZ-CASE",
  "sourceBulkMaterial": "SF-KETCHUP-BULK-STD",
  "targetQuantity": 500,
  "unitOfMeasure": "CASE",
  "bottlesPerCase": 24,
  "casesPerPallet": 60
}'
```

- **Required:** `packagingOrderNumber`, `workOrderId`, and `targetQuantity` (in cases, greater than 0).
- **Defaults:** `bottlesPerCase` is 24 and `casesPerPallet` is 60. `isa95Context` defaults to GlobalIndustries / Munich / PackagingArea / Line1.
- **Duplicates:** an order number that was already accepted returns `"status": "duplicate-ignored"`, so a retried dispatch never runs twice.
- **Progress:** the OPC UA folder `PackLine` shows the active order: `PACK_ORD_ID`, `PACK_ORD_CASES_DONE`, `PACK_ORD_PROGRESS_PCT`, `PACK_ORD_STATE` and more.
- **Completion:** when the order is done, `PACK_ORDER_COMPLETED` turns `true`. The `PackagingRunCompleted` event lists which bulk batches went into the cases (material genealogy).

## Monitor production

**Over OPC UA**
- **Connection:** endpoint `opc.tcp://<host>:26543/BatchPlantServer`, security None, anonymous. Namespace `http://opcfoundation.org/BatchPlant` (index 2).
- **NodeIds:** `ns=2;s=<Folder>/<Tag>`, for example `ns=2;s=BatchControl/BATCH_STATUS`. The browse path is the same as the NodeId.
- **Tag list:** every tag, with type, unit and description, is in [OPCUA-TAG-REFERENCE.md](Docs/OPCUA-TAG-REFERENCE.md).

| Tag (folder `BatchControl`) | Meaning |
|---|---|
| `BATCH_STATUS` | `IDLE` when no batch is in progress, otherwise the batch's status (`Running`, `Complete` or `Aborted`) |
| `BATCH_ID`, `BATCH_SIZE_KG` | The running batch |
| `BATCH_PHASE`, `BATCH_PHASE_TMR` | Current phase, e.g. `Cook.HeatToTarget`, and seconds in it |
| `BATCH_PCT` | Batch progress, % |
| `BATCH_START_TIME`, `BATCH_END_TIME` | Plant-clock timestamps |
| `BATCH_LINE_QUEUED` | New batches are held because every buffer tank is full |
| Event flags | See the next section |

`BatchLine` and `PackLine` hold line KPIs such as OEE, throughput and yield. `PlantStatus` holds the plant mode, shift, plant clock (`SIM_TIME`) and `SIM_SPEED`.

The `BATCH_START`, `BATCH_HOLD` and `BATCH_ABORT` command tags exist in `BatchControl`, but the simulator doesn't currently act on them. Start batches through the HTTP API.

**Over HTTP:** `GET /api/status` returns the queue depth, the current batch and work order, and the last 20 API events.

```json
{ "queueDepth": 0, "currentBatch": "B-20260101-001", "currentWorkOrder": "WO-0001",
  "currentCustomer": "", "currentProduct": "SF-KETCHUP-BULK-STD", "storedRecipes": 1,
  "recentEvents": ["[13:40:53] Control recipe downloaded: CR-1000000001-01", "…"] }
```

## Read events: flag, read, acknowledge

Every production event uses the same pattern, the one a PLC uses to hand data to a higher-level system:

1. The simulator builds the event and sets a **flag** to `true` in OPC UA folder `BatchControl`.
2. Your system sees the flag (subscribe to it, or poll it) and calls **`GET`** on the event endpoint to fetch the JSON.
3. After storing or forwarding it, your system calls **`POST …/ack`**. The simulator clears the event and sets the flag back to `false`.

| OPC UA flag | Read with `GET` | Reset with `POST` | Event | Raised when |
|---|---|---|---|---|
| `BATCH_SCHEDULED` | `/api/batch/scheduled` | `/api/batch/scheduled/ack` | BatchScheduled | A work order is accepted |
| `WO_ACTIVATED` | `/api/work-order/activated` | `/api/work-order/activated/ack` | WorkOrderActivated | The batch starts executing |
| `BATCH_STARTED` | `/api/batch/started` | `/api/batch/started/ack` | BatchStarted | The batch starts executing |
| `PHASE_STARTED` | `/api/batch/phase-started` | `/api/batch/phase-started/ack` | BatchPhaseStarted | A phase starts |
| `PHASE_COMPLETED` | `/api/batch/phase-completed` | `/api/batch/phase-completed/ack` | BatchPhaseCompleted | A phase ends, with actuals and deviations |
| `BATCH_COMPLETED` | `/api/batch/completed` | `/api/batch/completed/ack` | BatchCompleted | The batch leaves the last unit |
| `WO_COMPLETED` | `/api/work-order/completed` | `/api/work-order/completed/ack` | WorkOrderCompleted | Same moment as `BATCH_COMPLETED` |
| `BATCH_RELEASED` | `/api/batch/released` | `/api/batch/released/ack` | BatchReleased | Same moment; QA disposition of the batch |
| `PACK_ORDER_COMPLETED` | `/api/packaging/completed` | `/api/packaging/completed/ack` | PackagingRunCompleted | A packaging order reaches its target |

**Reading and resetting**
- While nothing is waiting, the `GET` returns a short placeholder such as `{"completed": false}` or `{"scheduled": false}`.
- Each `/ack` takes no body and returns, for example, `{"status": "acknowledged", "batchCompleted": false}`.
- **Each of these events holds one message at a time.** A new event of the same type replaces one that hasn't been read yet. Phases change every few seconds at `SIM_SPEED=60`, so subscribe to `PHASE_STARTED` and `PHASE_COMPLETED` (or poll about once a second) and acknowledge promptly.
- **Reset through the `/ack` endpoint, not by writing `false` to the flag.** The flags are writable over OPC UA, but writing to them clears only the flag; the event stays stored.

**Queued events.** Downtime, equipment faults and product disposal can happen faster than anyone polls, so these are queued rather than held one at a time:

| OPC UA flag | Read with `GET` | Reset with `POST` |
|---|---|---|
| `DOWNTIME_EVENT_PENDING` | `/api/downtime/events` | `/api/downtime/events/ack` |
| `FAULT_EVENT_PENDING` | `/api/fault/events` | `/api/fault/events/ack` |
| `DISPOSAL_EVENT_PENDING` | `/api/disposal/events` | `/api/disposal/events/ack` |

- The `GET` returns `{"pending": n, "events": [ … ]}`, oldest first.
- Acknowledge with `{"count": n}` to remove the first *n* events you handled. The response says how many remain, and the flag stays `true` until the queue is empty.

**A minimal integration loop**

```text
every second (or on an OPC UA data change):
    for each flag in BatchControl that is true:
        event = GET  <its endpoint>
        store or publish event        # historian, MQTT/UNS, MES …
        POST <its endpoint>/ack       # only after the event is safe
```

Every event has the same envelope: `eventType`, `eventId`, `eventVersion`, `timestamp`, `source` and `correlationId`, plus a `data` object. `eventId` is stable per event, so you can use it to drop duplicates. `correlationId` ties together all the events of one batch (`batch-<batchRecordId>`) or one work order. [BATCH-EVENTS.md](Docs/BATCH-EVENTS.md) shows every payload in full.

## Inject equipment faults

Every rotating asset (pumps, agitators, the homogeniser, the filler, conveyors, fans, the compressor) has vibration, bearing-temperature and motor-current signals. Vibration rises with load and wear and jumps under a modelled fault. You can also make an asset fail on demand, to test an alarm rule, an anomaly detector or an agent:

```bash
# List assets with their current and healthy vibration
curl http://localhost:5050/api/vibration/assets

# Raise FL-501's vibration to 4x healthy over 30 plant minutes, until cleared
curl -X POST http://localhost:5050/api/vibration/anomaly -H 'Content-Type: application/json' \
  -d '{"assetId": "FL-501", "mode": "Ramp", "severity": 4, "rampMinutes": 30, "durationMinutes": 0, "reason": "bearing wear test"}'

# Clear one asset, or all of them
curl -X POST http://localhost:5050/api/vibration/anomaly/clear -H 'Content-Type: application/json' -d '{"assetId": "FL-501"}'
curl -X POST http://localhost:5050/api/vibration/anomaly/clear -H 'Content-Type: application/json' -d '{"all": true}'
```

| Field | Meaning |
|---|---|
| `mode` | `Step` (jump at once), `Ramp` (rise over `rampMinutes`) or `Spike` (normal most of the time, with intermittent bursts, as with looseness or cavitation) |
| `severity` | Multiple of healthy vibration, 1 to 25 (default 3) |
| `durationMinutes` | Plant minutes before it clears itself; `0` means until cleared |

Injecting on an asset that already has an injected fault replaces that fault. Use the asset IDs exactly as `GET /api/vibration/assets` returns them, for example `FL-501`, `P-101` or `EQ-MUC-CP601`.

## The SCADA console

Open **`http://<host>:5050/`** (or `/scada`) in a browser. It shows the whole plant on one screen, from raw materials to utilities, and needs no setup.

**How it gets its data**
- On load it fetches `GET /api/scada/catalog` once: every tag's name, folder, unit, data type, description and whether it's writable.
- It then polls `GET /api/scada/values` every second, and `GET /api/vibration/assets` every 3 seconds. It reads the same tag registry as the OPC UA server, so the console and an OPC UA client always show the same values.

**How the graphic is linked to the data**
- **Each piece of equipment on the graphic is linked to one or more OPC UA folders**, for example FL-501 to `Area500_Filling/Filler_FL501`. Its values, running state and animation (flow in pipes, bottles on the conveyor) come from tags in those folders.
- **Clicking an object opens its faceplate**, which lists **every** tag in its folders with the live value, unit, a short trend and the tag name. Coded integers are shown with their meaning, for example `2 Fault`. Tag names in the faceplate match the OPC UA tag names, which helps when mapping tags in your own system.
- **The footer reports coverage**, for example "*N tags · M objects · every tag on the graphic*". A tag that isn't linked to any object is listed there, so nothing is silently missing.

**How to read it.** It follows ISA-101 high-performance HMI practice:
- The background and equipment are grey. **Colour means something is abnormal**, nothing else.
- Running or open equipment is drawn solid, stopped or closed equipment hollow. Red and green aren't used for state.
- **The alarm banner** at the top lists abnormal conditions by priority: **P1** for a fault or unplanned stop, **P2** for a process value that needs action now, and **P3** for an early warning or maintenance due. Priority is shown by colour, shape and number. Click a row to open that object.
  - These alarms are worked out **in the browser from the tag values**. The simulator publishes raw signals, as a real plant does, and the console applies the alarm rules.
- Where the normal range matters, an analog value has a pointer against its normal band.
- **Magenta means stale data.** If the simulator stops answering for more than 4 seconds, the header shows `COMMS LOST` and values turn magenta, so frozen numbers are never mistaken for live ones.

**What you can do in it**
- **Watch** batches move through pre-mix, cooking, processing and the buffer tanks, and the packaging line fill and pack.
- **Inspect** any asset by clicking it.
- **Inject vibration faults.** On a rotating asset's faceplate, pick a mode, severity and duration, then press **Inject** or **Clear**. This uses the fault API above, so you can watch your own condition-monitoring pipeline react.
- **Link straight to an asset:** `http://<host>:5050/#FL-501` opens that faceplate.

The console is otherwise read-only. Batches and packaging orders are started only through the HTTP API, by your system.

`/sim` is a simpler console that lists every rotating asset by area, with its vibration and fault controls.

## Documentation

| Document | Covers |
|---|---|
| [SIMULATION.md](Docs/SIMULATION.md) | The plant, its process and what is simulated |
| [SIMULATION-WALKTHROUGH.md](Docs/SIMULATION-WALKTHROUGH.md) | A walk through one production run |
| [OPCUA-TAG-REFERENCE.md](Docs/OPCUA-TAG-REFERENCE.md) | Every OPC UA tag, with NodeIds and units |
| [BATCH-EVENTS.md](Docs/BATCH-EVENTS.md) | Every batch and work order event payload |
| [PRODUCTION-SCHEDULING.md](Docs/PRODUCTION-SCHEDULING.md) | From ERP order to batch record, and how the simulator maps to it |
| [RECIPE-HIERARCHY.md](Docs/RECIPE-HIERARCHY.md) | ISA-88 master and control recipes |
| [ISA-95-88-HIERARCHY.md](Docs/ISA-95-88-HIERARCHY.md), [ISA-95-LOGICAL-HIERARCHY-IN-IT.md](Docs/ISA-95-LOGICAL-HIERARCHY-IN-IT.md) | How the plant maps to ISA-95 and ISA-88 |
| [nodered-batch-dispatch.json](Docs/nodered-batch-dispatch.json) | Example Node-RED flow that dispatches a full recipe and work order |

## Licence

[MIT](LICENSE)
