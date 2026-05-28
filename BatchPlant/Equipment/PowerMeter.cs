using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    public class PowerMeterTagMap
    {
        public string? ActivePower { get; set; }
        public string? ActiveEnergy { get; set; }
        public string? ReactivePower { get; set; }
        public string? PowerFactor { get; set; }
        public string? Voltage { get; set; }
        public string? Current { get; set; }
        public string? Frequency { get; set; }
        public string? ThdCurrent { get; set; }
        public string? VoltageUnbalance { get; set; }
    }

    /// <summary>
    /// Three-phase power meter model (Schneider PM5xxx / Siemens SENTRON class).
    /// Receives active power and load characteristics from the controller each tick,
    /// derives all other electrical values, and writes to OPC UA tags.
    /// </summary>
    public class PowerMeter
    {
        private readonly PlantContext _ctx;
        private readonly string _name;
        private PowerMeterTagMap _tags = new();

        // Electrical system configuration
        public double NominalVoltage { get; set; } = 480.0;    // V line-to-line (US industrial)
        public double NominalFrequency { get; set; } = 60.0;   // Hz
        public double MaxExpectedLoad { get; set; } = 100.0;   // kW (for voltage sag scaling)

        // Current state (readable by controller for logging)
        public double ActivePowerKw { get; private set; }
        public double ActiveEnergyKwh { get; private set; }
        public double ReactivePowerKvar { get; private set; }
        public double CurrentPowerFactor { get; private set; }
        public double VoltageV { get; private set; }
        public double CurrentA { get; private set; }
        public double FrequencyHz { get; private set; }
        public double ThdCurrentPct { get; private set; }
        public double VoltageUnbalancePct { get; private set; }

        public PowerMeter(PlantContext ctx, string name)
        {
            _ctx = ctx;
            _name = name;
            VoltageV = NominalVoltage;
            FrequencyHz = NominalFrequency;
        }

        public PowerMeter Bind(PowerMeterTagMap tags)
        {
            _tags = tags;
            return this;
        }

        /// <summary>
        /// Update meter with current load conditions. Called by the controller each tick.
        /// </summary>
        /// <param name="activePowerKw">Total active power draw for this work center (kW)</param>
        /// <param name="powerFactor">Weighted power factor for the current load mix (0.0-1.0)</param>
        /// <param name="thdContribution">Additional THD from VFDs/nonlinear loads (%)</param>
        /// <param name="dt">Simulation time step</param>
        public void Update(double activePowerKw, double powerFactor, double thdContribution, TimeSpan dt)
        {
            activePowerKw = Math.Max(0, activePowerKw);
            powerFactor = Math.Clamp(powerFactor, 0.1, 1.0);

            ActivePowerKw = _ctx.AddNoise(activePowerKw, activePowerKw * 0.005 + 0.05);

            // Energy integration (kWh = kW * hours)
            ActiveEnergyKwh += activePowerKw * dt.TotalHours;

            // Reactive power: Q = P * tan(arccos(PF))
            double pfAngle = Math.Acos(Math.Clamp(powerFactor, 0.1, 0.999));
            ReactivePowerKvar = _ctx.AddNoise(
                ActivePowerKw * Math.Tan(pfAngle),
                0.02);
            CurrentPowerFactor = _ctx.AddNoise(powerFactor, 0.003);

            // Apparent power for current calculation: S = P / PF
            double apparentPowerKva = ActivePowerKw / Math.Max(powerFactor, 0.1);

            // Voltage sags slightly under heavy load (up to 2% at full load)
            double loadFraction = Math.Clamp(activePowerKw / MaxExpectedLoad, 0, 1.0);
            double voltageSag = 1.0 - 0.02 * loadFraction;
            VoltageV = _ctx.AddNoise(NominalVoltage * voltageSag, 0.8);

            // Current from apparent power: I = S*1000 / (sqrt(3) * V)
            CurrentA = (apparentPowerKva * 1000.0) / (Math.Sqrt(3) * VoltageV);
            CurrentA = _ctx.AddNoise(CurrentA, CurrentA * 0.003 + 0.01);

            // Grid frequency (very stable in US/EU interconnected grids)
            FrequencyHz = _ctx.AddNoise(NominalFrequency, 0.015);

            // THD on current: base distortion + VFD contribution
            double baseThdPct = 2.5;
            ThdCurrentPct = _ctx.AddNoise(baseThdPct + thdContribution, 0.2);
            ThdCurrentPct = Math.Max(0.5, ThdCurrentPct);

            // Voltage unbalance: normally 0.5-1.5%, slightly worse under unbalanced loads
            double baseUnbalance = 0.8 + 0.3 * loadFraction;
            VoltageUnbalancePct = _ctx.AddNoise(baseUnbalance, 0.1);
            VoltageUnbalancePct = Math.Clamp(VoltageUnbalancePct, 0.1, 5.0);

            WriteTags();
        }

        private void WriteTags()
        {
            var t = _ctx.Tags;
            if (_tags.ActivePower != null) t.Write(_tags.ActivePower, Math.Round(ActivePowerKw, 2));
            if (_tags.ActiveEnergy != null) t.Write(_tags.ActiveEnergy, Math.Round(ActiveEnergyKwh, 1));
            if (_tags.ReactivePower != null) t.Write(_tags.ReactivePower, Math.Round(ReactivePowerKvar, 2));
            if (_tags.PowerFactor != null) t.Write(_tags.PowerFactor, Math.Round(Math.Clamp(CurrentPowerFactor, 0, 1.0), 3));
            if (_tags.Voltage != null) t.Write(_tags.Voltage, Math.Round(VoltageV, 1));
            if (_tags.Current != null) t.Write(_tags.Current, Math.Round(CurrentA, 2));
            if (_tags.Frequency != null) t.Write(_tags.Frequency, Math.Round(FrequencyHz, 3));
            if (_tags.ThdCurrent != null) t.Write(_tags.ThdCurrent, Math.Round(ThdCurrentPct, 1));
            if (_tags.VoltageUnbalance != null) t.Write(_tags.VoltageUnbalance, Math.Round(VoltageUnbalancePct, 2));
        }
    }
}
