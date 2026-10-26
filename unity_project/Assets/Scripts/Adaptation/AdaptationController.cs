using System;
using UnityEngine;
using A3R.ContentManager;

namespace A3R.Adaptation
{
    /// <summary>
    /// AdaptationController — Module 3 of the A³R system.
    ///
    /// Finite-state machine (FSM) that monitors affect scores from AffectScorer.
    /// When confusion or frustration exceeds the calibrated threshold T for a
    /// sustained dwell-time D, emits an AdaptationEvent and calls ARContentManager
    /// to mutate the displayed AR content.
    ///
    /// Threshold values (T_confusion = 0.60, T_frustration = 0.65) and dwell-time (D = 3.0s) are
    /// frozen runtime thresholds retained during A³R affect/adaptation validation. DAiSEE was
    /// evaluated as an external benchmark but did not provide sufficient task-specific evidence
    /// to replace these thresholds.
    ///
    /// Adaptation cooldown prevents re-triggering too rapidly on the same step.
    /// </summary>
    public class AdaptationController : MonoBehaviour
    {
        // ── Inspector Config ────────────────────────────────────────────────────
        [Header("Frozen Runtime Thresholds (Retained Baseline)")]
        [Tooltip("Confusion score threshold T. Score must exceed this to contribute to dwell timer.")]
        [Range(0f, 1f)]
        [SerializeField] private float confusionThreshold  = 0.40f;  // Frozen runtime threshold retained during validation

        [Tooltip("Frustration score threshold T.")]
        [Range(0f, 1f)]
        [SerializeField] private float frustrationThreshold = 0.65f; // Frozen runtime threshold retained during validation

        [Tooltip("Dwell time D (seconds). Score must exceed threshold for this long to fire.")]
        [SerializeField] private float dwellTimeSeconds = 2.0f;      // Adjusted for Review 2 demo

        [Header("Adaptation Cooldown")]
        [Tooltip("Minimum seconds between two adaptation events on the same step.")]
        [SerializeField] private float cooldownSeconds = 15.0f;

        [Tooltip("Maximum number of adaptations per step before giving up.")]
        [SerializeField] private int maxAdaptationsPerStep = 3;

        [Header("References")]
        [SerializeField] private ARContentManager contentManager;

        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>Fired whenever an adaptation event is triggered. Subscribe in SessionLogger.</summary>
        public event Action<AdaptationEvent> OnAdaptationTriggered;

        // ── Private State — FSM ──────────────────────────────────────────────────
        private float  _dwellTimer;           // actual elapsed seconds score has been above threshold
        private float  _elevatedStartTime;   // timestamp (Time.time) when score crossed threshold
        private bool   _inElevatedState;      // true when currently above threshold
        private bool   _fsmFired;            // true once FSM has fired this dwell run

        // ── Private State — Cooldown ─────────────────────────────────────────────
        private float  _lastAdaptationTime = -999f;
        private int    _adaptationsThisStep;
        private string _lastStepId = "";

        // ── Private State — Adaptation Sequences ─────────────────────────────────
        // Sequence of adaptation actions for confusion struggle (least to most intrusive)
        private static readonly AdaptationAction[] ConfusionSequence =
        {
            AdaptationAction.InjectCue,
            AdaptationAction.InjectCue,
            AdaptationAction.InjectCue,
            AdaptationAction.InjectCue,
        };

        // Sequence of adaptation actions for frustration disengagement
        private static readonly AdaptationAction[] FrustrationSequence =
        {
            AdaptationAction.InjectCue,
            AdaptationAction.InjectCue,
        };

        // Retained for backward compatibility
        private static readonly AdaptationAction[] ActionSequence = ConfusionSequence;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (contentManager == null)
                contentManager = GetComponent<ARContentManager>() ?? FindFirstObjectByType<ARContentManager>();
        }

        private void Update()
        {
            // Continuously update dwell timer display for debug HUD while elevated
            if (_inElevatedState && !_fsmFired)
            {
                _dwellTimer = Time.time - _elevatedStartTime;
            }
        }

        public int SuppressedCooldownCount { get; private set; }
        public int SuppressedMaxLimitCount { get; private set; }

        // ── Public API — called from AffectScorer every ~500ms ───────────────────
        /// <summary>
        /// Feed the latest affect scores into the FSM.
        /// Called by AffectScorer.cs every ~500ms with the latest computed scores.
        /// </summary>
        public void UpdateAffectScores(float confusionScore, float frustrationScore,
                                       float engagementScore, string currentStepId)
        {
            if (float.IsNaN(confusionScore) || float.IsNaN(frustrationScore) ||
                float.IsInfinity(confusionScore) || float.IsInfinity(frustrationScore))
                return;

            // ONLY ALLOW ADAPTATION IF ACTIVELY IN A PLACEMENT STEP
            var oldTutorial = UnityEngine.Object.FindFirstObjectByType<A3R.ARGuidance.A3R_ResistorTutorialController>();
            if (oldTutorial == null) return;
            
            var state = oldTutorial.CurrentState;
            currentStepId = state.ToString(); // Override to ensure accurate per-step cooldown resets
            
            if (state != A3R.ARGuidance.ResistorTutorialState.RESISTOR_TARGET &&
                state != A3R.ARGuidance.ResistorTutorialState.LED_TARGET &&
                state != A3R.ARGuidance.ResistorTutorialState.ESP32_TARGET &&
                state != A3R.ARGuidance.ResistorTutorialState.WIRE1_TARGET &&
                state != A3R.ARGuidance.ResistorTutorialState.WIRE2_TARGET &&
                state != A3R.ARGuidance.ResistorTutorialState.WIRE3_TARGET)
            {
                if (_inElevatedState) ResetFSMState();
                return; // Stop processing affect scores if not armed
            }

            // Reset step-level adaptation counter and cooldown when step changes
            if (currentStepId != _lastStepId)
            {
                ResetFSMState();
                _adaptationsThisStep = 0;
                _lastAdaptationTime = -999f; // FIX: Prevent previous step's cooldown from suppressing the new step
                _lastStepId = currentStepId;
            }

            // Check if either score exceeds its threshold
            bool aboveThreshold = confusionScore   > confusionThreshold
                                || frustrationScore > frustrationThreshold;

            if (aboveThreshold)
            {
                if (!_inElevatedState)
                {
                    // Entering elevated state — start dwell timer using actual time
                    _inElevatedState   = true;
                    _elevatedStartTime = Time.time;
                    _dwellTimer        = 0f;
                    _fsmFired          = false;
                    Debug.Log($"[AdaptationController] Entering elevated affect state at {Time.time:F2}s. " +
                              $"confusion={confusionScore:F3}, frustration={frustrationScore:F3}");
                }
                else
                {
                    // Update actual elapsed dwell duration
                    _dwellTimer = Time.time - _elevatedStartTime;
                }

                // Check if dwell time exceeded AND cooldown passed AND max adaptations not reached
                if (!_fsmFired && _dwellTimer >= dwellTimeSeconds)
                {
                    if ((Time.time - _lastAdaptationTime) < cooldownSeconds)
                    {
                        SuppressedCooldownCount++;
                    }
                    else if (_adaptationsThisStep >= maxAdaptationsPerStep)
                    {
                        SuppressedMaxLimitCount++;
                    }
                    else
                    {
                        FireAdaptation(confusionScore, frustrationScore, currentStepId);
                    }
                }
            }
            else
            {
                // Below threshold — reset dwell state
                if (_inElevatedState)
                {
                    Debug.Log($"[AdaptationController] Exiting elevated affect state after {_dwellTimer:F2}s (recovered).");
                }
                _inElevatedState   = false;
                _elevatedStartTime = 0f;
                _dwellTimer        = 0f;
                _fsmFired          = false;
            }
        }

        // ── Private Helpers ──────────────────────────────────────────────────────
        private void FireAdaptation(float confusionScore, float frustrationScore, string stepId)
        {
            string behavior = (frustrationScore > confusionScore && frustrationScore > frustrationThreshold)
                ? "FrustrationDisengage"
                : "ConfusionStruggle";

            // Pick the next action according to the triggering behavior's escalation sequence
            AdaptationAction action;
            if (behavior == "FrustrationDisengage")
            {
                int index = Mathf.Min(_adaptationsThisStep, FrustrationSequence.Length - 1);
                action = FrustrationSequence[index];
            }
            else
            {
                int index = Mathf.Min(_adaptationsThisStep, ConfusionSequence.Length - 1);
                action = ConfusionSequence[index];
            }

            var evt = new AdaptationEvent(action, stepId, confusionScore, frustrationScore, contentManager?.CurrentCanonicalEvent);

            Debug.Log($"[AdaptationController] FSM FIRED: {evt} (behavior={behavior})");

            // Apply to content manager
            contentManager?.ApplyAdaptation(action, behavior);

            // Record state
            _fsmFired            = true;
            _lastAdaptationTime  = Time.time;
            _adaptationsThisStep++;

            // Notify subscribers (SessionLogger)
            OnAdaptationTriggered?.Invoke(evt);
        }

        private void ResetFSMState()
        {
            _inElevatedState   = false;
            _elevatedStartTime = 0f;
            _dwellTimer        = 0f;
            _fsmFired          = false;
        }

        /// <summary>
        /// Resets all FSM, adaptation counters, and cooldown timers at session boundaries.
        /// </summary>
        public void ResetSession()
        {
            ResetFSMState();
            _adaptationsThisStep = 0;
            _lastStepId = "";
            _lastAdaptationTime = -999f;
            SuppressedCooldownCount = 0;
            SuppressedMaxLimitCount = 0;
        }

        // ── Public Accessors (for debug HUD) ─────────────────────────────────────
        public float DwellTimer            => _dwellTimer;
        public bool  IsInElevatedState     => _inElevatedState;
        public int   AdaptationsThisStep   => _adaptationsThisStep;
        public float ConfusionThreshold    => confusionThreshold;
        public float FrustrationThreshold  => frustrationThreshold;
        public float DwellTimeRequired     => dwellTimeSeconds;
    }
}
