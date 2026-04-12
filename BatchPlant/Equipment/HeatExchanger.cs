using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Heat exchanger model (jacketed kettle or plate/SSHE).
    /// Includes fouling model, PID-controlled steam valve, and energy tracking.
    /// </summary>
    public class HeatExchanger : EquipmentBase
    {
        // Process values
        public double InletTemp { get; set; }
        public double OutletTemp { get; private set; }
        public double SteamValvePosition { get; set; }  // 0-100%
        public double SteamFlow { get; private set; }    // kg/h
        public double PressureDrop { get; private set; } // bar
        public double FoulingIndex { get; private set; } // 0-100%
        public double EnergyConsumed { get; set; }       // kWh cumulative

        // Configuration
        public double SteamTemp { get; set; } = 165;     // Saturated steam temp degC
        public double MaxSteamFlow { get; set; } = 200;  // kg/h
        public double BaselinePressureDrop { get; set; } = 0.3; // bar
        public double HeatTransferCoeff { get; set; } = 0.8;   // Efficiency
        public double FoulingRate { get; set; } = 0.001;        // %/sec while hot
        public double LatentHeat { get; set; } = 2163;          // kJ/kg

        // Tags
        private string _tInlet = "";
        private string _tOutlet = "";
        private string _tSteamValve = "";
        private string _tSteamFlow = "";
        private string _tDP = "";
        private string _tFouling = "";

        public HeatExchanger(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
        }

        public override void RegisterTags(string folderPath)
        {
            _tInlet = $"{TagPrefix}_TIN";
            _tOutlet = $"{TagPrefix}_TOUT";
            _tSteamValve = $"{TagPrefix}_SV";
            _tSteamFlow = $"{TagPrefix}_STMFLOW";
            _tDP = $"{TagPrefix}_DP";
            _tFouling = $"{TagPrefix}_FOULING";

            Ctx.Tags.Define(_tInlet, BuiltInType.Double, folderPath, "degC", $"{TagPrefix} Inlet Temperature");
            Ctx.Tags.Define(_tOutlet, BuiltInType.Double, folderPath, "degC", $"{TagPrefix} Outlet Temperature");
            Ctx.Tags.Define(_tSteamValve, BuiltInType.Double, folderPath, "%", $"{TagPrefix} Steam Valve Position");
            Ctx.Tags.Define(_tSteamFlow, BuiltInType.Double, folderPath, "kg/h", $"{TagPrefix} Steam Flow");
            Ctx.Tags.Define(_tDP, BuiltInType.Double, folderPath, "bar", $"{TagPrefix} Pressure Drop");
            Ctx.Tags.Define(_tFouling, BuiltInType.Double, folderPath, "%", $"{TagPrefix} Fouling Index");
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Steam flow proportional to valve position
            SteamFlow = MaxSteamFlow * (SteamValvePosition / 100.0);
            SteamFlow = Ctx.AddNoise(SteamFlow, SteamFlow * 0.02);

            // Heat transfer (reduced by fouling)
            double efficiency = HeatTransferCoeff * (1.0 - FoulingIndex * 0.005);
            efficiency = Math.Max(0.3, efficiency);

            double heatPower = (SteamFlow * LatentHeat) / 3600.0; // kW
            double tempRise = heatPower * efficiency * dtSec / 4.18; // Rough: 1L water = 4.18 kJ/degC
            OutletTemp = InletTemp + tempRise * 0.1; // Scaled for realistic response

            // Fouling accumulates while hot
            if (InletTemp > 60)
            {
                FoulingIndex += FoulingRate * dtSec;
                FoulingIndex = Math.Min(100, FoulingIndex);
            }

            // Pressure drop increases with fouling
            PressureDrop = BaselinePressureDrop * (1.0 + FoulingIndex * 0.02);
            PressureDrop = Ctx.AddNoise(PressureDrop, 0.005);

            // Energy tracking
            EnergyConsumed += heatPower * dt.TotalHours;
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            SteamFlow = 0;
            OutletTemp = InletTemp;
            PressureDrop = Ctx.AddNoise(0.05, 0.005);

            // Fouling slowly recovers when cool
            if (InletTemp < 40 && FoulingIndex > 0)
            {
                FoulingIndex -= 0.0001 * dt.TotalSeconds;
                FoulingIndex = Math.Max(0, FoulingIndex);
            }
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tInlet, Math.Round(InletTemp, 1));
            Ctx.Tags.Write(_tOutlet, Math.Round(Ctx.AddNoise(OutletTemp, 0.1), 1));
            Ctx.Tags.Write(_tSteamValve, Math.Round(SteamValvePosition, 1));
            Ctx.Tags.Write(_tSteamFlow, Math.Round(SteamFlow, 1));
            Ctx.Tags.Write(_tDP, Math.Round(PressureDrop, 3));
            Ctx.Tags.Write(_tFouling, Math.Round(FoulingIndex, 1));
        }
    }
}
