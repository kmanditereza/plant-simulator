# Recipe Hierarchy for the Ketchup Plant

This document shows how the four ISA-88 recipe types would be concretely instantiated for the ketchup plant simulator, including the logical hierarchy mapping and the actual data structure of each recipe.

## The Recipe Hierarchy Visualised

```
┌───────────────────────────────────────────────────────────────────┐
│  R&D / PLM                                                        │
│                                                                   │
│    📄 GR-KETCHUP-001                   (General Recipe)           │
│       "Tomato Ketchup"                                            │
│       • Ingredients (as classes, with ranges)                     │
│       • Finished product spec (pH, Brix, viscosity, color)        │
│       • Process requirements (pasteurize, homogenize, deaerate)   │
│       • Allergen/regulatory baseline                              │
│       • NO equipment, NO supplier, NO site                        │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
                    │
                    │  "adapt for Springfield site"
                    ▼
┌───────────────────────────────────────────────────────────────────┐
│  Site MES — Springfield                                           │
│                                                                   │
│    📄 SR-KETCHUP-SPRINGFIELD-001       (Site Recipe)              │
│       based on: GR-KETCHUP-001                                    │
│       • Approved suppliers (Acme Paste, Heinz Vinegar, etc.)      │
│       • Local material codes (RM-PASTE-30B, RM-VIN-120)           │
│       • Water chemistry adjustments (Springfield is alkaline)     │
│       • Regional regulatory scope (FDA + Illinois)                │
│       • Tighter site-specific quality targets                     │
│       • Still NO physical equipment                                │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
                    │
                    │  "bind to BatchLine1 equipment, add procedures"
                    ▼
┌───────────────────────────────────────────────────────────────────┐
│  Batch Management System (InBatch / DeltaV Batch / FT Batch)      │
│                                                                   │
│    📄 MR-KETCHUP-14OZ-BL1-v3.2         (Master Recipe)            │
│       based on: SR-KETCHUP-SPRINGFIELD-001                        │
│       • Targets BatchLine1 (Process Cell)                         │
│       • Equipment CLASS requirements (HighShearMixer, etc.)       │
│       • Full ISA-88 procedure tree:                               │
│           Procedure → UnitProcedures → Operations → Phases        │
│       • Phase-level parameters (setpoints, times, tolerances)     │
│       • PID tuning, valve sequences, hold times                   │
│       • Report parameters to collect                              │
│       • Quality checks and interlocks                             │
│       • Still NO specific equipment INSTANCE (MX-201 vs MX-202)   │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
                    │
                    │  "instantiate for one specific batch run"
                    ▼
┌───────────────────────────────────────────────────────────────────┐
│  Batch Engine + DCS (created at execution time)                   │
│                                                                   │
│    📄 CR-B-20260420-001                (Control Recipe)           │
│       based on: MR-KETCHUP-14OZ-BL1-v3.2                          │
│       • Specific batch ID: B-20260420-001                         │
│       • Specific production order: PO-78451                       │
│       • Allocated equipment INSTANCES:                            │
│           HighShearMixer → MX-201                                 │
│           SteamKettle    → KT-301                                 │
│           BufferTank     → BT-452                                 │
│       • Allocated raw material LOTS (from WMS):                   │
│           TomatoPaste → LOT-PASTE-2026-04-15-0042                 │
│           Vinegar     → LOT-VIN-2026-04-12-0007                   │
│       • Scaled parameters (if batch size != standard)             │
│       • Assigned operator, scheduled time                         │
│       • Execution log, report parameters (filled in at runtime)   │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
                    │
                    │  "download parameters to PLC, execute phases"
                    ▼
┌───────────────────────────────────────────────────────────────────┐
│  PLC / DCS / OPC UA Server                                        │
│                                                                   │
│    Real-time tags:                                                │
│      TIT-301 = 89.5 degC                                          │
│      AGIT201_SPD = 1498 RPM                                       │
│      HOMOG_401_PRESS = 198 bar                                    │
│      BATCH_ID = "B-20260420-001"                                  │
│      BATCH_PHASE = "PasteurizationHold"                           │
│      ...                                                          │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

Each recipe **refers back** to its parent, creating a lineage that lets you trace any batch execution all the way back to the original R&D formula that spawned it.

---

## Where Each Recipe Lives in the ISA-95/88 Hierarchy

The four recipe types correspond to different levels of scope within the hierarchy:

```
Enterprise: KetchupCo
    │
    │   📄 General Recipe scope (enterprise-wide product definition)
    │       GR-KETCHUP-001 applies to ANY site making tomato ketchup
    │
    ├── Site: Springfield
    │   │
    │   │   📄 Site Recipe scope (site-specific adaptations)
    │   │       SR-KETCHUP-SPRINGFIELD-001 applies ONLY at Springfield
    │   │
    │   ├── Area: Sauce Production
    │   │   │
    │   │   ├── Process Cell: BatchLine1
    │   │   │   │
    │   │   │   │   📄 Master Recipe scope (equipment-bound template)
    │   │   │   │       MR-KETCHUP-14OZ-BL1-v3.2 can ONLY run on BatchLine1
    │   │   │   │
    │   │   │   ├── Unit: MX-201 (Premix)
    │   │   │   ├── Unit: KT-301 (Cook Kettle)
    │   │   │   ├── Unit: DA-401 (Deaerator)
    │   │   │   ├── Unit: HG-401 (Homogenizer)
    │   │   │   └── Unit: BT-451/452/453 (Buffer Tanks)
    │   │   │           │
    │   │   │           │   📄 Control Recipe scope (one batch instance)
    │   │   │           │       CR-B-20260420-001 runs ONCE on a
    │   │   │           │       specific subset of these units
    │   │   │
    │   │   └── Process Cell: CIPStation1
    │   │
    │   └── Area: Packaging
    │
    ├── Site: Newark
    │   │   (Newark has its own Site Recipe derived from GR-KETCHUP-001,
    │   │    and its own Master Recipes bound to its equipment)
    │
    └── Site: Fresno
```

**The key portability rule**: If you built BatchLine2 at Springfield with similar equipment, you'd create a new Master Recipe `MR-KETCHUP-14OZ-BL2-v1.0` — but it would reference the **same** Site Recipe `SR-KETCHUP-SPRINGFIELD-001`. The site-level formula decisions (suppliers, water chemistry) don't have to be re-approved for the new line.

Similarly, if Newark wants to start making ketchup, they create a new Site Recipe `SR-KETCHUP-NEWARK-001` based on the **same** General Recipe `GR-KETCHUP-001`. R&D doesn't have to re-approve the formula just because a new site adopted it.

---

## Data Structures

JSON is used below for readability. In real batch management systems these are often stored in B2MML XML, proprietary binary formats, or relational tables — but the content is equivalent.

### 1. General Recipe (R&D / PLM)

High-level product definition. Equipment-independent, site-independent.

```json
{
  "recipeId": "GR-KETCHUP-001",
  "recipeType": "General",
  "recipeName": "Tomato Ketchup",
  "version": "2.1",
  "status": "Approved",
  "owner": "R&D / Product Development",
  "approvedBy": "Dr. Jane Smith, VP Product",
  "approvedDate": "2024-03-15",
  "effectiveDate": "2024-04-01",
  "description": "Base formula for tomato ketchup - equipment and site independent",

  "productClassification": {
    "foodCategory": "Acid Food - Condiment",
    "productType": "Tomato Ketchup",
    "targetMarkets": ["US", "CA", "MX"],
    "shelfLifeMonths": 18,
    "storageRequirements": "Ambient, keep from freezing"
  },

  "ingredientClasses": [
    {
      "ingredientClass": "TomatoPaste",
      "role": "Primary",
      "percentMin": 28.0,
      "percentMax": 32.0,
      "percentTarget": 30.0,
      "inputSpec": {
        "brixMin": 28,
        "brixMax": 32,
        "phMax": 4.4,
        "colorAMin": 22
      }
    },
    {
      "ingredientClass": "Sugar",
      "role": "Sweetener",
      "percentMin": 20.0,
      "percentMax": 24.0,
      "percentTarget": 22.0,
      "allowedForms": ["Granulated", "HFCS-55", "HFCS-42"]
    },
    {
      "ingredientClass": "Vinegar",
      "role": "Acidulant",
      "percentMin": 10.0,
      "percentMax": 14.0,
      "percentTarget": 12.0,
      "inputSpec": {
        "grainMin": 100,
        "grainMax": 120,
        "type": "Distilled White"
      }
    },
    {
      "ingredientClass": "Water",
      "role": "Carrier",
      "percentMin": 18.0,
      "percentMax": 22.0,
      "percentTarget": 20.0,
      "inputSpec": {
        "potable": true,
        "softened": false
      }
    },
    {
      "ingredientClass": "Salt",
      "role": "Flavour",
      "percentMin": 2.0,
      "percentMax": 3.0,
      "percentTarget": 2.5
    },
    {
      "ingredientClass": "SpiceBlend",
      "role": "Flavour",
      "percentMin": 1.0,
      "percentMax": 2.0,
      "percentTarget": 1.5,
      "composition": "Onion, garlic, allspice, clove, cinnamon, celery"
    },
    {
      "ingredientClass": "StarchXanthan",
      "role": "Stabilizer",
      "percentMin": 0.5,
      "percentMax": 1.5,
      "percentTarget": 1.0
    }
  ],

  "finishedProductSpec": {
    "brix": { "min": 28, "max": 33, "target": 31, "unit": "Brix" },
    "ph": { "min": 3.5, "max": 3.9, "target": 3.7, "unit": "pH" },
    "viscosity": { "min": 4000, "max": 6000, "target": 5000, "unit": "cP" },
    "bostwick": { "min": 4, "max": 8, "target": 6, "unit": "cm/30sec" },
    "salt": { "min": 2.0, "max": 2.5, "target": 2.2, "unit": "%" },
    "colorL": { "min": 22, "max": 28, "target": 25 },
    "colorA": { "min": 20, "max": 28, "target": 24 }
  },

  "processRequirements": {
    "pasteurizationRequired": true,
    "minPasteTempC": 85,
    "minHoldTimeSec": 180,
    "homogenizationRequired": true,
    "minHomogPressureBar": 150,
    "deaerationRequired": true,
    "minVacuumMmHg": 650,
    "maxDissolvedO2Ppm": 2
  },

  "allergens": [],
  "certifications": ["FDA-Acid-Food", "Kosher-OU", "Non-GMO-Project"],
  "nutritionalTarget": {
    "per100g": {
      "calories": 112,
      "carbs_g": 27,
      "sugar_g": 22,
      "sodium_mg": 900,
      "fat_g": 0
    }
  }
}
```

---

### 2. Site Recipe (Site MES — Springfield)

Springfield-specific adaptation of the General Recipe. Adds supplier relationships, local regulatory scope, and site-specific adjustments.

```json
{
  "recipeId": "SR-KETCHUP-SPRINGFIELD-001",
  "recipeType": "Site",
  "recipeName": "Springfield Tomato Ketchup",
  "version": "1.4",
  "status": "Approved",
  "basedOn": {
    "recipeId": "GR-KETCHUP-001",
    "version": "2.1",
    "recipeType": "General"
  },

  "site": "Springfield",
  "owner": "Springfield Plant Engineering",
  "approvedBy": "John Doe, Plant Manager",
  "approvedDate": "2025-11-02",
  "effectiveDate": "2025-11-15",

  "regulatoryScope": [
    "FDA CFR Title 21 Part 113",
    "USDA FSIS",
    "State of Illinois Food Code"
  ],

  "approvedSuppliers": {
    "TomatoPaste": [
      {
        "supplier": "Acme Foods - Paste Division",
        "materialCode": "RM-PASTE-30B",
        "grade": "30 Brix Hot Break",
        "preferred": true
      },
      {
        "supplier": "Garden Fresh Bulk Paste",
        "materialCode": "RM-PASTE-30B-ALT",
        "grade": "30 Brix Hot Break",
        "preferred": false
      }
    ],
    "Vinegar": [
      {
        "supplier": "Heinz Industrial Vinegar",
        "materialCode": "RM-VIN-120",
        "grade": "120-grain Distilled White"
      }
    ],
    "Sugar": [
      {
        "supplier": "Cargill Industrial",
        "materialCode": "RM-SUGAR-GRAN",
        "grade": "Granulated, food grade"
      }
    ],
    "Salt": [
      {
        "supplier": "Morton Food Salt",
        "materialCode": "RM-SALT-FINE",
        "grade": "Fine food grade, non-iodized"
      }
    ],
    "SpiceBlend": [
      {
        "supplier": "McCormick Food Service",
        "materialCode": "RM-SPICE-KC-001",
        "grade": "Proprietary ketchup spice blend"
      }
    ],
    "StarchXanthan": [
      {
        "supplier": "Ingredion",
        "materialCode": "RM-STAB-X47",
        "grade": "Modified food starch with xanthan gum"
      }
    ]
  },

  "siteAdjustments": {
    "water": {
      "note": "Springfield municipal water: pH 8.2, hardness 240 ppm, alkalinity 180 ppm",
      "action": "Use vinegar at upper end of range to compensate for buffering",
      "vinegarTargetShift": "+0.3%"
    },
    "sugar": {
      "note": "Only granulated sugar available at Springfield (no HFCS bulk storage)",
      "enforcedForm": "Granulated"
    },
    "labeling": {
      "note": "Springfield products ship in both US and Illinois state-specific labels",
      "requiresIngredientStatementReview": true
    }
  },

  "siteFormula": [
    {
      "ingredientClass": "TomatoPaste",
      "materialCode": "RM-PASTE-30B",
      "percentTarget": 30.0
    },
    {
      "ingredientClass": "Sugar",
      "materialCode": "RM-SUGAR-GRAN",
      "percentTarget": 22.0
    },
    {
      "ingredientClass": "Water",
      "materialCode": "RM-WATER-POT",
      "percentTarget": 20.0
    },
    {
      "ingredientClass": "Vinegar",
      "materialCode": "RM-VIN-120",
      "percentTarget": 12.3,
      "note": "Adjusted +0.3% for Springfield water chemistry"
    },
    {
      "ingredientClass": "Salt",
      "materialCode": "RM-SALT-FINE",
      "percentTarget": 2.5
    },
    {
      "ingredientClass": "SpiceBlend",
      "materialCode": "RM-SPICE-KC-001",
      "percentTarget": 1.5
    },
    {
      "ingredientClass": "StarchXanthan",
      "materialCode": "RM-STAB-X47",
      "percentTarget": 1.0
    }
  ],

  "siteQualityTargets": {
    "brix": { "min": 30.0, "max": 32.0, "target": 31.0 },
    "ph": { "min": 3.6, "max": 3.8, "target": 3.7 },
    "viscosity": { "min": 4500, "max": 5500, "target": 5000 },
    "_note": "Tighter than General Recipe - Springfield QA policy"
  },

  "availableMasterRecipes": [
    "MR-KETCHUP-14OZ-BL1-v3.2",
    "MR-KETCHUP-20OZ-BL1-v2.1",
    "MR-KETCHUP-32OZ-BL1-v1.3"
  ]
}
```

---

### 3. Master Recipe (Batch Management System)

Full executable template bound to the `BatchLine1` Process Cell. Contains the ISA-88 procedure tree with phase-level parameters.

```json
{
  "recipeId": "MR-KETCHUP-14OZ-BL1-v3.2",
  "recipeType": "Master",
  "recipeName": "Springfield Standard Ketchup 14oz on BatchLine1",
  "version": "3.2",
  "status": "Approved",
  "basedOn": {
    "recipeId": "SR-KETCHUP-SPRINGFIELD-001",
    "version": "1.4",
    "recipeType": "Site"
  },

  "processCell": "BatchLine1",
  "owner": "Production Engineering",
  "author": "Mike Rodriguez",
  "approvedBy": "Mike Rodriguez, Production Manager",
  "approvedDate": "2026-02-10",
  "effectiveDate": "2026-02-15",
  "changeControlRef": "CC-2026-0041",
  "validationStatus": "Validated",
  "lastVerifiedBatch": "B-20260214-003",

  "standardBatchSizeKg": 3150.0,
  "minBatchSizeKg": 2500.0,
  "maxBatchSizeKg": 3500.0,
  "estimatedCycleTimeMin": 52,
  "finishedBottles14oz": 7910,

  "equipmentRequirements": {
    "HighShearMixer": {
      "equipmentClass": "HighShearMixer",
      "minCapacityL": 3500,
      "minAgitatorRpm": 1500,
      "requiredFeatures": ["JacketedVessel", "HighShearHead", "PowderInduction"]
    },
    "SteamKettle": {
      "equipmentClass": "JacketedSteamKettle",
      "minCapacityL": 3300,
      "requiredFeatures": ["ScrapedSurfaceAgitator", "InlinePhProbe", "MultiPointTemp"]
    },
    "Deaerator": {
      "equipmentClass": "VacuumDeaerator",
      "minVacuumMmHg": 680,
      "requiredFeatures": ["SprayDistribution"]
    },
    "Homogenizer": {
      "equipmentClass": "HighPressureHomogenizer",
      "minPressureBar": 180,
      "maxPressureBar": 250,
      "requiredFeatures": ["TwoStage"]
    },
    "BufferTank": {
      "equipmentClass": "InsulatedAgitatedVessel",
      "minCapacityL": 5000,
      "requiredFeatures": ["SlowAgitator", "TempMaintained"]
    }
  },

  "formula": [
    { "ingredientClass": "TomatoPaste",   "targetKg": 945.0, "tolerancePct": 0.5, "unit": "kg" },
    { "ingredientClass": "Sugar",         "targetKg": 693.0, "tolerancePct": 0.3, "unit": "kg" },
    { "ingredientClass": "Water",         "targetKg": 630.0, "tolerancePct": 1.0, "unit": "kg" },
    { "ingredientClass": "Vinegar",       "targetKg": 387.5, "tolerancePct": 0.5, "unit": "kg" },
    { "ingredientClass": "Salt",          "targetKg": 78.75, "tolerancePct": 1.0, "unit": "kg" },
    { "ingredientClass": "SpiceBlend",    "targetKg": 47.25, "tolerancePct": 2.0, "unit": "kg" },
    { "ingredientClass": "StarchXanthan", "targetKg": 31.5,  "tolerancePct": 1.0, "unit": "kg" }
  ],

  "procedure": {
    "procedureId": "PROC-KETCHUP-MAKE",
    "name": "Make Ketchup Batch",
    "unitProcedures": [
      {
        "unitProcedureId": "UP-PREMIX",
        "name": "Pre-Mix",
        "equipmentRole": "HighShearMixer",
        "operations": [
          {
            "operationId": "OP-PM-01",
            "name": "Dose Water (heated)",
            "phases": [
              {
                "phaseId": "PH-PM-01-01",
                "name": "Open Water Valve and Totalize",
                "phaseLogic": "WATER_DOSE",
                "parameters": {
                  "targetWeightKg": 630.0,
                  "waterTempC": 60.0,
                  "maxFillTimeSec": 300,
                  "toleranceKg": 6.3
                },
                "reportParameters": [
                  "actualWeightKg",
                  "fillDurationSec",
                  "finalWaterTempC"
                ]
              }
            ]
          },
          {
            "operationId": "OP-PM-02",
            "name": "Add Dry Ingredients",
            "phases": [
              {
                "phaseId": "PH-PM-02-01",
                "name": "Dose Sugar",
                "phaseLogic": "DRY_FEEDER",
                "parameters": { "targetKg": 693.0, "feederRateKgPerMin": 200, "toleranceKg": 2.1 },
                "reportParameters": ["actualKg", "doseDurationSec"]
              },
              {
                "phaseId": "PH-PM-02-02",
                "name": "Dose Salt",
                "phaseLogic": "DRY_FEEDER",
                "parameters": { "targetKg": 78.75, "feederRateKgPerMin": 50, "toleranceKg": 0.78 },
                "reportParameters": ["actualKg"]
              },
              {
                "phaseId": "PH-PM-02-03",
                "name": "Dose Spice Blend",
                "phaseLogic": "DRY_FEEDER",
                "parameters": { "targetKg": 47.25, "feederRateKgPerMin": 20, "toleranceKg": 0.94 },
                "reportParameters": ["actualKg"]
              },
              {
                "phaseId": "PH-PM-02-04",
                "name": "Dose Starch/Xanthan via Induction",
                "phaseLogic": "POWDER_INDUCTION",
                "parameters": { "targetKg": 31.5, "inductionRateKgPerMin": 10, "toleranceKg": 0.31 },
                "reportParameters": ["actualKg", "inductionDurationSec"]
              }
            ]
          },
          {
            "operationId": "OP-PM-03",
            "name": "Add Tomato Paste",
            "phases": [
              {
                "phaseId": "PH-PM-03-01",
                "name": "Start Paste Pump P-101, Dose by LOW",
                "phaseLogic": "LOSS_IN_WEIGHT",
                "parameters": { "targetKg": 945.0, "pumpSpeedPct": 80, "toleranceKg": 4.72 },
                "reportParameters": ["actualKg", "doseDurationSec", "avgPumpCurrent"]
              }
            ]
          },
          {
            "operationId": "OP-PM-04",
            "name": "Add Vinegar (last, after hydration)",
            "phases": [
              {
                "phaseId": "PH-PM-04-01",
                "name": "Open Vinegar Valve, Meter by FIT",
                "phaseLogic": "FLOW_DOSE",
                "parameters": { "targetKg": 387.5, "flowRateKgPerMin": 50, "toleranceKg": 1.94 },
                "reportParameters": ["actualKg", "doseDurationSec"]
              }
            ]
          },
          {
            "operationId": "OP-PM-05",
            "name": "High-Shear Mixing",
            "phases": [
              {
                "phaseId": "PH-PM-05-01",
                "name": "Ramp Agitator to High Shear",
                "phaseLogic": "AGITATOR_RAMP",
                "parameters": { "startRpm": 60, "targetRpm": 1500, "rampDurationSec": 30 },
                "reportParameters": ["finalRpm", "avgPowerKw"]
              },
              {
                "phaseId": "PH-PM-05-02",
                "name": "High-Shear Mix",
                "phaseLogic": "TIMED_HOLD",
                "parameters": { "durationSec": 1200, "agitatorRpm": 1500 },
                "reportParameters": ["avgPowerKw", "avgTorqueNm", "finalViscosityCp"]
              }
            ]
          }
        ]
      },
      {
        "unitProcedureId": "UP-TRANSFER",
        "name": "Transfer to Kettle",
        "equipmentRole": "HighShearMixer,SteamKettle",
        "operations": [
          {
            "operationId": "OP-TR-01",
            "name": "Pump to Kettle",
            "phases": [
              {
                "phaseId": "PH-TR-01-01",
                "name": "Transfer Product",
                "phaseLogic": "TRANSFER",
                "parameters": { "transferRateLpm": 250, "maxTransferTimeSec": 900 },
                "reportParameters": ["transferDurationSec", "transferredVolumeL"]
              }
            ]
          }
        ]
      },
      {
        "unitProcedureId": "UP-COOK",
        "name": "Cook and Pasteurize",
        "equipmentRole": "SteamKettle",
        "operations": [
          {
            "operationId": "OP-CK-01",
            "name": "Heat to Target",
            "phases": [
              {
                "phaseId": "PH-CK-01-01",
                "name": "PID Heat to 90C",
                "phaseLogic": "PID_HEAT",
                "parameters": {
                  "setpointC": 90.0,
                  "rampRateCPerMin": 2.5,
                  "pidKp": 2.5,
                  "pidKi": 0.8,
                  "pidKd": 0.2,
                  "steamValveMaxPct": 85,
                  "maxHeatTimeSec": 1500
                },
                "reportParameters": ["peakTempC", "heatDurationSec", "steamValveAvgPct", "steamUsedKg"]
              }
            ]
          },
          {
            "operationId": "OP-CK-02",
            "name": "Pasteurization Hold",
            "phases": [
              {
                "phaseId": "PH-CK-02-01",
                "name": "Hold at 90C for Pasteurization",
                "phaseLogic": "TEMP_HOLD",
                "parameters": {
                  "minTempC": 88.0,
                  "setpointC": 90.0,
                  "holdTimeSec": 300,
                  "allowBelowMinSec": 5
                },
                "reportParameters": [
                  "actualHoldTimeSec",
                  "timeBelowMinSec",
                  "minTempDuringHoldC",
                  "avgTempDuringHoldC"
                ]
              }
            ]
          }
        ]
      },
      {
        "unitProcedureId": "UP-DEAERATE-HOMOG",
        "name": "Deaerate and Homogenize",
        "equipmentRole": "Deaerator,Homogenizer",
        "operations": [
          {
            "operationId": "OP-DH-01",
            "name": "Vacuum Deaeration",
            "phases": [
              {
                "phaseId": "PH-DH-01-01",
                "name": "Pull Vacuum and Spray",
                "phaseLogic": "VACUUM_DEAERATE",
                "parameters": {
                  "targetVacuumMmHg": 700,
                  "holdTimeSec": 180,
                  "sprayFlowLpm": 150
                },
                "reportParameters": ["minVacuumAchievedMmHg", "actualHoldSec"]
              }
            ]
          },
          {
            "operationId": "OP-DH-02",
            "name": "Homogenize",
            "phases": [
              {
                "phaseId": "PH-DH-02-01",
                "name": "Run Homogenizer",
                "phaseLogic": "HOMOGENIZE",
                "parameters": {
                  "stage1PressureBar": 200,
                  "stage2PressureBar": 50,
                  "flowRateLpm": 80
                },
                "reportParameters": ["avgPressureBar", "minPressureBar", "flowRateLpm", "durationSec"]
              }
            ]
          }
        ]
      },
      {
        "unitProcedureId": "UP-BUFFER",
        "name": "Fill Buffer Tank",
        "equipmentRole": "BufferTank",
        "operations": [
          {
            "operationId": "OP-BF-01",
            "name": "Transfer to Buffer",
            "phases": [
              {
                "phaseId": "PH-BF-01-01",
                "name": "Fill Buffer Tank",
                "phaseLogic": "TRANSFER",
                "parameters": {
                  "transferRateLpm": 80,
                  "minBufferTempC": 82,
                  "maxFillTimeSec": 1800
                },
                "reportParameters": [
                  "transferDurationSec",
                  "finalBufferLevelPct",
                  "finalBufferTempC",
                  "productVolumeL"
                ]
              }
            ]
          }
        ]
      }
    ]
  },

  "qualityChecks": [
    {
      "checkId": "QC-01",
      "phase": "PH-CK-02-01",
      "parameter": "actualHoldTimeSec",
      "rule": "actualHoldTimeSec >= 300",
      "severity": "Critical",
      "action": "Hold Batch"
    },
    {
      "checkId": "QC-02",
      "phase": "PH-CK-02-01",
      "parameter": "minTempDuringHoldC",
      "rule": "minTempDuringHoldC >= 88.0",
      "severity": "Critical",
      "action": "Hold Batch"
    },
    {
      "checkId": "QC-03",
      "phase": "PH-DH-02-01",
      "parameter": "minPressureBar",
      "rule": "minPressureBar >= 180",
      "severity": "Major",
      "action": "Flag for QA Review"
    }
  ],

  "interlocks": [
    {
      "interlockId": "IL-01",
      "description": "Cannot start cook unless agitator running",
      "condition": "UP-COOK requires AGIT301_RUN == true"
    },
    {
      "interlockId": "IL-02",
      "description": "Cannot transfer to buffer unless buffer temp > 75C and level < 90%",
      "condition": "UP-BUFFER requires BT.temp > 75 AND BT.level < 90"
    }
  ]
}
```

---

### 4. Control Recipe (Batch Engine — created at execution time)

A specific, fully-bound, scheduled instance of the Master Recipe. Created when an operator or scheduler commits a batch to execution.

```json
{
  "recipeId": "CR-B-20260420-001",
  "recipeType": "Control",
  "basedOn": {
    "recipeId": "MR-KETCHUP-14OZ-BL1-v3.2",
    "version": "3.2",
    "recipeType": "Master"
  },

  "batchId": "B-20260420-001",
  "productionOrderId": "PO-78451",
  "customerOrderId": "SO-5531",
  "productCode": "SKU-KETCHUP-14OZ",

  "site": "Springfield",
  "area": "SauceProduction",
  "processCell": "BatchLine1",

  "scheduledStart": "2026-04-20T09:30:00-06:00",
  "scheduledEnd": "2026-04-20T10:22:00-06:00",
  "actualStart": null,
  "actualEnd": null,
  "status": "Scheduled",

  "batchSizeKg": 3150.0,
  "scaleFactor": 1.0,

  "operator": {
    "employeeId": "EMP-4412",
    "name": "J. Smith",
    "certifications": ["BatchOperator-L2", "HACCP-Trained"]
  },
  "shift": "Day",
  "supervisor": "EMP-3021",

  "equipmentAllocation": {
    "HighShearMixer": {
      "unit": "MX-201",
      "allocatedAt": "2026-04-20T09:15:00-06:00",
      "status": "Reserved"
    },
    "SteamKettle": {
      "unit": "KT-301",
      "allocatedAt": "2026-04-20T09:15:00-06:00",
      "status": "Reserved"
    },
    "Deaerator": {
      "unit": "DA-401",
      "allocatedAt": "2026-04-20T09:15:00-06:00",
      "status": "Reserved"
    },
    "Homogenizer": {
      "unit": "HG-401",
      "allocatedAt": "2026-04-20T09:15:00-06:00",
      "status": "Reserved"
    },
    "BufferTank": {
      "unit": "BT-452",
      "allocatedAt": "2026-04-20T09:15:00-06:00",
      "status": "Reserved",
      "note": "BT-451 occupied by B-20260420-000 until 10:05"
    }
  },

  "materialAllocation": [
    {
      "ingredientClass": "TomatoPaste",
      "materialCode": "RM-PASTE-30B",
      "lot": "LOT-PASTE-2026-04-15-0042",
      "sourceEquipment": "TK-101",
      "targetKg": 945.0,
      "reservedKg": 952.0,
      "actualKg": null,
      "supplier": "Acme Foods - Paste Division"
    },
    {
      "ingredientClass": "Sugar",
      "materialCode": "RM-SUGAR-GRAN",
      "lot": "LOT-SUG-2026-04-18-0019",
      "sourceEquipment": "SugarSilo",
      "targetKg": 693.0,
      "reservedKg": 700.0,
      "actualKg": null
    },
    {
      "ingredientClass": "Water",
      "materialCode": "RM-WATER-POT",
      "lot": "N/A",
      "sourceEquipment": "WaterSupply",
      "targetKg": 630.0,
      "actualKg": null
    },
    {
      "ingredientClass": "Vinegar",
      "materialCode": "RM-VIN-120",
      "lot": "LOT-VIN-2026-04-12-0007",
      "sourceEquipment": "VinegarTank",
      "targetKg": 387.5,
      "actualKg": null
    },
    {
      "ingredientClass": "Salt",
      "materialCode": "RM-SALT-FINE",
      "lot": "LOT-SALT-2026-03-28-0004",
      "sourceEquipment": "SaltBin",
      "targetKg": 78.75,
      "actualKg": null
    },
    {
      "ingredientClass": "SpiceBlend",
      "materialCode": "RM-SPICE-KC-001",
      "lot": "LOT-SPICE-2026-04-02-0011",
      "sourceEquipment": "SpiceHopper",
      "targetKg": 47.25,
      "actualKg": null
    },
    {
      "ingredientClass": "StarchXanthan",
      "materialCode": "RM-STAB-X47",
      "lot": "LOT-STAB-2026-04-05-0008",
      "sourceEquipment": "SpiceHopper",
      "targetKg": 31.5,
      "actualKg": null
    }
  ],

  "resolvedParameters": {
    "UP-PREMIX.OP-PM-01.PH-PM-01-01.targetWeightKg": 630.0,
    "UP-PREMIX.OP-PM-01.PH-PM-01-01.waterTempC": 60.0,
    "UP-PREMIX.OP-PM-02.PH-PM-02-01.targetKg": 693.0,
    "UP-PREMIX.OP-PM-02.PH-PM-02-02.targetKg": 78.75,
    "UP-PREMIX.OP-PM-02.PH-PM-02-03.targetKg": 47.25,
    "UP-PREMIX.OP-PM-02.PH-PM-02-04.targetKg": 31.5,
    "UP-PREMIX.OP-PM-03.PH-PM-03-01.targetKg": 945.0,
    "UP-PREMIX.OP-PM-04.PH-PM-04-01.targetKg": 387.5,
    "UP-PREMIX.OP-PM-05.PH-PM-05-02.durationSec": 1200,
    "UP-PREMIX.OP-PM-05.PH-PM-05-02.agitatorRpm": 1500,
    "UP-COOK.OP-CK-01.PH-CK-01-01.setpointC": 90.0,
    "UP-COOK.OP-CK-02.PH-CK-02-01.holdTimeSec": 300,
    "UP-COOK.OP-CK-02.PH-CK-02-01.minTempC": 88.0,
    "UP-DEAERATE-HOMOG.OP-DH-01.PH-DH-01-01.targetVacuumMmHg": 700,
    "UP-DEAERATE-HOMOG.OP-DH-02.PH-DH-02-01.stage1PressureBar": 200,
    "UP-BUFFER.OP-BF-01.PH-BF-01-01.minBufferTempC": 82.0
  },

  "executionLog": [],
  "reportParameters": {},
  "deviations": [],
  "alarmsDuringBatch": [],

  "qualityHolds": [],
  "finalDisposition": null,
  "labResults": null,
  "releasedBy": null,
  "releasedAt": null
}
```

---

## Runtime Example: What Happens When You Hit "Start"

Here's how the recipes are resolved and executed when operator J. Smith clicks "Start Batch" for `B-20260420-001`:

```
1. Scheduler creates Production Order PO-78451 from ERP
     └── Customer: Customer A, 50 pallets 14oz ketchup, due 2026-04-25
     └── Allocates to: Site=Springfield, Line=BatchLine1

2. Batch Manager plans 9 batches to fulfil the order
     └── For each batch, creates a Control Recipe header:
         CR-B-20260420-001 through CR-B-20260420-009
     └── Based on: MR-KETCHUP-14OZ-BL1-v3.2

3. At 09:15, Material Reservation runs
     └── WMS is queried for available lots (FIFO)
     └── Each Control Recipe is updated with allocated lots
     └── Equipment Reservation runs against the process cell
     └── Each Control Recipe is updated with allocated units (MX-201, KT-301, ...)

4. At 09:29, Control Recipe status → "Ready"
     └── Operator J. Smith sees it on the HMI queue

5. At 09:30, J. Smith clicks "Start"
     └── Batch engine reads MR-KETCHUP-14OZ-BL1-v3.2
     └── Resolves equipment roles against the Control Recipe:
         HighShearMixer → MX-201
         SteamKettle → KT-301
         ...
     └── Downloads first phase parameters to PLC:
         WATER_DOSE.targetWeightKg = 630.0
         WATER_DOSE.maxFillTimeSec = 300
         BATCH_ID = "B-20260420-001"
     └── PLC executes: opens XV-201, monitors FIT-201 totalizer, closes valve at 630 kg

6. Each phase runs to completion
     └── PLC writes report parameters to shared memory
     └── Batch engine reads them on phase completion:
         PH-PM-01-01.actualWeightKg = 629.8
         PH-PM-01-01.fillDurationSec = 42
     └── Writes them into CR-B-20260420-001.executionLog[]

7. If a quality check fails (e.g. pasteurization hold too short)
     └── CR-B-20260420-001.deviations[] entry created
     └── CR-B-20260420-001.qualityHolds[] entry created if critical
     └── Operator alerted on HMI
     └── Batch may proceed with "conditional" status pending QA review

8. Batch completes at 10:24
     └── CR-B-20260420-001.status = "Completed"
     └── Final report parameters captured
     └── Batch record committed to EBR database
     └── Lab is notified to take a sample
     └── Next batch in queue is released

9. Lab results arrive from LIMS (hours later)
     └── Via Batch ID join, lab data attaches to the batch record
     └── CR-B-20260420-001.labResults = { brix: 31.2, ph: 3.71, ... }
     └── QA reviews and sets finalDisposition = "Released"
     └── Product becomes available for shipping
```

---

## Key Observations

1. **Each recipe type references the one above it.** You can always trace a batch back from Control Recipe → Master Recipe → Site Recipe → General Recipe → R&D original. This is why recipe versioning is critical: if R&D approves a new `GR-KETCHUP-001 v2.2`, it doesn't invalidate batches running against `v2.1`-derived recipes. The lineage is frozen at batch creation time.

2. **The equipment binding gets more specific as you go down.** General Recipe: no equipment. Site Recipe: still no equipment. Master Recipe: equipment classes and roles. Control Recipe: specific equipment instances. This is what makes recipes portable across lines and sites.

3. **The material binding happens only at Control Recipe.** Raw material lots are reserved at execution time, not recipe authoring time. This is important because it means the Master Recipe doesn't need to be re-approved every time a new paste lot arrives.

4. **Parameters flow down, actuals flow up.** Control parameters (`targetKg`, `setpointC`, `holdTimeSec`) are defined in the Master Recipe, scaled if needed, and pushed down to the PLC. Report parameters (`actualKg`, `peakTempC`, `avgPressure`) are generated by the PLC phase logic and flow up into the Control Recipe's execution log. This is the "control vs report parameter" distinction from ISA-88 made concrete.

5. **The Control Recipe IS the batch record.** Once execution begins, the Control Recipe accumulates execution log entries, deviations, alarm events, and (later) lab results. By the time the batch completes and is released, the Control Recipe has become a complete electronic batch record covering everything that happened to that specific batch.

6. **The simulator already has the raw material for this.** Our OPC UA tags `BATCH_ID`, `BATCH_PHASE`, `BATCH_STATUS`, `CURRENT_BATCH_ID`, and the physical equipment tags (`TIT-301`, `AGIT201_SPD`, etc.) are exactly the DCS-side data that a batch engine would consume and weave into Control Recipes. When we build the batch execution engine in Phase 4, it will generate Control Recipes using the structure above and drive the physical tags according to the phase logic.
