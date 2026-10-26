using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using A3R.Affect;
using A3R.Adaptation;
using A3R.ContentManager;
using A3R.Hardware;

namespace A3R.Logging
{
    /// <summary>
    /// SessionLogger — Module 5 of the A³R system.
    ///
    /// Records the complete session in valid JSON Lines (JSONL) format:
    ///   - Session metadata (start, device info, worker ID, session ID)
    ///   - Affect trajectory: timestamped confusion/frustration/engagement scores (2Hz)
    ///   - Adaptation events: fired by AdaptationController
    ///   - Prediction events: anticipated precursor signals and lead times
    ///   - Step transitions: step advancement with object and PDI mapping
    ///   - Session summary at completion
    ///
    /// Uses explicit [Serializable] record types to guarantee valid JSONL without empty objects.
    /// </summary>
    public class SessionLogger : MonoBehaviour
    {
        // ── Inspector References ────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private AffectScorer        affectScorer;
        [SerializeField] private AdaptationController adaptationController;
        [SerializeField] private ARContentManager    contentManager;

        [Header("Identity Config")]
        [SerializeField] private string workerId = "learner_01";
        [SerializeField] private string taskVersion = "led_blink_circuit_v1";

        [Header("Logging Config")]
        [Tooltip("Write a summary record at session end in the line-delimited log.")]
        [SerializeField] private bool writeSummaryOnEnd = true;
        [Tooltip("Automatically begin logging session on Start.")]
        [SerializeField] private bool autoStartOnStart = false;

        // ── Public Events ────────────────────────────────────────────────────────
        public event Action<string> OnSessionStarted;
        public event Action<SessionSummaryData> OnSessionEnded;

        // ── Serializable Record Definitions ──────────────────────────────────────
        [Serializable]
        public class SessionSummaryData
        {
            public string sessionId;
            public string workerId;
            public float durationSeconds;
            public int affectSampleCount;
            public int adaptationEventCount;
            public int predictionEventCount;
            public int hardwareEventCount;
            public string logFilePath;
            public string sessionStartIso;
            public string sessionEndIso;
        }
        [Serializable]
        public class SessionStartRecord
        {
            public string type = "session_start";
            public string ts;
            public string session_id;
            public string worker_id;
            public string device_model;
            public string device_name;
            public string os;
            public string task_version;
            public string inference_mode;
        }

        [Serializable]
        public class AffectSampleRecord
        {
            public string type = "affect";
            public string ts;
            public string session_id;
            public string worker_id;
            public string step_id;
            public string object_id;
            public float confusion;
            public float frustration;
            public float engagement;
            public float brow_furrow;
            public float ear_raw;
            public float ear_norm;
            public float mouth_tension;
            public float brow_height;
            public bool is_simulated;
        }

        [Serializable]
        public class AdaptationRecord
        {
            public string type = "adaptation";
            public string ts;
            public string event_id;
            public string session_id;
            public string worker_id;
            public string step_id;
            public string object_id;
            public string action;
            public float confusion;
            public float frustration;
            public float dwell_duration_s;
            public bool accepted = true;
            public string suppression_reason = "";
        }

        [Serializable]
        public class PredictionRecord
        {
            public string type = "prediction";
            public string ts;
            public string event_id;
            public string session_id;
            public string worker_id;
            public string step_id;
            public string object_id;
            public string behavior;
            public bool is_prediction;
            public float lead_time_s;
            public float confidence;
            public float evidence_score;
            public float trend_slope;
            public float confusion_score;
            public float frustration_score;
            public bool is_simulated;
            public bool is_technical_disruption;
            public string prediction_state;
        }

        [Serializable]
        public class StepChangeRecord
        {
            public string type = "step_change";
            public string ts;
            public string session_id;
            public string worker_id;
            public int step_index;
            public string step_id;
            public string step_title;
            public string object_id;
        }

        [Serializable]
        public class HardwareRecord
        {
            public string type = "hardware";
            public string ts;
            public string session_id;
            public string worker_id;
            public string step_id;
            public string state;
            public string command_id;
            public string command_type;
            public string target_led;
            public string led_state;
            public string ack_status;
            public float latency_ms;
            public bool is_mock;
            public string details;
        }

        [Serializable]
        public class SessionEndRecord
        {
            public string type = "session_end";
            public string ts;
            public string session_id;
            public string worker_id;
            public string session_start;
            public float duration_s;
            public int affect_samples;
            public int adaptation_events;
            public int prediction_events;
            public int hardware_events;
        }

        // ── Private State ────────────────────────────────────────────────────────
        private StreamWriter _logWriter;
        private string       _logFilePath;
        private string       _sessionId;
        private DateTime     _sessionStart;
        private int          _affectSampleCount;
        private int          _adaptationEventCount;
        private int          _predictionEventCount;
        private int          _hardwareEventCount;
        private string       _currentStepId = "";
        private string       _currentObjectId = "";
        private bool         _isSessionActive = false;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _sessionStart = DateTime.UtcNow;
            if (affectScorer == null)
                affectScorer = GetComponent<AffectScorer>() ?? FindFirstObjectByType<AffectScorer>();
            if (adaptationController == null)
                adaptationController = GetComponent<AdaptationController>() ?? FindFirstObjectByType<AdaptationController>();
            if (contentManager == null)
                contentManager = GetComponent<ARContentManager>() ?? FindFirstObjectByType<ARContentManager>();
        }

        private void Start()
        {
            SubscribeToEvents();
            if (autoStartOnStart)
            {
                StartNewSession();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_isSessionActive)
            {
                EndCurrentSession();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _logWriter?.Flush();
        }

        // ── Event Subscription ───────────────────────────────────────────────────
        private void SubscribeToEvents()
        {
            if (affectScorer != null)
                affectScorer.OnAffectSampleReady += LogAffectSample;
            if (adaptationController != null)
                adaptationController.OnAdaptationTriggered += LogAdaptationEvent;
            if (contentManager != null)
                contentManager.OnStepChanged += LogStepChange;
        }

        private void UnsubscribeFromEvents()
        {
            if (affectScorer != null)
                affectScorer.OnAffectSampleReady -= LogAffectSample;
            if (adaptationController != null)
                adaptationController.OnAdaptationTriggered -= LogAdaptationEvent;
            if (contentManager != null)
                contentManager.OnStepChanged -= LogStepChange;
        }

        // ── Event Handlers ───────────────────────────────────────────────────────
        private void LogAffectSample(AffectSample sample)
        {
            if (_logWriter == null) return;
            _affectSampleCount++;

            var record = new AffectSampleRecord
            {
                ts            = sample.Timestamp.ToString("o"),
                session_id    = _sessionId,
                worker_id     = workerId,
                step_id       = sample.StepId,
                object_id     = _currentObjectId,
                confusion     = (float)Math.Round(sample.ConfusionScore,   4),
                frustration   = (float)Math.Round(sample.FrustrationScore, 4),
                engagement    = (float)Math.Round(sample.EngagementScore,  4),
                brow_furrow   = (float)Math.Round(sample.BrowFurrowNorm,   4),
                ear_raw       = (float)Math.Round(sample.EarRaw,           4),
                ear_norm      = (float)Math.Round(sample.EarNorm,          4),
                mouth_tension = (float)Math.Round(sample.MouthTensionNorm, 4),
                brow_height   = (float)Math.Round(sample.BrowHeightNorm,   4),
                is_simulated  = sample.IsSimulated
            };
            WriteJsonLine(record);
        }

        private void LogAdaptationEvent(AdaptationEvent evt)
        {
            if (_logWriter == null) return;
            _adaptationEventCount++;

            var record = new AdaptationRecord
            {
                ts               = evt.Timestamp.ToString("o"),
                event_id         = evt.CanonicalContext != null ? evt.CanonicalContext.eventId : Guid.NewGuid().ToString("N"),
                session_id       = _sessionId,
                worker_id        = evt.CanonicalContext != null ? evt.CanonicalContext.workerId : workerId,
                step_id          = evt.StepId,
                object_id        = evt.CanonicalContext != null ? evt.CanonicalContext.objectId : _currentObjectId,
                action           = evt.Action.ToString(),
                confusion        = (float)Math.Round(evt.ConfusionScore,   4),
                frustration      = (float)Math.Round(evt.FrustrationScore, 4),
                dwell_duration_s = adaptationController != null ? adaptationController.DwellTimeRequired : 3.0f
            };
            WriteJsonLine(record);
            Debug.Log($"[SessionLogger] Logged adaptation: {evt}");
        }

        public void LogAdaptationSuppressed(string stepId, string reason, float confusion, float frustration, float dwellDurationS = 3.0f)
        {
            if (_logWriter == null) return;

            var record = new AdaptationRecord
            {
                ts                 = DateTime.UtcNow.ToString("o"),
                event_id           = Guid.NewGuid().ToString("N"),
                session_id         = _sessionId,
                worker_id          = workerId,
                step_id            = !string.IsNullOrEmpty(stepId) ? stepId : _currentStepId,
                object_id          = _currentObjectId,
                action             = "None",
                confusion          = (float)Math.Round(confusion, 4),
                frustration        = (float)Math.Round(frustration, 4),
                dwell_duration_s   = dwellDurationS,
                accepted           = false,
                suppression_reason = reason
            };
            WriteJsonLine(record);
            Debug.Log($"[SessionLogger] Logged adaptation suppression: {reason} at step={record.step_id}");
        }

        public void LogPredictionEvent(CanonicalEvent canonical, float confidence)
        {
            if (_logWriter == null) return;
            _predictionEventCount++;

            var record = new PredictionRecord
            {
                ts            = DateTime.UtcNow.ToString("o"),
                event_id      = canonical != null ? canonical.eventId : Guid.NewGuid().ToString("N"),
                session_id    = _sessionId,
                worker_id     = canonical != null ? canonical.workerId : workerId,
                step_id       = canonical != null ? canonical.stepId : _currentStepId,
                object_id     = canonical != null ? canonical.objectId : _currentObjectId,
                behavior      = canonical != null ? canonical.behavior : "Unknown",
                is_prediction = true,
                lead_time_s   = canonical != null ? canonical.leadTimeSeconds : 0f,
                confidence    = (float)Math.Round(confidence, 4)
            };
            WriteJsonLine(record);
        }

        public void LogPrediction(
            string behavior,
            float evidenceScore,
            float confusion,
            float frustration,
            float trendSlope,
            string stepId,
            string predictionState,
            bool isSimulated,
            bool isTechnicalDisruption,
            float leadTimeSeconds = 0f)
        {
            if (_logWriter == null) return;
            _predictionEventCount++;

            var record = new PredictionRecord
            {
                ts                      = DateTime.UtcNow.ToString("o"),
                event_id                = Guid.NewGuid().ToString("N"),
                session_id              = _sessionId,
                worker_id               = workerId,
                step_id                 = !string.IsNullOrEmpty(stepId) ? stepId : _currentStepId,
                object_id               = _currentObjectId,
                behavior                = behavior,
                is_prediction           = true,
                lead_time_s             = leadTimeSeconds,
                confidence              = (float)Math.Round(evidenceScore, 4),
                evidence_score          = (float)Math.Round(evidenceScore, 4),
                trend_slope             = (float)Math.Round(trendSlope, 4),
                confusion_score         = (float)Math.Round(confusion, 4),
                frustration_score       = (float)Math.Round(frustration, 4),
                is_simulated            = isSimulated,
                is_technical_disruption = isTechnicalDisruption,
                prediction_state        = predictionState
            };
            WriteJsonLine(record);
        }

        private void LogStepChange(int stepIndex, InstructionStep step)
        {
            if (_logWriter == null) return;
            _currentStepId = step.id;
            _currentObjectId = step.highlight_component ?? "breadboard";

            var record = new StepChangeRecord
            {
                ts          = DateTime.UtcNow.ToString("o"),
                session_id  = _sessionId,
                worker_id   = workerId,
                step_index  = stepIndex,
                step_id     = step.id,
                step_title  = step.title,
                object_id   = _currentObjectId
            };
            WriteJsonLine(record);
        }

        // ── Hardware Event Logging ───────────────────────────────────────────────
        public void LogHardwareStateChange(string oldState, string newState, bool isMock, string details = "")
        {
            if (_logWriter == null) return;
            _hardwareEventCount++;
            var record = new HardwareRecord
            {
                ts           = DateTime.UtcNow.ToString("o"),
                session_id   = _sessionId,
                worker_id    = workerId,
                step_id      = _currentStepId,
                state        = newState,
                command_id   = "",
                command_type = "STATE_CHANGE",
                target_led   = "",
                led_state    = "",
                ack_status   = "",
                latency_ms   = 0f,
                is_mock      = isMock,
                details      = $"From {oldState} to {newState}. {details}"
            };
            WriteJsonLine(record);
        }

        public void LogHardwareCommand(HardwareCommand cmd, CommandAck ack, bool isMock)
        {
            if (_logWriter == null || cmd == null) return;
            _hardwareEventCount++;
            var record = new HardwareRecord
            {
                ts           = DateTime.UtcNow.ToString("o"),
                session_id   = _sessionId,
                worker_id    = workerId,
                step_id      = _currentStepId,
                state        = ack != null && ack.status == AckStatus.Success ? "ACK_RECEIVED" : "NACK_OR_TIMEOUT",
                command_id   = cmd.commandId,
                command_type = cmd.type.ToString(),
                target_led   = cmd.targetLed.ToString(),
                led_state    = cmd.state.ToString(),
                ack_status   = ack != null ? ack.status.ToString() : "NULL_ACK",
                latency_ms   = ack != null ? ack.latencyMs : 0f,
                is_mock      = isMock,
                details      = ack != null ? ack.message : "No ACK"
            };
            WriteJsonLine(record);
        }

        public void LogHardwareEvent(string eventType, string data, bool isMock)
        {
            if (_logWriter == null) return;
            _hardwareEventCount++;
            var record = new HardwareRecord
            {
                ts           = DateTime.UtcNow.ToString("o"),
                session_id   = _sessionId,
                worker_id    = workerId,
                step_id      = _currentStepId,
                state        = "HARDWARE_EVENT",
                command_id   = "",
                command_type = eventType,
                target_led   = "",
                led_state    = "",
                ack_status   = "",
                latency_ms   = 0f,
                is_mock      = isMock,
                details      = data
            };
            WriteJsonLine(record);
        }

        // ── Tutorial Event Logging (Phase 4) ─────────────────────────────────────
        public void LogTutorialEvent(string eventName, string details = "")
        {
            if (_logWriter == null) return;
            var record = new HardwareRecord
            {
                ts           = DateTime.UtcNow.ToString("o"),
                session_id   = _sessionId,
                worker_id    = workerId,
                step_id      = "step1_resistor",
                state        = "TUTORIAL_EVENT",
                command_id   = "",
                command_type = eventName,
                target_led   = "",
                led_state    = "",
                ack_status   = "",
                latency_ms   = 0f,
                is_mock      = false,
                details      = details
            };
            WriteJsonLine(record);
            Debug.Log($"[SessionLogger] Tutorial event: {eventName} ({details})");
        }

        // ── File Management ──────────────────────────────────────────────────────
        private void OpenLogFile()
        {
            try
            {
                string logDir = Path.Combine(Application.persistentDataPath, "session_logs");
                Directory.CreateDirectory(logDir);
                string timestamp = _sessionStart.ToString("yyyyMMdd_HHmmss");
                _logFilePath = Path.Combine(logDir, $"session_{timestamp}.jsonl");
                _logWriter = new StreamWriter(_logFilePath, append: false, Encoding.UTF8);
                Debug.Log($"[SessionLogger] Log file: {_logFilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionLogger] Failed to open log file: {ex.Message}");
            }
        }

        private void WriteSessionHeader()
        {
            var header = new SessionStartRecord
            {
                ts             = _sessionStart.ToString("o"),
                session_id     = _sessionId,
                worker_id      = workerId,
                device_model   = SystemInfo.deviceModel,
                device_name    = SystemInfo.deviceName,
                os             = SystemInfo.operatingSystem,
                task_version   = taskVersion,
                inference_mode = "RuleBased_MediaPipe"
            };
            WriteJsonLine(header);
        }

        private void WriteSummary()
        {
            if (_logWriter == null) return;
            var summary = new SessionEndRecord
            {
                ts                = DateTime.UtcNow.ToString("o"),
                session_id        = _sessionId,
                worker_id         = workerId,
                session_start     = _sessionStart.ToString("o"),
                duration_s        = (float)(DateTime.UtcNow - _sessionStart).TotalSeconds,
                affect_samples    = _affectSampleCount,
                adaptation_events = _adaptationEventCount,
                prediction_events = _predictionEventCount,
                hardware_events   = _hardwareEventCount
            };
            WriteJsonLine(summary);
        }

        private void CloseLogFile()
        {
            _logWriter?.Flush();
            _logWriter?.Close();
            _logWriter = null;
        }

        private void WriteJsonLine<T>(T record)
        {
            try
            {
                string json = JsonUtility.ToJson(record);
                _logWriter?.WriteLine(json);
                _logWriter?.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionLogger] Write error: {ex.Message}");
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────
        public string LogFilePath          => _logFilePath;
        public string SessionId            => _sessionId;
        public string WorkerId             { get => workerId; set => workerId = value; }
        public bool   IsSessionActive      => _isSessionActive;
        public float  CurrentSessionDurationSeconds => _isSessionActive ? (float)(DateTime.UtcNow - _sessionStart).TotalSeconds : 0f;
        public int    AffectSampleCount    => _affectSampleCount;
        public int    AdaptationEventCount => _adaptationEventCount;
        public int    PredictionEventCount => _predictionEventCount;
        public int    HardwareEventCount   => _hardwareEventCount;

        /// <summary>Explicitly start a new logging session from UI or manager.</summary>
        public void StartNewSession(string newWorkerId = null)
        {
            if (_isSessionActive)
            {
                EndCurrentSession();
            }

            if (!string.IsNullOrEmpty(newWorkerId))
                workerId = newWorkerId;

            _sessionId = Guid.NewGuid().ToString("N");
            _sessionStart = DateTime.UtcNow;
            _affectSampleCount = 0;
            _adaptationEventCount = 0;
            _predictionEventCount = 0;
            _hardwareEventCount = 0;

            OpenLogFile();
            WriteSessionHeader();
            _isSessionActive = true;
            Debug.Log($"[SessionLogger] Started new session {_sessionId} for worker {workerId}");
            OnSessionStarted?.Invoke(_sessionId);
        }

        /// <summary>Explicitly end current session, write summary, flush, and return summary data.</summary>
        public SessionSummaryData EndCurrentSession()
        {
            if (!_isSessionActive && _logWriter == null) return null;

            var summary = new SessionSummaryData
            {
                sessionId = _sessionId,
                workerId = workerId,
                durationSeconds = (float)(DateTime.UtcNow - _sessionStart).TotalSeconds,
                affectSampleCount = _affectSampleCount,
                adaptationEventCount = _adaptationEventCount,
                predictionEventCount = _predictionEventCount,
                hardwareEventCount = _hardwareEventCount,
                logFilePath = _logFilePath,
                sessionStartIso = _sessionStart.ToString("o"),
                sessionEndIso = DateTime.UtcNow.ToString("o")
            };

            if (writeSummaryOnEnd) WriteSummary();
            CloseLogFile();
            _isSessionActive = false;
            Debug.Log($"[SessionLogger] Ended session {_sessionId} (duration={summary.durationSeconds:F1}s, samples={_affectSampleCount})");
            OnSessionEnded?.Invoke(summary);
            return summary;
        }

        /// <summary>Returns a snapshot of current session metrics without closing the log.</summary>
        public SessionSummaryData GetCurrentSessionSummary()
        {
            return new SessionSummaryData
            {
                sessionId = _sessionId,
                workerId = workerId,
                durationSeconds = (float)(DateTime.UtcNow - _sessionStart).TotalSeconds,
                affectSampleCount = _affectSampleCount,
                adaptationEventCount = _adaptationEventCount,
                predictionEventCount = _predictionEventCount,
                hardwareEventCount = _hardwareEventCount,
                logFilePath = _logFilePath,
                sessionStartIso = _sessionStart.ToString("o"),
                sessionEndIso = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
