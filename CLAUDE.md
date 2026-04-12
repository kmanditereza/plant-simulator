# BatchPlant - Ketchup Batch Plant OPC UA Simulator

## Project Overview

A .NET 8 console application that simulates a ketchup batch production plant. It exposes process data via **OPC UA** (port 26543) and plant utility data via **Modbus TCP** (port 5020). The simulator runs continuous batch production cycles with realistic sensor noise, PID control, and OPC UA Part 9 alarms.

## Tech Stack

- **Runtime**: .NET 8 (C#, console app)
- **OPC UA**: `OPCFoundation.NetStandard.Opc.Ua` v1.5.375.457
- **Modbus TCP**: `FluentModbus` v5.3.2
- **Container**: Docker (Linux), docker-compose

## Architecture

### Entry Point
- `Program.cs` - Starts OPC UA server, initializes nodes, starts sensor simulation, batch controller, and Modbus utilities server.

### Core Server
- `BatchPlantServer.cs` - OPC UA `StandardServer` subclass. Creates the `BatchPlantNodeManager`.
- `BatchPlantNodeManager.cs` - **Large file (~1800 lines)**. Defines the entire OPC UA address space as static `BaseDataVariableState` fields. Organizes nodes into folders: TK101-TK103 (tanks), MX101 (mixer), HX301 (heat exchanger), RX401 (reactor), FL501 (filler), BatchProduction, BatchControl.
- `BatchPlantServerConfiguration.cs` - Empty configuration extension class.
- `Namespaces.cs` - Defines `http://opcfoundation.org/BatchPlant` namespace URI.

### Batch Production Stages (Operations/)
Each stage controller is in `BatchPlant.Operations`:

| Stage | Controller | Description |
|-------|-----------|-------------|
| 0 | `MaterialLoader.cs` | Loads 3 raw materials (tomato paste, additives, seasoning) into mixer |
| 1 | `MixerController.cs` | Mixing with S-curve speed ramp, viscosity/homogeneity tracking |
| 2 | `HeatExchangerController.cs` | PID-controlled heating to 90C with fouling simulation |
| 3 | `ReactorController.cs` | 4-phase cooking: starch activation, pH adjustment, Maillard reactions, concentration |
| 4 | `FillerController.cs` | Jar filling with fast-fill/dribble-fill, weight classification, statistics |
| - | `BatchController.cs` | Orchestrator - runs stages 0-4 in sequence, manages utility demand |
| - | `UtilitiesController.cs` | Steam boiler + cooling water system simulation over Modbus TCP |

### Sensor Simulation (Simulation/)
- `SensorSimulator.cs` - Timer-based (2s interval) simulation of idle-state sensor noise for tanks, mixer, heat exchanger, and reactor.

### Alarms (Alarms/)
- `AlarmDefinition.cs` - Alarm configuration model (limits, hysteresis, severity, delays).
- `PlantAlarms.cs` - Static alarm definitions: ALM-001 to ALM-005 (mixer), ALM-100 to ALM-105 (heat exchanger), ALM-200 to ALM-212 (reactor).
- `AlarmConditionManager.cs` - OPC UA Part 9 alarm engine. Creates `ExclusiveLimitAlarmState` nodes, evaluates conditions, handles acknowledge/confirm.
- `AlarmHelper.cs` - Static facade for alarm checks from controllers.

### Helpers
- `UaWriteHelpers.cs` - Thread-safe helper to write OPC UA node values with timestamp and change notification.

### Configuration
- `Config/BatchPlantServer.Config.xml` - OPC UA server configuration (endpoints, security policies, certificates). Server binds to `opc.tcp://localhost:26543/BatchPlantServer`. Auto-accepts untrusted certificates.

## Key Ports

| Port | Protocol | Description |
|------|----------|-------------|
| 26543 | OPC UA TCP | OPC UA server endpoint |
| 5020 | Modbus TCP | Utilities (steam + cooling) Modbus registers |

## Build & Run

### Local (Visual Studio / CLI)
```bash
cd BatchPlant
dotnet restore
dotnet build
dotnet run
```

### Docker
```bash
docker compose up --build
```

The `Dockerfile` is at `BatchPlant/Dockerfile` and uses a multi-stage build (sdk:8.0 for build, runtime:8.0 for run). The `docker-compose.yml` is at the solution root.

## OPC UA Address Space (Browse Path)

```
Objects/
  TK101/          Tank 1 (LT101, FCV101, P101_START/RUN/FAULT, TT101, PT101)
  TK102/          Tank 2 (LT102, FCV102, P102_START/RUN/FAULT, TT102, PT102)
  TK103/          Tank 3 (LT103, FCV103, P103_START/RUN/FAULT, TT103, PT103)
  MX101/          Mixer (TT201, LT201, VISC201, TURB201, MT201_*, MX101_STATE/MODE, mixer_status, ...)
  HX301/          Heat Exchanger (TT301, TT302, PT301, FT301-303, SV301, P301_*, HX301_STATE/MODE, ...)
  RX401/          Reactor (TT401, TT401_1-5, PT401, LT401, pH401, BRIX401, COLOR401_*, VISC401, ...)
  FL501/          Filler (TT501, LC501, FV501_*, P501_*, CONV501_*, CAP501_*, CNT501_*, ...)
  BatchProduction/ (batch_code, product_code, batch_status, material targets/actuals, ...)
  BatchControl/   (BATCH_START, BATCH_PAUSE, BATCH_ABORT)
  Alarms/         OPC UA Part 9 alarm conditions
    MixerAlarms/
    HeatExchangerAlarms/
    ReactorAlarms/
```

## Modbus Register Map (Utilities - Port 5020)

- **Registers 0-10**: Steam system (header pressure, temperature, flow, boiler status, efficiency, energy)
- **Registers 20-31**: Cooling water (supply/return temp, flow, pump/fan status, basin level, conductivity, energy)
- **Registers 40-43**: System status, active alarms, simulation running flag, heartbeat

All scaled INT16 values (typically x10). Unit ID = 1, FC03/FC16.

## Important Patterns

- All OPC UA node writes go through `UaWriteHelpers.Write()` for thread safety and change notification.
- Node state is stored as static fields on `BatchPlantNodeManager` (e.g., `BatchPlantNodeManager.LT101`).
- Alarm checks happen inline during controller step loops via `AlarmHelper.Check*()` methods.
- The OPC UA config supports a Docker override path (`/config/BatchPlantServer.Config.xml`) checked before the local path.
- `batches_required = -1` means infinite continuous production.
