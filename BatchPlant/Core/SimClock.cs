namespace BatchPlant.Core
{
    /// <summary>
    /// Plant simulation clock with time acceleration support.
    /// All simulation components use this instead of DateTime.Now.
    ///
    /// Shifts are numbered rather than named: the plant starts on shift 1 when the
    /// simulator boots and rolls over to the next shift every ShiftDurationMinutes.
    /// ResetShift() re-anchors the window so the count returns to shift 1 (driven by
    /// the writable SHIFT_RESET tag).
    ///
    /// The shift window is measured in REAL (wall-clock) minutes, not simulation
    /// minutes, so it is unaffected by SpeedMultiplier. Per-shift runtime/downtime
    /// counters therefore also accumulate wall minutes (see SimToWall), which keeps
    /// runtime + downtime == ShiftElapsedMinutes <= ShiftDurationMinutes.
    /// </summary>
    public class SimClock
    {
        private readonly DateTime _wallStart;
        private readonly DateTime _simStart;
        private readonly object _lock = new();

        /// <summary>Real (wall-clock) minutes per shift. The shift number increments each time this elapses.</summary>
        public double ShiftDurationMinutes { get; }

        /// <summary>Total wall time the engine has advanced (excludes any time the engine is stopped).</summary>
        private TimeSpan _wallElapsed;

        /// <summary>Wall-elapsed mark the current shift sequence is measured from (moved by ResetShift).</summary>
        private TimeSpan _shiftAnchorWall;

        public double SpeedMultiplier { get; set; }
        public DateTime Now { get; private set; }
        public TimeSpan SimElapsed => Now - _simStart;
        public TimeSpan WallElapsed { get { lock (_lock) { return _wallElapsed; } } }
        public DayOfWeek DayOfWeek => Now.DayOfWeek;
        public int Hour => Now.Hour;
        public int Minute => Now.Minute;

        /// <summary>Current shift number: 1 on boot (and after a reset), +1 every shift window.</summary>
        public int CurrentShift
        {
            get
            {
                lock (_lock)
                {
                    double mins = (_wallElapsed - _shiftAnchorWall).TotalMinutes;
                    if (mins < 0) mins = 0;
                    return 1 + (int)(mins / ShiftDurationMinutes);
                }
            }
        }

        /// <summary>Real minutes elapsed within the current shift window.</summary>
        public double ShiftElapsedMinutes
        {
            get
            {
                lock (_lock)
                {
                    double mins = (_wallElapsed - _shiftAnchorWall).TotalMinutes;
                    if (mins < 0) mins = 0;
                    return mins % ShiftDurationMinutes;
                }
            }
        }

        public string ShiftLabel => $"Shift {CurrentShift}";

        /// <summary>Re-anchor the shift window at the current instant: the count returns to shift 1.</summary>
        public void ResetShift()
        {
            lock (_lock)
            {
                _shiftAnchorWall = _wallElapsed;
            }
        }

        /// <summary>
        /// Creates a new SimClock.
        /// </summary>
        /// <param name="simStart">What simulation time to start at. Defaults to next Monday 06:00.</param>
        /// <param name="speedMultiplier">Time acceleration factor. 1.0 = real-time, 60.0 = 1 day in 24 min.</param>
        /// <param name="shiftDurationMinutes">Real (wall-clock) minutes per shift. Defaults to 90.</param>
        public SimClock(DateTime? simStart = null, double speedMultiplier = 1.0, double shiftDurationMinutes = 90.0)
        {
            SpeedMultiplier = speedMultiplier;
            ShiftDurationMinutes = shiftDurationMinutes > 0 ? shiftDurationMinutes : 90.0;
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
            _wallElapsed = TimeSpan.Zero;
            _shiftAnchorWall = TimeSpan.Zero;   // boot on shift 1
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
                _wallElapsed += wallDelta;   // drives the wall-clock shift window
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
