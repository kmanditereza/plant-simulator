using BatchPlant;
using BatchPlant.Helpers;
using BatchPlant.Alarms;

namespace BatchPlant.Operations
{
    /// <summary>
    /// Controls Stage 2: Heat Exchanger operation.
    /// Integrates with OPC UA Alarms &amp; Conditions (Part 9).
    /// </summary>
    public class HeatExchangerController
    {
        private readonly Random _rand = new();

        // Process parameters
        private const double TargetTemperature = 90.0;
        private const double SteamTemperature = 133.0;
        private const double FlowRateSetpoint = 25.0;
        private const double TransitionTemperature = 85.0;

        // PID parameters
        private const double Kp = 2.5;
        private const double Ki = 0.8;
        private const double Kd = 0.2;

        // PID state
        private double _pidIntegral = 0.0;
        private double _pidPreviousError = 0.0;

        /// <summary>
        /// Execute the heat exchanger operation.
        /// </summary>
        public async Task ExecuteAsync()
        {
            Console.WriteLine("=== Stage 2: Heat Exchanger Operation Started ===");

            // Pre-condition check (disabled for demo)
            double currentHomogeneity = Convert.ToDouble(BatchPlantNodeManager.TURB201.Value);
            if (currentHomogeneity < 95.0)
            {
                Console.WriteLine($"[INFO] Homogeneity {currentHomogeneity:F1}% < 95% - Proceeding anyway (interlocks disabled)");
            }

            InitializeHeatExchangerState();

            double initialTemperature = Convert.ToDouble(BatchPlantNodeManager.TT201.Value);
            _pidPreviousError = TargetTemperature - initialTemperature;

            int heatingDurationSeconds = Convert.ToInt32(BatchPlantNodeManager.heating_time.Value);
            if (heatingDurationSeconds <= 0) heatingDurationSeconds = 1200;

            int updateIntervalMs = 2000;
            int totalSteps = (heatingDurationSeconds * 1000) / updateIntervalMs;

            double bulkTemperature = initialTemperature;
            double pressureDrop = Convert.ToDouble(BatchPlantNodeManager.HX_BASELINE_DP.Value);
            double baselinePressureDrop = pressureDrop;
            double foulingIndex = 0.0;
            double energyConsumed = Convert.ToDouble(BatchPlantNodeManager.hx_energy_consumed.Value);

            double mixerVolume = Convert.ToDouble(BatchPlantNodeManager.LT201.Value);
            double productDensity = 1.05;
            double specificHeat = 3.9;
            double productMass = mixerVolume * productDensity;

            Console.WriteLine($"Heating Duration: {heatingDurationSeconds} seconds");
            Console.WriteLine($"Initial Temperature: {initialTemperature:F1}°C");
            Console.WriteLine($"Target Temperature: {TargetTemperature:F1}°C");
            Console.WriteLine($"Product Mass: {productMass:F1} kg");

            StartEquipment();

            for (int step = 0; step <= totalSteps; step++)
            {
                double progress = (double)step / totalSteps;
                double elapsedMinutes = (progress * heatingDurationSeconds) / 60.0;
                double dtSeconds = updateIntervalMs / 1000.0;

                double actualFlowRate = SimulateFlowRate(foulingIndex);
                UaWriteHelpers.Write(BatchPlantNodeManager.FT301, Math.Round(actualFlowRate, 1));

                double steamValvePosition = CalculatePidOutput(bulkTemperature, dtSeconds);
                UaWriteHelpers.Write(BatchPlantNodeManager.SV301, Math.Round(steamValvePosition, 1));

                double steamFlow = SimulateSteamFlow(steamValvePosition);
                UaWriteHelpers.Write(BatchPlantNodeManager.FT302, Math.Round(steamFlow, 1));

                var (newBulkTemp, heatInputKw) = CalculateHeatTransfer(
                    steamFlow, actualFlowRate, foulingIndex, bulkTemperature, productMass, specificHeat, dtSeconds);
                bulkTemperature = newBulkTemp;

                var (inletTemp, outletTemp) = SimulateTemperatures(bulkTemperature, steamValvePosition, actualFlowRate);
                UaWriteHelpers.Write(BatchPlantNodeManager.TT301, Math.Round(inletTemp, 2));
                UaWriteHelpers.Write(BatchPlantNodeManager.TT302, Math.Round(outletTemp, 2));
                UaWriteHelpers.Write(BatchPlantNodeManager.TT201, Math.Round(bulkTemperature, 2));

                (pressureDrop, foulingIndex) = UpdateFouling(bulkTemperature, actualFlowRate, baselinePressureDrop, foulingIndex);
                UaWriteHelpers.Write(BatchPlantNodeManager.PT301, Math.Round(pressureDrop, 3));
                UaWriteHelpers.Write(BatchPlantNodeManager.HX_FOULING_INDEX, Math.Round(foulingIndex, 1));

                UpdateViscosity(bulkTemperature, initialTemperature);

                double pumpPowerKw = 2.2;
                double totalPowerKw = heatInputKw + pumpPowerKw;
                energyConsumed += totalPowerKw * (updateIntervalMs / 1000.0) / 3600.0;
                UaWriteHelpers.Write(BatchPlantNodeManager.hx_energy_consumed, Math.Round(energyConsumed, 4));

                // Check alarms - both OPC UA and console
                CheckAlarmsAndWarnings(pressureDrop, baselinePressureDrop, actualFlowRate, bulkTemperature, foulingIndex, ref steamValvePosition);

                LogProgress(step, totalSteps, elapsedMinutes, bulkTemperature, inletTemp, outletTemp, steamValvePosition, actualFlowRate, initialTemperature);

                await Task.Delay(updateIntervalMs);
            }

            FinalizeHeatExchangerState(bulkTemperature, energyConsumed, foulingIndex);
        }

        private void InitializeHeatExchangerState()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Heating");
            UaWriteHelpers.Write(BatchPlantNodeManager.hx_status, "Heating");
            UaWriteHelpers.Write(BatchPlantNodeManager.HX301_STATE, 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.HX301_MODE, 2);
        }

        private void StartEquipment()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.P301_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.P301_RUN, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.P301_SPEED, 100.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_RPM, 30.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT201_RUN, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.mixer_status, "Gentle Agitation - Heating");

            Console.WriteLine(">>> Recirculation pump started");
            Console.WriteLine(">>> Steam valve opening...");
        }

        private double SimulateFlowRate(double foulingIndex)
        {
            double actualFlowRate = FlowRateSetpoint + (_rand.NextDouble() - 0.5) * 2.0;
            actualFlowRate *= (1.0 - foulingIndex * 0.002);
            return Math.Max(15.0, actualFlowRate);
        }

        private double CalculatePidOutput(double bulkTemperature, double dtSeconds)
        {
            double error = TargetTemperature - bulkTemperature;

            _pidIntegral += error * dtSeconds;
            _pidIntegral = Math.Max(-50, Math.Min(50, _pidIntegral));

            double derivative = (error - _pidPreviousError) / dtSeconds;
            _pidPreviousError = error;

            double pidOutput = (Kp * error) + (Ki * _pidIntegral) + (Kd * derivative);
            UaWriteHelpers.Write(BatchPlantNodeManager.PID_HX_OUTPUT, Math.Round(pidOutput, 1));

            return Math.Max(0, Math.Min(100, pidOutput));
        }

        private double SimulateSteamFlow(double steamValvePosition)
        {
            double maxSteamFlow = 50.0;
            double steamFlow = (steamValvePosition / 100.0) * maxSteamFlow;
            steamFlow += (_rand.NextDouble() - 0.5) * 2.0;
            return Math.Max(0, steamFlow);
        }

        private (double newBulkTemp, double heatInputKw) CalculateHeatTransfer(
            double steamFlow, double actualFlowRate, double foulingIndex,
            double bulkTemperature, double productMass, double specificHeat, double dtSeconds)
        {
            double latentHeat = 2163.0;
            double heatInputKw = (steamFlow / 3600.0) * latentHeat;

            double heatTransferEfficiency = 0.85 * (1.0 - foulingIndex * 0.003);

            bool lowFlowCondition = actualFlowRate < 20.0;
            if (lowFlowCondition)
            {
                heatTransferEfficiency *= 0.7;
            }

            double effectiveHeatKw = heatInputKw * heatTransferEfficiency;
            double tempRiseRate = effectiveHeatKw / (productMass * specificHeat / 1000.0);

            double newBulkTemp = bulkTemperature + tempRiseRate * dtSeconds;
            newBulkTemp = Math.Min(TargetTemperature + 2.0, newBulkTemp);
            newBulkTemp += (_rand.NextDouble() - 0.5) * 0.1;

            return (newBulkTemp, heatInputKw);
        }

        private (double inletTemp, double outletTemp) SimulateTemperatures(
            double bulkTemperature, double steamValvePosition, double actualFlowRate)
        {
            double inletTemp = bulkTemperature + (_rand.NextDouble() - 0.5) * 0.3;

            double hxDeltaT = (steamValvePosition / 100.0) * 15.0 * (25.0 / Math.Max(15, actualFlowRate));
            hxDeltaT = Math.Min(hxDeltaT, SteamTemperature - inletTemp - 10);
            double outletTemp = inletTemp + hxDeltaT;
            outletTemp += (_rand.NextDouble() - 0.5) * 0.5;

            return (inletTemp, outletTemp);
        }

        private (double pressureDrop, double foulingIndex) UpdateFouling(
            double bulkTemperature, double actualFlowRate, double baselinePressureDrop, double foulingIndex)
        {
            double foulingRate = 0.001 * (bulkTemperature / 50.0);
            if (actualFlowRate < 20.0) foulingRate *= 3.0;
            foulingIndex += foulingRate;
            foulingIndex = Math.Min(100, foulingIndex);

            double pressureDrop = baselinePressureDrop * (1.0 + foulingIndex * 0.01);
            pressureDrop += (_rand.NextDouble() - 0.5) * 0.02;

            return (pressureDrop, foulingIndex);
        }

        private void UpdateViscosity(double bulkTemperature, double initialTemperature)
        {
            double currentViscosity = Convert.ToDouble(BatchPlantNodeManager.VISC201.Value);
            double viscosityTempFactor = Math.Pow(0.97, (bulkTemperature - initialTemperature) / 10.0);
            double newViscosity = currentViscosity * viscosityTempFactor;
            newViscosity += (_rand.NextDouble() - 0.5) * 20;
            newViscosity = Math.Max(500, newViscosity);
            UaWriteHelpers.Write(BatchPlantNodeManager.VISC201, Math.Round(newViscosity, 1));
        }

        private void CheckAlarmsAndWarnings(double pressureDrop, double baselinePressureDrop,
            double actualFlowRate, double bulkTemperature, double foulingIndex, ref double steamValvePosition)
        {
            // ═══════════════════════════════════════════════════════════════════════
            // OPC UA ALARM CHECKS (Part 9 Alarms & Conditions)
            // ═══════════════════════════════════════════════════════════════════════

            // Check fouling index (ALM-100, ALM-101)
            AlarmHelper.CheckHxFouling(foulingIndex);

            // Check product flow rate (ALM-102, ALM-103)
            AlarmHelper.CheckProductFlow(actualFlowRate);

            // Check temperature (ALM-104, ALM-105)
            AlarmHelper.CheckHxTemperature(bulkTemperature);

            // ═══════════════════════════════════════════════════════════════════════
            // CONSOLE LOGGING (kept for operator visibility)
            // ═══════════════════════════════════════════════════════════════════════

            // Fouling detection
            double pressureDropIncrease = (pressureDrop - baselinePressureDrop) / baselinePressureDrop * 100;
            if (foulingIndex > 30 && foulingIndex <= 50)
            {
                Console.WriteLine($"[ALARM ALM-100] Heat exchanger fouling detected: Fouling index {foulingIndex:F1}%");
            }
            else if (foulingIndex > 50)
            {
                Console.WriteLine($"[ALARM ALM-101] Heat exchanger CRITICAL fouling: Fouling index {foulingIndex:F1}% - CLEANING REQUIRED");
            }

            // Low flow warnings
            if (actualFlowRate < 18.0 && actualFlowRate >= 12.0)
            {
                Console.WriteLine($"[ALARM ALM-102] Low product flow: {actualFlowRate:F1} L/min - Hot spot risk!");
            }
            else if (actualFlowRate < 12.0)
            {
                Console.WriteLine($"[ALARM ALM-103] CRITICAL: Low product flow: {actualFlowRate:F1} L/min - Reduce steam!");
                steamValvePosition = Math.Min(steamValvePosition, 30.0);
            }

            // Temperature warnings
            if (bulkTemperature > TargetTemperature + 1.0 && bulkTemperature <= 95.0)
            {
                Console.WriteLine($"[ALARM ALM-104] Temperature overshoot: {bulkTemperature:F1}°C");
            }
            else if (bulkTemperature > 95.0)
            {
                Console.WriteLine($"[ALARM ALM-105] CRITICAL: High temperature: {bulkTemperature:F1}°C - Product damage risk!");
            }
        }

        private void LogProgress(int step, int totalSteps, double elapsedMinutes,
            double bulkTemperature, double inletTemp, double outletTemp,
            double steamValvePosition, double actualFlowRate, double initialTemperature)
        {
            if (step % 15 == 0 || step == totalSteps)
            {
                double actualRampRate = (bulkTemperature - initialTemperature) / Math.Max(0.1, elapsedMinutes);
                Console.WriteLine($"[{elapsedMinutes:F1} min] Bulk: {bulkTemperature:F1}°C | " +
                                $"In: {inletTemp:F1}°C | Out: {outletTemp:F1}°C | " +
                                $"Steam: {steamValvePosition:F0}% | Flow: {actualFlowRate:F1} L/min | " +
                                $"Ramp: {actualRampRate:F2}°C/min");
            }

            if (bulkTemperature >= 50.0 && bulkTemperature < 51.0)
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.hx_status, "Heating - 50°C reached");
                Console.WriteLine(">>> 50°C milestone reached");
            }
            else if (bulkTemperature >= 70.0 && bulkTemperature < 71.0)
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.hx_status, "Heating - 70°C reached");
                Console.WriteLine(">>> 70°C milestone reached");
            }
            else if (bulkTemperature >= TransitionTemperature && bulkTemperature < TransitionTemperature + 1.0)
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.hx_status, "Heating - Transfer Ready");
                Console.WriteLine(">>> 85°C reached - Ready for reactor transfer");
            }

            if (bulkTemperature >= TargetTemperature - 0.5)
            {
                Console.WriteLine($">>> Target temperature {TargetTemperature}°C reached at {elapsedMinutes:F1} min");
            }
        }

        private void FinalizeHeatExchangerState(double bulkTemperature, double energyConsumed, double foulingIndex)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.SV301, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FT302, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.HX301_STATE, 1);

            UaWriteHelpers.Write(BatchPlantNodeManager.P301_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.P301_RUN, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.P301_SPEED, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FT301, 0.0);

            UaWriteHelpers.Write(BatchPlantNodeManager.hx_status, "Heating Complete");
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Heating Complete - Ready for Reaction");

            Console.WriteLine("=== Stage 2: Heat Exchanger Operation Complete ===");
            Console.WriteLine($"Final Bulk Temperature: {bulkTemperature:F1}°C");
            Console.WriteLine($"Energy Consumed: {energyConsumed:F3} kWh");
            Console.WriteLine($"Fouling Index: {foulingIndex:F1}%");

            // Print alarm summary
            var (total, active, unacked, unconfirmed) = AlarmHelper.GetSummary();
            if (active > 0)
            {
                Console.WriteLine($">>> Active Alarms: {active} ({unacked} unacknowledged)");
            }

            Console.WriteLine(">>> Proceeding to Stage 3: Reaction/Cooking");

            // Reset PID state for next run
            _pidIntegral = 0.0;
            _pidPreviousError = 0.0;
        }
    }
}