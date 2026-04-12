using BatchPlant.Core;

namespace BatchPlant.OpcUa
{
    /// <summary>
    /// Simulation component that flushes all pending tag writes to OPC UA nodes.
    /// Runs at the end of each tick (priority 90) to batch all writes into one lock acquisition.
    /// </summary>
    public class TagFlusher : ISimulatable
    {
        private readonly TagRegistry _registry;

        public int Priority => 90;

        public TagFlusher(TagRegistry registry)
        {
            _registry = registry;
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            _registry.Flush();
        }
    }
}
