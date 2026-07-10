namespace BatchPlant.Core
{
    /// <summary>
    /// Owns the shift window. Publishes the shift tags and services the SHIFT_RESET
    /// command tag.
    ///
    /// Runs early (Priority 5, before the line controllers at 40) so that when a reset
    /// lands, every controller observes the new shift number in the same tick and rolls
    /// its per-shift counters over exactly once.
    ///
    /// SHIFT_RESET is a normally-true command tag: an OPC UA client writes false to
    /// request a reset, and we latch it straight back to true. We deliberately only
    /// Write() it on an actual reset — TagRegistry.Read() prefers a pending write over
    /// the node value, so re-writing true every tick would shadow (and silently swallow)
    /// the client's false.
    /// </summary>
    public class ShiftManager : ISimulatable
    {
        private readonly PlantContext _ctx;

        public int Priority => 5;

        public ShiftManager(PlantContext ctx) { _ctx = ctx; }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            var tags = _ctx.Tags;
            var clock = _ctx.Clock;

            // Default true: a failed/absent read must never trigger a spurious reset.
            if (!tags.Read<bool>("SHIFT_RESET", true))
            {
                clock.ResetShift();
                tags.Write("SHIFT_RESET", true);
                Console.WriteLine($"[{simNow:HH:mm:ss}] [SHIFT] Reset requested via SHIFT_RESET -> shift 1");
            }

            int shift = clock.CurrentShift;
            string shiftId = shift.ToString();
            double elapsed = Math.Round(clock.ShiftElapsedMinutes, 2);
            double duration = clock.ShiftDurationMinutes;

            tags.Write("SHIFT_NUMBER", shift);
            tags.Write("SHIFT_ID", shiftId);
            tags.Write("SHIFT_ELAPSED_MIN", elapsed);
            tags.Write("SHIFT_DURATION_MIN", duration);

            // Mirror the shift context onto each line folder so line-scoped consumers can
            // read it alongside that line's per-shift counters without reaching into PlantStatus.
            tags.Write("BATCH_LINE_SHIFT_NUMBER", shift);
            tags.Write("BATCH_LINE_SHIFT_ID", shiftId);
            tags.Write("BATCH_LINE_SHIFT_ELAPSED_MIN", elapsed);
            tags.Write("BATCH_LINE_SHIFT_DURATION_MIN", duration);

            tags.Write("PACK_LINE_SHIFT_NUMBER", shift);
            tags.Write("PACK_LINE_SHIFT_ID", shiftId);
            tags.Write("PACK_LINE_SHIFT_ELAPSED_MIN", elapsed);
            tags.Write("PACK_LINE_SHIFT_DURATION_MIN", duration);
        }
    }
}
