using BatchPlant.Core;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 800 - Plant Utilities (Schneider BMS, ~2010)
    ///
    /// Steam system, cooling water, compressed air, hot water.
    /// Always running - no test cycle. Responds to demand from other areas.
    ///
    /// Cross-area coupling: process controllers (Area 300 cooking, Area 500 filling cooling tunnel)
    /// call RequestSteam() / RequestCooling() each tick with their current demand.
    /// Area 800 sums them, updates boiler firing and pressure droop, and publishes realistic
    /// utility supply conditions that consuming areas can read back.
    /// </summary>
    public class Area800_UtilitiesController : ISimulatable
    {
        private readonly PlantContext _ctx;

        public int Priority => 10; // Utilities tier - ticks early so process areas see fresh values

        // Steam system state
        public double SteamHeaderPressureBar { get; private set; } = 6.0;
        public double SteamHeaderTempC { get; private set; } = 165.0;
        public double SteamCapacityKgH { get; set; } = 2000;      // boiler max output
        public double SteamDemandKgH => _steamDemands.Values.Sum();
        public double SteamEnergyKwh { get; private set; }
        public double BoilerEfficiency { get; private set; } = 0.92;

        // Cooling water state
        public double CoolingSupplyTempC { get; private set; } = 8.0;
        public double CoolingReturnTempC { get; private set; } = 14.0;
        public double CoolingCapacityM3H { get; set; } = 300;
        public double CoolingDemandM3H => _coolingDemands.Values.Sum();
        public double CoolingEnergyKwh { get; private set; }

        // Compressed air (always available, passive)
        public double CompAirPressureBar { get; private set; } = 6.5;

        // Internal demand tracking (sourceId -> demand)
        private readonly Dictionary<string, double> _steamDemands = new();
        private readonly Dictionary<string, double> _coolingDemands = new();

        public Area800_UtilitiesController(PlantContext ctx)
        {
            _ctx = ctx;
        }

        // -------- Public API for other areas to call each tick --------

        /// <summary>
        /// Register current steam demand from a consumer (kg/hr). Pass 0 to stop drawing.
        /// Sources should call this every tick with their current demand.
        /// </summary>
        public void RequestSteam(string sourceId, double kgPerHour)
        {
            _steamDemands[sourceId] = Math.Max(0, kgPerHour);
        }

        /// <summary>
        /// Register current cooling water demand from a consumer (m³/hr).
        /// </summary>
        public void RequestCooling(string sourceId, double m3PerHour)
        {
            _coolingDemands[sourceId] = Math.Max(0, m3PerHour);
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            double dtHours = dt.TotalHours;

            // ---- Steam System ----
            double totalSteam = SteamDemandKgH;
            double demandFrac = totalSteam / SteamCapacityKgH;

            // Boiler response: pressure holds at 6.0 until 50% demand, then droops
            double targetPressure = 6.0;
            if (demandFrac > 0.5)
            {
                targetPressure = 6.0 - (demandFrac - 0.5) * 1.2; // droops to 5.4 at 100%
            }
            if (demandFrac > 1.0)
            {
                targetPressure = 5.4 - (demandFrac - 1.0) * 2.0; // more droop if overloaded
            }
            targetPressure = Math.Max(4.0, targetPressure);

            // Pressure follows target with small lag (first-order)
            SteamHeaderPressureBar += (targetPressure - SteamHeaderPressureBar) * 0.1;
            double noisyPressure = _ctx.AddNoise(SteamHeaderPressureBar, 0.03);

            // Saturated steam temperature ~ rough approximation
            SteamHeaderTempC = 100 + 15 * SteamHeaderPressureBar;
            double noisyTemp = _ctx.AddNoise(SteamHeaderTempC, 0.5);

            // Boiler efficiency depends on firing rate
            BoilerEfficiency = demandFrac switch
            {
                < 0.2 => 0.85,     // light load - poor efficiency
                < 0.4 => 0.89,
                < 0.7 => 0.93,     // sweet spot
                < 0.95 => 0.91,
                _ => 0.87           // overloaded
            };
            BoilerEfficiency = _ctx.AddNoise(BoilerEfficiency, 0.003);

            // Energy totalized (latent heat 2163 kJ/kg / boiler efficiency)
            double steamKgThisTick = totalSteam * dtHours;
            double energyThisTick = (steamKgThisTick * 2.163) / BoilerEfficiency;
            SteamEnergyKwh += energyThisTick;

            // Condensate return temperature drifts with load
            double condTemp = 70 + demandFrac * 20;
            condTemp = _ctx.AddNoise(condTemp, 1.0);

            // Feedwater temperature preheated by economizer
            double fwTemp = 55 + demandFrac * 25;
            fwTemp = _ctx.AddNoise(fwTemp, 0.5);

            // ---- Cooling Water System ----
            double totalCooling = CoolingDemandM3H;
            double coolDemandFrac = totalCooling / CoolingCapacityM3H;

            // Supply temperature stable (chiller/tower maintains)
            CoolingSupplyTempC = _ctx.AddNoise(8.0, 0.3);

            // Return temperature rises with load (simplified: 6°C ΔT at full load)
            CoolingReturnTempC = CoolingSupplyTempC + 6.0 * Math.Min(1.2, coolDemandFrac);
            CoolingReturnTempC = _ctx.AddNoise(CoolingReturnTempC, 0.3);

            // Two pumps - staged; pump 2 comes on above 60% demand
            bool pump1Running = true;
            bool pump2Running = coolDemandFrac > 0.6;

            // Two tower fans - staged; fan 2 comes on above 70% demand
            bool fan1Running = coolDemandFrac > 0.1;
            bool fan2Running = coolDemandFrac > 0.7;

            // Cooling energy (approximation)
            double coolM3ThisTick = totalCooling * dtHours;
            double coolDeltaT = CoolingReturnTempC - CoolingSupplyTempC;
            double coolEnergyThisTick = coolM3ThisTick * 1000 * 4.18 * coolDeltaT / 3600; // kWh
            CoolingEnergyKwh += coolEnergyThisTick;

            // ---- Compressed Air (passive, always available) ----
            CompAirPressureBar = _ctx.AddNoise(6.5, 0.08);

            // ---- Publish all Area 800 tags ----
            var t = _ctx.Tags;

            // Steam
            t.Write("STM_HDR_PRESS", Math.Round(noisyPressure, 2));
            t.Write("STM_HDR_TEMP", Math.Round(noisyTemp, 1));
            t.Write("STM_FLOW", Math.Round(_ctx.AddNoise(totalSteam, totalSteam * 0.01), 1));
            t.Write("STM_COND_TEMP", Math.Round(condTemp, 1));
            t.Write("BOILER_STS", 1); // always on
            t.Write("BOILER_PRESS", Math.Round(_ctx.AddNoise(SteamHeaderPressureBar + 1.0, 0.04), 2));
            t.Write("BOILER_FWATER_TEMP", Math.Round(fwTemp, 1));
            t.Write("BOILER_EFF", Math.Round(BoilerEfficiency * 100, 1));
            t.Write("STM_DEMAND", Math.Round(demandFrac * 100, 1));
            t.Write("STM_ENERGY", Math.Round(SteamEnergyKwh, 1));

            // Cooling water
            t.Write("CW_SUP_TEMP", Math.Round(CoolingSupplyTempC, 1));
            t.Write("CW_RET_TEMP", Math.Round(CoolingReturnTempC, 1));
            t.Write("CW_FLOW", Math.Round(_ctx.AddNoise(totalCooling, totalCooling * 0.01), 1));
            t.Write("CW_PUMP1_STS", pump1Running ? 1 : 0);
            t.Write("CW_PUMP2_STS", pump2Running ? 1 : 0);
            t.Write("CW_TOWER_FAN1", fan1Running);
            t.Write("CW_TOWER_FAN2", fan2Running);
            t.Write("CW_BASIN_LVL", Math.Round(_ctx.AddNoise(85.0 - coolDemandFrac * 2, 0.2), 1));
            t.Write("CW_COND", Math.Round(_ctx.AddNoise(350.0, 5.0), 0));
            t.Write("CW_DEMAND", Math.Round(coolDemandFrac * 100, 1));

            // Compressed air
            t.Write("COMP_AIR_PRESS", Math.Round(CompAirPressureBar, 2));
            t.Write("COMP_AIR_FLOW", Math.Round(_ctx.AddNoise(120.0, 5.0), 1));
            t.Write("COMP_AIR_DEW", Math.Round(_ctx.AddNoise(-40.0, 0.5), 1));
            t.Write("COMP_RUN", true);

            // Hot water (for CIP - simulated as steady)
            t.Write("HW_SUP_TEMP", Math.Round(_ctx.AddNoise(82.0, 0.3), 1));
            t.Write("HW_RET_TEMP", Math.Round(_ctx.AddNoise(65.0, 0.5), 1));
            t.Write("HW_FLOW", Math.Round(_ctx.AddNoise(0.0, 1.0), 1));
            t.Write("HW_PUMP_STS", 1);
        }
    }
}
