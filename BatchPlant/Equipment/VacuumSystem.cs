using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Vacuum pump + condenser system for deaeration.
    /// Maintains vacuum level (negative gauge pressure) with leak model.
    /// Used to remove dissolved oxygen from ketchup before filling.
    /// </summary>
    public class VacuumSystem : EquipmentBase
    {
        // Process values
        public double VacuumLevel { get; private set; }      // mmHg (0 = atmospheric, 760 = perfect vacuum)
        public double VacuumSetpoint { get; set; } = 700;    // mmHg target
        public double Current { get; private set; }          // A
        public double CondenserTemp { get; private set; }    // degC

        // Configuration
        public double MaxVacuum { get; set; } = 740;         // mmHg max achievable
        public double LeakRate { get; set; } = 0.5;          // mmHg/sec leak when pump off
        public double PumpDownRate { get; set; } = 50;       // mmHg/sec pump capacity
        public double RatedCurrent { get; set; } = 20;       // A

        // Tags
        private string _tVacuum = "";
        private string _tRun = "";
        private string _tFault = "";

        public VacuumSystem(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
        }

        public override void RegisterTags(string folderPath)
        {
            _tVacuum = $"{TagPrefix}_VAC";
            _tRun = $"{TagPrefix}_RUN";
            _tFault = $"{TagPrefix}_FAULT";

            Ctx.Tags.Define(_tVacuum, BuiltInType.Double, folderPath, "mmHg", $"{TagPrefix} Vacuum Level");
            Ctx.Tags.Define(_tRun, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Running");
            Ctx.Tags.Define(_tFault, BuiltInType.Boolean, folderPath, description: $"{TagPrefix} Fault");
        }

        protected override void UpdateRunning(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;

            // Pump toward setpoint, fighting leaks
            double pumpEffect = PumpDownRate * dtSec;
            double leakEffect = LeakRate * dtSec;

            if (VacuumLevel < VacuumSetpoint)
            {
                VacuumLevel += pumpEffect - leakEffect;
            }
            else
            {
                // Maintain: just compensate for leaks
                VacuumLevel += (leakEffect * 0.1) - leakEffect; // Slight overshoot/undershoot
            }

            VacuumLevel = Math.Clamp(VacuumLevel, 0, MaxVacuum);
            VacuumLevel = Ctx.AddNoise(VacuumLevel, 1.0);

            // Current based on load (higher when pumping down, lower when maintaining)
            double loadFrac = VacuumLevel < VacuumSetpoint * 0.95 ? 1.0 : 0.4;
            Current = RatedCurrent * loadFrac;
            Current = Ctx.AddNoise(Current, 0.2);

            // Condenser temperature
            CondenserTemp = 30 + (VacuumLevel / MaxVacuum) * 15;
            CondenserTemp = Ctx.AddNoise(CondenserTemp, 0.5);
        }

        protected override void UpdateIdle(TimeSpan dt, DateTime simNow)
        {
            // Vacuum decays due to leaks
            VacuumLevel -= LeakRate * dt.TotalSeconds;
            VacuumLevel = Math.Max(0, VacuumLevel);
            Current = 0;
            CondenserTemp = 25;
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tVacuum, Math.Round(VacuumLevel, 0));
            Ctx.Tags.Write(_tRun, State == EquipmentState.Running);
            Ctx.Tags.Write(_tFault, State == EquipmentState.Faulted);
        }
    }
}
