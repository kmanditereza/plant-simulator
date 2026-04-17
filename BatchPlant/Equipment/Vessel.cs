using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Maps vessel outputs to OPC UA tag names. Null entries are not published.
    /// </summary>
    public class VesselTagMap
    {
        public string? Level { get; set; }
        public string? LevelPct { get; set; }
        public string? Temperature { get; set; }
        public string? Pressure { get; set; }
        public string? Weight { get; set; }
    }

    /// <summary>
    /// Tank/vessel/kettle model with level, temperature, pressure tracking.
    /// Supports mass balance (inflows-outflows) and heat balance.
    /// Area controllers call AddMaterial/RemoveMaterial to move fluid in/out.
    /// </summary>
    public class Vessel : EquipmentBase
    {
        // Process values
        public double Level { get; set; }           // Liters
        public double Temperature { get; set; }     // degC
        public double Pressure { get; set; }        // bar (gauge)
        public double LevelPercent => Capacity > 0 ? (Level / Capacity) * 100.0 : 0;
        public double Weight => Level * Density;    // kg (mass-based on density)

        // Configuration
        public double Capacity { get; set; } = 5000;
        public double AmbientTemp { get; set; } = 22.0;
        public double HeatLossCoefficient { get; set; } = 0.001;
        public double AtmosphericPressure { get; set; } = 0.0;
        public double Density { get; set; } = 1.0;              // kg/L (water=1.0, ketchup~1.1)
        public bool IsJacketed { get; set; }
        public double JacketTemp { get; set; }
        public double JacketHeatTransferCoeff { get; set; } = 0.05;

        /// <summary>
        /// Additional heat input (kW) set by area controller each tick.
        /// Used for agitator heat dissipation, electrical heating elements, etc.
        /// </summary>
        public double ExternalHeatInputKw { get; set; } = 0;

        private VesselTagMap _tags = new();

        public override int Priority => 30;

        public Vessel(PlantContext ctx, string name) : base(ctx, name)
        {
            Temperature = AmbientTemp;
        }

        /// <summary>
        /// Bind this vessel's outputs to OPC UA tag names. Fluent.
        /// </summary>
        public Vessel Bind(VesselTagMap tags)
        {
            _tags = tags;
            return this;
        }

        /// <summary>
        /// Add material to the vessel (mass-weighted temperature mix).
        /// </summary>
        public void AddMaterial(double volume, double temperature)
        {
            if (volume <= 0) return;
            double newTotal = Level + volume;
            if (newTotal > Capacity) volume = Capacity - Level;
            if (volume <= 0) return;

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
        /// Remove material. Returns actual amount removed.
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
            if (IsJacketed && Level > 0 && Math.Abs(JacketTemp - Temperature) > 0.01)
            {
                double heatTransfer = JacketHeatTransferCoeff * (JacketTemp - Temperature) * dtSec;
                double fillFrac = Math.Min(1.0, Level / Capacity);
                Temperature += heatTransfer * fillFrac;
            }

            // External heat input (positive = heating, negative = cooling)
            if (ExternalHeatInputKw != 0 && Level > 0)
            {
                // specific heat ~3.5 kJ/(kg·K) for food product
                double massKg = Level * Density;
                double deltaT = (ExternalHeatInputKw * dtSec) / (massKg * 3.5);
                Temperature += deltaT;
            }
            ExternalHeatInputKw = 0; // reset each tick; controller re-sets if needed

            // Pressure (atmospheric vessels)
            if (AtmosphericPressure == 0)
            {
                Pressure = Ctx.AddNoise(0.0, 0.005);
            }

            Level = Math.Clamp(Level, 0, Capacity);
            Temperature = Math.Clamp(Temperature, -10, 200);

            WriteTags();
        }

        protected override void WriteTags()
        {
            if (_tags.Level != null) Ctx.Tags.Write(_tags.Level, Math.Round(Level, 1));
            if (_tags.LevelPct != null) Ctx.Tags.Write(_tags.LevelPct, Math.Round(LevelPercent, 1));
            if (_tags.Temperature != null) Ctx.Tags.Write(_tags.Temperature, Math.Round(Ctx.AddNoise(Temperature, 0.1), 1));
            if (_tags.Pressure != null) Ctx.Tags.Write(_tags.Pressure, Math.Round(Pressure, 3));
            if (_tags.Weight != null) Ctx.Tags.Write(_tags.Weight, Math.Round(Weight, 1));
        }
    }
}
