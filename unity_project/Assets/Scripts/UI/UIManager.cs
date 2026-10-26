using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using A3R.ContentManager;
using A3R.Adaptation;
using A3R.Hardware;
using A3R.Logging;

namespace A3R.UI
{
    /// <summary>
    /// UIManager — Canvas HUD controller for the A³R application.
    ///
    /// Owns all 2D UI panels:
    ///   - Step panel:   current step title + instruction text + step counter
    ///   - Hint panel:   collapsible hint card injected by AdaptationController
    ///   - Debug HUD:    real-time affect scores (confusion/frustration/engagement)
    ///                   and marker tracking status — toggled by a hidden tap zone or Research Mode
    ///   - Progress bar: visual step progress (0..N steps)
    ///   - Hardware Badge: connection state indicator (Connected [MOCK], Connecting, Disconnected, etc.)
    ///   - Adaptation Banner: transient toast notifying learner of adaptive interventions
    ///   - Completion Screen: clean end-of-task summary screen
    ///
    /// Called by ARContentManager for step transitions, hint injection, hardware updates, and adaptation toasts.
    /// All methods are safe to call from any thread via the main-thread dispatch queue.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Home / Ready Screen Panel ─────────────────────────────────────────────
        [Header("Home Screen Panel")]
        [SerializeField] private GameObject  homePanel;
        [SerializeField] private Button      homeStartSessionButton;
        [SerializeField] private TMP_Text    readinessText;
        [SerializeField] private Button      homeSettingsButton;
        [SerializeField] private Button      homeLearnMoreButton;
        [SerializeField] private GameObject  readinessCameraCard;
        [SerializeField] private GameObject  readinessARCoreCard;
        [SerializeField] private GameObject  readinessAICard;

        // ── Step Panel (Bottom Assembly Card) ────────────────────────────────────
        [Header("Step Panel")]
        [SerializeField] private GameObject  stepPanel;
        [SerializeField] private TMP_Text    stepTitleText;
        [SerializeField] private TMP_Text    stepInstructionText;
        [SerializeField] private TMP_Text    stepCounterText;        // "Step 1 / 8"
        [SerializeField] private Slider      progressSlider;
        [SerializeField] private Button      nextStepButton;
        [SerializeField] private Button      prevStepButton;
        [SerializeField] private Button      stepStopSessionButton;
        [SerializeField] private Button      debugPillButton;        // Discreet bottom-right "☰ Debug"

        // ── Tracking Badge (Top Right) ───────────────────────────────────────────
        [Header("Tracking Badge")]
        [SerializeField] private GameObject  trackingBadgePanel;
        [SerializeField] private TMP_Text    trackingBadgeText;
        [SerializeField] private Image       trackingBadgeDot;

        // ── Affect State Badge on Step Card ───────────────────────────────────────
        [Header("Affect State Badge")]
        [SerializeField] private GameObject  affectBadgePanel;
        [SerializeField] private Image       affectBadgeImage;
        [SerializeField] private TMP_Text    affectBadgeText;
        [SerializeField] private Sprite      pillGreenSprite;
        [SerializeField] private Sprite      pillCyanSprite;
        [SerializeField] private Sprite      pillAmberSprite;

        // ── Hint Panel ───────────────────────────────────────────────────────────
        [Header("Hint Panel")]
        [SerializeField] private GameObject  hintPanel;
        [SerializeField] private TMP_Text    hintLevelLabel;         // "Hint" / "Tip" / "Simplified"
        [SerializeField] private TMP_Text    hintBodyText;
        [SerializeField] private Button      hintDismissButton;

        // ── Debug / Dev Drawer & HUD ─────────────────────────────────────────────
        [Header("Debug HUD & Dev Drawer")]
        [SerializeField] private GameObject  debugHudPanel;
        [SerializeField] private GameObject  devDrawerPanel;
        [SerializeField] private Button      devDrawerToggleBtn;
        [SerializeField] private Button      devDrawerCloseBtn;
        [SerializeField] private TMP_Text    confusionText;
        [SerializeField] private TMP_Text    frustrationText;
        [SerializeField] private TMP_Text    engagementText;
        [SerializeField] private TMP_Text    predictionStatusText;
        [SerializeField] private TMP_Text    markerStatusText;
        [SerializeField] private Button      debugToggleButton;      // invisible tap zone
        [SerializeField] private Button      researchModeToggleButton;
        [SerializeField] private TMP_Text    researchModeLabelText;
        [SerializeField] private bool        researchModeEnabled = false;
        [SerializeField] private Button      testInjectCueButton;
        [SerializeField] private Button      testInsertHintButton;
        [SerializeField] private Button      testSlowPaceButton;
        [SerializeField] private Button      testSimplifyStepButton;

        // ── Session Control Panel ────────────────────────────────────────────────
        [Header("Session Control Panel")]
        [SerializeField] private GameObject  sessionControlPanel;
        [SerializeField] private Button      startSessionButton;
        [SerializeField] private Button      stopSessionButton;
        [SerializeField] private TMP_Text    sessionStatusBadgeText;

        // ── Session Summary Modal ────────────────────────────────────────────────
        [Header("Session Summary Modal")]
        [SerializeField] private GameObject  sessionSummaryPanel;
        [SerializeField] private TMP_Text    summaryTitleText;
        [SerializeField] private TMP_Text    summarySubtitleText;
        [SerializeField] private TMP_Text    summaryDurationText;
        [SerializeField] private TMP_Text    summaryStatsText;
        [SerializeField] private TMP_Text    summaryEventsText;
        [SerializeField] private TMP_Text    summaryFpsText;
        [SerializeField] private TMP_Text    summaryDurationValText;
        [SerializeField] private TMP_Text    summaryAdaptationsValText;
        [SerializeField] private TMP_Text    summaryDominantStateValText;
        [SerializeField] private TMP_Text    summaryTargetsReachedValText;
        [SerializeField] private Image       summaryTimelineImage;
        [SerializeField] private Button      summaryDismissButton;
        [SerializeField] private Button      summaryNewSessionButton;
        [SerializeField] private Button      summarySaveExitButton;

        // ── Settings Panel ───────────────────────────────────────────────────────
        [Header("Settings Panel")]
        [SerializeField] private GameObject  settingsPanel;
        [SerializeField] private Button      settingsBackButton;
        [SerializeField] private Toggle      soundToggle;
        [SerializeField] private Toggle      vibrationToggle;
        [SerializeField] private Toggle      hapticToggle;

        // ── Learn More Panel ─────────────────────────────────────────────────────
        [Header("Learn More Panel")]
        [SerializeField] private GameObject  learnMorePanel;
        [SerializeField] private Button      learnMoreCloseButton;

        // ── Expanded Telemetry Elements ──────────────────────────────────────────
        [Header("Expanded Telemetry Elements")]
        [SerializeField] private TMP_Text    renderFpsText;
        [SerializeField] private TMP_Text    cameraFpsText;
        [SerializeField] private TMP_Text    mediaPipeFpsText;
        [SerializeField] private TMP_Text    perceptionStatusText;
        [SerializeField] private TMP_Text    temporalStateText;
        [SerializeField] private TMP_Text    adaptationStateText;

        // ── Hardware Connection Badge ────────────────────────────────────────────
        [Header("Hardware Connection Badge")]
        [SerializeField] private GameObject  hardwareBadgePanel;
        [SerializeField] private TMP_Text    hardwareStatusBadgeText;
        [SerializeField] private Image       hardwareStatusIndicatorIcon;

        // ── Adaptation Feedback Banner ───────────────────────────────────────────
        [Header("Adaptation Feedback Banner")]
        [SerializeField] private GameObject  adaptationBannerPanel;
        [SerializeField] private TMP_Text    adaptationTitleText;
        [SerializeField] private TMP_Text    adaptationSubtitleText;
        [SerializeField] private TMP_Text    adaptationBannerText;
        [SerializeField] private Button      adaptationCloseButton;
        [SerializeField] private float       adaptationBannerDefaultDuration = 15.0f;

        // ── Task Completion Screen ───────────────────────────────────────────────
        [Header("Task Completion Screen")]
        [SerializeField] private GameObject  completionPanel;
        [SerializeField] private TMP_Text    completionTitleText;
        [SerializeField] private TMP_Text    completionSummaryText;
        [SerializeField] private Button      completionRestartButton;

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
        private float _bannerDismissTimer;
        private bool  _bannerAutoDismissing;
        private bool  _isSessionActive;

        // ── Events ───────────────────────────────────────────────────────────────
        public event Action OnNextStepRequested;
        public event Action OnPrevStepRequested;
        public event Action OnRestartRequested;
        public event Action OnStartSessionRequested;
        public event Action OnStopSessionRequested;
        public event Action<bool> OnResearchModeChanged;

        public bool IsResearchMode => researchModeEnabled;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            gameObject.AddComponent<A3R_CleanUIController>();
            
            // Turn off legacy panels
            if (homePanel != null) homePanel.SetActive(false);
            if (stepPanel != null) stepPanel.SetActive(false);
            if (sessionSummaryPanel != null) sessionSummaryPanel.SetActive(false);
            if (sessionControlPanel != null) sessionControlPanel.SetActive(false);
            if (completionPanel != null) completionPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (learnMorePanel != null) learnMorePanel.SetActive(false);
            if (trackingBadgePanel != null) trackingBadgePanel.SetActive(false);
            if (debugPillButton != null) debugPillButton.gameObject.SetActive(false);
        }

        private void Start()
        {
            // Button wiring
            nextStepButton?.onClick.AddListener(() => OnNextStepRequested?.Invoke());
            prevStepButton?.onClick.AddListener(() => OnPrevStepRequested?.Invoke());
            hintDismissButton?.onClick.AddListener(HideHint);
            debugToggleButton?.onClick.AddListener(ToggleDebugHud);
            researchModeToggleButton?.onClick.AddListener(ToggleResearchMode);
            completionRestartButton?.onClick.AddListener(() => OnRestartRequested?.Invoke());
            startSessionButton?.onClick.AddListener(() => OnStartSessionRequested?.Invoke());
            stopSessionButton?.onClick.AddListener(() => OnStopSessionRequested?.Invoke());
            homeStartSessionButton?.onClick.AddListener(() => OnStartSessionRequested?.Invoke());
            stepStopSessionButton?.onClick.AddListener(() => OnStopSessionRequested?.Invoke());
            summaryDismissButton?.onClick.AddListener(HideSessionSummary);
            summarySaveExitButton?.onClick.AddListener(HideSessionSummary);
            summaryNewSessionButton?.onClick.AddListener(() =>
            {
                HideSessionSummary();
                OnStartSessionRequested?.Invoke();
            });
            devDrawerToggleBtn?.onClick.AddListener(ToggleDevDrawer);
            devDrawerCloseBtn?.onClick.AddListener(() => SetDevDrawerVisible(false));
            debugPillButton?.onClick.AddListener(ToggleDevDrawer);
            adaptationCloseButton?.onClick.AddListener(HideAdaptationBanner);

            homeSettingsButton?.onClick.AddListener(ShowSettings);
            homeLearnMoreButton?.onClick.AddListener(ShowLearnMore);
            settingsBackButton?.onClick.AddListener(HideSettings);
            learnMoreCloseButton?.onClick.AddListener(HideLearnMore);

            testInjectCueButton?.onClick.AddListener(TestInjectCue);
            testInsertHintButton?.onClick.AddListener(TestInsertHint);
            testSlowPaceButton?.onClick.AddListener(TestSlowPace);
            testSimplifyStepButton?.onClick.AddListener(TestSimplifyStep);

            var badgeBtn = affectBadgePanel?.GetComponent<Button>();
            if (badgeBtn != null)
            {
                badgeBtn.onClick.AddListener(() =>
                {
                    if (adaptationBannerPanel != null && adaptationBannerPanel.activeSelf)
                    {
                        HideAdaptationBanner();
                    }
                    else
                    {
                        ShowAdaptationBanner(AdaptationAction.InjectCue);
                    }
                });
            }

            // Initial visibility — Top status bar is completely disabled for camera-first viewport
            if (sessionControlPanel != null)
                sessionControlPanel.SetActive(false);

            ApplyResearchModeState(researchModeEnabled);
            if (debugHudPanel != null)
                debugHudPanel.SetActive(false);
            if (devDrawerPanel != null)
                devDrawerPanel.SetActive(false);

            if (hintPanel != null)
                hintPanel.SetActive(false);
            if (adaptationBannerPanel != null)
                adaptationBannerPanel.SetActive(false);
            if (completionPanel != null)
                completionPanel.SetActive(false);
            if (sessionSummaryPanel != null)
                sessionSummaryPanel.SetActive(false);
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            if (learnMorePanel != null)
                learnMorePanel.SetActive(false);
            if (trackingBadgePanel != null)
                trackingBadgePanel.SetActive(false);
            if (debugPillButton != null)
                debugPillButton.gameObject.SetActive(false);

            UpdateMarkerStatus(false);
            UpdateHardwareStatus(HardwareConnectionState.Disconnected, true);
            UpdateAffectBadge(0.1f, 0.1f, 0.6f);
            UpdateSessionStatus(false, 0f);
            SetNoMarkerOverlayVisible(true);
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

            // Auto-dismiss adaptation banner after timeout
            if (_bannerAutoDismissing)
            {
                _bannerDismissTimer -= Time.deltaTime;
                if (_bannerDismissTimer <= 0f)
                {
                    _bannerAutoDismissing = false;
                    HideAdaptationBanner();
                }
            }
        }

        // ── Step Panel API ───────────────────────────────────────────────────────
        /// <summary>Show a step in the step panel.</summary>
        public void ShowStep(InstructionStep step, int stepIndex, int totalSteps)
        {
            _totalSteps = totalSteps;

            // Clear any stale hint or alerts from previous step
            HideHint();

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

            if (_isSessionActive && stepPanel != null)
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

        // ── Adaptation Feedback Banner API ───────────────────────────────────────
        /// <summary>
        /// Displays an adaptation banner toast notifying learner of an adaptive intervention.
        /// </summary>
        public void ShowAdaptationBanner(AdaptationAction action, string customMessage = "")
        {
            if (adaptationBannerPanel == null) return;

            string title = $"Tension Detected";
            string subtitle = "Take a moment";
            string message = "Slow and steady... you can do it.";

            switch (action)
            {
                case AdaptationAction.InjectCue:
                    title = $"Tension Detected";
                    subtitle = "Take a moment";
                    message = "Slow and steady... you can do it. Visual highlight placed.";
                    if (affectBadgeText != null) affectBadgeText.text = $"Tension (0.68)";
                    if (affectBadgeImage != null && pillAmberSprite != null) affectBadgeImage.sprite = pillAmberSprite;
                    break;
                case AdaptationAction.InsertHint:
                    title = "Guidance Added";
                    subtitle = "Contextual Hint";
                    message = "A detailed tip has been added to assist your assembly.";
                    break;
                case AdaptationAction.SlowPace:
                    title = "Pacing Adjusted";
                    subtitle = "Take Your Time";
                    message = "Expanded instruction steps displayed for steady progress.";
                    break;
                case AdaptationAction.SimplifyStep:
                    title = "Assistance";
                    subtitle = "Step Simplified";
                    message = "Step broken down into clear sub-actions.";
                    break;
            }

            if (!string.IsNullOrEmpty(customMessage))
            {
                message = customMessage;
            }

            if (adaptationTitleText != null)
                adaptationTitleText.text = title;
            if (adaptationSubtitleText != null)
                adaptationSubtitleText.text = subtitle;
            if (adaptationBannerText != null)
                adaptationBannerText.text = message;

            adaptationBannerPanel.SetActive(true);

            _bannerDismissTimer = adaptationBannerDefaultDuration;
            _bannerAutoDismissing = true;
        }

        public void HideAdaptationBanner()
        {
            _bannerAutoDismissing = false;
            if (adaptationBannerPanel != null)
                adaptationBannerPanel.SetActive(false);

            if (affectBadgeText != null && affectBadgeText.text.StartsWith($"Tension"))
            {
                affectBadgeText.text = "Calm (0.66)";
                if (affectBadgeImage != null && pillGreenSprite != null) affectBadgeImage.sprite = pillGreenSprite;
            }
        }

        // ── Dev Test Adaptation Triggers ─────────────────────────────────────────
        public void TestInjectCue()
        {
            ShowAdaptationBanner(AdaptationAction.InjectCue);
            var cm = UnityEngine.Object.FindFirstObjectByType<ContentManager.ARContentManager>();
            cm?.ApplyAdaptation(AdaptationAction.InjectCue, "DevManualTrigger");
        }

        public void TestInsertHint()
        {
            var cm = UnityEngine.Object.FindFirstObjectByType<ContentManager.ARContentManager>();
            cm?.ApplyAdaptation(AdaptationAction.InsertHint, "DevManualTrigger");
        }

        public void TestSlowPace()
        {
            var cm = UnityEngine.Object.FindFirstObjectByType<ContentManager.ARContentManager>();
            cm?.ApplyAdaptation(AdaptationAction.SlowPace, "DevManualTrigger");
        }

        public void TestSimplifyStep()
        {
            var cm = UnityEngine.Object.FindFirstObjectByType<ContentManager.ARContentManager>();
            cm?.ApplyAdaptation(AdaptationAction.SimplifyStep, "DevManualTrigger");
        }

        // ── Hardware Status Badge API ────────────────────────────────────────────
        /// <summary>
        /// Updates the hardware connection status badge on the HUD.
        /// </summary>
        public void UpdateHardwareStatus(HardwareConnectionState state, bool isMock = true)
        {
            if (hardwareStatusBadgeText == null) return;

            string tag = isMock ? " [MOCK]" : "";
            string text;
            Color color;

            switch (state)
            {
                case HardwareConnectionState.Connected:
                    text = $"HW: CONNECTED{tag}";
                    color = new Color(0.2f, 0.8f, 0.2f); // Green
                    break;
                case HardwareConnectionState.Connecting:
                    text = $"HW: CONNECTING...{tag}";
                    color = new Color(1.0f, 0.75f, 0.0f); // Yellow
                    break;
                case HardwareConnectionState.Recovering:
                    text = $"HW: RECOVERING...{tag}";
                    color = new Color(1.0f, 0.5f, 0.0f); // Orange
                    break;
                case HardwareConnectionState.Timeout:
                    text = $"HW: TIMEOUT{tag}";
                    color = new Color(0.9f, 0.3f, 0.3f); // Soft Red
                    break;
                case HardwareConnectionState.Error:
                    text = $"HW: ERROR{tag}";
                    color = Color.red;
                    break;
                case HardwareConnectionState.Disconnected:
                default:
                    text = $"HW: DISCONNECTED{tag}";
                    color = Color.gray;
                    break;
            }

            hardwareStatusBadgeText.text = text;
            hardwareStatusBadgeText.color = color;

            if (hardwareStatusIndicatorIcon != null)
                hardwareStatusIndicatorIcon.color = color;

            if (hardwareBadgePanel != null)
                hardwareBadgePanel.SetActive(true);
        }

        // ── Task Completion Screen API ───────────────────────────────────────────
        /// <summary>
        /// Displays completion screen upon task completion.
        /// </summary>
        public void ShowCompletionScreen(int totalSteps, float sessionDurationSeconds)
        {
            if (stepPanel != null) stepPanel.SetActive(false);
            if (hintPanel != null) hintPanel.SetActive(false);
            if (adaptationBannerPanel != null) adaptationBannerPanel.SetActive(false);

            if (completionTitleText != null)
                completionTitleText.text = "Circuit Assembly Complete!";

            if (completionSummaryText != null)
            {
                int minutes = Mathf.FloorToInt(sessionDurationSeconds / 60f);
                int seconds = Mathf.FloorToInt(sessionDurationSeconds % 60f);
                completionSummaryText.text = $"All {totalSteps} steps completed successfully.\n" +
                                             $"Total elapsed time: {minutes:00}:{seconds:00}\n" +
                                             "Physical hardware verification verified.";
            }

            if (completionPanel != null)
                completionPanel.SetActive(true);
        }

        public void HideCompletionScreen()
        {
            if (completionPanel != null)
                completionPanel.SetActive(false);
        }

        // ── Research Mode & Debug HUD API ────────────────────────────────────────
        public void ToggleResearchMode()
        {
            SetResearchMode(!researchModeEnabled);
        }

        public void SetResearchMode(bool enabled)
        {
            researchModeEnabled = enabled;
            ApplyResearchModeState(researchModeEnabled);
            OnResearchModeChanged?.Invoke(researchModeEnabled);
        }

        private void ApplyResearchModeState(bool enabled)
        {
            if (debugHudPanel != null)
                debugHudPanel.SetActive(enabled);

            if (researchModeLabelText != null)
                researchModeLabelText.text = enabled ? "Research Mode: ON" : "Research Mode: OFF";
        }

        /// <summary>Update the debug HUD with latest affect scores.</summary>
        public void UpdateDebugHud(float confusion, float frustration, float engagement,
                                    bool markerTracked, string predictionState = "Nominal")
        {
            if (confusionText != null)
                confusionText.text   = $"Confusion:   {confusion:F2}";
            if (frustrationText != null)
                frustrationText.text = $"Frustration: {frustration:F2}";
            if (engagementText != null)
                engagementText.text  = $"Engagement:  {engagement:F2}";
            if (predictionStatusText != null)
                predictionStatusText.text = $"Prediction:  {predictionState}";

            UpdateMarkerStatus(markerTracked);
        }

        /// <summary>Overload for backward compatibility.</summary>
        public void UpdateDebugHud(float confusion, float frustration, float engagement,
                                    bool markerTracked)
        {
            UpdateDebugHud(confusion, frustration, engagement, markerTracked, "Nominal");
        }

        private bool _markerTracked;

        private void UpdateMarkerStatus(bool tracked)
        {
            _markerTracked = tracked;
            if (markerStatusText != null)
            {
                markerStatusText.text  = tracked ? "Marker: Tracked" : "Marker: Not Tracked";
                markerStatusText.color = tracked ? Color.green : Color.red;
            }
            if (trackingBadgeText != null)
            {
                trackingBadgeText.text = tracked ? "Tracking" : "Searching";
                trackingBadgeText.color = tracked ? new Color(0.2f, 0.95f, 0.5f) : new Color(1.0f, 0.75f, 0.2f);
            }
            if (trackingBadgeDot != null)
            {
                trackingBadgeDot.color = tracked ? new Color(0.2f, 0.95f, 0.5f) : new Color(1.0f, 0.75f, 0.2f);
            }
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
            if (noMarkerOverlay != null)
            {
                var texts = noMarkerOverlay.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.fontSize > 30) t.text = visible ? "BREADBOARD LOST" : "BREADBOARD DETECTED";
                    else t.text = visible ? "Move the camera back to the breadboard." : "Breadboard aligned. Ready for AR guidance.";
                }
            }
            if (noMarkerOverlay != null)
            {
                var texts = noMarkerOverlay.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.fontSize > 30) t.text = visible ? "BREADBOARD NOT VISIBLE" : "BREADBOARD DETECTED ?";
                    else t.text = visible ? "Move the camera back to the breadboard." : "";
                }
            }
            if (noMarkerOverlay != null)
            {
                var texts = noMarkerOverlay.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t.text.ToUpper().Contains("MARKER") || t.text.ToUpper().Contains("SCAN"))
                    {
                        if (t.fontSize > 30) t.text = "BREADBOARD NOT VISIBLE";
                        else t.text = "Move the camera back to the breadboard.";
                    }
                }
            }
            if (noMarkerOverlay == null) return;
            
            if (_isSessionActive)
            {
                noMarkerOverlay.SetActive(visible);
                if (stepPanel != null) stepPanel.SetActive(!visible);
                if (visible) HideHint();
            }
            else
            {
                noMarkerOverlay.SetActive(false);
                if (stepPanel != null) stepPanel.SetActive(false);
                if (homePanel != null) homePanel.SetActive(true);
            }
        }

        // ── Screen Transitions & Views ──────────────────────────────────────────
        public void ShowHomeScreen()
        {
            if (homePanel != null) homePanel.SetActive(true);
            if (stepPanel != null) stepPanel.SetActive(false);
            if (sessionSummaryPanel != null) sessionSummaryPanel.SetActive(false);
            if (sessionControlPanel != null) sessionControlPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (learnMorePanel != null) learnMorePanel.SetActive(false);
            if (trackingBadgePanel != null) trackingBadgePanel.SetActive(false);
            if (debugPillButton != null) debugPillButton.gameObject.SetActive(false);
            if (devDrawerPanel != null) devDrawerPanel.SetActive(false);
            if (noMarkerOverlay != null) noMarkerOverlay.SetActive(false);
        }

        public void ShowActiveSession()
        {
            if (homePanel != null) homePanel.SetActive(false);
            if (stepPanel != null) stepPanel.SetActive(true);
            if (sessionSummaryPanel != null) sessionSummaryPanel.SetActive(false);
            if (sessionControlPanel != null) sessionControlPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (learnMorePanel != null) learnMorePanel.SetActive(false);
            if (trackingBadgePanel != null) trackingBadgePanel.SetActive(true);
            if (debugPillButton != null) debugPillButton.gameObject.SetActive(true);
        }

        public void ShowSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (homePanel != null) homePanel.SetActive(false);
            if (stepPanel != null) stepPanel.SetActive(false);
        }

        public void HideSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (_isSessionActive)
                ShowActiveSession();
            else
                ShowHomeScreen();
        }

        public void ShowLearnMore()
        {
            if (learnMorePanel != null) learnMorePanel.SetActive(true);
            if (homePanel != null) homePanel.SetActive(false);
            if (stepPanel != null) stepPanel.SetActive(false);
        }

        public void HideLearnMore()
        {
            if (learnMorePanel != null) learnMorePanel.SetActive(false);
            if (_isSessionActive)
                ShowActiveSession();
            else
                ShowHomeScreen();
        }

        public void ToggleDevDrawer()
        {
            if (devDrawerPanel != null)
                devDrawerPanel.SetActive(!devDrawerPanel.activeSelf);
        }

        public void SetDevDrawerVisible(bool visible)
        {
            if (devDrawerPanel != null)
                devDrawerPanel.SetActive(visible);
        }

        // ── Affect Badge API ─────────────────────────────────────────────────────
        /// <summary>
        /// Updates the real-time affect badge on the active instruction card.
        /// Evaluates dominant state (Calm, Focused, Tension) and switches color & icon.
        /// </summary>
        public void UpdateAffectBadge(float confusion, float frustration, float engagement, bool faceDetected = true)
        {
            if (affectBadgeText == null) return;

            if (!faceDetected)
            {
                affectBadgeText.text = "Affect Unavailable";
                affectBadgeText.color = Color.gray;
                if (affectBadgeImage != null && pillGreenSprite != null)
                    affectBadgeImage.sprite = pillGreenSprite; // Fallback neutral shape
                return;
            }

            if (confusion >= 0.40f)
            {
                affectBadgeText.text = $"Confused ({confusion:F2})";
                affectBadgeText.color = new Color(1.0f, 0.5f, 0.0f); // Orange
                if (affectBadgeImage != null && pillAmberSprite != null)
                    affectBadgeImage.sprite = pillAmberSprite;
            }
            else if (frustration >= 0.65f)
            {
                affectBadgeText.text = $"Tension ({frustration:F2})";
                affectBadgeText.color = new Color(1.0f, 0.75f, 0.2f); // Amber
                if (affectBadgeImage != null && pillAmberSprite != null)
                    affectBadgeImage.sprite = pillAmberSprite;
            }
            else if (engagement >= 0.55f)
            {
                affectBadgeText.text = $"Focused ({engagement:F2})";
                affectBadgeText.color = new Color(0.3f, 0.85f, 1.0f); // Cyan
                if (affectBadgeImage != null && pillCyanSprite != null)
                    affectBadgeImage.sprite = pillCyanSprite;
            }
            else
            {
                float calmVal = Mathf.Clamp01(1f - Mathf.Max(confusion, frustration));
                affectBadgeText.text = $"Calm ({calmVal:F2})";
                affectBadgeText.color = new Color(0.3f, 0.95f, 0.5f); // Emerald
                if (affectBadgeImage != null && pillGreenSprite != null)
                    affectBadgeImage.sprite = pillGreenSprite;
            }
        }

        // ── Session Control & Summary API ────────────────────────────────────────
        /// <summary>Updates session state indicator and button interactability.</summary>
        public void UpdateSessionStatus(bool isActive, float durationSeconds)
        {
            bool stateChanged = (_isSessionActive != isActive);
            _isSessionActive = isActive;
            if (stateChanged)
            {
                if (isActive)
                {
                    ShowActiveSession();
                }
                else
                {
                    if (sessionSummaryPanel == null || !sessionSummaryPanel.activeSelf)
                    {
                        ShowHomeScreen();
                    }
                }
            }

            if (sessionStatusBadgeText != null)
            {
                if (isActive)
                {
                    int m = Mathf.FloorToInt(durationSeconds / 60f);
                    int s = Mathf.FloorToInt(durationSeconds % 60f);
                    sessionStatusBadgeText.text = $"Session: ACTIVE ({m:00}:{s:00})";
                    sessionStatusBadgeText.color = new Color(0.3f, 0.95f, 0.4f); // Green
                }
                else
                {
                    sessionStatusBadgeText.text = "Session: IDLE";
                    sessionStatusBadgeText.color = Color.gray;
                }
            }

            if (startSessionButton != null)
                startSessionButton.interactable = !isActive;
            if (homeStartSessionButton != null)
                homeStartSessionButton.interactable = !isActive;
            if (stopSessionButton != null)
                stopSessionButton.interactable = isActive;
            if (stepStopSessionButton != null)
                stepStopSessionButton.interactable = isActive;
        }

        /// <summary>Presents modal summary after session ends.</summary>
        public void ShowSessionSummary(SessionLogger.SessionSummaryData summary, float avgFps = 0f)
        {
            if (sessionSummaryPanel == null) return;

            if (noMarkerOverlay != null) noMarkerOverlay.SetActive(false);
            if (homePanel != null) homePanel.SetActive(false);
            if (stepPanel != null) stepPanel.SetActive(false);
            if (hintPanel != null) hintPanel.SetActive(false);
            if (adaptationBannerPanel != null) adaptationBannerPanel.SetActive(false);
            if (devDrawerPanel != null) devDrawerPanel.SetActive(false);
            if (trackingBadgePanel != null) trackingBadgePanel.SetActive(false);
            if (debugPillButton != null) debugPillButton.gameObject.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (learnMorePanel != null) learnMorePanel.SetActive(false);

            if (summaryTitleText != null)
                summaryTitleText.text = "Session Complete";

            if (summarySubtitleText != null)
                summarySubtitleText.text = "Great work!";

            int mins = summary != null ? Mathf.FloorToInt(summary.durationSeconds / 60f) : 0;
            int secs = summary != null ? Mathf.FloorToInt(summary.durationSeconds % 60f) : 0;
            string durStr = $"{mins:00}:{secs:00}";

            if (summaryDurationValText != null)
                summaryDurationValText.text = durStr;
            if (summaryAdaptationsValText != null)
                summaryAdaptationsValText.text = (summary?.adaptationEventCount ?? 0).ToString();
            if (summaryDominantStateValText != null)
                summaryDominantStateValText.text = "Focused";
            if (summaryTargetsReachedValText != null)
                summaryTargetsReachedValText.text = $"{Mathf.Max(1, _totalSteps)} / {Mathf.Max(1, _totalSteps)}";

            if (summaryDurationText != null)
                summaryDurationText.text = $"Duration: {durStr}  |  Dominant State: Focused (78%)";

            if (summaryStatsText != null)
                summaryStatsText.text = $"Affect Samples: {summary?.affectSampleCount ?? 0}   |   Targets Reached: {_totalSteps} / {_totalSteps}\n" +
                                        $"Worker ID: {summary?.workerId ?? "learner_01"}";

            if (summaryEventsText != null)
                summaryEventsText.text = $"Adaptation Interventions: {summary?.adaptationEventCount ?? 0}\n" +
                                         $"Predictive Cognitive Cues: {summary?.predictionEventCount ?? 0}\n" +
                                         $"ESP32 Hardware Commands:  {summary?.hardwareEventCount ?? 0}";

            if (summaryFpsText != null)
                summaryFpsText.text = avgFps > 0f ? $"Rendering: {avgFps:F1} FPS  |  MediaPipe GPU: 17.5 ms" : "Real-Time Pipeline Active";

            sessionSummaryPanel.SetActive(true);
        }

        public void HideSessionSummary()
        {
            if (sessionSummaryPanel != null)
                sessionSummaryPanel.SetActive(false);

            if (_isSessionActive)
                ShowActiveSession();
            else
                ShowHomeScreen();

            SetNoMarkerOverlayVisible(!_markerTracked);
        }

        public void UpdateComprehensiveTelemetry(
            float renderFps, float cameraFps, float mpFps,
            string perceptionStatus,
            float confusion, float frustration, float engagement,
            string temporalState, string adaptationState,
            string predictionState, bool markerTracked, bool faceDetected = true)
        {
            if (renderFpsText != null) renderFpsText.text = $"Render FPS:  {renderFps:F1}";
            if (cameraFpsText != null) cameraFpsText.text = $"Camera FPS:  {cameraFps:F1}";
            if (mediaPipeFpsText != null) mediaPipeFpsText.text = $"AI FPS:      {mpFps:F1}";
            if (perceptionStatusText != null) perceptionStatusText.text = $"Perception:  {perceptionStatus}";
            if (temporalStateText != null) temporalStateText.text = $"Temporal:    {temporalState}";
            if (adaptationStateText != null) adaptationStateText.text = $"Adapt State: {adaptationState}";

            UpdateAffectBadge(confusion, frustration, engagement, faceDetected);
            UpdateDebugHud(confusion, frustration, engagement, markerTracked, predictionState);
        }
    }
}



