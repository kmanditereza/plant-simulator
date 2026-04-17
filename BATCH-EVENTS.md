# Batch Lifecycle Events — JSON Payload Examples

This document shows example JSON payloads for the six key events that a batch management system typically publishes as batches progress through their lifecycle. These are the events an MES, data platform, historian, or analytics system would subscribe to in order to drive dashboards, compute KPIs, trigger workflows, or build batch genealogy.

All examples trace a single batch (**B-20260420-001**) from scheduling through final quality release.

## Common Event Envelope

All events share a common outer envelope:

```json
{
  "eventType": "BatchStarted",
  "eventId": "evt-01HX4N7P2QR5A8T9ZXM3F4KWVY",
  "eventVersion": "1.0",
  "timestamp": "2026-04-20T06:00:30Z",
  "source": {
    "system": "BatchManagementSystem",
    "vendor": "AVEVA InBatch",
    "instance": "Springfield-BMS-01",
    "site": "Springfield"
  },
  "correlationId": "batch-B-20260420-001",
  "data": { ... event-specific payload ... }
}
```

| Field | Purpose |
|-------|---------|
| `eventType` | Event kind, matches one of the 6 types below |
| `eventId` | Globally unique ID (ULID/UUID) for idempotent consumption |
| `eventVersion` | Schema version for forward compatibility |
| `timestamp` | When the event occurred (ISO 8601 UTC) |
| `source` | System that emitted the event (for multi-plant deployments) |
| `correlationId` | Batch ID prefix — allows subscribers to correlate all events for one batch |
| `data` | Event-specific body (details in each section below) |

Typical transport: **Kafka topics**, **MQTT**, **AMQP/RabbitMQ**, or **cloud event buses** (AWS EventBridge, Azure Event Grid). Payloads are often also archived to immutable storage for regulatory audit (21 CFR Part 11).

---

## 1. BatchScheduled

**When**: A batch is added to the production schedule, typically minutes to hours before execution begins.
**Emitted by**: Batch Management System or Production Scheduler (upstream of the BMS).
**Typical subscribers**: MES (updates production dashboard), ERP (reserves inventory), Operations HMI (shows upcoming batches).

```json
{
  "eventType": "BatchScheduled",
  "eventId": "evt-01HX4N6R8KW2A9T1ZPM5F7CWVY",
  "eventVersion": "1.0",
  "timestamp": "2026-04-20T05:45:12Z",
  "source": {
    "system": "ProductionScheduler",
    "vendor": "SAP ME",
    "instance": "Springfield-MES",
    "site": "Springfield"
  },
  "correlationId": "batch-B-20260420-001",
  "data": {
    "batchId": "B-20260420-001",
    "productionOrderId": "PO-78451",
    "customerOrderId": "SO-5531",
    "productCode": "SKU-KETCHUP-14OZ",
    "recipe": {
      "id": "MR-KETCHUP-14OZ-BL1",
      "version": "3.2",
      "name": "Springfield Standard Ketchup 14oz",
      "masterRecipeRef": "recipes/master/MR-KETCHUP-14OZ-BL1-v3.2"
    },
    "scheduledStart": "2026-04-20T06:00:00Z",
    "scheduledEnd": "2026-04-20T09:15:00Z",
    "scheduledBy": "scheduler-auto",
    "scheduledAt": "2026-04-20T05:45:12Z",
    "site": "Springfield",
    "area": "SauceProduction",
    "processCell": "BatchLine1",
    "shift": "Day",
    "batchSizeKg": 3150.0,
    "priority": "Normal",
    "equipmentAllocation": [
      { "role": "HighShearMixer", "unit": "MX-201", "reservedFrom": "2026-04-20T06:00:00Z" },
      { "role": "SteamKettle",    "unit": "KT-301", "reservedFrom": "2026-04-20T06:25:00Z" },
      { "role": "Deaerator",      "unit": "DA-401", "reservedFrom": "2026-04-20T08:30:00Z" },
      { "role": "Homogenizer",    "unit": "HG-401", "reservedFrom": "2026-04-20T08:40:00Z" },
      { "role": "BufferTank",     "unit": "BT-452", "reservedFrom": "2026-04-20T09:00:00Z" }
    ],
    "materialReservations": [
      {
        "ingredientClass": "TomatoPaste",
        "targetKg": 945.0,
        "reservedKg": 952.0,
        "lot": "LOT-PASTE-2026-04-15-0042",
        "supplier": "Acme Foods - Paste Division",
        "sourceEquipment": "TK-101"
      },
      {
        "ingredientClass": "Sugar",
        "targetKg": 693.0,
        "reservedKg": 700.0,
        "lot": "LOT-SUG-2026-04-18-0019",
        "supplier": "Cargill Industrial",
        "sourceEquipment": "SugarSilo"
      },
      {
        "ingredientClass": "Water",
        "targetKg": 630.0,
        "lot": "N/A",
        "sourceEquipment": "WaterSupply"
      },
      {
        "ingredientClass": "Vinegar",
        "targetKg": 387.5,
        "reservedKg": 390.0,
        "lot": "LOT-VIN-2026-04-12-0007",
        "supplier": "Heinz Industrial Vinegar",
        "sourceEquipment": "VinegarTank"
      },
      {
        "ingredientClass": "Salt",
        "targetKg": 78.75,
        "lot": "LOT-SALT-2026-03-28-0004",
        "sourceEquipment": "SaltBin"
      },
      {
        "ingredientClass": "SpiceBlend",
        "targetKg": 47.25,
        "lot": "LOT-SPICE-2026-04-02-0011",
        "sourceEquipment": "SpiceHopper"
      },
      {
        "ingredientClass": "StarchXanthan",
        "targetKg": 31.5,
        "lot": "LOT-STAB-2026-04-05-0008",
        "sourceEquipment": "SpiceHopper"
      }
    ],
    "status": "Scheduled"
  }
}
```

**Key fields to note**:
- `equipmentAllocation` locks specific physical units so other batches can't grab them
- `materialReservations` hold raw material lots — these become part of the genealogy
- `scheduledBy` can be an automated scheduler or a human operator

---

## 2. BatchStarted

**When**: Execution begins — operator clicks Start, or scheduled trigger fires.
**Emitted by**: Batch Management System.
**Typical subscribers**: Historian (start collecting at-resolution data for this batch), real-time dashboards, operator HMI.

```json
{
  "eventType": "BatchStarted",
  "eventId": "evt-01HX4NA7M3F8K5Q9WZ2TPR4JVY",
  "eventVersion": "1.0",
  "timestamp": "2026-04-20T06:00:30Z",
  "source": {
    "system": "BatchManagementSystem",
    "vendor": "AVEVA InBatch",
    "instance": "Springfield-BMS-01",
    "site": "Springfield"
  },
  "correlationId": "batch-B-20260420-001",
  "data": {
    "batchId": "B-20260420-001",
    "productionOrderId": "PO-78451",
    "recipe": {
      "id": "MR-KETCHUP-14OZ-BL1",
      "version": "3.2"
    },
    "actualStart": "2026-04-20T06:00:30Z",
    "scheduledStart": "2026-04-20T06:00:00Z",
    "startDelaySec": 30,
    "startedBy": {
      "userId": "operator-4412",
      "name": "J. Smith",
      "role": "BatchOperator-L2"
    },
    "shift": "Day",
    "site": "Springfield",
    "processCell": "BatchLine1",
    "batchSizeKg": 3150.0,
    "scaleFactor": 1.0,
    "controlRecipeId": "CR-B-20260420-001",
    "parameterScaling": {
      "standardBatchSizeKg": 3150.0,
      "actualBatchSizeKg": 3150.0,
      "scaleFactor": 1.0
    },
    "initialPlantState": {
      "steamHeaderPressBar": 6.0,
      "steamHeaderTempC": 165.1,
      "coolingSupplyTempC": 8.0,
      "compressedAirPressBar": 6.5
    },
    "status": "Running",
    "currentStage": "PreMixing"
  }
}
```

**Key fields to note**:
- `startDelaySec` — actual vs scheduled start (small delays are normal; large delays indicate scheduling/resource issues)
- `startedBy` with e-signature fields (required for 21 CFR Part 11 compliance)
- `scaleFactor` — if batch size differs from standard recipe, all parameters scaled by this factor
- `initialPlantState` — snapshot of plant conditions at start (useful for troubleshooting later)

---

## 3. BatchPhaseStarted

**When**: Each phase within the batch procedure begins. A full batch emits 15+ of these events (one per phase in the ISA-88 procedure tree).
**Emitted by**: Batch Management System as it dispatches phase execution to the PLC.
**Typical subscribers**: Historian (creates phase markers for trending), MES (updates phase-level OEE), alarm management (scopes alarms to the current phase).

```json
{
  "eventType": "BatchPhaseStarted",
  "eventId": "evt-01HX4PT2W5N9R3K8FM4QAZ7BVY",
  "eventVersion": "1.0",
  "timestamp": "2026-04-20T06:34:54Z",
  "source": {
    "system": "BatchManagementSystem",
    "vendor": "AVEVA InBatch",
    "instance": "Springfield-BMS-01",
    "site": "Springfield"
  },
  "correlationId": "batch-B-20260420-001",
  "data": {
    "batchId": "B-20260420-001",
    "phaseId": "PH-CK-01-01",
    "phaseLabel": "Cook.HeatToTarget",
    "phaseLogic": "PID_HEAT",
    "unitProcedure": "UP-COOK",
    "operation": "OP-CK-01",
    "phaseSequenceNumber": 7,
    "unitName": "KT-301",
    "area": "SauceProduction",
    "startTime": "2026-04-20T06:34:54Z",
    "controlParameters": {
      "setpointC": 90.0,
      "rampRateCPerMin": 2.5,
      "pidKp": 5.0,
      "pidKi": 0.2,
      "pidKd": 0.1,
      "steamValveMaxPct": 100.0,
      "maxHeatTimeSec": 5400,
      "toleranceC": 2.0
    },
    "entrySnapshot": {
      "kettleLevelL": 1901.7,
      "kettleTempC": 17.9,
      "steamHeaderPressBar": 6.0,
      "steamHeaderTempC": 165.3,
      "agitatorRunning": true,
      "agitatorSpeedRpm": 30
    },
    "expectedReportParameters": [
      "peakTempC",
      "heatDurationSec",
      "steamValveAvgPct",
      "steamUsedKg",
      "startTempC",
      "endTempC"
    ],
    "interlocks": [
      {
        "id": "IL-01",
        "description": "Agitator must be running before steam valve opens",
        "state": "Satisfied"
      }
    ]
  }
}
```

**Key fields to note**:
- `controlParameters` are the **inputs** — setpoints and tuning from the recipe
- `entrySnapshot` captures "conditions when we started" for later diagnostic comparison
- `expectedReportParameters` tells consumers what will come back in the completion event
- `interlocks` show safety/sequencing pre-conditions that were checked

---

## 4. BatchPhaseCompleted

**When**: Each phase finishes (normally or with deviation).
**Emitted by**: Batch Management System when the phase state transitions to Complete.
**Typical subscribers**: Batch record database (appends to record), KPI engines, quality systems (deviations trigger review).

```json
{
  "eventType": "BatchPhaseCompleted",
  "eventId": "evt-01HX4R5N9K3J7P2WZQT4FM8AVY",
  "eventVersion": "1.0",
  "timestamp": "2026-04-20T07:52:54Z",
  "source": {
    "system": "BatchManagementSystem",
    "vendor": "AVEVA InBatch",
    "instance": "Springfield-BMS-01",
    "site": "Springfield"
  },
  "correlationId": "batch-B-20260420-001",
  "data": {
    "batchId": "B-20260420-001",
    "phaseId": "PH-CK-01-01",
    "phaseLabel": "Cook.HeatToTarget",
    "unitName": "KT-301",
    "startTime": "2026-04-20T06:34:54Z",
    "endTime": "2026-04-20T07:52:54Z",
    "durationSec": 4680,
    "expectedDurationSec": 4200,
    "durationDeviationPct": 11.4,
    "status": "Complete",
    "completionReason": "SetpointReached",
    "reportParameters": {
      "startTempC": 17.9,
      "endTempC": 88.1,
      "peakTempC": 88.1,
      "heatDurationSec": 4680,
      "steamValveAvgPct": 80.76,
      "pidOutputAvgPct": 80.76,
      "steamUsedKg": 314.37,
      "peakJacketPressBar": 6.05
    },
    "exitSnapshot": {
      "kettleLevelL": 1901.7,
      "kettleTempC": 88.1,
      "kettleTempBottomC": 87.6,
      "jacketPressBar": 6.05,
      "inlinePh": 3.72
    },
    "deviations": [],
    "alarmsFired": [],
    "operatorInterventions": [],
    "phaseSignature": {
      "signedBy": "system-auto",
      "signedAt": "2026-04-20T07:52:54Z",
      "reason": "Automatic phase completion"
    }
  }
}
```

### Example with a deviation:

```json
{
  "eventType": "BatchPhaseCompleted",
  "eventId": "evt-01HX4R5N9K3J7P2WZQT4FM8BCZ",
  "timestamp": "2026-04-20T07:57:54Z",
  "data": {
    "batchId": "B-20260420-001",
    "phaseId": "PH-CK-02-01",
    "phaseLabel": "Cook.PasteurizationHold",
    "unitName": "KT-301",
    "startTime": "2026-04-20T07:52:54Z",
    "endTime": "2026-04-20T07:57:54Z",
    "durationSec": 300,
    "status": "CompleteWithDeviation",
    "completionReason": "HoldTimerComplete",
    "reportParameters": {
      "holdDurationSec": 300,
      "minTempDuringHoldC": 87.6,
      "maxTempDuringHoldC": 89.2,
      "avgTempDuringHoldC": 88.4,
      "timeBelowMinTempSec": 18,
      "avgPhInline": 3.71,
      "steamDuringHoldKg": 4.32
    },
    "deviations": [
      {
        "deviationId": "DEV-20260420-001-001",
        "type": "ProcessParameterOutOfRange",
        "severity": "Minor",
        "parameter": "minTempDuringHoldC",
        "expected": ">= 88.0",
        "actual": 87.6,
        "durationSec": 18,
        "description": "Hold temperature dropped 0.4C below minimum for 18 seconds",
        "detectedAt": "2026-04-20T07:54:12Z",
        "acknowledgedBy": "operator-4412",
        "acknowledgedAt": "2026-04-20T07:54:30Z",
        "requiresInvestigation": false,
        "requiresQAReview": true
      }
    ],
    "alarmsFired": [
      {
        "alarmId": "ALM-301-TEMP-LOW",
        "severity": "Warning",
        "firedAt": "2026-04-20T07:54:12Z",
        "clearedAt": "2026-04-20T07:54:30Z",
        "acknowledgedBy": "operator-4412",
        "message": "Kettle temperature below pasteurization minimum"
      }
    ]
  }
}
```

**Key fields to note**:
- `reportParameters` are the **outputs** — actuals computed by the PLC
- `deviations` trigger QA review workflows
- `alarmsFired` during the phase are captured here (scoped to this phase)
- `operatorInterventions` would list manual pauses, resumes, overrides
- `phaseSignature` — who "signed off" on the phase completion (can be auto or operator)

---

## 5. BatchCompleted

**When**: All phases done, batch execution has finished. **But not yet QA-approved.**
**Emitted by**: Batch Management System.
**Typical subscribers**: LIMS (triggers lab sample scheduling), QA workflow (creates review task), MES (updates OEE rollups), ERP (marks production order as complete).

```json
{
  "eventType": "BatchCompleted",
  "eventId": "evt-01HX4T8W2M5P3K7FQR9NZ4AVCY",
  "eventVersion": "1.0",
  "timestamp": "2026-04-20T09:13:54Z",
  "source": {
    "system": "BatchManagementSystem",
    "vendor": "AVEVA InBatch",
    "instance": "Springfield-BMS-01",
    "site": "Springfield"
  },
  "correlationId": "batch-B-20260420-001",
  "data": {
    "batchId": "B-20260420-001",
    "productionOrderId": "PO-78451",
    "recipe": {
      "id": "MR-KETCHUP-14OZ-BL1",
      "version": "3.2"
    },
    "startTime": "2026-04-20T06:00:30Z",
    "endTime": "2026-04-20T09:13:54Z",
    "totalDurationSec": 11604,
    "expectedDurationSec": 10800,
    "status": "Complete",
    "finalStage": "Buffered",
    "pendingQAReview": true,
    "equipmentUsed": [
      { "role": "HighShearMixer", "unit": "MX-201", "durationSec": 2064 },
      { "role": "SteamKettle",    "unit": "KT-301", "durationSec": 8172 },
      { "role": "Deaerator",      "unit": "DA-401", "durationSec": 2178 },
      { "role": "Homogenizer",    "unit": "HG-401", "durationSec": 1416 },
      { "role": "BufferTank",     "unit": "BT-452", "durationSec": 0 }
    ],
    "phasesCompleted": 15,
    "phasesWithDeviations": 1,
    "phaseSummary": [
      { "phaseLabel": "PreMix.DoseWater",          "durationSec": 264,  "status": "Complete" },
      { "phaseLabel": "PreMix.DosePaste",          "durationSec": 726,  "status": "Complete" },
      { "phaseLabel": "PreMix.DoseVinegar",        "durationSec": 390,  "status": "Complete" },
      { "phaseLabel": "PreMix.HighShearMix",       "durationSec": 180,  "status": "Complete" },
      { "phaseLabel": "PreMix.Discharge",          "durationSec": 462,  "status": "Complete" },
      { "phaseLabel": "Cook.Fill",                 "durationSec": 462,  "status": "Complete" },
      { "phaseLabel": "Cook.HeatToTarget",         "durationSec": 4680, "status": "Complete" },
      { "phaseLabel": "Cook.PasteurizationHold",   "durationSec": 300,  "status": "CompleteWithDeviation" },
      { "phaseLabel": "Cook.Cool",                 "durationSec": 1968, "status": "Complete" },
      { "phaseLabel": "Cook.Discharge",            "durationSec": 462,  "status": "Complete" },
      { "phaseLabel": "Process.Fill",              "durationSec": 462,  "status": "Complete" },
      { "phaseLabel": "Process.PullVacuum",        "durationSec": 54,   "status": "Complete" },
      { "phaseLabel": "Process.Deaerate",          "durationSec": 180,  "status": "Complete" },
      { "phaseLabel": "Process.Homogenize",        "durationSec": 1416, "status": "Complete" },
      { "phaseLabel": "Process.VentVacuum",        "durationSec": 462,  "status": "Complete" }
    ],
    "aggregateConsumptions": {
      "steamKg": 318.69,
      "coolingWaterM3": 7.94,
      "electricalEnergyKwh": 12.3,
      "compressedAirNm3": 0.8
    },
    "aggregateProduction": {
      "productVolumeL": 1901.7,
      "productWeightKg": 2053.8,
      "estimatedBottleYield": 4810,
      "bufferTankAssigned": "BT-452"
    },
    "deviationCount": 1,
    "deviationSummary": [
      {
        "deviationId": "DEV-20260420-001-001",
        "phaseLabel": "Cook.PasteurizationHold",
        "severity": "Minor",
        "requiresQAReview": true
      }
    ],
    "alarmCount": 1,
    "batchRecordLocation": "/batch-records/B-20260420-001.json",
    "batchRecordFormat": "BatchPlant-JSON-v1"
  }
}
```

**Key fields to note**:
- `pendingQAReview: true` — the batch is NOT yet released; product is in the buffer tank on "hot hold"
- `phasesWithDeviations` — triggers QA review workflow
- `aggregateConsumptions` — **per-batch resource usage**, the foundation for cost accounting and sustainability KPIs
- `batchRecordLocation` — where the full record is stored for retrieval

---

## 6. BatchReleased

**When**: QA has reviewed the batch record, any deviations were investigated, lab results are in, and a final disposition is made. This typically happens **hours to days** after `BatchCompleted` (micro testing can hold batches for 48-72 hrs in food; weeks in pharma).
**Emitted by**: QA system or MES.
**Typical subscribers**: ERP (product available for picking/shipping), warehouse management (adjusts inventory from "on hold" to "available"), downstream sales systems.

```json
{
  "eventType": "BatchReleased",
  "eventId": "evt-01HX7N5K8P3M2J9QZR4FM7CVWY",
  "eventVersion": "1.0",
  "timestamp": "2026-04-22T14:30:00Z",
  "source": {
    "system": "QualityManagementSystem",
    "vendor": "Veeva QMS",
    "instance": "Springfield-QMS",
    "site": "Springfield"
  },
  "correlationId": "batch-B-20260420-001",
  "data": {
    "batchId": "B-20260420-001",
    "productionOrderId": "PO-78451",
    "batchCompletedAt": "2026-04-20T09:13:54Z",
    "reviewStartedAt": "2026-04-22T08:15:00Z",
    "releasedAt": "2026-04-22T14:30:00Z",
    "timeToReleaseHours": 53.3,
    "disposition": "Released",
    "releaseType": "Full",
    "releasedBy": {
      "userId": "qa-manager-0007",
      "name": "S. Martinez",
      "role": "QAManager",
      "qualifications": ["HACCP", "FSMA-PCQI"]
    },
    "electronicSignature": {
      "userId": "qa-manager-0007",
      "timestamp": "2026-04-22T14:30:00Z",
      "signatureAlgorithm": "SHA-256",
      "signatureHash": "a3f5b8c7d2e4f1a9b6c8d3e7f2a4b5c6d8e9f1a2b3c4d5e6f7a8b9c0d1e2f3a4",
      "reason": "Standard QA approval - batch within specification",
      "certificateRef": "cert:springfield-qa-2026"
    },
    "labResults": {
      "samplesTestedAt": "2026-04-21T10:30:00Z",
      "reportedAt": "2026-04-22T08:00:00Z",
      "performedBy": "qc-lab-tech-0022",
      "results": {
        "ph": { "value": 3.72, "spec": "3.6-3.8", "status": "Pass" },
        "brix": { "value": 31.2, "spec": "30-32", "status": "Pass", "unit": "Brix" },
        "viscosityCp": { "value": 5200, "spec": "4500-5500", "status": "Pass", "unit": "cP" },
        "bostwickCm": { "value": 5.8, "spec": "4-8", "status": "Pass", "unit": "cm/30sec" },
        "saltPct": { "value": 2.21, "spec": "2.0-2.5", "status": "Pass", "unit": "%" },
        "color": {
          "L": 25.4, "a": 24.1, "b": 14.8,
          "deltaE": 1.3,
          "spec": "deltaE < 2.0",
          "status": "Pass"
        },
        "dissolvedOxygenPpm": { "value": 1.2, "spec": "< 2.0", "status": "Pass" }
      },
      "microbiology": {
        "sampledAt": "2026-04-20T12:00:00Z",
        "incubationHours": 48,
        "aerobicPlateCount": { "value": "<10 CFU/g", "spec": "<100 CFU/g", "status": "Pass" },
        "yeastMold":         { "value": "<10 CFU/g", "spec": "<50 CFU/g",  "status": "Pass" },
        "coliforms":         { "value": "Negative",   "spec": "Negative",    "status": "Pass" }
      },
      "sensory": {
        "evaluatedBy": "sensory-panel-A",
        "appearance": "Pass",
        "flavor": "Pass",
        "texture": "Pass",
        "overallAcceptability": "Pass"
      }
    },
    "deviationResolutions": [
      {
        "deviationId": "DEV-20260420-001-001",
        "type": "ProcessParameterOutOfRange",
        "description": "Hold temperature dropped 0.4C below minimum for 18 seconds",
        "investigation": "Review of process data shows momentary steam supply fluctuation during simultaneous CIP cycle. No impact on pasteurization integrity as cumulative F-value exceeded requirement.",
        "investigatedBy": "process-engineer-0015",
        "resolvedAt": "2026-04-22T13:45:00Z",
        "resolution": "AcceptAsIs",
        "rationale": "Cumulative thermal processing exceeded minimum F-value despite brief temperature drop. Product microbial testing confirms pasteurization effectiveness.",
        "correctiveAction": "Scheduling review to separate CIP cycles from critical pasteurization phases",
        "correctiveActionRef": "CAPA-2026-0042"
      }
    ],
    "finalProduct": {
      "bufferTank": "BT-452",
      "drawStartedAt": "2026-04-20T09:18:00Z",
      "drawCompletedAt": "2026-04-20T09:48:00Z",
      "totalBottlesFilled": 4810,
      "goodBottles": 4798,
      "rejectBottles": 12,
      "firstPassYieldPct": 99.75,
      "avgFillWeightG": 397.1,
      "fillWeightStdDevG": 2.34,
      "processCapabilityCpk": 1.42
    },
    "palletsProduced": [
      {
        "palletId": "PAL-20260420-0023",
        "sscc": "00348900620000023",
        "casesCount": 60,
        "bottlesCount": 1440,
        "packagedAt": "2026-04-20T09:25:00Z"
      },
      {
        "palletId": "PAL-20260420-0024",
        "sscc": "00348900620000024",
        "casesCount": 60,
        "bottlesCount": 1440,
        "packagedAt": "2026-04-20T09:36:00Z"
      },
      {
        "palletId": "PAL-20260420-0025",
        "sscc": "00348900620000025",
        "casesCount": 60,
        "bottlesCount": 1440,
        "packagedAt": "2026-04-20T09:48:00Z"
      }
    ],
    "totalGoodBottles": 4320,
    "totalGoodCases": 180,
    "totalPallets": 3,
    "shelfLife": {
      "productionDate": "2026-04-20",
      "bestByDate": "2027-10-20",
      "shelfLifeMonths": 18
    },
    "inventoryStatus": "AvailableForShipment",
    "inventoryLocation": "Warehouse-A-Zone-3",
    "genealogy": {
      "consumedMaterialLots": [
        "LOT-PASTE-2026-04-15-0042",
        "LOT-SUG-2026-04-18-0019",
        "LOT-VIN-2026-04-12-0007",
        "LOT-SALT-2026-03-28-0004",
        "LOT-SPICE-2026-04-02-0011",
        "LOT-STAB-2026-04-05-0008"
      ],
      "producedLots": ["PAL-20260420-0023", "PAL-20260420-0024", "PAL-20260420-0025"]
    },
    "regulatoryCompliance": {
      "haccpVerified": true,
      "fsmaComplete": true,
      "allergenCleaningVerified": true,
      "cofaGenerated": true,
      "cofaRef": "COA-B-20260420-001-v1.pdf"
    }
  }
}
```

**Key fields to note**:
- `timeToReleaseHours: 53.3` — captures the time from production end to release (this is a meaningful KPI — shorter release cycle = less WIP inventory tied up)
- `electronicSignature` with SHA-256 hash meets 21 CFR Part 11 e-signature requirements
- `labResults` structured by test with spec ranges and pass/fail — enables automated quality trending
- `deviationResolutions` — full investigation record with CAPA reference
- `genealogy` — both directions: what raw material lots went in, what finished product lots came out
- `sscc` — Serial Shipping Container Code, used in GS1 standards for pallet tracking through the supply chain

### Alternative dispositions

If the batch fails QA, the same event type is used with different disposition:

```json
{
  "disposition": "Rejected",
  "releaseType": "None",
  "rejectReason": "Micro fail - coliforms detected",
  "rejectedBatchHandling": {
    "action": "DestroyAndDispose",
    "disposalRef": "DISPOSAL-2026-0007",
    "costImpactUsd": 4280.00,
    "rootCauseInvestigation": "RCI-2026-0019"
  }
}
```

Or:

```json
{
  "disposition": "ConditionalRelease",
  "releaseType": "Partial",
  "conditions": [
    "Release limited to domestic market only (does not meet EU viscosity spec)",
    "Label as 'Made in Springfield' only (export labels not applicable)"
  ]
}
```

---

## Event Flow for One Batch

Here's how these 6 event types chain together chronologically for batch `B-20260420-001`:

```
2026-04-20T05:45:12Z  ▶ BatchScheduled            (schedule entered, materials reserved)
2026-04-20T06:00:30Z  ▶ BatchStarted              (operator clicked Start)
2026-04-20T06:00:30Z  ▶ BatchPhaseStarted         (PreMix.DoseWater)
2026-04-20T06:04:54Z  ▶ BatchPhaseCompleted       (PreMix.DoseWater)
2026-04-20T06:04:54Z  ▶ BatchPhaseStarted         (PreMix.DosePaste)
2026-04-20T06:17:00Z  ▶ BatchPhaseCompleted       (PreMix.DosePaste)
...                                                (13 more phase start/complete pairs)
2026-04-20T07:52:54Z  ▶ BatchPhaseStarted         (Cook.PasteurizationHold)
2026-04-20T07:57:54Z  ▶ BatchPhaseCompleted       (Cook.PasteurizationHold - CompleteWithDeviation)
...
2026-04-20T09:13:54Z  ▶ BatchPhaseCompleted       (Process.VentVacuum - final phase)
2026-04-20T09:13:54Z  ▶ BatchCompleted            (batch execution done, pending QA)
                    ...
2026-04-22T14:30:00Z  ▶ BatchReleased             (QA approved, product available for shipping)
```

**Total event count for one batch**: 1 scheduled + 1 started + 15 phase-started + 15 phase-completed + 1 completed + 1 released = **34 events** per batch.

At 8-10 batches per day per line × multiple lines × multiple sites, this is the volume a real enterprise event bus handles for batch data alone. Typical throughput is well within the capacity of Kafka/MQTT but does require retention strategy (regulated records typically retain 7+ years).

---

## Design Notes

### Immutability

Once published, events are **immutable**. If a correction is needed (e.g., a phase record was wrong), you publish a new event like `BatchPhaseCorrected` that references the original event's `eventId`. You never modify published events — this is the foundation of audit trail integrity.

### Idempotency

Consumers should handle duplicate events gracefully. The `eventId` uniquely identifies each event, so seeing the same `eventId` twice should be a no-op. This is essential for reliable message delivery.

### Correlation

All events for a single batch share the same `correlationId` (typically `batch-{batchId}`). This lets subscribers efficiently query "give me everything about batch X" without joining on multiple keys.

### Schema Evolution

`eventVersion` in the envelope allows schema changes over time. Consumers should be able to handle multiple versions gracefully. Common approach: new optional fields are freely added within a major version; breaking changes increment the version.

### Partial Completeness

`BatchPhaseCompleted` events may be emitted even for aborted phases. The `status` field indicates whether it completed normally. This is important — a batch that was aborted mid-execution should still have a complete event trail.

### Enrichment Pipeline

Events from the BMS are typically **enriched** as they move through the data platform:
- BMS emits base event → Kafka topic `bms.raw`
- Enricher joins with ERP for customer/order info → topic `bms.enriched`
- Enricher joins with Historian for phase-time-series refs → topic `bms.analytics`
- Final archive to cold storage → `bms.archive`

Each stage adds context without modifying the original. This is a common pattern in modern MES architectures.
