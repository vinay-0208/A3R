using System;
using System.Collections.Generic;
using UnityEngine;
using A3R.ContentManager;
using A3R.Adaptation;
using A3R.ARTracking;

namespace A3R.ARGuidance
{
    public class ARGuidanceController : MonoBehaviour
    {
        [Header("Subsystem References")]
        [SerializeField] private ARContentManager contentManager;
        [SerializeField] private ARImageTracker arImageTracker;
        [SerializeField] private ARWorkbenchTracker arWorkbenchTracker;

        [Header("Guidance Visual Instances")]
        [SerializeField] private A3R_DirectionalArrow directionalArrow;
        [SerializeField] private A3R_TargetRing targetRing;
        [SerializeField] private GameObject highlightAnchor;

        [Header("Step Target Parent")]
        [SerializeField] private Transform contentRootTransform;

        private ARGuidanceStepDefinition[] _stepDefinitions;
        private readonly Dictionary<string, Transform> _cachedTargetTransforms = new Dictionary<string, Transform>();

        private int _currentStepIndex = -1;
        private GuidanceVisualState _currentState = GuidanceVisualState.Normal;
        private bool _isTracking = false;
        private bool _initialized = false;

        public int CurrentStepIndex => _currentStepIndex;
        public GuidanceVisualState CurrentState => _currentState;
        public bool IsTracking => _isTracking;
        public A3R_DirectionalArrow DirectionalArrow => directionalArrow;
        public A3R_TargetRing TargetRing => targetRing;
        public ARGuidanceStepDefinition CurrentStepDefinition =>
            (_stepDefinitions != null && _currentStepIndex >= 0 && _currentStepIndex < _stepDefinitions.Length)
            ? _stepDefinitions[_currentStepIndex] : null;

        private void Awake()
        {
            InitializeStepDefinitions();
            EnsureReferences();
            InitializeVisualElements();
            
            // Inject Diagnostics Overlay
            if (FindFirstObjectByType<A3R.Diagnostics.A3R_ARDiagnosticsUI>() == null)
            {
                var diagObj = new GameObject("A3R_ARDiagnostics");
                diagObj.AddComponent<A3R.Diagnostics.A3R_ARDiagnosticsUI>();
            }

            // Inject ESP32 Circuit Guidance
            if (GetComponent<ESP32CircuitGuidance>() == null)
            {
                gameObject.AddComponent<ESP32CircuitGuidance>();
            }

            SubscribeEvents(); // Subscribe early to prevent missing events
        }

        private void Start()
        {
            // If markerless is already tracking (e.g. injected or placed immediately), sync state
            if (arWorkbenchTracker != null && arWorkbenchTracker.enableMarkerless && arWorkbenchTracker.IsTracking)
            {
                OnMarkerDetected(arWorkbenchTracker.ContentRoot);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void InitializeStepDefinitions()
        {
            _stepDefinitions = new ARGuidanceStepDefinition[]
            {
                new ARGuidanceStepDefinition(0, "place_breadboard", "breadboard_center", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(1, "insert_resistor", "resistor_center", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(2, "insert_led", "led_center", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(3, "wire_led_to_resistor", "wire1_center", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(4, "wire_to_gpio25", "wire2_center", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(5, "wire_to_gnd", "wire3_center", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(6, "power_upload", "esp32_usb", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(7, "verify_blink", "led_center", Vector3.zero, false, false, 0f, Color.white, "", "", ""),
                new ARGuidanceStepDefinition(8, "success", "led_center", Vector3.zero, false, false, 0f, Color.white, "", "", "")
            };
        }

        private void EnsureReferences()
        {
            if (contentManager == null)
                contentManager = GetComponent<ARContentManager>() ?? FindFirstObjectByType<ARContentManager>();

            if (arImageTracker == null)
                arImageTracker = GetComponent<ARImageTracker>() ?? FindFirstObjectByType<ARImageTracker>();

            if (arWorkbenchTracker == null)
                arWorkbenchTracker = GetComponent<ARWorkbenchTracker>() ?? FindFirstObjectByType<ARWorkbenchTracker>();
            
            if (arWorkbenchTracker != null)
            {
                arWorkbenchTracker.enableMarkerless = false;
                arWorkbenchTracker.enabled = false;
            }
        }

        private void InitializeVisualElements()
        {
            if (_initialized) return;

            if (directionalArrow == null)
            {
                GameObject arrowObj = new GameObject("A3R_DirectionalArrow");
                arrowObj.transform.SetParent(null, false);
                directionalArrow = arrowObj.AddComponent<A3R_DirectionalArrow>();
            }

            if (targetRing == null)
            {
                GameObject ringObj = new GameObject("A3R_TargetRing");
                ringObj.transform.SetParent(null, false);
                targetRing = ringObj.AddComponent<A3R_TargetRing>();
            }

            if (highlightAnchor == null)
            {
                highlightAnchor = new GameObject("A3R_HighlightAnchor");
                highlightAnchor.transform.SetParent(null, false);
            }

            directionalArrow.gameObject.SetActive(false);
            targetRing.gameObject.SetActive(false);
            highlightAnchor.SetActive(false);

            _initialized = true;
        }

        private void SubscribeEvents()
        {
            if (contentManager != null)
            {
                contentManager.OnStepChanged += OnStepChanged;
                contentManager.OnTaskComplete += OnTaskComplete;
            }

            if (arImageTracker != null)
            {
                arImageTracker.OnMarkerDetected += OnMarkerDetected;
                arImageTracker.OnMarkerUpdated += OnMarkerUpdated;
                arImageTracker.OnMarkerLost += OnMarkerLost;
            }

            if (arWorkbenchTracker != null)
            {
                arWorkbenchTracker.OnWorkbenchDetected += OnMarkerDetected;
                arWorkbenchTracker.OnWorkbenchUpdated += OnMarkerUpdated;
                arWorkbenchTracker.OnWorkbenchLost += OnMarkerLost;
            }

            var markerless = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            if (markerless != null)
            {
                markerless.OnBreadboardPlaced += OnMarkerDetected;
            }
        }

        private void UnsubscribeEvents()
        {
            if (contentManager != null)
            {
                contentManager.OnStepChanged -= OnStepChanged;
                contentManager.OnTaskComplete -= OnTaskComplete;
            }

            if (arImageTracker != null)
            {
                arImageTracker.OnMarkerDetected -= OnMarkerDetected;
                arImageTracker.OnMarkerUpdated -= OnMarkerUpdated;
                arImageTracker.OnMarkerLost -= OnMarkerLost;
            }

            if (arWorkbenchTracker != null)
            {
                arWorkbenchTracker.OnWorkbenchDetected -= OnMarkerDetected;
                arWorkbenchTracker.OnWorkbenchUpdated -= OnMarkerUpdated;
                arWorkbenchTracker.OnWorkbenchLost -= OnMarkerLost;
            }

            var markerless = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            if (markerless != null)
            {
                markerless.OnBreadboardPlaced -= OnMarkerDetected;
            }
        }



        public void OnStepChanged(int stepIndex, InstructionStep step)
        {
            _currentStepIndex = stepIndex;
            _currentState = GuidanceVisualState.Normal;
            UpdateGuidanceVisuals();
        }

        public void OnTaskComplete()
        {
            _currentState = GuidanceVisualState.Hidden;
            directionalArrow?.SetVisualState(GuidanceVisualState.Hidden);
            targetRing?.SetVisualState(GuidanceVisualState.Hidden);
            if (highlightAnchor != null) highlightAnchor.SetActive(false);
        }

        public void OnMarkerDetected(Transform contentRoot)
        {
            if (arWorkbenchTracker != null && arWorkbenchTracker.enableMarkerless)
            {
                // If this event came from ImageTracker, ignore it if Markerless is active and tracking
                if (contentRoot != arWorkbenchTracker.ContentRoot && arWorkbenchTracker.IsTracking)
                    return;
            }

            contentRootTransform = contentRoot;
            
            var esp = GetComponent<ESP32CircuitGuidance>();
            if (esp != null) esp.SetBreadboardRoot(contentRoot);
            
            _isTracking = true;
            directionalArrow?.SetTrackingState(true);
            targetRing?.SetTrackingState(true);
            UpdateGuidanceVisuals();
        }

        public void OnMarkerUpdated(Transform contentRoot)
        {
            if (arWorkbenchTracker != null && arWorkbenchTracker.enableMarkerless)
            {
                if (contentRoot != arWorkbenchTracker.ContentRoot && arWorkbenchTracker.IsTracking)
                    return;
            }
            contentRootTransform = contentRoot;
            
            var esp = GetComponent<ESP32CircuitGuidance>();
            if (esp != null) esp.SetBreadboardRoot(contentRoot);
            
            _isTracking = true;
            directionalArrow?.SetTrackingState(true);
            targetRing?.SetTrackingState(true);
            UpdateGuidanceVisuals();
        }

        public void OnMarkerLost()
        {
            // If markerless is active, and the image tracker just lost tracking, ignore it!
            if (arWorkbenchTracker != null && arWorkbenchTracker.enableMarkerless)
            {
                // Only truly lose tracking if the workbench is NOT tracking
                if (arWorkbenchTracker.IsTracking)
                    return;
            }
            
            _isTracking = false;
            directionalArrow?.SetTrackingState(false);
            targetRing?.SetTrackingState(false);
            if (highlightAnchor != null) highlightAnchor.SetActive(false);
        }

        public void ApplyVisualAdaptation(AdaptationAction action)
        {
            switch (action)
            {
                case AdaptationAction.InjectCue:
                    _currentState = GuidanceVisualState.ProminentCue;
                    directionalArrow?.SetVisualState(GuidanceVisualState.ProminentCue);
                    targetRing?.SetVisualState(GuidanceVisualState.ProminentCue);
                    break;

                case AdaptationAction.InsertHint:
                    _currentState = GuidanceVisualState.HintEmphasized;
                    directionalArrow?.SetVisualState(GuidanceVisualState.HintEmphasized);
                    targetRing?.SetVisualState(GuidanceVisualState.HintEmphasized);
                    break;

                case AdaptationAction.SlowPace:
                    _currentState = GuidanceVisualState.PacedSlow;
                    directionalArrow?.SetVisualState(GuidanceVisualState.PacedSlow);
                    targetRing?.SetVisualState(GuidanceVisualState.PacedSlow);
                    break;

                case AdaptationAction.SimplifyStep:
                    _currentState = GuidanceVisualState.IsolatedStep;
                    directionalArrow?.SetVisualState(GuidanceVisualState.IsolatedStep);
                    targetRing?.SetVisualState(GuidanceVisualState.IsolatedStep);
                    break;
            }
        }

        public void ResetToNormalGuidance()
        {
            _currentState = GuidanceVisualState.Normal;
            directionalArrow?.SetVisualState(GuidanceVisualState.Normal);
            targetRing?.SetVisualState(GuidanceVisualState.Normal);
        }

        private float _currentScale = 1.0f;
        private float _targetScale = 1.0f;
        private float _lastDistance = -1f;

        private void Update()
        {
            if (!_isTracking || contentRootTransform == null) return;
            // Phase 3 requirement: strictly NO camera-distance scaling.
            // Guidance visual elements remain at authoritative local scale (1, 1, 1).
            ApplyScale(1.0f);
        }

        private void ApplyScale(float scale)
        {
            Vector3 vScale = new Vector3(scale, scale, scale);
            if (directionalArrow != null) directionalArrow.transform.localScale = vScale;
            if (targetRing != null) targetRing.transform.localScale = vScale;
            if (highlightAnchor != null) highlightAnchor.transform.localScale = vScale;
        }

        private void UpdateGuidanceVisuals()
        {
            if (!_isTracking)
            {
                directionalArrow?.gameObject.SetActive(false);
                targetRing?.gameObject.SetActive(false);
                if (highlightAnchor != null) highlightAnchor.SetActive(false);
                return;
            }

            int stepIndex = _currentStepIndex < 0 ? 0 : _currentStepIndex;
            if (stepIndex >= _stepDefinitions.Length) stepIndex = _stepDefinitions.Length - 1;

            var def = _stepDefinitions[stepIndex];

            if (string.IsNullOrEmpty(def.targetName))
            {
                directionalArrow?.gameObject.SetActive(false);
                targetRing?.gameObject.SetActive(false);
                if (highlightAnchor != null) highlightAnchor.SetActive(false);
                return;
            }

            Transform targetTransform = ResolveTargetTransform(def);

            if (directionalArrow != null)
            {
                directionalArrow.SetTarget(targetTransform, Vector3.zero, def.ringColor);
                if (def.showArrow)
                {
                    directionalArrow.SetVisualState(_currentState);
                }
                else
                {
                    directionalArrow.gameObject.SetActive(false);
                }
            }

            if (targetRing != null)
            {
                targetRing.SetTarget(targetTransform, Vector3.zero, def.ringColor, def.ringRadius);
                if (def.showRing)
                {
                    targetRing.SetVisualState(_currentState);
                }
                else
                {
                    targetRing.gameObject.SetActive(false);
                }
            }
        }

        public string GetTargetAlias(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return targetName;
            if (targetName.Equals("led_center", StringComparison.OrdinalIgnoreCase)) return "LEDAnchor";
            if (targetName.Equals("resistor_center", StringComparison.OrdinalIgnoreCase)) return "ResistorAnchor";
            if (targetName.Equals("esp32_usb", StringComparison.OrdinalIgnoreCase)) return "ESP32Anchor";
            if (targetName.Equals("wire1_center", StringComparison.OrdinalIgnoreCase)) return "WireAnchors/WireAnchor_01";
            if (targetName.Equals("wire2_center", StringComparison.OrdinalIgnoreCase)) return "WireAnchors/WireAnchor_02";
            if (targetName.Equals("wire3_center", StringComparison.OrdinalIgnoreCase)) return "WireAnchors/WireAnchor_03";
            if (targetName.Equals("breadboard_center", StringComparison.OrdinalIgnoreCase)) return "BreadboardBounds";
            return targetName;
        }

        private Transform ResolveTargetTransform(ARGuidanceStepDefinition def)
        {
            if (contentRootTransform == null)
            {
                if (highlightAnchor != null)
                {
                    highlightAnchor.transform.localPosition = def.targetLocalOffset;
                    return highlightAnchor.transform;
                }
                return this.transform;
            }

            // Phase 3: Check Hardware Anchor aliases first
            string alias = GetTargetAlias(def.targetName);

            if (!string.IsNullOrEmpty(alias) && alias != def.targetName)
            {
                Transform aliasFound = contentRootTransform.Find(alias);
                if (aliasFound != null) return aliasFound;
            }

            Transform found = contentRootTransform.Find(def.targetName);
            if (found == null)
                found = contentRootTransform.Find("TargetAnchors/" + def.targetName);

            if (found == null)
            {
                Transform[] allChildren = contentRootTransform.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allChildren.Length; i++)
                {
                    if (allChildren[i].name.Equals(def.targetName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = allChildren[i];
                        break;
                    }
                }
            }

            if (found != null)
                return found;

            if (_cachedTargetTransforms.TryGetValue(def.targetName, out Transform cached) && cached != null)
                return cached;

            GameObject anchorObj = new GameObject("Target_" + def.targetName);
            anchorObj.transform.SetParent(contentRootTransform, false);
            anchorObj.transform.localPosition = def.targetLocalOffset;
            anchorObj.transform.localRotation = Quaternion.identity;
            _cachedTargetTransforms[def.targetName] = anchorObj.transform;
            return anchorObj.transform;
        }
    }
}


