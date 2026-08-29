using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using A3R.Affect;
using A3R.Adaptation;
using A3R.ContentManager;

namespace A3R.Logging
{
    /// <summary>
    /// SessionLogger — Module 5 of the A³R system.
    ///
    /// Records the full session to a local JSON file for evaluation:
    ///   - Affect trajectory: timestamped confusion/frustration/engagement scores (2Hz)
    ///   - Adaptation events: every AdaptationController firing (action, step, scores, timestamp)
    ///   - Step transitions: when the learner advances to a new step
    ///   - Session metadata: start time, device info, task version
    ///
    /// Output: Application.persistentDataPath/session_logs/session_<UTC-timestamp>.json
    /// On Android: /storage/emulated/0/Android/data/<package>/files/session_logs/
    ///
    /// The log format is line-delimited JSON (one JSON object per line) for ease of
    /// streaming / incremental parsing during evaluation.
    /// </summary>
    public class SessionLogger : MonoBehaviour
    {
        // ── Inspector References ────────────────────────────────────────────────
        [Header("References")]
        [SerializeField] private AffectScorer        affectScorer;
        [SerializeField] private AdaptationController adaptationController;
        [SerializeField] private ARContentManager    contentManager;

        [Header("Logging Config")]
        [Tooltip("Write a summary JSON at session end in addition to the line-delimited log.")]
        [SerializeField] private bool writeSummaryOnEnd = true;

        // ── Private State ────────────────────────────────────────────────────────
        private StreamWriter _logWriter;
        private string       _logFilePath;
        private DateTime     _sessionStart;
        private int          _affectSampleCount;
        private int          _adaptationEventCount;
        private List<AdaptationEventRecord> _adaptationLog = new();

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _sessionStart = DateTime.UtcNow;
            OpenLogFile();
            WriteSessionHeader();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (writeSummaryOnEnd) WriteSummary();
            CloseLogFile();
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
            var record = new
            {
                type             = "affect",
                ts               = sample.Timestamp.ToString("o"),
                confusion        = MathF.Round(sample.ConfusionScore,   4),
                frustration      = MathF.Round(sample.FrustrationScore, 4),
                engagement       = MathF.Round(sample.EngagementScore,  4),
                brow_furrow      = MathF.Round(sample.BrowFurrowNorm,   4),
                ear              = MathF.Round(sample.EarNorm,          4),
                mouth_tension    = MathF.Round(sample.MouthTensionNorm, 4),
                brow_height      = MathF.Round(sample.BrowHeightNorm,   4),
                step_id          = sample.StepId
            };
            WriteJsonLine(record);
        }

        private void LogAdaptationEvent(AdaptationEvent evt)
        {
            if (_logWriter == null) return;
            _adaptationEventCount++;
            var record = new
            {
                type        = "adaptation",
                ts          = evt.Timestamp.ToString("o"),
                action      = evt.Action.ToString(),
                step_id     = evt.StepId,
                confusion   = MathF.Round(evt.ConfusionScore,   4),
                frustration = MathF.Round(evt.FrustrationScore, 4)
            };
            _adaptationLog.Add(new AdaptationEventRecord
            {
                Timestamp   = evt.Timestamp,
                Action      = evt.Action.ToString(),
                StepId      = evt.StepId,
                Confusion   = evt.ConfusionScore,
                Frustration = evt.FrustrationScore
            });
            WriteJsonLine(record);
            Debug.Log($"[SessionLogger] Logged adaptation: {evt}");
        }

        private void LogStepChange(int stepIndex, InstructionStep step)
        {
            if (_logWriter == null) return;
            var record = new
            {
                type        = "step_change",
                ts          = DateTime.UtcNow.ToString("o"),
                step_index  = stepIndex,
                step_id     = step.id,
                step_title  = step.title
            };
            WriteJsonLine(record);
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
            var header = new
            {
                type         = "session_start",
                ts           = _sessionStart.ToString("o"),
                device_model = SystemInfo.deviceModel,
                device_name  = SystemInfo.deviceName,
                os           = SystemInfo.operatingSystem,
                task_version = "led_blink_circuit_v1"
            };
            WriteJsonLine(header);
        }

        private void WriteSummary()
        {
            if (_logWriter == null) return;
            var summary = new
            {
                type                  = "session_end",
                ts                    = DateTime.UtcNow.ToString("o"),
                session_start         = _sessionStart.ToString("o"),
                duration_s            = (DateTime.UtcNow - _sessionStart).TotalSeconds,
                affect_samples        = _affectSampleCount,
                adaptation_events     = _adaptationEventCount,
                adaptations           = _adaptationLog
            };
            WriteJsonLine(summary);
        }

        private void CloseLogFile()
        {
            _logWriter?.Flush();
            _logWriter?.Close();
            _logWriter = null;
        }

        private void WriteJsonLine(object record)
        {
            try
            {
                string json = JsonUtility.ToJson(record);
                _logWriter?.WriteLine(json);
                _logWriter?.Flush();  // ensure data is written even if app crashes
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionLogger] Write error: {ex.Message}");
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────
        public string LogFilePath          => _logFilePath;
        public int    AffectSampleCount    => _affectSampleCount;
        public int    AdaptationEventCount => _adaptationEventCount;

        // ── Private Records ──────────────────────────────────────────────────────
        [Serializable]
        private class AdaptationEventRecord
        {
            public DateTime Timestamp;
            public string   Action;
            public string   StepId;
            public float    Confusion;
            public float    Frustration;
        }
    }
}
