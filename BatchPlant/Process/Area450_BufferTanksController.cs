using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 450 - Buffer/Holding Tanks.
    ///
    /// Three insulated agitated tanks (BT-451/452/453, 5,000 L each) that decouple batch
    /// production from continuous filling. This is the architectural "batch → discrete"
    /// bridge in the plant.
    ///
    /// Phase 3: Tanks no longer run independent test cycles. Instead they respond to:
    ///   - AcceptProduct(volume, temp)  called by Area 400 each tick during homogenizing
    ///   - DrawProduct(volume)           called by Area 500 filling line continuously
    ///
    /// Each tank has states:
    ///   Empty    - available to start filling
    ///   Filling  - currently being filled by Area 400
    ///   Ready    - full, hot, waiting for Area 500 to draw
    ///   Draining - Area 500 is pulling product
    ///
    /// Temperature is maintained through insulation (very low heat loss coefficient).
    /// </summary>
    public class Area450_BufferTanksController : ISimulatable
    {
        private readonly PlantContext _ctx;
        private readonly BufferTank _bt451;
        private readonly BufferTank _bt452;
        private readonly BufferTank _bt453;

        public int Priority => 40;

        public const double MinHoldTempC = 82.0;
        private const double TankCapacityL = 5000;
        private const double TankFullThresholdL = 4500;  // tank becomes Ready at 90% full
        private const double TankEmptyThresholdL = 5;

        public Area450_BufferTanksController(PlantContext ctx)
        {
            _ctx = ctx;
            _bt451 = new BufferTank(ctx, "BT-451", "BT451_LVL_L", "BT451_LVL", "BT451_TEMP",
                                     "BT451_AGIT_RUN", "BT451_VLV_IN", "BT451_VLV_OUT");
            _bt452 = new BufferTank(ctx, "BT-452", "BT452_LVL_L", "BT452_LVL", "BT452_TEMP",
                                     "BT452_AGIT_RUN", "BT452_VLV_IN", "BT452_VLV_OUT");
            _bt453 = new BufferTank(ctx, "BT-453", "BT453_LVL_L", "BT453_LVL", "BT453_TEMP",
                                     "BT453_AGIT_RUN", "BT453_VLV_IN", "BT453_VLV_OUT");
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            _bt451.Tick(dt, simNow);
            _bt452.Tick(dt, simNow);
            _bt453.Tick(dt, simNow);
        }

        // -------- Public API --------

        /// <summary>
        /// Called by Area 400 each tick during Homogenizing to push product into buffer tanks.
        /// Picks a tank: prefers one already in Filling state, otherwise an Empty one.
        /// Returns the actual volume accepted (0 if no tank is available).
        /// </summary>
        public double AcceptProduct(double volumeL, double tempC)
        {
            // Prefer tank already being filled (don't spread across multiple)
            var already = FindFillingTank();
            if (already != null)
            {
                double accepted = already.ExternalFill(volumeL, tempC);
                if (accepted > 0) return accepted;
            }

            // Otherwise pick an empty tank and start filling it
            var empty = FindEmptyTank();
            if (empty != null)
            {
                return empty.ExternalFill(volumeL, tempC);
            }

            return 0; // all tanks full or draining
        }

        /// <summary>
        /// Total product available for drawing (hot product in Ready or Draining tanks).
        /// </summary>
        public double TotalHotProductL(double minTempC = MinHoldTempC)
        {
            double total = 0;
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
            {
                if ((tank.TankState == BufferTankState.Ready || tank.TankState == BufferTankState.Draining)
                    && tank.VesselTemp >= minTempC)
                {
                    total += tank.VesselLevel;
                }
            }
            return total;
        }

        /// <summary>
        /// Draw product for Area 500 filling line. Picks from tanks in Ready or Draining state.
        /// Returns actual volume drawn.
        /// </summary>
        public double DrawProduct(double volumeL, double minTempC = MinHoldTempC)
        {
            double remaining = volumeL;
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
            {
                if (remaining <= 0) break;
                if (tank.VesselTemp >= minTempC &&
                    (tank.TankState == BufferTankState.Ready || tank.TankState == BufferTankState.Draining))
                {
                    double taken = tank.ExternalDraw(remaining);
                    remaining -= taken;
                }
            }
            return volumeL - remaining;
        }

        public double AverageProductTempC()
        {
            double sumLevel = 0;
            double weighted = 0;
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
            {
                if (tank.VesselLevel > 0)
                {
                    weighted += tank.VesselLevel * tank.VesselTemp;
                    sumLevel += tank.VesselLevel;
                }
            }
            return sumLevel > 0 ? weighted / sumLevel : 0;
        }

        private BufferTank? FindFillingTank()
        {
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
                if (tank.TankState == BufferTankState.Filling) return tank;
            return null;
        }

        private BufferTank? FindEmptyTank()
        {
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
                if (tank.TankState == BufferTankState.Empty) return tank;
            return null;
        }

        // -------- Nested buffer tank state machine --------

        public enum BufferTankState
        {
            Empty = 0,
            Filling = 1,
            Ready = 2,
            Draining = 3
        }

        private class BufferTank
        {
            private readonly PlantContext _ctx;
            private readonly string _name;
            private readonly string _tValveIn;
            private readonly string _tValveOut;
            private readonly string _tAgitRun;
            private readonly Vessel _vessel;

            public BufferTankState TankState { get; private set; } = BufferTankState.Empty;
            public double VesselLevel => _vessel.Level;
            public double VesselTemp => _vessel.Temperature;

            private DateTime _lastFillTime;
            private DateTime _lastDrawTime;
            private const double FillIdleTimeoutSec = 10; // become Ready if no fills for 10 sec
            private const double DrawIdleTimeoutSec = 10; // back to Ready if no draws for 10 sec

            public BufferTank(PlantContext ctx, string name,
                              string tLevelL, string tLevelPct, string tTemp,
                              string tAgitRun, string tValveIn, string tValveOut)
            {
                _ctx = ctx;
                _name = name;
                _tValveIn = tValveIn;
                _tValveOut = tValveOut;
                _tAgitRun = tAgitRun;

                _vessel = new Vessel(ctx, name)
                {
                    Capacity = TankCapacityL,
                    Temperature = 22.0,
                    AmbientTemp = 22.0,
                    Density = 1.08,
                    HeatLossCoefficient = 0.000003, // highly insulated
                    IsJacketed = false
                }.Bind(new VesselTagMap
                {
                    Level = tLevelL,
                    LevelPct = tLevelPct,
                    Temperature = tTemp
                });
            }

            /// <summary>
            /// Area 400 pushes product into this tank. Transitions Empty → Filling.
            /// Returns volume actually accepted.
            /// </summary>
            public double ExternalFill(double volumeL, double tempC)
            {
                if (TankState == BufferTankState.Draining) return 0;
                if (_vessel.Level >= TankCapacityL) return 0;

                double room = TankCapacityL - _vessel.Level;
                double accepted = Math.Min(volumeL, room);
                _vessel.AddMaterial(accepted, tempC);
                _lastFillTime = _ctx.Clock.Now;

                if (TankState == BufferTankState.Empty || TankState == BufferTankState.Ready)
                {
                    TankState = BufferTankState.Filling;
                    Console.WriteLine($"[{_ctx.Clock.Now:HH:mm:ss}] {_name}: -> Filling");
                }
                return accepted;
            }

            /// <summary>
            /// Area 500 pulls product from this tank. Transitions Ready → Draining.
            /// </summary>
            public double ExternalDraw(double volumeL)
            {
                if (TankState != BufferTankState.Ready && TankState != BufferTankState.Draining) return 0;
                double drawn = _vessel.RemoveMaterial(volumeL);
                if (drawn > 0)
                {
                    _lastDrawTime = _ctx.Clock.Now;
                    if (TankState == BufferTankState.Ready)
                    {
                        TankState = BufferTankState.Draining;
                        Console.WriteLine($"[{_ctx.Clock.Now:HH:mm:ss}] {_name}: -> Draining");
                    }
                }
                return drawn;
            }

            public void Tick(TimeSpan dt, DateTime simNow)
            {
                // Passive state transitions based on level and idle timers
                switch (TankState)
                {
                    case BufferTankState.Filling:
                        if (_vessel.Level >= TankFullThresholdL ||
                            (simNow - _lastFillTime).TotalSeconds > FillIdleTimeoutSec)
                        {
                            TankState = BufferTankState.Ready;
                            Console.WriteLine($"[{simNow:HH:mm:ss}] {_name}: -> Ready ({_vessel.Level:F0}L)");
                        }
                        break;

                    case BufferTankState.Draining:
                        if (_vessel.Level <= TankEmptyThresholdL)
                        {
                            TankState = BufferTankState.Empty;
                            Console.WriteLine($"[{simNow:HH:mm:ss}] {_name}: -> Empty");
                        }
                        else if ((simNow - _lastDrawTime).TotalSeconds > DrawIdleTimeoutSec)
                        {
                            // No draws for a while - go back to Ready (filling line starved, not blocked)
                            TankState = BufferTankState.Ready;
                        }
                        break;
                }

                _vessel.Tick(dt, simNow);
                PublishStateTags();
            }

            private void PublishStateTags()
            {
                _ctx.Tags.Write(_tValveIn, TankState == BufferTankState.Filling);
                _ctx.Tags.Write(_tValveOut, TankState == BufferTankState.Draining);
                _ctx.Tags.Write(_tAgitRun, _vessel.Level > 100);
            }
        }
    }
}
