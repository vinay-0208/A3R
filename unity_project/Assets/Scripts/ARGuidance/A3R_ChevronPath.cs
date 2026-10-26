using System.Collections.Generic;
using UnityEngine;

namespace A3R.ARGuidance
{
    public class A3R_ChevronPath : MonoBehaviour
    {
        [Header("Path Configuration")]
        [SerializeField] private int chevronCount = 4;
        [Header("Geometry Configuration")]
        [SerializeField] private float chevronWidth = 0.07f;     // 7 cm wide
        [SerializeField] private float chevronLength = 0.09f;    // 9 cm long
        [SerializeField] private float armThickness = 0.015f;    // 1.5 cm thick arms
        [SerializeField] private float chevronHeight = 0.002f;   // 2 mm tall (virtually flat)
        [SerializeField] private float pathElevation = 0.005f;   // 5 mm above tabletop

        [Header("Animation Settings")]
        [SerializeField] private float baseWaveSpeed = 1.05f;
        [SerializeField] private float pulseScaleAmount = 0.12f;
        [SerializeField] private float baseEmissionIntensity = 1.5f;
        [SerializeField] private float peakEmissionIntensity = 2.4f;

        [Header("Emissive Palette")]
        private static readonly Color ColorNormal = new Color(0.0f, 0.90f, 1.0f, 0.85f);
        private static readonly Color ColorCue = new Color(0.25f, 1.0f, 1.0f, 0.95f);
        private static readonly Color ColorHint = new Color(0.70f, 0.28f, 1.0f, 0.85f);
        private static readonly Color ColorSlow = new Color(0.16f, 0.47f, 1.0f, 0.85f);
        private static readonly Color ColorSimplify = new Color(1.0f, 0.57f, 0.0f, 0.85f);

        [SerializeField] private Color pathColor = ColorNormal;

        // Force defaults on awake in case inspector values are bad
        private void ResetGeometryToDefaults()
        {
            chevronWidth = 0.07f;
            chevronLength = 0.09f;
            armThickness = 0.015f;
            chevronHeight = 0.002f;
            pathElevation = 0.005f;
            chevronCount = 4;
        }

        // Runtime chevrons
        private GameObject[] _chevronObjects;
        private MeshFilter[] _meshFilters;
        private MeshRenderer[] _meshRenderers;
        private Material[] _materials;
        private Mesh _sharedChevronMesh;

        private Transform _targetTransform;
        private Vector3 _targetOffset = Vector3.zero;
        private Vector3 _pathStartPoint = new Vector3(0.08f, 0.014f, -0.10f);
        private Vector3 _targetPoint = Vector3.zero;
        private Vector3 _currentDirection = Vector3.forward;

        private bool _isTrackingValid = true;
        private GuidanceVisualState _visualState = GuidanceVisualState.Normal;
        private float _prominenceScale = 1.0f;
        private float _currentWaveSpeed = 1.05f;

        // Public Accessors for Tests & System Interop
        public int ChevronCount => chevronCount;
        public GameObject[] Chevrons => _chevronObjects;
        public GuidanceVisualState CurrentState => _visualState;
        public bool IsVisible => gameObject.activeInHierarchy && _isTrackingValid && _visualState != GuidanceVisualState.Hidden;
        public Color CurrentColor => pathColor;
        public Vector3 PointingDirection => _currentDirection;
        public Vector3 TargetPosition => _targetPoint;
        public Vector3 PathStartPosition => _pathStartPoint;
        public float PathElevation => pathElevation;

        private void Awake()
        {
            ResetGeometryToDefaults();
            InitializeChevrons();
        }

        public void InitializeChevrons()
        {
            chevronCount = Mathf.Clamp(chevronCount, 3, 6);

            if (_sharedChevronMesh == null)
            {
                _sharedChevronMesh = ProceduralMeshUtility.CreateChevronMesh(chevronWidth, chevronLength, armThickness, chevronHeight);
            }

            if (_chevronObjects == null || _chevronObjects.Length != chevronCount)
            {
                _chevronObjects = new GameObject[chevronCount];
                _meshFilters = new MeshFilter[chevronCount];
                _meshRenderers = new MeshRenderer[chevronCount];
                _materials = new Material[chevronCount];
            }

            for (int i = 0; i < chevronCount; i++)
            {
                string cName = "Chevron_" + i.ToString("D2");
                Transform existing = transform.Find(cName);
                GameObject ch = existing != null ? existing.gameObject : null;
                if (ch == null)
                {
                    ch = new GameObject(cName);
                    ch.transform.SetParent(transform, false);
                }

                MeshFilter mf = ch.GetComponent<MeshFilter>();
                if (mf == null) mf = ch.AddComponent<MeshFilter>();
                mf.sharedMesh = _sharedChevronMesh;

                MeshRenderer mr = ch.GetComponent<MeshRenderer>();
                if (mr == null) mr = ch.AddComponent<MeshRenderer>();

                Material mat = (_materials != null && i < _materials.Length && _materials[i] != null)
                    ? _materials[i]
                    : ProceduralMeshUtility.CreateMaterial(pathColor, true, true);
                mr.material = mat;

                _chevronObjects[i] = ch;
                _meshFilters[i] = mf;
                _meshRenderers[i] = mr;
                _materials[i] = mat;
            }

            _currentWaveSpeed = baseWaveSpeed;
            SetVisualState(_visualState);
            UpdatePathPositions();
        }

        private void Update()
        {
            if (!_isTrackingValid || _visualState == GuidanceVisualState.Hidden)
            {
                SetChevronsActive(false);
                return;
            }

            SetChevronsActive(true);
            UpdatePathPositions();
            AnimateWave();
        }

        public void UpdatePathPositions()
        {
            if (_chevronObjects == null || _chevronObjects.Length == 0)
                return;

            if (_targetTransform != null)
            {
                _targetPoint = _targetTransform.position + _targetOffset;
            }

            float groundY = _targetPoint.y > -10f ? _targetPoint.y + pathElevation : pathElevation;

            Camera cam = Camera.main;
            #if UNITY_2022_1_OR_NEWER
            var camManager = FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARCameraManager>();
            if (camManager != null) cam = camManager.GetComponent<Camera>();
            #endif

            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - _targetPoint;
                toCam.y = 0;
                if (toCam.sqrMagnitude > 0.001f)
                {
                    _currentDirection = toCam.normalized; 
                }
                
                float pathLen = Mathf.Min(0.35f, toCam.magnitude * 0.8f);
                _pathStartPoint = _targetPoint + (_currentDirection * pathLen);
                _pathStartPoint.y = groundY;
            }
            else
            {
                _pathStartPoint = _targetPoint + new Vector3(0, 0, -0.35f);
                _pathStartPoint.y = groundY;
            }
            
            Vector3 endPoint = _targetPoint;
            endPoint.y = groundY;
            
            float endBuffer = 0.032f;
            Vector3 effectiveEnd = endPoint - (_currentDirection * endBuffer);

            for (int i = 0; i < chevronCount; i++)
            {
                if (_chevronObjects[i] == null) continue;

                float t = chevronCount > 1 ? (float)i / (chevronCount - 1) : 1f;
                float waveOffset = Mathf.Sin(t * Mathf.PI) * 0.015f; 
                Vector3 rightVec = Vector3.Cross(Vector3.up, _currentDirection);

                Vector3 pos = Vector3.Lerp(_pathStartPoint, effectiveEnd, t);
                pos += rightVec * waveOffset;

                Quaternion arrowRot = Quaternion.LookRotation(_currentDirection, Vector3.up);

                _chevronObjects[i].transform.position = pos;
                _chevronObjects[i].transform.rotation = arrowRot;
            }
        }

        public void SetTarget(Transform target, Vector3 offset, Color color)
        {
            _targetTransform = target;
            _targetOffset = offset;
            pathColor = color;
            ApplyMaterialPalette();

            if (target != null)
            {
                _targetPoint = target.position + offset;
                UpdatePathPositions();
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
                    SetChevronsActive(false);
                    return;

                case GuidanceVisualState.Normal:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.0f;
                    _currentWaveSpeed = baseWaveSpeed;
                    pathColor = ColorNormal;
                    baseEmissionIntensity = 1.6f;
                    peakEmissionIntensity = 2.6f;
                    break;

                case GuidanceVisualState.ProminentCue:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.25f;
                    _currentWaveSpeed = baseWaveSpeed * 1.5f;
                    pathColor = ColorCue;
                    baseEmissionIntensity = 2.2f;
                    peakEmissionIntensity = 3.6f;
                    break;

                case GuidanceVisualState.HintEmphasized:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 1.15f;
                    _currentWaveSpeed = baseWaveSpeed * 1.1f;
                    pathColor = ColorHint;
                    baseEmissionIntensity = 1.8f;
                    peakEmissionIntensity = 2.8f;
                    break;

                case GuidanceVisualState.PacedSlow:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 0.95f;
                    _currentWaveSpeed = baseWaveSpeed * 0.6f;
                    pathColor = ColorSlow;
                    baseEmissionIntensity = 1.2f;
                    peakEmissionIntensity = 2.0f;
                    break;

                case GuidanceVisualState.IsolatedStep:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    _prominenceScale = 0.9f;
                    _currentWaveSpeed = baseWaveSpeed * 0.8f;
                    pathColor = ColorSimplify;
                    baseEmissionIntensity = 1.4f;
                    peakEmissionIntensity = 2.4f;
                    break;
            }

            ApplyMaterialPalette();
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
                SetChevronsActive(true);
                UpdatePathPositions();
            }
        }

        private void SetChevronsActive(bool active)
        {
            if (_chevronObjects == null) return;
            for (int i = 0; i < _chevronObjects.Length; i++)
            {
                if (_chevronObjects[i] != null)
                {
                    _chevronObjects[i].SetActive(active);
                }
            }
        }
        
        private void AnimateWave()
        {
            if (_chevronObjects == null || _materials == null) return;
            
            float time = Time.time * _currentWaveSpeed * 3f;
            for (int i = 0; i < chevronCount; i++)
            {
                if (_materials[i] == null || _chevronObjects[i] == null) continue;
                
                float phase = ((float)i / chevronCount) * Mathf.PI * 2f;
                float sine = Mathf.Sin(time - phase);
                float normalizedPulse = (sine + 1f) * 0.5f;
                
                float currentEmission = Mathf.Lerp(baseEmissionIntensity, peakEmissionIntensity, normalizedPulse);
                ProceduralMeshUtility.SetMaterialColor(_materials[i], pathColor, currentEmission);
                
                float scale = 1.0f + (normalizedPulse * pulseScaleAmount);
                _chevronObjects[i].transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        private void ApplyMaterialPalette()
        {
            if (_materials == null) return;
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] != null)
                {
                    ProceduralMeshUtility.SetMaterialColor(_materials[i], pathColor, baseEmissionIntensity);
                }
            }
        }

        private void OnDestroy()
        {
            if (_materials != null)
            {
                for (int i = 0; i < _materials.Length; i++)
                {
                    if (_materials[i] != null)
                    {
                        Destroy(_materials[i]);
                        _materials[i] = null;
                    }
                }
            }
        }
    }
}
