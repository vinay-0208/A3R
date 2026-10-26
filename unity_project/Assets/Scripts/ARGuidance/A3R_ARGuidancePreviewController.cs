using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using A3R.ContentManager;
using A3R.Adaptation;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace A3R.ARGuidance
{
    /// <summary>
    /// A3R_ARGuidancePreviewController ? Standalone offline preview controller providing
    /// visual inspection, state management, adaptive response validation, and HUD telemetry
    /// for the A3R AR Guidance System in Unity Editor without physical hardware.
    /// </summary>
    public class A3R_ARGuidancePreviewController : MonoBehaviour
    {
        [Header("AR Guidance Subsystems")]
        [SerializeField] private ARGuidanceController guidanceController;
        [SerializeField] private Transform tabletopRoot;

        [Header("3D Target Highlight & Overlays")]
        [SerializeField] private GameObject targetHighlightAnchor;
        [SerializeField] private GameObject targetFloatingLabel;
        [SerializeField] private TMP_Text floatingLabelStepText;
        [SerializeField] private TMP_Text floatingLabelTitleText;
        [SerializeField] private Image floatingLabelAccent;
        [SerializeField] private GameObject step7IdeCard;

        [Header("Tabletop Component Renderers (For Focus Isolation)")]
        [SerializeField] private Renderer breadboardRenderer;
        [SerializeField] private Renderer esp32BoardRenderer;
        [SerializeField] private Renderer resistorRenderer;
        [SerializeField] private Renderer ledRenderer;
        [SerializeField] private Renderer wirePin13Renderer;
        [SerializeField] private Renderer wireGndRenderer;
        [SerializeField] private Renderer usbCableRenderer;

        [Header("UI Readouts (Top HUD & Diagnostics)")]
        [SerializeField] private TMP_Text topStepBannerText;
        [SerializeField] private TMP_Text topStepTitleText;
        [SerializeField] private TMP_Text stepInstructionText;
        [SerializeField] private TMP_Text targetInfoText;
        [SerializeField] private TMP_Text trackingStatusText;
        [SerializeField] private TMP_Text visualStateText;
        [SerializeField] private TMP_Text adaptationText;
        [SerializeField] private TMP_Text diagnosticsSummaryText;
        [SerializeField] private TMP_Text provisionalNoteText;
        [SerializeField] private GameObject completionPanel;

        [Header("UI Navigation & Action Buttons")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button normalButton;
        [SerializeField] private Button injectCueButton;
        [SerializeField] private Button insertHintButton;
        [SerializeField] private Button slowPaceButton;
        [SerializeField] private Button simplifyStepButton;
        [SerializeField] private Button trackingLostButton;
        [SerializeField] private Button trackingRecoveredButton;
        [SerializeField] private Button trackingToggleButton;
        [SerializeField] private Button completeButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button btnCompletionReset;

        [Header("Calibration / Debug Readout (Dev Mode — Hidden by default)")]
        [SerializeField] private GameObject calibrationDebugPanel;
        [SerializeField] private TMP_Text calibrationDebugText;
        [SerializeField] private Button debugToggleButton;

        // Internal State
        private InstructionStep[] _steps;
        private int _currentStepIndex = 0;
        private bool _isTracking = true;
        private bool _isCompleted = false;
        private AdaptationAction? _currentAdaptation = null;

        // Material color caching for dimming/highlighting
        private readonly Dictionary<Renderer, Color> _originalColors = new Dictionary<Renderer, Color>();
        private bool _isSimplified = false;

        private bool _showDebugTelemetry = false;

        public int CurrentStepIndex => _currentStepIndex;
        public bool IsTracking => _isTracking;
        public bool IsCompleted => _isCompleted;
        public AdaptationAction? CurrentAdaptation => _currentAdaptation;
        public ARGuidanceController GuidanceController => guidanceController;
        public Transform TabletopRoot => tabletopRoot;
        public GameObject CompletionPanel => completionPanel;
        public GameObject TargetHighlightAnchor => targetHighlightAnchor;
        public GameObject TargetFloatingLabel => targetFloatingLabel;
        public GameObject Step7IdeCard => step7IdeCard;
        public Renderer BreadboardRenderer => breadboardRenderer;
        public GameObject CalibrationDebugPanel => calibrationDebugPanel;
        public TMP_Text CalibrationDebugText => calibrationDebugText;

        public bool ShowDebugTelemetry
        {
            get => _showDebugTelemetry;
            set
            {
                _showDebugTelemetry = value;
                if (calibrationDebugPanel != null)
                {
                    calibrationDebugPanel.SetActive(value);
                }
            }
        }

        public void ToggleDebugTelemetry()
        {
            ShowDebugTelemetry = !_showDebugTelemetry;
        }

        private void Awake()
        {
            // CRITICAL: Ensure completion modal & debug panel are strictly HIDDEN on startup
            if (completionPanel != null)
            {
                completionPanel.SetActive(false);
            }
            if (calibrationDebugPanel != null)
            {
                calibrationDebugPanel.SetActive(false);
            }

            CacheOriginalColors();
            LoadSteps();
            EnsureGuidanceController();
        }

        private void Start()
        {
            WireButtons();
            StartVisualPreview();
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame)
            {
                ToggleDebugTelemetry();
            }
#endif

            UpdateDebugTelemetry();
        }

        private void LateUpdate()
        {
            BillboardWorldOverlays();
            AnimateVerifyBlink();
        }

        private void BillboardWorldOverlays()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            if (targetFloatingLabel != null && targetFloatingLabel.activeSelf)
            {
                targetFloatingLabel.transform.rotation = Quaternion.LookRotation(targetFloatingLabel.transform.position - cam.transform.position);
            }

            if (step7IdeCard != null && step7IdeCard.activeSelf)
            {
                step7IdeCard.transform.rotation = Quaternion.LookRotation(step7IdeCard.transform.position - cam.transform.position);
            }
        }

        private void AnimateVerifyBlink()
        {
            // Step 8 (index 7): verify_blink LED pulse
            if (_isTracking && !_isCompleted && _currentStepIndex == 7 && ledRenderer != null)
            {
                float blink = Mathf.PingPong(Time.time * 2.5f, 1.0f);
                Color c = Color.Lerp(new Color(0.3f, 0.05f, 0.05f), new Color(1.0f, 0.2f, 0.2f), blink > 0.5f ? 1.0f : 0.0f);
                ledRenderer.material.color = c;
            }
        }

        private void CacheOriginalColors()
        {
            CacheRendererColor(breadboardRenderer);
            CacheRendererColor(esp32BoardRenderer);
            CacheRendererColor(resistorRenderer);
            CacheRendererColor(ledRenderer);
            CacheRendererColor(wirePin13Renderer);
            CacheRendererColor(wireGndRenderer);
            CacheRendererColor(usbCableRenderer);
        }

        private void CacheRendererColor(Renderer rend)
        {
            if (rend != null && rend.sharedMaterial != null)
            {
                _originalColors[rend] = rend.material.color;
            }
        }

        private void LoadSteps()
        {
            string taskJsonPath = Path.Combine(Application.dataPath, "StepData", "led_blink_task.json");
            if (File.Exists(taskJsonPath))
            {
                try
                {
                    string json = File.ReadAllText(taskJsonPath);
                    InstructionTask task = JsonUtility.FromJson<InstructionTask>(json);
                    if (task != null && task.steps != null && task.steps.Length > 0)
                    {
                        _steps = task.steps;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[A3R Preview] Could not parse led_blink_task.json: {ex.Message}");
                }
            }

            // Fallback definitions matching the 8 canonical steps
            _steps = new InstructionStep[]
            {
                new InstructionStep { id = "place_breadboard", step_number = 1, title = "Place Breadboard", instruction = "Place breadboard beside reference marker card.", highlight_component = "breadboard", ar_arrow_target = "breadboard_center" },
                new InstructionStep { id = "insert_resistor", step_number = 2, title = "Insert Resistor", instruction = "Insert 220? resistor from row 10 col E to row 14 col E.", highlight_component = "resistor", ar_arrow_target = "resistor_slot_row10_colE" },
                new InstructionStep { id = "insert_led", step_number = 3, title = "Insert LED", instruction = "Insert LED: long leg (anode) in row 14, short leg in row 15.", highlight_component = "led", ar_arrow_target = "led_slot_row14_colD" },
                new InstructionStep { id = "wire_resistor_to_pin13", step_number = 4, title = "Wire Pin 13", instruction = "Connect jumper wire from board GPIO 13 to row 10 col A.", highlight_component = "jumper_wire", ar_arrow_target = "board_gpio13" },
                new InstructionStep { id = "wire_gnd", step_number = 5, title = "Wire GND", instruction = "Connect jumper wire from board GND to row 15 col A.", highlight_component = "jumper_wire", ar_arrow_target = "board_gnd" },
                new InstructionStep { id = "connect_usb", step_number = 6, title = "Connect USB", instruction = "Connect development board to computer using USB cable.", highlight_component = "arduino_uno", ar_arrow_target = "board_usb_port" },
                new InstructionStep { id = "upload_blink_sketch", step_number = 7, title = "Upload Blink Sketch", instruction = "Upload Blink sketch from Arduino IDE (Off-marker IDE step).", highlight_component = "", ar_arrow_target = "" },
                new InstructionStep { id = "verify_blink", step_number = 8, title = "Verify Blink", instruction = "Observe LED blinking at 1-second intervals on physical circuit.", highlight_component = "led", ar_arrow_target = "led_position" }
            };
        }

        private void EnsureGuidanceController()
        {
            if (guidanceController == null)
            {
                guidanceController = GetComponent<ARGuidanceController>() ?? FindFirstObjectByType<ARGuidanceController>();
            }
        }

        private void WireButtons()
        {
            if (prevButton != null) prevButton.onClick.AddListener(PreviousStep);
            if (nextButton != null) nextButton.onClick.AddListener(NextStep);
            if (normalButton != null) normalButton.onClick.AddListener(ResetToNormal);
            if (injectCueButton != null) injectCueButton.onClick.AddListener(() => ApplyAdaptation(AdaptationAction.InjectCue));
            if (insertHintButton != null) insertHintButton.onClick.AddListener(() => ApplyAdaptation(AdaptationAction.InsertHint));
            if (slowPaceButton != null) slowPaceButton.onClick.AddListener(() => ApplyAdaptation(AdaptationAction.SlowPace));
            if (simplifyStepButton != null) simplifyStepButton.onClick.AddListener(() => ApplyAdaptation(AdaptationAction.SimplifyStep));
            if (trackingLostButton != null) trackingLostButton.onClick.AddListener(() => SetTracking(false));
            if (trackingRecoveredButton != null) trackingRecoveredButton.onClick.AddListener(() => SetTracking(true));
            if (trackingToggleButton != null) trackingToggleButton.onClick.AddListener(ToggleTracking);
            if (completeButton != null) completeButton.onClick.AddListener(CompleteTask);
            if (resetButton != null) resetButton.onClick.AddListener(ResetSimulation);
            if (btnCompletionReset != null) btnCompletionReset.onClick.AddListener(ResetSimulation);
            if (debugToggleButton != null) debugToggleButton.onClick.AddListener(ToggleDebugTelemetry);
        }

        public void ToggleTracking()
        {
            SetTracking(!_isTracking);
        }

        public void StartVisualPreview()
        {
            if (_steps == null || _steps.Length == 0)
            {
                CacheOriginalColors();
                LoadSteps();
                EnsureGuidanceController();
            }

            if (guidanceController != null)
            {
                guidanceController.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            }

            // Strictly hide completion modal
            _isCompleted = false;
            if (completionPanel != null)
            {
                completionPanel.SetActive(false);
            }

            _currentStepIndex = 0;
            _isTracking = true;
            _currentAdaptation = null;

            if (tabletopRoot != null && guidanceController != null)
            {
                guidanceController.OnMarkerDetected(tabletopRoot);
                guidanceController.OnStepChanged(_currentStepIndex, GetCurrentInstructionStep());
            }

            ResetComponentDimming();
            UpdateTargetOverlays();
            UpdateUI();
        }

        public void NextStep()
        {
            if (_isCompleted) return;

            if (_currentStepIndex < _steps.Length - 1)
            {
                SetStep(_currentStepIndex + 1);
            }
            else
            {
                CompleteTask();
            }
        }

        public void PreviousStep()
        {
            if (_isCompleted)
            {
                _isCompleted = false;
                if (completionPanel != null) completionPanel.SetActive(false);
                SetStep(_steps.Length - 1);
                return;
            }

            if (_currentStepIndex > 0)
            {
                SetStep(_currentStepIndex - 1);
            }
        }

        public void SetStep(int index)
        {
            if (_steps == null || index < 0 || index >= _steps.Length) return;

            _currentStepIndex = index;
            _isCompleted = false;
            _currentAdaptation = null;

            // Ensure completion panel is completely hidden
            if (completionPanel != null)
            {
                completionPanel.SetActive(false);
            }

            ResetComponentDimming();

            if (guidanceController != null)
            {
                guidanceController.OnStepChanged(_currentStepIndex, GetCurrentInstructionStep());
            }

            UpdateTargetOverlays();
            UpdateUI();
        }

        public void ApplyAdaptation(AdaptationAction action)
        {
            if (_isCompleted) return;

            _currentAdaptation = action;

            if (guidanceController != null)
            {
                guidanceController.ApplyVisualAdaptation(action);
            }

            if (action == AdaptationAction.SimplifyStep)
            {
                ApplySimplifyIsolation();
            }
            else
            {
                ResetComponentDimming();
            }

            UpdateTargetOverlays();
            UpdateUI();
        }

        public void ResetToNormal()
        {
            if (_isCompleted) return;

            _currentAdaptation = null;

            if (guidanceController != null)
            {
                guidanceController.ResetToNormalGuidance();
            }

            ResetComponentDimming();
            UpdateTargetOverlays();
            UpdateUI();
        }

        public void SetTracking(bool tracking)
        {
            _isTracking = tracking;

            if (guidanceController != null)
            {
                if (_isTracking)
                {
                    guidanceController.OnMarkerDetected(tabletopRoot);
                    guidanceController.OnStepChanged(_currentStepIndex, GetCurrentInstructionStep());
                    if (_currentAdaptation.HasValue)
                    {
                        guidanceController.ApplyVisualAdaptation(_currentAdaptation.Value);
                    }
                }
                else
                {
                    guidanceController.OnMarkerLost();
                }
            }

            UpdateTargetOverlays();
            UpdateUI();
        }

        public void CompleteTask()
        {
            _isCompleted = true;
            _currentAdaptation = null;

            if (guidanceController != null)
            {
                guidanceController.OnTaskComplete();
            }

            ResetComponentDimming();

            // Hide world overlays
            if (targetHighlightAnchor != null) targetHighlightAnchor.SetActive(false);
            if (targetFloatingLabel != null) targetFloatingLabel.SetActive(false);
            if (step7IdeCard != null) step7IdeCard.SetActive(false);

            // ONLY show completion panel when explicitly completed
            if (completionPanel != null)
            {
                completionPanel.SetActive(true);
            }

            UpdateUI();
        }

        public void ResetSimulation()
        {
            _isCompleted = false;
            if (completionPanel != null)
            {
                completionPanel.SetActive(false);
            }

            StartVisualPreview();
        }

        private InstructionStep GetCurrentInstructionStep()
        {
            if (_steps != null && _currentStepIndex >= 0 && _currentStepIndex < _steps.Length)
                return _steps[_currentStepIndex];
            return null;
        }

        private void UpdateTargetOverlays()
        {
            if (_isCompleted || !_isTracking)
            {
                if (targetHighlightAnchor != null) targetHighlightAnchor.SetActive(false);
                if (targetFloatingLabel != null) targetFloatingLabel.SetActive(false);
                if (step7IdeCard != null) step7IdeCard.SetActive(false);
                return;
            }

            // Step 7: Off-marker IDE step special case
            if (_currentStepIndex == 6)
            {
                if (targetHighlightAnchor != null) targetHighlightAnchor.SetActive(false);
                if (targetFloatingLabel != null) targetFloatingLabel.SetActive(false);
                if (step7IdeCard != null)
                {
                    step7IdeCard.SetActive(true);
                    step7IdeCard.transform.position = new Vector3(0.18f, 0.16f, 0.0f);
                }
                return;
            }

            if (step7IdeCard != null) step7IdeCard.SetActive(false);

            // Progressive component visibility across assembly sequence
            if (resistorRenderer != null) resistorRenderer.gameObject.SetActive(_currentStepIndex >= 1);
            if (ledRenderer != null) ledRenderer.gameObject.SetActive(_currentStepIndex >= 2);
            if (wirePin13Renderer != null) wirePin13Renderer.gameObject.SetActive(_currentStepIndex >= 3);
            if (wireGndRenderer != null) wireGndRenderer.gameObject.SetActive(_currentStepIndex >= 4);
            if (usbCableRenderer != null) usbCableRenderer.gameObject.SetActive(_currentStepIndex >= 5);

            var curStep = GetCurrentInstructionStep();
            Transform activeTarget = null;
            if (tabletopRoot != null && curStep != null && !string.IsNullOrEmpty(curStep.ar_arrow_target))
            {
                activeTarget = tabletopRoot.Find(curStep.ar_arrow_target);
                if (activeTarget == null)
                    activeTarget = tabletopRoot.Find("TargetAnchors/" + curStep.ar_arrow_target);
                if (activeTarget == null)
                    activeTarget = tabletopRoot.Find("Target_" + curStep.ar_arrow_target);
                if (activeTarget == null)
                {
                    Transform[] allChildren = tabletopRoot.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < allChildren.Length; i++)
                    {
                        if (allChildren[i].name.Equals(curStep.ar_arrow_target, System.StringComparison.OrdinalIgnoreCase) ||
                            allChildren[i].name.Equals("Target_" + curStep.ar_arrow_target, System.StringComparison.OrdinalIgnoreCase))
                        {
                            activeTarget = allChildren[i];
                            break;
                        }
                    }
                }
                if (activeTarget == null && breadboardRenderer != null)
                {
                    activeTarget = breadboardRenderer.transform;
                }
            }

            // Tune Reticle radius dynamically per target type (never obscuring the object)
            if (guidanceController != null && guidanceController.TargetRing != null && activeTarget != null)
            {
                // Breadboard = 0.045m radius (90mm diameter); Components/pins = 0.024m radius (48mm diameter)
                float tunedRadius = (_currentStepIndex == 0) ? 0.045f : 0.024f;
                guidanceController.TargetRing.SetTarget(activeTarget, Vector3.zero, guidanceController.TargetRing.CurrentColor, tunedRadius);
            }

            // Position target highlight pedestal
            if (targetHighlightAnchor != null)
            {
                if (activeTarget != null)
                {
                    targetHighlightAnchor.SetActive(true);
                    targetHighlightAnchor.transform.position = activeTarget.position;
                    // Controlled highlight scale: 0.08m for breadboard, 0.032m for components
                    float hlScale = (_currentStepIndex == 0) ? 0.08f : 0.032f;
                    targetHighlightAnchor.transform.localScale = new Vector3(hlScale, 0.002f, hlScale);
                    // Tint highlight based on state
                    Color hlColor = GetStateAccentColor();
                    var hlRenderer = targetHighlightAnchor.GetComponentInChildren<Renderer>();
                    if (hlRenderer != null)
                    {
                        ProceduralMeshUtility.SetMaterialColor(hlRenderer.material, hlColor, 1.8f);
                    }
                }
                else
                {
                    targetHighlightAnchor.SetActive(false);
                }
            }

            // Position target floating label beside/above target
            if (targetFloatingLabel != null)
            {
                if (activeTarget != null)
                {
                    targetFloatingLabel.SetActive(true);
                    targetFloatingLabel.transform.position = activeTarget.position + new Vector3(0.045f, 0.055f, 0.020f);

                    if (floatingLabelStepText != null)
                        floatingLabelStepText.text = $"STEP {curStep.step_number}";

                    if (floatingLabelTitleText != null)
                        floatingLabelTitleText.text = curStep.title.ToUpperInvariant();

                    if (floatingLabelAccent != null)
                        floatingLabelAccent.color = GetStateAccentColor();
                }
                else
                {
                    targetFloatingLabel.SetActive(false);
                }
            }
        }

        private void UpdateDebugTelemetry()
        {
            if (calibrationDebugPanel == null || !calibrationDebugPanel.activeSelf || calibrationDebugText == null)
                return;

            var curStep = GetCurrentInstructionStep();
            string targetName = curStep != null ? curStep.ar_arrow_target : "None";
            Vector3 targetPos = Vector3.zero;
            Transform activeTarget = null;
            if (tabletopRoot != null && !string.IsNullOrEmpty(targetName))
            {
                activeTarget = tabletopRoot.Find(targetName) ?? tabletopRoot.Find("TargetAnchors/" + targetName);
                if (activeTarget != null) targetPos = activeTarget.position;
            }

            Vector3 reticlePos = Vector3.zero;
            if (guidanceController != null && guidanceController.TargetRing != null)
            {
                reticlePos = guidanceController.TargetRing.transform.position;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>CALIBRATION TELEMETRY</b> (Dev Only)");
            sb.AppendLine($"Target: <b>{targetName}</b>");
            sb.AppendLine($"Target World Pos: ({targetPos.x:F3}, {targetPos.y:F3}, {targetPos.z:F3})");
            sb.AppendLine($"Reticle World Pos: ({reticlePos.x:F3}, {reticlePos.y:F3}, {reticlePos.z:F3})");

            var arrow = guidanceController != null ? guidanceController.DirectionalArrow : null;
            var chevronPath = arrow != null ? arrow.ChevronPath : null;
            if (chevronPath != null && chevronPath.Chevrons != null)
            {
                for (int i = 0; i < chevronPath.Chevrons.Length; i++)
                {
                    if (chevronPath.Chevrons[i] != null)
                    {
                        Vector3 aPos = chevronPath.Chevrons[i].transform.position;
                        sb.AppendLine($"Arrow {i + 1}: ({aPos.x:F3}, {aPos.y:F3}, {aPos.z:F3})");
                    }
                }
            }

            float dist = Vector3.Distance(targetPos, reticlePos);
            sb.AppendLine($"Distance to target: {dist:F4} m (Tol: <= 0.005 m)");
            calibrationDebugText.text = sb.ToString();
        }

        private Color GetStateAccentColor()
        {
            if (_currentAdaptation == AdaptationAction.InjectCue)
                return new Color(0.25f, 1.0f, 1.0f, 1.0f); // Bright cyan
            if (_currentAdaptation == AdaptationAction.InsertHint)
                return new Color(0.70f, 0.28f, 1.0f, 0.95f); // Violet
            if (_currentAdaptation == AdaptationAction.SlowPace)
                return new Color(0.16f, 0.47f, 1.0f, 0.95f); // Calm blue
            if (_currentAdaptation == AdaptationAction.SimplifyStep)
                return new Color(1.0f, 0.57f, 0.0f, 0.95f); // Amber

            return new Color(0.0f, 0.90f, 1.0f, 0.95f); // Normal Cyan
        }

        private void ApplySimplifyIsolation()
        {
            _isSimplified = true;
            string activeComp = GetCurrentInstructionStep()?.highlight_component?.ToLowerInvariant() ?? "";

            DimRenderer(breadboardRenderer, activeComp.Contains("breadboard"));
            DimRenderer(esp32BoardRenderer, activeComp.Contains("arduino") || activeComp.Contains("board") || activeComp.Contains("esp32"));
            DimRenderer(resistorRenderer, activeComp.Contains("resistor"));
            DimRenderer(ledRenderer, activeComp.Contains("led"));
            DimRenderer(wirePin13Renderer, activeComp.Contains("jumper_wire") && (_currentStepIndex == 3));
            DimRenderer(wireGndRenderer, activeComp.Contains("jumper_wire") && (_currentStepIndex == 4));
            DimRenderer(usbCableRenderer, activeComp.Contains("usb") || (_currentStepIndex == 5));
        }

        private void DimRenderer(Renderer rend, bool isTarget)
        {
            if (rend == null) return;
            if (isTarget)
            {
                if (_originalColors.TryGetValue(rend, out Color orig))
                    rend.material.color = orig;
            }
            else
            {
                if (_originalColors.TryGetValue(rend, out Color orig))
                {
                    Color dimmed = new Color(orig.r * 0.25f, orig.g * 0.25f, orig.b * 0.25f, 0.35f);
                    rend.material.color = dimmed;
                }
            }
        }

        private void ResetComponentDimming()
        {
            if (!_isSimplified) return;
            _isSimplified = false;

            foreach (var kvp in _originalColors)
            {
                if (kvp.Key != null && kvp.Key.material != null)
                {
                    kvp.Key.material.color = kvp.Value;
                }
            }
        }

        private void UpdateUI()
        {
            var curStep = GetCurrentInstructionStep();

            if (_isCompleted)
            {
                if (topStepBannerText != null) topStepBannerText.text = "TASK COMPLETE";
                if (topStepTitleText != null) topStepTitleText.text = "All 8 Assembly Steps Verified Successfully";
                if (stepInstructionText != null) stepInstructionText.text = "The electronics assembly workflow has completed with 100% verification.";
                if (targetInfoText != null) targetInfoText.text = "<b>Target:</b> None (Task Finished)";
            }
            else if (curStep != null)
            {
                if (topStepBannerText != null) topStepBannerText.text = $"STEP {curStep.step_number} / {_steps.Length}";
                if (topStepTitleText != null) topStepTitleText.text = curStep.title;
                if (stepInstructionText != null) stepInstructionText.text = curStep.instruction;

                string targetName = string.IsNullOrEmpty(curStep.ar_arrow_target) ? "[Off-Marker IDE Step]" : curStep.ar_arrow_target;
                if (targetInfoText != null) targetInfoText.text = $"<b>Target:</b> {targetName}";
            }

            if (trackingStatusText != null)
            {
                string status = _isTracking ? "<color=#00E5FF>TRACKING (Active)</color>" : "<color=#FF3D00>TRACKING LOST</color>";
                trackingStatusText.text = $"<b>Tracking:</b> {status}";
            }

            if (visualStateText != null)
            {
                GuidanceVisualState state = guidanceController != null ? guidanceController.CurrentState : GuidanceVisualState.Normal;
                visualStateText.text = $"<b>Visual State:</b> {state}";
            }

            if (adaptationText != null)
            {
                string adaptStr = _currentAdaptation.HasValue ? _currentAdaptation.Value.ToString() : "Normal (Baseline)";
                adaptationText.text = $"<b>Adaptation:</b> {adaptStr}";
            }

            if (diagnosticsSummaryText != null)
            {
                bool arrowVis = guidanceController != null && guidanceController.DirectionalArrow != null && guidanceController.DirectionalArrow.IsVisible;
                bool ringVis = guidanceController != null && guidanceController.TargetRing != null && guidanceController.TargetRing.IsVisible;
                diagnosticsSummaryText.text = $"Arrow: <b>{(arrowVis ? "<color=#00E5FF>ON</color>" : "<color=#888>OFF</color>")}</b>  |  Reticle: <b>{(ringVis ? "<color=#00E5FF>ON</color>" : "<color=#888>OFF</color>")}</b>";
            }

            if (provisionalNoteText != null)
            {
                provisionalNoteText.text = "Provisional software-test GPIO assignment: GPIO 13 (LED1) and GPIO 12 (LED2) are provisional software-test assignments only. Final ESP32 GPIO mapping will be frozen after physical hardware pinout confirmation.";
            }
        }
    }
}
