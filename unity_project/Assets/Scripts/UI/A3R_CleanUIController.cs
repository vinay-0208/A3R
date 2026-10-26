using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using A3R.ARTracking;
using A3R.ARGuidance;
using A3R.Affect;
using A3R.Adaptation;

namespace A3R.UI
{
    public class A3R_CleanUIController : MonoBehaviour
    {
        public static bool DeveloperModeEnabled { get; private set; } = false;

        private Canvas _canvas;
        private RectTransform _safeArea;
        
        private TMP_Text _humanStateText;
        private TMP_Text _trackingText;
        private RawImage _cameraPreview;
        
        private GameObject _instructionPanel;
        private TMP_Text _instructionTitle;
        private TMP_Text _instructionBody;
        private Button _actionButton;
        private Button _resetCornerButton;
        private TMP_Text _actionButtonText;
        
        private TMP_Text _devToggleText;
        private Image _devToggleImg;
        
        private FaceLandmarkBridge _faceBridge;
        private AffectScorer _affectScorer;
        private AdaptationController _adaptationController;
        private A3R_MarkerlessFoundationController _foundationController;
        private A3R_ResistorTutorialController _tutorialController;

        private void Awake()
        {
            var oldCanvas = GetComponent<Canvas>();
            if (oldCanvas != null)
            {
                oldCanvas.enabled = false;
            }
        }

        private void Start()
        {
            _faceBridge = FindFirstObjectByType<FaceLandmarkBridge>();
            _affectScorer = FindFirstObjectByType<AffectScorer>();
            _adaptationController = FindFirstObjectByType<AdaptationController>();
            _foundationController = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            _tutorialController = FindFirstObjectByType<A3R_ResistorTutorialController>();

            BuildUIHierarchy();
        }

        private void BuildUIHierarchy()
        {
            // 1. Canvas
            var canvasGO = new GameObject("CleanUICanvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2400);
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGO.AddComponent<GraphicRaycaster>();

            // 2. Safe Area Container
            // Fix 3: Remove faulty Screen.safeArea mapping that breaks Scaled UI
            var safeAreaGO = new GameObject("SafeAreaContainer");
            _safeArea = safeAreaGO.AddComponent<RectTransform>();
            _safeArea.SetParent(canvasGO.transform, false);
            _safeArea.anchorMin = Vector2.zero;
            _safeArea.anchorMax = Vector2.one;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;

            // 3. Top Bar (Tracking & Human State)
            var topBarGO = new GameObject("TopBar");
            var topBarRT = topBarGO.AddComponent<RectTransform>();
            topBarRT.SetParent(_safeArea, false);
            topBarRT.anchorMin = new Vector2(0, 1);
            topBarRT.anchorMax = new Vector2(1, 1);
            topBarRT.pivot = new Vector2(0.5f, 1);
            topBarRT.sizeDelta = new Vector2(0, 120);
            topBarRT.anchoredPosition = new Vector2(0, -20);
            
            var trackingGO = new GameObject("TrackingIndicator");
            var trackingRT = trackingGO.AddComponent<RectTransform>();
            trackingRT.SetParent(topBarRT, false);
            trackingRT.anchorMin = new Vector2(0.5f, 1);
            trackingRT.anchorMax = new Vector2(0.5f, 1);
            trackingRT.pivot = new Vector2(0.5f, 1);
            trackingRT.sizeDelta = new Vector2(400, 60);
            trackingRT.anchoredPosition = new Vector2(0, 0);
            _trackingText = CreateText(trackingGO, "? TRACKING", 42, new Color(0.2f, 0.95f, 0.5f), TextAlignmentOptions.Center);
            
            var humanStateGO = new GameObject("HumanStateIndicator");
            var humanStateRT = humanStateGO.AddComponent<RectTransform>();
            humanStateRT.SetParent(topBarRT, false);
            humanStateRT.anchorMin = new Vector2(0.5f, 1);
            humanStateRT.anchorMax = new Vector2(0.5f, 1);
            humanStateRT.pivot = new Vector2(0.5f, 1);
            humanStateRT.sizeDelta = new Vector2(600, 60);
            humanStateRT.anchoredPosition = new Vector2(0, -60);
            _humanStateText = CreateText(humanStateGO, "HUMAN STATE: FOCUSED", 48, new Color(0.3f, 0.85f, 1.0f), TextAlignmentOptions.Center);

            // 4. Recovery Controls (Top Left)
            var recoveryGO = new GameObject("RecoveryControls");
            var recoveryRT = recoveryGO.AddComponent<RectTransform>();
            recoveryRT.SetParent(_safeArea, false);
            recoveryRT.anchorMin = new Vector2(0, 1);
            recoveryRT.anchorMax = new Vector2(0, 1);
            recoveryRT.pivot = new Vector2(0, 1);
            recoveryRT.sizeDelta = new Vector2(300, 200);
            recoveryRT.anchoredPosition = new Vector2(20, -20);

            var restartArBtn = CreateButton(recoveryGO, "RESTART AR", new Vector2(260, 80), new Vector2(0, 0));
            restartArBtn.onClick.AddListener(() => {
                if (_tutorialController != null) {
                    var m = _tutorialController.GetType().GetMethod("RestartARFromTutorial", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (m != null) m.Invoke(_tutorialController, null);
                }
            });

            var restartTutBtn = CreateButton(recoveryGO, "RESTART TUTORIAL", new Vector2(260, 80), new Vector2(0, -100));
            restartTutBtn.onClick.AddListener(() => {
                if (_tutorialController != null) {
                    var m = _tutorialController.GetType().GetMethod("RestartTutorial", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (m != null) m.Invoke(_tutorialController, null);
                }
            });

            // 5. Front Camera Preview (Top Right)
            var camGO = new GameObject("CameraPreview");
            var camRT = camGO.AddComponent<RectTransform>();
            camRT.SetParent(_safeArea, false);
            camRT.anchorMin = new Vector2(1, 1);
            camRT.anchorMax = new Vector2(1, 1);
            camRT.pivot = new Vector2(1, 1);
            camRT.sizeDelta = new Vector2(160, 284);
            camRT.anchoredPosition = new Vector2(-20, -20);
            
            var camOutline = camGO.AddComponent<Image>();
            camOutline.color = new Color(0, 0, 0, 0.8f);

            var rawImgGO = new GameObject("RawImage");
            var rawRT = rawImgGO.AddComponent<RectTransform>();
            rawRT.SetParent(camGO.transform, false);
            rawRT.anchorMin = new Vector2(0.5f, 0.5f); 
            rawRT.anchorMax = new Vector2(0.5f, 0.5f);
            rawRT.pivot = new Vector2(0.5f, 0.5f);
            
            _cameraPreview = rawImgGO.AddComponent<RawImage>();

            // 6. Instruction Panel (Bottom Center)
            _instructionPanel = new GameObject("InstructionPanel");
            var instRT = _instructionPanel.AddComponent<RectTransform>();
            instRT.SetParent(_safeArea, false);
            instRT.anchorMin = new Vector2(0.5f, 0);
            instRT.anchorMax = new Vector2(0.5f, 0);
            instRT.pivot = new Vector2(0.5f, 0);
            instRT.sizeDelta = new Vector2(900, 320);
            instRT.anchoredPosition = new Vector2(0, 40);
            var instImg = _instructionPanel.AddComponent<Image>();
            instImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

            var titleGO = new GameObject("Title");
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.SetParent(instRT, false);
            titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.sizeDelta = new Vector2(-40, 60);
            titleRT.anchoredPosition = new Vector2(0, -20);
            _instructionTitle = CreateText(titleGO, "STEP X", 48, Color.white, TextAlignmentOptions.TopLeft);
            _instructionTitle.fontStyle = FontStyles.Bold;

            var bodyGO = new GameObject("Body");
            var bodyRT = bodyGO.AddComponent<RectTransform>();
            bodyRT.SetParent(instRT, false);
            bodyRT.anchorMin = new Vector2(0, 1); bodyRT.anchorMax = new Vector2(1, 1);
            bodyRT.pivot = new Vector2(0.5f, 1);
            bodyRT.sizeDelta = new Vector2(-40, 120);
            bodyRT.anchoredPosition = new Vector2(0, -80);
            _instructionBody = CreateText(bodyGO, "Instruction text...", 36, new Color(0.9f, 0.9f, 0.9f), TextAlignmentOptions.TopLeft);

            _actionButton = CreateButton(_instructionPanel, "FINISH STEP", new Vector2(820, 90), new Vector2(0, 20));
            var btnRT = _actionButton.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0); btnRT.anchorMax = new Vector2(0.5f, 0);
            btnRT.pivot = new Vector2(0.5f, 0);
            _actionButton.GetComponent<Image>().color = new Color(0.1f, 0.9f, 0.35f, 0.95f);
            _actionButtonText = _actionButton.GetComponentInChildren<TMP_Text>();
            _actionButtonText.color = Color.black;
            _actionButtonText.fontStyle = FontStyles.Bold;
            _actionButton.onClick.AddListener(() => {
                if (_foundationController != null && _foundationController.CurrentState != PlacementState.PLACED)
                {
                    if (_foundationController.CurrentState == PlacementState.READY) {
                        var m = _foundationController.GetType().GetMethod("ConfirmPlacement", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (m != null) m.Invoke(_foundationController, null);
                    }
                    else if (_foundationController.CurrentState == PlacementState.FOUR_CORNER_REVIEW) {
                        var m = _foundationController.GetType().GetMethod("LockFourCornerRegistration", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (m != null) m.Invoke(_foundationController, null);
                    }
                }
                else if (_tutorialController != null)
                {
                    var m = _tutorialController.GetType().GetMethod("HandleNextStepRequested", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (m != null) m.Invoke(_tutorialController, null);
                }
            });


            // 4-Corner Reset Button
            _resetCornerButton = CreateButton(_safeArea.gameObject, "RESET CORNERS", new Vector2(400, 90), Vector2.zero);
            var resetRT = _resetCornerButton.GetComponent<RectTransform>();
            resetRT.anchorMin = new Vector2(0.5f, 0); resetRT.anchorMax = new Vector2(0.5f, 0);
            resetRT.pivot = new Vector2(0.5f, 0);
            resetRT.anchoredPosition = new Vector2(0, 380); // Placed above instruction panel
            _resetCornerButton.GetComponent<Image>().color = new Color(0.9f, 0.2f, 0.2f, 0.95f);
            var resetTxt = _resetCornerButton.GetComponentInChildren<TMP_Text>();
            resetTxt.color = Color.white;
            resetTxt.fontStyle = FontStyles.Bold;
            _resetCornerButton.gameObject.SetActive(false);
            _resetCornerButton.onClick.AddListener(() => {
                if (_foundationController != null) {
                    var m = _foundationController.GetType().GetMethod("ResetFourCorner", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (m != null) m.Invoke(_foundationController, null);
                }
            });

            // 7. Developer Mode Toggle (Bottom Right)
            var devGO = new GameObject("DevToggle");
            var devRT = devGO.AddComponent<RectTransform>();
            devRT.SetParent(_safeArea, false);
            devRT.anchorMin = new Vector2(1, 0); devRT.anchorMax = new Vector2(1, 0);
            devRT.pivot = new Vector2(1, 0);
            devRT.sizeDelta = new Vector2(180, 80);
            devRT.anchoredPosition = new Vector2(-20, 20);
            
            var devBtn = devGO.AddComponent<Button>();
            _devToggleImg = devGO.AddComponent<Image>();
            
            var devTxtGO = new GameObject("Text");
            var devTxtRT = devTxtGO.AddComponent<RectTransform>();
            devTxtRT.SetParent(devGO.transform, false);
            devTxtRT.anchorMin = Vector2.zero; devTxtRT.anchorMax = Vector2.one;
            devTxtRT.sizeDelta = Vector2.zero;
            _devToggleText = CreateText(devTxtGO, "DEV MODE", 28, Color.white, TextAlignmentOptions.Center);
            _devToggleText.fontStyle = FontStyles.Bold;
            
            devBtn.onClick.AddListener(() => { DeveloperModeEnabled = !DeveloperModeEnabled; });
        }

        private TMP_Text CreateText(GameObject parent, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            var tmp = parent.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private Button CreateButton(GameObject parent, string text, Vector2 size, Vector2 anchoredPosition)
        {
            var btnGO = new GameObject("Button_" + text.Replace(" ", ""));
            var rt = btnGO.AddComponent<RectTransform>();
            rt.SetParent(parent.transform, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            var btn = btnGO.AddComponent<Button>();
            
            var txtGO = new GameObject("Text");
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.SetParent(btnGO.transform, false);
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            var tmp = CreateText(txtGO, text, 36, Color.white, TextAlignmentOptions.Center);
            tmp.fontStyle = FontStyles.Bold;
            
            return btn;
        }

        private void Update()
        {
            // Update Camera Preview
            if (_faceBridge != null && _cameraPreview != null)
            {
                var tex = _faceBridge.FrontCameraTexture;
                if (tex != null && tex.isPlaying)
                {
                    _cameraPreview.texture = tex;
                    _cameraPreview.gameObject.SetActive(true);
                    
                    float w = tex.width;
                    float h = tex.height;
                    int rot = tex.videoRotationAngle;
                    
                    float visualW = (rot == 90 || rot == 270) ? h : w;
                    float visualH = (rot == 90 || rot == 270) ? w : h;
                    
                    float aspect = visualW > 0 ? visualW / visualH : 1f;
                    float targetW = 160f;
                    float targetH = 284f;
                    float targetAspect = targetW / targetH;
                    
                    float finalW, finalH;
                    if (aspect > targetAspect) {
                        finalH = targetH;
                        finalW = targetH * aspect;
                    } else {
                        finalW = targetW;
                        finalH = targetW / aspect;
                    }
                    
                    if (rot == 90 || rot == 270) {
                        _cameraPreview.rectTransform.sizeDelta = new Vector2(finalH, finalW);
                    } else {
                        _cameraPreview.rectTransform.sizeDelta = new Vector2(finalW, finalH);
                    }
                    
                    _cameraPreview.rectTransform.localEulerAngles = new Vector3(0, 0, -rot);
                    
                    // Fix 2: Remove the vertical mirror inversion that flips the face upside down
                    float mirrorX = 1f;
                    float mirrorY = 1f;
                    if (rot == 90 || rot == 270) {
                        mirrorY = -1f; // Standard selfie horizontal mirror for portrait orientation
                    } else {
                        mirrorX = -1f; // Standard selfie horizontal mirror for landscape orientation
                    }
                    _cameraPreview.rectTransform.localScale = new Vector3(mirrorX, mirrorY, 1f);
                }
                else
                {
                    _cameraPreview.gameObject.SetActive(false);
                }
            }

            // Update Dev Toggle
            if (_devToggleText != null && _devToggleImg != null)
            {
                _devToggleText.text = DeveloperModeEnabled ? "DEV MODE: ON" : "DEV MODE";
                _devToggleImg.color = DeveloperModeEnabled ? new Color(0.8f, 0.2f, 0.2f, 0.95f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
            }

            // Fix 1: Human State explicitly reads AffectScorer.FaceDetected
            if (_affectScorer != null && _adaptationController != null && _humanStateText != null)
            {
                bool faceDetected = _affectScorer.FaceDetected;
                
                bool isConfused = false;
                var field = _adaptationController.GetType().GetField("_inElevatedState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    isConfused = (bool)field.GetValue(_adaptationController);
                }
                
                // If adaptation controller is intentionally blocked (e.g. before tutorial), fall back to checking the authoritative raw signal
                if (!isConfused && _tutorialController != null)
                {
                    var tutState = _tutorialController.CurrentState;
                    if (tutState == A3R.ARGuidance.ResistorTutorialState.INACTIVE || tutState == A3R.ARGuidance.ResistorTutorialState.INTRO || tutState == A3R.ARGuidance.ResistorTutorialState.STEP_COMPLETE)
                    {
                        if (_affectScorer.ConfusionScore >= _adaptationController.ConfusionThreshold)
                        {
                            isConfused = true;
                        }
                    }
                }
                
                if (!faceDetected)
                {
                    _humanStateText.text = "HUMAN STATE: NO FACE";
                    _humanStateText.color = new Color(0.6f, 0.6f, 0.6f); // Grey
                }
                else if (isConfused)
                {
                    _humanStateText.text = "HUMAN STATE: CONFUSED";
                    _humanStateText.color = new Color(1.0f, 0.5f, 0.0f); // Orange
                }
                else
                {
                    _humanStateText.text = "HUMAN STATE: FOCUSED";
                    _humanStateText.color = new Color(0.3f, 0.85f, 1.0f); // Cyan
                }
            }

            // Update Tracking
            if (_foundationController != null && _trackingText != null)
            {
                bool isTracking = _foundationController.HasReadyPose || (_foundationController.CurrentState >= PlacementState.PLACED);
                if (isTracking)
                {
                    _trackingText.text = "? TRACKING";
                    _trackingText.color = new Color(0.2f, 0.95f, 0.5f); // Green
                }
                else
                {
                    _trackingText.text = "? SEARCHING";
                    _trackingText.color = new Color(1.0f, 0.75f, 0.2f); // Amber
                }
            }

            // Update Instructions
            UpdateInstructionPanel();
        }

        private void UpdateInstructionPanel()
        {
            if (_foundationController == null || _tutorialController == null) return;
            
            _instructionPanel.SetActive(true);

            var regState = _foundationController.CurrentState;
            if (regState != PlacementState.PLACED)
            {
                _actionButton.gameObject.SetActive(true);
                switch (regState)
                {
                    case PlacementState.SEARCHING:
                        _instructionTitle.text = "SURFACE DETECTION";
                        _instructionBody.text = "Point the camera at the table and move it slowly to detect the surface.";
                        _actionButton.gameObject.SetActive(false);
                        break;
                    case PlacementState.READY:
                        _instructionTitle.text = "PLACE BREADBOARD";
                        _instructionBody.text = "Tap when the virtual marker aligns with the physical breadboard.";
                        _actionButtonText.text = "PLACE BREADBOARD";
                        break;
                    case PlacementState.PLACING:
                        _instructionTitle.text = "PLACING...";
                        _instructionBody.text = "Anchoring the AR content...";
                        _actionButton.gameObject.SetActive(false);
                        break;
                    case PlacementState.FOUR_CORNER_TL:
                        _instructionTitle.text = "CALIBRATION";
                        _instructionBody.text = "Tap the TOP-LEFT corner of the physical breadboard.";
                        _actionButton.gameObject.SetActive(false);
                        break;
                    case PlacementState.FOUR_CORNER_TR:
                        _instructionTitle.text = "CALIBRATION";
                        _instructionBody.text = "Tap the TOP-RIGHT corner of the physical breadboard.";
                        _actionButton.gameObject.SetActive(false);
                        break;
                    case PlacementState.FOUR_CORNER_BR:
                        _instructionTitle.text = "CALIBRATION";
                        _instructionBody.text = "Tap the BOTTOM-RIGHT corner of the physical breadboard.";
                        _actionButton.gameObject.SetActive(false);
                        break;
                    case PlacementState.FOUR_CORNER_BL:
                        _instructionTitle.text = "CALIBRATION";
                        _instructionBody.text = "Tap the BOTTOM-LEFT corner of the physical breadboard.";
                        _actionButton.gameObject.SetActive(false);
                        break;
                    case PlacementState.FOUR_CORNER_REVIEW:
                        _instructionTitle.text = "CALIBRATION";
                        _instructionBody.text = "Review the four corners. If they match the physical breadboard, register them.";
                        _actionButtonText.text = "REGISTER CORNERS";
                        break;
                }
                return;
            }

            var tutState = _tutorialController.CurrentState;
            if (tutState == ResistorTutorialState.INACTIVE)
            {
                _instructionPanel.SetActive(false);
                return;
            }

            _actionButton.gameObject.SetActive(true);
            
            switch (tutState)
            {
                case ResistorTutorialState.RESISTOR_TARGET:
                    _instructionTitle.text = "STEP 1: Place Resistor";
                    _instructionBody.text = "Place the resistor at the highlighted location on the breadboard.";
                    _actionButtonText.text = "FINISH STEP";
                    break;
                case ResistorTutorialState.RESISTOR_CONFIRMED:
                    _instructionTitle.text = "STEP 1: Complete";
                    _instructionBody.text = "Validating resistor placement...";
                    _actionButton.gameObject.SetActive(false);
                    break;
                case ResistorTutorialState.LED_TARGET:
                    _instructionTitle.text = "STEP 2: Place LED";
                    _instructionBody.text = "Place the LED.\n(+) Anode to Row 10 (Red)\n(-) Cathode to Row 11 (Blue)";
                    _actionButtonText.text = "FINISH STEP";
                    break;
                case ResistorTutorialState.LED_CONFIRMED:
                    _instructionTitle.text = "STEP 2: Complete";
                    _instructionBody.text = "Validating LED placement...";
                    _actionButton.gameObject.SetActive(false);
                    break;
                case ResistorTutorialState.ESP32_TARGET:
                    _instructionTitle.text = "STEP 3: Place ESP32";
                    _instructionBody.text = "Place the ESP32 spanning the center gap.";
                    _actionButtonText.text = "FINISH STEP";
                    break;
                case ResistorTutorialState.ESP32_CONFIRMED:
                    _instructionTitle.text = "STEP 3: Complete";
                    _instructionBody.text = "Validating ESP32 placement...";
                    _actionButton.gameObject.SetActive(false);
                    break;
                case ResistorTutorialState.WIRE1_TARGET:
                    _instructionTitle.text = "STEP 4: Wire 1";
                    _instructionBody.text = "Connect Row 10 (Red) to ESP32 Pin D2.";
                    _actionButtonText.text = "FINISH STEP";
                    break;
                case ResistorTutorialState.WIRE1_CONFIRMED:
                    _instructionTitle.text = "STEP 4: Complete";
                    _instructionBody.text = "Validating Wire 1 placement...";
                    _actionButton.gameObject.SetActive(false);
                    break;
                case ResistorTutorialState.WIRE2_TARGET:
                    _instructionTitle.text = "STEP 5: Wire 2";
                    _instructionBody.text = "Connect Row 11 (Blue) to ESP32 GND.";
                    _actionButtonText.text = "FINISH STEP";
                    break;
                case ResistorTutorialState.WIRE2_CONFIRMED:
                    _instructionTitle.text = "STEP 5: Complete";
                    _instructionBody.text = "Validating Wire 2 placement...";
                    _actionButton.gameObject.SetActive(false);
                    break;
                case ResistorTutorialState.WIRE3_TARGET:
                    _instructionTitle.text = "STEP 6: Wire 3";
                    _instructionBody.text = "Connect Resistor to ESP32 3V3.";
                    _actionButtonText.text = "FINISH STEP";
                    break;
                case ResistorTutorialState.WIRE3_CONFIRMED:
                    _instructionTitle.text = "STEP 6: Complete";
                    _instructionBody.text = "Validating Wire 3 placement...";
                    _actionButton.gameObject.SetActive(false);
                    break;
                case ResistorTutorialState.STEP_COMPLETE:
                    _instructionTitle.text = "ASSEMBLY COMPLETE";
                    _instructionBody.text = "Congratulations! You have successfully completed the circuit.";
                    _actionButton.gameObject.SetActive(false);
                    break;
            }
        }
    }
}

