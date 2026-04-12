using BatchPlant;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BatchPlant.Helpers;

namespace BatchPlant.Simulation
{
    /// <summary>
    /// Simulates sensor data during idle states for realistic OPC UA behavior.
    /// </summary>
    public class SensorSimulator
    {
        private readonly Random _rand = new();
        private Timer? _pollingTimer;

        /// <summary>
        /// Start the sensor simulation timer.
        /// </summary>
        /// <param name="intervalMs">Polling interval in milliseconds</param>
        public void Start(int intervalMs = 2000)
        {
            _pollingTimer = new Timer(SimulateSensorData, null, 0, intervalMs);
        }

        /// <summary>
        /// Stop the sensor simulation timer.
        /// </summary>
        public void Stop()
        {
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }

        private void SimulateSensorData(object? state)
        {
            SimulateRawMaterialTanks();
            SimulateMixerIdleState();
            SimulateHeatExchangerIdleState();
            SimulateReactorIdleState();
        }

        private void SimulateRawMaterialTanks()
        {
            var tank1Temp = Math.Round(20.0 + (_rand.NextDouble() - 0.5), 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.TT101, tank1Temp);

            var tank2Temp = Math.Round(20.0 + (_rand.NextDouble() - 0.5), 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.TT102, tank2Temp);

            var tank3Temp = Math.Round(19.0 + (_rand.NextDouble() - 0.5), 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.TT103, tank3Temp);

            var tank1Pressure = Math.Round(1.2 + (_rand.NextDouble() - 0.05), 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.PT101, tank1Pressure);

            var tank2Pressure = Math.Round(1.2 + (_rand.NextDouble() - 0.05), 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.PT102, tank2Pressure);

            var tank3Pressure = Math.Round(1.2 + (_rand.NextDouble() - 0.05), 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.PT103, tank3Pressure);
        }

        private void SimulateMixerIdleState()
        {
            int mixerState = Convert.ToInt32(BatchPlantNodeManager.MX101_STATE.Value);
            bool mixerRunning = Convert.ToBoolean(BatchPlantNodeManager.MT201_RUN.Value);
            string mixerStatus = BatchPlantNodeManager.mixer_status.Value?.ToString() ?? "Idle";

            bool isIdleState = (mixerState == 1) || (mixerState == 0) ||
                               (!mixerRunning && !mixerStatus.Contains("Mixing"));

            if (!isIdleState) return;

            double mixerLevel = Convert.ToDouble(BatchPlantNodeManager.LT201.Value);

            // Temperature simulation
            double baseTemp = 22.0;
            double currentTemp = Convert.ToDouble(BatchPlantNodeManager.TT201.Value);

            if (currentTemp > baseTemp + 0.5)
            {
                currentTemp -= 0.05;
            }
            currentTemp += (_rand.NextDouble() - 0.5) * 0.3;
            currentTemp = Math.Max(18.0, Math.Min(30.0, currentTemp));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT201, Math.Round(currentTemp, 2));

            // Viscosity simulation
            double currentViscosity = Convert.ToDouble(BatchPlantNodeManager.VISC201.Value);
            if (mixerLevel > 10.0)
            {
                double settlingFactor = 0.01 * _rand.NextDouble();
                currentViscosity += currentViscosity * settlingFactor;
                currentViscosity += (_rand.NextDouble() - 0.5) * 30;
                currentViscosity = Math.Max(100, Math.Min(20000, currentViscosity));
            }
            else
            {
                currentViscosity = 0.0;
            }
            UaWriteHelpers.Write(BatchPlantNodeManager.VISC201, Math.Round(currentViscosity, 1));

            // Homogeneity simulation
            double currentHomogeneity = Convert.ToDouble(BatchPlantNodeManager.TURB201.Value);
            if (mixerLevel > 10.0)
            {
                double separationRate = 0.02 * _rand.NextDouble();
                currentHomogeneity -= separationRate;
                currentHomogeneity += (_rand.NextDouble() - 0.5) * 0.5;
                currentHomogeneity = Math.Max(0, Math.Min(100, currentHomogeneity));
            }
            else
            {
                currentHomogeneity = 0.0;
            }
            UaWriteHelpers.Write(BatchPlantNodeManager.TURB201, Math.Round(currentHomogeneity, 1));

            // Motor parameters
            SimulateMixerMotorParameters(mixerRunning);
        }

        private void SimulateMixerMotorParameters(bool mixerRunning)
        {
            double idleRpm = Convert.ToDouble(BatchPlantNodeManager.MT201_RPM.Value);

            if (mixerRunning && idleRpm > 0)
            {
                double holdingSpeed = 20.0 + (_rand.NextDouble() - 0.5) * 2.0;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_RPM, Math.Round(holdingSpeed, 1));

                double idleTorque = 5.0 + (_rand.NextDouble() - 0.5) * 1.0;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_TRQ, Math.Round(idleTorque, 2));

                double angularVelocity = holdingSpeed * 2 * Math.PI / 60.0;
                double idlePower = (idleTorque * angularVelocity) / 1000.0;
                idlePower += (_rand.NextDouble() - 0.5) * 0.05;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_PWR, Math.Round(idlePower, 3));

                double voltage = 400.0;
                double powerFactor = 0.85;
                double idleAmperage = (idlePower * 1000) / (Math.Sqrt(3) * voltage * powerFactor);
                idleAmperage += (_rand.NextDouble() - 0.5) * 0.2;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_AMP, Math.Round(idleAmperage, 2));

                double idleVibration = 0.5 + (_rand.NextDouble() - 0.5) * 0.2;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_VIB, Math.Round(idleVibration, 3));
            }
            else
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_RPM, 0.0);
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_TRQ, 0.0);
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_PWR, 0.0);
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_AMP, 0.0);

                double ambientVibration = 0.02 + (_rand.NextDouble() * 0.03);
                UaWriteHelpers.Write(BatchPlantNodeManager.MT201_VIB, Math.Round(ambientVibration, 3));
            }
        }

        private void SimulateHeatExchangerIdleState()
        {
            int hxState = Convert.ToInt32(BatchPlantNodeManager.HX301_STATE.Value);
            bool hxPumpRunning = Convert.ToBoolean(BatchPlantNodeManager.P301_RUN.Value);
            string hxStatus = BatchPlantNodeManager.hx_status.Value?.ToString() ?? "Idle";

            bool isHxIdle = (hxState == 1) || (hxState == 0) ||
                           (!hxPumpRunning && !hxStatus.Contains("Heating") && !hxStatus.Contains("Cooling"));

            if (!isHxIdle) return;

            double ambientTemperature = 22.0;
            double mixerTemperature = Convert.ToDouble(BatchPlantNodeManager.TT201.Value);

            // Inlet temperature
            double currentInletTemp = Convert.ToDouble(BatchPlantNodeManager.TT301.Value);
            if (hxPumpRunning)
            {
                currentInletTemp += (mixerTemperature - currentInletTemp) * 0.1;
            }
            else
            {
                if (currentInletTemp > ambientTemperature + 0.5)
                {
                    double coolingRate = (currentInletTemp - ambientTemperature) * 0.002;
                    currentInletTemp -= coolingRate;
                }
                else if (currentInletTemp < ambientTemperature - 0.5)
                {
                    currentInletTemp += 0.02;
                }
            }
            currentInletTemp += (_rand.NextDouble() - 0.5) * 0.2;
            currentInletTemp = Math.Max(15.0, Math.Min(100.0, currentInletTemp));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT301, Math.Round(currentInletTemp, 2));

            // Outlet temperature
            double currentOutletTemp = Convert.ToDouble(BatchPlantNodeManager.TT302.Value);
            double steamValvePos = Convert.ToDouble(BatchPlantNodeManager.SV301.Value);
            if (steamValvePos < 1.0)
            {
                currentOutletTemp += (currentInletTemp - currentOutletTemp) * 0.15;
            }
            currentOutletTemp += (_rand.NextDouble() - 0.5) * 0.2;
            currentOutletTemp = Math.Max(15.0, Math.Min(100.0, currentOutletTemp));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT302, Math.Round(currentOutletTemp, 2));

            // Pressure drop
            double baselinePressureDrop = Convert.ToDouble(BatchPlantNodeManager.HX_BASELINE_DP.Value);
            double currentPressureDrop = Convert.ToDouble(BatchPlantNodeManager.PT301.Value);
            double currentFoulingIndex = Convert.ToDouble(BatchPlantNodeManager.HX_FOULING_INDEX.Value);

            if (hxPumpRunning)
            {
                double expectedPressureDrop = baselinePressureDrop * (1.0 + currentFoulingIndex * 0.01);
                currentPressureDrop += (expectedPressureDrop - currentPressureDrop) * 0.2;
                currentPressureDrop += (_rand.NextDouble() - 0.5) * 0.01;
            }
            else
            {
                currentPressureDrop += (0.05 - currentPressureDrop) * 0.1;
                currentPressureDrop += (_rand.NextDouble() - 0.5) * 0.005;
            }
            currentPressureDrop = Math.Max(0.0, currentPressureDrop);
            UaWriteHelpers.Write(BatchPlantNodeManager.PT301, Math.Round(currentPressureDrop, 3));

            // Flow rates
            if (hxPumpRunning)
            {
                double pumpSpeed = Convert.ToDouble(BatchPlantNodeManager.P301_SPEED.Value);
                double idleFlowRate = (pumpSpeed / 100.0) * 25.0;
                idleFlowRate += (_rand.NextDouble() - 0.5) * 1.0;
                idleFlowRate = Math.Max(0, idleFlowRate);
                UaWriteHelpers.Write(BatchPlantNodeManager.FT301, Math.Round(idleFlowRate, 1));
            }
            else
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.FT301, 0.0);
            }

            // Steam flow
            double currentSteamFlow = Convert.ToDouble(BatchPlantNodeManager.FT302.Value);
            if (steamValvePos < 1.0)
            {
                currentSteamFlow *= 0.5;
                if (currentSteamFlow < 0.1) currentSteamFlow = 0.0;
            }
            UaWriteHelpers.Write(BatchPlantNodeManager.FT302, Math.Round(currentSteamFlow, 1));

            // Cooling water
            int hxMode = Convert.ToInt32(BatchPlantNodeManager.HX301_MODE.Value);
            if (hxMode != 3)
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.FT303, 0.0);
            }

            // Valve positions in idle
            if (hxState == 1 || hxState == 0)
            {
                double currentValvePos = Convert.ToDouble(BatchPlantNodeManager.SV301.Value);
                if (currentValvePos > 0)
                {
                    currentValvePos *= 0.8;
                    if (currentValvePos < 0.5) currentValvePos = 0.0;
                    UaWriteHelpers.Write(BatchPlantNodeManager.SV301, Math.Round(currentValvePos, 1));
                }

                double currentPidOutput = Convert.ToDouble(BatchPlantNodeManager.PID_HX_OUTPUT.Value);
                if (currentPidOutput != 0)
                {
                    currentPidOutput *= 0.7;
                    if (Math.Abs(currentPidOutput) < 0.5) currentPidOutput = 0.0;
                    UaWriteHelpers.Write(BatchPlantNodeManager.PID_HX_OUTPUT, Math.Round(currentPidOutput, 1));
                }
            }

            // Fouling recovery when cool
            if (currentInletTemp < 40.0 && currentFoulingIndex > 0)
            {
                double recoveryRate = 0.001 * _rand.NextDouble();
                currentFoulingIndex -= recoveryRate;
                currentFoulingIndex = Math.Max(0, currentFoulingIndex);
                UaWriteHelpers.Write(BatchPlantNodeManager.HX_FOULING_INDEX, Math.Round(currentFoulingIndex, 1));
            }

            // Pump parameters
            if (!hxPumpRunning)
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.P301_SPEED, 0.0);

                bool currentFault = Convert.ToBoolean(BatchPlantNodeManager.P301_FAULT.Value);
                if (!currentFault && _rand.NextDouble() < 0.0001)
                {
                    UaWriteHelpers.Write(BatchPlantNodeManager.P301_FAULT, true);
                    Console.WriteLine("[FAULT] P301 Recirculation pump fault detected");
                }
            }
            else
            {
                double idlePumpSpeed = 20.0 + (_rand.NextDouble() - 0.5) * 2.0;
                UaWriteHelpers.Write(BatchPlantNodeManager.P301_SPEED, Math.Round(idlePumpSpeed, 1));
            }
        }

        private void SimulateReactorIdleState()
        {
            int reactorState = Convert.ToInt32(BatchPlantNodeManager.RX401_STATE.Value);
            bool agitatorRunning = Convert.ToBoolean(BatchPlantNodeManager.MT401_RUN.Value);
            bool vacuumPumpRunning = Convert.ToBoolean(BatchPlantNodeManager.VP401_RUN.Value);
            string reactorStatus = BatchPlantNodeManager.reactor_status.Value?.ToString() ?? "Idle";

            bool isReactorIdle = (reactorState == 1) || (reactorState == 0) ||
                                 (!agitatorRunning && !vacuumPumpRunning &&
                                  !reactorStatus.Contains("Reaction") &&
                                  !reactorStatus.Contains("Filling") &&
                                  !reactorStatus.Contains("Starch") &&
                                  !reactorStatus.Contains("pH") &&
                                  !reactorStatus.Contains("Maillard") &&
                                  !reactorStatus.Contains("Concentration"));

            if (!isReactorIdle) return;

            double ambientTemperature = 22.0;
            double reactorLevel = Convert.ToDouble(BatchPlantNodeManager.LT401.Value);

            SimulateReactorTemperature(ambientTemperature, reactorLevel);
            SimulateReactorPressure(vacuumPumpRunning);
            SimulateReactorJacket(reactorState, ambientTemperature);
            SimulateReactorAnalyticalInstruments(reactorLevel, ambientTemperature);
            SimulateReactorAgitator(agitatorRunning, reactorLevel);
            SimulateReactorVacuumPump(vacuumPumpRunning);
            SimulateReactorValves(reactorState);
            SimulateReactorAcidSystem(reactorState);
        }

        private void SimulateReactorTemperature(double ambientTemperature, double reactorLevel)
        {
            double currentReactorTemp = Convert.ToDouble(BatchPlantNodeManager.TT401.Value);

            if (reactorLevel > 10.0)
            {
                if (currentReactorTemp > ambientTemperature + 1.0)
                {
                    double coolingRate = (currentReactorTemp - ambientTemperature) * 0.001;
                    currentReactorTemp -= coolingRate;
                }
                else if (currentReactorTemp < ambientTemperature - 1.0)
                {
                    currentReactorTemp += 0.01;
                }
            }
            else
            {
                currentReactorTemp += (ambientTemperature - currentReactorTemp) * 0.05;
            }
            currentReactorTemp += (_rand.NextDouble() - 0.5) * 0.15;
            currentReactorTemp = Math.Max(15.0, Math.Min(100.0, currentReactorTemp));

            UaWriteHelpers.Write(BatchPlantNodeManager.TT401, Math.Round(currentReactorTemp, 2));

            // Multi-point temperatures
            double tempVariation = reactorLevel > 10.0 ? 0.5 : 0.2;
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_1, Math.Round(currentReactorTemp + (_rand.NextDouble() - 0.5) * tempVariation, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_2, Math.Round(currentReactorTemp + (_rand.NextDouble() - 0.5) * tempVariation * 0.8, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_3, Math.Round(currentReactorTemp + (_rand.NextDouble() - 0.5) * tempVariation * 0.5, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_4, Math.Round(currentReactorTemp + (_rand.NextDouble() - 0.5) * tempVariation * 0.8, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_5, Math.Round(currentReactorTemp + (_rand.NextDouble() - 0.5) * tempVariation, 2));
        }

        private void SimulateReactorPressure(bool vacuumPumpRunning)
        {
            double currentPressure = Convert.ToDouble(BatchPlantNodeManager.PT401.Value);

            if (vacuumPumpRunning)
            {
                double targetPressure = -0.05;
                currentPressure += (targetPressure - currentPressure) * 0.1;
            }
            else
            {
                currentPressure += (0.0 - currentPressure) * 0.05;
            }
            currentPressure += (_rand.NextDouble() - 0.5) * 0.005;
            currentPressure = Math.Max(-0.5, Math.Min(0.5, currentPressure));

            UaWriteHelpers.Write(BatchPlantNodeManager.PT401, Math.Round(currentPressure, 3));
        }

        private void SimulateReactorJacket(int reactorState, double ambientTemperature)
        {
            double currentReactorTemp = Convert.ToDouble(BatchPlantNodeManager.TT401.Value);
            double currentJacketTemp = Convert.ToDouble(BatchPlantNodeManager.TT401_JKT.Value);
            double steamValvePos = Convert.ToDouble(BatchPlantNodeManager.SV401.Value);
            double coolingValvePos = Convert.ToDouble(BatchPlantNodeManager.CV401.Value);

            if (steamValvePos < 1.0 && coolingValvePos < 1.0)
            {
                currentJacketTemp += (currentReactorTemp - currentJacketTemp) * 0.02;
            }
            currentJacketTemp += (_rand.NextDouble() - 0.5) * 0.2;
            currentJacketTemp = Math.Max(15.0, Math.Min(130.0, currentJacketTemp));

            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_JKT, Math.Round(currentJacketTemp, 1));
        }

        private void SimulateReactorAnalyticalInstruments(double reactorLevel, double ambientTemperature)
        {
            // pH
            double currentPh = Convert.ToDouble(BatchPlantNodeManager.pH401.Value);
            if (reactorLevel > 10.0)
            {
                currentPh += (_rand.NextDouble() - 0.5) * 0.01;
                currentPh = Math.Max(3.5, Math.Min(7.0, currentPh));
            }
            else
            {
                currentPh += (7.0 - currentPh) * 0.1;
                currentPh += (_rand.NextDouble() - 0.5) * 0.05;
            }
            UaWriteHelpers.Write(BatchPlantNodeManager.pH401, Math.Round(currentPh, 2));

            // Brix
            double currentBrix = Convert.ToDouble(BatchPlantNodeManager.BRIX401.Value);
            if (reactorLevel > 10.0)
            {
                currentBrix += (_rand.NextDouble() - 0.5) * 0.05;
                currentBrix = Math.Max(0, Math.Min(15.0, currentBrix));
            }
            else
            {
                currentBrix += (0.0 - currentBrix) * 0.2;
                if (currentBrix < 0.1) currentBrix = 0.0;
            }
            UaWriteHelpers.Write(BatchPlantNodeManager.BRIX401, Math.Round(currentBrix, 1));

            // Viscosity
            double currentReactorTemp = Convert.ToDouble(BatchPlantNodeManager.TT401.Value);
            double currentViscosity = Convert.ToDouble(BatchPlantNodeManager.VISC401.Value);
            if (reactorLevel > 10.0)
            {
                double viscosityTempEffect = (ambientTemperature - currentReactorTemp) * 0.5;
                if (currentReactorTemp > ambientTemperature)
                {
                    currentViscosity += viscosityTempEffect * 0.01;
                }
                currentViscosity += (_rand.NextDouble() - 0.5) * 20;
                currentViscosity = Math.Max(100, Math.Min(25000, currentViscosity));
            }
            else
            {
                currentViscosity = 0.0;
            }
            UaWriteHelpers.Write(BatchPlantNodeManager.VISC401, Math.Round(currentViscosity, 1));

            // Color
            double colorL = Convert.ToDouble(BatchPlantNodeManager.COLOR401_L.Value);
            double colorA = Convert.ToDouble(BatchPlantNodeManager.COLOR401_a.Value);
            double colorB = Convert.ToDouble(BatchPlantNodeManager.COLOR401_b.Value);

            if (reactorLevel > 10.0)
            {
                colorL += (_rand.NextDouble() - 0.5) * 0.1;
                colorA += (_rand.NextDouble() - 0.5) * 0.1;
                colorB += (_rand.NextDouble() - 0.5) * 0.1;
                colorL = Math.Max(0, Math.Min(100, colorL));
                colorA = Math.Max(-128, Math.Min(128, colorA));
                colorB = Math.Max(-128, Math.Min(128, colorB));
            }
            else
            {
                colorL += (50.0 - colorL) * 0.1;
                colorA += (0.0 - colorA) * 0.1;
                colorB += (0.0 - colorB) * 0.1;
            }

            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_L, Math.Round(colorL, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_a, Math.Round(colorA, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_b, Math.Round(colorB, 1));

            // Color ΔE
            double colorL_ref = 45.0;
            double colorA_ref = 25.0;
            double colorB_ref = 20.0;
            double deltaE = Math.Sqrt(
                Math.Pow(colorL - colorL_ref, 2) +
                Math.Pow(colorA - colorA_ref, 2) +
                Math.Pow(colorB - colorB_ref, 2)
            );
            if (reactorLevel <= 10.0) deltaE = 0.0;
            UaWriteHelpers.Write(BatchPlantNodeManager.color_delta_e, Math.Round(deltaE, 2));
        }

        private void SimulateReactorAgitator(bool agitatorRunning, double reactorLevel)
        {
            if (agitatorRunning)
            {
                double holdingSpeed = 10.0 + (_rand.NextDouble() - 0.5) * 1.0;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RPM, Math.Round(holdingSpeed, 1));

                double currentViscosity = Convert.ToDouble(BatchPlantNodeManager.VISC401.Value);
                double idleTorque = 5.0;
                if (reactorLevel > 10.0)
                {
                    idleTorque = 5.0 + (currentViscosity / 10000.0) * 3.0;
                }
                idleTorque += (_rand.NextDouble() - 0.5) * 0.5;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT401_TRQ, Math.Round(idleTorque, 2));

                double angularVelocity = holdingSpeed * 2 * Math.PI / 60.0;
                double idlePower = (idleTorque * angularVelocity) / 1000.0;
                idlePower += (_rand.NextDouble() - 0.5) * 0.02;
                UaWriteHelpers.Write(BatchPlantNodeManager.MT401_PWR, Math.Round(idlePower, 3));
            }
            else
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RPM, 0.0);
                UaWriteHelpers.Write(BatchPlantNodeManager.MT401_TRQ, 0.0);
                UaWriteHelpers.Write(BatchPlantNodeManager.MT401_PWR, 0.0);
            }
        }

        private void SimulateReactorVacuumPump(bool vacuumPumpRunning)
        {
            if (!vacuumPumpRunning)
            {
                bool currentFault = Convert.ToBoolean(BatchPlantNodeManager.VP401_FAULT.Value);
                if (!currentFault && _rand.NextDouble() < 0.0001)
                {
                    UaWriteHelpers.Write(BatchPlantNodeManager.VP401_FAULT, true);
                    Console.WriteLine("[FAULT] VP401 Vacuum pump fault detected");
                }
            }
        }

        private void SimulateReactorValves(int reactorState)
        {
            if (reactorState == 1 || reactorState == 0)
            {
                // Pressure control valve
                double currentPcvPos = Convert.ToDouble(BatchPlantNodeManager.PCV401.Value);
                if (currentPcvPos > 0)
                {
                    currentPcvPos *= 0.8;
                    if (currentPcvPos < 0.5) currentPcvPos = 0.0;
                    UaWriteHelpers.Write(BatchPlantNodeManager.PCV401, Math.Round(currentPcvPos, 1));
                }

                // Steam valve
                double currentSteamValve = Convert.ToDouble(BatchPlantNodeManager.SV401.Value);
                if (currentSteamValve > 0)
                {
                    currentSteamValve *= 0.7;
                    if (currentSteamValve < 0.5) currentSteamValve = 0.0;
                    UaWriteHelpers.Write(BatchPlantNodeManager.SV401, Math.Round(currentSteamValve, 1));
                }

                // Cooling valve
                double currentCoolingValve = Convert.ToDouble(BatchPlantNodeManager.CV401.Value);
                if (currentCoolingValve > 0)
                {
                    currentCoolingValve *= 0.7;
                    if (currentCoolingValve < 0.5) currentCoolingValve = 0.0;
                    UaWriteHelpers.Write(BatchPlantNodeManager.CV401, Math.Round(currentCoolingValve, 1));
                }

                // PID output
                double currentPidOutput = Convert.ToDouble(BatchPlantNodeManager.PID_RX_OUTPUT.Value);
                if (currentPidOutput != 0)
                {
                    currentPidOutput *= 0.7;
                    if (Math.Abs(currentPidOutput) < 0.5) currentPidOutput = 0.0;
                    UaWriteHelpers.Write(BatchPlantNodeManager.PID_RX_OUTPUT, Math.Round(currentPidOutput, 1));
                }

                // Transfer valve
                int currentValvePos = Convert.ToInt32(BatchPlantNodeManager.VLV401.Value);
                if (currentValvePos > 0)
                {
                    UaWriteHelpers.Write(BatchPlantNodeManager.VLV401, 0);
                }

                // Reaction phase
                UaWriteHelpers.Write(BatchPlantNodeManager.reaction_phase, 0);
                UaWriteHelpers.Write(BatchPlantNodeManager.hold_time_remaining, 0.0);
            }
        }

        private void SimulateReactorAcidSystem(int reactorState)
        {
            bool acidPumpRunning = Convert.ToBoolean(BatchPlantNodeManager.ACID_PUMP_RUN.Value);
            if (acidPumpRunning && (reactorState == 1 || reactorState == 0))
            {
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_START, false);
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_RUN, false);
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_FLOW, 0.0);
            }
        }
    }
}


