using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using A3R.ARGuidance;

namespace A3R.ARTracking
{
    public enum PlacementState
    {
        SEARCHING,

        READY,
        PLACING,
        PLACED,
        FOUR_CORNER_TL,
        FOUR_CORNER_TR,
        FOUR_CORNER_BR,
        FOUR_CORNER_BL,
        FOUR_CORNER_REVIEW
    }

    /// <summary>
    /// A3R_MarkerlessFoundationController — Phase 1–4 authoritative controller.
    ///
    /// THREE MODES OF OPERATION:
    /// 1. LIVE CANDIDATE MODE (SEARCHING):
    ///    - Preview follows AR plane raycast.
    ///    - RaycastDrivingPreview = true.
    ///
    /// 2. CALIBRATION ANCHORED MODE (CALIBRATING / READY):
    ///    - When position is locked, a provider-managed ARAnchor is created at the candidate pose.
    ///    - A3R_PlacementPreviewRoot is reparented directly under this anchor at local (0,0,0) and identity rotation.
    ///    - ZERO live raycast or camera writes to preview world transform.
    ///    - RaycastDrivingPreview = false.
    ///    - All adjustments (X/Z/Rot) occur strictly on child PreviewCalibratedFrame (local).
    ///    - Width/Depth regenerate geometry locally under PreviewCalibratedFrame.
    ///
    /// 3. PLACED MODE (PLACED):
    ///    - Provider-managed anchor with BreadboardARRoot hierarchy.
    ///    - Preview hidden, plane detection disabled.
    /// </summary>
    [RequireComponent(typeof(Unity.XR.CoreUtils.XROrigin))]
    public class A3R_MarkerlessFoundationController : MonoBehaviour
    {
        // ── Subsystem Managers ───────────────────────────────────────────────
        private ARPlaneManager _planeManager;
        private ARRaycastManager _raycastManager;
        private ARAnchorManager _anchorManager;
        private Camera _arCamera;

        // ── Placement State Machine ──────────────────────────────────────────
        public PlacementState CurrentState { get; private set; } = PlacementState.SEARCHING;

        // ── Plane Tracking Diagnostics ───────────────────────────────────────
        public bool PlaneManagerExists => _planeManager != null;
        public bool PlaneManagerEnabled => _planeManager != null && _planeManager.enabled;
        public int PlaneCount => _planeManager != null ? _planeManager.trackables.count : 0;
        public int TrackingPlaneCount { get; private set; }
        public string BestPlaneState { get; private set; } = "N/A";

        // ── Raycast Diagnostics ──────────────────────────────────────────────
        public bool RaycastManagerExists => _raycastManager != null;
        public int RaycastHitCount { get; private set; }
        public Pose RawRaycastPose { get; private set; }
        public Vector3 LastRaycastHitPos { get; private set; }
        public string LastRaycastHitType { get; private set; } = "NONE";

        // ── Placement & Root Properties ──────────────────────────────────────
        public string SurfaceType => (CurrentState == PlacementState.READY || CurrentState == PlacementState.PLACED) ? "HORIZONTAL" : "NONE";
        public bool IsPreviewVisible => _previewFootprint != null && _previewFootprint.activeSelf;
        public bool HasValidPose => _hasValidPose;
        public bool HasPlacedRoot => _breadboardARRoot != null;
        public bool IsRootLocked => _isRootLocked;
        public bool HasAnchor => MainAnchor != null;
        public Vector3 RootPosition => _breadboardARRoot != null ? _breadboardARRoot.transform.position : Vector3.zero;
        public Vector3 RootRotation => _breadboardARRoot != null ? _breadboardARRoot.transform.eulerAngles : Vector3.zero;
        public Transform BreadboardRootTransform => _breadboardARRoot != null ? _breadboardARRoot.transform : null;

        // ── Anchor & Breadboard Root Diagnostics Properties ──────────────────
        public ARAnchor MainAnchor { get; private set; }
        public bool AnchorComponentExists => (MainAnchor != null || _previewAnchor != null);
        public bool AnchorProviderCreated { get; private set; }
        public ARAnchor ActiveAnchor => MainAnchor != null ? MainAnchor : _previewAnchor;
        public TrackableId AnchorTrackableId => ActiveAnchor != null ? ActiveAnchor.trackableId : TrackableId.invalidId;
        public TrackingState AnchorTrackingState => ActiveAnchor != null ? ActiveAnchor.trackingState : TrackingState.None;
        public bool AnchorPending => ActiveAnchor != null && ActiveAnchor.pending;
        public Vector3 AnchorWorldPos => ActiveAnchor != null ? ActiveAnchor.transform.position : Vector3.zero;
        public Vector3 AnchorWorldRot => ActiveAnchor != null ? ActiveAnchor.transform.eulerAngles : Vector3.zero;

        public string BreadboardRootParentName => (_breadboardARRoot != null && _breadboardARRoot.transform.parent != null) ? _breadboardARRoot.transform.parent.name : "NONE";
        public Vector3 BreadboardRootLocalPos => _breadboardARRoot != null ? _breadboardARRoot.transform.localPosition : Vector3.zero;
        public Vector3 BreadboardRootLocalRot => _breadboardARRoot != null ? _breadboardARRoot.transform.localEulerAngles : Vector3.zero;
        public Vector3 BreadboardRootLocalScale => _breadboardARRoot != null ? _breadboardARRoot.transform.localScale : Vector3.one;

        // ── Preview Transform Diagnostics ────────────────────────────────────
        public Transform PreviewTransform => _previewTransform;
        public Vector3 PreviewWorldScale => _previewTransform != null ? _previewTransform.lossyScale : Vector3.zero;
        public Vector3 PreviewWorldPosition => _previewTransform != null ? _previewTransform.position : Vector3.zero;
        public Vector3 PreviewWorldRotation => _previewTransform != null ? _previewTransform.eulerAngles : Vector3.zero;
        public Vector3 PreviewLocalPosition => _previewTransform != null ? _previewTransform.localPosition : Vector3.zero;
        public string PreviewParentName => (_previewTransform != null && _previewTransform.parent != null) ? _previewTransform.parent.name : "NONE";
        public bool RaycastDrivingPreview => CurrentState == PlacementState.SEARCHING && _hasValidPose;

        // Diagnostic exports for temporary registration tracking reference
        public bool HasRegistrationAnchor => _registrationAnchor != null;
        public string RegistrationAnchorType => _registrationAnchor != null ? _registrationAnchor.GetType().Name : "NONE";
        public string RegistrationAnchorTracking => _registrationAnchor != null ? _registrationAnchor.trackingState.ToString() : "NONE";
        public string CornerMarkerParentName => (_cornerMarkers != null && _cornerMarkers.Length > 0 && _cornerMarkers[0] != null && _cornerMarkers[0].transform.parent != null) ? _cornerMarkers[0].transform.parent.name : "NONE";
        public int CapturedCornerCount 
        {
            get 
            {
                int count = 0;
                for (int i = 0; i < _cornerMarkers.Length; i++) { if (_cornerMarkers[i] != null) count++; }
                return count;
            }
        }
        public bool HasMainAnchor => MainAnchor != null;
        public string MainAnchorTracking => MainAnchor != null ? MainAnchor.trackingState.ToString() : "NONE";

        public bool PreviewRendererEnabled
        {
            get
            {
                if (_previewRenderer != null) return _previewRenderer.enabled;
                if (_previewFootprint != null)
                {
                    var mr = _previewFootprint.GetComponentInChildren<MeshRenderer>();
                    return mr != null && mr.enabled;
                }
                return false;
            }
        }
        public float HeadingAngle => _headingOffsetAngle;
        public int PreviewVertexCount
        {
            get
            {
                if (_previewVisualGo != null)
                {
                    var mf = _previewVisualGo.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.vertexCount;
                }
                return 0;
            }
        }
        public int PreviewTriangleCount
        {
            get
            {
                if (_previewVisualGo != null)
                {
                    var mf = _previewVisualGo.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.triangles.Length / 3;
                }
                return 0;
            }
        }
        public bool HasPreviewMeshFilter => _previewVisualGo != null && _previewVisualGo.GetComponentInChildren<MeshFilter>() != null;
        public bool HasPreviewMeshRenderer => _previewVisualGo != null && _previewVisualGo.GetComponentInChildren<MeshRenderer>() != null;

        public Vector3 PreviewMeshBoundsSize
        {
            get
            {
                if (_previewVisualGo != null)
                {
                    var mf = _previewVisualGo.GetComponentInChildren<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds.size;
                }
                return Vector3.zero;
            }
        }

        // ── ONE AUTHORITATIVE VISUAL — Diagnostics ───────────────────────────
        public string AuthoritativeRootName => _previewFootprint != null ? _previewFootprint.name : "NULL";
        public string CyanOverlayParentName
        {
            get
            {
                if (_cyanOutlineGo != null && _cyanOutlineGo.transform.parent != null)
                    return _cyanOutlineGo.transform.parent.name;
                return "NULL";
            }
        }
        public Vector3 CyanOverlayLocalPos => _cyanOutlineGo != null ? _cyanOutlineGo.transform.localPosition : Vector3.zero;
        public Vector3 CyanOverlayLocalRot => _cyanOutlineGo != null ? _cyanOutlineGo.transform.localEulerAngles : Vector3.zero;
        public Vector3 CyanOverlayLocalScale => _cyanOutlineGo != null ? _cyanOutlineGo.transform.localScale : Vector3.one;
        public int CyanOverlayInstanceId => _cyanOutlineGo != null ? _cyanOutlineGo.GetInstanceID() : 0;
        public int PreviewVisualInstanceId => _previewVisualGo != null ? _previewVisualGo.GetInstanceID() : 0;
        public string PreviewVisualParentName
        {
            get
            {
                if (_previewVisualGo != null && _previewVisualGo.transform.parent != null)
                    return _previewVisualGo.transform.parent.name;
                return "NULL";
            }
        }

        // SAME ROOT is true when the cyan outline parent chain leads to _previewFootprint
        public bool SameRoot
        {
            get
            {
                if (_cyanOutlineGo == null || _previewFootprint == null) return false;
                Transform t = _cyanOutlineGo.transform.parent;
                while (t != null)
                {
                    if (t.gameObject == _previewFootprint) return true;
                    t = t.parent;
                }
                return false;
            }
        }

        // ── Calibration Stability & State Diagnostics ────────────────────────
        public string CalibrationStateString
        {
            get
            {
                switch (CurrentState)
                {
                    case PlacementState.SEARCHING:
                        return "LIVE";
                    case PlacementState.READY:
                    case PlacementState.PLACING:
                        return (_previewAnchor != null || MainAnchor != null) ? "ANCHORED" : "FROZEN";
                    case PlacementState.PLACED:
                        return "PLACED";
                    default:
                        return "LIVE";
                }
            }
        }

        // ── Phase 2 Frozen Placement Data & Diagnostics ──────────────────────
        private Pose _candidatePose;
        private TrackableId _candidateTrackableId = TrackableId.invalidId;
        private Pose _readyPlacementPose;
        private TrackableId _readyTrackableId = TrackableId.invalidId;
        private Quaternion _baseFrozenRotation = Quaternion.identity;
        private Transform _previewCalibratedFrame;
        private bool _isCoarseStep = false;
        private GameObject _previewVisualGo;   // child of _previewCalibratedFrame
        private GameObject _cyanOutlineGo;     // child of _previewVisualGo
        private ARAnchor _previewAnchor;   // provider-managed anchor for calibration preview

        // Phase 2 temporary stabilization anchor
        private ARAnchor _registrationAnchor;
        private bool _isCreatingRegistrationAnchor = false;

        // Corner marker visual configuration: 8mm diameter provides clear AR visibility at 0.5-0.8m
        // while safely staying outside all breadboard holes (nearest tie-point hole rim is 4.0mm from corner).
        private const float CORNER_MARKER_DIAMETER = 0.008f;
        private static readonly Vector3 CORNER_MARKER_SCALE = new Vector3(CORNER_MARKER_DIAMETER, CORNER_MARKER_DIAMETER, CORNER_MARKER_DIAMETER);
        private Material _sharedCornerMaterial;

        private Material GetOrCreateCornerMaterial()
        {
            if (_sharedCornerMaterial == null)
            {
                _sharedCornerMaterial = ProceduralMeshUtility.CreateMaterial(Color.yellow, false, false);
            }
            return _sharedCornerMaterial;
        }

        private Vector3[] _cornerPoints = new Vector3[4];
        private GameObject[] _cornerMarkers = new GameObject[4];
        private Pose _fourCornerComputedPose;
        private Vector3 _fourCornerReviewPosition;
        private Quaternion _fourCornerReviewRotation;
        public Vector3[] CornerPoints => _cornerPoints;
        public Vector3[] ActiveCornerPoints 
        {
            get
            {
                Vector3[] points = new Vector3[4];
                points[0] = _cornerMarkers[0] != null ? _cornerMarkers[0].transform.position : _cornerPoints[0];
                points[1] = _cornerMarkers[1] != null ? _cornerMarkers[1].transform.position : _cornerPoints[1];
                points[2] = _cornerMarkers[2] != null ? _cornerMarkers[2].transform.position : _cornerPoints[2];
                points[3] = _cornerMarkers[3] != null ? _cornerMarkers[3].transform.position : _cornerPoints[3];
                return points;
            }
        }

        /// <summary>
        /// Destroys the existing PreviewVisual child and recreates it under _previewCalibratedFrame
        /// with the fixed 82x54mm calibration dimensions.
        /// ONLY the visual geometry changes — no world transforms are moved.
        /// </summary>
        private void RebuildPreviewVisual()
        {
            if (_previewCalibratedFrame == null) return;

            if (_previewVisualGo != null)
            {
                Destroy(_previewVisualGo);
                _previewVisualGo = null;
                _cyanOutlineGo = null;
                _previewRenderer = null;
            }

            BuildBreadboardVisual("PreviewVisual", _previewCalibratedFrame, true, A3R_BreadboardConfig.WIDTH, A3R_BreadboardConfig.DEPTH);
        }

        public string PlacementPoseLockStatus
        {
            get
            {
                switch (CurrentState)
                {
                    case PlacementState.SEARCHING: return "LIVE CANDIDATE";
                    case PlacementState.READY: return (_previewAnchor != null) ? "ANCHORED (READY)" : "POSITION LOCKED";
                    case PlacementState.PLACING: return "PLACING";
                    case PlacementState.PLACED: return "PLACED";
                    default: return "LIVE CANDIDATE";
                }
            }
        }
        public TrackableId CurrentTrackableId => (CurrentState == PlacementState.READY || CurrentState == PlacementState.PLACED) ? _readyTrackableId : _candidateTrackableId;
        public Vector3 CandidatePosition => _candidatePose.position;
        public Vector3 ReadyPosition => (CurrentState == PlacementState.READY || CurrentState == PlacementState.PLACED) ? _readyPlacementPose.position : Vector3.zero;
        public bool HasReadyPose => CurrentState == PlacementState.READY || CurrentState == PlacementState.PLACED;
        public Vector3 FourCornerReviewPosition => _fourCornerReviewPosition;
        public Vector3 FourCornerReviewRotation => _fourCornerReviewRotation.eulerAngles;

        // Phase 3 Local Coordinates & Hierarchy References
        public Vector3 Esp32LocalPos { get; private set; } = new Vector3(0.015f, A3R_BreadboardConfig.HEIGHT, 0f);
        public Vector3 LedLocalPos { get; private set; } = new Vector3(-0.020f, A3R_BreadboardConfig.HEIGHT, 0.010f);
        public Vector3 ResistorLocalPos { get; private set; } = A3R_BreadboardConfig.DEFAULT_RESISTOR_LOCAL_POS;
        public Transform TutorialVisualsTransform { get; private set; }
        public Transform BreadboardBoundsTransform { get; private set; }
        public Transform HoleGridTransform { get; private set; }
        public Transform WireAnchorsTransform { get; private set; }

        // ── Events ──────────────────────────────────────────────────────────────────────────
        public event Action<Transform> OnBreadboardPlaced;

        // ── Internal Placement Data ──────────────────────────────────────────
        private Pose _latestValidPose;
        private bool _hasValidPose = false;
        private GameObject _previewFootprint;   // THE AUTHORITATIVE PREVIEW ROOT
        private Transform _previewTransform;    // = _previewFootprint.transform
        private MeshRenderer _previewRenderer;  // outline renderer for diagnostics
        private GameObject _breadboardARRoot;
        private bool _isRootLocked = false;
        private float _headingOffsetAngle = 0f;
        private Quaternion _baseHeadingRot = Quaternion.identity;

        private float _raycastTimer = 0f;
        private const float RAYCAST_INTERVAL = 0.05f;
        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        // Cached GUIStyles
        private GUIStyle _btnStyle;
        private GUIStyle _rotBtnStyle;
        private bool _stylesInitialized = false;

        // ── Unity Lifecycle ──────────────────────────────────────────────────
        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;



            // Deterministically validate the CAD-referenced breadboard map
            A3R_BreadboardMap.Validate();

            var arSession = UnityEngine.Object.FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARSession>();
            if (arSession != null)
            {
                arSession.matchFrameRateRequested = false;
            }

            _arCamera = Camera.main;

            _planeManager = GetComponent<ARPlaneManager>();
            if (_planeManager == null)
            {
                _planeManager = gameObject.AddComponent<ARPlaneManager>();
            }

            _raycastManager = GetComponent<ARRaycastManager>();
            if (_raycastManager == null)
            {
                _raycastManager = gameObject.AddComponent<ARRaycastManager>();
            }

            _anchorManager = GetComponent<ARAnchorManager>();
            if (_anchorManager == null)
            {
                _anchorManager = gameObject.AddComponent<ARAnchorManager>();
            }
            // Prevent ARFoundation from spawning stale anchor prefabs
            _anchorManager.anchorPrefab = null;

            if (GetComponent<A3R.ARGuidance.A3R_ResistorTutorialController>() == null)
            {
                gameObject.AddComponent<A3R.ARGuidance.A3R_ResistorTutorialController>();
            }

            _planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            _planeManager.enabled = true;

            // Create the ONE authoritative preview root
            CreatePreviewFootprint();

            Debug.Log("[A3R_PHASE2] Initialized markerless placement foundation in SEARCHING state.");
        }

        private void OnEnable()
        {
            if (_planeManager != null)
                _planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        private void OnDisable()
        {
            if (_planeManager != null)
                _planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        private void OnDestroy()
        {
            if (_sharedCornerMaterial != null)
            {
                Destroy(_sharedCornerMaterial);
                _sharedCornerMaterial = null;
            }
        }

        private void HandleFourCornerInput()
        {
            if (CurrentState < PlacementState.FOUR_CORNER_TL || CurrentState > PlacementState.FOUR_CORNER_BL) return;

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                Vector2 touchPos = Input.GetTouch(0).position;
                
                // 1. Find the AR table plane
                if (_planeManager == null) return;
                ARPlane bestPlane = _planeManager.GetPlane(_candidateTrackableId);

                if (bestPlane == null) return;

                // 2. Construct mathematical plane offset 9mm above the table
                Vector3 tableNormal = bestPlane.normal;
                Vector3 tablePoint = bestPlane.center;
                Vector3 boardTopPoint = tablePoint + (tableNormal * 0.009f);
                Plane boardTopPlane = new Plane(tableNormal, boardTopPoint);

                // 3. Intersect screen tap
                Ray ray = _arCamera.ScreenPointToRay(touchPos);
                if (boardTopPlane.Raycast(ray, out float enterDist))
                {
                    Vector3 cornerWorldPos = ray.GetPoint(enterDist);
                    int cornerIdx = (int)CurrentState - (int)PlacementState.FOUR_CORNER_TL;
                    _cornerPoints[cornerIdx] = cornerWorldPos;

                    // Place a visual marker
                    if (_cornerMarkers[cornerIdx] != null) Destroy(_cornerMarkers[cornerIdx]);
                    _cornerMarkers[cornerIdx] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    
                    // Set absolute world position first
                    _cornerMarkers[cornerIdx].transform.position = cornerWorldPos;
                    _cornerMarkers[cornerIdx].transform.localScale = CORNER_MARKER_SCALE;
                    Destroy(_cornerMarkers[cornerIdx].GetComponent<Collider>());
                    var mr = _cornerMarkers[cornerIdx].GetComponent<MeshRenderer>();
                    mr.sharedMaterial = GetOrCreateCornerMaterial();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                    // Parent to the tracked reference, preserving the accurate world position
                    if (_registrationAnchor != null)
                    {
                        _cornerMarkers[cornerIdx].transform.SetParent(_registrationAnchor.transform, true);
                    }

                    // Advance state
                    if (CurrentState == PlacementState.FOUR_CORNER_BL)
                    {
                        ComputeFourCornerPose();
                    }
                    else
                    {
                        CurrentState++;
                    }
                }
            }
        }

        private void ComputeFourCornerPose()
        {
            // FIX: The static _cornerPoints were captured in raw world space. As the AR SLAM map 
            // drifted over several seconds, those fixed Vector3 values ceased to align with the physical board.
            // Because the _cornerMarkers are now physically stabilized by an ARAnchor, we must read 
            // their active world positions here to get the true, physically-anchored locations.
            Vector3 p1 = _cornerMarkers[0] != null ? _cornerMarkers[0].transform.position : _cornerPoints[0]; // TL
            Vector3 p2 = _cornerMarkers[1] != null ? _cornerMarkers[1].transform.position : _cornerPoints[1]; // TR
            Vector3 p3 = _cornerMarkers[2] != null ? _cornerMarkers[2].transform.position : _cornerPoints[2]; // BR
            Vector3 p4 = _cornerMarkers[3] != null ? _cornerMarkers[3].transform.position : _cornerPoints[3]; // BL

            // Validate dimensions
            float width = Vector3.Distance(p1, p2);
            float depth = Vector3.Distance(p1, p4);

            if (width < 0.05f || width > 0.12f || depth < 0.03f || depth > 0.08f)
            {
                Debug.LogWarning($"[A3R] 4-Corner Validation Failed. Width={width}, Depth={depth}");
                ResetFourCorner();
                return;
            }

            // Math
            Vector3 center = (p1 + p2 + p3 + p4) / 4f;

            // FIX 2a: Average the two right-axis vectors (both point TL→TR and BL→BR = rightward)
            // Then EXPLICITLY re-normalize before Cross product.
            // Previously the averaged result was used without re-normalizing, producing a
            // degenerate or low-magnitude vector that corrupted LookRotation.
            Vector3 rightEdge1 = (p2 - p1).normalized; // Top: TL → TR
            Vector3 rightEdge2 = (p3 - p4).normalized; // Bottom: BL → BR
            Vector3 approxRight = (rightEdge1 + rightEdge2);
            if (approxRight.sqrMagnitude < 0.001f)
                approxRight = rightEdge1; // fallback to single edge if parallel but opposite (degenerate tap)
            approxRight = approxRight.normalized; // ← THE KEY FIX: always unit vector

            // FIX 2b: Force up = Vector3.up (world Y-axis).
            // Previously: up = plane.normal from ARCore, which can be tilted 1–5° by SLAM estimation.
            // Using a tilted up in LookRotation tilts the entire board frame off the physical surface.
            // The board is always on a flat table — world Y is the correct up for a tabletop board.
            Vector3 up = Vector3.up;

            Vector3 strictForward = Vector3.Cross(approxRight, up).normalized;
            Quaternion rotation = Quaternion.LookRotation(strictForward, up);
            
            // FIX: The 4 corners were raycasted onto the TOP surface (Y=9mm).
            // However, the rest of the application (A3R_BreadboardMap) assumes BreadboardARRoot 
            // is placed on the TABLE surface (Y=0mm) and explicitly adds 9mm to all Y coordinates.
            // We must lower the anchor center by 9mm to the table to prevent everything from 
            // floating at 18mm, which causes the observed ~2 row/column parallax visual offset.
            Vector3 tableCenter = center - up * A3R_BreadboardConfig.HEIGHT;
            
            _fourCornerReviewPosition = tableCenter;
            _fourCornerReviewRotation = rotation;
            
            _fourCornerComputedPose = new Pose(tableCenter, rotation);
            
            // Re-use _readyPlacementPose for anchoring later
            _readyPlacementPose = _fourCornerComputedPose;
            _readyTrackableId = _candidateTrackableId; // FIX: Attach to the physical table plane to prevent drift

            CurrentState = PlacementState.FOUR_CORNER_REVIEW;

            RebuildPreviewVisual();
        }

        public void ResetFourCorner()
        {
            foreach (var m in _cornerMarkers) if (m != null) Destroy(m);
            CurrentState = PlacementState.FOUR_CORNER_TL;
            SetPreviewActive(false);
            
            // Clean up temporary anchor if user resets
            if (_registrationAnchor != null)
            {
                if (_previewFootprint != null) _previewFootprint.transform.SetParent(null, true);
                Destroy(_registrationAnchor.gameObject);
                _registrationAnchor = null;
            }
            
            // In SEARCHING state, we need a new anchor for the new session
            CurrentState = PlacementState.SEARCHING;
        }

        public void RestartAR()
        {
            // Clean up corner markers
            foreach (var m in _cornerMarkers) if (m != null) Destroy(m);
            
            // Preserve the preview footprint geometry by detaching it safely
            if (_previewFootprint != null) _previewFootprint.transform.SetParent(null, true);
            
            // Destroy all anchors
            if (_registrationAnchor != null) { Destroy(_registrationAnchor.gameObject); _registrationAnchor = null; }
            if (_previewAnchor != null) { Destroy(_previewAnchor.gameObject); _previewAnchor = null; }
            if (MainAnchor != null) { Destroy(MainAnchor.gameObject); MainAnchor = null; }
            if (_breadboardARRoot != null) { Destroy(_breadboardARRoot.gameObject); _breadboardARRoot = null; }
            
            _isCreatingRegistrationAnchor = false;
            _hasValidPose = false;
            _isRootLocked = false;
            
            SetPreviewActive(false);

            // Re-enable plane detection
            if (_planeManager != null)
            {
                _planeManager.enabled = true;
                foreach (var plane in _planeManager.trackables)
                {
                    plane.gameObject.SetActive(true);
                }
            }

            // Genuinely clear the initial flat surface tracking by resetting the ARSession
            var arSession = UnityEngine.Object.FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARSession>();
            if (arSession != null)
            {
                arSession.Reset();
            }

            CurrentState = PlacementState.SEARCHING;
            Debug.Log("[A3R] AR Restarted. Returned to SEARCHING state with fresh surface tracking.");
        }

        public void LockFourCornerRegistration()
        {
            CurrentState = PlacementState.READY;
            foreach (var m in _cornerMarkers) if (m != null) Destroy(m);
            
            if (_previewAnchor == null)
            {
                _ = CreatePreviewAnchorAsync(_readyPlacementPose, _readyTrackableId);
            }
        }

        private void Update()
        {
            if (_arCamera == null) _arCamera = Camera.main;

            if (CurrentState == PlacementState.PLACED)
            {
                // ARCore fully owns the anchor transform. No application writes here.
                return;
            }

            HandleFourCornerInput();

            // FIX 1: Only raycast during SEARCHING — not during corner-tapping or review.
            // Previously, raycasts fired during FOUR_CORNER_* states, flooding diagnostics
            // with live _candidatePose updates and giving the false appearance of jitter.
            if (CurrentState == PlacementState.SEARCHING)
            {
                if (_raycastManager != null && _arCamera != null)
                {
                    _raycastTimer += Time.deltaTime;
                    if (_raycastTimer >= RAYCAST_INTERVAL)
                    {
                        _raycastTimer = 0f;
                        PerformPlacementRaycast();
                    }
                }
            }

            if (CurrentState == PlacementState.SEARCHING)
            {
                // NO automatic breadboard preview at startup
                SetPreviewActive(false);

                if (_hasValidPose)
                {
                    if (_registrationAnchor == null && !_isCreatingRegistrationAnchor)
                    {
                        _isCreatingRegistrationAnchor = true;
                        _ = CreateRegistrationAnchorAsync(_candidatePose);
                    }
                    
                    if (_registrationAnchor != null)
                    {
                        if (_previewFootprint != null)
                        {
                            _previewFootprint.transform.SetParent(_registrationAnchor.transform, true);
                        }
                        CurrentState = PlacementState.FOUR_CORNER_TL;
                    }
                }
            }
            else if (CurrentState == PlacementState.READY || CurrentState == PlacementState.FOUR_CORNER_REVIEW)
            {
                // Ensure preview stays active and locked to the computed four-corner pose
                SetPreviewActive(true);

                if (_previewTransform != null)
                {
                    _previewTransform.position = _fourCornerReviewPosition;
                    _previewTransform.rotation = _fourCornerReviewRotation;
                }
            }
        }

        private void SetPreviewActive(bool active)
        {
            if (_previewFootprint != null && _previewFootprint.activeSelf != active)
            {
                _previewFootprint.SetActive(active);
            }
        }

        // ── Surface Raycasting & Preview ─────────────────────────────────────
        private void PerformPlacementRaycast()
        {
            if (CurrentState == PlacementState.PLACED || CurrentState == PlacementState.PLACING) return;

            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            bool hitFound = TryRaycastHorizontalSurface(screenCenter, out Pose hitPose, out TrackableId hitTrackable);

            if (!hitFound)
            {
                Vector2 secondaryProbe = new Vector2(Screen.width / 2f, Screen.height * 0.44f);
                hitFound = TryRaycastHorizontalSurface(secondaryProbe, out hitPose, out hitTrackable);
            }

            if (!hitFound)
            {
                Vector2 tertiaryProbe =
                    new Vector2(Screen.width / 2f, Screen.height * 0.32f);

                hitFound = TryRaycastHorizontalSurface(
                    tertiaryProbe, out hitPose, out hitTrackable);
            }

            if (hitFound)
            {
                Vector3 planeNormal = hitPose.up;

                if (!_hasValidPose)
                {
                    Vector3 camFwdOnPlane = Vector3.ProjectOnPlane(_arCamera.transform.forward, planeNormal).normalized;
                    if (camFwdOnPlane.sqrMagnitude > 0.001f)
                    {
                        _baseHeadingRot = Quaternion.LookRotation(camFwdOnPlane, planeNormal);
                    }
                    else
                    {
                        _baseHeadingRot = Quaternion.FromToRotation(Vector3.up, planeNormal);
                    }
                }

                Quaternion orientation = _baseHeadingRot * Quaternion.Euler(0f, _headingOffsetAngle, 0f);

                _candidatePose = new Pose(hitPose.position, orientation);
                _candidateTrackableId = hitTrackable;
                _latestValidPose = _candidatePose;
                _hasValidPose = true;

                // Preview update moved to Update()
            }
            else
            {
                _hasValidPose = false;
            }
        }

        private bool TryRaycastHorizontalSurface(Vector2 screenPoint, out Pose hitPose, out TrackableId trackableId)
        {
            hitPose = Pose.identity;
            trackableId = TrackableId.invalidId;

            TrackableType types = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds;
            if (_raycastManager.Raycast(screenPoint, s_Hits, types) && s_Hits.Count > 0)
            {
                var hit = s_Hits[0];
                RaycastHitCount = s_Hits.Count;
                LastRaycastHitPos = hit.pose.position;
                LastRaycastHitType = hit.hitType.ToString();
                RawRaycastPose = hit.pose;

                bool isHorizontal = Vector3.Dot(hit.pose.up, Vector3.up) > 0.65f;
                if (isHorizontal)
                {
                    hitPose = hit.pose;
                    trackableId = hit.trackableId;
                    return true;
                }
            }
            return false;
        }

        // ── ONE AUTHORITATIVE PREVIEW FOOTPRINT ──────────────────────────────
        private void CreatePreviewFootprint()
        {
            if (_previewFootprint != null) return;

            // Root — world-space while SEARCHING, child of ARAnchor once locked
            _previewFootprint = new GameObject("A3R_PlacementPreviewRoot");
            _previewFootprint.SetActive(false);
            _previewTransform = _previewFootprint.transform;
            _previewTransform.localScale = Vector3.one;

            // Calibrated frame — local X/Z offset and Y-rotation calibration
            var calibFrameGo = new GameObject("PreviewCalibratedFrame");
            calibFrameGo.transform.SetParent(_previewTransform, false);
            calibFrameGo.transform.localPosition = Vector3.zero;
            calibFrameGo.transform.localRotation = Quaternion.identity;
            calibFrameGo.transform.localScale = Vector3.one;
            _previewCalibratedFrame = calibFrameGo.transform;

            // Build ONE visual under the calibrated frame
            RebuildPreviewVisual();
        }

        private async Task CreateRegistrationAnchorAsync(Pose pose)
        {
            if (_anchorManager == null) 
            {
                _isCreatingRegistrationAnchor = false;
                return;
            }

            try
            {
                var result = await _anchorManager.TryAddAnchorAsync(pose);
                if (result.status.IsSuccess() && result.value != null)
                {
                    _registrationAnchor = result.value;
                    Debug.Log($"[A3R_PHASE2] Registration Anchor created: {_registrationAnchor.trackableId}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[A3R_PHASE2] Registration Anchor exception: {ex.Message}");
            }
            finally
            {
                _isCreatingRegistrationAnchor = false;
            }
        }

        // ── Provider-Managed Anchor Creation Helper ──────────────────────────
        /// <summary>
        /// Creates a provider-managed ARAnchor using AR Foundation 6.3.5 API.
        /// When successfully created, reparents A3R_PlacementPreviewRoot as a direct child
        /// with localPosition = (0,0,0) and localRotation = identity.
        /// </summary>
        private async Task<bool> CreatePreviewAnchorAsync(Pose pose, TrackableId planeTrackableId)
        {
            if (_anchorManager == null) return false;

            ARAnchor anchor = null;
            bool providerCreated = false;

            // 1. Primary: AttachAnchor (AR Foundation plane-attached API) or TryAddAnchorAsync fallback
            try
            {
                ARPlane plane = null;
                if (_planeManager != null && planeTrackableId != TrackableId.invalidId)
                {
                    plane = _planeManager.GetPlane(planeTrackableId);
                }

                if (plane != null)
                {
                    anchor = _anchorManager.AttachAnchor(plane, pose);
                    if (anchor != null)
                    {
                        providerCreated = true;
                        Debug.Log($"[A3R_ANCHOR] AttachAnchor succeeded: trackableId={anchor.trackableId}, trackingState={anchor.trackingState}");
                    }
                    else
                    {
                        Debug.LogWarning("[A3R_ANCHOR] AttachAnchor returned null.");
                    }
                }
                else
                {
                    var result = await _anchorManager.TryAddAnchorAsync(pose);
                    if (result.status.IsSuccess() && result.value != null)
                    {
                        anchor = result.value;
                        providerCreated = true;
                        Debug.Log($"[A3R_ANCHOR] TryAddAnchorAsync succeeded: trackableId={anchor.trackableId}, trackingState={anchor.trackingState}");
                    }
                    else
                    {
                        Debug.LogWarning($"[A3R_ANCHOR] TryAddAnchorAsync returned non-success: {result.status.statusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[A3R_ANCHOR] Anchor creation exception: {ex.Message}");
            }

            // FIX 3: Do NOT fall back to an orphan AddComponent<ARAnchor>().
            // An ARAnchor created this way is NOT registered with ARAnchorManager.
            // ARCore never knows it exists → trackingState = None forever → no SLAM stabilization.
            // The board appears stable initially but drifts as the SLAM map corrects.
            // Instead: if TryAddAnchorAsync failed, we surface the failure and abort.
            // The user must retry (move phone to improve plane tracking, then re-register).
            if (anchor == null)
            {
                Debug.LogError("[A3R_ANCHOR] TryAddAnchorAsync failed. Anchor NOT created. " +
                               "Please ensure planes are well-tracked and retry registration.");
                return false;
            }

            _previewAnchor = anchor;
            AnchorProviderCreated = providerCreated;

            // Reparent preview root under provider anchor.
            // Local transform is strictly zero/identity so preview is 100% coincident with anchor.
            if (_previewFootprint != null)
            {
                _previewFootprint.transform.SetParent(_previewAnchor.transform, false);
                _previewFootprint.transform.localPosition = Vector3.zero;
                _previewFootprint.transform.localRotation = Quaternion.identity;
                _previewFootprint.transform.localScale = Vector3.one;
            }

            Debug.Log($"[A3R_ANCHOR] Preview parented to provider anchor: trackableId={anchor.trackableId}, trackingState={anchor.trackingState}. World pos={anchor.transform.position:F3}");
            return true;
        }

        // ── User Confirmation & Final Root Creation ──────────────────────────
        public async void ConfirmPlacement()
        {
            if (CurrentState != PlacementState.READY)
            {
                Debug.LogWarning("[A3R_PHASE2] ConfirmPlacement ignored: not in READY state.");
                return;
            }

            CurrentState = PlacementState.PLACING;

            ARAnchor anchor = _previewAnchor;
            bool providerCreated = AnchorProviderCreated;

            // Fallback: if no calibration anchor exists, create one now
            if (anchor == null && _anchorManager != null)
            {
                try
                {
                    var result = await _anchorManager.TryAddAnchorAsync(_readyPlacementPose);
                    if (result.status.IsSuccess() && result.value != null)
                    {
                        anchor = result.value;
                        providerCreated = true;
                        Debug.Log($"[A3R_PHASE3] TryAddAnchorAsync placement succeeded. TrackableId: {anchor.trackableId}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[A3R_PHASE3] Exception in TryAddAnchorAsync: {ex.Message}");
                }

                // FIX 3: Do NOT fall back to an orphan AddComponent<ARAnchor>().
                // An ARAnchor added this way is NOT registered with ARAnchorManager.
                // ARCore never tracks it → TrackingState.None → no SLAM correction → board drifts.
                // Surface the failure so the user can retry with better plane detection.
                if (anchor == null)
                {
                    Debug.LogError("[A3R_ANCHOR] TryAddAnchorAsync failed in ConfirmPlacement. " +
                                   "Cannot create a valid anchor. Reverting to READY state. " +
                                   "Move phone to improve plane tracking quality, then retry PLACE BREADBOARD.");
                    CurrentState = PlacementState.READY;
                    AnchorProviderCreated = false;
                    return;
                }
            }

            if (anchor == null)
            {
                Debug.LogError("[A3R_PHASE3] ARAnchor creation rejected. Reverting to READY.");
                CurrentState = PlacementState.READY;
                AnchorProviderCreated = false;
                return;
            }

            MainAnchor = anchor;
            AnchorProviderCreated = providerCreated;
            MainAnchor.gameObject.name = "A3R_MainAnchor";

            // 1. Create BreadboardARRoot parented to provider ARAnchor
            _breadboardARRoot = new GameObject("BreadboardARRoot");
            _breadboardARRoot.transform.SetParent(anchor.transform, false);
            _breadboardARRoot.transform.localPosition = Vector3.zero;
            _breadboardARRoot.transform.localRotation = Quaternion.identity;
            _breadboardARRoot.transform.localScale = Vector3.one;

            // 2. Build coordinate anchor hierarchy
            CreateAnchorHierarchy(_breadboardARRoot.transform);

            // 3. Lock world pose
            _isRootLocked = true;

            // 4. Hide placement preview
            SetPreviewActive(false);

            // 5. Disable plane detection
            DisablePlaneDetection();

            // 6. Transition to PLACED
            CurrentState = PlacementState.PLACED;
            Debug.Log($"[A3R_PHASE3] BreadboardARRoot anchored at {_readyPlacementPose.position:F3}, rotation {_readyPlacementPose.rotation.eulerAngles:F3}.");

            // Clean up temporary registration anchor now that MainAnchor is established
            if (_registrationAnchor != null)
            {
                Destroy(_registrationAnchor.gameObject);
                _registrationAnchor = null;
            }

            // 7. Fire placement event
            OnBreadboardPlaced?.Invoke(_breadboardARRoot.transform);
        }

        private void CreateAnchorHierarchy(Transform root)
        {
            var calibFrameGo = new GameObject("CalibratedBoardFrame");
            calibFrameGo.transform.SetParent(root, false);
            calibFrameGo.transform.localPosition = Vector3.zero;
            calibFrameGo.transform.localRotation = Quaternion.identity;
            calibFrameGo.transform.localScale = Vector3.one;
            Transform calibRoot = calibFrameGo.transform;

            // 1. BreadboardBounds
            var boundsGo = new GameObject("BreadboardBounds");
            boundsGo.transform.SetParent(calibRoot, false);
            boundsGo.transform.localPosition = Vector3.zero;
            boundsGo.transform.localRotation = Quaternion.identity;
            boundsGo.transform.localScale = Vector3.one;
            var boundsComp = boundsGo.AddComponent<A3R_BreadboardBounds>();
            boundsComp.SetDimensions(A3R_BreadboardConfig.WIDTH, A3R_BreadboardConfig.DEPTH);
            BreadboardBoundsTransform = boundsGo.transform;

            // 2. HoleGrid
            var gridGo = new GameObject("HoleGrid");
            gridGo.transform.SetParent(calibRoot, false);
            gridGo.transform.localPosition = Vector3.zero;
            gridGo.transform.localRotation = Quaternion.identity;
            gridGo.transform.localScale = Vector3.one;
            gridGo.AddComponent<A3R_HoleGrid>();
            gridGo.AddComponent<A3R_BreadboardMapDebug>();
            HoleGridTransform = gridGo.transform;

            // 3. Hardware Anchors
            var esp32Go = new GameObject("ESP32Anchor");
            esp32Go.transform.SetParent(calibRoot, false);
            Esp32LocalPos = new Vector3(0.015f, A3R_BreadboardConfig.HEIGHT, 0f);
            esp32Go.transform.localPosition = Esp32LocalPos;
            esp32Go.transform.localRotation = Quaternion.identity;
            esp32Go.transform.localScale = Vector3.one;

            var ledGo = new GameObject("LEDAnchor");
            ledGo.transform.SetParent(calibRoot, false);
            LedLocalPos = new Vector3(-0.020f, A3R_BreadboardConfig.HEIGHT, 0.010f);
            ledGo.transform.localPosition = LedLocalPos;
            ledGo.transform.localRotation = Quaternion.identity;
            ledGo.transform.localScale = Vector3.one;

            var resGo = new GameObject("ResistorAnchor");
            resGo.transform.SetParent(calibRoot, false);
            ResistorLocalPos = A3R_BreadboardConfig.DEFAULT_RESISTOR_LOCAL_POS;
            resGo.transform.localPosition = ResistorLocalPos;
            resGo.transform.localRotation = Quaternion.identity;
            resGo.transform.localScale = Vector3.one;

            var wiresGo = new GameObject("WireAnchors");
            wiresGo.transform.SetParent(calibRoot, false);
            wiresGo.transform.localPosition = new Vector3(0f, A3R_BreadboardConfig.HEIGHT, 0f);
            wiresGo.transform.localRotation = Quaternion.identity;
            wiresGo.transform.localScale = Vector3.one;
            WireAnchorsTransform = wiresGo.transform;

            string[] wireNames = new[] { "WireAnchor_01", "WireAnchor_02", "WireAnchor_03", "WireAnchor_04" };
            Vector3[] wireOffsets = new[]
            {
                new Vector3(-0.020f, 0f,  0.005f),
                new Vector3(-0.010f, 0f,  0.010f),
                new Vector3( 0.000f, 0f, -0.010f),
                new Vector3( 0.010f, 0f, -0.005f)
            };
            for (int i = 0; i < wireNames.Length; i++)
            {
                var wGo = new GameObject(wireNames[i]);
                wGo.transform.SetParent(wiresGo.transform, false);
                wGo.transform.localPosition = wireOffsets[i];
                wGo.transform.localRotation = Quaternion.identity;
                wGo.transform.localScale = Vector3.one;
            }

            // 4. TutorialVisuals
            var tutGo = new GameObject("TutorialVisuals");
            tutGo.transform.SetParent(calibRoot, false);
            tutGo.transform.localPosition = Vector3.zero;
            tutGo.transform.localRotation = Quaternion.identity;
            tutGo.transform.localScale = Vector3.one;
            TutorialVisualsTransform = tutGo.transform;

            // 5. Post-placement validation visual parented to calibRoot
            BuildBreadboardVisual("PlacedValidationVisual", calibRoot, false, A3R_BreadboardConfig.WIDTH, A3R_BreadboardConfig.DEPTH);
        }

        private void BuildBreadboardVisual(string name, Transform parent, bool isPreview, float width, float depth)
        {
            if (width < 0.010f) width = A3R_BreadboardConfig.WIDTH;
            if (depth < 0.010f) depth = A3R_BreadboardConfig.DEPTH;

            var visualRoot = new GameObject(name);
            visualRoot.transform.SetParent(parent, false);
            visualRoot.transform.localPosition = new Vector3(0f, A3R_BreadboardConfig.HEIGHT, 0f);
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            // 1. Cyan Rectangular Outline
            var outlineGo = new GameObject("BreadboardOutline");
            outlineGo.transform.SetParent(visualRoot.transform, false);
            outlineGo.transform.localPosition = Vector3.zero;
            outlineGo.transform.localRotation = Quaternion.identity;
            outlineGo.transform.localScale = Vector3.one;

            var mf = outlineGo.AddComponent<MeshFilter>();
            mf.sharedMesh = ProceduralMeshUtility.CreateFootprintOutlineMesh(
                width, depth,
                0.0025f,   // 2.5mm line thickness
                0.0005f    // 0.5mm elevation above board
            );
            var mr = outlineGo.AddComponent<MeshRenderer>();
            mr.material = ProceduralMeshUtility.CreateMaterial(new Color(0f, 1f, 1f, 1f), false, false);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (isPreview)
            {
                _previewVisualGo = visualRoot;
                _cyanOutlineGo = outlineGo;
                _previewRenderer = mr;
            }

            // 2. Center Marker (Yellow sphere)
            var centerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            centerGo.name = "CenterMarker";
            centerGo.transform.SetParent(visualRoot.transform, false);
            centerGo.transform.localPosition = new Vector3(0f, 0.001f, 0f);
            centerGo.transform.localScale = new Vector3(0.004f, 0.004f, 0.004f);
            var centerCol = centerGo.GetComponent<Collider>();
            if (centerCol != null) Destroy(centerCol);
            var centerMr = centerGo.GetComponent<MeshRenderer>();
            centerMr.material = ProceduralMeshUtility.CreateMaterial(Color.yellow, false, false);
            centerMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            centerMr.receiveShadows = false;

            // 3. Corner Markers (Yellow spheres)
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            Vector3[] cornerPositions = new[]
            {
                new Vector3( halfW, 0.001f,  halfD),
                new Vector3(-halfW, 0.001f,  halfD),
                new Vector3(-halfW, 0.001f, -halfD),
                new Vector3( halfW, 0.001f, -halfD)
            };
            string[] cornerNames = { "Corner_NE", "Corner_NW", "Corner_SW", "Corner_SE" };
            for (int i = 0; i < 4; i++)
            {
                var cGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cGo.name = cornerNames[i];
                cGo.transform.SetParent(visualRoot.transform, false);
                cGo.transform.localPosition = cornerPositions[i];
                cGo.transform.localScale = CORNER_MARKER_SCALE;
                var cCol = cGo.GetComponent<Collider>();
                if (cCol != null) Destroy(cCol);
                var cMr = cGo.GetComponent<MeshRenderer>();
                cMr.sharedMaterial = GetOrCreateCornerMaterial();
                cMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cMr.receiveShadows = false;
                cMr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                cMr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            // 4. +X Axis Indicator (Red)
            var xIndGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            xIndGo.name = "Axis_X_Indicator";
            xIndGo.transform.SetParent(visualRoot.transform, false);
            xIndGo.transform.localPosition = new Vector3(0.0125f, 0.001f, 0f);
            xIndGo.transform.localScale = new Vector3(0.025f, 0.0015f, 0.0015f);
            var xCol = xIndGo.GetComponent<Collider>();
            if (xCol != null) Destroy(xCol);
            var xMr = xIndGo.GetComponent<MeshRenderer>();
            xMr.material = ProceduralMeshUtility.CreateMaterial(new Color(1f, 0.2f, 0.2f, 1f), false, false);
            xMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            xMr.receiveShadows = false;

            // 5. +Z Axis Indicator (Blue)
            var zIndGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zIndGo.name = "Axis_Z_Indicator";
            zIndGo.transform.SetParent(visualRoot.transform, false);
            zIndGo.transform.localPosition = new Vector3(0f, 0.001f, 0.009f);
            zIndGo.transform.localScale = new Vector3(0.0015f, 0.0015f, 0.018f);
            var zCol = zIndGo.GetComponent<Collider>();
            if (zCol != null) Destroy(zCol);
            var zMr = zIndGo.GetComponent<MeshRenderer>();
            zMr.material = ProceduralMeshUtility.CreateMaterial(new Color(0.2f, 0.4f, 1f, 1f), false, false);
            zMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zMr.receiveShadows = false;
        }

        private void DisablePlaneDetection()
        {
            if (_planeManager != null)
            {
                _planeManager.enabled = false;
                foreach (var plane in _planeManager.trackables)
                {
                    plane.gameObject.SetActive(false);
                }
            }
        }

        // ── Plane Tracking Listener ──────────────────────────────────────────
        private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            if (_planeManager == null) return;

            int trackingCount = 0;
            TrackingState bestState = TrackingState.None;

            foreach (var plane in _planeManager.trackables)
            {
                if (plane.trackingState == TrackingState.Tracking)
                {
                    trackingCount++;
                    bestState = TrackingState.Tracking;
                }
                else if (plane.trackingState == TrackingState.Limited && bestState == TrackingState.None)
                {
                    bestState = TrackingState.Limited;
                }
            }

            TrackingPlaneCount = trackingCount;
            BestPlaneState = bestState.ToString();
        }

        // ── Calibration UI (OnGUI) ────────────────────────────────────────────
        private void OnGUI()
        {
            if (!A3R.UI.A3R_CleanUIController.DeveloperModeEnabled) return;

            if (!_stylesInitialized)
            {
                _btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _btnStyle.normal.textColor = Color.black;

                _rotBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _rotBtnStyle.normal.textColor = Color.white;

                _stylesInitialized = true;
            }

            float btnW = Mathf.Min(Screen.width * 0.46f, 440f);
            float btnH = 80f;
            float btnX = Screen.width - btnW - 24f;
            float btnY = Screen.height * 0.45f;

            var oldBg = GUI.backgroundColor;

            if (CurrentState == PlacementState.READY)
            {
                GUI.backgroundColor = new Color(0f, 0.95f, 1f, 0.95f);
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "PLACE BREADBOARD", _btnStyle))
                {
                    ConfirmPlacement();
                }
            }
            else if (CurrentState == PlacementState.FOUR_CORNER_REVIEW)
            {
                GUI.backgroundColor = new Color(0f, 0.9f, 0.3f, 0.95f);
                if (GUI.Button(new Rect(btnX, btnY, btnW, btnH), "REGISTER", _btnStyle))
                {
                    LockFourCornerRegistration();
                }

                GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 0.95f);
                if (GUI.Button(new Rect(btnX, btnY + btnH + 12f, btnW, btnH), "RESET CORNERS", _btnStyle))
                {
                    ResetFourCorner();
                }
            }
            else if (CurrentState >= PlacementState.FOUR_CORNER_TL && CurrentState <= PlacementState.FOUR_CORNER_BL)
            {
                GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f, 0.95f);
                if (GUI.Button(new Rect(btnX, btnY + btnH + 12f, btnW, btnH), "CANCEL / RESET", _btnStyle))
                {
                    ResetFourCorner();
                }
            }

            GUI.backgroundColor = oldBg;
        }
    }
}
