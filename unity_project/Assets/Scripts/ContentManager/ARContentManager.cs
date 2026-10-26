using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using A3R.ARTracking;
using A3R.Adaptation;
using A3R.ARGuidance;
using A3R.Hardware;
using A3R.UI;
using A3R.Affect;
using A3R.Logging;

namespace A3R.ContentManager
{
    /// <summary>
    /// ARContentManager — Module 4 of the A³R system.
    ///
    /// Manages the instructional step sequence loaded from led_blink_task.json.
    /// Renders the current step's AR overlay (text panel, component highlight, arrow cue)
    /// as children of the content root provided by ARImageTracker or BreadboardARRoot.
    /// </summary>
    public class ARContentManager : MonoBehaviour
    {
        [Header("Task Data")]
        [SerializeField] private TextAsset taskJsonFile;

        [Header("AR Tracker Reference")]
        [SerializeField] private ARImageTracker arImageTracker;

        [Header("UI Panels (children of ContentRoot prefab)")]
        [SerializeField] private GameObject stepPanel;
        [SerializeField] private TextMeshProUGUI stepNumberText;
        [SerializeField] private TextMeshProUGUI stepTitleText;
        [SerializeField] private TextMeshProUGUI stepInstructionText;
        [SerializeField] private TextMeshProUGUI stepDetailText;
        [SerializeField] private GameObject hintPanel;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button nextStepButton;
        [SerializeField] private Button prevStepButton;

        [Header("AR Visual Elements")]
        [SerializeField] private GameObject arCueArrow;
        [SerializeField] private GameObject highlightMarker;
        [SerializeField] private ARGuidanceController arGuidanceController;

        [Header("UI & Hardware Systems")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private MockESP32Bridge hardwareBridge;

        [Header("Debug HUD")]
        [SerializeField] private GameObject debugHUD;
        [SerializeField] private TextMeshProUGUI debugAffectText;

        [Header("Canonical Identity & Prediction")]
        [SerializeField] private string workerId = "learner_01";
        [SerializeField] private Affect.AffectPredictor affectPredictor;

        // ── Canonical Identity ──────────────────────────────────────────────────
        public CanonicalEvent CurrentCanonicalEvent { get; private set; }

        // ── AR Guidance Visual System ─────────────────────────────────────────
        public ARGuidanceController ARGuidanceController => arGuidanceController;
        public Affect.AffectPredictor AffectPredictor => affectPredictor;
        public MockESP32Bridge HardwareBridge => hardwareBridge;
        public UIManager UIManager => uiManager;

        // ── Events ───────────────────────────────────────────────────────────
        public event Action<int, InstructionStep> OnStepChanged;
        public event Action OnTaskComplete;

        // ── Private State ────────────────────────────────────────────────────
        private InstructionTask _task;
        private int             _currentStepIndex;
        private bool            _slowPaceActive;
        private bool            _simplifyActive;
        private bool            _hintActive;
        private bool            _cueActive;
        private float           _sessionStartTime;
        private Transform       _activeContentRoot;

        public bool IsHintActive => _hintActive;
        public bool IsCueActive  => _cueActive;
        public bool IsSlowPaceActive => _slowPaceActive;
        public bool IsSimplifyActive => _simplifyActive;

        // ── Unity Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            if (arImageTracker == null)
                arImageTracker = GetComponent<ARImageTracker>() ?? FindFirstObjectByType<ARImageTracker>();
            if (affectPredictor == null)
                affectPredictor = GetComponent<Affect.AffectPredictor>() ?? FindFirstObjectByType<Affect.AffectPredictor>();
            if (uiManager == null)
                uiManager = GetComponent<UIManager>() ?? FindFirstObjectByType<UIManager>();
            if (hardwareBridge == null)
                hardwareBridge = GetComponent<MockESP32Bridge>() ?? FindFirstObjectByType<MockESP32Bridge>();
            if (arGuidanceController == null)
                arGuidanceController = GetComponent<ARGuidanceController>() ?? FindFirstObjectByType<ARGuidanceController>();

            if (taskJsonFile == null)
                taskJsonFile = Resources.Load<TextAsset>("led_blink_task");

            _sessionStartTime = Time.time;
        }

        private void Start()
        {
            LoadTask();
            SetupButtonCallbacks();
            HideAllAdaptationOverlays();

            if (arImageTracker != null)
            {
                arImageTracker.OnMarkerDetected += OnMarkerDetected;
                arImageTracker.OnMarkerLost     += OnMarkerLost;
            }

            var markerless = FindFirstObjectByType<A3R.ARTracking.A3R_MarkerlessFoundationController>();
            if (markerless != null)
            {
                markerless.OnBreadboardPlaced += OnBreadboardPlaced;
            }

            if (uiManager != null)
            {
                uiManager.OnNextStepRequested += NextStep;
                uiManager.OnPrevStepRequested += PreviousStep;
                uiManager.OnRestartRequested  += RestartTask;
                uiManager.OnStartSessionRequested += HandleStartSession;
                uiManager.OnStopSessionRequested  += HandleStopSession;
            }

            if (hardwareBridge != null)
            {
                hardwareBridge.OnConnectionStateChanged += OnHardwareConnectionStateChanged;
                uiManager?.UpdateHardwareStatus(hardwareBridge.CurrentState, hardwareBridge.IsMock);
            }

            uiManager?.ShowHomeScreen();
        }

        private void Update()
        {
            var logger = GetComponent<SessionLogger>() ?? FindFirstObjectByType<SessionLogger>();
            if (logger != null && uiManager != null)
            {
                uiManager.UpdateSessionStatus(logger.IsSessionActive, logger.CurrentSessionDurationSeconds);
            }
        }

        private void OnDestroy()
        {
            if (arImageTracker != null)
            {
                arImageTracker.OnMarkerDetected -= OnMarkerDetected;
                arImageTracker.OnMarkerLost     -= OnMarkerLost;
            }

            var markerless = FindFirstObjectByType<A3R.ARTracking.A3R_MarkerlessFoundationController>();
            if (markerless != null)
            {
                markerless.OnBreadboardPlaced -= OnBreadboardPlaced;
            }

            if (uiManager != null)
            {
                uiManager.OnNextStepRequested -= NextStep;
                uiManager.OnPrevStepRequested -= PreviousStep;
                uiManager.OnRestartRequested  -= RestartTask;
                uiManager.OnStartSessionRequested -= HandleStartSession;
                uiManager.OnStopSessionRequested  -= HandleStopSession;
            }

            if (hardwareBridge != null)
            {
                hardwareBridge.OnConnectionStateChanged -= OnHardwareConnectionStateChanged;
            }
        }

        private void HandleStartSession()
        {
            var logger = GetComponent<SessionLogger>() ?? FindFirstObjectByType<SessionLogger>();
            logger?.StartNewSession();
            var adapt = GetComponent<AdaptationController>() ?? FindFirstObjectByType<AdaptationController>();
            adapt?.ResetSession();
            RestartTask();
            uiManager?.HideSessionSummary();
            uiManager?.UpdateSessionStatus(true, 0f);
            Debug.Log("[ARContentManager] Explicit session start triggered from UI.");
        }

        private void HandleStopSession()
        {
            var logger = GetComponent<SessionLogger>() ?? FindFirstObjectByType<SessionLogger>();
            var summary = logger?.EndCurrentSession();
            uiManager?.UpdateSessionStatus(false, summary?.durationSeconds ?? 0f);
            if (summary != null)
            {
                var scorer = GetComponent<AffectScorer>() ?? FindFirstObjectByType<AffectScorer>();
                uiManager?.ShowSessionSummary(summary, scorer != null ? scorer.RenderFps : 60f);
            }
            Debug.Log("[ARContentManager] Explicit session stop triggered from UI.");
        }

        private void OnHardwareConnectionStateChanged(HardwareConnectionState oldState, HardwareConnectionState newState)
        {
            uiManager?.UpdateHardwareStatus(newState, hardwareBridge != null ? hardwareBridge.IsMock : true);
        }

        // ── Adaptation Controller Integration ────────────────────────────────────
        public void ApplyAdaptation(AdaptationAction action, string triggeringBehavior = "ConfusionStruggle")
        {
            if (_task == null || _currentStepIndex >= _task.steps.Length) return;

            var step = _task.steps[_currentStepIndex];
            Debug.Log($"[ARContentManager] Applying adaptation: {action} at step {step.id} (behavior={triggeringBehavior})");

            if (CurrentCanonicalEvent != null)
            {
                CurrentCanonicalEvent.behavior = triggeringBehavior;
                affectPredictor?.NotifyActualEventFired(triggeringBehavior, CurrentCanonicalEvent);
            }

            if (action != AdaptationAction.InjectCue)
            {
                uiManager?.ShowAdaptationBanner(action);
            }

            if (action == AdaptationAction.SlowPace || triggeringBehavior == "FrustrationDisengage")
            {
                hardwareBridge?.SetLED(LedIdentifier.Led2, LedState.On);
            }

            switch (action)
            {
                case AdaptationAction.SimplifyStep:
                    ApplySimplifyStep(step);
                    break;
                case AdaptationAction.InjectCue:
                    ApplyInjectCue(step);
                    break;
                case AdaptationAction.SlowPace:
                    ApplySlowPace(step);
                    break;
                case AdaptationAction.InsertHint:
                    ApplyInsertHint(step);
                    break;
            }
        }

        // ── Step Navigation ──────────────────────────────────────────────────────
        public void NextStep()
        {
            if (_task == null) return;
            if (_currentStepIndex >= _task.steps.Length - 1)
            {
                OnTaskComplete?.Invoke();
                arGuidanceController?.OnTaskComplete();
                uiManager?.ShowCompletionScreen(_task.steps.Length, Time.time - _sessionStartTime);
                hardwareBridge?.ResetCircuit();
                return;
            }
            affectPredictor?.InvalidatePendingPrediction();
            _currentStepIndex++;
            HideAllAdaptationOverlays();
            DisplayCurrentStep();
        }

        public void PreviousStep()
        {
            if (_currentStepIndex <= 0) return;
            affectPredictor?.InvalidatePendingPrediction();
            _currentStepIndex--;
            HideAllAdaptationOverlays();
            DisplayCurrentStep();
        }

        public void RestartTask()
        {
            _currentStepIndex = 0;
            _sessionStartTime = Time.time;
            HideAllAdaptationOverlays();
            uiManager?.HideCompletionScreen();
            hardwareBridge?.ResetCircuit();
            DisplayCurrentStep();
        }

        public int CurrentStepIndex => _currentStepIndex;
        public int TotalSteps       => _task?.steps?.Length ?? 0;
        public InstructionStep CurrentStep => (_task != null && _currentStepIndex < _task.steps.Length)
                                              ? _task.steps[_currentStepIndex] : null;

        // ── Private Methods ──────────────────────────────────────────────────────
        private void LoadTask()
        {
            if (taskJsonFile == null)
            {
                Debug.LogError("[ARContentManager] No task JSON file assigned!");
                return;
            }

            try
            {
                _task = JsonUtility.FromJson<InstructionTask>(taskJsonFile.text);
                _currentStepIndex = 0;
                Debug.Log($"[ARContentManager] Loaded task: {_task.task_name} ({_task.steps.Length} steps)");
                DisplayCurrentStep();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARContentManager] Failed to parse task JSON: {ex.Message}");
            }
        }

        private void DisplayCurrentStep()
        {
            if (_task == null || _currentStepIndex >= _task.steps.Length) return;

            var step = _task.steps[_currentStepIndex];
            FindFirstObjectByType<AffectScorer>()?.SetCurrentStepId(step.id);

            string objId = !string.IsNullOrEmpty(step.highlight_component) ? step.highlight_component : "breadboard";
            string pdi = !string.IsNullOrEmpty(step.ar_arrow_target) ? step.ar_arrow_target : "workbench_marker";
            string wid = !string.IsNullOrEmpty(workerId) ? workerId : "learner_01";

            CurrentCanonicalEvent = new CanonicalEvent(
                sessionId: Guid.NewGuid().ToString("N"),
                workerId: wid,
                stepId: !string.IsNullOrEmpty(step.id) ? step.id : "step_unknown",
                stepIndex: _currentStepIndex,
                objectId: objId,
                pdiTarget: pdi,
                behavior: "NormalProgress"
            );

            if (stepNumberText    != null) stepNumberText.text    = $"Step {step.step_number}/{_task.steps.Length}";
            if (stepTitleText     != null) stepTitleText.text     = step.title;
            if (stepInstructionText != null) stepInstructionText.text = step.instruction;
            if (stepDetailText    != null)
            {
                stepDetailText.text    = _slowPaceActive ? step.detail : "";
                stepDetailText.gameObject.SetActive(_slowPaceActive);
            }

            uiManager?.ShowStep(step, _currentStepIndex, _task.steps.Length);

            if (hardwareBridge != null && hardwareBridge.IsConnected)
            {
                hardwareBridge.SendCommand(new HardwareCommand(
                    HardwareCommandType.StepSync,
                    LedIdentifier.Led1,
                    LedState.Off,
                    1000,
                    step.id
                ));
            }

            if (step.id == "verify_blink" && hardwareBridge != null)
            {
                hardwareBridge.BlinkLED(LedIdentifier.Led1, 1000);
            }

            if (prevStepButton != null) prevStepButton.interactable = _currentStepIndex > 0;
            if (nextStepButton != null) nextStepButton.interactable = true;

            OnStepChanged?.Invoke(_currentStepIndex, step);
        }

        private void SetupButtonCallbacks()
        {
            if (nextStepButton != null) nextStepButton.onClick.AddListener(NextStep);
            if (prevStepButton != null) prevStepButton.onClick.AddListener(PreviousStep);
        }

        private void HideAllAdaptationOverlays()
        {
            _slowPaceActive  = false;
            _simplifyActive  = false;
            _hintActive      = false;
            _cueActive       = false;

            if (hintPanel     != null) hintPanel.SetActive(false);
            if (arCueArrow    != null) arCueArrow.SetActive(false);
            if (highlightMarker != null) highlightMarker.SetActive(false);

            arGuidanceController?.ResetToNormalGuidance();
        }

        // ── Adaptation Implementations ───────────────────────────────────────────
        private void ApplySimplifyStep(InstructionStep step)
        {
            _simplifyActive = true;
            if (stepInstructionText != null && step.adaptation_hints != null)
                stepInstructionText.text = step.adaptation_hints.SimplifyStep;
        }

        private void ApplyInjectCue(InstructionStep step)
        {
            _cueActive = true;
            
            var oldTutorial = UnityEngine.Object.FindFirstObjectByType<A3R.ARGuidance.A3R_ResistorTutorialController>();
            if (oldTutorial != null)
            {
                if (oldTutorial.CurrentState == A3R.ARGuidance.ResistorTutorialState.RESISTOR_TARGET)
                    oldTutorial.TransitionToTarget();
                else if (oldTutorial.CurrentState == A3R.ARGuidance.ResistorTutorialState.LED_TARGET)
                    oldTutorial.TransitionToLedTarget();
                else if (oldTutorial.CurrentState == A3R.ARGuidance.ResistorTutorialState.ESP32_TARGET)
                    oldTutorial.TransitionToEsp32Target();
                else if (oldTutorial.CurrentState == A3R.ARGuidance.ResistorTutorialState.WIRE1_TARGET)
                    oldTutorial.TransitionToWire1Target();
                else if (oldTutorial.CurrentState == A3R.ARGuidance.ResistorTutorialState.WIRE2_TARGET)
                    oldTutorial.TransitionToWire2Target();
                else if (oldTutorial.CurrentState == A3R.ARGuidance.ResistorTutorialState.WIRE3_TARGET)
                    oldTutorial.TransitionToWire3Target();
            }
        }

        private void ApplySlowPace(InstructionStep step)
        {
            _slowPaceActive = true;
            if (stepDetailText != null)
            {
                stepDetailText.text = _simplifyActive
                    ? step.adaptation_hints?.SlowPace ?? step.detail
                    : step.detail;
                stepDetailText.gameObject.SetActive(true);
            }
        }

        private void ApplyInsertHint(InstructionStep step)
        {
            _hintActive = true;
            if (hintPanel != null)
            {
                hintPanel.SetActive(true);
                if (hintText != null && step.adaptation_hints != null)
                    hintText.text = step.adaptation_hints.InsertHint;
            }
        }

        private void PositionCueArrow(string targetName)
        {
            if (string.IsNullOrEmpty(targetName) || arCueArrow == null) return;
            Transform root = _activeContentRoot ?? arImageTracker?.ContentRoot;
            if (root != null)
            {
                string resolvedName = targetName;
                if (arGuidanceController != null)
                {
                    resolvedName = arGuidanceController.GetTargetAlias(targetName);
                }

                var target = root.Find(resolvedName);
                if (target == null) target = root.Find("TargetAnchors/" + resolvedName);
                if (target != null)
                    arCueArrow.transform.position = target.position + Vector3.up * 0.05f;
            }
        }

        // ── Marker & Placement Events ──────────────────────────────────────────
        private void OnBreadboardPlaced(Transform root)
        {
            Transform tutorialVisuals = root.Find("TutorialVisuals") ?? root;
            OnMarkerDetected(tutorialVisuals);
        }

        private void OnMarkerDetected(Transform contentRoot)
        {
            _activeContentRoot = contentRoot;
            if (_currentStepIndex == 0 && stepTitleText != null)
            {
                stepTitleText.text = "BREADBOARD DETECTED \u2713";
            }
            if (stepPanel != null) stepPanel.SetActive(true);
            uiManager?.SetNoMarkerOverlayVisible(false);
            arGuidanceController?.OnMarkerDetected(contentRoot);
            DisplayCurrentStep();
            Debug.Log("[ARContentManager] Marker detected -> showing AR content.");
        }

        private void OnMarkerLost()
        {
            if (stepPanel != null) stepPanel.SetActive(false);
            if (hintPanel != null) hintPanel.SetActive(false);
            if (arCueArrow != null) arCueArrow.SetActive(false);
            if (highlightMarker != null) highlightMarker.SetActive(false);
            uiManager?.SetNoMarkerOverlayVisible(true);
            arGuidanceController?.OnMarkerLost();
            HideAllAdaptationOverlays();
            Debug.Log("[ARContentManager] Marker lost -> hiding AR content.");
        }

        // ── Debug HUD ────────────────────────────────────────────────────────────
        public void UpdateDebugHUD(float confusion, float frustration, float engagement, bool markerTracked)
        {
            if (debugAffectText != null)
            {
                debugAffectText.text = $"Confusion:   {confusion:F3}\n" +
                                       $"Frustration: {frustration:F3}\n" +
                                       $"Engagement:  {engagement:F3}\n" +
                                       $"Marker: {(markerTracked ? "TRACKED" : "LOST")}\n" +
                                       $"Step: {_currentStepIndex + 1}/{TotalSteps}";
            }

            string predState = (affectPredictor != null && affectPredictor.HasPendingPrediction)
                ? "ANTICIPATING"
                : (affectPredictor != null ? affectPredictor.PredictionState : "NOMINAL");

            var scorer = GetComponent<AffectScorer>() ?? FindFirstObjectByType<AffectScorer>();
            var bridge = GetComponent<FaceLandmarkBridge>() ?? FindFirstObjectByType<FaceLandmarkBridge>();
            var adapt = GetComponent<AdaptationController>() ?? FindFirstObjectByType<AdaptationController>();

            float renderFps = scorer != null ? scorer.RenderFps : 60f;
            float camFps = bridge != null ? bridge.CameraFps : 30f;
            float mpFps = bridge != null ? bridge.MediaPipeFps : 5f;
            string perception = bridge != null && bridge.GetCurrentLandmarks() != null
                ? $"Active (478 pts, {bridge.LastInferenceLatencyMs:F1}ms)"
                : (bridge != null ? bridge.Mode.ToString() : "N/A");
            string temporal = adapt != null
                ? (adapt.IsInElevatedState ? $"Elevated ({adapt.DwellTimer:F1}s/{adapt.DwellTimeRequired:F1}s)" : "Nominal")
                : "Idle";
            string adaptState = arGuidanceController != null ? arGuidanceController.CurrentState.ToString() : "Normal";

            bool faceDetected = scorer != null && scorer.FaceDetected;
            
            uiManager?.UpdateComprehensiveTelemetry(
                renderFps, camFps, mpFps,
                perception,
                confusion, frustration, engagement,
                temporal, adaptState, predState, markerTracked, faceDetected);
        }
    }
}
