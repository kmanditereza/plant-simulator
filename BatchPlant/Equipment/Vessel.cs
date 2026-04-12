using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Tank/vessel/kettle model with level, temperature, pressure tracking.
    /// Supports mass balance (inflows-outflows), heat balance, and optional agitation.
    /// Used for paste tanks, premix tank, cook kettle, buffer tanks, etc.
    /// </summary>
    public class Vessel : EquipmentBase
    {
        // Process values
        public double Level { get; set; }           // Liters
        public double Temperature { get; set; }     // degC
        public double Pressure { get; set; }        // bar (gauge)
        public double LevelPercent => Capacity > 0 ? (Level / Capacity) * 100.0 : 0;

        // Configuration
        public double Capacity { get; set; } = 5000;       // Liters max
        public double AmbientTemp { get; set; } = 22.0;    // degC
        public double HeatLossCoefficient { get; set; } = 0.001; // degC/sec per degC difference (insulation quality)
        public double AtmosphericPressure { get; set; } = 0.0;   // barg (0 = atmospheric)
        public bool IsJacketed { get; set; }
        public double JacketTemp { get; set; }              // Jacket/steam temp degC
        public double JacketHeatTransferCoeff { get; set; } = 0.05; // Heat transfer rate

        // Tags
        private string _tLevel = "";
        private string _tLevelPct = "";
        private string _tTemp = "";
        private string _tPress = "";

        public override int Priority => 30;

        public Vessel(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            Temperature = AmbientTemp;
        }

        public override void RegisterTags(string folderPath)
        {
            _tLevel = $"{TagPrefix}_LVL";
            _tLevelPct = $"{TagPrefix}_LVL_PCT";
            _tTemp = $"{TagPrefix}_TEMP";
            _tPress = $"{TagPrefix}_PRESS";

            Ctx.Tags.Define(_tLevel, BuiltInType.Double, folderPath, "L", $"{TagPrefix} Level");
            Ctx.Tags.Define(_tLevelPct, BuiltInType.Double, folderPath, "%", $"{TagPrefix} Level Percent");
            Ctx.Tags.Define(_tTemp, BuiltInType.Double, folderPath, "degC", $"{TagPrefix} Temperature");
            Ctx.Tags.Define(_tPress, BuiltInType.Double, folderPath, "bar", $"{TagPrefix} Pressure");
        }

        /// <summary>
        /// Add material to the vessel.
        /// </summary>
        /// <param name="volume">Liters to add.</param>
        /// <param name="temperature">Temperature of incoming material (degC).</param>
        public void AddMaterial(double volume, double temperature)
        {
            if (volume <= 0) return;
            double newTotal = Level + volume;
            if (newTotal > Capacity) volume = Capacity - Level;
            if (volume <= 0) return;

            // Mix temperature (mass-weighted average, assuming density ~1)
            if (Level > 0)
            {
                Temperature = (Level * Temperature + volume * temperature) / (Level + volume);
            }
            else
            {
                Temperature = temperature;
            }
            Level += volume;
        }

        /// <summary>
        /// Remove material from the vessel. Returns actual amount removed.
        /// </summary>
        public double RemoveMaterial(double volume)
        {
            double actual = Math.Min(volume, Level);
            Level -= actual;
            return actual;
        }

        public override void Tick(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Heat loss to ambient
            if (Level > 0)
            {
                double heatLoss = HeatLossCoefficient * (Temperature - AmbientTemp) * dtSec;
                Temperature -= heatLoss;
            }

            // Jacket heating/cooling
            if (IsJacketed && Level > 0 && JacketTemp != Temperature)
            {
                double heatTransfer = JacketHeatTransferCoeff * (JacketTemp - Temperature) * dtSec;
                // Scale heat transfer by fill fraction (less contact area when low level)
                double fillFrac = Math.Min(1.0, Level / Capacity);
                Temperature += heatTransfer * fillFrac;
            }

            // Pressure (for sealed vessels, increases with temp; otherwise atmospheric)
            if (AtmosphericPressure == 0)
            {
                Pressure = Ctx.AddNoise(0.0, 0.005); // Atmospheric gauge
            }

            Level = Math.Clamp(Level, 0, Capacity);
            Temperature = Math.Clamp(Temperature, -10, 200);

            WriteTags();
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tLevel, Math.Round(Level, 1));
            Ctx.Tags.Write(_tLevelPct, Math.Round(LevelPercent, 1));
            Ctx.Tags.Write(_tTemp, Math.Round(Ctx.AddNoise(Temperature, 0.1), 1));
            Ctx.Tags.Write(_tPress, Math.Round(Pressure, 3));
        }
    }
}
