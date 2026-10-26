using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace A3R.ARTracking
{
    /// <summary>
    /// Listens to ARTrackedImageManager for the "marker" image target.
    ///
    /// The XRReferenceImageLibrary is assigned to ARTrackedImageManager in the scene.
    /// It is rebuilt before every build by A3R_LibraryBuilder.RebuildLibrary()
    /// (called from BuildHelper.BuildAndroidApkStandard) to ensure m_DataStore
    /// is populated so ARCore has a non-empty image database.
    ///
    /// This script does NOT create or replace the reference library at runtime.
    /// </summary>
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class ARImageTracker : MonoBehaviour
    {
        [Header("Tracking")]
        [Tooltip("Must match the name set in XRReferenceImageLibrary (\"marker\").")]
        public string referenceImageName = "marker";

        [Header("Outline")]
        public float physicalWidth  = 0.085f;
        public float physicalHeight = 0.055f;

        private ARTrackedImageManager _mgr;
        private ARTrackedImage        _activeImage;   // the currently tracked image
        private GameObject            _contentRoot;
        private GameObject            _outlineRoot;
        private bool                  _isTracking;
        private int                   _frameCount;

        // ── Public diagnostics (read by A3R_ARDiagnosticsUI) ───────────────
        public bool      IsTracking       => _isTracking;
        public Transform ContentRoot      => _contentRoot != null ? _contentRoot.transform : null;
        public string    TrackingStateDiag { get; private set; } = "NONE";

        // ── Events ─────────────────────────────────────────────────────────
        public event Action<Transform> OnMarkerDetected;
        public event Action<Transform> OnMarkerUpdated;
        public event Action            OnMarkerLost;

        private void Awake()
        {
            _mgr = GetComponent<ARTrackedImageManager>();
        }

        private void OnEnable()
        {
            if (_mgr != null)
                _mgr.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        private void OnDisable()
        {
            if (_mgr != null)
                _mgr.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        private void Update()
        {
            _frameCount++;
            if (_frameCount % 90 == 0)
            {
                bool libAssigned = _mgr != null && _mgr.referenceLibrary != null;
                int  libCount    = libAssigned ? _mgr.referenceLibrary.count : -1;
                bool subRunning  = _mgr?.subsystem?.running ?? false;
                Debug.Log($"[A3R_IMAGE_TRACKING] Heartbeat | LibAssigned={libAssigned} | LibCount={libCount} | SubRunning={subRunning} | IsTracking={_isTracking} | TrackState={TrackingStateDiag}");
            }
        }

        // ── Tracking Events ─────────────────────────────────────────────────
        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            foreach (var img in args.added)
                if (IsOurMarker(img)) HandleAdded(img);

            foreach (var img in args.updated)
                if (IsOurMarker(img)) HandleUpdated(img);

            foreach (var kvp in args.removed)
                if (kvp.Value != null && IsOurMarker(kvp.Value)) HandleLost();
        }

        private bool IsOurMarker(ARTrackedImage img) =>
            string.Equals(img.referenceImage.name, referenceImageName, StringComparison.OrdinalIgnoreCase);

        private void HandleAdded(ARTrackedImage img)
        {
            _activeImage = img;
            Debug.Log($"[A3R_IMAGE_TRACKING] EVENT: ADDED | ref={img.referenceImage.name} | state={img.trackingState} | pos={img.transform.position} | refSize={img.referenceImage.size}");

            if (_contentRoot == null)
            {
                _contentRoot = new GameObject("BreadboardARRoot");
            }

            // Parent directly to the ARTrackedImage so pose updates automatically.
            // Zero local transform so it sits exactly on the detected image.
            _contentRoot.transform.SetParent(img.transform, false);
            _contentRoot.transform.localPosition = Vector3.zero;
            _contentRoot.transform.localRotation = Quaternion.identity;
            _contentRoot.transform.localScale    = Vector3.one;
            _contentRoot.SetActive(true);

            _isTracking = true;
            TrackingStateDiag = img.trackingState.ToString();

            EnsureOutline(img.referenceImage.size);
            SetOutlineVisible(true);

            Debug.Log($"[A3R_IMAGE_TRACKING] BreadboardARRoot parented to ARTrackedImage. WorldPos={_contentRoot.transform.position} WorldScale={_contentRoot.transform.lossyScale}");
            OnMarkerDetected?.Invoke(_contentRoot.transform);
        }

        private void HandleUpdated(ARTrackedImage img)
        {
            if (_contentRoot == null) { HandleAdded(img); return; }
            _activeImage = img;
            TrackingStateDiag = img.trackingState.ToString();

            switch (img.trackingState)
            {
                case TrackingState.Tracking:
                case TrackingState.Limited:
                    _contentRoot.SetActive(true);
                    SetOutlineVisible(true);
                    _isTracking = true;
                    OnMarkerUpdated?.Invoke(_contentRoot.transform);
                    break;

                case TrackingState.None:
                    _contentRoot.SetActive(false);
                    SetOutlineVisible(false);
                    if (_isTracking)
                    {
                        _isTracking = false;
                        Debug.Log("[A3R_IMAGE_TRACKING] TrackingState.None — overlay hidden");
                        OnMarkerLost?.Invoke();
                    }
                    break;
            }
        }

        private void HandleLost()
        {
            if (_contentRoot != null) _contentRoot.SetActive(false);
            SetOutlineVisible(false);
            if (_isTracking)
            {
                _isTracking = false;
                TrackingStateDiag = "REMOVED";
                Debug.Log("[A3R_IMAGE_TRACKING] EVENT: REMOVED — overlay hidden");
                OnMarkerLost?.Invoke();
            }
        }

        // ── Cyan Outline ───────────────────────────────────────────────────
        private void EnsureOutline(Vector2 refSize)
        {
            if (_outlineRoot != null) return;
            if (_contentRoot == null) return;

            float w     = refSize.x > 0.001f ? refSize.x : physicalWidth;
            float h     = refSize.y > 0.001f ? refSize.y : physicalHeight;
            float thick = 0.002f;
            float raised= 0.001f;

            _outlineRoot = new GameObject("BreadboardOutline");
            _outlineRoot.transform.SetParent(_contentRoot.transform, false);
            _outlineRoot.transform.localPosition = Vector3.zero;
            _outlineRoot.transform.localRotation = Quaternion.identity;
            _outlineRoot.transform.localScale    = Vector3.one;

            Color cyan = new Color(0f, 1f, 1f, 1f);
            MakeEdge(_outlineRoot, "EdgeTop",    new Vector3(0,    raised,  h/2f), new Vector3(w + thick, thick, thick), cyan);
            MakeEdge(_outlineRoot, "EdgeBottom", new Vector3(0,    raised, -h/2f), new Vector3(w + thick, thick, thick), cyan);
            MakeEdge(_outlineRoot, "EdgeLeft",   new Vector3(-w/2f,raised,  0),   new Vector3(thick, thick, h),          cyan);
            MakeEdge(_outlineRoot, "EdgeRight",  new Vector3( w/2f,raised,  0),   new Vector3(thick, thick, h),          cyan);

            Debug.Log($"[A3R_IMAGE_TRACKING] Cyan outline built: {w*100f:F1}cm x {h*100f:F1}cm");
        }

        private void MakeEdge(GameObject parent, string eName, Vector3 lPos, Vector3 lScale, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<BoxCollider>());
            go.name = eName;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = lPos;
            go.transform.localScale    = lScale;

            var mat = new Material(Shader.Find("Standard"));
            mat.color = col;
            mat.SetFloat("_Glossiness", 0f);
            go.GetComponent<MeshRenderer>().material = mat;
        }

        private void SetOutlineVisible(bool v)
        {
            if (_outlineRoot != null) _outlineRoot.SetActive(v);
        }
    }
}
