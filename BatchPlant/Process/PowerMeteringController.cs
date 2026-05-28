using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Process
{
    /// <summary>
    /// Simulates 5 power meters at the MCC (Motor Control Center) level,
    /// one per work center. Reads equipment state tags to compute realistic
    /// electrical load profiles, then drives PowerMeter models that produce
    /// energy, power quality, and consumption data.
    ///
    /// Meters:
    ///   PM-PL1   Production Line 1 (Areas 200 + 300 + 400 + 450)
    ///   PM-CIP   Production CIP (Area 700)
    ///   PM-PKG1  Packaging Line 1 (Areas 500 + 600)
    ///   PM-UTIL  Utilities (Area 800)
    ///   PM-RM    Raw Materials (Area 100)
    /// </summary>
    public class PowerMeteringController : ISimulatable
    {
        private readonly PlantContext _ctx;

        public int Priority => 80; // after all process controllers, before tag flusher

        // The 5 work center meters
        public PowerMeter PL1 { get; }
        public PowerMeter CIP { get; }
        public PowerMeter PKG1 { get; }
        public PowerMeter UTIL { get; }
        public PowerMeter RM { get; }

        // Compressor cycling state for utilities
        private double _compressorCycleTimer;
        private bool _compressorRunning = true;
        private const double CompressorOnSec = 45;
        private const double CompressorOffSec = 20;

        // Palletizer intermittent peak state
        private double _palletizerCycleTimer;

        public PowerMeteringController(PlantContext ctx)
        {
            _ctx = ctx;

            PL1 = new PowerMeter(ctx, "PM-PL1") { MaxExpectedLoad = 150.0 };
            CIP = new PowerMeter(ctx, "PM-CIP") { MaxExpectedLoad = 45.0 };
            PKG1 = new PowerMeter(ctx, "PM-PKG1") { MaxExpectedLoad = 50.0 };
            UTIL = new PowerMeter(ctx, "PM-UTIL") { MaxExpectedLoad = 90.0 };
            RM = new PowerMeter(ctx, "PM-RM") { MaxExpectedLoad = 20.0 };

            PL1.Bind(new PowerMeterTagMap
            {
                ActivePower = "PM_PL1_kW",
                ActiveEnergy = "PM_PL1_kWh",
                ReactivePower = "PM_PL1_kVAR",
                PowerFactor = "PM_PL1_PF",
                Voltage = "PM_PL1_V_LL",
                Current = "PM_PL1_I_Avg",
                Frequency = "PM_PL1_Freq",
                ThdCurrent = "PM_PL1_THD_I",
                VoltageUnbalance = "PM_PL1_V_Unbal"
            });

            CIP.Bind(new PowerMeterTagMap
            {
                ActivePower = "PM_CIP_kW",
                ActiveEnergy = "PM_CIP_kWh",
                ReactivePower = "PM_CIP_kVAR",
                PowerFactor = "PM_CIP_PF",
                Voltage = "PM_CIP_V_LL",
                Current = "PM_CIP_I_Avg",
                Frequency = "PM_CIP_Freq",
                ThdCurrent = "PM_CIP_THD_I",
                VoltageUnbalance = "PM_CIP_V_Unbal"
            });

            PKG1.Bind(new PowerMeterTagMap
            {
                ActivePower = "PM_PKG1_kW",
                ActiveEnergy = "PM_PKG1_kWh",
                ReactivePower = "PM_PKG1_kVAR",
                PowerFactor = "PM_PKG1_PF",
                Voltage = "PM_PKG1_V_LL",
                Current = "PM_PKG1_I_Avg",
                Frequency = "PM_PKG1_Freq",
                ThdCurrent = "PM_PKG1_THD_I",
                VoltageUnbalance = "PM_PKG1_V_Unbal"
            });

            UTIL.Bind(new PowerMeterTagMap
            {
                ActivePower = "PM_UTIL_kW",
                ActiveEnergy = "PM_UTIL_kWh",
                ReactivePower = "PM_UTIL_kVAR",
                PowerFactor = "PM_UTIL_PF",
                Voltage = "PM_UTIL_V_LL",
                Current = "PM_UTIL_I_Avg",
                Frequency = "PM_UTIL_Freq",
                ThdCurrent = "PM_UTIL_THD_I",
                VoltageUnbalance = "PM_UTIL_V_Unbal"
            });

            RM.Bind(new PowerMeterTagMap
            {
                ActivePower = "PM_RM_kW",
                ActiveEnergy = "PM_RM_kWh",
                ReactivePower = "PM_RM_kVAR",
                PowerFactor = "PM_RM_PF",
                Voltage = "PM_RM_V_LL",
                Current = "PM_RM_I_Avg",
                Frequency = "PM_RM_Freq",
                ThdCurrent = "PM_RM_THD_I",
                VoltageUnbalance = "PM_RM_V_Unbal"
            });
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            double dtSec = dt.TotalSeconds;
            var t = _ctx.Tags;

            // --- PM-PL1: Production Line 1 (Areas 200 + 300 + 400 + 450) ---
            UpdateProductionLine1(t, dt);

            // --- PM-CIP: Production CIP (Area 700) ---
            UpdateCip(t, dt);

            // --- PM-PKG1: Packaging Line 1 (Areas 500 + 600) ---
            UpdatePackagingLine1(t, dt, dtSec);

            // --- PM-UTIL: Utilities (Area 800) ---
            UpdateUtilities(t, dt, dtSec);

            // --- PM-RM: Raw Materials (Area 100) ---
            UpdateRawMaterials(t, dt);
        }

        private void UpdateProductionLine1(OpcUa.TagRegistry t, TimeSpan dt)
        {
            double kw = 3.5; // base load: instrumentation, panel power, lighting
            double thdVfd = 0.0;
            bool hasHeavyMotor = false;
            bool hasVfd = false;

            // Area 200: Pre-Mix
            int mx201State = t.Read<int>("MX201_STATE");
            switch (mx201State)
            {
                case 1: // FillingWater
                    kw += 2.5;
                    break;
                case 2: // DosingPaste
                    kw += 5.5; // viscous paste pump draws more
                    break;
                case 3: // DosingVinegar
                    kw += 3.0;
                    break;
                case 4: // RampingAgitator
                    kw += 25.0; // average of ramp (5→45 kW)
                    hasVfd = true;
                    thdVfd += 4.0; // ramping VFD generates more harmonics
                    break;
                case 5: // Mixing (high-shear at full speed)
                    kw += 45.0;
                    hasVfd = true;
                    hasHeavyMotor = true;
                    thdVfd += 6.0;
                    break;
                case 6: // RampingDown
                    kw += 15.0; // decelerating
                    hasVfd = true;
                    thdVfd += 3.0;
                    break;
                case 7: // Discharging
                    kw += 4.0; // discharge pump
                    break;
            }

            // Area 300: Cooking
            int kt301State = t.Read<int>("KT301_STATE");
            switch (kt301State)
            {
                case 1: // Filling
                    kw += 3.0; // transfer receiving
                    break;
                case 2: // HeatingUp
                    kw += 12.0; // agitator + steam valve actuators
                    hasHeavyMotor = true;
                    break;
                case 3: // Holding
                    kw += 10.0; // agitator at moderate speed
                    hasHeavyMotor = true;
                    break;
                case 4: // Cooling
                    kw += 10.0; // agitator + cooling valve actuator
                    hasHeavyMotor = true;
                    break;
                case 5: // Discharging
                    kw += 5.0; // agitator + discharge pump
                    break;
            }

            // Area 400: Processing
            int da401State = t.Read<int>("DA401_STATE");
            switch (da401State)
            {
                case 1: // Filling
                    kw += 3.0;
                    break;
                case 2: // PullingVacuum (vacuum pump startup — draws extra)
                    kw += 18.0;
                    hasHeavyMotor = true;
                    thdVfd += 2.0;
                    break;
                case 3: // Deaerating (vacuum pump steady state)
                    kw += 15.0;
                    hasHeavyMotor = true;
                    thdVfd += 1.5;
                    break;
                case 4: // Homogenizing (biggest electrical load in the plant)
                    kw += 75.0; // homogenizer
                    kw += 12.0; // vacuum pump maintaining
                    hasVfd = true;
                    hasHeavyMotor = true;
                    thdVfd += 5.0;
                    break;
                case 5: // VentingVacuum
                    kw += 2.0;
                    break;
                case 6: // Draining
                    kw += 5.0; // transfer pump
                    break;
            }

            // Area 450: Buffer tanks (instruments + recirculation pump)
            kw += 2.0;

            // Power factor depends on load composition
            double pf;
            if (kw < 8.0)
                pf = 0.75; // very light load — poor PF
            else if (hasVfd && hasHeavyMotor)
                pf = 0.93; // VFD-driven heavy motors — good PF
            else if (hasHeavyMotor)
                pf = 0.85; // direct-on-line motors — moderate PF
            else if (hasVfd)
                pf = 0.96; // VFD with light load
            else
                pf = 0.89; // mixed light loads

            PL1.Update(kw, pf, thdVfd, dt);
        }

        private void UpdateCip(OpcUa.TagRegistry t, TimeSpan dt)
        {
            double kw = 0.5; // panel/instruments standby
            double thdVfd = 0.0;
            bool hasPump = false;
            bool hasHeating = false;

            int cipStep = t.Read<int>("CIP_STEP");
            switch (cipStep)
            {
                case 1: // PreRinse
                    kw += 15.0;
                    hasPump = true;
                    break;
                case 2: // CausticWash (pump + solution heating)
                    kw += 15.0;
                    kw += 8.0; // inline heater
                    hasPump = true;
                    hasHeating = true;
                    break;
                case 3: // IntRinse
                    kw += 15.0;
                    hasPump = true;
                    break;
                case 4: // AcidWash (pump + heating, slightly less)
                    kw += 15.0;
                    kw += 6.0;
                    hasPump = true;
                    hasHeating = true;
                    break;
                case 5: // FinalRinse
                    kw += 15.0;
                    hasPump = true;
                    break;
                case 6: // Sanitize (pump + chemical dosing pump)
                    kw += 15.0;
                    kw += 1.5;
                    hasPump = true;
                    thdVfd += 1.0;
                    break;
                case 7: // Drain (pump at reduced speed)
                    kw += 8.0;
                    hasPump = true;
                    thdVfd += 2.0; // VFD at partial speed = more THD
                    break;
            }

            // CIP pump is VFD-driven
            if (hasPump) thdVfd += 3.0;

            double pf;
            if (cipStep == 0)
                pf = 0.70; // standby — mostly instrument transformers
            else if (hasHeating)
                pf = 0.95; // heating element is resistive, improves overall PF
            else if (hasPump)
                pf = 0.87; // pump motor load
            else
                pf = 0.80;

            CIP.Update(kw, pf, thdVfd, dt);
        }

        private void UpdatePackagingLine1(OpcUa.TagRegistry t, TimeSpan dt, double dtSec)
        {
            double kw = 3.0; // base: sensors, idle conveyors, HMIs
            double thdVfd = 0.0;
            bool lineRunning = false;

            int packState = t.Read<int>("PACK_LINE_OP_STATE");

            switch (packState)
            {
                case 3: // Running
                    lineRunning = true;
                    // Filler (servo-driven rotary, 12 heads)
                    kw += 8.0;
                    thdVfd += 3.0;
                    // Cap torquer (servo)
                    kw += 2.5;
                    thdVfd += 0.5;
                    // Inversion tunnel drive
                    kw += 1.5;
                    // Cooling tunnel (fans + conveyor)
                    kw += 4.0;
                    // Exit conveyor
                    kw += 2.0;
                    // Labeler (servo-driven applicator)
                    kw += 3.0;
                    thdVfd += 1.0;
                    // Date coder (laser/inkjet)
                    kw += 0.5;
                    // X-ray inspection + metal detector
                    kw += 2.0;
                    // Case packer (pneumatic + servo)
                    kw += 5.0;
                    thdVfd += 1.5;
                    // Palletizer (intermittent peaks during layer placement)
                    _palletizerCycleTimer += dtSec;
                    double palCycleSec = 8.0; // one layer every ~8 seconds
                    bool palPeak = (_palletizerCycleTimer % palCycleSec) < 2.0; // peak for 2 of every 8 sec
                    kw += palPeak ? 12.0 : 5.0;
                    thdVfd += 2.0;
                    break;

                case 4: // Starved — some machines idle at partial load
                case 5: // Blocked
                    kw += 6.0; // conveyors still moving, cooling tunnel still running
                    thdVfd += 1.0;
                    break;

                case 6: // UnplannedDT — most machines stopped, some coasting
                    kw += 2.0;
                    break;
            }

            double pf;
            if (lineRunning)
                pf = 0.91; // many servo drives — decent PF
            else if (packState == 4 || packState == 5)
                pf = 0.83; // partial load — worse PF
            else
                pf = 0.72; // idle — mostly instrument transformers

            PKG1.Update(kw, pf, thdVfd, dt);
        }

        private void UpdateUtilities(OpcUa.TagRegistry t, TimeSpan dt, double dtSec)
        {
            double kw = 8.0; // BMS controls, plant lighting, panel HVAC
            double thdVfd = 0.0;

            // Steam system: boiler blower/feed pump scale with demand
            double steamDemandPct = t.Read<double>("STM_DEMAND");
            double steamFrac = Math.Clamp(steamDemandPct / 100.0, 0, 1.0);
            kw += 3.0 + 10.0 * steamFrac; // blower fan 3-13 kW
            kw += 4.0 * steamFrac;         // feedwater pump 0-4 kW

            // Cooling water system: staged pumps and fans
            int cwPump1 = t.Read<int>("CW_PUMP1_STS");
            int cwPump2 = t.Read<int>("CW_PUMP2_STS");
            int cwFan1 = t.Read<int>("CW_TOWER_FAN1");
            int cwFan2 = t.Read<int>("CW_TOWER_FAN2");
            if (cwPump1 == 1) kw += 7.5;
            if (cwPump2 == 1) kw += 7.5;
            if (cwFan1 == 1) kw += 5.5;
            if (cwFan2 == 1) kw += 5.5;
            thdVfd += 1.5; // cooling pump VFDs

            // Compressed air compressor (cycles on/off)
            _compressorCycleTimer += dtSec;
            double cyclePeriod = CompressorOnSec + CompressorOffSec;
            if (_compressorCycleTimer >= cyclePeriod)
                _compressorCycleTimer -= cyclePeriod;
            _compressorRunning = _compressorCycleTimer < CompressorOnSec;
            if (_compressorRunning)
            {
                kw += 30.0;
                thdVfd += 4.0; // compressor VFD
            }

            // Hot water pump (runs when CIP is active)
            bool cipActive = t.Read<int>("CIP_STEP") > 0;
            if (cipActive) kw += 3.0;

            // PF: heavy motor load from compressor + pumps
            double pf = _compressorRunning ? 0.88 : 0.84;
            if (steamFrac > 0.5) pf += 0.03; // blower at high speed improves PF slightly

            UTIL.Update(kw, pf, thdVfd, dt);
        }

        private void UpdateRawMaterials(OpcUa.TagRegistry t, TimeSpan dt)
        {
            double kw = 1.5; // tank instruments, area lighting
            double thdVfd = 0.0;
            bool hasPumps = false;

            // P-101: paste pump
            int p101Sts = t.Read<int>("P101_STS");
            if (p101Sts == 1)
            {
                kw += 5.5; // viscous paste requires more power
                hasPumps = true;
            }

            // P-102: vinegar/water pump (check via valve status)
            bool vinVlvOpen = t.Read<bool>("VIN_VLV_STS");
            if (vinVlvOpen)
            {
                kw += 3.5;
                hasPumps = true;
            }

            // Sugar feeder
            bool sugarRun = t.Read<bool>("SUGAR_FEEDER_RUN");
            if (sugarRun) kw += 1.5;

            // Salt feeder
            bool saltRun = t.Read<bool>("SALT_FEEDER_RUN");
            if (saltRun) kw += 0.8;

            // Spice feeder
            bool spiceRun = t.Read<bool>("SPICE_FEEDER_RUN");
            if (spiceRun) kw += 0.5;

            // Tank agitators (intermittent — paste tanks have slow agitators to prevent settling)
            // Simulate ~50% duty cycle using sim time
            double agitCycle = 120.0; // 2 minute cycle
            double fracInCycle = (_ctx.Clock.Now.Second + _ctx.Clock.Now.Minute * 60.0) % agitCycle;
            if (fracInCycle < agitCycle * 0.5)
                kw += 3.0; // two tank agitators running

            double pf;
            if (hasPumps)
                pf = 0.86; // pump motors
            else if (kw > 4.0)
                pf = 0.82; // feeders + agitators (small motors, poor PF at partial load)
            else
                pf = 0.72; // standby

            if (hasPumps) thdVfd += 2.0; // pump VFDs

            RM.Update(kw, pf, thdVfd, dt);
        }
    }
}
