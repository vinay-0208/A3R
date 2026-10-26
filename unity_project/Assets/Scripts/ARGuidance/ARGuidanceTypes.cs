using System;
using UnityEngine;
using A3R.Adaptation;

namespace A3R.ARGuidance
{
    public enum GuidanceVisualState
    {
        Hidden,
        Normal,
        ProminentCue,
        HintEmphasized,
        PacedSlow,
        IsolatedStep
    }

    [Serializable]
    public class ARGuidanceStepDefinition
    {
        public int stepIndex;
        public string stepId;
        public string targetName;
        public Vector3 targetLocalOffset;
        public bool showArrow;
        public bool showRing;
        public float ringRadius;
        public Color ringColor;
        public string highlightComponent;
        public string instructionSummary;
        public string completionCondition;
        public string provisionalGpioNotice;

        public ARGuidanceStepDefinition()
        {
            stepIndex = 0;
            stepId = "";
            targetName = "";
            targetLocalOffset = Vector3.zero;
            showArrow = true;
            showRing = true;
            ringRadius = 0.03f;
            ringColor = new Color(0.2f, 0.8f, 1.0f, 0.85f);
            highlightComponent = "";
            instructionSummary = "";
            completionCondition = "";
            provisionalGpioNotice = "Provisional software-test GPIO assignment.";
        }

        public ARGuidanceStepDefinition(
            int index,
            string id,
            string target,
            Vector3 offset,
            bool arrow,
            bool ring,
            float radius,
            Color color,
            string component,
            string instruction,
            string completion,
            string gpioNotice = "Provisional software-test GPIO assignment.")
        {
            stepIndex = index;
            stepId = id;
            targetName = target;
            targetLocalOffset = offset;
            showArrow = arrow;
            showRing = ring;
            ringRadius = radius;
            ringColor = color;
            highlightComponent = component;
            instructionSummary = instruction;
            completionCondition = completion;
            provisionalGpioNotice = gpioNotice;
        }
    }
}
