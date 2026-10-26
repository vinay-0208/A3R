using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using A3R.ARTracking;

namespace A3R.Diagnostics
{
    /// <summary>
    /// A3R_ForensicDiagnostic — Minimal, non-intrusive runtime diagnostic for overlay stability on POCO X7 Pro.
    /// Captures low-frequency (4 Hz) anchor and camera pose telemetry strictly during PlacementState.PLACED.
    /// Does NOT modify any transforms, tracking configurations, or scene objects.
    /// </summary>
    public class A3R_ForensicDiagnostic : MonoBehaviour
    {
        private const float SAMPLE_INTERVAL = 0.25f; // 4 Hz (4 samples per second)

        private A3R_MarkerlessFoundationController _controller;
        private Camera _cam;

        private Vector3 _lastCamPos;
        private Vector3 _lastAnchorPos;
        private float _sampleTimer = 0f;
        private bool _isSessionActive = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            var go = new GameObject("A3R_ForensicDiagnostic_Runtime");
            go.AddComponent<A3R_ForensicDiagnostic>();
            DontDestroyOnLoad(go);
        }

        private void Start()
        {
            _cam = Camera.main;
            _controller = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
        }

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_controller == null) _controller = FindFirstObjectByType<A3R_MarkerlessFoundationController>();

            bool isPlaced = _controller != null && 
                            _controller.CurrentState == PlacementState.PLACED && 
                            _controller.MainAnchor != null;

            if (isPlaced)
            {
                var anchor = _controller.MainAnchor;

                // 1. Session start transition
                if (!_isSessionActive)
                {
                    _isSessionActive = true;
                    _lastAnchorPos = anchor.transform.position;
                    if (_cam != null) _lastCamPos = _cam.transform.position;
                    _sampleTimer = 0f;

                    Debug.Log($"[A3R_STABILITY] === SESSION STARTED: TrackableId={anchor.trackableId}, " +
                              $"InitialAnchorPos={_lastAnchorPos.ToString("F4")}, " +
                              $"InitialAnchorRot={anchor.transform.eulerAngles.ToString("F1")} ===");
                }

                // 2. Periodic sampling (no more than 4 Hz)
                _sampleTimer += Time.deltaTime;
                if (_sampleTimer >= SAMPLE_INTERVAL)
                {
                    _sampleTimer = 0f;

                    Vector3 currentAnchorPos = anchor.transform.position;
                    Vector3 currentAnchorRot = anchor.transform.eulerAngles;
                    float dAnchor = Vector3.Distance(currentAnchorPos, _lastAnchorPos) * 1000f; // mm
                    _lastAnchorPos = currentAnchorPos;

                    Vector3 currentCamPos = _cam != null ? _cam.transform.position : Vector3.zero;
                    Vector3 currentCamRot = _cam != null ? _cam.transform.eulerAngles : Vector3.zero;
                    float dCam = _cam != null ? Vector3.Distance(currentCamPos, _lastCamPos) * 1000f : 0f; // mm
                    if (_cam != null) _lastCamPos = currentCamPos;

                    float relDistMm = _cam != null ? Vector3.Distance(currentCamPos, currentAnchorPos) * 1000f : 0f;
                    TrackingState anchorTracking = anchor.trackingState;

                    Debug.Log($"[A3R_STABILITY] t={Time.time:F2}s | State={_controller.CurrentState} | AnchorState={anchorTracking} | " +
                              $"AnchorPos={currentAnchorPos.ToString("F4")} AnchorRot={currentAnchorRot.ToString("F1")} | " +
                              $"CamPos={currentCamPos.ToString("F4")} CamRot={currentCamRot.ToString("F1")} | " +
                              $"RelDist={relDistMm:F1}mm | dAnchor={dAnchor:F1}mm dCam={dCam:F1}mm");
                }
            }
            else if (_isSessionActive)
            {
                // 3. Session stop transition (placement reset, anchor destroyed, or state changed)
                _isSessionActive = false;
                string stateStr = _controller != null ? _controller.CurrentState.ToString() : "NullController";
                bool anchorNull = _controller == null || _controller.MainAnchor == null;
                Debug.Log($"[A3R_STABILITY] === SESSION STOPPED: Reason=StateChanged (CurrentState={stateStr}, AnchorNull={anchorNull}) ===");
            }
        }

        private void OnDisable()
        {
            if (_isSessionActive)
            {
                _isSessionActive = false;
                Debug.Log("[A3R_STABILITY] === SESSION STOPPED: Reason=ComponentDisabled ===");
            }
        }

        private void OnDestroy()
        {
            if (_isSessionActive)
            {
                _isSessionActive = false;
                Debug.Log("[A3R_STABILITY] === SESSION STOPPED: Reason=ComponentDestroyed ===");
            }
        }
    }
}
