using System;
using UnityEngine;

namespace A3R.Affect
{
    /// <summary>
    /// FaceLandmarkBridge — Module 2 (input layer) of the A³R system.
    ///
    /// Wraps the MediaPipe Face Landmarker integration and exposes a simple API
    /// that delivers the 478 facial landmark positions as a flat float array.
    ///
    /// Integration Strategy (chosen in M3, locked here):
    ///   The MediaPipe Unity Plugin (com.github.homuler.MediaPipeUnityPlugin v0.14.x)
    ///   is the primary integration path. If unavailable, a Unity Sentis fallback
    ///   with an exported face landmarker model is used.
    ///
    /// This bridge is intentionally decoupled from AffectScorer so the integration
    /// approach can be swapped without modifying scoring logic.
    ///
    /// NOTE: The actual MediaPipe plugin integration requires the plugin package to be
    /// installed in Unity (see manifest.json) and setup per the plugin documentation.
    /// This script provides the interface and stub implementation — the plugin-specific
    /// code will be added once the MediaPipe Unity Plugin is confirmed for M3.
    /// </summary>
    public class FaceLandmarkBridge : MonoBehaviour
    {
        // ── Inspector Config ────────────────────────────────────────────────────
        [Header("Camera")]
        [Tooltip("The front-facing camera WebCamTexture source. " +
                 "Set at runtime by AndroidCameraManager or manually in Editor.")]
        [SerializeField] private WebCamTexture frontCameraTexture;

        [Header("Integration")]
        [Tooltip("If true, outputs random landmark data for UI testing without real MediaPipe.")]
        [SerializeField] private bool debugSimulateOutput = false;

        [Header("Debug")]
        [SerializeField] private bool logLandmarksFPS = false;

        // ── State ────────────────────────────────────────────────────────────────
        private float[]  _currentLandmarks;  // flat array: [x0,y0,z0, x1,y1,z1, ...]
        private bool     _initialized;
        private float    _lastUpdateTime;
        private int      _frameCount;

        // Total landmarks × 3 floats (x, y, z in normalised coords)
        private const int LANDMARK_COUNT     = 478;
        private const int LANDMARK_ARRAY_LEN = LANDMARK_COUNT * 3;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            InitializeFrontCamera();
            InitializeMediaPipe();
        }

        private void Update()
        {
            if (!_initialized) return;

            if (debugSimulateOutput)
            {
                UpdateSimulated();
                return;
            }

            // In the real integration, MediaPipe delivers results asynchronously
            // via callbacks registered in InitializeMediaPipe().
            // Nothing to poll here — OnLandmarksDetected() is called by the plugin.
        }

        private void OnDestroy()
        {
            if (frontCameraTexture != null && frontCameraTexture.isPlaying)
                frontCameraTexture.Stop();
        }

        // ── Public API ───────────────────────────────────────────────────────────
        /// <summary>
        /// Returns the latest flat landmark array [x0,y0,z0, x1,y1,z1, ...] for 478 points,
        /// or null if no face has been detected yet.
        /// Thread-safe: landmark array is written atomically.
        /// </summary>
        public float[] GetCurrentLandmarks() => _currentLandmarks;

        /// <summary>Returns true if the front camera is active and MediaPipe is initialized.</summary>
        public bool IsReady => _initialized;

        /// <summary>Returns the current front camera texture (for display in a RawImage, if needed).</summary>
        public WebCamTexture FrontCameraTexture => frontCameraTexture;

        // ── Initialization ───────────────────────────────────────────────────────
        private void InitializeFrontCamera()
        {
            // On Android, Unity's WebCamDevice with isFrontFacing=true
            foreach (var device in WebCamTexture.devices)
            {
                if (device.isFrontFacing)
                {
                    frontCameraTexture = new WebCamTexture(device.name, 640, 480, 30);
                    frontCameraTexture.Play();
                    Debug.Log($"[FaceLandmarkBridge] Front camera started: {device.name}");
                    break;
                }
            }

            if (frontCameraTexture == null)
                Debug.LogWarning("[FaceLandmarkBridge] No front-facing camera found. " +
                                 "Affect inference will not function.");
        }

        private void InitializeMediaPipe()
        {
            // ── MediaPipe Unity Plugin Integration (M3 implementation) ────────────
            // The actual plugin initialization happens here once
            // com.github.homuler.MediaPipeUnityPlugin v0.14.x is installed.
            //
            // Placeholder stub — replace with plugin-specific initialization:
            //   var options = new FaceLandmarkerOptions { ... };
            //   _faceLandmarker = new FaceLandmarker(options);
            //   _faceLandmarker.SetResultCallback(OnLandmarksDetected);
            //
            // For now, set debugSimulateOutput=true in Inspector to test downstream code.

            _initialized = true;
            Debug.Log("[FaceLandmarkBridge] Initialized. " +
                      (debugSimulateOutput ? "(SIMULATED MODE)" : "(MediaPipe stub — plugin integration pending M3)"));
        }

        // ── MediaPipe Result Callback ─────────────────────────────────────────────
        /// <summary>
        /// Called by the MediaPipe plugin when face landmarks are detected.
        /// Replace the stub implementation with the plugin's actual callback signature.
        /// </summary>
        public void OnLandmarksDetected(float[] landmarkArray)
        {
            if (landmarkArray != null && landmarkArray.Length == LANDMARK_ARRAY_LEN)
            {
                _currentLandmarks = landmarkArray;
                _lastUpdateTime   = Time.time;

                if (logLandmarksFPS)
                {
                    _frameCount++;
                    if (_frameCount % 30 == 0)
                        Debug.Log($"[FaceLandmarkBridge] Landmarks updated. " +
                                  $"Approx rate: {_frameCount / Time.time:F1} Hz");
                }
            }
        }

        // ── Simulated Output (for testing without MediaPipe) ─────────────────────
        private void UpdateSimulated()
        {
            // Produce plausible-looking landmark data for testing the downstream pipeline
            if (_currentLandmarks == null)
                _currentLandmarks = new float[LANDMARK_ARRAY_LEN];

            float t = Time.time;
            // Simulate a mildly confused face with slowly varying brow furrow
            for (int i = 0; i < LANDMARK_COUNT; i++)
            {
                _currentLandmarks[i * 3]     = 0.5f + UnityEngine.Random.Range(-0.01f, 0.01f);
                _currentLandmarks[i * 3 + 1] = 0.5f + UnityEngine.Random.Range(-0.01f, 0.01f);
                _currentLandmarks[i * 3 + 2] = 0.0f;
            }

            // Override key landmarks with physically meaningful positions
            // (mimic a neutral face)
            int[] lEye  = { 33, 133, 159, 145 };
            int[] rEye  = { 263, 362, 386, 374 };

            // Left eye
            _currentLandmarks[33  * 3] = 0.35f; _currentLandmarks[33  * 3 + 1] = 0.45f;
            _currentLandmarks[133 * 3] = 0.40f; _currentLandmarks[133 * 3 + 1] = 0.45f;
            _currentLandmarks[159 * 3] = 0.375f; _currentLandmarks[159 * 3 + 1] = 0.43f;
            _currentLandmarks[145 * 3] = 0.375f; _currentLandmarks[145 * 3 + 1] = 0.47f;

            // Right eye
            _currentLandmarks[263 * 3] = 0.65f; _currentLandmarks[263 * 3 + 1] = 0.45f;
            _currentLandmarks[362 * 3] = 0.60f; _currentLandmarks[362 * 3 + 1] = 0.45f;
            _currentLandmarks[386 * 3] = 0.625f; _currentLandmarks[386 * 3 + 1] = 0.43f;
            _currentLandmarks[374 * 3] = 0.625f; _currentLandmarks[374 * 3 + 1] = 0.47f;

            // Brows (simulated furrow that increases over a 10s cycle)
            float furrow = 0.5f + 0.3f * Mathf.Sin(t * 0.2f);
            _currentLandmarks[105 * 3] = 0.5f - furrow * 0.05f;
            _currentLandmarks[105 * 3 + 1] = 0.38f;
            _currentLandmarks[334 * 3] = 0.5f + furrow * 0.05f;
            _currentLandmarks[334 * 3 + 1] = 0.38f;

            // Face top/bottom
            _currentLandmarks[10  * 3 + 1] = 0.1f;
            _currentLandmarks[152 * 3 + 1] = 0.9f;
        }
    }
}
