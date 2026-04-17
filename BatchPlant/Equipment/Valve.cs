using BatchPlant.Core;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Maps valve outputs to OPC UA tag names. Null entries are not published.
    /// </summary>
    public class ValveTagMap
    {
        /// <summary>For on/off valves: boolean open/closed indicator.</summary>
        public string? Open { get; set; }
        /// <summary>Continuous position 0-100% (for modulating valves, or as position feedback on on/off).</summary>
        public string? Position { get; set; }
        /// <summary>Position feedback with noise and deadband (separate instrument).</summary>
        public string? Feedback { get; set; }
    }

    /// <summary>
    /// Valve model supporting both on/off (XV) and modulating (FCV/TCV) types.
    /// Includes travel time and position feedback with noise.
    /// </summary>
    public class Valve : EquipmentBase
    {
        // Process values
        public double CommandPosition { get; set; }
        public double ActualPosition { get; private set; }
        public double FeedbackPosition { get; private set; }

        // Configuration
        public double TravelTimeSeconds { get; set; } = 5.0;
        public bool IsOnOff { get; set; }
        public double Deadband { get; set; } = 0.5;

        /// <summary>True if valve is substantially open (>50% for modulating, >90% for on/off).</summary>
        public bool IsOpen => IsOnOff ? ActualPosition > 90 : ActualPosition > 50;

        /// <summary>True if valve is substantially closed.</summary>
        public bool IsClosed => ActualPosition < 10;

        private ValveTagMap _tags = new();

        public override int Priority => 30;

        public Valve(PlantContext ctx, string name) : base(ctx, name)
        {
            SetTransitionDelays(TimeSpan.Zero, TimeSpan.Zero);
        }

        /// <summary>
        /// Bind this valve's outputs to OPC UA tag names. Fluent.
        /// </summary>
        public Valve Bind(ValveTagMap tags)
        {
            _tags = tags;
            return this;
        }

        public void Open() { CommandPosition = 100; }
        public void Close() { CommandPosition = 0; }
        public void SetPosition(double percent) { CommandPosition = Math.Clamp(percent, 0, 100); }

        public override void Tick(TimeSpan dt, DateTime simNow)
        {
            if (State == EquipmentState.Faulted)
            {
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

            // Feedback with noise
            FeedbackPosition = ActualPosition;
            if (Math.Abs(error) > Deadband)
            {
                FeedbackPosition = Ctx.AddNoise(ActualPosition, 0.3);
            }
            FeedbackPosition = Math.Clamp(FeedbackPosition, 0, 100);

            if (ActualPosition > 1.0)
            {
                RunHours += dt.TotalHours;
            }

            WriteTags();
        }

        protected override void WriteTags()
        {
            if (_tags.Open != null)
            {
                // For on/off valves expose as boolean
                Ctx.Tags.Write(_tags.Open, IsOpen);
            }
            if (_tags.Position != null)
            {
                Ctx.Tags.Write(_tags.Position, Math.Round(ActualPosition, 1));
            }
            if (_tags.Feedback != null)
            {
                Ctx.Tags.Write(_tags.Feedback, Math.Round(FeedbackPosition, 1));
            }
        }
    }
}
