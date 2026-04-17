using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Maps vacuum system outputs to OPC UA tag names. Null entries are not published.
    /// </summary>
    public class VacuumSystemTagMap
    {
        public string? VacuumLevel { get; set; }
        public string? Run { get; set; }
        public string? Fault { get; set; }
        public string? Current { get; set; }
    }

    /// <summary>
    /// Vacuum pump + condenser system for deaeration.
    /// Maintains vacuum level (mmHg, 0 = atmospheric, 760 = perfect vacuum) with leak model.
    /// Used to remove dissolved oxygen from ketchup before filling.
    /// </summary>
    public class VacuumSystem : EquipmentBase
    {
        // Process values
        public double VacuumLevel { get; private set; }
        public double VacuumSetpoint { get; set; } = 700;
        public double Current { get; private set; }
        public double CondenserTemp { get; private set; }

        // Configuration
        public double MaxVacuum { get; set; } = 740;
        public double LeakRate { get; set; } = 2.0;       // mmHg/sec decay when off
        public double PumpDownRate { get; set; } = 30;    // mmHg/sec pull when pumping
        public double RatedCurrent { get; set; } = 20;

        private VacuumSystemTagMap _tags = new();

        public VacuumSystem(PlantContext ctx, string name) : base(ctx, name)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
        }

        public VacuumSystem Bind(VacuumSystemTagMap tags)
        {
            _tags = tags;
            return this;
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Pump down toward setpoint, fighting natural leak
            double pumpEffect = PumpDownRate * dtSec;
            double leakEffect = LeakRate * dtSec;

            if (VacuumLevel < VacuumSetpoint)
            {
                // Pulling down
                VacuumLevel += pumpEffect - leakEffect;
            }
            else
            {
                // At setpoint - maintain by just compensating for leaks
                double error = VacuumLevel - VacuumSetpoint;
                VacuumLevel -= error * 0.2; // return toward setpoint
                VacuumLevel -= leakEffect * 0.1; // slight over-pumping
            }

            VacuumLevel = Math.Clamp(VacuumLevel, 0, MaxVacuum);

            // Current based on whether we're actively pumping or just maintaining
            double loadFrac = VacuumLevel < VacuumSetpoint * 0.95 ? 1.0 : 0.4;
            Current = RatedCurrent * loadFrac;
            Current = Ctx.AddNoise(Current, 0.2);

            // Condenser temp rises with load
            CondenserTemp = 30 + (VacuumLevel / MaxVacuum) * 15;
            CondenserTemp = Ctx.AddNoise(CondenserTemp, 0.5);
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            // Vacuum decays due to leaks when pump is off
            VacuumLevel -= LeakRate * dt.TotalSeconds;
            VacuumLevel = Math.Max(0, VacuumLevel);
            Current = 0;
            CondenserTemp = 25;
        }

        protected override void WriteTags()
        {
            double noisy = Ctx.AddNoise(VacuumLevel, 1.5);
            noisy = Math.Max(0, noisy);

            if (_tags.VacuumLevel != null) Ctx.Tags.Write(_tags.VacuumLevel, Math.Round(noisy, 0));
            if (_tags.Run != null) Ctx.Tags.Write(_tags.Run, State == EquipmentState.Running);
            if (_tags.Fault != null) Ctx.Tags.Write(_tags.Fault, State == EquipmentState.Faulted);
            if (_tags.Current != null) Ctx.Tags.Write(_tags.Current, Math.Round(Current, 1));
        }
    }
}
