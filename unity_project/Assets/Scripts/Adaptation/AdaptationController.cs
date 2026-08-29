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
    /// Threshold values (T_confusion, T_frustration) and dwell-time (D) are set from
    /// the DAiSEE calibration results (PROJECT_PLAN.md Section 6). They start as
    /// placeholder values and are replaced with calibrated values after M3.
    ///
    /// Adaptation cooldown prevents re-triggering too rapidly on the same step.
    /// </summary>
    public class AdaptationController : MonoBehaviour
    {
        // ── Inspector Config ────────────────────────────────────────────────────
        [Header("Calibrated Thresholds (from DAiSEE calibration — M3)")]
        [Tooltip("Confusion score threshold T. Score must exceed this to contribute to dwell timer.")]
        [Range(0f, 1f)]
        [SerializeField] private float confusionThreshold  = 0.60f;  // placeholder — replace after M3

        [Tooltip("Frustration score threshold T.")]
        [Range(0f, 1f)]
        [SerializeField] private float frustrationThreshold = 0.65f; // placeholder — replace after M3

        [Tooltip("Dwell time D (seconds). Score must exceed threshold for this long to fire.")]
        [SerializeField] private float dwellTimeSeconds = 3.0f;      // placeholder — replace after M3

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
        private float  _dwellTimer;           // seconds score has been above threshold
        private bool   _inElevatedState;      // true when currently above threshold
        private bool   _fsmFired;            // true once FSM has fired this dwell run

        // ── Private State — Cooldown ─────────────────────────────────────────────
        private float  _lastAdaptationTime = -999f;
        private int    _adaptationsThisStep;
        private string _lastStepId = "";

        // ── Private State — Adaptation Sequence ──────────────────────────────────
        // Sequence of adaptation actions to apply in order within a single step
        private static readonly AdaptationAction[] ActionSequence =
        {
            AdaptationAction.InjectCue,     // 1st: visual cue (least intrusive)
            AdaptationAction.InsertHint,    // 2nd: hint text
            AdaptationAction.SlowPace,      // 3rd: slow down pacing
            AdaptationAction.SimplifyStep,  // 4th: full step breakdown (most intrusive)
        };

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Update()
        {
            // FSM is driven by UpdateAffectScores() called from AffectScorer,
            // not from Update() directly. Update() handles timer decay only.
        }

        // ── Public API — called from AffectScorer every 500ms ────────────────────
        /// <summary>
        /// Feed the latest affect scores into the FSM.
        /// Called by AffectScorer.cs every ~500ms with the latest computed scores.
        /// </summary>
        public void UpdateAffectScores(float confusionScore, float frustrationScore,
                                       float engagementScore, string currentStepId)
        {
            // Reset step-level adaptation counter when step changes
            if (currentStepId != _lastStepId)
            {
                ResetFSMState();
                _adaptationsThisStep = 0;
                _lastStepId = currentStepId;
            }

            // Check if either score exceeds its threshold
            bool aboveThreshold = confusionScore   > confusionThreshold
                                || frustrationScore > frustrationThreshold;

            if (aboveThreshold)
            {
                if (!_inElevatedState)
                {
                    // Entering elevated state — start dwell timer
                    _inElevatedState = true;
                    _dwellTimer      = 0f;
                    _fsmFired        = false;
                    Debug.Log($"[AdaptationController] Entering elevated affect state. " +
                              $"confusion={confusionScore:F3}, frustration={frustrationScore:F3}");
                }

                _dwellTimer += Time.deltaTime;  // accumulate time above threshold

                // Check if dwell time exceeded AND cooldown passed AND max adaptations not reached
                if (!_fsmFired
                    && _dwellTimer >= dwellTimeSeconds
                    && (Time.time - _lastAdaptationTime) >= cooldownSeconds
                    && _adaptationsThisStep < maxAdaptationsPerStep)
                {
                    FireAdaptation(confusionScore, frustrationScore, currentStepId);
                }
            }
            else
            {
                // Below threshold — reset dwell state
                if (_inElevatedState)
                {
                    Debug.Log($"[AdaptationController] Exiting elevated affect state after {_dwellTimer:F1}s.");
                }
                _inElevatedState = false;
                _dwellTimer      = 0f;
                _fsmFired        = false;
            }
        }

        // ── Private Helpers ──────────────────────────────────────────────────────
        private void FireAdaptation(float confusionScore, float frustrationScore, string stepId)
        {
            // Pick the next action in the escalating sequence
            AdaptationAction action = ActionSequence[
                Mathf.Min(_adaptationsThisStep, ActionSequence.Length - 1)];

            var evt = new AdaptationEvent(action, stepId, confusionScore, frustrationScore);

            Debug.Log($"[AdaptationController] FSM FIRED: {evt}");

            // Apply to content manager
            contentManager?.ApplyAdaptation(action);

            // Record state
            _fsmFired            = true;
            _lastAdaptationTime  = Time.time;
            _adaptationsThisStep++;

            // Notify subscribers (SessionLogger)
            OnAdaptationTriggered?.Invoke(evt);
        }

        private void ResetFSMState()
        {
            _inElevatedState = false;
            _dwellTimer      = 0f;
            _fsmFired        = false;
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
