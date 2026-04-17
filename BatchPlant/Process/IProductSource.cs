namespace BatchPlant.Process
{
    /// <summary>
    /// Interface implemented by an area that can hand off product to a downstream area.
    /// Uses a "pull" model: the downstream area calls DrawForTransfer() each tick during
    /// its fill/receive phase, and the upstream area only returns product when it's in
    /// a state where it's actually ready to discharge.
    /// </summary>
    public interface IProductSource
    {
        /// <summary>True if this source currently has product ready to transfer out.</summary>
        bool HasProductAvailable { get; }

        /// <summary>Temperature of the available product (°C). Caller uses this for mass-weighted mixing.</summary>
        double ProductTempC { get; }

        /// <summary>
        /// Request a volume of product for transfer. Returns the actual volume drawn
        /// (may be less than requested, or 0 if not available).
        /// </summary>
        double DrawForTransfer(double litersRequested);
    }
}
