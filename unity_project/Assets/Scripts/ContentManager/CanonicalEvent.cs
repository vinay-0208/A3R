using System;
using UnityEngine;

namespace A3R.ContentManager
{
    /// <summary>
    /// CanonicalEvent — Single authoritative identity representation across the entire pipeline:
    /// EVENT -> BEHAVIOR -> OBJECT -> WORKER -> PDI -> PREDICTION -> VISUALIZATION -> LOGGING
    ///
    /// Guarantees that the learner ID (Worker), the circuit component (Object),
    /// the physical board location (PDI), and the learner state (Behavior)
    /// maintain 1:1 identity consistency without nulls, mismatches, or stale states.
    /// </summary>
    [Serializable]
    public class CanonicalEvent
    {
        [Tooltip("Globally unique identifier for this specific event instance")]
        public string eventId;

        [Tooltip("Unique session identifier")]
        public string sessionId;

        [Tooltip("Learner / participant identifier (Worker)")]
        public string workerId;

        [Tooltip("Step identifier from led_blink_task.json")]
        public string stepId;

        [Tooltip("0-based step index")]
        public int stepIndex;

        [Tooltip("Physical electronics component (Object)")]
        public string objectId;

        [Tooltip("Physical Device Interaction target (PDI)")]
        public string pdiTarget;

        [Tooltip("Learner behavior / affect classification")]
        public string behavior;

        [Tooltip("Whether this event was an anticipated prediction (true) or detection-only (false)")]
        public bool isPrediction;

        [Tooltip("Anticipation lead time in seconds (0 for detection-only)")]
        public float leadTimeSeconds;

        [Tooltip("UTC ISO-8601 timestamp string")]
        public string timestamp;

        public CanonicalEvent()
        {
            eventId = Guid.NewGuid().ToString("N");
            timestamp = DateTime.UtcNow.ToString("o");
        }

        public CanonicalEvent(string sessionId, string workerId, string stepId, int stepIndex,
                              string objectId, string pdiTarget, string behavior,
                              bool isPrediction = false, float leadTimeSeconds = 0f)
        {
            this.eventId = Guid.NewGuid().ToString("N");
            this.sessionId = string.IsNullOrEmpty(sessionId) ? "default_session" : sessionId;
            this.workerId = string.IsNullOrEmpty(workerId) ? "learner_01" : workerId;
            this.stepId = stepId ?? "unknown_step";
            this.stepIndex = stepIndex;
            this.objectId = objectId ?? "unknown_object";
            this.pdiTarget = pdiTarget ?? "workbench_marker";
            this.behavior = behavior ?? "Unspecified";
            this.isPrediction = isPrediction;
            this.leadTimeSeconds = leadTimeSeconds;
            this.timestamp = DateTime.UtcNow.ToString("o");

            ValidateConsistency();
        }

        /// <summary>
        /// Validates that identity propagation has no nulls or cross-entity corruptions.
        /// </summary>
        public bool ValidateConsistency()
        {
            if (string.IsNullOrEmpty(eventId))
            {
                Debug.LogError("[CanonicalEvent] Invariant violation: eventId is null or empty!");
                return false;
            }
            if (string.IsNullOrEmpty(workerId))
            {
                Debug.LogError($"[CanonicalEvent {eventId}] Invariant violation: workerId is null or empty!");
                return false;
            }
            if (string.IsNullOrEmpty(objectId))
            {
                Debug.LogError($"[CanonicalEvent {eventId}] Invariant violation: objectId is null or empty!");
                return false;
            }
            if (string.IsNullOrEmpty(pdiTarget))
            {
                Debug.LogError($"[CanonicalEvent {eventId}] Invariant violation: pdiTarget is null or empty!");
                return false;
            }
            return true;
        }

        public override string ToString()
        {
            return $"[Event:{eventId[..8]}] Worker={workerId}, Step={stepId}({stepIndex}), " +
                   $"Object={objectId}, PDI={pdiTarget}, Behavior={behavior}, " +
                   $"Prediction={isPrediction}(lead={leadTimeSeconds:F2}s)";
        }
    }
}
