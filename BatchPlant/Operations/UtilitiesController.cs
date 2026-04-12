using System.Net;
using FluentModbus;

namespace BatchPlant.Operations
{
    /// <summary>
    /// Simulates plant utilities (Steam and Cooling Water systems) and exposes data via Modbus TCP.
    /// Default Modbus TCP Server runs on port 5020 (configurable).
    /// Note: Port 502 (standard Modbus) requires administrator privileges on Windows.
    /// </summary>
    public class UtilitiesController : IDisposable
    {
        private readonly Random _rand = new();
        private readonly ModbusTcpServer _modbusServer;
        private readonly int _modbusPort;
        private CancellationTokenSource? _cts;
        private Task? _simulationTask;
        private bool _disposed;

        // Modbus TCP Configuration
        private const int DefaultModbusPort = 5020;  // Use non-privileged port (502 requires admin)
        private const byte UnitIdentifier = 1;

        // ═══════════════════════════════════════════════════════════════════════
        // MODBUS REGISTER MAP (Holding Registers - FC03/FC16)
        // ═══════════════════════════════════════════════════════════════════════
        // 
        // STEAM SYSTEM (Registers 0-19)
        // ───────────────────────────────────────────────────────────────────────
        // Register  | Description                  | Unit    | Scale | Data Type
        // ───────────────────────────────────────────────────────────────────────
        //    0      | Steam Header Pressure        | bar     | x10   | INT16
        //    1      | Steam Temperature            | °C      | x10   | INT16
        //    2      | Steam Flow Rate              | kg/h    | x1    | INT16
        //    3      | Condensate Return Temp       | °C      | x10   | INT16
        //    4      | Boiler Status                | -       | -     | INT16 (0=Off,1=On,2=Fault)
        //    5      | Boiler Pressure              | bar     | x10   | INT16
        //    6      | Feedwater Temperature        | °C      | x10   | INT16
        //    7      | Steam Demand (from plant)    | %       | x10   | INT16
        //    8      | Boiler Efficiency            | %       | x10   | INT16
        //    9      | Steam Energy (High Word)     | kWh     | x1    | UINT16 (High)
        //   10      | Steam Energy (Low Word)      | kWh     | x1    | UINT16 (Low)
        //
        // COOLING WATER SYSTEM (Registers 20-39)
        // ───────────────────────────────────────────────────────────────────────
        // Register  | Description                  | Unit    | Scale | Data Type
        // ───────────────────────────────────────────────────────────────────────
        //   20      | Supply Temperature           | °C      | x10   | INT16
        //   21      | Return Temperature           | °C      | x10   | INT16
        //   22      | Flow Rate                    | m³/h    | x10   | INT16
        //   23      | Pump 1 Status                | -       | -     | INT16 (0=Off,1=On,2=Fault)
        //   24      | Pump 2 Status                | -       | -     | INT16 (0=Off,1=On,2=Fault)
        //   25      | Cooling Tower Fan 1 Status   | -       | -     | INT16 (0=Off,1=On,2=Fault)
        //   26      | Cooling Tower Fan 2 Status   | -       | -     | INT16 (0=Off,1=On,2=Fault)
        //   27      | Basin Level                  | %       | x10   | INT16
        //   28      | Conductivity                 | µS/cm   | x1    | INT16
        //   29      | Cooling Demand (from plant)  | %       | x10   | INT16
        //   30      | Cooling Energy (High Word)   | kWh     | x1    | UINT16 (High)
        //   31      | Cooling Energy (Low Word)    | kWh     | x1    | UINT16 (Low)
        //
        // SYSTEM STATUS (Registers 40-49)
        // ───────────────────────────────────────────────────────────────────────
        //   40      | Utilities System Status      | -       | -     | INT16 (0=OK,1=Warning,2=Alarm)
        //   41      | Active Alarms Count          | -       | -     | INT16
        //   42      | Simulation Running           | -       | -     | INT16 (0=No,1=Yes)
        //   43      | Heartbeat Counter            | -       | -     | UINT16
        //
        // ═══════════════════════════════════════════════════════════════════════

        // Steam System State
        private double _steamHeaderPressure = 6.0;      // bar
        private double _steamTemperature = 165.0;       // °C (saturated at 6 bar)
        private double _steamFlowRate = 0.0;            // kg/h
        private double _condensateReturnTemp = 80.0;    // °C
        private int _boilerStatus = 1;                  // 0=Off, 1=On, 2=Fault
        private double _boilerPressure = 7.0;           // bar
        private double _feedwaterTemp = 60.0;           // °C
        private double _steamDemand = 0.0;              // % (from batch process)
        private double _boilerEfficiency = 92.0;        // %
        private double _steamEnergyTotal = 0.0;         // kWh

        // Cooling Water System State
        private double _cwSupplyTemp = 25.0;            // °C
        private double _cwReturnTemp = 30.0;            // °C
        private double _cwFlowRate = 0.0;               // m³/h
        private int _cwPump1Status = 1;                 // 0=Off, 1=On, 2=Fault
        private int _cwPump2Status = 0;                 // 0=Off, 1=On, 2=Fault (standby)
        private int _ctFan1Status = 1;                  // 0=Off, 1=On, 2=Fault
        private int _ctFan2Status = 0;                  // 0=Off, 1=On, 2=Fault (standby)
        private double _basinLevel = 85.0;              // %
        private double _conductivity = 450.0;           // µS/cm
        private double _coolingDemand = 0.0;            // % (from batch process)
        private double _coolingEnergyTotal = 0.0;       // kWh

        // System State
        private int _systemStatus = 0;                  // 0=OK, 1=Warning, 2=Alarm
        private int _activeAlarms = 0;
        private ushort _heartbeat = 0;

        public UtilitiesController(int modbusPort = DefaultModbusPort)
        {
            _modbusPort = modbusPort;
            _modbusServer = new ModbusTcpServer();
        }

        /// <summary>
        /// Start the Modbus TCP server and utilities simulation.
        /// </summary>
        public void Start()
        {
            Console.WriteLine($"[...] Starting Utilities Modbus TCP Server on port {_modbusPort}...");

            try
            {
                // Add unit identifier before starting
                _modbusServer.AddUnit(UnitIdentifier);

                _modbusServer.Start(new IPEndPoint(IPAddress.Any, _modbusPort));
                Console.WriteLine($"[OK] Modbus TCP Server started on port {_modbusPort}");

                // Initialize registers
                InitializeRegisters();

                // Start simulation loop
                _cts = new CancellationTokenSource();
                _simulationTask = Task.Run(() => SimulationLoopAsync(_cts.Token));

                Console.WriteLine("[OK] Utilities simulation started");
                PrintRegisterMap();
            }
            catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
            {
                Console.WriteLine($"[ERROR] Port {_modbusPort} is already in use.");
                Console.WriteLine($"        Try a different port or stop the conflicting application.");
                throw;
            }
            catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.AccessDenied)
            {
                Console.WriteLine($"[ERROR] Access denied for port {_modbusPort}.");
                Console.WriteLine($"        Ports below 1024 require administrator privileges.");
                Console.WriteLine($"        Run as Administrator or use a port above 1024.");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to start Modbus server: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stop the Modbus TCP server and simulation.
        /// </summary>
        public void Stop()
        {
            Console.WriteLine("[...] Stopping Utilities simulation...");

            _cts?.Cancel();

            try
            {
                _simulationTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { }

            _modbusServer.Stop();
            Console.WriteLine("[OK] Utilities Modbus TCP Server stopped");
        }

        /// <summary>
        /// Set the steam demand from the batch process (0-100%).
        /// </summary>
        public void SetSteamDemand(double demandPercent)
        {
            _steamDemand = Math.Clamp(demandPercent, 0, 100);
        }

        /// <summary>
        /// Set the cooling demand from the batch process (0-100%).
        /// </summary>
        public void SetCoolingDemand(double demandPercent)
        {
            _coolingDemand = Math.Clamp(demandPercent, 0, 100);
        }

        private void InitializeRegisters()
        {
            // Get reference to holding registers
            var registers = _modbusServer.GetHoldingRegisters(UnitIdentifier);

            // Initialize all registers to zero
            for (int i = 0; i < 50; i++)
            {
                registers.SetLittleEndian<short>((ushort)(i * 2), 0);
            }

            // Set simulation running flag
            registers.SetLittleEndian<short>(42 * 2, 1);
        }

        private async Task SimulationLoopAsync(CancellationToken ct)
        {
            const int updateIntervalMs = 1000; // 1 second update rate
            DateTime lastUpdate = DateTime.Now;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    double dtSeconds = (DateTime.Now - lastUpdate).TotalSeconds;
                    lastUpdate = DateTime.Now;

                    // Simulate steam system
                    SimulateSteamSystem(dtSeconds);

                    // Simulate cooling water system
                    SimulateCoolingWaterSystem(dtSeconds);

                    // Update system status
                    UpdateSystemStatus();

                    // Write all values to Modbus registers
                    UpdateModbusRegisters();

                    // Increment heartbeat
                    _heartbeat++;

                    await Task.Delay(updateIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Utilities simulation error: {ex.Message}");
                    await Task.Delay(1000, ct);
                }
            }
        }

        private void SimulateSteamSystem(double dtSeconds)
        {
            // Boiler pressure control - maintains ~7 bar
            if (_boilerStatus == 1)
            {
                double pressureSetpoint = 7.0;
                double pressureError = pressureSetpoint - _boilerPressure;
                _boilerPressure += pressureError * 0.1 + (_rand.NextDouble() - 0.5) * 0.05;
                _boilerPressure = Math.Clamp(_boilerPressure, 5.0, 8.0);
            }

            // Steam header pressure follows boiler with losses
            double headerTarget = _boilerPressure - 0.5 - (_steamDemand / 100.0) * 1.0;
            _steamHeaderPressure += (headerTarget - _steamHeaderPressure) * 0.2;
            _steamHeaderPressure += (_rand.NextDouble() - 0.5) * 0.02;
            _steamHeaderPressure = Math.Clamp(_steamHeaderPressure, 4.0, 7.5);

            // Steam temperature (saturated steam at header pressure)
            // Using simplified steam table approximation: T ≈ 100 + 20*P (rough)
            _steamTemperature = 100 + 15 * _steamHeaderPressure + (_rand.NextDouble() - 0.5) * 2.0;

            // Steam flow rate based on demand
            double maxSteamFlow = 500.0; // kg/h max capacity
            double targetFlow = (_steamDemand / 100.0) * maxSteamFlow;
            _steamFlowRate += (targetFlow - _steamFlowRate) * 0.3;
            _steamFlowRate += (_rand.NextDouble() - 0.5) * 5.0;
            _steamFlowRate = Math.Max(0, _steamFlowRate);

            // Condensate return temperature
            double condensateTarget = 70 + (_steamDemand / 100.0) * 20; // Higher return temp at higher load
            _condensateReturnTemp += (condensateTarget - _condensateReturnTemp) * 0.1;
            _condensateReturnTemp += (_rand.NextDouble() - 0.5) * 1.0;
            _condensateReturnTemp = Math.Clamp(_condensateReturnTemp, 60, 95);

            // Feedwater temperature (preheated by economizer)
            _feedwaterTemp = 50 + (_steamDemand / 100.0) * 30 + (_rand.NextDouble() - 0.5) * 2.0;
            _feedwaterTemp = Math.Clamp(_feedwaterTemp, 40, 90);

            // Boiler efficiency varies with load
            _boilerEfficiency = 85 + (_steamDemand / 100.0) * 10 - Math.Abs(_steamDemand - 70) * 0.05;
            _boilerEfficiency += (_rand.NextDouble() - 0.5) * 0.5;
            _boilerEfficiency = Math.Clamp(_boilerEfficiency, 80, 95);

            // Energy calculation (simplified)
            // Steam energy ≈ flow rate * enthalpy change / efficiency
            double steamPowerKw = (_steamFlowRate * 2200) / 3600.0; // ~2200 kJ/kg enthalpy
            _steamEnergyTotal += steamPowerKw * (dtSeconds / 3600.0);

            // Random boiler fault (very rare)
            if (_rand.NextDouble() < 0.00001)
            {
                _boilerStatus = 2; // Fault
                Console.WriteLine("[ALARM] Utilities: Boiler fault detected!");
            }
        }

        private void SimulateCoolingWaterSystem(double dtSeconds)
        {
            // Ambient temperature affects cooling tower performance
            double ambientTemp = 20.0 + Math.Sin(DateTime.Now.Hour * Math.PI / 12) * 5; // Daily variation

            // Cooling water supply temperature (from cooling tower)
            double approachTemp = 5.0; // Tower approach temperature
            double supplyTarget = ambientTemp + approachTemp;

            // Fan operation affects supply temperature
            int fansRunning = (_ctFan1Status == 1 ? 1 : 0) + (_ctFan2Status == 1 ? 1 : 0);
            if (fansRunning == 0) supplyTarget += 5.0; // No fans = poor cooling
            if (fansRunning == 2) supplyTarget -= 2.0; // Both fans = better cooling

            _cwSupplyTemp += (supplyTarget - _cwSupplyTemp) * 0.1;
            _cwSupplyTemp += (_rand.NextDouble() - 0.5) * 0.3;
            _cwSupplyTemp = Math.Clamp(_cwSupplyTemp, 15, 35);

            // Flow rate based on demand and pump status
            double maxFlowRate = 50.0; // m³/h per pump
            double pumpCapacity = (_cwPump1Status == 1 ? 1 : 0) + (_cwPump2Status == 1 ? 0.8 : 0);
            double targetFlow = (_coolingDemand / 100.0) * maxFlowRate * pumpCapacity;
            _cwFlowRate += (targetFlow - _cwFlowRate) * 0.2;
            _cwFlowRate += (_rand.NextDouble() - 0.5) * 0.5;
            _cwFlowRate = Math.Max(0, _cwFlowRate);

            // Return temperature based on heat load
            double heatLoad = _coolingDemand * 10; // kW
            double deltaT = _cwFlowRate > 1 ? heatLoad / (_cwFlowRate * 1.163) : 0; // Q = m*Cp*dT
            _cwReturnTemp = _cwSupplyTemp + deltaT;
            _cwReturnTemp += (_rand.NextDouble() - 0.5) * 0.5;
            _cwReturnTemp = Math.Clamp(_cwReturnTemp, _cwSupplyTemp, _cwSupplyTemp + 15);

            // Basin level (makeup water vs evaporation)
            double evaporationRate = _cwFlowRate * 0.001 * fansRunning; // Simplified
            double makeupRate = evaporationRate * 0.95; // Slightly less than evaporation
            _basinLevel += (makeupRate - evaporationRate) * dtSeconds * 0.1;
            _basinLevel += (_rand.NextDouble() - 0.5) * 0.1;
            _basinLevel = Math.Clamp(_basinLevel, 50, 100);

            // Conductivity increases over time (concentration)
            _conductivity += 0.01 * dtSeconds;
            _conductivity += (_rand.NextDouble() - 0.5) * 2;
            _conductivity = Math.Clamp(_conductivity, 300, 800);

            // Blowdown when conductivity too high
            if (_conductivity > 700)
            {
                _conductivity -= 50;
                _basinLevel -= 2;
            }

            // Cooling energy calculation
            double coolingPowerKw = heatLoad;
            _coolingEnergyTotal += coolingPowerKw * (dtSeconds / 3600.0);

            // Auto-start standby equipment on high demand
            if (_coolingDemand > 70 && _cwPump2Status == 0)
            {
                _cwPump2Status = 1;
                Console.WriteLine("[INFO] Utilities: Standby CW pump started (high demand)");
            }
            if (_coolingDemand < 40 && _cwPump2Status == 1)
            {
                _cwPump2Status = 0;
                Console.WriteLine("[INFO] Utilities: Standby CW pump stopped (low demand)");
            }

            if (_cwReturnTemp > 35 && _ctFan2Status == 0)
            {
                _ctFan2Status = 1;
                Console.WriteLine("[INFO] Utilities: Standby CT fan started (high return temp)");
            }
            if (_cwReturnTemp < 28 && _ctFan2Status == 1)
            {
                _ctFan2Status = 0;
                Console.WriteLine("[INFO] Utilities: Standby CT fan stopped (normal temp)");
            }
        }

        private void UpdateSystemStatus()
        {
            _activeAlarms = 0;
            _systemStatus = 0;

            // Check for warnings and alarms
            if (_steamHeaderPressure < 5.0)
            {
                _activeAlarms++;
                _systemStatus = Math.Max(_systemStatus, 1);
            }
            if (_steamHeaderPressure < 4.5)
            {
                _activeAlarms++;
                _systemStatus = 2;
            }
            if (_boilerStatus == 2)
            {
                _activeAlarms++;
                _systemStatus = 2;
            }
            if (_cwSupplyTemp > 32)
            {
                _activeAlarms++;
                _systemStatus = Math.Max(_systemStatus, 1);
            }
            if (_basinLevel < 60)
            {
                _activeAlarms++;
                _systemStatus = Math.Max(_systemStatus, 1);
            }
            if (_conductivity > 700)
            {
                _activeAlarms++;
                _systemStatus = Math.Max(_systemStatus, 1);
            }
        }

        private void UpdateModbusRegisters()
        {
            var registers = _modbusServer.GetHoldingRegisters(UnitIdentifier);

            // Steam System (Registers 0-10)
            registers.SetLittleEndian<short>(0 * 2, (short)(_steamHeaderPressure * 10));
            registers.SetLittleEndian<short>(1 * 2, (short)(_steamTemperature * 10));
            registers.SetLittleEndian<short>(2 * 2, (short)_steamFlowRate);
            registers.SetLittleEndian<short>(3 * 2, (short)(_condensateReturnTemp * 10));
            registers.SetLittleEndian<short>(4 * 2, (short)_boilerStatus);
            registers.SetLittleEndian<short>(5 * 2, (short)(_boilerPressure * 10));
            registers.SetLittleEndian<short>(6 * 2, (short)(_feedwaterTemp * 10));
            registers.SetLittleEndian<short>(7 * 2, (short)(_steamDemand * 10));
            registers.SetLittleEndian<short>(8 * 2, (short)(_boilerEfficiency * 10));

            // Steam energy as 32-bit value split into two 16-bit registers
            uint steamEnergyScaled = (uint)_steamEnergyTotal;
            registers.SetLittleEndian<ushort>(9 * 2, (ushort)(steamEnergyScaled >> 16));
            registers.SetLittleEndian<ushort>(10 * 2, (ushort)(steamEnergyScaled & 0xFFFF));

            // Cooling Water System (Registers 20-31)
            registers.SetLittleEndian<short>(20 * 2, (short)(_cwSupplyTemp * 10));
            registers.SetLittleEndian<short>(21 * 2, (short)(_cwReturnTemp * 10));
            registers.SetLittleEndian<short>(22 * 2, (short)(_cwFlowRate * 10));
            registers.SetLittleEndian<short>(23 * 2, (short)_cwPump1Status);
            registers.SetLittleEndian<short>(24 * 2, (short)_cwPump2Status);
            registers.SetLittleEndian<short>(25 * 2, (short)_ctFan1Status);
            registers.SetLittleEndian<short>(26 * 2, (short)_ctFan2Status);
            registers.SetLittleEndian<short>(27 * 2, (short)(_basinLevel * 10));
            registers.SetLittleEndian<short>(28 * 2, (short)_conductivity);
            registers.SetLittleEndian<short>(29 * 2, (short)(_coolingDemand * 10));

            // Cooling energy as 32-bit value
            uint coolingEnergyScaled = (uint)_coolingEnergyTotal;
            registers.SetLittleEndian<ushort>(30 * 2, (ushort)(coolingEnergyScaled >> 16));
            registers.SetLittleEndian<ushort>(31 * 2, (ushort)(coolingEnergyScaled & 0xFFFF));

            // System Status (Registers 40-43)
            registers.SetLittleEndian<short>(40 * 2, (short)_systemStatus);
            registers.SetLittleEndian<short>(41 * 2, (short)_activeAlarms);
            registers.SetLittleEndian<short>(42 * 2, 1); // Simulation running
            registers.SetLittleEndian<ushort>(43 * 2, _heartbeat);
        }

        private void PrintRegisterMap()
        {
            Console.WriteLine();
            Console.WriteLine("Utilities Modbus TCP Register Map:");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("  STEAM SYSTEM (Registers 0-10)");
            Console.WriteLine("  ───────────────────────────────────────────────────────────");
            Console.WriteLine("    0  : Steam Header Pressure    (bar × 10)");
            Console.WriteLine("    1  : Steam Temperature        (°C × 10)");
            Console.WriteLine("    2  : Steam Flow Rate          (kg/h)");
            Console.WriteLine("    3  : Condensate Return Temp   (°C × 10)");
            Console.WriteLine("    4  : Boiler Status            (0=Off, 1=On, 2=Fault)");
            Console.WriteLine("    5  : Boiler Pressure          (bar × 10)");
            Console.WriteLine("    6  : Feedwater Temperature    (°C × 10)");
            Console.WriteLine("    7  : Steam Demand             (% × 10)");
            Console.WriteLine("    8  : Boiler Efficiency        (% × 10)");
            Console.WriteLine("    9-10: Steam Energy Total      (kWh, 32-bit)");
            Console.WriteLine();
            Console.WriteLine("  COOLING WATER SYSTEM (Registers 20-31)");
            Console.WriteLine("  ───────────────────────────────────────────────────────────");
            Console.WriteLine("   20  : CW Supply Temperature    (°C × 10)");
            Console.WriteLine("   21  : CW Return Temperature    (°C × 10)");
            Console.WriteLine("   22  : CW Flow Rate             (m³/h × 10)");
            Console.WriteLine("   23  : CW Pump 1 Status         (0=Off, 1=On, 2=Fault)");
            Console.WriteLine("   24  : CW Pump 2 Status         (0=Off, 1=On, 2=Fault)");
            Console.WriteLine("   25  : CT Fan 1 Status          (0=Off, 1=On, 2=Fault)");
            Console.WriteLine("   26  : CT Fan 2 Status          (0=Off, 1=On, 2=Fault)");
            Console.WriteLine("   27  : Basin Level              (% × 10)");
            Console.WriteLine("   28  : Conductivity             (µS/cm)");
            Console.WriteLine("   29  : Cooling Demand           (% × 10)");
            Console.WriteLine("   30-31: Cooling Energy Total    (kWh, 32-bit)");
            Console.WriteLine();
            Console.WriteLine("  SYSTEM STATUS (Registers 40-43)");
            Console.WriteLine("  ───────────────────────────────────────────────────────────");
            Console.WriteLine("   40  : System Status            (0=OK, 1=Warning, 2=Alarm)");
            Console.WriteLine("   41  : Active Alarms Count");
            Console.WriteLine("   42  : Simulation Running       (0=No, 1=Yes)");
            Console.WriteLine("   43  : Heartbeat Counter");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"  Connection: localhost:{_modbusPort}  |  Unit ID: {UnitIdentifier}  |  FC03/FC16");
            Console.WriteLine();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                    _cts?.Dispose();
                    _modbusServer.Dispose();
                }
                _disposed = true;
            }
        }
    }
}