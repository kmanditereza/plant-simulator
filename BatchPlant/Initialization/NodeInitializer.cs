using BatchPlant;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BatchPlant.Initialization
{
    public static class NodeInitializer
    {
        /// <summary>
        /// Initialize all OPC UA nodes with default values.
        /// </summary>
        public static void InitializeAllNodes()
        {
            InitializeRawMaterialTanks();
            InitializeBatchControlSignals();
            InitializeBatchProductionNodes();
            InitializeMixerNodes();
            InitializeHeatExchangerNodes();
            InitializeReactorNodes();
            InitializeFillerNodes();
        }

        private static void InitializeRawMaterialTanks()
        {
            // TANK 1
            BatchPlantNodeManager.LT101.Value = 50000.0;
            BatchPlantNodeManager.FCV101.Value = 0;
            BatchPlantNodeManager.P101_START.Value = false;
            BatchPlantNodeManager.P101_RUN.Value = false;
            BatchPlantNodeManager.P101_FAULT.Value = false;
            BatchPlantNodeManager.TT101.Value = 25.0;
            BatchPlantNodeManager.PT101.Value = 50.0;

            // TANK 2
            BatchPlantNodeManager.LT102.Value = 50000.0;
            BatchPlantNodeManager.FCV102.Value = 0;
            BatchPlantNodeManager.P102_START.Value = false;
            BatchPlantNodeManager.P102_RUN.Value = false;
            BatchPlantNodeManager.P102_FAULT.Value = false;
            BatchPlantNodeManager.TT102.Value = 25.0;
            BatchPlantNodeManager.PT102.Value = 50.0;

            // TANK 3
            BatchPlantNodeManager.LT103.Value = 50000.0;
            BatchPlantNodeManager.FCV103.Value = 0;
            BatchPlantNodeManager.P103_START.Value = false;
            BatchPlantNodeManager.P103_RUN.Value = false;
            BatchPlantNodeManager.P103_FAULT.Value = false;
            BatchPlantNodeManager.TT103.Value = 25.0;
            BatchPlantNodeManager.PT103.Value = 50.0;
        }

        private static void InitializeBatchControlSignals()
        {
            BatchPlantNodeManager.BATCH_START.Value = true;
            BatchPlantNodeManager.BATCH_PAUSE.Value = false;
            BatchPlantNodeManager.BATCH_ABORT.Value = false;
        }

        private static void InitializeBatchProductionNodes()
        {
            BatchPlantNodeManager.batch_code.Value = DateTime.Now + BatchPlantNodeManager.product_code.Value.ToString() + "L1";
            BatchPlantNodeManager.product_code.Value = "SKU-10045";
            BatchPlantNodeManager.batch_status.Value = "idle";

            BatchPlantNodeManager.batch_start_time.Value = DateTime.Now;
            BatchPlantNodeManager.batch_end_time.Value = DateTime.Now;
            BatchPlantNodeManager.batches_done.Value = 0;
            BatchPlantNodeManager.batches_required.Value = -1;

            BatchPlantNodeManager.material1_target.Value = 300.0;
            BatchPlantNodeManager.material2_target.Value = 150.0;
            BatchPlantNodeManager.material3_target.Value = 150.0;
            BatchPlantNodeManager.material1_actual.Value = 0.0;
            BatchPlantNodeManager.material2_actual.Value = 0.0;
            BatchPlantNodeManager.material3_actual.Value = 0.0;
            BatchPlantNodeManager.material1_name.Value = "TOMATO PASTE";
            BatchPlantNodeManager.material2_name.Value = "ADDITIVES";
            BatchPlantNodeManager.material3_name.Value = "SEASONING BLEND";
        }

        private static void InitializeMixerNodes()
        {
            BatchPlantNodeManager.TT201.Value = 25.0;
            BatchPlantNodeManager.LT201.Value = 0.0;
            BatchPlantNodeManager.VISC201.Value = 0.0;
            BatchPlantNodeManager.TURB201.Value = 0.0;

            BatchPlantNodeManager.MT201_START.Value = true;
            BatchPlantNodeManager.MT201_RUN.Value = true;
            BatchPlantNodeManager.MT201_FAULT.Value = false;
            BatchPlantNodeManager.MT201_RPM.Value = 20.0;
            BatchPlantNodeManager.MT201_TRQ.Value = 5.0;
            BatchPlantNodeManager.MT201_PWR.Value = 1.5;
            BatchPlantNodeManager.MT201_AMP.Value = 2.5;
            BatchPlantNodeManager.MT201_VIB.Value = 0.02;

            BatchPlantNodeManager.MX101_STATE.Value = 1;
            BatchPlantNodeManager.MX101_MODE.Value = 2;
            BatchPlantNodeManager.mixer_status.Value = "Idle";
            BatchPlantNodeManager.mixing_time.Value = 900;
            BatchPlantNodeManager.mixer_energy_consumed.Value = 0.0;

            // Mixing setpoints
            BatchPlantNodeManager.SP_MX_INITIAL_SPEED.Value = 60.0;
            BatchPlantNodeManager.SP_MX_FINAL_SPEED.Value = 150.0;
            BatchPlantNodeManager.SP_MX_AMBIENT_TEMP.Value = 22.0;
            BatchPlantNodeManager.SP_MX_INITIAL_VISC.Value = 15000.0;
            BatchPlantNodeManager.SP_MX_TARGET_VISC.Value = 8000.0;

            // Mixer report parameters
            BatchPlantNodeManager.MX_RPT_AVG_AGITATOR_SPEED.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_AVG_MOTOR_POWER.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_FINAL_HOMOGENEITY.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_FINAL_TEMPERATURE.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_FINAL_TURBIDITY.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_FINAL_VISCOSITY.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_INITIAL_VISCOSITY.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_MAX_MOTOR_POWER.Value = 0.0;
            BatchPlantNodeManager.MX_RPT_MIX_DURATION.Value = 0.0;
        }

        private static void InitializeHeatExchangerNodes()
        {
            BatchPlantNodeManager.TT301.Value = 22.0;
            BatchPlantNodeManager.TT302.Value = 22.0;
            BatchPlantNodeManager.PT301.Value = 0.3;
            BatchPlantNodeManager.FT301.Value = 0.0;
            BatchPlantNodeManager.FT302.Value = 0.0;
            BatchPlantNodeManager.FT303.Value = 0.0;

            BatchPlantNodeManager.SV301.Value = 0.0;

            BatchPlantNodeManager.P301_START.Value = false;
            BatchPlantNodeManager.P301_RUN.Value = false;
            BatchPlantNodeManager.P301_FAULT.Value = false;
            BatchPlantNodeManager.P301_SPEED.Value = 0.0;

            BatchPlantNodeManager.HX301_STATE.Value = 1;
            BatchPlantNodeManager.HX301_MODE.Value = 1;
            BatchPlantNodeManager.hx_status.Value = "Idle";
            BatchPlantNodeManager.heating_time.Value = 1200;
            BatchPlantNodeManager.hx_energy_consumed.Value = 0.0;

            BatchPlantNodeManager.SP_HX_TEMP.Value = 90.0;
            BatchPlantNodeManager.PID_HX_OUTPUT.Value = 0.0;

            BatchPlantNodeManager.HX_FOULING_INDEX.Value = 0.0;
            BatchPlantNodeManager.HX_BASELINE_DP.Value = 0.3;
        }

        private static void InitializeReactorNodes()
        {
            // Temperature (Multi-point)
            BatchPlantNodeManager.TT401.Value = 25.0;
            BatchPlantNodeManager.TT401_1.Value = 25.0;
            BatchPlantNodeManager.TT401_2.Value = 25.0;
            BatchPlantNodeManager.TT401_3.Value = 25.0;
            BatchPlantNodeManager.TT401_4.Value = 25.0;
            BatchPlantNodeManager.TT401_5.Value = 25.0;

            // Pressure and Level
            BatchPlantNodeManager.PT401.Value = 0.0;
            BatchPlantNodeManager.LT401.Value = 0.0;

            // Analytical Instruments
            BatchPlantNodeManager.pH401.Value = 7.0;
            BatchPlantNodeManager.BRIX401.Value = 0.0;
            BatchPlantNodeManager.COLOR401_L.Value = 50.0;
            BatchPlantNodeManager.COLOR401_a.Value = 0.0;
            BatchPlantNodeManager.COLOR401_b.Value = 0.0;
            BatchPlantNodeManager.VISC401.Value = 0.0;

            // Agitator Motor
            BatchPlantNodeManager.MT401_START.Value = false;
            BatchPlantNodeManager.MT401_RUN.Value = false;
            BatchPlantNodeManager.MT401_FAULT.Value = false;
            BatchPlantNodeManager.MT401_RPM.Value = 0.0;
            BatchPlantNodeManager.MT401_TRQ.Value = 0.0;
            BatchPlantNodeManager.MT401_PWR.Value = 0.0;

            // Vacuum System
            BatchPlantNodeManager.VP401_START.Value = false;
            BatchPlantNodeManager.VP401_RUN.Value = false;
            BatchPlantNodeManager.VP401_FAULT.Value = false;
            BatchPlantNodeManager.PCV401.Value = 0.0;

            // Jacket Control
            BatchPlantNodeManager.SV401.Value = 0.0;
            BatchPlantNodeManager.CV401.Value = 0.0;
            BatchPlantNodeManager.TT401_JKT.Value = 25.0;

            // Acid Dosing System
            BatchPlantNodeManager.ACID_PUMP_START.Value = false;
            BatchPlantNodeManager.ACID_PUMP_RUN.Value = false;
            BatchPlantNodeManager.ACID_FLOW.Value = 0.0;
            BatchPlantNodeManager.ACID_TOTAL.Value = 0.0;

            // Transfer Valve
            BatchPlantNodeManager.VLV401.Value = 0;

            // Reactor State
            BatchPlantNodeManager.RX401_STATE.Value = 1;
            BatchPlantNodeManager.RX401_MODE.Value = 1;
            BatchPlantNodeManager.reactor_status.Value = "Idle";
            BatchPlantNodeManager.reaction_time.Value = 1800;
            BatchPlantNodeManager.reactor_energy_consumed.Value = 0.0;

            // PID Setpoints
            BatchPlantNodeManager.SP_RX_TEMP.Value = 88.0;
            BatchPlantNodeManager.SP_RX_PH.Value = 4.1;
            BatchPlantNodeManager.SP_RX_VACUUM.Value = -0.2;
            BatchPlantNodeManager.PID_RX_OUTPUT.Value = 0.0;

            // Reaction Phase Tracking
            BatchPlantNodeManager.reaction_phase.Value = 0;
            BatchPlantNodeManager.phase_start_time.Value = DateTime.Now;
            BatchPlantNodeManager.hold_time_remaining.Value = 0.0;

            // Quality Metrics
            BatchPlantNodeManager.color_delta_e.Value = 0.0;
            BatchPlantNodeManager.batch_ph_ok.Value = false;
        }

        private static void InitializeFillerNodes()
        {
            // Fill Head Temperature
            BatchPlantNodeManager.TT501.Value = 25.0;

            // Load Cell / Weighing
            BatchPlantNodeManager.LC501.Value = 0.0;
            BatchPlantNodeManager.LC501_TARE.Value = 0.0;
            BatchPlantNodeManager.LC501_NET.Value = 0.0;

            // Fill Valves
            BatchPlantNodeManager.FV501_FAST.Value = 0;
            BatchPlantNodeManager.FV501_DRIBBLE.Value = 0;

            // Dosing Pump
            BatchPlantNodeManager.P501_START.Value = false;
            BatchPlantNodeManager.P501_RUN.Value = false;
            BatchPlantNodeManager.P501_FAULT.Value = false;
            BatchPlantNodeManager.P501_SPEED.Value = 0.0;

            // Conveyor
            BatchPlantNodeManager.CONV501_RUN.Value = false;
            BatchPlantNodeManager.CONV501_SPEED.Value = 0.0;
            BatchPlantNodeManager.CONV501_FAULT.Value = false;

            // Capper
            BatchPlantNodeManager.CAP501_RUN.Value = false;
            BatchPlantNodeManager.CAP501_FAULT.Value = false;

            // Filler State
            BatchPlantNodeManager.FL501_STATE.Value = 1;
            BatchPlantNodeManager.FL501_MODE.Value = 1;
            BatchPlantNodeManager.filler_status.Value = "Idle";
            BatchPlantNodeManager.filler_energy_consumed.Value = 0.0;

            // Fill Setpoints
            BatchPlantNodeManager.SP_FILL_WEIGHT.Value = 525.0;
            BatchPlantNodeManager.SP_FILL_TEMP_MIN.Value = 77.0;
            BatchPlantNodeManager.SP_FILL_TEMP_MAX.Value = 84.0;
            BatchPlantNodeManager.SP_JAR_VOLUME.Value = 500.0;
            BatchPlantNodeManager.SP_FILL_RATE.Value = 20.0;

            // Weight Tolerances
            BatchPlantNodeManager.SP_WEIGHT_TOL_PCT.Value = 2.0;
            BatchPlantNodeManager.SP_OVERWEIGHT.Value = 535.5;
            BatchPlantNodeManager.SP_UNDERWEIGHT.Value = 514.5;

            // Inflight Compensation
            BatchPlantNodeManager.INFLIGHT_COMP.Value = 4.0;

            // Headspace
            BatchPlantNodeManager.SP_HEADSPACE.Value = 9.0;
            BatchPlantNodeManager.HEADSPACE_ACTUAL.Value = 0.0;

            // Counters
            BatchPlantNodeManager.CNT501_TOTAL.Value = 0;
            BatchPlantNodeManager.CNT501_PASS.Value = 0;
            BatchPlantNodeManager.CNT501_UNDERWEIGHT.Value = 0;
            BatchPlantNodeManager.CNT501_OVERWEIGHT.Value = 0;
            BatchPlantNodeManager.CNT501_TEMP_REJECT.Value = 0;

            // Current Jar Data
            BatchPlantNodeManager.CURRENT_JAR_WEIGHT.Value = 0.0;
            BatchPlantNodeManager.CURRENT_JAR_TEMP.Value = 0.0;
            BatchPlantNodeManager.CURRENT_JAR_STATUS.Value = "";
            BatchPlantNodeManager.PREVIOUS_JAR_WEIGHT.Value = 0.0;

            // Fill Statistics
            BatchPlantNodeManager.FILL_WEIGHT_AVG.Value = 0.0;
            BatchPlantNodeManager.FILL_WEIGHT_MIN.Value = 0.0;
            BatchPlantNodeManager.FILL_WEIGHT_MAX.Value = 0.0;
            BatchPlantNodeManager.FILL_WEIGHT_STDDEV.Value = 0.0;
            BatchPlantNodeManager.FIRST_PASS_YIELD.Value = 0.0;
            BatchPlantNodeManager.GIVEAWAY_TOTAL.Value = 0.0;

            // Batch Tracking
            BatchPlantNodeManager.JARS_REMAINING.Value = 0;
            BatchPlantNodeManager.FILL_START_TIME.Value = DateTime.Now;
            BatchPlantNodeManager.FILL_END_TIME.Value = DateTime.Now;
            BatchPlantNodeManager.FILL_ELAPSED_TIME.Value = 0.0;

            // Indicators
            BatchPlantNodeManager.IND_FILLING.Value = false;
            BatchPlantNodeManager.IND_JAR_PRESENT.Value = false;
            BatchPlantNodeManager.IND_WEIGHT_OK.Value = false;
            BatchPlantNodeManager.IND_TEMP_OK.Value = false;
        }
    }
}

