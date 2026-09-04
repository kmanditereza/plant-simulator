using BatchPlant.OpcUa;

namespace BatchPlant.Core
{
    /// <summary>
    /// Shared context passed to all simulation components.
    /// Provides access to clock, tag registry, and engine without global statics.
    /// </summary>
    public class PlantContext
    {
        public SimClock Clock { get; }
        public TagRegistry Tags { get; }
        public Random Rng { get; }

        /// <summary>
        /// Index of the condition monitors that have declared an identity, so the API can
        /// inject a vibration fault into a named asset without knowing which controller owns it.
        /// </summary>
        public Equipment.ConditionMonitorRegistry ConditionMonitors { get; } = new();

        public PlantContext(SimClock clock, TagRegistry tags, Random? rng = null)
        {
            Clock = clock;
            Tags = tags;
            Rng = rng ?? new Random();
        }

        /// <summary>
        /// Gaussian random number using Box-Muller transform.
        /// </summary>
        public double NextGaussian(double mean, double stdDev)
        {
            double u1 = 1.0 - Rng.NextDouble();
            double u2 = 1.0 - Rng.NextDouble();
            double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * normal;
        }

        /// <summary>
        /// Add realistic sensor noise to a value.
        /// </summary>
        public double AddNoise(double value, double noiseAmplitude)
        {
            return value + (Rng.NextDouble() * 2.0 - 1.0) * noiseAmplitude;
        }
    }
}
