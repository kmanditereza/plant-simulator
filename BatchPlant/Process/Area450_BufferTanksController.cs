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
    ///   Empty     - available to start filling
    ///   Filling   - currently being filled by Area 400
    ///   Ready     - full, hot, waiting for Area 500 to draw
    ///   Draining  - Area 500 is pulling product
    ///   Disposing - product is out of spec and is being pumped to waste
    ///
    /// Hot hold and disposition
    /// ------------------------
    /// This is a hot-fill line: MinHoldTempC (82 C) is a food-safety limit, not a preference.
    /// Area 500 fills at >= 82 C so the product sterilises the container, and it will refuse
    /// colder product outright.
    ///
    /// The tanks are therefore JACKETED and actively hold product at HoldSetpointC, exactly as a
    /// real hot-fill surge tank does. Without that, product left in a tank coasts down to ambient
    /// during any pause, and once it crosses 82 C it is simultaneously (a) too cold for the pack
    /// line to ever draw and (b) still above TankEmptyThresholdL, so the tank never reports Empty
    /// and the batch line blocks for want of a free tank. That is a permanent deadlock: the only
    /// consumer of tank contents is the pack line, and it has just refused the contents forever.
    ///
    /// Holding alone is a mitigation, not a guarantee, so product also has a disposition path:
    ///   - MaxHoldHours   the hot-hold clock. Holding hot degrades colour and viscosity, so product
    ///                    held beyond this is out of spec and is dumped. This is the PRIMARY trigger:
    ///                    without it the jacket would cheerfully reheat day-old bulk and the filler
    ///                    would pack it.
    ///   - OutOfSpecGrace how long the product may sit below MinHoldTempC before it is written off.
    ///                    The grace period lets the jacket recover a dip (or a fresh, cooler charge)
    ///                    without dumping product that would have come back to temperature; it only
    ///                    fires when holding has genuinely failed.
    /// Either way the tank drains to waste and returns to Empty, so the plant can always recover.
    /// The dumped mass is reported as a holding loss, which is what makes a long stoppage cost
    /// product rather than nothing.
    /// </summary>
    public class Area450_BufferTanksController : ISimulatable
    {
        private readonly PlantContext _ctx;
        private readonly BufferTank _bt451;
        private readonly BufferTank _bt452;
        private readonly BufferTank _bt453;
        private Api.ApiServer? _apiServer;

        public int Priority => 40;

        public const double MinHoldTempC = 82.0;
        private const double TankCapacityL = 5000;
        private const double TankFullThresholdL = 4500;  // tank becomes Ready at 90% full
        private const double TankEmptyThresholdL = 5;

        /// <summary>Jacket setpoint. Sits above MinHoldTempC so held product keeps a margin over the fill limit.</summary>
        private const double HoldSetpointC = 85.0;
        /// <summary>Max hot-hold before the product is out of spec on quality (plant hours).</summary>
        private const double MaxHoldHours = 8.0;
        /// <summary>How long product may sit below MinHoldTempC before it is written off (plant minutes).</summary>
        private const double OutOfSpecGraceMin = 20.0;
        /// <summary>Rate the waste pump empties a condemned tank (L per plant minute).</summary>
        private const double DisposalRateLPerMin = 300.0;

        /// <summary>Product density, matching the Vessel configuration below. Converts L -> kg.</summary>
        public const double ProductDensityKgL = 1.08;

        /// <summary>Combined capacity of BT-451/452/453.</summary>
        public double TotalCapacityL => TankCapacityL * 3;

        /// <summary>
        /// Cumulative product (litres) delivered into the buffer per bulk batch. Monotonic for
        /// the life of a batch, so it represents "good quantity completed" for that batch's
        /// work order -- unlike the tank levels, which fall again as the pack line draws.
        /// </summary>
        private readonly Dictionary<string, double> _deliveredLByBatch = new();

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

        /// <summary>A tank of product condemned and pumped to waste.</summary>
        public sealed class DisposalRecord
        {
            public string TankId = "";
            public string BatchId = "";
            public string Reason = "";      // HoldTimeExpired | TemperatureOutOfSpec
            public double VolumeL;
            public double Kg;
            public double TempC;
            public double HeldHours;
            public DateTime At;
        }

        private double _disposedKgTotal;
        private double _disposedKgShift;
        private int _disposalsShift;
        private int _lastShift;
        private bool _shiftInit;

        public void ConnectApiServer(Api.ApiServer apiServer) => _apiServer = apiServer;

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            _bt451.Tick(dt, simNow);
            _bt452.Tick(dt, simNow);
            _bt453.Tick(dt, simNow);

            // Rebase the holding-loss counters each shift, like the other _SHIFT counters.
            int shift = _ctx.Clock.CurrentShift;
            if (!_shiftInit || shift != _lastShift)
            {
                _shiftInit = true;
                _lastShift = shift;
                _disposedKgShift = 0;
                _disposalsShift = 0;
            }

            // Collect any tank that finished dumping this tick.
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
            {
                var d = tank.TakeDisposal();
                if (d == null) continue;

                _disposedKgTotal += d.Kg;
                _disposedKgShift += d.Kg;
                _disposalsShift++;

                Console.WriteLine($"[{simNow:HH:mm:ss}] [BUFFER] {d.TankId}: DISPOSED {d.VolumeL:F0}L " +
                                  $"({d.Kg:F0}kg) of batch {d.BatchId} -- {d.Reason} " +
                                  $"(temp={d.TempC:F1}C, held={d.HeldHours:F1}h)");

                _apiServer?.EnqueueDisposalEvent(BuildDisposalEvent(d));
            }

            var t = _ctx.Tags;
            t.Write("BUFFER_HOLDING_LOSS_KG_SHIFT", Math.Round(_disposedKgShift, 1));
            t.Write("BUFFER_HOLDING_LOSS_KG_TOTAL", Math.Round(_disposedKgTotal, 1));
            t.Write("BUFFER_DISPOSALS_SHIFT", _disposalsShift);
            t.Write("BT451_HOLD_MIN", Math.Round(_bt451.HoldElapsedMinutes, 1));
            t.Write("BT452_HOLD_MIN", Math.Round(_bt452.HoldElapsedMinutes, 1));
            t.Write("BT453_HOLD_MIN", Math.Round(_bt453.HoldElapsedMinutes, 1));
        }

        /// <summary>
        /// Standard production event. Published to Production/Events/ProductDisposed, so the generic
        /// TimescaleDB and knowledge-graph ingests pick it up with no per-type wiring.
        /// </summary>
        private object BuildDisposalEvent(DisposalRecord d) => new
        {
            eventType = "ProductDisposed",
            eventId = $"EVT-DISPOSED-{d.TankId}-{d.At.Ticks}",
            eventVersion = "2.0",
            timestamp = d.At.ToString("o"),
            source = new
            {
                origin = "MES-LineMonitor",
                uri = "opc.tcp://localhost:26543/BatchPlantServer",
                gateway = "Ignition-Gateway-Munich-Line1",
                originSystemId = 1030
            },
            correlationId = $"disposal-{d.TankId}-{d.At.Ticks}",
            data = new
            {
                equipmentId = d.TankId,
                producedBatch = d.BatchId,
                isa95Context = new
                {
                    enterprise = "GlobalIndustries",
                    site = "Munich",
                    area = "ProductionArea",
                    processCell = "Line1"
                },
                reason = d.Reason,
                category = "HoldingLoss",
                quantityL = Math.Round(d.VolumeL, 1),
                quantityKg = Math.Round(d.Kg, 1),
                temperatureC = Math.Round(d.TempC, 1),
                heldHours = Math.Round(d.HeldHours, 2),
                disposition = "Waste",
                disposedAt = d.At.ToString("o"),
                comment = "Bulk held in the buffer went out of specification and was pumped to waste; the " +
                          "tank was returned to service. Product is filled hot, so bulk that falls below the " +
                          "hot-fill limit, or that has been held hot beyond its permitted time, cannot be packed."
            }
        };

        // -------- Public API --------

        /// <summary>
        /// Called by Area 400 each tick during Homogenizing to push product into buffer tanks.
        /// Picks a tank: prefers one already in Filling state, otherwise an Empty one.
        /// Returns the actual volume accepted (0 if no tank is available).
        /// </summary>
        public double AcceptProduct(double volumeL, double tempC, string batchId = "")
        {
            // One batch per tank: prefer a tank already being filled with THIS batch.
            var already = FindFillingTank();
            if (already != null && (already.BatchId == batchId || string.IsNullOrEmpty(already.BatchId)))
            {
                double accepted = already.ExternalFill(volumeL, tempC, batchId);
                if (accepted > 0) return RecordDelivery(batchId, accepted);
            }

            // Otherwise pick an empty tank and dedicate it to this batch.
            var empty = FindEmptyTank();
            if (empty != null)
            {
                return RecordDelivery(batchId, empty.ExternalFill(volumeL, tempC, batchId));
            }

            return 0; // all tanks full or draining
        }

        /// <summary>
        /// Open a fresh delivery ledger for a batch that is starting.
        ///
        /// Batch IDs are not unique over the life of the process: a work order re-dispatched from its
        /// retained UNS message yields a new batch carrying the same BatchId. Without this reset the
        /// ledger keeps summing across those runs, so DeliveredKgFor reports the total of every run
        /// under that ID rather than the current one, and yield climbs without bound (2x, 3x, ...).
        /// </summary>
        public void BeginBatch(string batchId)
        {
            if (!string.IsNullOrEmpty(batchId)) _deliveredLByBatch.Remove(batchId);
        }

        /// <summary>Accumulate what this batch has delivered into the buffer, and pass the volume through.</summary>
        private double RecordDelivery(string batchId, double acceptedL)
        {
            if (acceptedL > 0 && !string.IsNullOrEmpty(batchId))
            {
                _deliveredLByBatch.TryGetValue(batchId, out var sofar);
                _deliveredLByBatch[batchId] = sofar + acceptedL;
            }
            return acceptedL;
        }

        /// <summary>Good product (kg) this batch has delivered into the buffer so far.</summary>
        public double DeliveredKgFor(string batchId)
        {
            if (string.IsNullOrEmpty(batchId)) return 0;
            return _deliveredLByBatch.TryGetValue(batchId, out var l) ? l * ProductDensityKgL : 0;
        }

        /// <summary>Total product held across all three tanks, regardless of state or temperature.</summary>
        public double TotalProductL()
        {
            double total = 0;
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
                total += tank.VesselLevel;
            return total;
        }

        /// <summary>Total product held across all three tanks, in kg (same units as batch size).</summary>
        public double TotalProductKg() => TotalProductL() * ProductDensityKgL;

        /// <summary>Buffer fullness across all three tanks, as a percentage of combined capacity.</summary>
        public double BufferFullnessPct() => TotalCapacityL > 0 ? TotalProductL() / TotalCapacityL * 100.0 : 0;

        /// <summary>
        /// True while at least one tank is Empty and can accept a new batch. The batch
        /// line gates new-batch starts on this so it never cooks bulk with nowhere to buffer.
        /// </summary>
        public bool HasCapacityForNewBatch() => EmptyTankCount() > 0;

        public int EmptyTankCount()
        {
            int n = 0;
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
                if (tank.TankState == BufferTankState.Empty) n++;
            return n;
        }

        /// <summary>
        /// Draw for the pack line from a SINGLE buffer tank (one bulk batch at a time).
        /// Finishes a Draining tank before switching to a fresh Ready one. Returns the
        /// volume drawn and the produced-batch id it came from (for genealogy).
        /// </summary>
        public (double drawn, string batchId) DrawFromOneBatch(double volumeL, double minTempC = MinHoldTempC)
        {
            // Finish the tank we're already draining first.
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
            {
                if (tank.TankState == BufferTankState.Draining && tank.VesselTemp >= minTempC)
                {
                    double taken = tank.ExternalDraw(volumeL);
                    if (taken > 0) return (taken, tank.BatchId);
                }
            }
            // Otherwise start on the next Ready tank.
            foreach (var tank in new[] { _bt451, _bt452, _bt453 })
            {
                if (tank.TankState == BufferTankState.Ready && tank.VesselTemp >= minTempC)
                {
                    double taken = tank.ExternalDraw(volumeL);
                    if (taken > 0) return (taken, tank.BatchId);
                }
            }
            return (0, "");
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
            Draining = 3,
            Disposing = 4   // out of spec, pumping to waste; returns to Empty
        }

        private class BufferTank
        {
            private readonly PlantContext _ctx;
            private readonly string _name;
            private readonly string _tValveIn;
            private readonly string _tValveOut;
            private readonly string _tAgitRun;
            private readonly string _agitBase;
            private readonly ConditionMonitor _cmAgit;
            private readonly Vessel _vessel;

            public BufferTankState TankState { get; private set; } = BufferTankState.Empty;
            public double VesselLevel => _vessel.Level;
            public double VesselTemp => _vessel.Temperature;
            // The produced-batch number of the bulk currently held (one batch per tank).
            public string BatchId { get; private set; } = "";

            private DateTime _lastFillTime;
            private DateTime _lastDrawTime;
            private const double FillIdleTimeoutSec = 10; // become Ready if no fills for 10 sec
            private const double DrawIdleTimeoutSec = 10; // back to Ready if no draws for 10 sec

            // Hot-hold clock: starts when product first enters an empty tank, clears when it empties.
            private DateTime? _holdStartTime;
            // When the product first fell below the hot-fill limit (null = currently in spec).
            private DateTime? _belowSpecSince;
            // Disposal in progress: what we condemned and how much has gone to waste so far.
            private string _disposalReason = "";
            private double _disposalStartTempC;
            private double _disposalHeldHours;
            private double _disposedL;
            private DisposalRecord? _completedDisposal;

            /// <summary>How long this tank has been holding product (plant minutes); 0 when empty.</summary>
            public double HoldElapsedMinutes =>
                _holdStartTime.HasValue ? (_ctx.Clock.Now - _holdStartTime.Value).TotalMinutes : 0.0;

            /// <summary>Pops the disposal record once a condemned tank has finished draining.</summary>
            public DisposalRecord? TakeDisposal()
            {
                var d = _completedDisposal;
                _completedDisposal = null;
                return d;
            }

            public BufferTank(PlantContext ctx, string name,
                              string tLevelL, string tLevelPct, string tTemp,
                              string tAgitRun, string tValveIn, string tValveOut)
            {
                _ctx = ctx;
                _name = name;
                _tValveIn = tValveIn;
                _tValveOut = tValveOut;
                _tAgitRun = tAgitRun;
                _agitBase = tAgitRun.Replace("_RUN", "");   // e.g. "BT451_AGIT"
                _cmAgit = new ConditionMonitor(ctx) { BaseVibration = 1.2, BearingRise = 25 }
                    .Identify($"EQ-MUC-{_agitBase.Replace("_AGIT", "")}", $"{name} Agitator", "Processing", $"{_agitBase}_VIB");

                _vessel = new Vessel(ctx, name)
                {
                    Capacity = TankCapacityL,
                    Temperature = 22.0,
                    AmbientTemp = 22.0,
                    Density = 1.08,
                    HeatLossCoefficient = 0.000003, // highly insulated
                    // Hot-fill surge tanks are jacketed and hold product at temperature. The jacket
                    // setpoint is driven each tick (see Tick), not fixed here.
                    IsJacketed = true
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
            public double ExternalFill(double volumeL, double tempC, string batchId = "")
            {
                // A condemned tank takes no product until it has been emptied.
                if (TankState == BufferTankState.Draining || TankState == BufferTankState.Disposing) return 0;
                if (_vessel.Level >= TankCapacityL) return 0;

                double room = TankCapacityL - _vessel.Level;
                double accepted = Math.Min(volumeL, room);
                _vessel.AddMaterial(accepted, tempC);
                _lastFillTime = _ctx.Clock.Now;

                // The hot-hold clock starts the moment product first lands in an empty tank.
                _holdStartTime ??= _ctx.Clock.Now;

                if (TankState == BufferTankState.Empty || TankState == BufferTankState.Ready)
                {
                    // Dedicate this tank to the batch that first fills it.
                    if (!string.IsNullOrEmpty(batchId)) BatchId = batchId;
                    TankState = BufferTankState.Filling;
                    Console.WriteLine($"[{_ctx.Clock.Now:HH:mm:ss}] {_name}: -> Filling (batch={BatchId})");
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
                // The jacket holds product at temperature while the tank is in service. It is shut off
                // for a condemned tank -- no point heating what is on its way to the drain.
                _vessel.JacketTemp = (TankState == BufferTankState.Disposing)
                    ? _vessel.AmbientTemp
                    : HoldSetpointC;

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
                            GoEmpty(simNow);
                        }
                        else if ((simNow - _lastDrawTime).TotalSeconds > DrawIdleTimeoutSec)
                        {
                            // No draws for a while - go back to Ready (filling line starved, not blocked)
                            TankState = BufferTankState.Ready;
                        }
                        break;

                    case BufferTankState.Disposing:
                        // Pump the condemned bulk to waste, then hand the tank back to production.
                        double dump = _vessel.RemoveMaterial(DisposalRateLPerMin * dt.TotalMinutes);
                        _disposedL += dump;
                        if (_vessel.Level <= 0.01)
                        {
                            _completedDisposal = new DisposalRecord
                            {
                                TankId = _name,
                                BatchId = BatchId,
                                Reason = _disposalReason,
                                VolumeL = _disposedL,
                                Kg = _disposedL * ProductDensityKgL,
                                TempC = _disposalStartTempC,
                                HeldHours = _disposalHeldHours,
                                At = simNow
                            };
                            GoEmpty(simNow);
                        }
                        break;
                }

                // Out-of-spec check on product actually in service. Draining is included: a tank can
                // time out mid-draw, and product the pack line has stopped taking must not linger.
                if (TankState == BufferTankState.Ready || TankState == BufferTankState.Draining)
                {
                    CheckOutOfSpec(simNow);
                }

                _vessel.Tick(dt, simNow);

                // Agitator condition monitoring (runs whenever the tank holds product)
                bool agitRunning = _vessel.Level > 100;
                _cmAgit.Update(agitRunning, 0.5, false, dt.TotalSeconds);

                PublishStateTags();
            }

            /// <summary>
            /// Condemn product that can no longer be legally or safely packed.
            ///
            /// Two triggers, deliberately different in character:
            ///   HoldTimeExpired      - held hot beyond MaxHoldHours. Quality, not temperature: the
            ///                          product may be perfectly hot and still be too old to pack.
            ///                          This is what stops the jacket quietly reheating stale bulk.
            ///   TemperatureOutOfSpec - sat below the hot-fill limit for longer than the grace
            ///                          period, i.e. holding has actually failed. The grace window
            ///                          exists so a dip (or a cooler fresh charge still coming up to
            ///                          temperature) is not mistaken for a failure.
            /// </summary>
            private void CheckOutOfSpec(DateTime simNow)
            {
                if (_vessel.Level <= TankEmptyThresholdL) return;   // nothing worth condemning

                double heldHours = _holdStartTime.HasValue
                    ? (simNow - _holdStartTime.Value).TotalHours : 0.0;

                if (heldHours > MaxHoldHours)
                {
                    Condemn(simNow, "HoldTimeExpired", heldHours);
                    return;
                }

                if (_vessel.Temperature < MinHoldTempC)
                {
                    _belowSpecSince ??= simNow;
                    if ((simNow - _belowSpecSince.Value).TotalMinutes > OutOfSpecGraceMin)
                    {
                        Condemn(simNow, "TemperatureOutOfSpec", heldHours);
                    }
                }
                else
                {
                    _belowSpecSince = null;   // back in spec; the jacket recovered it
                }
            }

            private void Condemn(DateTime simNow, string reason, double heldHours)
            {
                _disposalReason = reason;
                _disposalStartTempC = _vessel.Temperature;
                _disposalHeldHours = heldHours;
                _disposedL = 0;
                TankState = BufferTankState.Disposing;
                Console.WriteLine($"[{simNow:HH:mm:ss}] {_name}: -> Disposing ({_vessel.Level:F0}L of " +
                                  $"batch {BatchId}, {reason}, temp={_vessel.Temperature:F1}C, held={heldHours:F1}h)");
            }

            /// <summary>Return the tank to service: empty, unbatched, hold clock cleared.</summary>
            private void GoEmpty(DateTime simNow)
            {
                TankState = BufferTankState.Empty;
                Console.WriteLine($"[{simNow:HH:mm:ss}] {_name}: -> Empty (was batch={BatchId})");
                BatchId = "";
                _holdStartTime = null;
                _belowSpecSince = null;
                _disposalReason = "";
                _disposedL = 0;
            }

            private void PublishStateTags()
            {
                _ctx.Tags.Write(_tValveIn, TankState == BufferTankState.Filling);
                // The outlet is open both when feeding the filler and when pumping to waste.
                _ctx.Tags.Write(_tValveOut, TankState == BufferTankState.Draining
                                         || TankState == BufferTankState.Disposing);
                // Tank occupancy + which bulk batch it holds (buffer→pack genealogy source)
                var idBase = _agitBase.Replace("_AGIT", "");   // e.g. "BT451"
                _ctx.Tags.Write($"{idBase}_STATE", (int)TankState);
                _ctx.Tags.Write($"{idBase}_BATCH_ID", BatchId);
                _ctx.Tags.Write(_tAgitRun, _vessel.Level > 100);
                _ctx.Tags.Write($"{_agitBase}_FAULT", false);
                _cmAgit.Publish(_ctx.Tags,
                    vibration: $"{_agitBase}_VIB", bearingTemp: $"{_agitBase}_BRGTEMP",
                    mcsa: $"{_agitBase}_MCSA", failureCount: $"{_agitBase}_FAILCNT");
            }
        }
    }
}
