using System;
using UnityEngine;
using A3R.ARTracking;
using A3R.Logging;

namespace A3R.ARGuidance
{
    public enum ResistorTutorialState
    {
        INACTIVE,
        INTRO,
        RESISTOR_TARGET,
        RESISTOR_CONFIRMED,
        LED_TARGET,
        LED_CONFIRMED,
        ESP32_TARGET,
        ESP32_CONFIRMED,
        WIRE1_TARGET,
        WIRE1_CONFIRMED,
        WIRE2_TARGET,
        WIRE2_CONFIRMED,
        WIRE3_TARGET,
        WIRE3_CONFIRMED,
        STEP_COMPLETE
    }

    /// <summary>
    /// A3R_ResistorTutorialController — Phase 4 Step 1 AR Tutorial Controller.
    /// Guides the learner through Step 1 of the LED Blinking Experiment:
    /// 1. Experiment Introduction ("LED BLINKING EXPERIMENT" -> "Step 1: Place the resistor.")
    /// 2. Resistor Target Guidance (3D indicator around ResistorAnchor local to BreadboardARRoot)
    /// 3. Learner Confirmation via "RESISTOR PLACED" button
    /// 4. Step 1 Completion confirmation ("Resistor placement complete." -> "Step 1 Complete")
    /// </summary>
    public class A3R_ResistorTutorialController : MonoBehaviour
    {
        public static A3R_ResistorTutorialController Instance { get; private set; }

        public ResistorTutorialState CurrentState { get; private set; } = ResistorTutorialState.INACTIVE;
        public string StepName => "RESISTOR";
        public string StateString => CurrentState.ToString();
        public bool IsActiveOrComplete => CurrentState != ResistorTutorialState.INACTIVE;
        public bool IsTargetActive => CurrentState >= ResistorTutorialState.RESISTOR_TARGET;

        public Transform ResistorAnchorTransform { get; private set; }
        public Transform LedAnchorTransform { get; private set; }
        public string TutorialParentName => (ResistorAnchorTransform != null && ResistorAnchorTransform.parent != null) 
            ? ResistorAnchorTransform.parent.name : "NONE";

        public Vector3 TargetLocalPos => ResistorAnchorTransform != null ? ResistorAnchorTransform.localPosition : A3R_BreadboardConfig.DEFAULT_RESISTOR_LOCAL_POS;
        public Vector3 TargetWorldPos => ResistorAnchorTransform != null ? ResistorAnchorTransform.position : Vector3.zero;

        // Visual References
        private GameObject _targetVisualObj;
        private MeshRenderer _targetRenderer;
        private Material _targetMaterial;
        private GameObject _pin1Obj;
        private GameObject _pin2Obj;
        private MeshRenderer _pin1Renderer;
        private MeshRenderer _pin2Renderer;

        // LED Visual References
        private GameObject _ledVisualObj;
        private Material _anodeMaterial;
        private Material _cathodeMaterial;

        // Timers
        private float _introTimer = 0f;
        private const float INTRO_DURATION = 2.5f;

        // GUI Styles
        private GUIStyle _cardStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _btnStyle;
        private bool _guiStylesInit = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnEnable()
        {
            var foundation = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            if (foundation != null)
            {
                foundation.OnBreadboardPlaced += HandleBreadboardPlaced;
                
                // If it's already placed when we enable
                if (foundation.HasPlacedRoot && CurrentState == ResistorTutorialState.INACTIVE)
                {
                    HandleBreadboardPlaced(foundation.BreadboardRootTransform);
                }
            }

            var ui = FindFirstObjectByType<A3R.UI.UIManager>();
            if (ui != null) ui.OnNextStepRequested += HandleNextStepRequested;
        }

        private void OnDisable()
        {
            var foundation = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            if (foundation != null)
            {
                foundation.OnBreadboardPlaced -= HandleBreadboardPlaced;
            }

            var ui = FindFirstObjectByType<A3R.UI.UIManager>();
            if (ui != null) ui.OnNextStepRequested -= HandleNextStepRequested;
        }

        public void HandleNextStepRequested()
        {
            if (CurrentState == ResistorTutorialState.RESISTOR_TARGET) ConfirmResistorPlaced();
            else if (CurrentState == ResistorTutorialState.LED_TARGET) ConfirmLedPlaced();
            else if (CurrentState == ResistorTutorialState.ESP32_TARGET) ConfirmEsp32Placed();
            else if (CurrentState == ResistorTutorialState.WIRE1_TARGET) ConfirmWire1Placed();
            else if (CurrentState == ResistorTutorialState.WIRE2_TARGET) ConfirmWire2Placed();
            else if (CurrentState == ResistorTutorialState.WIRE3_TARGET) ConfirmWire3Placed();
        }

        public void HandleBreadboardPlaced(Transform breadboardRoot)
        {
            if (breadboardRoot == null) return;

            // Cleanup any existing wires from a previous registration
            if (_wire1 != null) { Destroy(_wire1.gameObject); _wire1 = null; }
            if (_wire2 != null) { Destroy(_wire2.gameObject); _wire2 = null; }
            if (_wire3 != null) { Destroy(_wire3.gameObject); _wire3 = null; }

            // 1. Locate ResistorAnchor under BreadboardARRoot
            ResistorAnchorTransform = breadboardRoot.Find("ResistorAnchor");
            if (ResistorAnchorTransform == null)
            {
                var resGo = new GameObject("ResistorAnchor");
                resGo.transform.SetParent(breadboardRoot, false);
                resGo.transform.localRotation = Quaternion.identity;
                resGo.transform.localScale = Vector3.one;
                ResistorAnchorTransform = resGo.transform;
            }
            // Always enforce the authoritative coordinate regardless of who created the anchor
            ResistorAnchorTransform.localPosition = BreadboardCoordinateSystem.ResolveLogicalName("resistor_center");

            // 2. Locate LedAnchor under BreadboardARRoot (MarkerlessFoundationController creates "LEDAnchor")
            LedAnchorTransform = breadboardRoot.Find("LEDAnchor");
            if (LedAnchorTransform == null) LedAnchorTransform = breadboardRoot.Find("LedAnchor");
            if (LedAnchorTransform == null)
            {
                var ledGo = new GameObject("LedAnchor");
                ledGo.transform.SetParent(breadboardRoot, false);
                ledGo.transform.localRotation = Quaternion.identity;
                ledGo.transform.localScale = Vector3.one;
                LedAnchorTransform = ledGo.transform;
            }
            // Always enforce the authoritative coordinate
            LedAnchorTransform.localPosition = BreadboardCoordinateSystem.ResolveLogicalName("led_center");

            // 3. Build 3D AR Visual Indicators under anchors
            CreateResistorVisual(ResistorAnchorTransform);
            CreateLedVisual(LedAnchorTransform);
            CreateEsp32Visual(breadboardRoot);

            if (CurrentState == ResistorTutorialState.INACTIVE)
            {
                // 4. Enter RESISTOR_TARGET state
                CurrentState = ResistorTutorialState.RESISTOR_TARGET;
                Debug.Log("[A3R_PHASE4] Entered RESISTOR_TARGET state for LED Blinking Experiment Step 1.");
            }
            else
            {
                // We are recovering from an AR restart. Restore visual visibility for completed steps.
                if (CurrentState > ResistorTutorialState.RESISTOR_TARGET)
                {
                    if (_targetVisualObj != null) _targetVisualObj.SetActive(true);
                    if (_targetMaterial != null) _targetMaterial.color = new Color(0.1f, 0.95f, 0.3f, 0.95f);
                }
                if (CurrentState > ResistorTutorialState.LED_TARGET)
                {
                    if (_ledVisualObj != null) _ledVisualObj.SetActive(true);
                }
                if (CurrentState > ResistorTutorialState.ESP32_TARGET)
                {
                    if (_esp32VisualObj != null) _esp32VisualObj.SetActive(true);
                    if (_esp32Material != null) _esp32Material.color = new Color(0.2f, 0.8f, 1f, 0.95f);
                }
                if (CurrentState > ResistorTutorialState.WIRE1_TARGET)
                {
                    DrawWire1();
                    if (_wire1 != null) _wire1.startColor = _wire1.endColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
                }
                if (CurrentState > ResistorTutorialState.WIRE2_TARGET)
                {
                    DrawWire2();
                    if (_wire2 != null) _wire2.startColor = _wire2.endColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
                }
                if (CurrentState > ResistorTutorialState.WIRE3_TARGET)
                {
                    DrawWire3();
                    if (_wire3 != null) _wire3.startColor = _wire3.endColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
                }
                Debug.Log($"[A3R_PHASE4] Restored AR visuals for recovered state {CurrentState}.");
            }
        }

        private void CreateResistorVisual(Transform anchor)
        {
            if (_targetVisualObj != null) Destroy(_targetVisualObj);

            _targetVisualObj = new GameObject("ResistorVisualTarget");
            _targetVisualObj.transform.SetParent(anchor, false);
            _targetVisualObj.transform.localPosition = new Vector3(0f, 0.001f, 0f); // 1mm above breadboard surface
            _targetVisualObj.transform.localRotation = Quaternion.identity;
            _targetVisualObj.transform.localScale = Vector3.one;

            Vector3 leg1Local = BreadboardCoordinateSystem.ResolveLogicalName("resistor_leg1");
            Vector3 leg2Local = BreadboardCoordinateSystem.ResolveLogicalName("resistor_leg2");
            Vector3 centerLocal = BreadboardCoordinateSystem.ResolveLogicalName("resistor_center");

            // Relative offsets from the anchor (which is at centerLocal)
            Vector3 leg1Rel = leg1Local - centerLocal;
            Vector3 leg2Rel = leg2Local - centerLocal;

            // Optional: elevate them slightly so they sit precisely on top of the hole geometry
            leg1Rel.y += 0.0005f; 
            leg2Rel.y += 0.0005f;

            _targetMaterial = ProceduralMeshUtility.CreateMaterial(new Color(1f, 0.85f, 0.1f, 0.95f), false, false);

            // 1. Terminal 1 Target (D15)
            _pin1Obj = new GameObject("Terminal1_Target");
            _pin1Obj.transform.SetParent(_targetVisualObj.transform, false);
            _pin1Obj.transform.localPosition = leg1Rel;
            _pin1Obj.transform.localRotation = Quaternion.identity;
            _pin1Obj.transform.localScale = Vector3.one;

            var mf1 = _pin1Obj.AddComponent<MeshFilter>();
            // Small ring exactly highlighting the hole (2.5mm outer, 1.5mm inner diameter)
            mf1.sharedMesh = ProceduralMeshUtility.CreateFootprintOutlineMesh(0.003f, 0.003f, 0.0005f);
            _pin1Renderer = _pin1Obj.AddComponent<MeshRenderer>();
            _pin1Renderer.material = _targetMaterial;
            _pin1Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pin1Renderer.receiveShadows = false;

            // 2. Terminal 2 Target (D20)
            _pin2Obj = new GameObject("Terminal2_Target");
            _pin2Obj.transform.SetParent(_targetVisualObj.transform, false);
            _pin2Obj.transform.localPosition = leg2Rel;
            _pin2Obj.transform.localRotation = Quaternion.identity;
            _pin2Obj.transform.localScale = Vector3.one;

            var mf2 = _pin2Obj.AddComponent<MeshFilter>();
            mf2.sharedMesh = ProceduralMeshUtility.CreateFootprintOutlineMesh(0.003f, 0.003f, 0.0005f);
            _pin2Renderer = _pin2Obj.AddComponent<MeshRenderer>();
            _pin2Renderer.material = _targetMaterial;
            _pin2Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pin2Renderer.receiveShadows = false;

            // 3. Virtual Resistor Body
            var bodyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyObj.name = "Virtual_Resistor_Body";
            bodyObj.transform.SetParent(_targetVisualObj.transform, false);
            // Center the body between the pins, elevated slightly
            bodyObj.transform.localPosition = new Vector3(0, 0.003f, 0);
            
            // Orient the capsule to span between leg1 and leg2
            Vector3 direction = leg2Rel - leg1Rel;
            if (direction.sqrMagnitude > 0.000001f)
            {
                // Capsule is Y-up by default, so we rotate it so its Y-axis aligns with the direction vector
                bodyObj.transform.rotation = anchor.rotation * Quaternion.FromToRotation(Vector3.up, direction.normalized);
            }
            
            // Scale the capsule to match the physical resistor size
            bodyObj.transform.localScale = new Vector3(0.002f, direction.magnitude * 0.45f, 0.002f);
            
            var col = bodyObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            var bodyRenderer = bodyObj.GetComponent<MeshRenderer>();
            var bodyMat = ProceduralMeshUtility.CreateMaterial(new Color(0.9f, 0.8f, 0.6f, 0.8f), false, false);
            bodyRenderer.material = bodyMat;

            _targetVisualObj.SetActive(false);
        }

        private void CreateLedVisual(Transform anchor)
        {
            if (_ledVisualObj != null) Destroy(_ledVisualObj);

            _ledVisualObj = new GameObject("LedVisualTarget");
            _ledVisualObj.transform.SetParent(anchor, false);
            _ledVisualObj.transform.localPosition = new Vector3(0f, 0.001f, 0f); // 1mm above breadboard surface
            _ledVisualObj.transform.localRotation = Quaternion.identity;
            _ledVisualObj.transform.localScale = Vector3.one;

            Vector3 anodeLocal = BreadboardCoordinateSystem.ResolveLogicalName("led_anode");
            Vector3 cathodeLocal = BreadboardCoordinateSystem.ResolveLogicalName("led_cathode");
            Vector3 centerLocal = BreadboardCoordinateSystem.ResolveLogicalName("led_center");

            // Relative offsets from the anchor (which is at centerLocal)
            Vector3 anodeRel = anodeLocal - centerLocal;
            Vector3 cathodeRel = cathodeLocal - centerLocal;

            // Optional: elevate them slightly so they sit precisely on top of the hole geometry
            anodeRel.y += 0.0005f; 
            cathodeRel.y += 0.0005f;

            _anodeMaterial = ProceduralMeshUtility.CreateMaterial(new Color(1f, 0.2f, 0.2f, 0.95f), false, false);
            _cathodeMaterial = ProceduralMeshUtility.CreateMaterial(new Color(0.2f, 0.4f, 1f, 0.95f), false, false);

            // Anode Target (Row 10)
            var anodeObj = new GameObject("Anode_Target");
            anodeObj.transform.SetParent(_ledVisualObj.transform, false);
            anodeObj.transform.localPosition = anodeRel;
            anodeObj.transform.localRotation = Quaternion.identity;
            anodeObj.transform.localScale = Vector3.one;

            var mf1 = anodeObj.AddComponent<MeshFilter>();
            mf1.sharedMesh = ProceduralMeshUtility.CreateFootprintOutlineMesh(0.003f, 0.003f, 0.0005f);
            var mr1 = anodeObj.AddComponent<MeshRenderer>();
            mr1.material = _anodeMaterial;
            mr1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr1.receiveShadows = false;

            // Cathode Target (Row 11)
            var cathodeObj = new GameObject("Cathode_Target");
            cathodeObj.transform.SetParent(_ledVisualObj.transform, false);
            cathodeObj.transform.localPosition = cathodeRel;
            cathodeObj.transform.localRotation = Quaternion.identity;
            cathodeObj.transform.localScale = Vector3.one;

            var mf2 = cathodeObj.AddComponent<MeshFilter>();
            mf2.sharedMesh = ProceduralMeshUtility.CreateFootprintOutlineMesh(0.003f, 0.003f, 0.0005f);
            var mr2 = cathodeObj.AddComponent<MeshRenderer>();
            mr2.material = _cathodeMaterial;
            mr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr2.receiveShadows = false;

            // Virtual LED Body
            var bodyObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bodyObj.name = "Virtual_LED_Body";
            bodyObj.transform.SetParent(_ledVisualObj.transform, false);
            bodyObj.transform.localPosition = new Vector3(0, 0.004f, 0);
            bodyObj.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
            
            var col = bodyObj.GetComponent<Collider>();
            if (col != null) Destroy(col);
            
            var bodyRenderer = bodyObj.GetComponent<MeshRenderer>();
            var bodyMat = ProceduralMeshUtility.CreateMaterial(new Color(1f, 0.2f, 0.2f, 0.6f), false, false);
            bodyRenderer.material = bodyMat;

            _ledVisualObj.SetActive(false);
        }

        private GameObject _esp32VisualObj;
        private Material _esp32Material;

        // --- Wire System ---
        private Transform _pinGpio23;
        private Transform _pinGnd;
        private LineRenderer _wire1;
        private LineRenderer _wire2;
        private LineRenderer _wire3;
        [SerializeField] private float wireBridgeHeight = 0.015f;

        private void CreateEsp32Visual(Transform breadboardRoot)
        {
            if (_esp32VisualObj != null) Destroy(_esp32VisualObj);

            _esp32VisualObj = new GameObject("Esp32VisualTarget");
            _esp32VisualObj.transform.SetParent(breadboardRoot, false);
            
            // Placement: Beside breadboard, parallel. 
            // FIXED POS MUST NOT CHANGE.
            _esp32VisualObj.transform.localPosition = new Vector3(0.0635f, 0.001f, 0f);
            _esp32VisualObj.transform.localRotation = Quaternion.identity;
            _esp32VisualObj.transform.localScale = Vector3.one;

            GameObject loadedModel = Resources.Load<GameObject>("ESP32_DevKit");
            if (loadedModel != null)
            {
                GameObject cadMesh = Instantiate(loadedModel, _esp32VisualObj.transform);
                cadMesh.name = "ESP32_Model_Root";
                
                // Correct orientation: CAD +Y -> Unity +Z (Antenna=Top)
                cadMesh.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                // Un-mirror the mesh to restore native physical chirality
                cadMesh.transform.localScale = new Vector3(-1f, 1f, 1f);
                // Correct origin: Shift CAD centroid to Unity origin.
                cadMesh.transform.localPosition = new Vector3(-0.012065f, 0.003458f, -0.022830f);

                // Pins parented to the visible cyan ESP32 footprint root
                GameObject pin23 = new GameObject("Pin_GPIO23");
                pin23.transform.SetParent(_esp32VisualObj.transform, false);
                pin23.transform.localPosition = new Vector3(0.0127f, 0.001f, 0.0200f);
                _pinGpio23 = pin23.transform;

                GameObject pinGnd = new GameObject("Pin_GND");
                pinGnd.transform.SetParent(_esp32VisualObj.transform, false);
                pinGnd.transform.localPosition = new Vector3(0.0127f, 0.001f, -0.0130f);
                _pinGnd = pinGnd.transform;

                var renderers = cadMesh.GetComponentsInChildren<MeshRenderer>();
                foreach(var r in renderers) {
                    r.enabled = false;
                }

                // Create a clean cyan footprint outline (29mm x 53.08mm)
                GameObject footprintObj = new GameObject("ESP32_Footprint_Outline");
                footprintObj.transform.SetParent(_esp32VisualObj.transform, false);
                LineRenderer lr = footprintObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.startWidth = 0.003f; // Visually clear thickness
                lr.endWidth = 0.003f;
                lr.positionCount = 4;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = Color.cyan;
                lr.endColor = Color.cyan;

                // Rectangle dimensions: Width (X) = 29.0mm, Length (Z) = 53.08mm
                float hw = 0.029f / 2f;
                float hl = 0.05308f / 2f;
                lr.SetPositions(new Vector3[] {
                    new Vector3(-hw, 0f, -hl),
                    new Vector3(-hw, 0f, hl),
                    new Vector3(hw, 0f, hl),
                    new Vector3(hw, 0f, -hl)
                });
            }
            else
            {
                Debug.LogWarning("[A3R_PHASE4] Failed to load ESP32_DevKit from Resources.");
                MeshFilter mf = _esp32VisualObj.AddComponent<MeshFilter>();
                MeshRenderer mr = _esp32VisualObj.AddComponent<MeshRenderer>();
                mf.mesh = ProceduralMeshUtility.CreateFootprintOutlineMesh(0.025f, 0.050f, 0.002f);
                _esp32Material = ProceduralMeshUtility.CreateMaterial(new Color(0.2f, 0.8f, 1f, 0.9f), false, false);
                mr.material = _esp32Material;
            }

            _esp32VisualObj.SetActive(false);
        }

        private void Update()
        {
            if (CurrentState == ResistorTutorialState.RESISTOR_TARGET)
            {
                // Pulsing amber/yellow animation around ResistorAnchor
                if (_targetMaterial != null)
                {
                    float pulse = Mathf.PingPong(Time.time * 2.5f, 0.45f) + 0.55f;
                    _targetMaterial.color = new Color(1f, 0.85f, 0.1f, pulse);
                }
            }
            else if (CurrentState == ResistorTutorialState.LED_TARGET)
            {
                // Pulsing animation for LED anchor
                if (_anodeMaterial != null && _cathodeMaterial != null)
                {
                    float pulse = Mathf.PingPong(Time.time * 2.5f, 0.45f) + 0.55f;
                    _anodeMaterial.color = new Color(1f, 0.2f, 0.2f, pulse);
                    _cathodeMaterial.color = new Color(0.2f, 0.4f, 1f, pulse);
                }
            }
            else if (CurrentState == ResistorTutorialState.ESP32_TARGET)
            {
                if (_esp32Material != null)
                {
                    float pulse = Mathf.PingPong(Time.time * 2.5f, 0.45f) + 0.55f;
                    _esp32Material.color = new Color(0.2f, 0.8f, 1f, pulse);
                }
            }
        }

        public void TransitionToTarget()
        {
            CurrentState = ResistorTutorialState.RESISTOR_TARGET;
            if (_targetVisualObj != null) _targetVisualObj.SetActive(true);

            var logger = FindFirstObjectByType<SessionLogger>();
            if (logger != null)
            {
                logger.LogTutorialEvent("RESISTOR_STEP_STARTED", "Target active at local pos " + TargetLocalPos.ToString("F3"));
            }
            Debug.Log("[A3R_PHASE4] Transitioned to RESISTOR_TARGET. Target visual active at " + TargetLocalPos.ToString("F3"));
        }

        public void ConfirmResistorPlaced()
        {
            if (CurrentState != ResistorTutorialState.RESISTOR_TARGET) return;

            CurrentState = ResistorTutorialState.LED_TARGET;

            // Stop pulse and turn solid green
            if (_targetMaterial != null)
            {
                _targetMaterial.color = new Color(0.1f, 0.95f, 0.3f, 0.95f);
            }

            var logger = FindFirstObjectByType<SessionLogger>();
            if (logger != null)
            {
                logger.LogTutorialEvent("RESISTOR_PLACEMENT_CONFIRMED", "Confirmed at local pos " + TargetLocalPos.ToString("F3"));
            }
            Debug.Log("[A3R_PHASE4] Resistor placement CONFIRMED by user.");
        }

        public void TransitionToLedTarget()
        {
            CurrentState = ResistorTutorialState.LED_TARGET;
            if (_ledVisualObj != null) _ledVisualObj.SetActive(true);

            var logger = FindFirstObjectByType<SessionLogger>();
            if (logger != null)
            {
                logger.LogTutorialEvent("LED_STEP_STARTED", "Target active.");
            }
            Debug.Log("[A3R_PHASE4] Transitioned to LED_TARGET.");
        }

        public void ConfirmLedPlaced()
        {
            if (CurrentState != ResistorTutorialState.LED_TARGET) return;

            CurrentState = ResistorTutorialState.ESP32_TARGET; // Decoupled from instant reveal

            // Stop pulse and turn solid
            if (_anodeMaterial != null) _anodeMaterial.color = new Color(1f, 0.2f, 0.2f, 0.95f);
            if (_cathodeMaterial != null) _cathodeMaterial.color = new Color(0.2f, 0.4f, 1f, 0.95f);

            var logger = FindFirstObjectByType<SessionLogger>();
            if (logger != null)
            {
                logger.LogTutorialEvent("LED_PLACEMENT_CONFIRMED", "Confirmed LED");
                logger.LogTutorialEvent("ESP32_STEP_STARTED", "Target active.");
            }
            Debug.Log("[A3R_PHASE4] LED placement CONFIRMED by user. Transitioned to ESP32_TARGET.");
        }

        public void TransitionToEsp32Target()
        {
            CurrentState = ResistorTutorialState.ESP32_TARGET;
            if (_esp32VisualObj != null) _esp32VisualObj.SetActive(true);

            var logger = FindFirstObjectByType<SessionLogger>();
            if (logger != null)
            {
                logger.LogTutorialEvent("ESP32_ADAPTIVE_REVEAL", "ESP32 Target revealed adaptively.");
            }
            Debug.Log("[A3R_PHASE4] ESP32 target adaptively revealed.");
        }

        public void ConfirmEsp32Placed()
        {
            if (CurrentState != ResistorTutorialState.ESP32_TARGET) return;

            CurrentState = ResistorTutorialState.WIRE1_TARGET;

            if (_esp32Material != null) _esp32Material.color = new Color(0.2f, 0.8f, 1f, 0.95f);

            var logger = FindFirstObjectByType<SessionLogger>();
            if (logger != null) logger.LogTutorialEvent("ESP32_PLACEMENT_CONFIRMED", "Confirmed ESP32");
            Debug.Log("[A3R_PHASE4] ESP32 placement CONFIRMED by user.");
        }

        public void TransitionToWire1Target()
        {
            CurrentState = ResistorTutorialState.WIRE1_TARGET;
            DrawWire1();
            Debug.Log("[A3R_PHASE4] Transitioned to WIRE1_TARGET.");
        }

        public void ConfirmWire1Placed()
        {
            if (CurrentState != ResistorTutorialState.WIRE1_TARGET) return;
            CurrentState = ResistorTutorialState.WIRE2_TARGET;
            if (_wire1 != null) _wire1.startColor = _wire1.endColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
            Debug.Log("[A3R_PHASE4] Wire 1 CONFIRMED.");
        }

        public void TransitionToWire2Target()
        {
            CurrentState = ResistorTutorialState.WIRE2_TARGET;
            DrawWire2();
            Debug.Log("[A3R_PHASE4] Transitioned to WIRE2_TARGET.");
        }

        public void ConfirmWire2Placed()
        {
            if (CurrentState != ResistorTutorialState.WIRE2_TARGET) return;
            CurrentState = ResistorTutorialState.WIRE3_TARGET;
            if (_wire2 != null) _wire2.startColor = _wire2.endColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
            Debug.Log("[A3R_PHASE4] Wire 2 CONFIRMED.");
        }

        public void TransitionToWire3Target()
        {
            CurrentState = ResistorTutorialState.WIRE3_TARGET;
            DrawWire3();
            Debug.Log("[A3R_PHASE4] Transitioned to WIRE3_TARGET.");
        }

        public void ConfirmWire3Placed()
        {
            if (CurrentState != ResistorTutorialState.WIRE3_TARGET) return;
            CurrentState = ResistorTutorialState.STEP_COMPLETE;
            if (_wire3 != null) _wire3.startColor = _wire3.endColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
            Debug.Log("[A3R_PHASE4] Wire 3 CONFIRMED.");
        }

        public void CompleteTutorial()
        {
            CurrentState = ResistorTutorialState.STEP_COMPLETE;
            Debug.Log("[A3R_PHASE4] Tutorial Complete.");
        }

        private void RestartTutorial()
        {
            var foundation = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            if (foundation != null && foundation.HasPlacedRoot && foundation.BreadboardRootTransform != null)
            {
                CurrentState = ResistorTutorialState.INACTIVE;
                HandleBreadboardPlaced(foundation.BreadboardRootTransform);
            }
            else
            {
                // If AR is not currently registered, just reset state
                CurrentState = ResistorTutorialState.INACTIVE;
            }

            var contentManager = FindFirstObjectByType<A3R.ContentManager.ARContentManager>();
            if (contentManager != null) contentManager.RestartTask();
            
            Debug.Log("[A3R] Tutorial Restarted.");
        }

        private void RestartARFromTutorial()
        {
            // 1. Genuinely reset tutorial step and adaptation state back to start
            CurrentState = ResistorTutorialState.INACTIVE;
            var contentManager = FindFirstObjectByType<A3R.ContentManager.ARContentManager>();
            if (contentManager != null) contentManager.RestartTask();

            // 2. Clear AR surface registration and anchors
            var foundation = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            if (foundation != null)
            {
                foundation.RestartAR();
            }
            
            Debug.Log("[A3R] Session and AR Restarted.");
        }

        private LineRenderer CreateWireRenderer(string name, Color color, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.startWidth = 0.0015f;
            lr.endWidth = 0.0015f;
            lr.material = ProceduralMeshUtility.CreateMaterial(color, false, false);
            lr.positionCount = 4;
            return lr;
        }

        private void SetWirePositions(LineRenderer lr, Vector3 localStart, Vector3 localEnd)
        {
            Vector3 mid1 = localStart; mid1.y += wireBridgeHeight;
            Vector3 mid2 = localEnd; mid2.y += wireBridgeHeight;
            lr.SetPositions(new Vector3[] { localStart, mid1, mid2, localEnd });
        }

        private void DrawWire1()
        {
            if (_wire1 != null) Destroy(_wire1.gameObject);
            _wire1 = CreateWireRenderer("Wire1_Visual", new Color(1f, 0.2f, 0.2f, 0.8f), _esp32VisualObj.transform.parent);
            
            Vector3 startLocal = _pinGpio23 != null ? _esp32VisualObj.transform.parent.InverseTransformPoint(_pinGpio23.position) : Vector3.zero;
            Vector3 endLocal = A3R_BreadboardMap.GetHolePosition("E15");
            SetWirePositions(_wire1, startLocal, endLocal);
        }

        private void DrawWire2()
        {
            if (_wire2 != null) Destroy(_wire2.gameObject);
            _wire2 = CreateWireRenderer("Wire2_Visual", new Color(0.2f, 0.8f, 1f, 0.8f), _esp32VisualObj.transform.parent);
            
            Vector3 startLocal = A3R_BreadboardMap.GetHolePosition("E20");
            Vector3 endLocal = A3R_BreadboardMap.GetHolePosition("E10");
            SetWirePositions(_wire2, startLocal, endLocal);
        }

        private void DrawWire3()
        {
            if (_wire3 != null) Destroy(_wire3.gameObject);
            _wire3 = CreateWireRenderer("Wire3_Visual", new Color(0.2f, 0.2f, 0.2f, 0.8f), _esp32VisualObj.transform.parent);
            
            Vector3 startLocal = A3R_BreadboardMap.GetHolePosition("E11");
            Vector3 endLocal = _pinGnd != null ? _esp32VisualObj.transform.parent.InverseTransformPoint(_pinGnd.position) : Vector3.zero;
            SetWirePositions(_wire3, startLocal, endLocal);
        }

        private void OnGUI()
        {
            // Legacy IMGUI disabled in favor of A3R_CleanUIController
            return;

            var oldBg = GUI.backgroundColor;

            // Recovery Controls (Top Left)
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.95f);
            if (GUI.Button(new Rect(24f, 24f, 240f, 60f), "RESTART TUTORIAL", _btnStyle))
            {
                RestartTutorial();
            }
            if (GUI.Button(new Rect(288f, 24f, 240f, 60f), "RESTART AR", _btnStyle))
            {
                RestartARFromTutorial();
            }

            float cardW = Mathf.Min(Screen.width * 0.46f, 440f);
            float cardX = Screen.width - cardW - 24f;
            float cardY = Screen.height * 0.38f;
            float cardH = 220f;

            // Semi-transparent background card
            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.92f);
            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, _cardStyle);

            float pad = 16f;
            float contentX = cardX + pad;
            float contentW = cardW - (pad * 2f);

            if (CurrentState == ResistorTutorialState.RESISTOR_TARGET)
            {
                GUI.Label(new Rect(contentX, cardY + 16f, contentW, 30f), "Step 1", _titleStyle);
                GUI.Label(new Rect(contentX, cardY + 52f, contentW, 60f), "Place the resistor at the highlighted location.", _bodyStyle);

                GUI.backgroundColor = new Color(0.1f, 0.9f, 0.35f, 0.95f); // Bright Green
                if (GUI.Button(new Rect(contentX, cardY + 130f, contentW, 65f), "✓ RESISTOR PLACED", _btnStyle))
                {
                    ConfirmResistorPlaced();
                }
            }
            else if (CurrentState == ResistorTutorialState.LED_TARGET)
            {
                GUI.Label(new Rect(contentX, cardY + 16f, contentW, 30f), "Step 2: Place LED", _titleStyle);
                GUI.Label(new Rect(contentX, cardY + 52f, contentW, 60f), "Place the LED.\n(+) Anode to Row 10 (Red)\n(-) Cathode to Row 11 (Blue)", _bodyStyle);

                GUI.backgroundColor = new Color(0.1f, 0.9f, 0.35f, 0.95f); // Bright Green
                if (GUI.Button(new Rect(contentX, cardY + 130f, contentW, 65f), "✓ LED PLACED", _btnStyle))
                {
                    ConfirmLedPlaced();
                }
            }
            else if (CurrentState == ResistorTutorialState.ESP32_TARGET)
            {
                GUI.Label(new Rect(contentX, cardY + 16f, contentW, 30f), "Step 3: Place ESP32", _titleStyle);
                GUI.Label(new Rect(contentX, cardY + 52f, contentW, 60f), "Place the ESP32 in the highlighted 50x25mm area beside the breadboard.", _bodyStyle);

                GUI.backgroundColor = new Color(0.1f, 0.9f, 0.35f, 0.95f); // Bright Green
                if (GUI.Button(new Rect(contentX, cardY + 130f, contentW, 65f), "✓ ESP32 PLACED", _btnStyle))
                {
                    ConfirmEsp32Placed();
                }
            }
            else if (CurrentState == ResistorTutorialState.WIRE1_TARGET)
            {
                GUI.Label(new Rect(contentX, cardY + 16f, contentW, 30f), "Step 4: Wire 1", _titleStyle);
                GUI.Label(new Rect(contentX, cardY + 52f, contentW, 60f), "Place Wire 1 (Red).\nFrom: ESP32 GPIO23\nTo: Breadboard E15", _bodyStyle);

                GUI.backgroundColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
                if (GUI.Button(new Rect(contentX, cardY + 130f, contentW, 65f), "✓ WIRE 1 PLACED", _btnStyle))
                {
                    ConfirmWire1Placed();
                }
            }
            else if (CurrentState == ResistorTutorialState.WIRE2_TARGET)
            {
                GUI.Label(new Rect(contentX, cardY + 16f, contentW, 30f), "Step 5: Wire 2", _titleStyle);
                GUI.Label(new Rect(contentX, cardY + 52f, contentW, 60f), "Place Wire 2 (Blue).\nFrom: Breadboard E20\nTo: Breadboard E10", _bodyStyle);

                GUI.backgroundColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
                if (GUI.Button(new Rect(contentX, cardY + 130f, contentW, 65f), "✓ WIRE 2 PLACED", _btnStyle))
                    ConfirmWire2Placed();
            }
            else if (CurrentState == ResistorTutorialState.WIRE3_TARGET)
            {
                GUI.Label(new Rect(contentX, cardY + 16f, contentW, 30f), "Step 6: Wire 3", _titleStyle);
                GUI.Label(new Rect(contentX, cardY + 52f, contentW, 60f), "Place Wire 3 (Black).\nFrom: Breadboard E11\nTo: ESP32 GND", _bodyStyle);

                GUI.backgroundColor = new Color(0.1f, 0.9f, 0.35f, 0.95f);
                if (GUI.Button(new Rect(contentX, cardY + 130f, contentW, 65f), "✓ WIRE 3 PLACED", _btnStyle))
                    ConfirmWire3Placed();
            }
            else if (CurrentState == ResistorTutorialState.STEP_COMPLETE)
            {
                GUI.Label(new Rect(contentX, cardY + 16f, contentW, 30f), "Assembly Complete", _titleStyle);
                GUI.Label(new Rect(contentX, cardY + 55f, contentW, 60f), "All components placed and wired.", _bodyStyle);

                GUI.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.9f);
                GUI.Box(new Rect(contentX, cardY + 135f, contentW, 55f), "✔ VERIFIED", _btnStyle);
            }

            GUI.backgroundColor = oldBg;
        }

        private void InitStyles()
        {
            if (_guiStylesInit) return;

            _cardStyle = new GUIStyle(GUI.skin.box);

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _titleStyle.normal.textColor = new Color(0f, 1f, 1f, 1f); // Cyan

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            _bodyStyle.normal.textColor = Color.white;

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _btnStyle.normal.textColor = Color.black;

            _guiStylesInit = true;
        }
    }
}
