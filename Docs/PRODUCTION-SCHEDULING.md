# Production Scheduling — From ERP to the Plant Floor

This document describes how production scheduling data flows from high-level enterprise planning down to the batch manager that executes work on the plant floor. It covers the data structures at each level, how they map to the ISA-95 Part 4 formal model, and the practical terminology used in food manufacturing.

> **Terminology note**: This document uses common food industry language throughout. ISA-95 Part 4 formal terms are shown in parentheses on first use for cross-reference. See [Appendix: ISA-95 Part 4 Term Mapping](#appendix-isa-95-part-4-term-mapping) for the full translation table.

---

## The Scheduling Cascade

Production scheduling is a top-down cascade. Each level strips detail and adds specificity:

```
ERP / Planning
|  "We need 20,000 kg of ketchup by Friday"
|
v
Production Scheduler (MES or standalone)
|  "Here are 7 batches across 3 days, assigned to BatchLine1"
|
v
Batch Manager (plant floor)
|  "Execute batch B-20260420-001 now using recipe CR-KETCHUP-14OZ"
|
v
Area Controllers (PLCs / sim engine)
   "Fill MX-201 with 630L water at 45C"
```

Each level receives a different data structure. The sections below show what each one looks like using our ketchup plant as a concrete example.

---

## Level 1: Production Order (from ERP)

*(ISA-95: Production Request)*

This is what SAP/Oracle pushes to the MES. Intentionally vague about *how* -- just *what* and *when*:

```json
{
  "orderId": "PO-78451",
  "orderType": "ProcessOrder",
  "productCode": "SKU-KETCHUP-14OZ",
  "productDescription": "Springfield Standard Ketchup 14oz",
  "totalRequiredKg": 20000.0,
  "requiredByDate": "2026-04-25T00:00:00Z",
  "priority": "Normal",
  "sourceOrders": [
    { "salesOrderId": "SO-5531", "customerName": "Walmart DC-Southwest", "qtyKg": 12000.0 },
    { "salesOrderId": "SO-5538", "customerName": "Kroger DC-Central",    "qtyKg": 8000.0 }
  ],
  "bomReference": "BOM-KETCHUP-14OZ-v3",
  "recipeRef": "MR-KETCHUP-14OZ-BL1",
  "plantCode": "SPR-01",
  "status": "Released",
  "releasedBy": "planner-0041",
  "releasedAt": "2026-04-19T14:30:00Z"
}
```

Notice: **no batch IDs, no equipment, no schedule times, no material lots**. ERP doesn't know or care about those. It just says "make this much of this product by this date."

### Production Orders and Batches

The relationship between production orders and batches is not always 1:1. There are three patterns seen in practice:

**Pattern 1: 1 order = 1 batch** (small/mid plants, simple products)

```
PO-78451  -->  B-20260420-001
```

The batch ID is assigned at scheduling time and lives directly on the order. This is the simplest case.

**Pattern 2: 1 order = N batches** (most common in food manufacturing)

A customer order says "make 20,000 kg of ketchup." The plant can only make 3,150 kg per batch. So one production order spawns multiple batches:

```
PO-78451  (20,000 kg)
  |-- B-20260420-001  (3,150 kg)
  |-- B-20260420-002  (3,150 kg)
  |-- B-20260420-003  (3,150 kg)
  |-- B-20260421-001  (3,150 kg)
  |-- B-20260421-002  (3,150 kg)
  |-- B-20260421-003  (3,150 kg)
  +-- B-20260422-001  (1,100 kg)   <-- last batch is partial
```

In this case the batch ID is **not on the production order**. It's on a child table and may not even be assigned until execution begins.

**Pattern 3: N orders = 1 batch** (campaign manufacturing, pharma)

Multiple small orders are consolidated into one production run because changeover is expensive:

```
SO-5531  (2,000 kg)  --+
SO-5532  (1,000 kg)  --+--->  B-20260420-001  (3,150 kg)
SO-5533    (150 kg)  --+
```

### What SAP specifically does

SAP separates these clearly:

| SAP Term | What It Is | ID Example |
|----------|-----------|------------|
| Planned Order | Demand signal from MRP | `000012345678` |
| Process Order | Executable production instruction | `000100078451` |
| Batch | The physical material produced | `B-20260420-001` |

The process order has a batch number field, but it's often blank at creation and only assigned when the batch is actually started on the plant floor. The MES or batch manager assigns it, then writes it back to SAP.

---

## Level 2: Production Schedule (MES Scheduler Output)

*(ISA-95: Production Schedule containing Production Requests with Job Orders)*

The scheduler takes the production order, checks equipment availability, and produces a **batch plan**:

```json
{
  "scheduleId": "SCH-20260420-BL1",
  "generatedAt": "2026-04-19T15:00:00Z",
  "generatedBy": "scheduler-auto",
  "horizon": {
    "start": "2026-04-20T06:00:00Z",
    "end": "2026-04-22T22:00:00Z"
  },
  "line": "BatchLine1",
  "site": "Springfield",
  "orders": [
    {
      "orderId": "PO-78451",
      "productCode": "SKU-KETCHUP-14OZ",
      "totalRequiredKg": 20000.0,
      "requiredByDate": "2026-04-25T00:00:00Z",
      "batches": [
        {
          "sequence": 1,
          "batchSizeKg": 3150.0,
          "plannedStart": "2026-04-20T06:00:00Z",
          "plannedEnd": "2026-04-20T09:15:00Z",
          "shift": "Day",
          "recipeId": "MR-KETCHUP-14OZ-BL1",
          "recipeVersion": "3.2"
        },
        {
          "sequence": 2,
          "batchSizeKg": 3150.0,
          "plannedStart": "2026-04-20T09:45:00Z",
          "plannedEnd": "2026-04-20T13:00:00Z",
          "shift": "Day",
          "recipeId": "MR-KETCHUP-14OZ-BL1",
          "recipeVersion": "3.2"
        },
        {
          "sequence": 3,
          "batchSizeKg": 3150.0,
          "plannedStart": "2026-04-20T14:00:00Z",
          "plannedEnd": "2026-04-20T17:15:00Z",
          "shift": "Swing",
          "recipeId": "MR-KETCHUP-14OZ-BL1",
          "recipeVersion": "3.2"
        },
        {
          "sequence": 4,
          "batchSizeKg": 3150.0,
          "plannedStart": "2026-04-21T06:00:00Z",
          "plannedEnd": "2026-04-21T09:15:00Z",
          "shift": "Day",
          "recipeId": "MR-KETCHUP-14OZ-BL1",
          "recipeVersion": "3.2"
        },
        {
          "sequence": 5,
          "batchSizeKg": 3150.0,
          "plannedStart": "2026-04-21T09:45:00Z",
          "plannedEnd": "2026-04-21T13:00:00Z",
          "shift": "Day",
          "recipeId": "MR-KETCHUP-14OZ-BL1",
          "recipeVersion": "3.2"
        },
        {
          "sequence": 6,
          "batchSizeKg": 3150.0,
          "plannedStart": "2026-04-21T14:00:00Z",
          "plannedEnd": "2026-04-21T17:15:00Z",
          "shift": "Swing",
          "recipeId": "MR-KETCHUP-14OZ-BL1",
          "recipeVersion": "3.2"
        },
        {
          "sequence": 7,
          "batchSizeKg": 1100.0,
          "plannedStart": "2026-04-22T06:00:00Z",
          "plannedEnd": "2026-04-22T08:30:00Z",
          "shift": "Day",
          "recipeId": "MR-KETCHUP-14OZ-BL1",
          "recipeVersion": "3.2",
          "notes": "Partial batch - remainder of order"
        }
      ],
      "cipSlots": [
        {
          "plannedStart": "2026-04-20T17:30:00Z",
          "plannedEnd": "2026-04-20T19:00:00Z",
          "type": "IntermediateCIP",
          "equipment": ["MX-201", "KT-301"]
        },
        {
          "plannedStart": "2026-04-21T17:30:00Z",
          "plannedEnd": "2026-04-21T19:00:00Z",
          "type": "IntermediateCIP",
          "equipment": ["MX-201", "KT-301"]
        }
      ],
      "constraints": {
        "maxBatchesPerDay": 3,
        "minGapBetweenBatchesMin": 30,
        "noProductionShifts": ["Night"],
        "cipRequiredEveryNBatches": 3,
        "cipRequiredOnProductChange": true
      }
    }
  ]
}
```

Key things the scheduler added:
- Broke 20,000 kg into 7 batches (6 full + 1 partial)
- Assigned time slots respecting shift patterns and gaps
- Inserted CIP slots between production days
- Last batch is a partial (1,100 kg) to hit the exact order quantity
- **Still no batch IDs** -- those are assigned at execution time

---

## Level 3: Batch Instruction (What the Batch Manager Receives)

*(ISA-95: Job Order with Work Directive)*

When it's time to start a batch, the MES hands the batch manager a single instruction. This is the **control recipe instantiation** -- the specific "go do this now":

```json
{
  "orderId": "PO-78451",
  "sequence": 1,
  "productCode": "SKU-KETCHUP-14OZ",
  "recipe": {
    "id": "MR-KETCHUP-14OZ-BL1",
    "version": "3.2"
  },
  "batchSizeKg": 3150.0,
  "scaleFactor": 1.0,

  "equipment": {
    "PreMix":     "MX-201",
    "Cook":       "KT-301",
    "Deaerate":   "DA-401",
    "Homogenize": "HG-401",
    "Buffer":     "BT-452"
  },

  "recipeParameters": {
    "PreMix": {
      "waterVolumeL":    630.0,
      "pasteVolumeL":    859.1,
      "vinegarVolumeL":  352.3,
      "sugarKg":         693.0,
      "saltKg":          78.75,
      "spiceKg":         47.25,
      "mixSpeedRpm":     1450,
      "mixDurationSec":  180
    },
    "Cook": {
      "targetTempC":     90.0,
      "rampRateCPerMin": 2.5,
      "holdTimeSec":     300,
      "holdMinTempC":    88.0,
      "coolTargetTempC": 45.0,
      "pidKp":           5.0,
      "pidKi":           0.2,
      "pidKd":           0.1
    },
    "DeaerateAndHomogenize": {
      "vacuumTargetMbar": 150,
      "deaerateTimeSec":  180,
      "homogPressureBar": 200,
      "homogPassCount":   1
    },
    "BufferTransfer": {
      "minTransferTempC":    40.0,
      "maxBufferHoldTimeMin": 120
    }
  },

  "bom": [
    { "ingredient": "TomatoPaste", "lot": "LOT-PASTE-2026-04-15-0042", "qtyKg": 945.0,  "source": "TK-101" },
    { "ingredient": "Sugar",       "lot": "LOT-SUG-2026-04-18-0019",   "qtyKg": 693.0,  "source": "SugarSilo" },
    { "ingredient": "Water",       "lot": null,                         "qtyKg": 630.0,  "source": "WaterSupply" },
    { "ingredient": "Vinegar",     "lot": "LOT-VIN-2026-04-12-0007",   "qtyKg": 387.5,  "source": "VinegarTank" },
    { "ingredient": "Salt",        "lot": "LOT-SALT-2026-03-28-0004",  "qtyKg": 78.75,  "source": "SaltBin" },
    { "ingredient": "SpiceBlend",  "lot": "LOT-SPICE-2026-04-02-0011", "qtyKg": 47.25,  "source": "SpiceHopper" },
    { "ingredient": "Stabilizer",  "lot": "LOT-STAB-2026-04-05-0008",  "qtyKg": 31.5,   "source": "SpiceHopper" }
  ],

  "staffing": [
    { "role": "BatchOperator", "minimumQualification": "L2-BatchOps", "count": 1 },
    { "role": "AreaOperator",  "minimumQualification": "L1-AreaOps",  "count": 1 }
  ],

  "qualityTargets": {
    "phRange":         [3.6, 3.8],
    "brixRange":       [30.0, 32.0],
    "viscosityRange":  [4500, 5500],
    "fillWeightG":     397.0,
    "fillToleranceG":  5.0,
    "samplingPlan":    "SP-KETCHUP-STD",
    "holdForQARelease": true
  }
}
```

This is what the batch manager needs to execute. Notice what changed from the schedule:
- **Batch ID is still not assigned** -- the batch manager generates it at the moment execution begins (`B-YYYYMMDD-NNN`)
- **Equipment is now specific** -- the scheduler reserved units, now they're locked
- **Recipe parameters are scaled** -- the master recipe's standard quantities x scaleFactor
- **Material lots are assigned** -- warehouse has issued specific lots
- **Quality targets are included** -- so the batch manager knows what "good" looks like

---

## Level 4: Batch Record (What Comes Back)

*(ISA-95: Job Response)*

After a batch completes, the batch manager produces a **batch record** that mirrors the instruction -- planned vs. actual for every field:

```json
{
  "batchId": "B-20260420-001",
  "orderId": "PO-78451",
  "sequence": 1,
  "status": "Complete",
  "actualStart": "2026-04-20T06:00:30Z",
  "actualEnd": "2026-04-20T09:13:54Z",
  "totalDurationSec": 11604,

  "steps": [
    {
      "step": "PreMix",
      "unit": "MX-201",
      "startTime": "2026-04-20T06:00:30Z",
      "endTime": "2026-04-20T06:34:54Z",
      "durationSec": 2064,
      "status": "Complete",
      "phases": [
        {
          "phase": "DoseWater",
          "durationSec": 264,
          "status": "Complete",
          "actuals": {
            "actualVolumeL": 631.2,
            "targetVolumeL": 630.0,
            "flowRateAvgLPM": 143.5,
            "waterTempC": 44.8
          }
        },
        {
          "phase": "DosePaste",
          "durationSec": 726,
          "status": "Complete",
          "actuals": {
            "actualVolumeL": 860.4,
            "targetVolumeL": 859.1,
            "pumpRunTimeSec": 680,
            "pasteTempC": 28.3
          }
        },
        {
          "phase": "DoseVinegar",
          "durationSec": 390,
          "status": "Complete",
          "actuals": {
            "actualVolumeL": 352.8,
            "targetVolumeL": 352.3,
            "flowRateAvgLPM": 54.3
          }
        },
        {
          "phase": "HighShearMix",
          "durationSec": 180,
          "status": "Complete",
          "actuals": {
            "peakSpeedRpm": 1452,
            "avgCurrentA": 32.7,
            "mixEnergyKwh": 1.63
          }
        },
        {
          "phase": "Discharge",
          "durationSec": 462,
          "status": "Complete",
          "actuals": {
            "volumeTransferredL": 1843.4,
            "transferTempC": 31.2
          }
        }
      ]
    },
    {
      "step": "Cook",
      "unit": "KT-301",
      "startTime": "2026-04-20T06:34:54Z",
      "endTime": "2026-04-20T08:51:06Z",
      "durationSec": 8172,
      "status": "CompleteWithDeviation",
      "phases": [
        {
          "phase": "Fill",
          "durationSec": 462,
          "status": "Complete",
          "actuals": {
            "volumeReceivedL": 1843.4,
            "fillTempC": 30.8
          }
        },
        {
          "phase": "HeatToTarget",
          "durationSec": 4680,
          "status": "Complete",
          "actuals": {
            "startTempC": 17.9,
            "endTempC": 88.1,
            "peakTempC": 88.1,
            "steamValveAvgPct": 80.76,
            "steamUsedKg": 314.37,
            "pidOutputAvgPct": 80.76
          }
        },
        {
          "phase": "PasteurizationHold",
          "durationSec": 300,
          "status": "CompleteWithDeviation",
          "actuals": {
            "holdDurationSec": 300,
            "minTempC": 87.6,
            "maxTempC": 89.2,
            "avgTempC": 88.4,
            "timeBelowMinTempSec": 18,
            "steamDuringHoldKg": 4.32
          },
          "deviations": [
            {
              "deviationId": "DEV-20260420-001-001",
              "type": "ProcessParameterOutOfRange",
              "severity": "Minor",
              "parameter": "minTempC",
              "expected": ">= 88.0",
              "actual": 87.6,
              "durationSec": 18,
              "description": "Hold temperature dropped 0.4C below minimum for 18 seconds"
            }
          ]
        },
        {
          "phase": "Cool",
          "durationSec": 1968,
          "status": "Complete",
          "actuals": {
            "startTempC": 88.4,
            "endTempC": 45.1,
            "coolingWaterUsedM3": 7.94,
            "coolRateCPerMin": 1.32
          }
        },
        {
          "phase": "Discharge",
          "durationSec": 462,
          "status": "Complete",
          "actuals": {
            "volumeTransferredL": 1901.7,
            "dischargeTempC": 44.8
          }
        }
      ]
    },
    {
      "step": "DeaerateAndHomogenize",
      "unit": "DA-401 / HG-401",
      "startTime": "2026-04-20T08:51:06Z",
      "endTime": "2026-04-20T09:13:54Z",
      "durationSec": 2178,
      "status": "Complete",
      "phases": [
        {
          "phase": "Fill",
          "durationSec": 462,
          "status": "Complete",
          "actuals": {
            "volumeReceivedL": 1901.7,
            "fillTempC": 44.6
          }
        },
        {
          "phase": "PullVacuum",
          "durationSec": 54,
          "status": "Complete",
          "actuals": {
            "finalPressureMbar": 148.2,
            "pumpDownTimeSec": 52
          }
        },
        {
          "phase": "Deaerate",
          "durationSec": 180,
          "status": "Complete",
          "actuals": {
            "avgPressureMbar": 151.3,
            "estimatedO2ReductionPct": 85.0
          }
        },
        {
          "phase": "Homogenize",
          "durationSec": 1416,
          "status": "Complete",
          "actuals": {
            "avgPressureBar": 198.7,
            "peakPressureBar": 201.3,
            "flowRateLPM": 80.5,
            "energyKwh": 8.94
          }
        },
        {
          "phase": "VentVacuum",
          "durationSec": 462,
          "status": "Complete",
          "actuals": {
            "ventTimeSec": 42,
            "finalPressureMbar": 1013.0
          }
        }
      ]
    }
  ],

  "consumption": [
    {
      "ingredient": "TomatoPaste",
      "lot": "LOT-PASTE-2026-04-15-0042",
      "targetKg": 945.0,
      "actualKg": 946.4,
      "variancePct": 0.15
    },
    {
      "ingredient": "Sugar",
      "lot": "LOT-SUG-2026-04-18-0019",
      "targetKg": 693.0,
      "actualKg": 693.0,
      "variancePct": 0.0
    },
    {
      "ingredient": "Water",
      "lot": null,
      "targetKg": 630.0,
      "actualKg": 631.2,
      "variancePct": 0.19
    },
    {
      "ingredient": "Vinegar",
      "lot": "LOT-VIN-2026-04-12-0007",
      "targetKg": 387.5,
      "actualKg": 388.1,
      "variancePct": 0.15
    },
    {
      "ingredient": "Salt",
      "lot": "LOT-SALT-2026-03-28-0004",
      "targetKg": 78.75,
      "actualKg": 78.75,
      "variancePct": 0.0
    },
    {
      "ingredient": "SpiceBlend",
      "lot": "LOT-SPICE-2026-04-02-0011",
      "targetKg": 47.25,
      "actualKg": 47.25,
      "variancePct": 0.0
    },
    {
      "ingredient": "Stabilizer",
      "lot": "LOT-STAB-2026-04-05-0008",
      "targetKg": 31.5,
      "actualKg": 31.5,
      "variancePct": 0.0
    }
  ],

  "equipmentUsage": [
    { "role": "HighShearMixer", "unit": "MX-201", "runTimeSec": 2064 },
    { "role": "SteamKettle",    "unit": "KT-301", "runTimeSec": 8172 },
    { "role": "Deaerator",      "unit": "DA-401", "runTimeSec": 2178 },
    { "role": "Homogenizer",    "unit": "HG-401", "runTimeSec": 1416 },
    { "role": "BufferTank",     "unit": "BT-452", "runTimeSec": 0 }
  ],

  "operators": [
    { "role": "BatchOperator", "userId": "operator-4412", "name": "J. Smith" }
  ],

  "utilities": {
    "steamKg": 318.69,
    "coolingWaterM3": 7.94,
    "electricalKwh": 12.3,
    "compressedAirNm3": 0.8
  },

  "output": {
    "productCode": "SKU-KETCHUP-14OZ",
    "volumeL": 1901.7,
    "weightKg": 2053.8,
    "bufferTank": "BT-452",
    "productTempC": 44.6,
    "pendingQARelease": true
  },

  "quality": {
    "deviationCount": 1,
    "alarmCount": 1,
    "overallStatus": "PendingReview"
  }
}
```

---

## Level 5: Production Report (Back to ERP)

*(ISA-95: Production Response)*

Rolls up all batch records for the original production order. This is what goes back to ERP to close the loop:

```json
{
  "reportId": "RPT-78451",
  "orderId": "PO-78451",
  "status": "InProgress",
  "reportedAt": "2026-04-21T17:00:00Z",

  "targetKg": 20000.0,
  "producedToDateKg": 12600.0,
  "remainingKg": 7400.0,
  "completionPct": 63.0,

  "batchSummary": {
    "planned": 7,
    "completed": 4,
    "inProgress": 0,
    "scheduled": 3
  },

  "batches": [
    { "batchId": "B-20260420-001", "sequence": 1, "status": "Complete",  "producedKg": 2053.8, "deviations": 1 },
    { "batchId": "B-20260420-002", "sequence": 2, "status": "Complete",  "producedKg": 2091.4, "deviations": 0 },
    { "batchId": "B-20260420-003", "sequence": 3, "status": "Complete",  "producedKg": 2044.7, "deviations": 0 },
    { "batchId": "B-20260421-001", "sequence": 4, "status": "Complete",  "producedKg": 2067.2, "deviations": 0 },
    { "batchId": null,             "sequence": 5, "status": "Scheduled", "producedKg": 0 },
    { "batchId": null,             "sequence": 6, "status": "Scheduled", "producedKg": 0 },
    { "batchId": null,             "sequence": 7, "status": "Scheduled", "producedKg": 0 }
  ],

  "totalUtilities": {
    "steamKg": 1274.8,
    "coolingWaterM3": 31.8,
    "electricalKwh": 49.2,
    "compressedAirNm3": 3.2
  },

  "totalConsumption": {
    "tomatoPasteKg": 3785.6,
    "sugarKg": 2772.0,
    "waterKg": 2524.8,
    "vinegarKg": 1552.4,
    "saltKg": 315.0,
    "spiceKg": 189.0,
    "stabilizerKg": 126.0
  },

  "quality": {
    "totalDeviations": 1,
    "batchesPendingQAReview": 2,
    "batchesReleased": 2,
    "batchesRejected": 0
  },

  "scheduleFidelity": {
    "onTimeStartPct": 75.0,
    "avgStartDelayMin": 4.2,
    "avgDurationVariancePct": 7.8
  }
}
```

ERP can compare planned vs. actual without knowing anything about batch control:

```
Order:   "Make 20,000 kg of SKU-KETCHUP-14OZ using 945 kg paste per batch"
Report:  "Made 12,600 kg so far across 4 batches, consumed 3,786 kg paste,
          1 batch had a deviation, 3 batches still scheduled"
```

---

## The Closed Loop

The critical design principle: **the report mirrors the order**. Every field in the production order has a corresponding field in the production report. Every field in the batch instruction has a corresponding field in the batch record.

```
ERP                          MES                          Plant Floor
 |                            |                            |
 |-- Production Order ------>|                            |
 |   ("make 20,000 kg")      |                            |
 |                           |-- Batch Instructions ----->|
 |                           |   (recipe parameters +     |
 |                           |    equipment + BOM +        |
 |                           |    quality targets)         |
 |                           |                            |
 |                           |                            |-- executes -->
 |                           |                            |
 |                           |<-- Batch Records -----------|
 |                           |    (step actuals +          |
 |                           |     consumption +           |
 |                           |     equipment usage +       |
 |                           |     deviations)             |
 |                           |                            |
 |<-- Production Report -----|                            |
 |    (aggregated actuals    |                            |
 |     vs. order)            |                            |
```

---

## How Our Simulator Maps to This Model

Our simulator currently acts as the plant floor layer. The batch manager implicitly combines levels 2 and 3 -- it runs batches continuously without an explicit schedule or production order.

| Scheduling Layer | Our Simulator | Status |
|------------------|---------------|--------|
| Production Order | Not modeled | Batches run continuously |
| Production Schedule | Not modeled | No concept of a schedule horizon |
| Batch Instruction | Implicit in area controller constants | Recipe parameters are hardcoded |
| Recipe | Hardcoded in area controllers | Not externalized |
| Steps / Phases | Area controller state machines | Fully implemented |
| Batch Record | `BatchRecord` JSON in `/app/batch-records/` | Implemented |
| Production Report | Not modeled | No aggregation back to order level |

To make the simulator more realistic, the scheduling cascade would be:

```
What we'd add                          What it replaces
--------------------------------------------------------------
ProductionSchedule class               Infinite loop of batches
  +-- list of BatchInstruction         Hardcoded recipe values in area controllers
        +-- BatchManager consumes      BatchManager auto-creates batches
            one at a time
```

The batch manager would pull the next batch instruction from the schedule queue, assign a batch ID, and pass the scaled parameters to the area controllers. When the queue empties, the plant goes idle until a new schedule arrives.

---

## Appendix: ISA-95 Part 4 Term Mapping

The data structures in this document follow the ISA-95 Part 4 (IEC 62264-3/4) operations management model using common food industry terminology. This table provides the formal cross-reference:

| This Document | ISA-95 Part 4 Term | Description |
|---------------|-------------------|-------------|
| Production Order | Production Request | One "make X of Y" instruction from ERP |
| Production Schedule | Production Schedule | Full schedule horizon -- all work planned for a time window |
| Batch (in schedule) | Job Order | A single executable batch -- one run through the equipment |
| Recipe | Work Master | The template that defines how to make the product |
| Batch Instruction | Job Order + Work Directive | The recipe instantiated with specific values for this batch |
| Recipe Parameters | Process Segment Parameters | Setpoints, targets, limits per step |
| Step | Process Segment | A defined step in the process (PreMix, Cook, etc.) |
| Phase | Phase (ISA-88) | A sub-step within a process segment |
| BOM / Ingredients | Material Requirement | What materials and lots are needed |
| Equipment | Equipment Requirement | What equipment units are reserved |
| Staffing | Personnel Requirement | What operators/roles are needed |
| Batch Record | Job Response | What actually happened -- actuals vs. targets |
| Step Actuals | Process Segment Response | Actuals for each step |
| Consumption | Material Actual | What was actually consumed |
| Equipment Usage | Equipment Actual | What units were used, for how long |
| Operators | Personnel Actual | Who actually worked the batch |
| Production Report | Production Response | Aggregation of all batch records back to ERP |

### ISA-95 Formal Hierarchy

For reference, the complete ISA-95 Part 4 hierarchy with our informal equivalents:

```
Production Schedule              = "the schedule" / "the plan"
|
+-- Production Request           = "production order" / "work order"
    |
    +-- Job Order                = "batch" / "batch instruction"
    |   +-- Work Master (ref)    = "recipe" / "the spec"
    |   |   +-- Process Segment  = "step" / "mixing", "cooking", etc.
    |   |
    |   +-- Job Response         = "batch record" / "batch sheet"
    |
    +-- Production Response      = "production report" / "shift report"
```
