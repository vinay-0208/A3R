using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using A3R.ContentManager;

namespace A3R.UI
{
    /// <summary>
    /// UIManager — Canvas HUD controller for the A³R application.
    ///
    /// Owns all 2D UI panels:
    ///   - Step panel:   current step title + instruction text + step counter
    ///   - Hint panel:   collapsible hint card injected by AdaptationController
    ///   - Debug HUD:    real-time affect scores (confusion/frustration/engagement)
    ///                   and marker tracking status — toggled by a hidden tap zone
    ///   - Progress bar: visual step progress (0..N steps)
    ///
    /// Called by ARContentManager for step transitions and hint injection.
    /// All methods are safe to call from any thread via the main-thread dispatch queue.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Step Panel ───────────────────────────────────────────────────────────
        [Header("Step Panel")]
        [SerializeField] private GameObject  stepPanel;
        [SerializeField] private TMP_Text    stepTitleText;
        [SerializeField] private TMP_Text    stepInstructionText;
        [SerializeField] private TMP_Text    stepCounterText;        // "Step 3 / 8"
        [SerializeField] private Slider      progressSlider;
        [SerializeField] private Button      nextStepButton;
        [SerializeField] private Button      prevStepButton;

        // ── Hint Panel ───────────────────────────────────────────────────────────
        [Header("Hint Panel")]
        [SerializeField] private GameObject  hintPanel;
        [SerializeField] private TMP_Text    hintLevelLabel;         // "Hint" / "Tip" / "Simplified"
        [SerializeField] private TMP_Text    hintBodyText;
        [SerializeField] private Button      hintDismissButton;

        // ── Debug HUD ────────────────────────────────────────────────────────────
        [Header("Debug HUD")]
        [SerializeField] private GameObject  debugHudPanel;
        [SerializeField] private TMP_Text    confusionText;
        [SerializeField] private TMP_Text    frustrationText;
        [SerializeField] private TMP_Text    engagementText;
        [SerializeField] private TMP_Text    markerStatusText;
        [SerializeField] private Button      debugToggleButton;      // invisible tap zone

        // ── No-Marker Overlay ────────────────────────────────────────────────────
        [Header("No-Marker Overlay")]
        [SerializeField] private GameObject  noMarkerOverlay;        // "Point camera at marker"
        [SerializeField] private TMP_Text    noMarkerText;

        // ── Config ───────────────────────────────────────────────────────────────
        [Header("Config")]
        [SerializeField] private bool  showDebugHudOnStart = false;
        [SerializeField] private float hintAutoDismissSeconds = 0.0f;  // 0 = manual only

        // ── Private state ────────────────────────────────────────────────────────
        private int   _totalSteps;
        private float _hintDismissTimer;
        private bool  _hintAutoDismissing;

        // ── Events ───────────────────────────────────────────────────────────────
        public event Action OnNextStepRequested;
        public event Action OnPrevStepRequested;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            // Button wiring
            nextStepButton?.onClick.AddListener(() => OnNextStepRequested?.Invoke());
            prevStepButton?.onClick.AddListener(() => OnPrevStepRequested?.Invoke());
            hintDismissButton?.onClick.AddListener(HideHint);
            debugToggleButton?.onClick.AddListener(ToggleDebugHud);

            // Initial visibility
            if (debugHudPanel != null)
                debugHudPanel.SetActive(showDebugHudOnStart);
            if (hintPanel != null)
                hintPanel.SetActive(false);
            if (noMarkerOverlay != null)
                noMarkerOverlay.SetActive(false);

            UpdateMarkerStatus(false);
        }

        private void Update()
        {
            // Auto-dismiss hint after timeout
            if (_hintAutoDismissing)
            {
                _hintDismissTimer -= Time.deltaTime;
                if (_hintDismissTimer <= 0f)
                {
                    _hintAutoDismissing = false;
                    HideHint();
                }
            }
        }

        // ── Step Panel API ───────────────────────────────────────────────────────
        /// <summary>Show a step in the step panel.</summary>
        public void ShowStep(InstructionStep step, int stepIndex, int totalSteps)
        {
            _totalSteps = totalSteps;

            if (stepTitleText != null)
                stepTitleText.text = step.title ?? "";

            if (stepInstructionText != null)
                stepInstructionText.text = step.instruction ?? "";

            if (stepCounterText != null)
                stepCounterText.text = $"Step {stepIndex + 1} / {totalSteps}";

            if (progressSlider != null)
            {
                progressSlider.minValue = 0;
                progressSlider.maxValue = Mathf.Max(1, totalSteps - 1);
                progressSlider.value    = stepIndex;
            }

            // Disable prev on first step, next on last
            if (prevStepButton != null)
                prevStepButton.interactable = stepIndex > 0;
            if (nextStepButton != null)
                nextStepButton.interactable = stepIndex < totalSteps - 1;

            if (stepPanel != null)
                stepPanel.SetActive(true);
        }

        // ── Hint Panel API ───────────────────────────────────────────────────────
        /// <summary>Show a contextual hint card.</summary>
        public void ShowHint(string hintText, string label = "Hint")
        {
            if (hintPanel == null) return;

            if (hintLevelLabel != null)  hintLevelLabel.text = label;
            if (hintBodyText != null)    hintBodyText.text   = hintText;

            hintPanel.SetActive(true);

            if (hintAutoDismissSeconds > 0f)
            {
                _hintDismissTimer   = hintAutoDismissSeconds;
                _hintAutoDismissing = true;
            }
        }

        /// <summary>Hide the hint panel.</summary>
        public void HideHint()
        {
            _hintAutoDismissing = false;
            if (hintPanel != null)
                hintPanel.SetActive(false);
        }

        // ── Debug HUD API ────────────────────────────────────────────────────────
        /// <summary>Update the debug HUD with latest affect scores.</summary>
        public void UpdateDebugHud(float confusion, float frustration, float engagement,
                                    bool markerTracked)
        {
            if (confusionText != null)
                confusionText.text   = $"Confusion:   {confusion:F2}";
            if (frustrationText != null)
                frustrationText.text = $"Frustration: {frustration:F2}";
            if (engagementText != null)
                engagementText.text  = $"Engagement:  {engagement:F2}";

            UpdateMarkerStatus(markerTracked);
        }

        private void UpdateMarkerStatus(bool tracked)
        {
            if (markerStatusText == null) return;
            markerStatusText.text  = tracked ? "✓ Marker tracked" : "✗ No marker";
            markerStatusText.color = tracked ? Color.green : Color.red;
        }

        private void ToggleDebugHud()
        {
            if (debugHudPanel == null) return;
            debugHudPanel.SetActive(!debugHudPanel.activeSelf);
        }

        // ── No-Marker Overlay API ────────────────────────────────────────────────
        /// <summary>Show/hide the "point camera at marker" overlay.</summary>
        public void SetNoMarkerOverlayVisible(bool visible)
        {
            if (noMarkerOverlay == null) return;
            noMarkerOverlay.SetActive(visible);
            // When marker is lost, also hide the step panel and hint
            if (stepPanel != null) stepPanel.SetActive(!visible);
            if (visible) HideHint();
        }
    }
}
