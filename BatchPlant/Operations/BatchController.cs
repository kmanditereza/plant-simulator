using BatchPlant;
using BatchPlant.Helpers;
using BatchPlant.Operations;
using BatchPlant.Helpers;

namespace BatchPlant.Operations
{
    /// <summary>
    /// Orchestrates the complete batch production process through all stages.
    /// </summary>
    public class BatchController
    {
        private readonly MaterialLoader _materialLoader;
        private readonly MixerController _mixerController;
        private readonly HeatExchangerController _heatExchangerController;
        private readonly ReactorController _reactorController;
        private readonly FillerController _fillerController;

        /// <summary>
        /// Optional reference to utilities controller for demand integration.
        /// </summary>
        public UtilitiesController? Utilities { get; set; }

        public BatchController()
        {
            _materialLoader = new MaterialLoader();
            _mixerController = new MixerController();
            _heatExchangerController = new HeatExchangerController();
            _reactorController = new ReactorController();
            _fillerController = new FillerController();
        }

        /// <summary>
        /// Execute continuous batch production loop.
        /// </summary>
        public async Task RunContinuousProductionAsync()
        {
            int batchesDone = Convert.ToInt32(BatchPlantNodeManager.batches_done.Value);
            int batchesRequired = Convert.ToInt32(BatchPlantNodeManager.batches_required.Value);

            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         KETCHUP BATCH PLANT - CONTINUOUS PRODUCTION        ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            while (batchesRequired == -1 || batchesDone < batchesRequired)
            {
                bool batchStart = Convert.ToBoolean(BatchPlantNodeManager.BATCH_START.Value);

                if (batchStart)
                {
                    await ExecuteSingleBatchAsync();

                    batchesDone = Convert.ToInt32(BatchPlantNodeManager.batches_done.Value);
                    batchesDone++;
                    UaWriteHelpers.Write(BatchPlantNodeManager.batches_done, batchesDone);

                    Console.WriteLine();
                    Console.WriteLine($"═══════════════════════════════════════════════════════════════");
                    Console.WriteLine($"  BATCH #{batchesDone} COMPLETE - Starting next batch...");
                    Console.WriteLine($"═══════════════════════════════════════════════════════════════");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("[WAITING] Batch start signal not received. Waiting...");
                    await Task.Delay(5000);
                }
            }

            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              ALL BATCHES COMPLETE                          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        }

        /// <summary>
        /// Execute a single batch from start to finish.
        /// </summary>
        public async Task ExecuteSingleBatchAsync()
        {
            DateTime batchStartTime = DateTime.Now;
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_start_time, batchStartTime);

            string batchCode = GenerateBatchCode();
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_code, batchCode);

            Console.WriteLine();
            Console.WriteLine($"┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine($"│  BATCH: {batchCode,-50}│");
            Console.WriteLine($"│  START: {batchStartTime:yyyy-MM-dd HH:mm:ss,-50}│");
            Console.WriteLine($"└────────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Reset material loader for new batch
            _materialLoader.ResetBatchWeight();

            // Stage 0: Material Loading
            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine("  STAGE 0: RAW MATERIAL LOADING");
            Console.WriteLine("──────────────────────────────────────────────────────────────");

            // Low utility demand during loading
            Utilities?.SetSteamDemand(5);
            Utilities?.SetCoolingDemand(10);

            await _materialLoader.LoadMaterial1Async();
            await _materialLoader.LoadMaterial2Async();
            await _materialLoader.LoadMaterial3Async();

            Console.WriteLine($">>> Total batch weight: {_materialLoader.BatchTotalWeight:F1} kg");
            Console.WriteLine();

            // Stage 1: Initial Mixing
            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine("  STAGE 1: INITIAL MIXING");
            Console.WriteLine("──────────────────────────────────────────────────────────────");

            // Moderate utility demand during mixing
            Utilities?.SetSteamDemand(10);
            Utilities?.SetCoolingDemand(15);

            await _mixerController.ExecuteAsync();
            Console.WriteLine();

            // Stage 2: Heat Exchanger
            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine("  STAGE 2: HEAT EXCHANGER");
            Console.WriteLine("──────────────────────────────────────────────────────────────");

            // High steam demand during heating
            Utilities?.SetSteamDemand(80);
            Utilities?.SetCoolingDemand(20);

            await _heatExchangerController.ExecuteAsync();
            Console.WriteLine();

            // Stage 3: Reactor/Cooking
            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine("  STAGE 3: REACTOR / COOKING");
            Console.WriteLine("──────────────────────────────────────────────────────────────");

            // High steam demand for cooking, moderate cooling for control
            Utilities?.SetSteamDemand(60);
            Utilities?.SetCoolingDemand(40);

            await _reactorController.ExecuteAsync();
            Console.WriteLine();

            // Stage 4: Filling
            Console.WriteLine("──────────────────────────────────────────────────────────────");
            Console.WriteLine("  STAGE 4: FILLING");
            Console.WriteLine("──────────────────────────────────────────────────────────────");

            // Low steam, low cooling during filling
            Utilities?.SetSteamDemand(15);
            Utilities?.SetCoolingDemand(25);

            await _fillerController.ExecuteAsync();
            Console.WriteLine();

            // Batch complete - return to idle demands
            Utilities?.SetSteamDemand(5);
            Utilities?.SetCoolingDemand(10);

            // Batch completion
            DateTime batchEndTime = DateTime.Now;
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_end_time, batchEndTime);

            TimeSpan batchDuration = batchEndTime - batchStartTime;

            Console.WriteLine($"┌────────────────────────────────────────────────────────────┐");
            Console.WriteLine($"│  BATCH COMPLETE: {batchCode,-42}│");
            Console.WriteLine($"│  DURATION: {batchDuration.TotalMinutes:F1} minutes{new string(' ', 39)}│");
            Console.WriteLine($"└────────────────────────────────────────────────────────────┘");

            // Clear batch parameters for next batch
            await ClearBatchParametersAsync();
        }

        /// <summary>
        /// Generate a unique batch code.
        /// </summary>
        private string GenerateBatchCode()
        {
            string productCode = BatchPlantNodeManager.product_code.Value?.ToString() ?? "SKU-10045";
            return $"{DateTime.Now:yyyyMMdd-HHmmss}-{productCode}-L1";
        }

        /// <summary>
        /// Reset batch parameters for the next batch.
        /// </summary>
        private async Task ClearBatchParametersAsync()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.material1_actual, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.material2_actual, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.material3_actual, 0.0);

            UaWriteHelpers.Write(BatchPlantNodeManager.mixer_energy_consumed, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.hx_energy_consumed, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.reactor_energy_consumed, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.filler_energy_consumed, 0.0);

            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_TOTAL, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_PASS, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_UNDERWEIGHT, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_OVERWEIGHT, 0);
            UaWriteHelpers.Write(BatchPlantNodeManager.CNT501_TEMP_REJECT, 0);

            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_AVG, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_MIN, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_MAX, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FILL_WEIGHT_STDDEV, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.FIRST_PASS_YIELD, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.GIVEAWAY_TOTAL, 0.0);

            UaWriteHelpers.Write(BatchPlantNodeManager.ACID_TOTAL, 0.0);

            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Idle - Ready for next batch");

            await Task.Delay(100);
        }
    }
}