using BatchPlant.Core;
using BatchPlant.Equipment;

namespace BatchPlant.Process
{
    /// <summary>
    /// Area 700 - Clean-in-Place (CIP).
    ///
    /// 7-step automated cleaning sequence. Runs periodically (every ~45 min sim time in demo
    /// mode; would be every 8-12 hours in a real plant, or triggered by product changeover).
    ///
    /// Sequence:
    ///   1. Pre-Rinse       (10 min) - Water @ 40°C, flush residual product
    ///   2. Caustic Wash    (20 min) - 2% NaOH @ 75°C (conductivity ~20,000 µS/cm)
    ///   3. Intermediate Rinse (10 min) - Water until CIT < 200 µS/cm
    ///   4. Acid Wash       (15 min) - 1% phosphoric @ 60°C (conductivity ~8,000 µS/cm)
    ///   5. Final Rinse     (10 min) - Water until CIT < 100 µS/cm
    ///   6. Sanitize         (5 min) - 200 ppm peracetic acid
    ///   7. Drain            (5 min) - Gravity drain + air blow
    ///
    /// Verification is by temperature, time, and conductivity (end of each rinse).
    /// Real plants also take ATP swabs after CIP - not simulated here.
    /// </summary>
    public class Area700_CIPController : ISimulatable
    {
        private readonly PlantContext _ctx;

        public int Priority => 40;

        // Step configuration (duration in sim seconds, target temp, target conductivity)
        private static readonly (CipStep Step, double DurationSec, double TargetTempC, double TargetConductivityUs, string Description)[] StepConfig =
        {
            (CipStep.PreRinse,       600, 40.0,    500,  "Pre-rinse with water @ 40°C"),
            (CipStep.CausticWash,   1200, 75.0,  20000,  "Caustic wash 2% NaOH @ 75°C"),
            (CipStep.IntRinse,       600, 40.0,    150,  "Intermediate rinse until CIT < 200 µS/cm"),
            (CipStep.AcidWash,       900, 60.0,   8000,  "Acid wash 1% phosphoric @ 60°C"),
            (CipStep.FinalRinse,     600, 25.0,     80,  "Final rinse until CIT < 100 µS/cm"),
            (CipStep.Sanitize,       300, 25.0,    400,  "Sanitize 200 ppm peracetic acid"),
            (CipStep.Drain,          300, 20.0,      0,  "Drain and air blow")
        };

        private enum CipStep
        {
            Idle = 0,
            PreRinse = 1,
            CausticWash = 2,
            IntRinse = 3,
            AcidWash = 4,
            FinalRinse = 5,
            Sanitize = 6,
            Drain = 7
        }

        private CipStep _currentStep = CipStep.Idle;
        private int _currentStepIndex = -1;
        private double _stepTimerSec;
        private double _currentTempC = 22.0;
        private double _currentConductivityUs;
        private double _currentFlowLpm;
        private double _currentPumpAmps;
        private double _supplyValvePos;
        private double _returnValvePos;
        private bool _pumpRunning;

        // Condition monitoring for the CIP supply pump
        private readonly ConditionMonitor _cmCipPump;
        private DateTime _nextCipStartTime;
        private bool _initialized;

        // Time between CIP cycles in sim seconds (demo: 45 min; realistic: 8-12 hr)
        private const double IntervalBetweenCyclesSec = 2700;

        public Area700_CIPController(PlantContext ctx)
        {
            _ctx = ctx;
            _cmCipPump = new ConditionMonitor(ctx){ BaseVibration = 1.7, BearingRise = 35 }
                .Identify("EQ-MUC-CIP-P701", "CIP Pump P-701", "CIP", "CIP_P701_VIB");
            _cmCipPump.SeedHours(150);
        }

        public void Tick(TimeSpan dt, DateTime simNow)
        {
            if (!_initialized)
            {
                _nextCipStartTime = simNow.AddMinutes(10); // first CIP starts 10 min in
                _initialized = true;
            }

            double dtSec = dt.TotalSeconds;

            // State machine
            if (_currentStep == CipStep.Idle)
            {
                if (simNow >= _nextCipStartTime)
                {
                    StartCipCycle(simNow);
                }
                else
                {
                    // Idle state: pump off, valves closed, temperature drifting to ambient
                    _currentTempC += (22.0 - _currentTempC) * 0.02 * dtSec;
                    _currentConductivityUs *= Math.Exp(-0.05 * dtSec); // decay
                    _currentFlowLpm = 0;
                    _currentPumpAmps = 0;
                    _pumpRunning = false;
                    _supplyValvePos = 0;
                    _returnValvePos = 0;
                }
            }
            else
            {
                // Run the active step
                _stepTimerSec += dtSec;
                var (_, duration, targetTemp, targetCond, _) = StepConfig[_currentStepIndex];

                // Simulate temperature ramping toward target
                double tempError = targetTemp - _currentTempC;
                _currentTempC += tempError * 0.01 * dtSec;

                // Simulate conductivity. For caustic/acid steps, rises toward target.
                // For rinse steps, decays toward target (water rinsing solution away).
                bool isRinse = _currentStep == CipStep.PreRinse ||
                               _currentStep == CipStep.IntRinse ||
                               _currentStep == CipStep.FinalRinse;
                if (isRinse)
                {
                    // Exponential decay toward target
                    _currentConductivityUs += (targetCond - _currentConductivityUs) * 0.008 * dtSec;
                }
                else if (_currentStep != CipStep.Drain)
                {
                    // Ramp up to target (solution dosing)
                    _currentConductivityUs += (targetCond - _currentConductivityUs) * 0.015 * dtSec;
                }
                else
                {
                    // Draining - conductivity drops quickly
                    _currentConductivityUs *= Math.Exp(-0.2 * dtSec);
                }

                // Flow and pump state
                _currentFlowLpm = _currentStep == CipStep.Drain ? 20 : 120;
                _currentFlowLpm = _ctx.AddNoise(_currentFlowLpm, 2);
                _currentPumpAmps = _currentStep == CipStep.Drain ? 3 : 15;
                _currentPumpAmps = _ctx.AddNoise(_currentPumpAmps, 0.3);
                _pumpRunning = true;
                _supplyValvePos = _currentStep == CipStep.Drain ? 0 : 100;
                _returnValvePos = 100;

                // Check for step completion
                if (_stepTimerSec >= duration)
                {
                    AdvanceStep(simNow);
                }
            }

            // Publish Area 700 tags
            var t = _ctx.Tags;
            t.Write("CIP_TIT_701", Math.Round(_ctx.AddNoise(_currentTempC, 0.2), 1));
            t.Write("CIP_FIT_701", Math.Round(_currentFlowLpm, 1));
            t.Write("CIP_CIT_701", Math.Round(_currentConductivityUs, 0));
            t.Write("CIP_PIT_701", Math.Round(_pumpRunning ? _ctx.AddNoise(3.5, 0.05) : 0.0, 2));
            t.Write("CIP_V701_POS", Math.Round(_supplyValvePos, 0));
            t.Write("CIP_V702_POS", Math.Round(_returnValvePos, 0));
            t.Write("CIP_P701_RUN", _pumpRunning);
            t.Write("CIP_P701_AMPS", Math.Round(_currentPumpAmps, 1));
            _cmCipPump.Update(_pumpRunning, 0.7, false, dt.TotalSeconds);
            t.Write("CIP_P701_FAULT", false);
            _cmCipPump.Publish(t, vibration: "CIP_P701_VIB", bearingTemp: "CIP_P701_BRGTEMP",
                mcsa: "CIP_P701_MCSA", failureCount: "CIP_P701_FAILCNT");
            t.Write("CIP_STEP", (int)_currentStep);
            t.Write("CIP_TIMER", Math.Round(_stepTimerSec, 0));
            t.Write("CIP_PHASE", _currentStep == CipStep.Idle ? "Idle" : StepConfig[_currentStepIndex].Description);
            t.Write("CIP_ACTIVE", _currentStep != CipStep.Idle);
        }

        private void StartCipCycle(DateTime simNow)
        {
            _currentStepIndex = 0;
            _currentStep = StepConfig[0].Step;
            _stepTimerSec = 0;
            Console.WriteLine($"[{simNow:HH:mm:ss}] Area 700 CIP: CYCLE START -> {_currentStep} ({StepConfig[0].Description})");
        }

        private void AdvanceStep(DateTime simNow)
        {
            _currentStepIndex++;
            _stepTimerSec = 0;

            if (_currentStepIndex >= StepConfig.Length)
            {
                // Cycle complete
                Console.WriteLine($"[{simNow:HH:mm:ss}] Area 700 CIP: CYCLE COMPLETE, next cycle at {simNow.AddSeconds(IntervalBetweenCyclesSec):HH:mm:ss}");
                _currentStep = CipStep.Idle;
                _currentStepIndex = -1;
                _nextCipStartTime = simNow.AddSeconds(IntervalBetweenCyclesSec);
            }
            else
            {
                _currentStep = StepConfig[_currentStepIndex].Step;
                Console.WriteLine($"[{simNow:HH:mm:ss}] Area 700 CIP: -> {_currentStep} ({StepConfig[_currentStepIndex].Description})");
            }
        }
    }
}
