using System;
using System.Collections;
using UnityEngine;
using A3R.Adaptation;
using A3R.ContentManager;

namespace A3R.Affect
{
    /// <summary>
    /// AffectScorer — Module 2 (orchestrator) of the A³R system.
    ///
    /// Runs the affect inference pipeline every ~500ms:
    ///   1. Request facial landmarks from FaceLandmarkBridge (front camera)
    ///   2. Extract geometric features via AffectFeatureExtractor
    ///   3. Compute confusion/frustration/engagement scores using the fixed rule-based formula
    ///   4. Push scores to AdaptationController FSM
    ///   5. Update the debug HUD via ARContentManager
    ///   6. Emit AffectSample for SessionLogger
    ///
    /// Scoring formula (from PROJECT_PLAN.md Section 6, matches score_affect_rulebased.py):
    ///   confusion   = 0.40*brow_furrow + 0.30*(1-EAR) + 0.30*mouth_tension
    ///   frustration = 0.35*brow_furrow + 0.25*(1-EAR) + 0.40*mouth_downturn
    ///   engagement  = 0.50*EAR + 0.30*(1-brow_furrow) + 0.20*brow_height
    /// </summary>
    public class AffectScorer : MonoBehaviour
    {
        // ── Inspector Config ────────────────────────────────────────────────────
        [Header("Inference Rate")]
        [Tooltip("Seconds between affect score updates. Default 0.5s = 2Hz.")]
        [SerializeField] private float updateIntervalSeconds = 0.5f;

        [Header("References")]
        [SerializeField] private FaceLandmarkBridge faceLandmarkBridge;
        [SerializeField] private AdaptationController adaptationController;
        [SerializeField] private ARContentManager contentManager;

        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>Fired every update interval with the latest affect scores.</summary>
        public event Action<AffectSample> OnAffectSampleReady;

        // ── Public Read-Only State ────────────────────────────────────────────────
        public float ConfusionScore    { get; private set; }
        public float FrustrationScore  { get; private set; }
        public float EngagementScore   { get; private set; }
        public bool  FaceDetected      { get; private set; }
        public AffectFeatureExtractor.FaceFeatures LastFeatures { get; private set; }

        // ── Scoring Weights (locked — must match score_affect_rulebased.py) ──────
        private const float W_CONFUSION_BROW   = 0.40f;
        private const float W_CONFUSION_EYE    = 0.30f;
        private const float W_CONFUSION_MOUTH  = 0.30f;

        private const float W_FRUSTRATION_BROW  = 0.35f;
        private const float W_FRUSTRATION_EYE   = 0.25f;
        private const float W_FRUSTRATION_MOUTH = 0.40f;

        private const float W_ENGAGEMENT_EAR   = 0.50f;
        private const float W_ENGAGEMENT_BROW  = 0.30f;
        private const float W_ENGAGEMENT_HEIGHT = 0.20f;

        // ── Private State ────────────────────────────────────────────────────────
        private Coroutine _scoringCoroutine;
        private string    _currentStepId = "";

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            if (faceLandmarkBridge == null)
                Debug.LogError("[AffectScorer] FaceLandmarkBridge reference not set!");
            if (adaptationController == null)
                Debug.LogWarning("[AffectScorer] AdaptationController not set — scores will not drive adaptation.");

            _scoringCoroutine = StartCoroutine(ScoringLoop());
        }

        private void OnDisable()
        {
            if (_scoringCoroutine != null)
                StopCoroutine(_scoringCoroutine);
        }

        // ── Public API ───────────────────────────────────────────────────────────
        /// <summary>Update the current step ID (called by ARContentManager on step change).</summary>
        public void SetCurrentStepId(string stepId) => _currentStepId = stepId;

        // ── Coroutine ────────────────────────────────────────────────────────────
        private IEnumerator ScoringLoop()
        {
            var interval = new WaitForSeconds(updateIntervalSeconds);
            while (true)
            {
                yield return interval;
                ProcessFrame();
            }
        }

        private void ProcessFrame()
        {
            if (faceLandmarkBridge == null) return;

            float[] landmarks = faceLandmarkBridge.GetCurrentLandmarks();
            if (landmarks == null || landmarks.Length == 0)
            {
                FaceDetected = false;
                return;
            }

            var features = AffectFeatureExtractor.Extract(landmarks);
            LastFeatures = features;
            FaceDetected = features.Valid;

            if (!features.Valid) return;

            // ── Compute Scores ───────────────────────────────────────────────────
            float confusion = Mathf.Clamp01(
                W_CONFUSION_BROW  * features.BrowFurrowNorm
              + W_CONFUSION_EYE   * (1f - features.EarNorm)
              + W_CONFUSION_MOUTH * features.MouthTensionNorm);

            float frustration = Mathf.Clamp01(
                W_FRUSTRATION_BROW  * features.BrowFurrowNorm
              + W_FRUSTRATION_EYE   * (1f - features.EarNorm)
              + W_FRUSTRATION_MOUTH * features.MouthTensionNorm);

            float engagement = Mathf.Clamp01(
                W_ENGAGEMENT_EAR    * features.EarNorm
              + W_ENGAGEMENT_BROW   * (1f - features.BrowFurrowNorm)
              + W_ENGAGEMENT_HEIGHT * features.BrowHeightNorm);

            ConfusionScore   = confusion;
            FrustrationScore = frustration;
            EngagementScore  = engagement;

            // ── Push to downstream systems ────────────────────────────────────────
            adaptationController?.UpdateAffectScores(
                confusion, frustration, engagement, _currentStepId);

            contentManager?.UpdateDebugHUD(
                confusion, frustration, engagement,
                markerTracked: true);  // tracker status injected separately

            var sample = new AffectSample
            {
                Timestamp        = DateTime.UtcNow,
                ConfusionScore   = confusion,
                FrustrationScore = frustration,
                EngagementScore  = engagement,
                BrowFurrowNorm   = features.BrowFurrowNorm,
                EarNorm          = features.EarNorm,
                MouthTensionNorm = features.MouthTensionNorm,
                BrowHeightNorm   = features.BrowHeightNorm,
                StepId           = _currentStepId,
            };

            OnAffectSampleReady?.Invoke(sample);
        }
    }

    /// <summary>
    /// AffectSample — Single timestamped snapshot of all affect scores and raw features.
    /// Used by SessionLogger for the affect trajectory log.
    /// </summary>
    public class AffectSample
    {
        public DateTime Timestamp        { get; set; }
        public float    ConfusionScore   { get; set; }
        public float    FrustrationScore { get; set; }
        public float    EngagementScore  { get; set; }
        public float    BrowFurrowNorm   { get; set; }
        public float    EarNorm          { get; set; }
        public float    MouthTensionNorm { get; set; }
        public float    BrowHeightNorm   { get; set; }
        public string   StepId           { get; set; }
    }
}
