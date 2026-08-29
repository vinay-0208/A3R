using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using A3R.ARTracking;
using A3R.Adaptation;

namespace A3R.ContentManager
{
    /// <summary>
    /// ARContentManager — Module 4 of the A³R system.
    ///
    /// Manages the instructional step sequence loaded from led_blink_task.json.
    /// Renders the current step's AR overlay (text panel, component highlight, arrow cue)
    /// as children of the content root provided by ARImageTracker.
    ///
    /// Subscribes to AdaptationController events and mutates rendered content:
    ///   SimplifyStep → show detailed sub-step breakdown
    ///   InjectCue    → enable AR arrow pointing at the target component
    ///   SlowPace     → expand detail text + slow auto-advance timer
    ///   InsertHint   → show hint panel with contextual guidance
    ///
    /// All displayed content is parented to the content root (anchored to the marker).
    /// </summary>
    public class ARContentManager : MonoBehaviour
    {
        // ── Inspector References ────────────────────────────────────────────────
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

        [Header("Debug HUD")]
        [SerializeField] private GameObject debugHUD;
        [SerializeField] private TextMeshProUGUI debugAffectText;

        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>Fired when the step advances. Args: new step index, step data.</summary>
        public event Action<int, InstructionStep> OnStepChanged;

        /// <summary>Fired when all steps are complete.</summary>
        public event Action OnTaskComplete;

        // ── Private State ────────────────────────────────────────────────────────
        private InstructionTask _task;
        private int             _currentStepIndex;
        private bool            _slowPaceActive;
        private bool            _simplifyActive;
        private bool            _hintActive;
        private bool            _cueActive;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
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
        }

        private void OnDestroy()
        {
            if (arImageTracker != null)
            {
                arImageTracker.OnMarkerDetected -= OnMarkerDetected;
                arImageTracker.OnMarkerLost     -= OnMarkerLost;
            }
        }

        // ── Adaptation Controller Integration ────────────────────────────────────
        /// <summary>
        /// Called by AdaptationController when an adaptation action is triggered.
        /// Subscribe in AdaptationController.cs via ARContentManager.ApplyAdaptation.
        /// </summary>
        public void ApplyAdaptation(AdaptationAction action)
        {
            if (_task == null || _currentStepIndex >= _task.steps.Length) return;

            var step = _task.steps[_currentStepIndex];
            Debug.Log($"[ARContentManager] Applying adaptation: {action} at step {step.id}");

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
                return;
            }
            _currentStepIndex++;
            HideAllAdaptationOverlays();
            DisplayCurrentStep();
        }

        public void PreviousStep()
        {
            if (_currentStepIndex <= 0) return;
            _currentStepIndex--;
            HideAllAdaptationOverlays();
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

            if (stepNumberText    != null) stepNumberText.text    = $"Step {step.step_number}/{_task.steps.Length}";
            if (stepTitleText     != null) stepTitleText.text     = step.title;
            if (stepInstructionText != null) stepInstructionText.text = step.instruction;
            if (stepDetailText    != null)
            {
                stepDetailText.text    = _slowPaceActive ? step.detail : "";
                stepDetailText.gameObject.SetActive(_slowPaceActive);
            }

            // Update nav buttons
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
            if (arCueArrow != null)
            {
                arCueArrow.SetActive(true);
                // Arrow targeting is set up via component name lookup in the scene
                // (matches to named child transforms in the content root)
                PositionCueArrow(step.ar_arrow_target);
            }
            if (stepInstructionText != null && step.adaptation_hints != null)
                stepInstructionText.text += "\n\n" + step.adaptation_hints.InjectCue;
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
            // Find a named child transform in the ContentRoot that matches the target
            if (arImageTracker?.ContentRoot != null)
            {
                var target = arImageTracker.ContentRoot.Find(targetName);
                if (target != null)
                    arCueArrow.transform.position = target.position + Vector3.up * 0.05f;
            }
        }

        // ── Marker Events ────────────────────────────────────────────────────────
        private void OnMarkerDetected(Transform contentRoot)
        {
            if (stepPanel != null) stepPanel.SetActive(true);
            DisplayCurrentStep();
            Debug.Log("[ARContentManager] Marker detected — showing AR content.");
        }

        private void OnMarkerLost()
        {
            if (stepPanel != null) stepPanel.SetActive(false);
            if (hintPanel != null) hintPanel.SetActive(false);
            Debug.Log("[ARContentManager] Marker lost — hiding AR content.");
        }

        // ── Debug HUD ────────────────────────────────────────────────────────────
        /// <summary>Called by AffectScorer to update the debug HUD with live affect scores.</summary>
        public void UpdateDebugHUD(float confusion, float frustration, float engagement, bool markerTracked)
        {
            if (debugAffectText == null) return;
            debugAffectText.text = $"Confusion:   {confusion:F3}\n" +
                                   $"Frustration: {frustration:F3}\n" +
                                   $"Engagement:  {engagement:F3}\n" +
                                   $"Marker: {(markerTracked ? "TRACKED" : "LOST")}\n" +
                                   $"Step: {_currentStepIndex + 1}/{TotalSteps}";
        }
    }
}
