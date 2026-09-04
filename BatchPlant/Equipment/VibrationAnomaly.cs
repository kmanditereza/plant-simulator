namespace BatchPlant.Equipment
{
    /// <summary>
    /// Shape of an injected vibration fault. The three modes are the three things that
    /// actually go wrong on rotating machinery, and they look different to a diagnostic:
    ///
    ///   Step  - an abrupt jump to a new level and it stays there. What a sudden mechanical
    ///           change looks like: a coupling lets go, a foundation bolt shears, a fan
    ///           sheds a blade. Useful for testing threshold alarms.
    ///   Ramp  - a slow climb to the target over RampMinutes. What genuine wear looks like:
    ///           a bearing spalling, an impeller eroding. This is the one worth training a
    ///           predictive model on, because the interesting part is the slope, not the level.
    ///   Spike - baseline most of the time with intermittent bursts. What looseness, cavitation
    ///           or an intermittent rub looks like. Deliberately awkward: a naive threshold or
    ///           a single spot reading will miss it, which is the point of simulating it.
    /// </summary>
    public enum VibrationAnomalyMode
    {
        Step,
        Ramp,
        Spike
    }

    /// <summary>
    /// A vibration fault injected into a ConditionMonitor from the operator UI or the API.
    ///
    /// Severity is a MULTIPLIER on the asset's healthy vibration, not an absolute mm/s value.
    /// That keeps one number meaningful across the whole plant: a cooling-tower fan idles near
    /// 2.5 mm/s and a feeder near 1.0, so "3x" degrades both believably where "8 mm/s" would be
    /// catastrophic for one and unremarkable for the other.
    ///
    /// A real bearing fault does not raise vibration alone -- the bearing also heats and the
    /// motor current signature distorts. The monitor therefore drags BearingTemp and McsaIndex
    /// along with the injected vibration, so an anomaly still looks like a fault to anything
    /// correlating the three rather than watching one tag.
    /// </summary>
    public sealed class VibrationAnomaly
    {
        /// <summary>How the fault develops over time.</summary>
        public VibrationAnomalyMode Mode { get; init; } = VibrationAnomalyMode.Step;

        /// <summary>Multiplier on healthy vibration at full effect. 1.0 = no change, 3.0 = triple.</summary>
        public double Severity { get; init; } = 3.0;

        /// <summary>Ramp mode: simulated minutes taken to reach Severity. Ignored by the other modes.</summary>
        public double RampMinutes { get; init; } = 30.0;

        /// <summary>Simulated minutes before the fault clears itself. 0 = persists until cleared.</summary>
        public double DurationMinutes { get; init; }

        /// <summary>Free text carried through to the API so the UI can show why it was injected.</summary>
        public string Reason { get; init; } = "";

        /// <summary>Simulated minutes since injection. Advanced by the monitor.</summary>
        public double ElapsedMinutes { get; internal set; }

        /// <summary>Plant time the fault was injected.</summary>
        public DateTime InjectedAt { get; init; }

        /// <summary>
        /// Current multiplier, given how far the fault has developed.
        /// </summary>
        public double CurrentFactor(Random rng)
        {
            double target = Math.Max(1.0, Severity);
            switch (Mode)
            {
                case VibrationAnomalyMode.Ramp:
                    // Linear climb from healthy to target, then hold.
                    double p = RampMinutes <= 0 ? 1.0 : Math.Clamp(ElapsedMinutes / RampMinutes, 0.0, 1.0);
                    return 1.0 + (target - 1.0) * p;

                case VibrationAnomalyMode.Spike:
                    // Bursty: mostly near baseline, occasionally at or above target. The burst
                    // probability is per-tick, so a faster sim speed does not change how often
                    // a burst appears in PLANT time.
                    return rng.NextDouble() < 0.18
                        ? target * (0.85 + 0.45 * rng.NextDouble())
                        : 1.0 + (target - 1.0) * 0.12;

                case VibrationAnomalyMode.Step:
                default:
                    return target;
            }
        }

        /// <summary>True once a bounded fault has run its course.</summary>
        public bool Expired => DurationMinutes > 0 && ElapsedMinutes >= DurationMinutes;
    }
}
