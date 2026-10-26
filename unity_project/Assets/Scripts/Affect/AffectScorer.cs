using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using A3R.Adaptation;
using A3R.ContentManager;
using Debug = UnityEngine.Debug;

namespace A3R.Affect
{
    /// <summary>
    /// AffectScorer — Module 2 (orchestrator) of the A³R system.
    ///
    /// Runs the affect inference pipeline on newest frames with bounded queuing:
    ///   1. Samples landmarks from FaceLandmarkBridge
    ///   2. Extracts geometric features via AffectFeatureExtractor (with min-max EAR normalization)
    ///   3. Computes confusion/frustration/engagement scores via fixed rule-based formula
    ///   4. Dispatches scores to AdaptationController and SessionLogger
    ///   5. Profiles inference latency and frame rates (render FPS, AI FPS, mean/p95 latency)
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
        public event Action<AffectSample> OnAffectSampleReady;

        // ── Public Read-Only State ────────────────────────────────────────────────
        public float ConfusionScore    { get; private set; }
        public float FrustrationScore  { get; private set; }
        public float EngagementScore   { get; private set; }
        public bool  FaceDetected      { get; private set; }
        public AffectFeatureExtractor.FaceFeatures LastFeatures { get; private set; }

        // ── Performance Metrics ──────────────────────────────────────────────────
        public float RenderFps               { get; private set; }
        public float InferenceFps            { get; private set; }
        public float MeanInferenceLatencyMs  { get; private set; }
        public float P95LatencyMs            { get; private set; }
        public int   DroppedFramesCount      { get; private set; }
        public int   QueueDepth              => _isProcessing ? 1 : 0;

        // ── Scoring Weights (locked — matches score_affect_rulebased.py) ─────────
        private const float W_CONFUSION_BROW    = 0.40f;
        private const float W_CONFUSION_EYE     = 0.30f;
        private const float W_CONFUSION_MOUTH   = 0.30f;

        private const float W_FRUSTRATION_BROW  = 0.35f;
        private const float W_FRUSTRATION_EYE   = 0.25f;
        private const float W_FRUSTRATION_MOUTH = 0.40f;

        private const float W_ENGAGEMENT_EAR    = 0.50f;
        private const float W_ENGAGEMENT_BROW   = 0.30f;
        private const float W_ENGAGEMENT_HEIGHT = 0.20f;

        // ── Private State ────────────────────────────────────────────────────────
        private Coroutine _scoringCoroutine;
        private string    _currentStepId = "";
        private bool      _isProcessing;
        private int       _processedFrames;
        private float     _lastInferenceFpsTime;
        private float     _lastBenchmarkLogTime;
        private int       _inferenceFrameCount;
        private float     _renderDeltaTime;
        private readonly List<float> _recentLatenciesMs = new(64);
        private readonly Stopwatch   _stopwatch = new();

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (faceLandmarkBridge == null)
                faceLandmarkBridge = GetComponent<FaceLandmarkBridge>() ?? FindFirstObjectByType<FaceLandmarkBridge>();
            if (adaptationController == null)
                adaptationController = GetComponent<AdaptationController>() ?? FindFirstObjectByType<AdaptationController>();
            if (contentManager == null)
                contentManager = GetComponent<ARContentManager>() ?? FindFirstObjectByType<ARContentManager>();
        }

        private void Start()
        {
            if (faceLandmarkBridge == null)
                Debug.LogError("[AffectScorer] FaceLandmarkBridge reference not set!");
            if (adaptationController == null)
                Debug.LogWarning("[AffectScorer] AdaptationController not set — scores will not drive adaptation.");

            _lastInferenceFpsTime = Time.time;
            _lastBenchmarkLogTime = Time.time;
            _scoringCoroutine = StartCoroutine(ScoringLoop());
        }

        private void Update()
        {
            // Smooth render frame rate tracking
            _renderDeltaTime += (Time.unscaledDeltaTime - _renderDeltaTime) * 0.1f;
            RenderFps = _renderDeltaTime > 1e-5f ? 1.0f / _renderDeltaTime : 60f;

            // AI / Inference FPS calculation
            if (Time.time - _lastInferenceFpsTime >= 1.0f)
            {
                InferenceFps = _inferenceFrameCount / (Time.time - _lastInferenceFpsTime);
                _inferenceFrameCount = 0;
                _lastInferenceFpsTime = Time.time;
            }

            // Periodic Phase 7 benchmark log (every 5.0 seconds)
            if (Time.time - _lastBenchmarkLogTime >= 5.0f)
            {
                _lastBenchmarkLogTime = Time.time;
                float predLat = contentManager?.AffectPredictor != null ? contentManager.AffectPredictor.MeanPredictionLatencyMs : 0f;
                float camFps = faceLandmarkBridge != null ? faceLandmarkBridge.CameraFps : 0f;
                float mpFps = faceLandmarkBridge != null ? faceLandmarkBridge.MediaPipeFps : 0f;
                float mpLat = faceLandmarkBridge != null ? faceLandmarkBridge.LastInferenceLatencyMs : 0f;
                int mpDropped = faceLandmarkBridge != null ? faceLandmarkBridge.DroppedInferenceFrames : 0;
                float heapMb = (float)System.GC.GetTotalMemory(false) / (1024f * 1024f);

                Debug.Log($"[A3R_BENCHMARK] t={Time.time:F1}s | RenderFps={RenderFps:F1} | CamFps={camFps:F1} | MpFps={mpFps:F1} | " +
                          $"MpLat={mpLat:F1}ms | ScorerMeanLat={MeanInferenceLatencyMs:F2}ms | ScorerP95Lat={P95LatencyMs:F2}ms | " +
                          $"PredLat={predLat:F3}ms | DropInf={mpDropped} | DropScore={DroppedFramesCount} | " +
                          $"Face={FaceDetected} | HeapMB={heapMb:F2}");
            }
        }

        private void OnDisable()
        {
            if (_scoringCoroutine != null)
                StopCoroutine(_scoringCoroutine);
        }

        // ── Public API ───────────────────────────────────────────────────────────
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
            // Bounded queue: skip processing if already active on previous frame (prefer recent frames)
            if (_isProcessing)
            {
                DroppedFramesCount++;
                return;
            }

            if (faceLandmarkBridge == null) return;

            _isProcessing = true;
            _stopwatch.Restart();

            bool isTracked = false;
            var guidance = UnityEngine.Object.FindFirstObjectByType<A3R.ARGuidance.ARGuidanceController>();
            if (guidance != null) isTracked = guidance.IsTracking;

            try
            {
                float[] landmarks = faceLandmarkBridge.GetCurrentLandmarks();
                if (landmarks == null || landmarks.Length == 0)
                {
                    FaceDetected = false;
                    ConfusionScore = 0f;
                    FrustrationScore = 0f;

                    adaptationController?.UpdateAffectScores(0f, 0f, 0f, _currentStepId);
                    contentManager?.UpdateDebugHUD(0f, 0f, 0f, markerTracked: isTracked);
                    return;
                }

                var features = AffectFeatureExtractor.Extract(landmarks);
                LastFeatures = features;
                FaceDetected = features.Valid;

                if (!features.Valid)
                {
                    ConfusionScore = 0f;
                    FrustrationScore = 0f;

                    adaptationController?.UpdateAffectScores(0f, 0f, 0f, _currentStepId);
                    contentManager?.UpdateDebugHUD(0f, 0f, 0f, markerTracked: isTracked);
                    return;
                }

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
                _inferenceFrameCount++;
                _processedFrames++;

                if (_processedFrames == 1 || _processedFrames % 15 == 0)
                {
                    float calm = Mathf.Clamp01(1f - Mathf.Max(confusion, frustration));
                    Debug.Log($"[A3R_DIAG_AFFECT] Valid:{features.Valid} " +
                              $"| Yaw:{features.YawRatio:F3} Pitch:{features.PitchRatio:F3} " +
                              $"| EarRaw:{features.EarRaw:F3} EarNorm:{features.EarNorm:F3} " +
                              $"| BrowRaw:{features.BrowFurrowRaw:F3} BrowNorm:{features.BrowFurrowNorm:F3} " +
                              $"| MouthRaw:{features.MouthTensionRaw:F4} MouthNorm:{features.MouthTensionNorm:F3} " +
                              $"| Conf:{confusion:F3} Frust:{frustration:F3} Calm:{calm:F3}");
                }

                // ── Push to downstream systems ────────────────────────────────────────
                adaptationController?.UpdateAffectScores(
                    confusion, frustration, engagement, _currentStepId);

                contentManager?.UpdateDebugHUD(
                    confusion, frustration, engagement,
                    markerTracked: isTracked);

                var sample = new AffectSample
                {
                    Timestamp        = DateTime.UtcNow,
                    ConfusionScore   = confusion,
                    FrustrationScore = frustration,
                    EngagementScore  = engagement,
                    BrowFurrowNorm   = features.BrowFurrowNorm,
                    EarRaw           = features.EarRaw,
                    EarNorm          = features.EarNorm,
                    MouthTensionNorm = features.MouthTensionNorm,
                    BrowHeightNorm   = features.BrowHeightNorm,
                    StepId           = _currentStepId,
                    IsSimulated      = faceLandmarkBridge.IsSimulated
                };

                OnAffectSampleReady?.Invoke(sample);
            }
            finally
            {
                _stopwatch.Stop();
                float elapsedMs = (float)_stopwatch.Elapsed.TotalMilliseconds;
                RecordLatency(elapsedMs);
                _isProcessing = false;
            }
        }

        private void RecordLatency(float ms)
        {
            if (_recentLatenciesMs.Count >= 60)
                _recentLatenciesMs.RemoveAt(0);
            _recentLatenciesMs.Add(ms);

            // Compute mean and p95
            float sum = 0f;
            for (int i = 0; i < _recentLatenciesMs.Count; i++) sum += _recentLatenciesMs[i];
            MeanInferenceLatencyMs = sum / _recentLatenciesMs.Count;

            var sorted = new List<float>(_recentLatenciesMs);
            sorted.Sort();
            int p95Idx = Mathf.Clamp(Mathf.FloorToInt(sorted.Count * 0.95f), 0, sorted.Count - 1);
            P95LatencyMs = sorted[p95Idx];
        }
    }

    /// <summary>
    /// AffectSample — Single timestamped snapshot of all affect scores and raw features.
    /// </summary>
    public class AffectSample
    {
        public DateTime Timestamp        { get; set; }
        public float    ConfusionScore   { get; set; }
        public float    FrustrationScore { get; set; }
        public float    EngagementScore  { get; set; }
        public float    BrowFurrowNorm   { get; set; }
        public float    EarRaw           { get; set; }
        public float    EarNorm          { get; set; }
        public float    MouthTensionNorm { get; set; }
        public float    BrowHeightNorm   { get; set; }
        public string   StepId           { get; set; }
        public bool     IsSimulated      { get; set; }
    }
}
