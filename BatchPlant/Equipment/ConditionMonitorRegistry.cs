using System.Collections.Concurrent;

namespace BatchPlant.Equipment
{
    /// <summary>
    /// Plant-wide index of every ConditionMonitor that has been given an identity.
    ///
    /// Monitors are created deep inside the area controllers and equipment classes as private
    /// fields, which is right for the simulation but leaves the API with nothing to address.
    /// A monitor calls Identify(...) once at construction and becomes reachable by asset id,
    /// so a request to shake the palletizer does not need the API to know that the palletizer
    /// lives in Area600_PackagingLineController.
    ///
    /// Registration is opt-in on purpose. A monitor with no id is still simulated and still
    /// publishes its tags; it simply is not offered to the operator as something to fault.
    /// That keeps assets with no meaningful rotating part out of the UI rather than listing
    /// every monitor in the plant.
    ///
    /// Concurrent because the API server answers on its own threads while the simulation
    /// tick is writing. Injection is a reference swap on a single field, so a request can
    /// never catch a monitor mid-update with a half-applied fault.
    /// </summary>
    public sealed class ConditionMonitorRegistry
    {
        private readonly ConcurrentDictionary<string, ConditionMonitor> _byId = new(StringComparer.OrdinalIgnoreCase);

        public void Add(ConditionMonitor cm)
        {
            if (!string.IsNullOrWhiteSpace(cm.AssetId)) _byId[cm.AssetId] = cm;
        }

        public bool TryGet(string assetId, out ConditionMonitor cm) => _byId.TryGetValue(assetId ?? "", out cm!);

        /// <summary>Every identified monitor, ordered by area then asset id so the UI list is stable.</summary>
        public IReadOnlyList<ConditionMonitor> All =>
            _byId.Values.OrderBy(c => c.Area).ThenBy(c => c.AssetId).ToList();

        public int Count => _byId.Count;

        /// <summary>Clear every injected fault. Used by the UI's "reset all" and on shutdown.</summary>
        public int ClearAll()
        {
            int n = 0;
            foreach (var cm in _byId.Values)
                if (cm.ClearAnomaly()) n++;
            return n;
        }
    }
}
