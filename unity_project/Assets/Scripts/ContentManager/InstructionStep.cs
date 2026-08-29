using System;
using System.Collections.Generic;
using UnityEngine;

namespace A3R.ContentManager
{
    /// <summary>
    /// InstructionStep — Data container for a single step in the electronics assembly task.
    /// Deserialized from led_blink_task.json via JsonUtility.
    /// </summary>
    [Serializable]
    public class InstructionStep
    {
        // ── Core Fields ──────────────────────────────────────────────────────────
        [Tooltip("Unique step identifier, e.g. 'insert_led'")]
        public string id;

        [Tooltip("1-based step number for display")]
        public int step_number;

        [Tooltip("Short title shown as the AR overlay header")]
        public string title;

        [Tooltip("Main instruction text shown to the learner")]
        public string instruction;

        [Tooltip("Detailed explanation text (shown in expanded/slow-pace mode)")]
        public string detail;

        [Tooltip("Name of the 3D component to highlight, or null")]
        public string highlight_component;

        [Tooltip("RGB highlight color for the component (each channel 0-1)")]
        public float[] highlight_color;

        [Tooltip("Logical target for the AR cue arrow, or null")]
        public string ar_arrow_target;

        [Tooltip("Common errors for this step — used by logger")]
        public string[] common_errors;

        [Tooltip("Adaptation hints: one per adaptation action type")]
        public AdaptationHints adaptation_hints;
    }

    /// <summary>Adaptation-specific hint texts for each of the four adaptation actions.</summary>
    [Serializable]
    public class AdaptationHints
    {
        [Tooltip("Used when AdaptationController fires SimplifyStep")]
        public string SimplifyStep;

        [Tooltip("Used when AdaptationController fires InjectCue")]
        public string InjectCue;

        [Tooltip("Used when AdaptationController fires SlowPace")]
        public string SlowPace;

        [Tooltip("Used when AdaptationController fires InsertHint")]
        public string InsertHint;
    }

    /// <summary>Root container for the full task JSON.</summary>
    [Serializable]
    public class InstructionTask
    {
        public string task_id;
        public string task_name;
        public string description;
        public string[] components;
        public InstructionStep[] steps;
    }
}
