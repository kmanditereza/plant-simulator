namespace BatchPlant.Batch
{
    public enum AggregationType
    {
        /// <summary>Track the maximum value seen (e.g., peak temperature).</summary>
        Peak,
        /// <summary>Track the minimum value seen.</summary>
        Min,
        /// <summary>Track the running average across all samples.</summary>
        Average,
        /// <summary>Integrate the value over time (kg/h × h = kg, etc.). Assumes tag is in per-hour units.</summary>
        IntegratePerHour,
        /// <summary>Integrate assuming per-minute units.</summary>
        IntegratePerMinute,
        /// <summary>Take the final value at phase exit.</summary>
        Final,
        /// <summary>Take the first value at phase entry.</summary>
        Initial
    }

    /// <summary>
    /// Declarative definition of what report parameters to collect for a given phase.
    /// Maps (AreaName, StateName) → (PhaseLabel, sampling rules, exit snapshots).
    /// </summary>
    public class PhaseObserver
    {
        public string AreaName { get; set; } = "";    // Match against ActiveBatch's current area
        public string StateName { get; set; } = "";   // Match against area's current state value
        public string PhaseLabel { get; set; } = "";  // Becomes PhaseRecord.PhaseLabel
        public string UnitName { get; set; } = "";    // E.g. "MX-201"

        /// <summary>
        /// Tags to sample each tick. Key = report parameter name, Value = (tagName, aggregation).
        /// </summary>
        public Dictionary<string, (string tag, AggregationType agg)> SampledTags { get; set; } = new();

        /// <summary>
        /// Tags to snapshot at phase entry. Key = report parameter name, Value = tag name.
        /// </summary>
        public Dictionary<string, string> SnapshotOnEntry { get; set; } = new();

        /// <summary>
        /// Tags to snapshot at phase exit. Key = report parameter name, Value = tag name.
        /// </summary>
        public Dictionary<string, string> SnapshotOnExit { get; set; } = new();
    }

    /// <summary>
    /// Runtime state for aggregating a phase's report parameters as it executes.
    /// Instantiated when a phase begins, finalized when it ends.
    /// </summary>
    public class ActivePhaseTracker
    {
        private readonly PhaseObserver _observer;
        private readonly Dictionary<string, double> _peaks = new();
        private readonly Dictionary<string, double> _mins = new();
        private readonly Dictionary<string, (double sum, int count)> _averages = new();
        private readonly Dictionary<string, double> _integrals = new();
        private readonly Dictionary<string, double> _finals = new();
        private readonly Dictionary<string, double> _initials = new();

        public PhaseRecord Record { get; }
        public DateTime LastSampleTime;
        public bool FirstSample = true;

        public ActivePhaseTracker(PhaseObserver observer, DateTime startTime)
        {
            _observer = observer;
            LastSampleTime = startTime;
            Record = new PhaseRecord
            {
                PhaseLabel = observer.PhaseLabel,
                AreaName = observer.AreaName,
                UnitName = observer.UnitName,
                StartTime = startTime
            };
        }

        public void RecordEntrySnapshot(Func<string, double> readTag)
        {
            foreach (var (paramName, tag) in _observer.SnapshotOnEntry)
            {
                Record.ReportParameters[paramName] = readTag(tag);
            }
        }

        public void Sample(DateTime now, Func<string, double> readTag)
        {
            double dtSec = (now - LastSampleTime).TotalSeconds;
            LastSampleTime = now;

            foreach (var (paramName, rule) in _observer.SampledTags)
            {
                double value = readTag(rule.tag);
                switch (rule.agg)
                {
                    case AggregationType.Peak:
                        if (!_peaks.ContainsKey(paramName) || value > _peaks[paramName])
                            _peaks[paramName] = value;
                        break;
                    case AggregationType.Min:
                        if (!_mins.ContainsKey(paramName) || value < _mins[paramName])
                            _mins[paramName] = value;
                        break;
                    case AggregationType.Average:
                        if (!_averages.ContainsKey(paramName))
                            _averages[paramName] = (0, 0);
                        var (sum, count) = _averages[paramName];
                        _averages[paramName] = (sum + value, count + 1);
                        break;
                    case AggregationType.IntegratePerHour:
                        if (!_integrals.ContainsKey(paramName)) _integrals[paramName] = 0;
                        _integrals[paramName] += value * (dtSec / 3600.0);
                        break;
                    case AggregationType.IntegratePerMinute:
                        if (!_integrals.ContainsKey(paramName)) _integrals[paramName] = 0;
                        _integrals[paramName] += value * (dtSec / 60.0);
                        break;
                    case AggregationType.Final:
                        _finals[paramName] = value;
                        break;
                    case AggregationType.Initial:
                        if (FirstSample) _initials[paramName] = value;
                        break;
                }
            }
            FirstSample = false;
        }

        public PhaseRecord Finalize(DateTime endTime, Func<string, double> readTag)
        {
            Record.EndTime = endTime;
            Record.DurationSec = (endTime - Record.StartTime).TotalSeconds;

            foreach (var kv in _peaks) Record.ReportParameters[kv.Key] = Math.Round(kv.Value, 2);
            foreach (var kv in _mins) Record.ReportParameters[kv.Key] = Math.Round(kv.Value, 2);
            foreach (var kv in _averages)
            {
                double avg = kv.Value.count > 0 ? kv.Value.sum / kv.Value.count : 0;
                Record.ReportParameters[kv.Key] = Math.Round(avg, 2);
            }
            foreach (var kv in _integrals) Record.ReportParameters[kv.Key] = Math.Round(kv.Value, 2);
            foreach (var kv in _finals) Record.ReportParameters[kv.Key] = Math.Round(kv.Value, 2);
            foreach (var kv in _initials) Record.ReportParameters[kv.Key] = Math.Round(kv.Value, 2);

            foreach (var (paramName, tag) in _observer.SnapshotOnExit)
            {
                Record.ReportParameters[paramName] = Math.Round(readTag(tag), 2);
            }

            return Record;
        }
    }
}
