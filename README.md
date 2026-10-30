# Ketchup Batch Plant Simulator

A .NET 8 simulator of a ketchup manufacturing plant, for building and demonstrating industrial data pipelines: OPC UA collection, Unified Namespace, contextualisation, MES/ERP integration and analytics.

It runs a 24/7 plant (raw materials, pre-mix, cooking, deaeration and homogenisation, buffer tanks, filling, packaging, CIP and utilities) and exposes it the way a real DCS would: process values, motor and valve states, alarms, batch status and line KPIs over **OPC UA**, plus ISA-88/ISA-95 batch and work order events over an **HTTP API**.

## Run it

```bash
docker compose up --build
```

Or without Docker:

```bash
cd BatchPlant
dotnet run
```

| Port | What |
|---|---|
| 26543 | OPC UA server: `opc.tcp://localhost:26543/BatchPlantServer` (security None, anonymous) |
| 5050 | HTTP API, and a SCADA console at `http://localhost:5050/scada` |

| Environment variable | Default | Meaning |
|---|---|---|
| `SIM_SPEED` | `1.0` | Simulation speed multiplier, 0.1 to 60 |
| `API_PORT` | `5050` | Port of the HTTP API |
| `SIM_START` | next Monday 06:00 | Simulated start time |

## Documentation

| Document | Covers |
|---|---|
| [SIMULATION.md](Docs/SIMULATION.md) | The plant, its process and what is simulated |
| [SIMULATION-WALKTHROUGH.md](Docs/SIMULATION-WALKTHROUGH.md) | A walk through one production run |
| [OPCUA-TAG-REFERENCE.md](Docs/OPCUA-TAG-REFERENCE.md) | Every OPC UA tag, with NodeIds and units |
| [BATCH-EVENTS.md](Docs/BATCH-EVENTS.md) | Batch and work order events on the HTTP API |
| [PRODUCTION-SCHEDULING.md](Docs/PRODUCTION-SCHEDULING.md) | Dispatching work orders and packaging orders |
| [RECIPE-HIERARCHY.md](Docs/RECIPE-HIERARCHY.md) | ISA-88 recipes |
| [ISA-95-88-HIERARCHY.md](Docs/ISA-95-88-HIERARCHY.md), [ISA-95-LOGICAL-HIERARCHY-IN-IT.md](Docs/ISA-95-LOGICAL-HIERARCHY-IN-IT.md) | How the plant maps to ISA-95 and ISA-88 |

## Licence

[MIT](LICENSE)
