using BatchPlant;
using BatchPlant.Helpers;
using BatchPlant.Helpers;

namespace BatchPlant.Operations
{
    /// <summary>
    /// Controls Stage 4: Filling operation.
    /// </summary>
    public class FillerController
    {
        private readonly Random _rand = new();

        // Fill parameters
        private const double EmptyJarWeight = 200.0;
        private const double ProductDensity = 1.05;
        private const double FastFillRate = 400.0;
        private const double DribbleFillRate = 50.0;

        /// <summary>
        /// Execute the filling operation.
        /// </summary>
        public async Task ExecuteAsync()
        {
            Console.WriteLine("=== Stage 4: Filling Operation Started ===");

            double reactorTemp = Convert.ToDouble(BatchPlantNodeManager.TT401.Value);
            double reactorPh = Convert.ToDouble(BatchPlantNodeManager.pH401.Value);
            double reactorLevel = Convert.ToDouble(BatchPlantNodeManager.LT401.Value);

            double minFillTemp = Convert.ToDouble(BatchPlantNodeManager.SP_FILL_TEMP_MIN.Value);
            double maxFillTemp = Convert.ToDouble(BatchPlantNodeManager.SP_FILL_TEMP_MAX.Value);

            // Log warnings but proceed (interlocks disabled for demo)
            LogPreConditionWarnings(reactorPh, reactorTemp, reactorLevel, minFillTemp, maxFillTemp);

            InitializeFillerState();

            var fillParams = GetFillParameters();
            var statistics = new FillingStatistics();

            Console.WriteLine($"Fill Parameters:");
            Console.WriteLine($"  Target Weight: {fillParams.TargetWeight}g (±{(fillParams.OverweightLimit - fillParams.TargetWeight):F1}g)");
            Console.WriteLine($"  Available Product: {reactorLevel:F0}L");
            Console.WriteLine($"  Estimated Jars: {fillParams.TotalJarsEstimate}");
            Console.WriteLine($"  Estimated Fill Time: {fillParams.EstimatedFillTime:F1} minutes");
            Console.WriteLine($"  Fill Rate: {fillParams.FillRate} jars/min");

            UaWriteHelpers.Write(BatchPlantNodeManager.JARS_REMAINING, fillParams.TotalJarsEstimate);

            StartFillingEquipment(fillParams.FillRate);

            DateTime fillStartTime = DateTime.Now;
            double currentProductTemp = reactorTemp;
            double nozzleTemp = reactorTemp - 2.0;

            bool fillingActive = true;

            while (fillingActive)
            {
                double currentReactorLevel = Convert.ToDouble(BatchPlantNodeManager.LT401.Value);
                if (currentReactorLevel < 10.0)
                {
                    Console.WriteLine(">>> Reactor nearly empty - stopping fill");
                    break;
                }

                // Temperature monitoring
                (currentProductTemp, nozzleTemp) = UpdateTemperatures(
                    reactorTemp, reactorLevel, currentReactorLevel, currentProductTemp, nozzleTemp, minFillTemp, statistics.TotalJars);

                // Fill a jar
                double finalNetWeight = await FillJarAsync(fillParams);

                // Classify and update statistics
                string jarStatus = ClassifyJar(finalNetWeight, fillParams, ref statistics);

                // Update OPC UA nodes
                UpdateStatisticsNodes(statistics, finalNetWeight, fillParams.TargetWeight);

                // Update reactor level
                double newReactorLevel = UpdateReactorLevel(currentReactorLevel, finalNetWeight, fillParams.JarVolume);
                int jarsRemaining = (int)((newReactorLevel - 10.0) * 1000 / fillParams.JarVolume);
                jarsRemaining = Math.Max(0, jarsRemaining);
                UaWriteHelpers.Write(BatchPlantNodeManager.JARS_REMAINING, jarsRemaining);

                // Timing & energy
                UpdateTimingAndEnergy(fillStartTime, ref statistics, fillParams.SecondsPerJar);

                // Logging
                LogJarProgress(statistics, finalNetWeight, jarStatus, nozzleTemp, newReactorLevel, fillStartTime);

                // Wait for next jar
                await WaitForNextJarCycle(fillStartTime, fillParams.FillCycleMs);

                // Check completion
                if (jarsRemaining <= 0 || newReactorLevel < 10.0)
                {
                    fillingActive = false;
                }
            }

            FinalizeFillingOperation(fillStartTime, statistics, fillParams.TargetWeight);
        }

        private void LogPreConditionWarnings(double reactorPh, double reactorTemp, double reactorLevel,
            double minFillTemp, double maxFillTemp)
        {
            if (reactorPh >= 4.3)
            {
                Console.WriteLine($"[INFO] pH {reactorPh:F2} >= 4.3 - Proceeding anyway (interlocks disabled)");
            }

            if (reactorTemp < minFillTemp)
            {
                Console.WriteLine($"[INFO] Temperature {reactorTemp:F1}°C < {minFillTemp}°C - Proceeding anyway (interlocks disabled)");
            }

            if (reactorTemp > maxFillTemp)
            {
                Console.WriteLine($"[INFO] Temperature {reactorTemp:F1}°C > {maxFillTemp}°C - Proceeding anyway (interlocks disabled)");
            }

            if (reactorLevel < 20.0)
            {
                Console.WriteLine($"[INFO] Reactor level {reactorLevel:F0}L < 20L - Proceeding anyway (interlocks disabled)");
            }
        }

        private void InitializeFillerState()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Filling");
            UaWriteHelpers.Write(BatchPlantNodeManager.filler_status, "Running");
            UaWriteHelpers.Write(BatchPlantNodeManager.FL501_STATE, 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.FL501_MODE, 2);
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_START_TIME, DateTime.Now);
        }

        private FillParameters GetFillParameters()
        {
            double targetWeight = Convert.ToDouble(BatchPlantNodeManager.SP_FILL_WEIGHT.Value);
            double jarVolume = Convert.ToDouble(BatchPlantNodeManager.SP_JAR_VOLUME.Value);
            double overweightLimit = Convert.ToDouble(BatchPlantNodeManager.SP_OVERWEIGHT.Value);
            double underweightLimit = Convert.ToDouble(BatchPlantNodeManager.SP_UNDERWEIGHT.Value);
            double inflightComp = Convert.ToDouble(BatchPlantNodeManager.INFLIGHT_COMP.Value);
            double fillRate = Convert.ToDouble(BatchPlantNodeManager.SP_FILL_RATE.Value);
            double reactorLevel = Convert.ToDouble(BatchPlantNodeManager.LT401.Value);

            int totalJarsEstimate = (int)(reactorLevel * 1000 / jarVolume);
            double estimatedFillTime = totalJarsEstimate / fillRate;
            double secondsPerJar = 60.0 / fillRate;
            int fillCycleMs = (int)(secondsPerJar * 1000);

            return new FillParameters
            {
                TargetWeight = targetWeight,
                JarVolume = jarVolume,
                OverweightLimit = overweightLimit,
                UnderweightLimit = underweightLimit,
                InflightComp = inflightComp,
                FillRate = fillRate,
                TotalJarsEstimate = totalJarsEstimate,
                EstimatedFillTime = estimatedFillTime,
                SecondsPerJar = secondsPerJar,
                FillCycleMs = fillCycleMs
            };
        }

        private void StartFillingEquipment(double fillRate)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.CONV501_RUN, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.CONV501_SPEED, fillRate);
            Console.WriteLine($">>> Conveyor started at {fillRate} jars/min");

            UaWriteHelpers.Write(BatchPlantNodeManager.P501_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.P501_RUN, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.P501_SPEED, 80.0);
            Console.WriteLine(">>> Dosing pump started");

            UaWriteHelpers.Write(BatchPlantNodeManager.CAP501_RUN, true);
            Console.WriteLine(">>> Capper started");

            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RPM, 15.0);
        }

        private (double productTemp, double nozzleTemp) UpdateTemperatures(
            double reactorTemp, double availableProduct, double currentReactorLevel,
            double currentProductTemp, double nozzleTemp, double minFillTemp, int totalJars)
        {
            double batchProgress = 1.0 - (currentReactorLevel / availableProduct);
            double tempDrop = batchProgress * 8.0;
            currentProductTemp = reactorTemp - tempDrop + (_rand.NextDouble() - 0.5) * 0.5;
            currentProductTemp = Math.Max(70.0, currentProductTemp);

            nozzleTemp += (currentProductTemp - nozzleTemp) * 0.3;
            nozzleTemp += (_rand.NextDouble() - 0.5) * 0.3;

            UaWriteHelpers.Write(BatchPlantNodeManager.TT501, Math.Round(nozzleTemp, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.TT401, Math.Round(currentProductTemp, 1));

            bool tempOk = nozzleTemp >= minFillTemp;
            UaWriteHelpers.Write(BatchPlantNodeManager.IND_TEMP_OK, tempOk);

            if (!tempOk && totalJars % 50 == 0)
            {
                Console.WriteLine($"[WARNING ALM-003] Fill temperature {nozzleTemp:F1}°C < {minFillTemp}°C (continuing for demo)");
            }

            return (currentProductTemp, nozzleTemp);
        }

        private async Task<double> FillJarAsync(FillParameters fillParams)
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.IND_JAR_PRESENT, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.IND_FILLING, true);

            // Tare weight
            double tareWeight = EmptyJarWeight + (_rand.NextDouble() - 0.5) * 5.0;
            UaWriteHelpers.Write(BatchPlantNodeManager.LC501_TARE, Math.Round(tareWeight, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.LC501, Math.Round(tareWeight, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.LC501_NET, 0.0);

            double fastFillTarget = fillParams.TargetWeight * 0.90;
            double dribbleFillTarget = fillParams.TargetWeight - fillParams.InflightComp;

            // Stage 1: Fast Fill
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_FAST, 1);
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_DRIBBLE, 0);

            double fastFillTime = fastFillTarget / FastFillRate;
            int fastFillSteps = Math.Max(1, (int)(fastFillTime * 10));

            double currentWeight = 0.0;
            for (int i = 0; i <= fastFillSteps; i++)
            {
                double fillProgress = (double)i / fastFillSteps;
                currentWeight = fastFillTarget * fillProgress;
                currentWeight += (_rand.NextDouble() - 0.5) * 2.0;

                UaWriteHelpers.Write(BatchPlantNodeManager.LC501, Math.Round(tareWeight + currentWeight, 1));
                UaWriteHelpers.Write(BatchPlantNodeManager.LC501_NET, Math.Round(currentWeight, 1));

                await Task.Delay(50);
            }

            // Stage 2: Dribble Fill
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_FAST, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_DRIBBLE, 1);

            double remainingWeight = dribbleFillTarget - currentWeight;
            double dribbleFillTime = remainingWeight / DribbleFillRate;
            int dribbleFillSteps = Math.Max(1, (int)(dribbleFillTime * 10));

            for (int i = 0; i <= dribbleFillSteps; i++)
            {
                double fillProgress = (double)i / dribbleFillSteps;
                double dribbleWeight = currentWeight + (remainingWeight * fillProgress);
                dribbleWeight += (_rand.NextDouble() - 0.5) * 1.0;

                UaWriteHelpers.Write(BatchPlantNodeManager.LC501, Math.Round(tareWeight + dribbleWeight, 1));
                UaWriteHelpers.Write(BatchPlantNodeManager.LC501_NET, Math.Round(dribbleWeight, 1));

                await Task.Delay(50);
            }

            // Close valves
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_FAST, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_DRIBBLE, 0);

            // Inflight settling
            await Task.Delay(100);

            // Final weight with realistic variation
            double fillVariation = (_rand.NextDouble() + _rand.NextDouble() + _rand.NextDouble() - 1.5) * 6.0;
            double finalNetWeight = fillParams.TargetWeight + fillVariation;
            finalNetWeight = Math.Max(500, Math.Min(550, finalNetWeight));

            double finalGrossWeight = tareWeight + finalNetWeight;

            UaWriteHelpers.Write(BatchPlantNodeManager.LC501, Math.Round(finalGrossWeight, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.LC501_NET, Math.Round(finalNetWeight, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.CURRENT_JAR_WEIGHT, Math.Round(finalNetWeight, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.CURRENT_JAR_TEMP, Math.Round(Convert.ToDouble(BatchPlantNodeManager.TT501.Value), 1));

            // Headspace calculation
            double expectedHeadspace = 9.0 + (fillParams.TargetWeight - finalNetWeight) * 0.02;
            expectedHeadspace += (_rand.NextDouble() - 0.5) * 1.0;
            expectedHeadspace = Math.Max(5, Math.Min(15, expectedHeadspace));
            UaWriteHelpers.Write(BatchPlantNodeManager.HEADSPACE_ACTUAL, Math.Round(expectedHeadspace, 1));

            return finalNetWeight;
        }

        private string ClassifyJar(double finalNetWeight, FillParameters fillParams, ref FillingStatistics stats)
        {
            string jarStatus;
            bool weightOk;

            if (finalNetWeight < fillParams.UnderweightLimit)
            {
                jarStatus = "UNDERWEIGHT";
                stats.UnderweightJars++;
                weightOk = false;
                Console.WriteLine($"[REJECT] Jar #{stats.TotalJars + 1}: {finalNetWeight:F1}g - UNDERWEIGHT");
            }
            else if (finalNetWeight > fillParams.OverweightLimit)
            {
                jarStatus = "OVERWEIGHT";
                stats.OverweightJars++;
                stats.PassedJars++;
                weightOk = true;
                double giveaway = finalNetWeight - fillParams.TargetWeight;
                stats.TotalGiveaway += giveaway;
            }
            else
            {
                jarStatus = "PASS";
                stats.PassedJars++;
                weightOk = true;
            }

            stats.TotalJars++;
            stats.WeightSum += finalNetWeight;
            stats.WeightSumSquares += finalNetWeight * finalNetWeight;

            if (finalNetWeight < stats.MinWeight) stats.MinWeight = finalNetWeight;
            if (finalNetWeight > stats.MaxWeight) stats.MaxWeight = finalNetWeight;

            UaWriteHelpers.Write(BatchPlantNodeManager.CURRENT_JAR_STATUS, jarStatus);
            UaWriteHelpers.Write(BatchPlantNodeManager.IND_WEIGHT_OK, weightOk);

            return jarStatus;
        }

        private void UpdateStatisticsNodes(FillingStatistics stats, double finalNetWeight, double targetWeight)
        {
            double avgWeight = stats.WeightSum / stats.TotalJars;
            double variance = (stats.WeightSumSquares / stats.TotalJars) - (avgWeight * avgWeight);
            double stdDev = Math.Sqrt(Math.Max(0, variance));
            double firstPassYield = (double)stats.PassedJars / stats.TotalJars * 100.0;

            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_TOTAL, stats.TotalJars);
            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_PASS, stats.PassedJars);
            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_UNDERWEIGHT, stats.UnderweightJars);
            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_OVERWEIGHT, stats.OverweightJars);

            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_AVG, Math.Round(avgWeight, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_MIN, Math.Round(stats.MinWeight, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_MAX, Math.Round(stats.MaxWeight, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_STDDEV, Math.Round(stdDev, 2));
            UaWriteHelpers.Write(BatchPlantNodeManager.FIRST_PASS_YIELD, Math.Round(firstPassYield, 1));
            UaWriteHelpers.Write(BatchPlantNodeManager.GIVEAWAY_TOTAL, Math.Round(stats.TotalGiveaway, 1));

            UaWriteHelpers.Write(BatchPlantNodeManager.PREVIOUS_JAR_WEIGHT, Math.Round(finalNetWeight, 1));

            UaWriteHelpers.Write(BatchPlantNodeManager.IND_FILLING, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.IND_JAR_PRESENT, false);
        }

        private double UpdateReactorLevel(double currentReactorLevel, double finalNetWeight, double jarVolume)
        {
            double productUsed = finalNetWeight / 1000.0 / ProductDensity;
            double newReactorLevel = currentReactorLevel - productUsed;
            UaWriteHelpers.Write(BatchPlantNodeManager.LT401, Math.Round(newReactorLevel, 1));
            return newReactorLevel;
        }

        private void UpdateTimingAndEnergy(DateTime fillStartTime, ref FillingStatistics stats, double secondsPerJar)
        {
            double elapsedSeconds = (DateTime.Now - fillStartTime).TotalSeconds;
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_ELAPSED_TIME, Math.Round(elapsedSeconds, 0));

            double powerKw = 2.5;
            double energyIncrement = powerKw * (secondsPerJar / 3600.0);
            stats.EnergyConsumed += energyIncrement;
            UaWriteHelpers.Write(BatchPlantNodeManager.filler_energy_consumed, Math.Round(stats.EnergyConsumed, 4));
        }

        private void LogJarProgress(FillingStatistics stats, double finalNetWeight, string jarStatus,
            double nozzleTemp, double newReactorLevel, DateTime fillStartTime)
        {
            double elapsedSeconds = (DateTime.Now - fillStartTime).TotalSeconds;
            double avgWeight = stats.WeightSum / stats.TotalJars;
            double variance = (stats.WeightSumSquares / stats.TotalJars) - (avgWeight * avgWeight);
            double stdDev = Math.Sqrt(Math.Max(0, variance));
            double firstPassYield = (double)stats.PassedJars / stats.TotalJars * 100.0;

            if (stats.TotalJars % 20 == 0 || stats.TotalJars == 1)
            {
                double actualRate = stats.TotalJars / (elapsedSeconds / 60.0);
                Console.WriteLine($"[Jar #{stats.TotalJars}] Weight: {finalNetWeight:F1}g ({jarStatus}) | " +
                                $"Temp: {nozzleTemp:F1}°C | Reactor: {newReactorLevel:F0}L | " +
                                $"Rate: {actualRate:F1}/min | Yield: {firstPassYield:F1}%");
            }

            if (stats.TotalJars == 100)
            {
                Console.WriteLine($">>> 100 jars milestone - Avg: {avgWeight:F1}g, StdDev: {stdDev:F2}g");
            }
            else if (stats.TotalJars == 500)
            {
                Console.WriteLine($">>> 500 jars milestone - Yield: {firstPassYield:F1}%, Giveaway: {stats.TotalGiveaway:F0}g");
            }

            if (stdDev > 5.0 && stats.TotalJars > 20)
            {
                Console.WriteLine($"[WARNING] High weight variation - StdDev: {stdDev:F2}g");
            }

            if (firstPassYield < 95.0 && stats.TotalJars > 50)
            {
                Console.WriteLine($"[WARNING] Low yield: {firstPassYield:F1}%");
            }
        }

        private async Task WaitForNextJarCycle(DateTime fillStartTime, int fillCycleMs)
        {
            int remainingCycleTime = fillCycleMs - (int)((DateTime.Now - fillStartTime).TotalMilliseconds % fillCycleMs);
            if (remainingCycleTime > 100)
            {
                await Task.Delay(Math.Min(remainingCycleTime, 500));
            }
        }

        private void FinalizeFillingOperation(DateTime fillStartTime, FillingStatistics stats, double targetWeight)
        {
            // Stop equipment
            UaWriteHelpers.Write(BatchPlantNodeManager.CONV501_RUN, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.CONV501_SPEED, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.P501_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.P501_RUN, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.P501_SPEED, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.CAP501_RUN, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_FAST, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FV501_DRIBBLE, 0);

            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RUN, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.MT401_RPM, 0.0);

            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_END_TIME, DateTime.Now);
            UaWriteHelpers.Write(BatchPlantNodeManager.IND_FILLING, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.IND_JAR_PRESENT, false);

            // Calculate final statistics
            double finalAvgWeight = stats.TotalJars > 0 ? stats.WeightSum / stats.TotalJars : 0;
            double finalVariance = stats.TotalJars > 0 ? (stats.WeightSumSquares / stats.TotalJars) - (finalAvgWeight * finalAvgWeight) : 0;
            double finalStdDev = Math.Sqrt(Math.Max(0, finalVariance));
            double finalYield = stats.TotalJars > 0 ? (double)stats.PassedJars / stats.TotalJars * 100.0 : 0;
            double totalFillTime = (DateTime.Now - fillStartTime).TotalMinutes;
            double actualFillRate = stats.TotalJars / totalFillTime;

            UaWriteHelpers.Write(BatchPlantNodeManager.filler_status, "Complete");
            UaWriteHelpers.Write(BatchPlantNodeManager.FL501_STATE, 1);
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Batch Complete");

            Console.WriteLine("=== Stage 4: Filling Operation Complete ===");
            Console.WriteLine($"");
            Console.WriteLine($"  PRODUCTION SUMMARY");
            Console.WriteLine($"  ══════════════════════════════════════");
            Console.WriteLine($"  Total Jars Filled:    {stats.TotalJars}");
            Console.WriteLine($"  Passed:               {stats.PassedJars} ({finalYield:F1}%)");
            Console.WriteLine($"  Underweight Rejects:  {stats.UnderweightJars}");
            Console.WriteLine($"  Overweight (accepted):{stats.OverweightJars}");
            Console.WriteLine($"  Temperature Rejects:  {stats.TempRejectJars}");
            Console.WriteLine($"");
            Console.WriteLine($"  WEIGHT STATISTICS");
            Console.WriteLine($"  ──────────────────────────────────────");
            Console.WriteLine($"  Target Weight:        {targetWeight:F1}g");
            Console.WriteLine($"  Average Weight:       {finalAvgWeight:F2}g");
            Console.WriteLine($"  Std Deviation:        {finalStdDev:F2}g");
            Console.WriteLine($"  Min Weight:           {stats.MinWeight:F1}g");
            Console.WriteLine($"  Max Weight:           {stats.MaxWeight:F1}g");
            Console.WriteLine($"  Total Giveaway:       {stats.TotalGiveaway:F1}g ({stats.TotalGiveaway / 1000:F2}kg)");
            Console.WriteLine($"");
            Console.WriteLine($"  TIMING");
            Console.WriteLine($"  ──────────────────────────────────────");
            Console.WriteLine($"  Fill Time:            {totalFillTime:F1} minutes");
            Console.WriteLine($"  Actual Rate:          {actualFillRate:F1} jars/min");
            Console.WriteLine($"  Energy Consumed:      {stats.EnergyConsumed:F3} kWh");
            Console.WriteLine($"");
            Console.WriteLine(">>> Batch filling complete - Ready for next batch");
        }

        private class FillParameters
        {
            public double TargetWeight { get; set; }
            public double JarVolume { get; set; }
            public double OverweightLimit { get; set; }
            public double UnderweightLimit { get; set; }
            public double InflightComp { get; set; }
            public double FillRate { get; set; }
            public int TotalJarsEstimate { get; set; }
            public double EstimatedFillTime { get; set; }
            public double SecondsPerJar { get; set; }
            public int FillCycleMs { get; set; }
        }

        private class FillingStatistics
        {
            public int TotalJars { get; set; } = 0;
            public int PassedJars { get; set; } = 0;
            public int UnderweightJars { get; set; } = 0;
            public int OverweightJars { get; set; } = 0;
            public int TempRejectJars { get; set; } = 0;
            public double WeightSum { get; set; } = 0.0;
            public double WeightSumSquares { get; set; } = 0.0;
            public double MinWeight { get; set; } = double.MaxValue;
            public double MaxWeight { get; set; } = double.MinValue;
            public double TotalGiveaway { get; set; } = 0.0;
            public double EnergyConsumed { get; set; } = 0.0;
        }
    }
}
