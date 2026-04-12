namespace BatchPlant.Core
{
    public enum ShiftType
    {
        Day,    // 06:00 - 14:00
        Swing,  // 14:00 - 22:00
        Night   // 22:00 - 06:00
    }

    /// <summary>
    /// Plant simulation clock with time acceleration support.
    /// All simulation components use this instead of DateTime.Now.
    /// </summary>
    public class SimClock
    {
        private readonly DateTime _wallStart;
        private readonly DateTime _simStart;
        private readonly object _lock = new();

        public double SpeedMultiplier { get; set; }
        public DateTime Now { get; private set; }
        public TimeSpan SimElapsed => Now - _simStart;
        public DayOfWeek DayOfWeek => Now.DayOfWeek;
        public int Hour => Now.Hour;
        public int Minute => Now.Minute;

        public ShiftType CurrentShift
        {
            get
            {
                int h = Hour;
                if (h >= 6 && h < 14) return ShiftType.Day;
                if (h >= 14 && h < 22) return ShiftType.Swing;
                return ShiftType.Night;
            }
        }

        public string ShiftLabel => CurrentShift switch
        {
            ShiftType.Day => "Day (06:00-14:00)",
            ShiftType.Swing => "Swing (14:00-22:00)",
            ShiftType.Night => "Night (22:00-06:00)",
            _ => "Unknown"
        };

        /// <summary>
        /// Creates a new SimClock.
        /// </summary>
        /// <param name="simStart">What simulation time to start at. Defaults to next Monday 06:00.</param>
        /// <param name="speedMultiplier">Time acceleration factor. 1.0 = real-time, 60.0 = 1 day in 24 min.</param>
        public SimClock(DateTime? simStart = null, double speedMultiplier = 1.0)
        {
            SpeedMultiplier = speedMultiplier;
            _wallStart = DateTime.UtcNow;

            if (simStart.HasValue)
            {
                _simStart = simStart.Value;
            }
            else
            {
                // Default: start at next Monday 06:00:00
                var now = DateTime.UtcNow;
                int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilMonday == 0) daysUntilMonday = 7;
                _simStart = now.Date.AddDays(daysUntilMonday).AddHours(6);
            }

            Now = _simStart;
        }

        /// <summary>
        /// Advance the clock by one tick's worth of wall time.
        /// Returns the simulation delta for this tick.
        /// </summary>
        public TimeSpan Advance(TimeSpan wallDelta)
        {
            var simDelta = TimeSpan.FromTicks((long)(wallDelta.Ticks * SpeedMultiplier));
            lock (_lock)
            {
                Now += simDelta;
            }
            return simDelta;
        }

        /// <summary>
        /// Convert a simulation duration to wall-clock duration.
        /// </summary>
        public TimeSpan SimToWall(TimeSpan simDuration)
        {
            if (SpeedMultiplier <= 0) return TimeSpan.MaxValue;
            return TimeSpan.FromTicks((long)(simDuration.Ticks / SpeedMultiplier));
        }
    }
}
