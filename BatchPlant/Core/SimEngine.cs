using System.Diagnostics;

namespace BatchPlant.Core
{
    /// <summary>
    /// Main simulation engine. Runs a fixed-rate tick loop (100ms wall-clock)
    /// and calls Tick() on all registered components in priority order.
    /// </summary>
    public class SimEngine
    {
        private readonly List<ISimulatable> _components = new();
        private readonly SimClock _clock;
        private readonly object _lock = new();
        private Timer? _tickTimer;
        private bool _running;
        private long _tickCount;
        private readonly Stopwatch _perfWatch = new();

        /// <summary>
        /// Wall-clock interval between ticks in milliseconds.
        /// </summary>
        public const int TickIntervalMs = 100;

        public long TickCount => Interlocked.Read(ref _tickCount);
        public bool IsRunning => _running;
        public SimClock Clock => _clock;

        public SimEngine(SimClock clock)
        {
            _clock = clock;
        }

        /// <summary>
        /// Register a component for ticking. Components are sorted by Priority.
        /// </summary>
        public void Register(ISimulatable component)
        {
            lock (_lock)
            {
                _components.Add(component);
                _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        /// <summary>
        /// Register multiple components.
        /// </summary>
        public void RegisterAll(IEnumerable<ISimulatable> components)
        {
            lock (_lock)
            {
                _components.AddRange(components);
                _components.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        /// <summary>
        /// Start the tick loop.
        /// </summary>
        public void Start()
        {
            if (_running) return;
            _running = true;

            Console.WriteLine($"[ENGINE] Starting simulation engine ({_components.Count} components, speed {_clock.SpeedMultiplier}x)");
            Console.WriteLine($"[ENGINE] Sim start time: {_clock.Now:yyyy-MM-dd HH:mm:ss} ({_clock.ShiftLabel})");

            _tickTimer = new Timer(OnTick, null, 0, TickIntervalMs);
        }

        /// <summary>
        /// Stop the tick loop.
        /// </summary>
        public void Stop()
        {
            _running = false;
            _tickTimer?.Dispose();
            _tickTimer = null;
            Console.WriteLine($"[ENGINE] Stopped after {_tickCount} ticks");
        }

        private void OnTick(object? state)
        {
            if (!_running) return;

            try
            {
                var wallDelta = TimeSpan.FromMilliseconds(TickIntervalMs);
                var simDelta = _clock.Advance(wallDelta);

                List<ISimulatable> snapshot;
                lock (_lock)
                {
                    snapshot = new List<ISimulatable>(_components);
                }

                foreach (var component in snapshot)
                {
                    try
                    {
                        component.Tick(simDelta, _clock.Now);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ENGINE] Error in {component.GetType().Name}: {ex.Message}");
                    }
                }

                Interlocked.Increment(ref _tickCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ENGINE] Fatal tick error: {ex.Message}");
            }
        }
    }
}
