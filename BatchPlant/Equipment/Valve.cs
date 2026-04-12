using BatchPlant.Core;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Valve model supporting both on/off (XV) and modulating (FCV/TCV) types.
    /// Includes travel time, position feedback with noise, and seat wear.
    /// </summary>
    public class Valve : EquipmentBase
    {
        // Process values
        public double CommandPosition { get; set; }    // 0-100% (or 0/100 for on/off)
        public double ActualPosition { get; private set; } // 0-100% with travel time
        public double FeedbackPosition { get; private set; } // Actual + noise + deadband

        // Configuration
        public double TravelTimeSeconds { get; set; } = 5.0; // Full stroke time
        public bool IsOnOff { get; set; }                      // XV type (binary)
        public double Deadband { get; set; } = 0.5;           // Position feedback deadband %
        public double SeatLeakRate { get; set; }               // L/min leak when "closed"

        // Tags
        private string _tPos = "";
        private string _tCmd = "";
        private string _tFb = "";

        public override int Priority => 30;

        public Valve(PlantContext ctx, string tagPrefix) : base(ctx, tagPrefix)
        {
            SetTransitionDelays(TimeSpan.Zero, TimeSpan.Zero); // Valves don't use start/stop
        }

        public override void RegisterTags(string folderPath)
        {
            _tPos = $"{TagPrefix}_POS";
            _tCmd = $"{TagPrefix}_CMD";
            _tFb = $"{TagPrefix}_FB";

            Ctx.Tags.Define(_tPos, BuiltInType.Double, folderPath, "%", $"{TagPrefix} Position");
            Ctx.Tags.Define(_tCmd, BuiltInType.Double, folderPath, "%", $"{TagPrefix} Command", writable: true);
            Ctx.Tags.Define(_tFb, BuiltInType.Double, folderPath, "%", $"{TagPrefix} Feedback Position");
        }

        /// <summary>
        /// Open the valve (on/off type).
        /// </summary>
        public void Open() { CommandPosition = 100; }

        /// <summary>
        /// Close the valve (on/off type).
        /// </summary>
        public void Close() { CommandPosition = 0; }

        public override void Tick(TimeSpan dt, DateTime simNow)
        {
            if (State == EquipmentState.Faulted)
            {
                // Stuck at current position when faulted
                FeedbackPosition = Ctx.AddNoise(ActualPosition, 0.2);
                WriteTags();
                return;
            }

            double dtSec = dt.TotalSeconds;

            // Travel toward command position
            double maxTravel = (100.0 / TravelTimeSeconds) * dtSec;
            double error = CommandPosition - ActualPosition;
            ActualPosition += Math.Clamp(error, -maxTravel, maxTravel);
            ActualPosition = Math.Clamp(ActualPosition, 0, 100);

            // Seat wear increases minimum leak
            double wearLeak = RunHours / 20000.0; // Very gradual
            SeatLeakRate = wearLeak;

            // Feedback with noise and deadband
            FeedbackPosition = ActualPosition;
            if (Math.Abs(error) > Deadband)
            {
                FeedbackPosition = Ctx.AddNoise(ActualPosition, 0.3);
            }
            FeedbackPosition = Math.Clamp(FeedbackPosition, 0, 100);

            // Track run hours when not fully closed
            if (ActualPosition > 1.0)
            {
                RunHours += dt.TotalHours;
            }

            WriteTags();
        }

        protected override void WriteTags()
        {
            Ctx.Tags.Write(_tPos, Math.Round(ActualPosition, 1));
            Ctx.Tags.Write(_tFb, Math.Round(FeedbackPosition, 1));
        }

        /// <summary>
        /// Get the effective flow fraction (0.0-1.0) accounting for valve position.
        /// </summary>
        public double GetFlowFraction()
        {
            double frac = ActualPosition / 100.0;
            // Add small leak when "closed" due to seat wear
            if (frac < 0.01 && SeatLeakRate > 0)
            {
                frac = SeatLeakRate * 0.001;
            }
            return Math.Clamp(frac, 0, 1);
        }
    }
}
