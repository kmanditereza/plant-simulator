namespace BatchPlant.Core
{
    /// <summary>
    /// Interface for all components that participate in the simulation tick loop.
    /// </summary>
    public interface ISimulatable
    {
        /// <summary>
        /// Priority determines tick execution order. Lower values tick first.
        /// 0=Clock, 10=Utilities, 20=Faults, 30=Equipment, 40=Process, 50=Batch, 60=Filling, 70=Packaging, 80=Schedule, 90=TagFlush
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Called every tick by the SimEngine.
        /// </summary>
        /// <param name="dt">Simulation time elapsed since last tick.</param>
        /// <param name="simNow">Current simulation time.</param>
        void Tick(TimeSpan dt, DateTime simNow);
    }
}
