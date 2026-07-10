using BatchPlant.Core;
using BatchPlant.OpcUa;
using Opc.Ua;

namespace BatchPlant.Equipment
{
    public enum EquipmentState
    {
        Off = 0,
        Starting = 1,
        Running = 2,
        Stopping = 3,
        Faulted = 4,
        Maintenance = 5
    }

    /// <summary>
    /// Base class for all plant equipment. Provides state machine, run hour tracking,
    /// fault model, and OPC UA tag registration.
    /// </summary>
    public abstract class EquipmentBase : ISimulatable
    {
        protected readonly PlantContext Ctx;
        protected readonly string TagPrefix;

        public EquipmentState State { get; protected set; } = EquipmentState.Off;
        public double RunHours { get; protected set; }
        public double RunHoursSinceService { get; protected set; }
        public bool IsFaulted => State == EquipmentState.Faulted;
        public string FaultCode { get; protected set; } = "";

        /// <summary>Cumulative count of fault trips since simulator start (condition monitoring).</summary>
        public int FailureCount { get; protected set; }

        private TimeSpan _startupDelay = TimeSpan.FromSeconds(3);
        private TimeSpan _shutdownDelay = TimeSpan.FromSeconds(2);
        private DateTime? _transitionStart;
        private DateTime? _faultRepairTime;
        private bool _faultFromCondition;

        // ---- Failure model. All disabled by default: equipment that doesn't opt in never fails. ----

        /// <summary>Mean RUN hours between random breakdowns (plant time). 0 = never fails randomly.</summary>
        public double MtbfRunHours { get; set; }

        /// <summary>Repair time (plant minutes) drawn uniformly from this range when a fault trips.</summary>
        public double MttrMinutesMin { get; set; } = 5;
        public double MttrMinutesMax { get; set; } = 20;

        /// <summary>Vibration (mm/s RMS) at which condition monitoring trips a fault. 0 = disabled.</summary>
        public double VibrationTripLimit { get; set; }

        /// <summary>Bearing temperature (degC) at which condition monitoring trips a fault. 0 = disabled.</summary>
        public double BearingTempTripLimit { get; set; }

        /// <summary>True while the equipment is stopped by a fault raised from a condition limit.</summary>
        public bool FaultedFromCondition => IsFaulted && _faultFromCondition;

        public virtual int Priority => 30; // Equipment layer

        /// <summary>
        /// Condition-monitoring signal generator (vibration, bearing temp, MCSA, hydraulic/lube).
        /// Updated automatically each tick; subclasses publish the signals they bind.
        /// </summary>
        public ConditionMonitor Condition { get; }

        /// <summary>
        /// Mechanical load fraction (0..1.5+) reported to the condition monitor.
        /// Override in subclasses that know their true load; defaults to a mid load when running.
        /// </summary>
        protected virtual double ConditionLoad => State == EquipmentState.Running ? 0.6 : 0.0;

        protected EquipmentBase(PlantContext ctx, string tagPrefix)
        {
            Ctx = ctx;
            TagPrefix = tagPrefix;
            Condition = new ConditionMonitor(ctx);
        }

        /// <summary>
        /// Register this equipment's tags with the tag registry.
        /// Override in subclasses to add specific tags.
        /// </summary>
        public virtual void RegisterTags(string folderPath)
        {
            // Subclasses define their specific tags
        }

        /// <summary>
        /// Request equipment start. Transitions through Starting state.
        /// </summary>
        public virtual void Start()
        {
            if (State == EquipmentState.Off || State == EquipmentState.Stopping)
            {
                State = EquipmentState.Starting;
                _transitionStart = Ctx.Clock.Now;
            }
        }

        /// <summary>
        /// Request equipment stop. Transitions through Stopping state.
        /// </summary>
        public virtual void Stop()
        {
            if (State == EquipmentState.Running || State == EquipmentState.Starting)
            {
                State = EquipmentState.Stopping;
                _transitionStart = Ctx.Clock.Now;
            }
        }

        /// <summary>
        /// Trip equipment into fault state.
        /// </summary>
        public virtual void Fault(string code, TimeSpan repairTime)
        {
            // Count only new trips (not re-asserting an existing fault).
            if (State != EquipmentState.Faulted)
            {
                FailureCount++;
            }
            State = EquipmentState.Faulted;
            FaultCode = code;
            _faultRepairTime = Ctx.Clock.Now + repairTime;
        }

        /// <summary>
        /// Clear fault (after repair time elapsed or manual reset).
        /// </summary>
        public virtual void ClearFault()
        {
            if (State == EquipmentState.Faulted)
            {
                State = EquipmentState.Off;
                FaultCode = "";
                _faultRepairTime = null;

                // A condition-based trip is repaired by maintenance (bearing/seal replaced), so the
                // wear that caused it is reset -- degradation signals and speed loss start over.
                // A random breakdown (jam) is just cleared; accumulated wear survives it.
                if (_faultFromCondition)
                {
                    ResetServiceHours();
                    Condition.Service();
                    _faultFromCondition = false;
                }
            }
        }

        /// <summary>Repair duration drawn from [MttrMinutesMin, MttrMinutesMax], in plant minutes.</summary>
        protected TimeSpan RandomMttr() =>
            TimeSpan.FromMinutes(MttrMinutesMin + Ctx.Rng.NextDouble() * Math.Max(0, MttrMinutesMax - MttrMinutesMin));

        public virtual void Tick(TimeSpan dt, DateTime simNow)
        {
            // State machine transitions
            switch (State)
            {
                case EquipmentState.Starting:
                    if (_transitionStart.HasValue && simNow - _transitionStart.Value >= _startupDelay)
                    {
                        State = EquipmentState.Running;
                        _transitionStart = null;
                    }
                    break;

                case EquipmentState.Stopping:
                    if (_transitionStart.HasValue && simNow - _transitionStart.Value >= _shutdownDelay)
                    {
                        State = EquipmentState.Off;
                        _transitionStart = null;
                    }
                    break;

                case EquipmentState.Running:
                    double hours = dt.TotalHours;
                    RunHours += hours;
                    RunHoursSinceService += hours;

                    // Random breakdown: constant-hazard (Poisson) process over RUN hours, so
                    // equipment only ages toward failure while it is actually running.
                    if (MtbfRunHours > 0 && Ctx.Rng.NextDouble() < hours / MtbfRunHours)
                    {
                        _faultFromCondition = false;
                        Fault("BREAKDOWN", RandomMttr());
                    }
                    break;

                case EquipmentState.Faulted:
                    // Auto-repair after MTTR
                    if (_faultRepairTime.HasValue && simNow >= _faultRepairTime.Value)
                    {
                        ClearFault();
                    }
                    break;
            }

            // Subclass physics
            if (State == EquipmentState.Running)
            {
                UpdateRunning(dt, simNow);
            }
            else if (State == EquipmentState.Off)
            {
                UpdateIdle(dt, simNow);
            }

            // Advance condition-monitoring signals before publishing tags.
            Condition.Update(State == EquipmentState.Running, ConditionLoad, IsFaulted, dt.TotalSeconds);

            // Condition-based trip: degradation escalates into an actual stoppage, which is what
            // ties the predictive-maintenance signals to a consequence. Evaluated after the update
            // so we trip on this tick's values.
            if (State == EquipmentState.Running)
            {
                if (VibrationTripLimit > 0 && Condition.Vibration >= VibrationTripLimit)
                {
                    TripFromCondition("HIGH-VIBRATION");
                }
                else if (BearingTempTripLimit > 0 && Condition.BearingTemp >= BearingTempTripLimit)
                {
                    TripFromCondition("HIGH-BEARING-TEMP");
                }
            }

            // Write common tags
            WriteTags();
        }

        /// <summary>Trip a fault caused by a condition-monitoring limit; repair will service the asset.</summary>
        private void TripFromCondition(string code)
        {
            _faultFromCondition = true;
            Fault(code, RandomMttr());
            Console.WriteLine($"[{Ctx.Clock.Now:HH:mm:ss}] [{TagPrefix}] Condition trip: {code} " +
                              $"(vib={Condition.Vibration:F2} mm/s, brg={Condition.BearingTemp:F1} C)");
        }

        /// <summary>Update physics while running. Override in subclasses.</summary>
        protected virtual void UpdateRunning(TimeSpan dt, DateTime simNow) { }

        /// <summary>Update physics while idle. Override in subclasses.</summary>
        protected virtual void UpdateIdle(TimeSpan dt, DateTime simNow) { }

        /// <summary>Write tag values. Override in subclasses to add specific tags.</summary>
        protected virtual void WriteTags() { }

        /// <summary>
        /// Configure startup/shutdown delays.
        /// </summary>
        protected void SetTransitionDelays(TimeSpan startup, TimeSpan shutdown)
        {
            _startupDelay = startup;
            _shutdownDelay = shutdown;
        }

        /// <summary>
        /// Reset service hours (after maintenance).
        /// </summary>
        public void ResetServiceHours()
        {
            RunHoursSinceService = 0;
        }
    }
}
