using UnityEngine;

namespace A3R.ARGuidance
{
    /// <summary>
    /// A3R_TargetRing — Modern AR target reticle system featuring:
    /// - 4-segment outer quadrant ring with corner framing gaps
    /// - 4 outer corner L-brackets
    /// - Inner concentric alignment ring
    /// - 4 cardinal crosshair alignment ticks
    /// - Central target anchor dot
    /// - Vertical holographic target beacon with floating diamond marker
    /// - Emissive pulse, gentle planar rotation, and state-adaptive palette.
    /// </summary>
    public class A3R_TargetRing : MonoBehaviour
    {
        [Header("Geometry")]
        [SerializeField] private float innerRadius = 0.05f;
        [SerializeField] private float outerRadius = 0.06f; // 12cm total diameter
        [SerializeField] private float thickness = 0.002f;  // 2mm flat

        [Header("Animation & Pulse")]
        [SerializeField] private float basePulseFrequency = 2.0f;
        [SerializeField] private float pulseAmplitude = 0.06f; // Subtle pulse
        [SerializeField] private float rotationSpeed = 12.0f; // Subtle degrees per second

        [Header("Beacon Settings")]
        [SerializeField] private bool enableBeacon = false; // Disable holographic beam
        [SerializeField] private float beaconHeight = 0.045f;
        [SerializeField] private float beamRadius = 0.001f;
        [SerializeField] private float markerRadius = 0.005f;

        [Header("Emissive Palette")]
        private static readonly Color ColorNormal = new Color(0.0f, 0.90f, 1.0f, 0.95f);
        private static readonly Color ColorCue = new Color(0.25f, 1.0f, 1.0f, 1.0f);
        private static readonly Color ColorHint = new Color(0.70f, 0.28f, 1.0f, 0.95f);
        private static readonly Color ColorSlow = new Color(0.16f, 0.47f, 1.0f, 0.95f);
        private static readonly Color ColorSimplify = new Color(1.0f, 0.57f, 0.0f, 0.95f);

        [SerializeField] private Color ringColor = ColorNormal;

        private void ResetGeometryToDefaults()
        {
            innerRadius = 0.05f;
            outerRadius = 0.06f;
            thickness = 0.002f;
            enableBeacon = false;
        }

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;

        // Holographic vertical beacon child
        private GameObject _beaconObject;
        private MeshFilter _beaconFilter;
        private MeshRenderer _beaconRenderer;
        private Material _beaconMaterial;

        private Vector3 _baseScale = Vector3.one;
        private float _prominenceScale = 1.0f;
        private float _currentPulseFreq = 2.5f;
        private Transform _targetTransform;
        private Vector3 _targetOffset = Vector3.zero;
        private bool _isTrackingValid = true;
        private GuidanceVisualState _visualState = GuidanceVisualState.Normal;

        private float _currentRadius = 0.038f;

        public GuidanceVisualState CurrentState => _visualState;
        public bool IsVisible => gameObject.activeInHierarchy && _isTrackingValid && _visualState != GuidanceVisualState.Hidden;
        public Color CurrentColor => ringColor;
        public GameObject BeaconObject => _beaconObject;
        public Transform TargetTransform => _targetTransform;
        public Vector3 TargetOffset => _targetOffset;
        public float CurrentRadius => _currentRadius;

        private void Awake()
        {
            ResetGeometryToDefaults();
            InitializeVisuals();
        }

        public void InitializeVisuals()
        {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            if (_meshFilter.sharedMesh == null)
                _meshFilter.sharedMesh = ProceduralMeshUtility.CreateReticleMesh(innerRadius, outerRadius, thickness);

            if (_material == null)
            {
                _material = ProceduralMeshUtility.CreateMaterial(ringColor, true, true);
                _meshRenderer.material = _material;
            }

            // Holographic vertical beacon
            if (enableBeacon && _beaconObject == null)
            {
                Transform existingBeacon = transform.Find("TargetBeacon");
                if (existingBeacon != null)
                {
                    _beaconObject = existingBeacon.gameObject;
                    _beaconFilter = _beaconObject.GetComponent<MeshFilter>();
                    _beaconRenderer = _beaconObject.GetComponent<MeshRenderer>();
                }
                else
                {
                    _beaconObject = new GameObject("TargetBeacon");
                    _beaconObject.transform.SetParent(transform, false);
                    _beaconObject.transform.localPosition = Vector3.zero;
                    _beaconFilter = _beaconObject.AddComponent<MeshFilter>();
                    _beaconRenderer = _beaconObject.AddComponent<MeshRenderer>();
                }

                _beaconFilter.sharedMesh = ProceduralMeshUtility.CreateBeaconMesh(beaconHeight, beamRadius, markerRadius);
                _beaconMaterial = ProceduralMeshUtility.CreateMaterial(ringColor, true, true);
                _beaconRenderer.material = _beaconMaterial;
            }

            _baseScale = transform.localScale;
            _currentPulseFreq = basePulseFrequency;
            SetVisualState(_visualState);
        }

        private void Update()
        {
            if (!_isTrackingValid || _visualState == GuidanceVisualState.Hidden)
                return;

            if (_targetTransform != null)
            {
                transform.position = _targetTransform.position + _targetOffset;
                
                // Keep the ring flat on the table, matching the target's rotation exactly.
                transform.rotation = _targetTransform.rotation;
            }

            // Radial sinusoidal breathing pulse
            float sin = Mathf.Sin(Time.time * _currentPulseFreq);
            float scaleMultiplier = _prominenceScale * (1.0f + sin * pulseAmplitude);
            transform.localScale = _baseScale * scaleMultiplier;

            // Gentle planar rotation
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        public void SetTarget(Transform target, Vector3 offset, Color color, float radius = 0.038f)
        {
            _targetTransform = target;
            _targetOffset = offset;
            _currentRadius = radius;

            if (_meshFilter == null || _meshFilter.sharedMesh == null || (_beaconObject == null && enableBeacon))
            {
                InitializeVisuals();
            }

            if (radius > 0.005f)
            {
                _baseScale = Vector3.one * (radius / 0.038f);
            }

            if (_visualState == GuidanceVisualState.Normal)
            {
                ringColor = ColorNormal;
            }

            UpdateMaterials();

            if (target != null)
            {
                transform.position = target.position + offset;
                gameObject.SetActive(_isTrackingValid && _visualState != GuidanceVisualState.Hidden);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void SetVisualState(GuidanceVisualState state)
        {
            _visualState = state;

            switch (state)
            {
                case GuidanceVisualState.Hidden:
                    gameObject.SetActive(false);
                    return;

                case GuidanceVisualState.Normal:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.0f;
                    _currentPulseFreq = basePulseFrequency;
                    ringColor = ColorNormal;
                    break;

                case GuidanceVisualState.ProminentCue:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.30f;
                    _currentPulseFreq = basePulseFrequency * 1.6f;
                    ringColor = ColorCue;
                    break;

                case GuidanceVisualState.HintEmphasized:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.15f;
                    _currentPulseFreq = basePulseFrequency * 1.2f;
                    ringColor = ColorHint;
                    break;

                case GuidanceVisualState.PacedSlow:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.0f;
                    _currentPulseFreq = basePulseFrequency * 0.6f;
                    ringColor = ColorSlow;
                    break;

                case GuidanceVisualState.IsolatedStep:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.20f;
                    _currentPulseFreq = basePulseFrequency * 1.3f;
                    ringColor = ColorSimplify;
                    break;
            }

            UpdateMaterials();
        }

        public void SetTrackingState(bool isTracking)
        {
            _isTrackingValid = isTracking;
            if (!isTracking)
            {
                gameObject.SetActive(false);
            }
            else if (_targetTransform != null && _visualState != GuidanceVisualState.Hidden)
            {
                gameObject.SetActive(true);
            }
        }

        private void UpdateMaterials()
        {
            float mult = (_visualState == GuidanceVisualState.ProminentCue) ? 2.4f :
                         (_visualState == GuidanceVisualState.IsolatedStep) ? 2.1f : 1.6f;

            if (_material != null)
            {
                ProceduralMeshUtility.SetMaterialColor(_material, ringColor, mult);
            }

            if (_beaconMaterial != null)
            {
                ProceduralMeshUtility.SetMaterialColor(_beaconMaterial, ringColor, mult * 1.2f);
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }

            if (_beaconMaterial != null)
            {
                Destroy(_beaconMaterial);
                _beaconMaterial = null;
            }
        }
    }
}
