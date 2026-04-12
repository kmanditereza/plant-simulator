using BatchPlant;
using BatchPlant.Helpers;
using BatchPlant.Alarms;

namespace BatchPlant.Operations
{
    /// <summary>
    /// Controls Stage 3: Reactor/Cooking operation.
    /// </summary>
    public class ReactorController
    {
        private readonly Random _rand = new();

        // Process parameters
        private const double TemperatureSetpoint = 88.0;
        private const double VacuumSetpoint = -0.2;
        private const double AgitatorSpeed = 30.0;
        private const double TargetPh = 4.1;

        // PID parameters
        private const double Kp = 1.8;
        private const double Ki = 0.5;
        private const double Kd = 0.1;

        // Color reference values
        private const double ColorL_Reference = 45.0;
        private const double ColorA_Reference = 25.0;
        private const double ColorB_Reference = 20.0;

        // PID state
        private double _pidIntegral = 0.0;
        private double _pidPreviousError = 0.0;

        /// <summary>
        /// Execute the reactor operation.
        /// </summary>
        public async Task ExecuteAsync()
        {
            Console.WriteLine("=== Stage 3: Reactor/Cooking Operation Started ===");

            // Pre-condition check (disabled for demo)
            double currentTemperature = Convert.ToDouble(BatchPlantNodeManager.TT201.Value);
            if (currentTemperature < 85.0)
            {
                Console.WriteLine($"[INFO] Temperature {currentTemperature:F1}°C < 85°C - Proceeding anyway (interlocks disabled)");
            }

            await TransferToReactor(currentTemperature);
            await ExecuteReaction();
        }

        private async Task TransferToReactor(double transferTemperature)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Transferring to Reactor");
            UaWriteHelpers.Write(BatchPlantNodeManager.reactor_status, "Filling");
            UaWriteHelpers.Write(BatchPlantNodeManager.RX401_STATE, 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.RX401_MODE, 2);

            double mixerLevel = Convert.ToDouble(BatchPlantNodeManager.LT201.Value);
            double transferVolume = mixerLevel;

            Console.WriteLine($"Transferring {transferVolume:F0}L at {transferTemperature:F1}°C to reactor...");

            UaWriteHelpers.Write(BatchPlantNodeManager.VLV401, 100);

            double transferRate = 250.0;
            double transferTime = transferVolume / transferRate;
            int transferSteps = (int)(transferTime * 60 / 2);

            for (int i = 0; i <= transferSteps; i++)
            {
                double progress = (double)i / transferSteps;
                double transferred = transferVolume * progress;
                double remaining = transferVolume - transferred;

                UaWriteHelpers.Write(BatchPlantNodeManager.LT201, Math.Round(remaining, 1));
                UaWriteHelpers.Write(BatchPlantNodeManager.LT401, Math.Round(transferred, 1));

                double reactorTemp = transferTemperature - (2.0 * progress) + (_rand.NextDouble() - 0.5) * 0.3;
                UaWriteHelpers.Write(BatchPlantNodeManager.TT401, Math.Round(reactorTemp, 2));

                UpdateMultiPointTemperatures(reactorTemp);

                if (i % 10 == 0)
                {
                    Console.WriteLine($"Transfer: {progress * 100:F0}% - Reactor level: {transferred:F0}L");
                }

                await Task.Delay(2000);
            }

            UaWriteHelpers.Write(BatchPlantNodeManager.VLV401, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.LT201, 0.0);

            Console.WriteLine(">>> Transfer complete");
        }

        private void UpdateMultiPointTemperatures(double reactorTemp)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_1, Math.Round(reactorTemp + (_rand.NextDouble() - 0.5) * 1.0, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_2, Math.Round(reactorTemp + (_rand.NextDouble() - 0.5) * 0.8, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_3, Math.Round(reactorTemp + (_rand.NextDouble() - 0.5) * 0.5, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_4, Math.Round(reactorTemp + (_rand.NextDouble() - 0.5) * 0.8, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_5, Math.Round(reactorTemp + (_rand.NextDouble() - 0.5) * 1.0, 2));
        }

        private async Task ExecuteReaction()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.reactor_status, "Reaction");
            UaWriteHelpers.Write(BatchPlantNodeManager.RX401_STATE, 3);

            int reactionDurationSeconds = Convert.ToInt32(BatchPlantNodeManager.reaction_time.Value);
            if (reactionDurationSeconds <= 0) reactionDurationSeconds = 1800;

            int updateIntervalMs = 2000;
            int totalSteps = (reactionDurationSeconds * 1000) / updateIntervalMs;

            // Initialize state variables
            double reactorTemperature = Convert.ToDouble(BatchPlantNodeManager.TT401.Value);
            double reactorLevel = Convert.ToDouble(BatchPlantNodeManager.LT401.Value);
            double initialLevel = reactorLevel;
            double reactorPressure = 0.0;
            double reactorPh = 5.5;
            double reactorBrix = 9.0;
            double reactorViscosity = Convert.ToDouble(BatchPlantNodeManager.VISC201.Value);
            double initialViscosity = reactorViscosity;

            double colorL = 45.0;
            double colorA = 25.0;
            double colorB = 20.0;

            _pidPreviousError = TemperatureSetpoint - reactorTemperature;
            double totalAcidDosed = 0.0;
            bool phTargetReached = false;
            double energyConsumed = Convert.ToDouble(BatchPlantNodeManager.reactor_energy_consumed.Value);

            InitializeAnalyticalInstruments(reactorPh, reactorBrix, reactorViscosity, colorL, colorA, colorB);

            Console.WriteLine($"Reaction Duration: {reactionDurationSeconds} seconds");
            Console.WriteLine($"Initial Temperature: {reactorTemperature:F1}°C");
            Console.WriteLine($"Initial pH: {reactorPh:F2}");
            Console.WriteLine($"Initial Brix: {reactorBrix:F1}°");
            Console.WriteLine($"Initial Viscosity: {reactorViscosity:F0} cP");

            StartVacuumAndAgitator();

            for (int step = 0; step <= totalSteps; step++)
            {
                double progress = (double)step / totalSteps;
                double elapsedMinutes = (progress * reactionDurationSeconds) / 60.0;
                double elapsedSeconds = progress * reactionDurationSeconds;
                double dtSeconds = updateIntervalMs / 1000.0;

                var (currentPhase, phaseName) = DeterminePhase(elapsedMinutes);
                UaWriteHelpers.Write(BatchPlantNodeManager.reaction_phase, currentPhase);
                UaWriteHelpers.Write(BatchPlantNodeManager.hold_time_remaining, Math.Round(reactionDurationSeconds - elapsedSeconds, 0));

                reactorPressure = UpdateVacuumControl(elapsedMinutes, reactorPressure);
                var (steamValvePos, coolingValvePos) = UpdateTemperatureControl(reactorTemperature, dtSeconds);
                reactorTemperature = UpdateReactorTemperature(reactorTemperature, steamValvePos, coolingValvePos);

                // Phase-specific simulations
                switch (currentPhase)
                {
                    case 1:
                        reactorViscosity = SimulateStarchActivation(elapsedMinutes, initialViscosity, reactorViscosity);
                        break;
                    case 2:
                        (reactorPh, totalAcidDosed, phTargetReached) = SimulatePhAdjustment(
                            reactorPh, totalAcidDosed, phTargetReached, dtSeconds);
                        reactorViscosity += (_rand.NextDouble() - 0.5) * 30;
                        break;
                    case 3:
                        (colorL, colorA, colorB, reactorViscosity, reactorBrix, reactorPh) = SimulateMaillardReactions(
                            elapsedMinutes, colorL, colorA, colorB, reactorViscosity, reactorBrix, reactorPh);
                        break;
                    case 4:
                        (reactorLevel, reactorBrix, reactorViscosity, colorL, colorA, reactorPh) = SimulateConcentration(
                            elapsedMinutes, initialLevel, reactorLevel, reactorBrix, initialViscosity, reactorViscosity, colorL, colorA, reactorPh, dtSeconds);
                        break;
                }

                reactorPh = Math.Max(3.5, Math.Min(6.0, reactorPh));

                UpdateAnalyticalInstruments(reactorPh, reactorBrix, reactorViscosity, colorL, colorA, colorB, reactorLevel);

                double deltaE = CalculateColorDeltaE(colorL, colorA, colorB);
                UaWriteHelpers.Write(BatchPlantNodeManager.color_delta_e, Math.Round(deltaE, 2));

                var agitatorPower = UpdateAgitatorParameters(reactorViscosity);

                energyConsumed = UpdateEnergyConsumption(steamValvePos, agitatorPower, energyConsumed, updateIntervalMs);

                CheckAlarmsAndWarnings(reactorPressure, reactorPh, reactorBrix, deltaE, reactorTemperature, elapsedMinutes);
                LogProgress(step, totalSteps, elapsedMinutes, currentPhase, phaseName, reactorTemperature, reactorPh, reactorBrix, reactorViscosity, reactorLevel, reactorPressure);

                await Task.Delay(updateIntervalMs);
            }

            FinalizeReactorState(reactorTemperature, reactorPh, reactorBrix, reactorViscosity, reactorLevel, initialLevel, totalAcidDosed, energyConsumed);
        }

        private void InitializeAnalyticalInstruments(double reactorPh, double reactorBrix, double reactorViscosity,
            double colorL, double colorA, double colorB)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.pH401, Math.Round(reactorPh, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.BRIX401, Math.Round(reactorBrix, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.VISC401, Math.Round(reactorViscosity, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_L, Math.Round(colorL, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_a, Math.Round(colorA, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_b, Math.Round(colorB, 1));
        }

        private void StartVacuumAndAgitator()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.VP401_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.VP401_RUN, true);
            Console.WriteLine(">>> Vacuum pump started - Target: -0.2 bar");

            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RUN, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RPM, AgitatorSpeed);
            Console.WriteLine($">>> Agitator started at {AgitatorSpeed} RPM");
        }

        private (int phase, string name) DeterminePhase(double elapsedMinutes)
        {
            if (elapsedMinutes < 5) return (1, "Starch Activation");
            if (elapsedMinutes < 10) return (2, "pH Adjustment");
            if (elapsedMinutes < 20) return (3, "Maillard Reactions");
            return (4, "Concentration");
        }

        private double UpdateVacuumControl(double elapsedMinutes, double reactorPressure)
        {
            double targetPressure = VacuumSetpoint;
            if (elapsedMinutes < 2)
            {
                targetPressure = 0.0 + (VacuumSetpoint - 0.0) * (elapsedMinutes / 2.0);
            }

            double pressureError = targetPressure - reactorPressure;
            double pcvPosition = Math.Max(0, Math.Min(100, 50 + pressureError * 100));
            UaWriteHelpers.Write(BatchPlantNodeManager.PCV401, Math.Round(pcvPosition, 1));

            reactorPressure += (targetPressure - reactorPressure) * 0.1;
            reactorPressure += (_rand.NextDouble() - 0.5) * 0.01;
            UaWriteHelpers.Write(BatchPlantNodeManager.PT401, Math.Round(reactorPressure, 3));

            return reactorPressure;
        }

        private (double steamValvePos, double coolingValvePos) UpdateTemperatureControl(double reactorTemperature, double dtSeconds)
        {
            double tempError = TemperatureSetpoint - reactorTemperature;

            _pidIntegral += tempError * dtSeconds;
            _pidIntegral = Math.Max(-30, Math.Min(30, _pidIntegral));

            double derivative = (tempError - _pidPreviousError) / dtSeconds;
            _pidPreviousError = tempError;

            double pidOutput = (Kp * tempError) + (Ki * _pidIntegral) + (Kd * derivative);

            double steamValvePos = 0.0;
            double coolingValvePos = 0.0;

            if (pidOutput > 0)
            {
                steamValvePos = Math.Min(100, pidOutput);
            }
            else
            {
                coolingValvePos = Math.Min(100, -pidOutput);
            }

            UaWriteHelpers.Write(BatchPlantNodeManager.SV401, Math.Round(steamValvePos, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.CV401, Math.Round(coolingValvePos, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.PID_RX_OUTPUT, Math.Round(pidOutput, 1));

            return (steamValvePos, coolingValvePos);
        }

        private double UpdateReactorTemperature(double reactorTemperature, double steamValvePos, double coolingValvePos)
        {
            double jacketTemp;
            if (steamValvePos > 0)
            {
                jacketTemp = 100 + (steamValvePos / 100.0) * 30;
            }
            else if (coolingValvePos > 0)
            {
                jacketTemp = 20 + (1 - coolingValvePos / 100.0) * 60;
            }
            else
            {
                jacketTemp = reactorTemperature;
            }
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_JKT, Math.Round(jacketTemp, 1));

            double heatTransfer = (jacketTemp - reactorTemperature) * 0.02;
            reactorTemperature += heatTransfer;
            reactorTemperature += (_rand.NextDouble() - 0.5) * 0.1;
            reactorTemperature = Math.Max(70, Math.Min(95, reactorTemperature));

            UaWriteHelpers.Write(BatchPlantNodeManager.TT401, Math.Round(reactorTemperature, 2));
            UpdateMultiPointTemperaturesReaction(reactorTemperature);

            return reactorTemperature;
        }

        private void UpdateMultiPointTemperaturesReaction(double reactorTemperature)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_1, Math.Round(reactorTemperature + (_rand.NextDouble() - 0.5) * 0.8, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_2, Math.Round(reactorTemperature + (_rand.NextDouble() - 0.5) * 0.5, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_3, Math.Round(reactorTemperature + (_rand.NextDouble() - 0.5) * 0.3, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_4, Math.Round(reactorTemperature + (_rand.NextDouble() - 0.5) * 0.5, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401_5, Math.Round(reactorTemperature + (_rand.NextDouble() - 0.5) * 0.8, 2));
        }

        private double SimulateStarchActivation(double elapsedMinutes, double initialViscosity, double reactorViscosity)
        {
            double starchProgress = elapsedMinutes / 5.0;
            double viscosityIncrease = initialViscosity * 0.6 * (1 - Math.Exp(-3 * starchProgress));
            reactorViscosity = initialViscosity + viscosityIncrease;
            reactorViscosity += (_rand.NextDouble() - 0.5) * 100;
            return reactorViscosity;
        }

        private (double ph, double totalAcid, bool targetReached) SimulatePhAdjustment(
            double reactorPh, double totalAcidDosed, bool phTargetReached, double dtSeconds)
        {
            if (!phTargetReached && reactorPh > TargetPh + 0.05)
            {
                double phError = reactorPh - TargetPh;
                double acidFlowRate = Math.Min(50.0, phError * 30.0);

                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_START, true);
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_RUN, true);
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_FLOW, Math.Round(acidFlowRate, 1));

                double acidAdded = acidFlowRate * (dtSeconds / 60.0);
                totalAcidDosed += acidAdded;
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_TOTAL, Math.Round(totalAcidDosed, 1));

                double phDrop = acidAdded * 0.008;
                reactorPh -= phDrop;
                reactorPh += (_rand.NextDouble() - 0.5) * 0.02;
            }
            else
            {
                phTargetReached = true;
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_START, false);
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_RUN, false);
                UaWriteHelpers.Write(BatchPlantNodeManager.ACID_FLOW, 0.0);
            }

            return (reactorPh, totalAcidDosed, phTargetReached);
        }

        private (double colorL, double colorA, double colorB, double viscosity, double brix, double ph) SimulateMaillardReactions(
            double elapsedMinutes, double colorL, double colorA, double colorB, double reactorViscosity, double reactorBrix, double reactorPh)
        {
            double maillardProgress = (elapsedMinutes - 10.0) / 10.0;

            colorL = 45.0 - (5.0 * maillardProgress);
            colorL += (_rand.NextDouble() - 0.5) * 0.3;

            colorA = 25.0 + (5.0 * maillardProgress);
            colorA += (_rand.NextDouble() - 0.5) * 0.2;

            colorB = 20.0 + (3.0 * maillardProgress);
            colorB += (_rand.NextDouble() - 0.5) * 0.2;

            reactorViscosity += 2.0 * _rand.NextDouble();
            reactorBrix += 0.0005;
            reactorPh += (_rand.NextDouble() - 0.5) * 0.01;

            return (colorL, colorA, colorB, reactorViscosity, reactorBrix, reactorPh);
        }

        private (double level, double brix, double viscosity, double colorL, double colorA, double ph) SimulateConcentration(
            double elapsedMinutes, double initialLevel, double reactorLevel, double reactorBrix, double initialViscosity,
            double reactorViscosity, double colorL, double colorA, double reactorPh, double dtSeconds)
        {
            double evaporationRate = 0.05 / 10.0 / 60.0;
            double waterLoss = initialLevel * evaporationRate * dtSeconds;
            reactorLevel -= waterLoss;
            reactorLevel = Math.Max(initialLevel * 0.94, reactorLevel);

            double concentrationFactor = initialLevel / reactorLevel;
            reactorBrix = 9.0 * concentrationFactor;
            reactorBrix += (_rand.NextDouble() - 0.5) * 0.1;
            reactorBrix = Math.Min(12.0, reactorBrix);

            reactorViscosity = initialViscosity * 1.6 * concentrationFactor;
            reactorViscosity += (_rand.NextDouble() - 0.5) * 50;

            colorL -= 0.05;
            colorA += 0.02;
            reactorPh += (_rand.NextDouble() - 0.5) * 0.005;

            return (reactorLevel, reactorBrix, reactorViscosity, colorL, colorA, reactorPh);
        }

        private void UpdateAnalyticalInstruments(double reactorPh, double reactorBrix, double reactorViscosity,
            double colorL, double colorA, double colorB, double reactorLevel)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.pH401, Math.Round(reactorPh, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.BRIX401, Math.Round(reactorBrix, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.VISC401, Math.Round(reactorViscosity, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_L, Math.Round(colorL, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_a, Math.Round(colorA, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.COLOR401_b, Math.Round(colorB, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.LT401, Math.Round(reactorLevel, 1));
        }

        private double CalculateColorDeltaE(double colorL, double colorA, double colorB)
        {
            return Math.Sqrt(
                Math.Pow(colorL - ColorL_Reference, 2) +
                Math.Pow(colorA - ColorA_Reference, 2) +
                Math.Pow(colorB - ColorB_Reference, 2)
            );
        }

        private double UpdateAgitatorParameters(double reactorViscosity)
        {
            double agitatorTorque = 15.0 + (reactorViscosity / 10000.0) * 10.0;
            agitatorTorque += (_rand.NextDouble() - 0.5) * 1.0;
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_TRQ, Math.Round(agitatorTorque, 2));

            double agitatorPower = (agitatorTorque * AgitatorSpeed * 2 * Math.PI / 60.0) / 1000.0;
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_PWR, Math.Round(agitatorPower, 3));

            double actualRpm = AgitatorSpeed + (_rand.NextDouble() - 0.5) * 1.0;
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RPM, Math.Round(actualRpm, 1));

            return agitatorPower;
        }

        private double UpdateEnergyConsumption(double steamValvePos, double agitatorPower, double energyConsumed, int updateIntervalMs)
        {
            double jacketHeatPower = (steamValvePos / 100.0) * 20.0;
            double vacuumPumpPower = 3.0;
            double totalPower = jacketHeatPower + vacuumPumpPower + agitatorPower;
            energyConsumed += totalPower * (updateIntervalMs / 1000.0) / 3600.0;
            UaWriteHelpers.Write(BatchPlantNodeManager.reactor_energy_consumed, Math.Round(energyConsumed, 4));
            return energyConsumed;
        }

        private void CheckAlarmsAndWarnings(double reactorPressure, double reactorPh, double reactorBrix,
            double deltaE, double reactorTemperature, double elapsedMinutes)
        {
            // ═══════════════════════════════════════════════════════════════════════
            // OPC UA ALARM CHECKS (Part 9 Alarms & Conditions)
            // ═══════════════════════════════════════════════════════════════════════

            // Check reactor pressure (ALM-200, ALM-201)
            AlarmHelper.CheckReactorPressure(reactorPressure);

            // Check pH levels (ALM-202, ALM-203, ALM-204)
            if (elapsedMinutes > 5)  // Only check after initial stabilization
            {
                AlarmHelper.CheckReactorPH(reactorPh);
            }

            // Check Brix levels (ALM-205, ALM-206)
            if (elapsedMinutes > 20)  // Only check during concentration phase
            {
                AlarmHelper.CheckReactorBrix(reactorBrix);
            }

            // Check color drift (ALM-207, ALM-208)
            AlarmHelper.CheckColorDrift(deltaE);

            // Check reactor temperature (ALM-209, ALM-210)
            AlarmHelper.CheckReactorTemperature(reactorTemperature);

            // Check vacuum (ALM-211)
            double currentPressure = Convert.ToDouble(BatchPlantNodeManager.PT401.Value);
            if (elapsedMinutes > 3)  // Only check during vacuum operation
            {
                AlarmHelper.CheckReactorVacuum(currentPressure);
            }

            // Check agitator fault (ALM-212)
            bool agitatorFault = Convert.ToBoolean(BatchPlantNodeManager.MT401_FAULT.Value);
            AlarmHelper.CheckReactorAgitatorFault(agitatorFault);

            // ═══════════════════════════════════════════════════════════════════════
            // CONSOLE LOGGING (kept for operator visibility)
            // ═══════════════════════════════════════════════════════════════════════

            if (reactorPressure > 1.2 && reactorPressure <= 1.5)
            {
                Console.WriteLine($"[ALARM ALM-200] Reactor overpressure warning: {reactorPressure:F2} bar");
            }
            else if (reactorPressure > 1.5)
            {
                Console.WriteLine($"[ALARM ALM-201] Reactor CRITICAL overpressure: {reactorPressure:F2} bar");
            }

            if (reactorPh > 4.4 && reactorPh <= 4.6 && elapsedMinutes > 10)
            {
                Console.WriteLine($"[ALARM ALM-202] pH high: {reactorPh:F2} - Approaching microbial risk");
            }
            else if (reactorPh > 4.6 && elapsedMinutes > 10)
            {
                Console.WriteLine($"[ALARM ALM-203] pH CRITICAL: {reactorPh:F2} > 4.6 - Microbial risk!");
            }
            else if (reactorPh < 3.8 && elapsedMinutes > 5)
            {
                Console.WriteLine($"[ALARM ALM-204] pH low: {reactorPh:F2} - Excessive acidity");
            }

            if (reactorBrix > 14.0 && elapsedMinutes > 25)
            {
                Console.WriteLine($"[ALARM ALM-205] Brix high: {reactorBrix:F1}° - Over-concentration");
            }
            else if (reactorBrix < 10.0 && elapsedMinutes > 25)
            {
                Console.WriteLine($"[ALARM ALM-206] Brix low: {reactorBrix:F1}° - Under-concentration");
            }

            if (deltaE > 3.0 && deltaE <= 5.0)
            {
                Console.WriteLine($"[ALARM ALM-207] Color drift: ΔE = {deltaE:F2}");
            }
            else if (deltaE > 5.0)
            {
                Console.WriteLine($"[ALARM ALM-208] Significant color drift: ΔE = {deltaE:F2}");
            }

            if (reactorTemperature > 92.0)
            {
                Console.WriteLine($"[ALARM ALM-209] Reactor high temperature: {reactorTemperature:F1}°C");
            }
            else if (reactorTemperature < 82.0 && elapsedMinutes > 5)
            {
                Console.WriteLine($"[ALARM ALM-210] Reactor low temperature: {reactorTemperature:F1}°C");
            }

            if (currentPressure > -0.1 && elapsedMinutes > 3)
            {
                Console.WriteLine($"[ALARM ALM-211] Vacuum loss: {currentPressure:F2} bar");
            }
        }

        private void LogProgress(int step, int totalSteps, double elapsedMinutes, int currentPhase, string phaseName,
            double reactorTemperature, double reactorPh, double reactorBrix, double reactorViscosity, double reactorLevel, double reactorPressure)
        {
            if (step % 15 == 0 || step == totalSteps)
            {
                Console.WriteLine($"[{elapsedMinutes:F1} min] Phase {currentPhase}: {phaseName}");
                Console.WriteLine($"         Temp: {reactorTemperature:F1}°C | pH: {reactorPh:F2} | " +
                                $"Brix: {reactorBrix:F1}° | Visc: {reactorViscosity:F0} cP | " +
                                $"Level: {reactorLevel:F0}L | Vacuum: {reactorPressure:F2} bar");
            }

            if (step > 0)
            {
                int reactionDurationSeconds = Convert.ToInt32(BatchPlantNodeManager.reaction_time.Value);
                if (reactionDurationSeconds <= 0) reactionDurationSeconds = 1800;

                double prevMinutes = ((double)(step - 1) / totalSteps) * reactionDurationSeconds / 60.0;
                var (prevPhase, _) = DeterminePhase(prevMinutes);

                if (currentPhase != prevPhase)
                {
                    Console.WriteLine($">>> Entering Phase {currentPhase}: {phaseName}");
                    UaWriteHelpers.Write(BatchPlantNodeManager.reactor_status, phaseName);
                    UaWriteHelpers.Write(BatchPlantNodeManager.phase_start_time, DateTime.Now);
                }
            }
        }

        private void FinalizeReactorState(double reactorTemperature, double reactorPh, double reactorBrix,
            double reactorViscosity, double reactorLevel, double initialLevel, double totalAcidDosed, double energyConsumed)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.VP401_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.VP401_RUN, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.PCV401, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RPM, 10.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.SV401, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.CV401, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.ACID_PUMP_RUN, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.ACID_FLOW, 0.0);

            UaWriteHelpers.Write(BatchPlantNodeManager.batch_ph_ok, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.reactor_status, "Reaction Complete");
            UaWriteHelpers.Write(BatchPlantNodeManager.RX401_STATE, 1);
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Reaction Complete - Ready for Filling");

            double deltaE = Convert.ToDouble(BatchPlantNodeManager.color_delta_e.Value);

            Console.WriteLine("=== Stage 3: Reactor/Cooking Operation Complete ===");
            Console.WriteLine($"Final Temperature: {reactorTemperature:F1}°C");
            Console.WriteLine($"Final pH: {reactorPh:F2}");
            Console.WriteLine($"Final Brix: {reactorBrix:F1}°");
            Console.WriteLine($"Final Viscosity: {reactorViscosity:F0} cP");
            Console.WriteLine($"Final Level: {reactorLevel:F0}L ({(reactorLevel / initialLevel * 100):F1}% of initial)");
            Console.WriteLine($"Total Acid Dosed: {totalAcidDosed:F1} mL");
            Console.WriteLine($"Color ΔE: {deltaE:F2}");
            Console.WriteLine($"Energy Consumed: {energyConsumed:F3} kWh");

            // Print alarm summary
            var (total, active, unacked, unconfirmed) = AlarmHelper.GetSummary();
            if (active > 0)
            {
                Console.WriteLine($">>> Active Alarms: {active} ({unacked} unacknowledged)");
            }

            Console.WriteLine(">>> Proceeding to Stage 4: Filling");

            // Reset PID state
            _pidIntegral = 0.0;
            _pidPreviousError = 0.0;
        }
    }
}