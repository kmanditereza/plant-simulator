namespace BatchPlant.Batch
{
    /// <summary>
    /// Defines all phase observers — one per (area, state) pair worth tracking.
    /// When a batch's current state matches an entry here, the corresponding report
    /// parameters are collected for the duration of that phase.
    /// </summary>
    public static class PhaseObserverCatalog
    {
        /// <summary>
        /// Key: (areaName, stateName)
        /// Value: PhaseObserver defining what to collect
        /// </summary>
        public static readonly Dictionary<(string area, string state), PhaseObserver> Observers = BuildCatalog();

        private static Dictionary<(string area, string state), PhaseObserver> BuildCatalog()
        {
            var cat = new Dictionary<(string, string), PhaseObserver>();

            // ---- AREA 200: PRE-MIX ----

            cat[("Area 200", "FillingWater")] = new PhaseObserver
            {
                AreaName = "Area 200",
                StateName = "FillingWater",
                PhaseLabel = "PreMix.DoseWater",
                UnitName = "MX-201",
                SnapshotOnEntry = { ["startLevelL"] = "LIT-201" },
                SnapshotOnExit = { ["endLevelL"] = "LIT-201", ["endTempC"] = "TIT-201" },
                SampledTags =
                {
                    ["avgInflowLpm"] = ("FIT-201", AggregationType.Average),
                    ["totalWaterL"] = ("H2O_FLOW", AggregationType.IntegratePerMinute)
                }
            };

            cat[("Area 200", "DosingPaste")] = new PhaseObserver
            {
                AreaName = "Area 200",
                StateName = "DosingPaste",
                PhaseLabel = "PreMix.DosePaste",
                UnitName = "MX-201",
                SnapshotOnExit = { ["endLevelL"] = "LIT-201", ["endWeightKg"] = "WIT-201" },
                SampledTags =
                {
                    ["avgPumpCurrentA"] = ("P101_AMPS", AggregationType.Average),
                    ["totalPasteKg"] = ("P101_FLOW", AggregationType.IntegratePerMinute)
                }
            };

            cat[("Area 200", "DosingVinegar")] = new PhaseObserver
            {
                AreaName = "Area 200",
                StateName = "DosingVinegar",
                PhaseLabel = "PreMix.DoseVinegar",
                UnitName = "MX-201",
                SnapshotOnExit = { ["endLevelL"] = "LIT-201" },
                SampledTags =
                {
                    ["totalVinegarL"] = ("VIN_TK_FLOW", AggregationType.IntegratePerMinute)
                }
            };

            cat[("Area 200", "Mixing")] = new PhaseObserver
            {
                AreaName = "Area 200",
                StateName = "Mixing",
                PhaseLabel = "PreMix.HighShearMix",
                UnitName = "MX-201",
                SnapshotOnEntry = { ["startTempC"] = "TIT-201" },
                SnapshotOnExit = { ["finalTempC"] = "TIT-201", ["finalLevelL"] = "LIT-201" },
                SampledTags =
                {
                    ["avgPowerKw"] = ("AGIT201_PWR", AggregationType.Average),
                    ["maxPowerKw"] = ("AGIT201_PWR", AggregationType.Peak),
                    ["avgTorqueNm"] = ("AGIT201_TRQ", AggregationType.Average),
                    ["avgCurrentA"] = ("AGIT201_AMPS", AggregationType.Average),
                    ["maxVibrationMmS"] = ("AGIT201_VIB", AggregationType.Peak)
                }
            };

            cat[("Area 200", "Discharging")] = new PhaseObserver
            {
                AreaName = "Area 200",
                StateName = "Discharging",
                PhaseLabel = "PreMix.Discharge",
                UnitName = "MX-201",
                SnapshotOnEntry = { ["startLevelL"] = "LIT-201" }
            };

            // ---- AREA 300: COOKING ----

            cat[("Area 300", "Filling")] = new PhaseObserver
            {
                AreaName = "Area 300",
                StateName = "Filling",
                PhaseLabel = "Cook.Fill",
                UnitName = "KT-301",
                SnapshotOnExit = { ["endLevelL"] = "LIT-301", ["startTempC"] = "TIT-301" }
            };

            cat[("Area 300", "HeatingUp")] = new PhaseObserver
            {
                AreaName = "Area 300",
                StateName = "HeatingUp",
                PhaseLabel = "Cook.HeatToTarget",
                UnitName = "KT-301",
                SnapshotOnEntry = { ["startTempC"] = "TIT-301" },
                SnapshotOnExit = { ["endTempC"] = "TIT-301" },
                SampledTags =
                {
                    ["peakTempC"] = ("TIT-301", AggregationType.Peak),
                    ["avgSteamValvePct"] = ("TCV-301", AggregationType.Average),
                    ["avgPidOutputPct"] = ("PID_301_OP", AggregationType.Average),
                    ["steamUsedKg"] = ("FIT-302", AggregationType.IntegratePerHour),
                    ["peakJacketPressBar"] = ("PT-301_JKT", AggregationType.Peak)
                }
            };

            cat[("Area 300", "Holding")] = new PhaseObserver
            {
                AreaName = "Area 300",
                StateName = "Holding",
                PhaseLabel = "Cook.PasteurizationHold",
                UnitName = "KT-301",
                SnapshotOnExit = { ["holdTimerFinalSec"] = "KT301_HOLD_TMR" },
                SampledTags =
                {
                    ["minTempC"] = ("TIT-301", AggregationType.Min),
                    ["maxTempC"] = ("TIT-301", AggregationType.Peak),
                    ["avgTempC"] = ("TIT-301", AggregationType.Average),
                    ["avgPhInline"] = ("AIT-301", AggregationType.Average),
                    ["steamDuringHoldKg"] = ("FIT-302", AggregationType.IntegratePerHour)
                }
            };

            cat[("Area 300", "Cooling")] = new PhaseObserver
            {
                AreaName = "Area 300",
                StateName = "Cooling",
                PhaseLabel = "Cook.Cool",
                UnitName = "KT-301",
                SnapshotOnEntry = { ["startTempC"] = "TIT-301" },
                SnapshotOnExit = { ["endTempC"] = "TIT-301" }
            };

            cat[("Area 300", "Discharging")] = new PhaseObserver
            {
                AreaName = "Area 300",
                StateName = "Discharging",
                PhaseLabel = "Cook.Discharge",
                UnitName = "KT-301",
                SnapshotOnEntry = { ["startLevelL"] = "LIT-301" }
            };

            // ---- AREA 400: DEAERATION + HOMOGENIZATION ----

            cat[("Area 400", "Filling")] = new PhaseObserver
            {
                AreaName = "Area 400",
                StateName = "Filling",
                PhaseLabel = "Process.Fill",
                UnitName = "DA-401",
                SnapshotOnExit = { ["endLevelL"] = "401-LI-001", ["endTempC"] = "401-TI-001" }
            };

            cat[("Area 400", "PullingVacuum")] = new PhaseObserver
            {
                AreaName = "Area 400",
                StateName = "PullingVacuum",
                PhaseLabel = "Process.PullVacuum",
                UnitName = "DA-401",
                SnapshotOnExit = { ["finalVacuumMmHg"] = "401-PI-001" },
                SampledTags =
                {
                    ["peakVacuumMmHg"] = ("401-PI-001", AggregationType.Peak)
                }
            };

            cat[("Area 400", "Deaerating")] = new PhaseObserver
            {
                AreaName = "Area 400",
                StateName = "Deaerating",
                PhaseLabel = "Process.Deaerate",
                UnitName = "DA-401",
                SampledTags =
                {
                    ["minVacuumMmHg"] = ("401-PI-001", AggregationType.Min),
                    ["avgVacuumMmHg"] = ("401-PI-001", AggregationType.Average)
                }
            };

            cat[("Area 400", "Homogenizing")] = new PhaseObserver
            {
                AreaName = "Area 400",
                StateName = "Homogenizing",
                PhaseLabel = "Process.Homogenize",
                UnitName = "HG-401",
                SampledTags =
                {
                    ["avgPressureBar"] = ("HOMOG_401_PRESS", AggregationType.Average),
                    ["minPressureBar"] = ("HOMOG_401_PRESS", AggregationType.Min),
                    ["maxPressureBar"] = ("HOMOG_401_PRESS", AggregationType.Peak),
                    ["avgMotorCurrentA"] = ("HOMOG_401_AMPS", AggregationType.Average),
                    ["valveHoursAtStart"] = ("HOMOG_401_VLV_HRS", AggregationType.Initial),
                    ["valveHoursAtEnd"] = ("HOMOG_401_VLV_HRS", AggregationType.Final)
                }
            };

            cat[("Area 400", "VentingVacuum")] = new PhaseObserver
            {
                AreaName = "Area 400",
                StateName = "VentingVacuum",
                PhaseLabel = "Process.VentVacuum",
                UnitName = "DA-401"
            };

            return cat;
        }

        public static PhaseObserver? TryGet(string area, string state)
        {
            Observers.TryGetValue((area, state), out var obs);
            return obs;
        }
    }
}
