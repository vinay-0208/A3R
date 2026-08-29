using System;

namespace A3R.Adaptation
{
    /// <summary>
    /// AdaptationAction — The four discrete adaptation actions the AdaptationController can emit.
    /// Used as the event payload for AdaptationEvent.
    /// These map directly to the adaptation strategies described in PROJECT_PLAN.md.
    /// </summary>
    public enum AdaptationAction
    {
        /// <summary>Break the current step into smaller, more explicit sub-steps.</summary>
        SimplifyStep,

        /// <summary>Show a visual AR arrow cue pointing at the target component.</summary>
        InjectCue,

        /// <summary>Expand the detail text and slow the instructional pacing.</summary>
        SlowPace,

        /// <summary>Display a contextual hint panel with corrective guidance.</summary>
        InsertHint
    }

    /// <summary>
    /// AdaptationEvent — Payload emitted by AdaptationController when the FSM fires.
    /// Carries the action type, the step it was triggered on, and the affect scores
    /// that triggered it (for logging by SessionLogger).
    /// </summary>
    public class AdaptationEvent
    {
        /// <summary>The adaptation action to apply.</summary>
        public AdaptationAction Action { get; }

        /// <summary>Step ID (from led_blink_task.json) when the adaptation was triggered.</summary>
        public string StepId { get; }

        /// <summary>Confusion score at the moment of triggering.</summary>
        public float ConfusionScore { get; }

        /// <summary>Frustration score at the moment of triggering.</summary>
        public float FrustrationScore { get; }

        /// <summary>UTC timestamp of the event.</summary>
        public DateTime Timestamp { get; }

        public AdaptationEvent(AdaptationAction action, string stepId,
                               float confusionScore, float frustrationScore)
        {
            Action           = action;
            StepId           = stepId;
            ConfusionScore   = confusionScore;
            FrustrationScore = frustrationScore;
            Timestamp        = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"[AdaptationEvent] {Action} at step={StepId} " +
                   $"(confusion={ConfusionScore:F3}, frustration={FrustrationScore:F3}) " +
                   $"@ {Timestamp:HH:mm:ss.fff}";
        }
    }
}
