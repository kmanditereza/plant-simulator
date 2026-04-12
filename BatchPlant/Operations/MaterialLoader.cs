using BatchPlant;
using BatchPlant.Helpers;
using BatchPlant.Helpers;

namespace BatchPlant.Operations
{
    /// <summary>
    /// Handles loading of raw materials into the mixer.
    /// </summary>
    public class MaterialLoader
    {
        private readonly Random _rand = new();

        /// <summary>
        /// Gets or sets the cumulative batch weight from loaded materials.
        /// </summary>
        public double BatchTotalWeight { get; private set; } = 0.0;

        /// <summary>
        /// Reset the batch total weight for a new batch.
        /// </summary>
        public void ResetBatchWeight()
        {
            BatchTotalWeight = 0.0;
        }

        /// <summary>
        /// Load Material 1 (Tomato Paste) into the mixer.
        /// </summary>
        public async Task LoadMaterial1Async()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.FCV101, 95.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.P101_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.P101_RUN, BatchPlantNodeManager.P101_START.Value);
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Loading : " + BatchPlantNodeManager.material1_name.Value);

            Console.WriteLine($"Material 1: Target - {BatchPlantNodeManager.material1_target.Value}");

            while (Convert.ToDouble(BatchPlantNodeManager.material1_actual.Value) <= Convert.ToDouble(BatchPlantNodeManager.material1_target.Value))
            {
                double currentValue = Convert.ToDouble(BatchPlantNodeManager.material1_actual.Value);
                double increment = _rand.NextDouble() * (15 - 10) + 10;
                double material1Actual = currentValue + increment;

                UaWriteHelpers.Write(BatchPlantNodeManager.material1_actual, material1Actual);

                // Refill silo if empty
                if (Convert.ToDouble(BatchPlantNodeManager.LT101.Value) <= 500.0)
                {
                    UaWriteHelpers.Write(BatchPlantNodeManager.LT101, 50000.0);
                }

                Console.WriteLine($"Material 1 Current Value: {BatchPlantNodeManager.material1_actual.Value}");

                UaWriteHelpers.Write(BatchPlantNodeManager.LT201, Convert.ToDouble(BatchPlantNodeManager.LT201.Value) + increment);
                UaWriteHelpers.Write(BatchPlantNodeManager.LT101, Convert.ToDouble(BatchPlantNodeManager.LT101.Value) - increment);

                await Task.Delay(12000);
            }

            BatchTotalWeight += Convert.ToDouble(BatchPlantNodeManager.material1_actual.Value);

            UaWriteHelpers.Write(BatchPlantNodeManager.FCV101, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.P101_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.P101_RUN, BatchPlantNodeManager.P101_START.Value);

            Console.WriteLine("Material 1 Target reached.");
        }

        /// <summary>
        /// Load Material 2 (Additives) into the mixer.
        /// </summary>
        public async Task LoadMaterial2Async()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.FCV102, 95.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.P102_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.P102_RUN, BatchPlantNodeManager.P102_START.Value);
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Loading : " + BatchPlantNodeManager.material2_name.Value);

            Console.WriteLine($"Material 2: Target - {BatchPlantNodeManager.material2_target.Value}");

            while (Convert.ToDouble(BatchPlantNodeManager.material2_actual.Value) <= Convert.ToDouble(BatchPlantNodeManager.material2_target.Value))
            {
                double currentValue = Convert.ToDouble(BatchPlantNodeManager.material2_actual.Value);
                double increment = _rand.NextDouble() * (15 - 10) + 10;
                double material2Actual = currentValue + increment;

                UaWriteHelpers.Write(BatchPlantNodeManager.material2_actual, material2Actual);

                // Refill silo if empty
                if (Convert.ToDouble(BatchPlantNodeManager.LT102.Value) <= 500.0)
                {
                    UaWriteHelpers.Write(BatchPlantNodeManager.LT102, 50000.0);
                }

                Console.WriteLine($"Material 2 Current Value: {BatchPlantNodeManager.material2_actual.Value}");

                UaWriteHelpers.Write(BatchPlantNodeManager.LT201, Convert.ToDouble(BatchPlantNodeManager.LT201.Value) + increment);
                UaWriteHelpers.Write(BatchPlantNodeManager.LT102, Convert.ToDouble(BatchPlantNodeManager.LT102.Value) - increment);

                await Task.Delay(6000);
            }

            BatchTotalWeight += Convert.ToDouble(BatchPlantNodeManager.material2_actual.Value);

            UaWriteHelpers.Write(BatchPlantNodeManager.FCV102, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.P102_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.P102_RUN, BatchPlantNodeManager.P102_START.Value);

            Console.WriteLine("Material 2 Target reached.");
        }

        /// <summary>
        /// Load Material 3 (Seasoning Blend) into the mixer.
        /// </summary>
        public async Task LoadMaterial3Async()
        {
            UaWriteHelpers.Write(BatchPlantNodeManager.FCV103, 95.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.P103_START, true);
            UaWriteHelpers.Write(BatchPlantNodeManager.P103_RUN, BatchPlantNodeManager.P103_START.Value);
            UaWriteHelpers.Write(BatchPlantNodeManager.batch_status, "Loading : " + BatchPlantNodeManager.material3_name.Value);

            Console.WriteLine($"Material 3: Target - {BatchPlantNodeManager.material3_target.Value}");

            while (Convert.ToDouble(BatchPlantNodeManager.material3_actual.Value) <= Convert.ToDouble(BatchPlantNodeManager.material3_target.Value))
            {
                double currentValue = Convert.ToDouble(BatchPlantNodeManager.material3_actual.Value);
                double increment = _rand.NextDouble() * (15 - 10) + 10;
                double material3Actual = currentValue + increment;

                UaWriteHelpers.Write(BatchPlantNodeManager.material3_actual, material3Actual);

                // Refill silo if empty
                if (Convert.ToDouble(BatchPlantNodeManager.LT103.Value) <= 500.0)
                {
                    UaWriteHelpers.Write(BatchPlantNodeManager.LT103, 50000.0);
                }

                Console.WriteLine($"Material 3 Current Value: {BatchPlantNodeManager.material3_actual.Value}");

                UaWriteHelpers.Write(BatchPlantNodeManager.LT201, Convert.ToDouble(BatchPlantNodeManager.LT201.Value) + increment);
                UaWriteHelpers.Write(BatchPlantNodeManager.LT103, Convert.ToDouble(BatchPlantNodeManager.LT103.Value) - increment);

                await Task.Delay(4000);
            }

            BatchTotalWeight += Convert.ToDouble(BatchPlantNodeManager.material3_actual.Value);

            UaWriteHelpers.Write(BatchPlantNodeManager.FCV103, 0.0);
            UaWriteHelpers.Write(BatchPlantNodeManager.P103_START, false);
            UaWriteHelpers.Write(BatchPlantNodeManager.P103_RUN, BatchPlantNodeManager.P103_START.Value);

            Console.WriteLine("Material 3 Target reached.");
        }
    }
}