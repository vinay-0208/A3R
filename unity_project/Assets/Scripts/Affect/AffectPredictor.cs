using System;
using System.Collections.Generic;
using UnityEngine;
using A3R.ContentManager;
using A3R.Logging;

namespace A3R.Affect
{
    /// <summary>
    /// AffectPredictor — Module 3: Anticipation & Deterministic Prediction Engine for the A³R system.
    ///
    /// Clearly separates:
    ///   - DETECTION: Event is already occurring (e.g. affect score exceeded threshold for >= 3s dwell).
    ///   - PREDICTION: Event is anticipated BEFORE it occurs, based on deterministic precursor dynamics:
    ///       * Linear slope of confusion trajectory (d(confusion)/dt >= 0.05/s)
    ///       * Brow furrow velocity (v_brow >= 0.10/s) & eye squinting (d(ear)/dt <= -0.05/s)
    ///       * Frustration onset slope & mouth tension velocity (v_mouth >= 0.08/s)
    ///       * Polarity hesitation (micro-squint / hesitation during LED insertion or competing affect co-activation)
    ///   - TECHNICAL DISRUPTION: Camera tracking loss (face missing >= 1.5s) or ESP32 disconnection (pending Phase 9).
    ///
    /// Measures lead time: lead_time = actual_event_timestamp - prediction_timestamp (valid strictly if lead_time > 0).
    /// Explicitly classifies every behavior as PREDICTIVE or DETECTION-ONLY per the research taxonomy.
    /// </summary>
    public class AffectPredictor : MonoBehaviour
    {
        public enum BehaviorPredictability
        {
            Predictive,
            DetectionOnly
        }

        [Serializable]
        public struct BehaviorClassification
        {
            public string behaviorName;
            public BehaviorPredictability predictability;
            public string precursorDescription;
            public string limitationNotes;
        }

        // ── Inspector Config ────────────────────────────────────────────────────
        [Header("Precursor Signal Thresholds")]
        [Tooltip("Minimum rate of confusion increase (per second) indicating onset of struggle.")]
        [SerializeField] private float confusionSlopeThreshold = 0.05f;

        [Tooltip("Minimum rate of brow furrow increase (per second) indicating onset of confusion.")]
        [SerializeField] private float browVelocityThreshold = 0.10f;

        [Tooltip("Minimum rate of EAR decrease (per second) indicating onset of squint/strain.")]
        [SerializeField] private float earVelocityThreshold = -0.05f;

        [Tooltip("Precursor confusion score floor (must be above this to consider rising struggle).")]
        [SerializeField] private float confusionPrecursorFloor = 0.35f;

        [Tooltip("Precursor confusion score ceiling (must be below actual detection threshold of 0.60).")]
        [SerializeField] private float confusionPrecursorCeiling = 0.55f;

        [Header("Frustration Precursor Thresholds")]
        [Tooltip("Minimum rate of frustration increase (per second) indicating disengagement risk.")]
        [SerializeField] private float frustrationSlopeThreshold = 0.05f;

        [Tooltip("Minimum rate of mouth tension increase (per second).")]
        [SerializeField] private float mouthVelocityThreshold = 0.08f;

        [Tooltip("Precursor frustration score floor.")]
        [SerializeField] private float frustrationPrecursorFloor = 0.35f;

        [Tooltip("Precursor frustration score ceiling (below actual detection threshold of 0.65).")]
        [SerializeField] private float frustrationPrecursorCeiling = 0.60f;

        [Header("Temporal Window & Cooldown")]
        [Tooltip("Number of samples in bounded rolling window.")]
        [SerializeField] private int rollingWindowCapacity = 5;

        [Tooltip("Minimum samples required before evaluating trends.")]
        [SerializeField] private int minSamplesForPrediction = 3;

        [Tooltip("Seconds to suppress repeated identical predictions.")]
        [SerializeField] private float predictionCooldownSeconds = 5.0f;

        [Tooltip("Duration of lost face tracking before triggering TrackingLossDisruption.")]
        [SerializeField] private float trackingLossDurationThreshold = 1.5f;

        [Tooltip("Score threshold below which precursor state recovers to normal.")]
        [SerializeField] private float recoveryScoreFloor = 0.30f;

        [Header("References")]
        [SerializeField] private AffectScorer affectScorer;
        [SerializeField] private SessionLogger sessionLogger;

        // ── Taxonomy of Behaviors ────────────────────────────────────────────────
        public static readonly BehaviorClassification[] BehaviorTaxonomy = new[]
        {
            new BehaviorClassification
            {
                behaviorName = "ConfusionStruggle",
                predictability = BehaviorPredictability.Predictive,
                precursorDescription = "Rising confusion slope (>= 0.05/s) or brow furrow acceleration with narrowing EAR before 3s dwell threshold.",
                limitationNotes = "Predictable with positive lead time before actual 3.0s dwell threshold crossing."
            },
            new BehaviorClassification
            {
                behaviorName = "FrustrationDisengage",
                predictability = BehaviorPredictability.Predictive,
                precursorDescription = "Accelerating mouth corner tension (>= 0.08/s) and rising frustration slope (>= 0.05/s).",
                limitationNotes = "Predictable prior to affective disengagement."
            },
            new BehaviorClassification
            {
                behaviorName = "PolarityHesitation",
                predictability = BehaviorPredictability.Predictive,
                precursorDescription = "Micro-squinting and brow furrow specifically during Step 3 (LED insertion) or competing affect co-activation.",
                limitationNotes = "Specific to high-error polarity step or affect oscillation."
            },
            new BehaviorClassification
            {
                behaviorName = "TrackingLossDisruption",
                predictability = BehaviorPredictability.DetectionOnly,
                precursorDescription = "None. Physical camera occlusion, head turn, or lighting loss.",
                limitationNotes = "Detection-only: facial geometry cannot predict sudden physical camera occlusion."
            },
            new BehaviorClassification
            {
                behaviorName = "HardwareDisconnection",
                predictability = BehaviorPredictability.DetectionOnly,
                precursorDescription = "None. Physical ESP32 connection state is external.",
                limitationNotes = "Detection-only: hardware electrical disconnection is external (pending Phase 9)."
            }
        };

        // ── Events & Public Structs ──────────────────────────────────────────────
        public event Action<PredictionEventData> OnPredictionGenerated;

        public struct PredictionEventData
        {
            public string Behavior;
            public float  EvidenceScore;
            public float  Confidence => EvidenceScore; // backwards compatibility
            public float  Timestamp;
            public string StepId;
            public float  ConfusionScore;
            public float  FrustrationScore;
            public float  TrendSlope;
            public bool   IsSimulated;
            public bool   IsTechnicalDisruption;
            public CanonicalEvent Context;
        }

        private struct TimedAffectRecord
        {
            public float time;
            public float confusion;
            public float frustration;
            public float engagement;
            public float browFurrow;
            public float ear;
            public float mouthTension;
        }

        // ── Private Tracking State ───────────────────────────────────────────────
        private readonly List<TimedAffectRecord> _history = new(8);
        private string _predictionState = "IDLE";
        public string PredictionState => _predictionState;

        private float  _lastPredictionTime = -999f;
        private string _lastPredictedBehavior = "";
        private bool   _hasPendingPrediction;
        public bool   HasPendingPrediction => _hasPendingPrediction;
        private float  _pendingPredictionTime;
        private string _pendingBehavior = "";
        private float  _pendingEvidence;

        private int    _precursorPersistenceCount;
        private string _activePrecursorCandidate = "";

        // Tracking Loss
        private float _trackingLossTimer;
        private bool  _trackingLossReported;

        // ── Verification Statistics ──────────────────────────────────────────────
        public int   TotalPredictions    { get; private set; }
        public int   CorrectPredictions  { get; private set; }
        public int   FalsePositives      { get; private set; }
        public int   FalseNegatives      { get; private set; }
        public float MeanLeadTimeSeconds { get; private set; }
        public float LastPredictionLatencyMs { get; private set; }
        public float MeanPredictionLatencyMs { get; private set; }
        private readonly List<float> _leadTimes = new();
        private readonly List<float> _recentPredictionLatenciesMs = new(64);
        private readonly System.Diagnostics.Stopwatch _predictionStopwatch = new();

        private void RecordPredictionLatency(float ms)
        {
            if (_recentPredictionLatenciesMs.Count >= 60)
                _recentPredictionLatenciesMs.RemoveAt(0);
            _recentPredictionLatenciesMs.Add(ms);

            float sum = 0f;
            for (int i = 0; i < _recentPredictionLatenciesMs.Count; i++) sum += _recentPredictionLatenciesMs[i];
            MeanPredictionLatencyMs = sum / _recentPredictionLatenciesMs.Count;
            LastPredictionLatencyMs = ms;
        }

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (affectScorer == null)
                affectScorer = GetComponent<AffectScorer>() ?? FindFirstObjectByType<AffectScorer>();
            if (sessionLogger == null)
                sessionLogger = GetComponent<SessionLogger>() ?? FindFirstObjectByType<SessionLogger>();
        }

        private void Start()
        {
            if (affectScorer != null)
                affectScorer.OnAffectSampleReady += HandleAffectSample;
        }

        private void OnDestroy()
        {
            if (affectScorer != null)
                affectScorer.OnAffectSampleReady -= HandleAffectSample;
        }

        private void Update()
        {
            // Technical disruption detection: monitor face tracking loss
            if (affectScorer != null)
            {
                if (!affectScorer.FaceDetected)
                {
                    _trackingLossTimer += Time.deltaTime;
                    if (_trackingLossTimer >= trackingLossDurationThreshold && !_trackingLossReported)
                    {
                        _trackingLossReported = true;
                        _predictionState = "TRACKING_LOST";
                        EmitTechnicalDisruption("TrackingLossDisruption");
                    }
                }
                else
                {
                    if (_trackingLossTimer > 0f)
                    {
                        _trackingLossTimer = 0f;
                        _trackingLossReported = false;
                        if (_predictionState == "TRACKING_LOST")
                            _predictionState = "IDLE";
                    }
                }
            }
        }

        // ── Precursor Evaluation Pipeline ────────────────────────────────────────
        private void HandleAffectSample(AffectSample sample)
        {
            _predictionStopwatch.Restart();
            try
            {
                if (float.IsNaN(sample.ConfusionScore) || float.IsNaN(sample.FrustrationScore))
                    return;

            float now = Time.time;

            // Maintain bounded rolling window
            _history.Add(new TimedAffectRecord
            {
                time         = now,
                confusion    = Mathf.Clamp01(sample.ConfusionScore),
                frustration  = Mathf.Clamp01(sample.FrustrationScore),
                engagement   = Mathf.Clamp01(sample.EngagementScore),
                browFurrow   = Mathf.Clamp01(sample.BrowFurrowNorm),
                ear          = Mathf.Clamp01(sample.EarNorm),
                mouthTension = Mathf.Clamp01(sample.MouthTensionNorm)
            });

            while (_history.Count > rollingWindowCapacity)
                _history.RemoveAt(0);

            if (_history.Count < minSamplesForPrediction)
                return;

            float dtWindow = _history[^1].time - _history[0].time;
            if (dtWindow < 0.4f)
                return;

            // Compute slopes via linear regression over window
            float slopeConf  = CalculateSlope(h => h.confusion);
            float slopeFrust = CalculateSlope(h => h.frustration);
            float slopeBrow  = CalculateSlope(h => h.browFurrow);
            float slopeEar   = CalculateSlope(h => h.ear);
            float slopeMouth = CalculateSlope(h => h.mouthTension);

            float currentC = _history[^1].confusion;
            float currentF = _history[^1].frustration;
            float currentE = _history[^1].engagement;

            // Recovery check
            if (currentC < recoveryScoreFloor && currentF < recoveryScoreFloor)
            {
                if (_predictionState is "PRECURSOR" or "PREDICTED")
                {
                    _predictionState = "RECOVERY";
                    _hasPendingPrediction = false;
                    _precursorPersistenceCount = 0;
                    _activePrecursorCandidate = "";
                }
                return;
            }

            // Precursor conditions
            bool isLedStep = !string.IsNullOrEmpty(sample.StepId) &&
                             (sample.StepId.Equals("insert_led", StringComparison.OrdinalIgnoreCase) ||
                              sample.StepId.Contains("led", StringComparison.OrdinalIgnoreCase));

            float stdConf = CalculateStdDev(h => h.confusion);

            // Condition A: ConfusionStruggle
            bool isConf = (currentC >= confusionPrecursorFloor && currentC <= confusionPrecursorCeiling) &&
                          (slopeConf >= confusionSlopeThreshold || (slopeBrow >= browVelocityThreshold && slopeEar <= earVelocityThreshold));

            // Condition B: FrustrationDisengage
            bool isFrust = (currentF >= frustrationPrecursorFloor && currentF <= frustrationPrecursorCeiling) &&
                           (slopeFrust >= frustrationSlopeThreshold || slopeMouth >= mouthVelocityThreshold);

            // Condition C: PolarityHesitation
            bool isPolarity = false;
            if (isLedStep && (currentC >= 0.30f && currentC <= 0.55f) && (slopeBrow >= 0.08f && slopeEar <= -0.04f))
            {
                isPolarity = true;
            }
            else if (!isConf && !isFrust && Mathf.Abs(currentC - currentE) <= 0.12f && currentC >= 0.35f && stdConf >= 0.05f && currentF < 0.35f)
            {
                isPolarity = true;
            }

            string candidate = null;
            float evidence = 0f;
            float trend = 0f;

            if (isPolarity)
            {
                candidate = "PolarityHesitation";
                evidence = Mathf.Clamp01(0.60f + 0.40f * (stdConf / 0.10f));
                trend = slopeConf;
            }
            else if (isConf)
            {
                candidate = "ConfusionStruggle";
                float normC = (currentC - confusionPrecursorFloor) / (confusionPrecursorCeiling - confusionPrecursorFloor);
                float normSlope = slopeConf / 0.20f;
                evidence = Mathf.Clamp01(0.50f * normC + 0.50f * normSlope);
                trend = slopeConf;
            }
            else if (isFrust)
            {
                candidate = "FrustrationDisengage";
                float normF = (currentF - frustrationPrecursorFloor) / (frustrationPrecursorCeiling - frustrationPrecursorFloor);
                float normSlope = slopeFrust / 0.20f;
                evidence = Mathf.Clamp01(0.50f * normF + 0.50f * normSlope);
                trend = slopeFrust;
            }

            // Persistence gating
            if (candidate != null)
            {
                if (candidate == _activePrecursorCandidate)
                    _precursorPersistenceCount++;
                else
                {
                    _activePrecursorCandidate = candidate;
                    _precursorPersistenceCount = 1;
                }

                _predictionState = "PRECURSOR";

                bool inCooldown = (now - _lastPredictionTime) < predictionCooldownSeconds;

                if (_precursorPersistenceCount >= 2)
                {
                    if (!inCooldown || candidate != _lastPredictedBehavior)
                    {
                        _predictionState = "PREDICTED";
                        _lastPredictionTime = now;
                        _lastPredictedBehavior = candidate;
                        _hasPendingPrediction = true;
                        _pendingPredictionTime = now;
                        _pendingBehavior = candidate;
                        _pendingEvidence = evidence;
                        TotalPredictions++;

                        var evtData = new PredictionEventData
                        {
                            Behavior              = candidate,
                            EvidenceScore         = (float)Math.Round(evidence, 4),
                            Timestamp             = now,
                            StepId                = sample.StepId,
                            ConfusionScore        = (float)Math.Round(currentC, 4),
                            FrustrationScore      = (float)Math.Round(currentF, 4),
                            TrendSlope            = (float)Math.Round(trend, 4),
                            IsSimulated           = sample.IsSimulated,
                            IsTechnicalDisruption = false
                        };

                        OnPredictionGenerated?.Invoke(evtData);
                        sessionLogger?.LogPrediction(
                            behavior: candidate,
                            evidenceScore: evidence,
                            confusion: currentC,
                            frustration: currentF,
                            trendSlope: trend,
                            stepId: sample.StepId,
                            predictionState: "PREDICTED",
                            isSimulated: sample.IsSimulated,
                            isTechnicalDisruption: false
                        );

                        Debug.Log($"[AffectPredictor] PREDICTION fired: {candidate} at t={now:F2}s (evidence={evidence:F2}, slope={trend:F2}/s)");
                    }
                }
            }
            else
            {
                _precursorPersistenceCount = 0;
                _activePrecursorCandidate = "";
                if (_predictionState == "PRECURSOR")
                    _predictionState = "IDLE";
            }
            }
            finally
            {
                _predictionStopwatch.Stop();
                RecordPredictionLatency((float)_predictionStopwatch.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// Public entrypoint for technical disruption notifications (e.g. HardwareDisconnection).
        /// </summary>
        public void NotifyTechnicalDisruption(string disruptionBehavior)
        {
            EmitTechnicalDisruption(disruptionBehavior);
        }

        private void EmitTechnicalDisruption(string disruptionBehavior)
        {
            var evtData = new PredictionEventData
            {
                Behavior              = disruptionBehavior,
                EvidenceScore         = 1.0f,
                Timestamp             = Time.time,
                StepId                = "",
                ConfusionScore        = 0f,
                FrustrationScore      = 0f,
                TrendSlope            = 0f,
                IsSimulated           = affectScorer != null && affectScorer.LastFeatures.Valid,
                IsTechnicalDisruption = true
            };

            OnPredictionGenerated?.Invoke(evtData);
            sessionLogger?.LogPrediction(
                behavior: disruptionBehavior,
                evidenceScore: 1.0f,
                confusion: 0f,
                frustration: 0f,
                trendSlope: 0f,
                stepId: "",
                predictionState: "TRACKING_LOST",
                isSimulated: false,
                isTechnicalDisruption: true
            );

            Debug.LogWarning($"[AffectPredictor] Technical disruption event: {disruptionBehavior} at t={Time.time:F2}s");
        }

        // ── Lead-Time Measurement ────────────────────────────────────────────────
        /// <summary>
        /// Called when an actual detection/adaptation event fires (e.g. from AdaptationController).
        /// Matches the actual event to any prior prediction to record true lead time.
        /// </summary>
        public void NotifyActualEventFired(string behavior, CanonicalEvent canonicalContext)
        {
            if (_hasPendingPrediction)
            {
                float leadTime = Time.time - _pendingPredictionTime;
                if (leadTime >= 0.1f && leadTime <= 8.0f && _pendingBehavior == behavior)
                {
                    // Valid prediction strictly prior to actual event!
                    CorrectPredictions++;
                    _leadTimes.Add(leadTime);
                    UpdateMeanLeadTime();

                    if (canonicalContext != null)
                    {
                        canonicalContext.isPrediction = true;
                        canonicalContext.leadTimeSeconds = leadTime;
                    }

                    sessionLogger?.LogPredictionEvent(canonicalContext, _pendingEvidence);
                    Debug.Log($"[AffectPredictor] VALID PREDICTION confirmed: behavior={behavior}, lead_time={leadTime:F2}s > 0");
                }
                else
                {
                    FalsePositives++;
                }

                _hasPendingPrediction = false;
            }
            else
            {
                // Actual event occurred with no prior prediction
                FalseNegatives++;
            }
        }

        /// <summary>
        /// Called when step changes or affect drops back to normal without an event firing.
        /// </summary>
        public void InvalidatePendingPrediction()
        {
            if (_hasPendingPrediction)
            {
                FalsePositives++;
                _hasPendingPrediction = false;
            }
            _predictionState = "IDLE";
            _precursorPersistenceCount = 0;
            _activePrecursorCandidate = "";
        }

        // ── Private Math Helpers ─────────────────────────────────────────────────
        private float CalculateSlope(Func<TimedAffectRecord, float> selector)
        {
            if (_history.Count < 2) return 0f;
            float sumT = 0f, sumY = 0f;
            for (int i = 0; i < _history.Count; i++)
            {
                sumT += _history[i].time;
                sumY += selector(_history[i]);
            }
            float meanT = sumT / _history.Count;
            float meanY = sumY / _history.Count;

            float num = 0f, den = 0f;
            for (int i = 0; i < _history.Count; i++)
            {
                float dt = _history[i].time - meanT;
                float dy = selector(_history[i]) - meanY;
                num += dt * dy;
                den += dt * dt;
            }
            return den > 1e-6f ? num / den : 0f;
        }

        private float CalculateStdDev(Func<TimedAffectRecord, float> selector)
        {
            if (_history.Count < 2) return 0f;
            float sum = 0f;
            for (int i = 0; i < _history.Count; i++) sum += selector(_history[i]);
            float mean = sum / _history.Count;

            float sumSq = 0f;
            for (int i = 0; i < _history.Count; i++)
            {
                float d = selector(_history[i]) - mean;
                sumSq += d * d;
            }
            return Mathf.Sqrt(sumSq / _history.Count);
        }

        private void UpdateMeanLeadTime()
        {
            if (_leadTimes.Count == 0) return;
            float sum = 0f;
            for (int i = 0; i < _leadTimes.Count; i++) sum += _leadTimes[i];
            MeanLeadTimeSeconds = sum / _leadTimes.Count;
        }
    }
}
