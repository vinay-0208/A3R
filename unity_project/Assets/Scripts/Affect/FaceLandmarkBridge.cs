// ────────────────────────────────────────────────────────────────────────────
// FaceLandmarkBridge.cs — A³R Affect-Adaptive AR
// Module 2 (Input Layer): MediaPipe FaceLandmarker Integration
//
// INSTALLATION REQUIREMENT:
//   This file integrates with com.github.homuler.mediapipe (MediaPipeUnityPlugin).
//   The plugin must be installed via its PRE-BUILT RELEASE .tgz file:
//
//   1. Download from: https://github.com/homuler/MediaPipeUnityPlugin/releases
//      File: com.github.homuler.mediapipe-<version>.tgz (latest ≥ v0.14.0)
//   2. In Unity: Window → Package Manager → "+" → "Add package from tarball..."
//   3. Select the downloaded .tgz file.
//   4. Download face_landmarker.task to Assets/StreamingAssets/ (see README.md there).
//   5. In Player Settings → Scripting Define Symbols, add: MEDIAPIPE_UNITY_PLUGIN
//
//   DO NOT use the git URL in manifest.json — it pulls C# sources only, without the
//   required native Protobuf DLLs and C++ shared libraries.
//
// COMPILE STATUS:
//   WITHOUT plugin installed:  Compiles cleanly. FaceLandmarkBridge runs in
//                              MissingDependencyFallback mode (no inference).
//   WITH plugin installed:     Real MediaPipe inference path is active.
//
// ANDROID HARDWARE TEST:       NOT PERFORMED (2026-09-06). Awaiting physical device.
// SIMULATION MODE:             Preserved. Clearly isolated. Never used for real claims.
// ────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

// ── MediaPipe Tasks API (only active when plugin is installed) ───────────────
#if MEDIAPIPE_UNITY_PLUGIN
using Mediapipe;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Components.Containers;
#endif

namespace A3R.Affect
{
    /// <summary>
    /// FaceLandmarkBridge — Module 2 (input layer) of the A³R system.
    ///
    /// Exposes 478-landmark flat float array [x0,y0,z0, x1,y1,z1, ...] from the
    /// MediaPipe Face Landmarker Tasks API to AffectFeatureExtractor and AffectScorer.
    ///
    /// === Pipeline Modes ===
    ///   RealMediaPipe            Production mode. Requires MEDIAPIPE_UNITY_PLUGIN
    ///                            scripting define + plugin installed via .tgz.
    ///                            Runs FaceLandmarker (LIVE_STREAM) on WebCamTexture
    ///                            front camera frames.
    ///
    ///   SimulatedDebug           Offline/Editor testing mode. Generates synthetic
    ///                            plausible neutral-face landmarks without hardware.
    ///                            NEVER used for real affect inference claims.
    ///                            AffectScorer stamps all samples with IsSimulated=true.
    ///
    ///   MissingDependencyFallback  Automatic when plugin is absent, model file
    ///                            missing, camera permission denied, or init fails.
    ///                            Returns null landmarks. AffectScorer reports "no face".
    ///
    /// === Camera Architecture ===
    ///   ARCore: rear camera (Camera2 API, exclusive session).
    ///   Front camera: separate hardware unit; accessible via WebCamTexture on API 30+.
    ///   ARCore minimum is API 30+, so concurrent access is architecturally supported.
    ///   Conflicts are caught in try/catch with graceful fallback.
    ///
    /// === Frame Queue Policy ===
    ///   LATEST-FRAME-WINS: if inference is active, new frame is dropped.
    ///   Throttled to maxInferenceHz (default 5 Hz). Prevents backlog accumulation.
    ///   DroppedInferenceFrames counter tracks drops for instrumentation.
    ///
    /// === Performance Instrumentation ===
    ///   CameraFps, MediaPipeFps, LastInferenceLatencyMs, DroppedInferenceFrames
    ///   are measured from actual timing, not fabricated values.
    ///   Meaningful numbers only available on real Android hardware.
    /// </summary>
    public class FaceLandmarkBridge : MonoBehaviour
    {
        // ── Pipeline Mode ────────────────────────────────────────────────────────
        public enum InferencePipelineMode
        {
            RealMediaPipe,
            SimulatedDebug,
            MissingDependencyFallback
        }

        // ── Inspector Config ─────────────────────────────────────────────────────
        [Header("Pipeline Mode")]
        [Tooltip(
            "RealMediaPipe: on-device MediaPipe inference (requires plugin + model).\n" +
            "SimulatedDebug: synthetic geometry for offline testing.\n" +
            "MissingDependencyFallback: no inference (set automatically on error).")]
        [SerializeField] private InferencePipelineMode pipelineMode = InferencePipelineMode.RealMediaPipe;

        [Header("MediaPipe / Model")]
        [Tooltip("Name of .task model file in Assets/StreamingAssets/. Download from MediaPipe models page.")]
        [SerializeField] private string modelFileName = "face_landmarker.task";

        [Tooltip("Inference acceleration. GPU recommended for Android; falls back to CPU automatically.")]
        [SerializeField] private InferenceDelegateMode inferenceDelegate = InferenceDelegateMode.GPU;

        [Tooltip("Max simultaneous faces to detect. Keep 1 for performance.")]
        [SerializeField] [Range(1, 2)] private int numFaces = 1;

        [Tooltip("Min face detection confidence [0-1].")]
        [SerializeField] [Range(0.1f, 1f)] private float detectionConfidence = 0.5f;

        [Tooltip("Min face tracking confidence [0-1].")]
        [SerializeField] [Range(0.1f, 1f)] private float trackingConfidence  = 0.5f;

        [Header("Front Camera")]
        [Tooltip("Requested WebCamTexture width.")]
        [SerializeField] private int cameraWidth     = 640;
        [Tooltip("Requested WebCamTexture height.")]
        [SerializeField] private int cameraHeight    = 480;
        [Tooltip("Requested camera framerate (fps).")]
        [SerializeField] private int cameraFramerate = 30;

        [Header("Inference Rate")]
        [Tooltip("Max inference submissions per second. Frames beyond this are dropped.")]
        [SerializeField] [Range(1f, 15f)] private float maxInferenceHz = 5f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // ── Delegate proxy enum (avoids compile dependency on Mediapipe.Tasks.Core) ──
        public enum InferenceDelegateMode { CPU, GPU }

        // ── Public Properties ────────────────────────────────────────────────────
        public InferencePipelineMode Mode            => pipelineMode;
        public bool                  IsSimulated     => pipelineMode == InferencePipelineMode.SimulatedDebug;
        public bool                  IsRealInference => pipelineMode == InferencePipelineMode.RealMediaPipe;
        public bool                  IsInitialized   => _initialized;
        public bool                  FrontCameraAvailable => _frontCameraAvailable;
        public WebCamTexture         FrontCameraTexture   => _webCamTexture;

        // ── Performance Metrics ──────────────────────────────────────────────────
        /// <summary>Measured WebCamTexture updates per second (1s sliding window).</summary>
        public float CameraFps              { get; private set; }
        /// <summary>Measured MediaPipe inference completions per second (1s sliding window).</summary>
        public float MediaPipeFps           { get; private set; }
        /// <summary>Duration of last MediaPipe inference call in milliseconds.</summary>
        public float LastInferenceLatencyMs { get; private set; }
        /// <summary>Total dropped frames due to inference backlog (bounded queue overflow).</summary>
        public int   DroppedInferenceFrames { get; private set; }

        // ── Constants ────────────────────────────────────────────────────────────
        private const int LANDMARK_COUNT     = 478;
        private const int LANDMARK_ARRAY_LEN = LANDMARK_COUNT * 3;

        // ── State ────────────────────────────────────────────────────────────────
        private float[]          _landmarks;
        private readonly float[] _bufferA = new float[LANDMARK_ARRAY_LEN];
        private readonly float[] _bufferB = new float[LANDMARK_ARRAY_LEN];
        private bool             _useBufferA;
        private long             _lastTimestampMs;
        private bool             _initialized;
        private bool             _frontCameraAvailable;
        private WebCamTexture    _webCamTexture;
        private Texture2D        _readbackTex;
        private volatile bool    _isInferencing;
        private Coroutine        _mainLoopCoroutine;

        // Instrumentation
        private int   _cameraFrameCount;
        private int   _mpFrameCount;
        private float _fpsWindowStart;
        private readonly Stopwatch _sw = new();

#if MEDIAPIPE_UNITY_PLUGIN
        // MediaPipe Tasks API objects — only exist when plugin is installed.
        private FaceLandmarker _faceLandmarker;
        private bool           _mediaPipeReady;
#endif

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private IEnumerator Start()
        {
            _fpsWindowStart = Time.time;

            // Step 1: Acquire front camera
            yield return InitFrontCamera();

            // Step 2: Initialize MediaPipe (requires MEDIAPIPE_UNITY_PLUGIN symbol)
            if (pipelineMode == InferencePipelineMode.RealMediaPipe)
            {
#if MEDIAPIPE_UNITY_PLUGIN
                yield return InitMediaPipe();
#else
                Debug.LogWarning(
                    "[FaceLandmarkBridge] RealMediaPipe requested but MEDIAPIPE_UNITY_PLUGIN " +
                    "scripting define is NOT set. The plugin is not installed or not configured.\n" +
                    "Install via: Window → Package Manager → '+' → 'Add from tarball...'\n" +
                    "Then add MEDIAPIPE_UNITY_PLUGIN to Player Settings → Scripting Define Symbols.\n" +
                    "Switching to MissingDependencyFallback.");
                pipelineMode = InferencePipelineMode.MissingDependencyFallback;
#endif
            }

            _initialized = true;
            Debug.Log($"[FaceLandmarkBridge] Initialized — mode: {pipelineMode}");

            // Step 3: Start the appropriate inference or simulation loop
            switch (pipelineMode)
            {
#if MEDIAPIPE_UNITY_PLUGIN
                case InferencePipelineMode.RealMediaPipe when _mediaPipeReady:
                    _mainLoopCoroutine = StartCoroutine(RealInferenceLoop());
                    break;
#endif
                case InferencePipelineMode.SimulatedDebug:
                    _mainLoopCoroutine = StartCoroutine(SimulatedLoop());
                    break;

                default:
                    Debug.LogWarning(
                        "[FaceLandmarkBridge] MissingDependencyFallback: no inference running. " +
                        "GetCurrentLandmarks() returns null. AffectScorer will report no face.");
                    break;
            }
        }

        private void Update()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying && _webCamTexture.didUpdateThisFrame)
                _cameraFrameCount++;

            float elapsed = Time.time - _fpsWindowStart;
            if (elapsed >= 1.0f)
            {
                CameraFps    = _cameraFrameCount / elapsed;
                MediaPipeFps = _mpFrameCount     / elapsed;
                _cameraFrameCount = 0;
                _mpFrameCount     = 0;
                _fpsWindowStart   = Time.time;
            }
        }

        private void OnDestroy()
        {
            if (_mainLoopCoroutine != null)
                StopCoroutine(_mainLoopCoroutine);

#if MEDIAPIPE_UNITY_PLUGIN
            try { _faceLandmarker?.Close(); }
            catch (Exception ex) { Debug.LogWarning($"[FaceLandmarkBridge] Error closing FaceLandmarker: {ex.Message}"); }
            _faceLandmarker = null;
#endif
            if (_webCamTexture != null && _webCamTexture.isPlaying)
                _webCamTexture.Stop();
            if (_readbackTex != null)
                Destroy(_readbackTex);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                if (_webCamTexture != null && _webCamTexture.isPlaying)
                    _webCamTexture.Pause();
            }
            else
            {
                if (_webCamTexture != null && _frontCameraAvailable && !_webCamTexture.isPlaying)
                    _webCamTexture.Play();
            }
        }

        // ── Public API ───────────────────────────────────────────────────────────
        /// <summary>
        /// Latest 478-landmark flat array [x0,y0,z0, x1,y1,z1, ...].
        /// Values in normalized image coords [0,1]. Returns null when no face or unavailable.
        /// Thread-safe: reference write is atomic in .NET.
        /// </summary>
        public float[] GetCurrentLandmarks() => _landmarks;

        // ── Front Camera Initialization ──────────────────────────────────────────
        private IEnumerator InitFrontCamera()
        {
            _frontCameraAvailable = false;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Debug.Log("[FaceLandmarkBridge] Requesting CAMERA permission...");
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                float timeout = Time.realtimeSinceStartup + 6.0f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && Time.realtimeSinceStartup < timeout)
                {
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Debug.LogError("[FaceLandmarkBridge] CAMERA permission not granted. " +
                               "Switching to MissingDependencyFallback.");
                if (pipelineMode == InferencePipelineMode.RealMediaPipe)
                    pipelineMode = InferencePipelineMode.MissingDependencyFallback;
                yield break;
            }
            Debug.Log("[FaceLandmarkBridge] CAMERA permission confirmed granted.");
#else
            yield return null;
#endif

            bool cameraStarted = false;
            try
            {
                WebCamDevice? front = null;
                foreach (var dev in WebCamTexture.devices)
                {
                    if (dev.isFrontFacing) { front = dev; break; }
                }

                if (front == null)
                {
                    Debug.LogWarning("[FaceLandmarkBridge] No front-facing camera found.");
                    if (pipelineMode == InferencePipelineMode.RealMediaPipe)
                        pipelineMode = InferencePipelineMode.MissingDependencyFallback;
                    yield break;
                }

                _webCamTexture = new WebCamTexture(
                    front.Value.name, cameraWidth, cameraHeight, cameraFramerate);
                _webCamTexture.Play();
                _frontCameraAvailable = true;
                cameraStarted = true;
                Debug.Log($"[FaceLandmarkBridge] Front camera '{front.Value.name}' started " +
                          $"({cameraWidth}×{cameraHeight} @ {cameraFramerate}fps).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[FaceLandmarkBridge] Front camera init failed: " + ex.Message + "\n" +
                    "May indicate Camera2 resource conflict (unlikely on API 30+). " +
                    "Switching to MissingDependencyFallback.");
                _webCamTexture = null;
                _frontCameraAvailable = false;
                if (pipelineMode == InferencePipelineMode.RealMediaPipe)
                    pipelineMode = InferencePipelineMode.MissingDependencyFallback;
            }

            // Wait up to 3s for first frame — OUTSIDE try/catch (C# disallows yield inside try-catch)
            if (cameraStarted && _webCamTexture != null)
            {
                float waited = 0f;
                while (!_webCamTexture.didUpdateThisFrame && waited < 3f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }


        // ── MediaPipe Initialization (only compiled when plugin is installed) ────
#if MEDIAPIPE_UNITY_PLUGIN
        private IEnumerator InitMediaPipe()
        {
            _mediaPipeReady = false;

#if UNITY_ANDROID && !UNITY_EDITOR
            // StreamingAssets on Android live inside the APK — must copy to persistentDataPath.
            string destPath = Path.Combine(Application.persistentDataPath, modelFileName);
            if (!File.Exists(destPath))
            {
                string srcUrl = Path.Combine(Application.streamingAssetsPath, modelFileName);
                Debug.Log($"[FaceLandmarkBridge] Copying {modelFileName} from StreamingAssets...");
                using var req = UnityWebRequest.Get(srcUrl);
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(
                        $"[FaceLandmarkBridge] Cannot load model: {req.error}\n" +
                        "Download face_landmarker.task → Assets/StreamingAssets/. See README.md there.\n" +
                        "Switching to MissingDependencyFallback.");
                    pipelineMode = InferencePipelineMode.MissingDependencyFallback;
                    yield break;
                }
                File.WriteAllBytes(destPath, req.downloadHandler.data);
                Debug.Log($"[FaceLandmarkBridge] Model cached at: {destPath}");
            }
            string modelPath = destPath;
#else
            string modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
            yield return null;
            if (!File.Exists(modelPath))
            {
                Debug.LogWarning(
                    $"[FaceLandmarkBridge] Model not found: {modelPath}\n" +
                    "Download face_landmarker.task → Assets/StreamingAssets/. See README.md there.\n" +
                    "Falling back to SimulatedDebug for Editor development.");
                pipelineMode = InferencePipelineMode.SimulatedDebug;
                yield break;
            }
#endif

            try
            {
                var baseOptions = new BaseOptions(
                    delegateCase: inferenceDelegate == InferenceDelegateMode.GPU
                        ? BaseOptions.Delegate.GPU
                        : BaseOptions.Delegate.CPU,
                    modelAssetPath: modelPath
                );

                var options = new FaceLandmarkerOptions(
                    baseOptions:                baseOptions,
                    runningMode:                RunningMode.LIVE_STREAM,
                    numFaces:                   numFaces,
                    minFaceDetectionConfidence: detectionConfidence,
                    minFacePresenceConfidence:  trackingConfidence,
                    minTrackingConfidence:      trackingConfidence,
                    outputFaceBlendshapes:      false,
                    outputFaceTransformationMatrixes: false,
                    resultCallback:             HandleFaceLandmarkerResult
                );

                _faceLandmarker = FaceLandmarker.CreateFromOptions(options);
                _mediaPipeReady = true;

                int texW = _webCamTexture != null && _webCamTexture.width > 16 ? _webCamTexture.width  : cameraWidth;
                int texH = _webCamTexture != null && _webCamTexture.height > 16 ? _webCamTexture.height : cameraHeight;
                _readbackTex = new Texture2D(texW, texH, TextureFormat.RGBA32, mipChain: false);

                Debug.Log($"[FaceLandmarkBridge] FaceLandmarker ready — model: {modelFileName}, " +
                          $"delegate: {inferenceDelegate}, numFaces: {numFaces}");
                Debug.Log($"[A3R_DIAG] MediaPipe initialized: {_mediaPipeReady} | IsSimulated: {IsSimulated} | Delegate: {inferenceDelegate}");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[FaceLandmarkBridge] FaceLandmarker.CreateFromOptions failed: " + ex + "\n" +
                    "Ensure native plugin DLLs and .so files are present from the .tgz release.\n" +
                    "Switching to MissingDependencyFallback.");
                pipelineMode = InferencePipelineMode.MissingDependencyFallback;
            }
        }

        // ── Real Inference Loop ──────────────────────────────────────────────────
        private bool _loggedCameraMetadata = false;

        private IEnumerator RealInferenceLoop()
        {
            var wait = new WaitForSeconds(1f / maxInferenceHz);

            while (true)
            {
                yield return wait;

                if (_webCamTexture == null || !_webCamTexture.isPlaying || _webCamTexture.width <= 16)
                    continue;

                // LATEST-FRAME-WINS: skip if inference still in progress
                if (_isInferencing)
                {
                    DroppedInferenceFrames++;
                    continue;
                }

                _isInferencing = true;
                _sw.Restart();

                try
                {
                    if (_readbackTex == null || _readbackTex.width != _webCamTexture.width || _readbackTex.height != _webCamTexture.height)
                    {
                        if (_readbackTex != null) Destroy(_readbackTex);
                        _readbackTex = new Texture2D(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, mipChain: false);
                    }

                    if (!_loggedCameraMetadata)
                    {
                        _loggedCameraMetadata = true;
                        Debug.Log($"[A3R_DIAG_CAMERA] W:{_webCamTexture.width} H:{_webCamTexture.height} " +
                                  $"Rot:{_webCamTexture.videoRotationAngle} Mir:{_webCamTexture.videoVerticallyMirrored} " +
                                  $"MpRot:{_webCamTexture.videoRotationAngle}");
                    }

                    // Copy current WebCamTexture frame into pre-allocated Texture2D.
                    // GetPixels32 allocates ~10KB per call (Color32[] at 640x480).
                    // Accepted GC cost at 5 Hz = ~50 KB/s.
                    _readbackTex.SetPixels32(_webCamTexture.GetPixels32());
                    _readbackTex.Apply(updateMipmaps: false);

                    // Wrap as Mediapipe.Image (wraps native texture, no copy).
                    // TextureFormat.RGBA32 matches ImageFormat.Types.Format.Srgba (4 channels).
                    // Monotonically increasing millisecond timestamp is strictly enforced.
                    long timestampMs = (long)(Time.time * 1000.0);
                    if (timestampMs <= _lastTimestampMs)
                        timestampMs = _lastTimestampMs + 1;
                    _lastTimestampMs = timestampMs;

                    int rot = _webCamTexture.videoRotationAngle;
                    var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: rot);

                    using var mpImage = new Image(ImageFormat.Types.Format.Srgba, _readbackTex);
                    _faceLandmarker.DetectAsync(mpImage, timestampMs, imageProcessingOptions);
                    // _isInferencing released in HandleFaceLandmarkerResult.
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FaceLandmarkBridge] DetectAsync error: {ex.Message}");
                    _isInferencing = false;  // always release on error
                }
            }
        }

        // ── MediaPipe Result Callback ─────────────────────────────────────────────
        /// <summary>
        /// Called by MediaPipe Tasks runtime (may be background thread) on DetectAsync completion.
        /// Extracts NormalizedLandmarks into flat float[1434] array.
        /// Thread-safe: double-buffered array filled completely before atomic assignment to _landmarks.
        /// </summary>
        private void HandleFaceLandmarkerResult(
            FaceLandmarkerResult result,
            Image                image,
            long                 timestampMillisec)
        {
            _sw.Stop();
            LastInferenceLatencyMs = (float)_sw.Elapsed.TotalMilliseconds;
            _mpFrameCount++;
            _isInferencing = false;

            if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
            {
                _landmarks = null;
                if (_mpFrameCount == 1 || _mpFrameCount % 15 == 0)
                {
                    Debug.Log($"[A3R_DIAG] FaceDetected: false | LandmarkCount: 0 | CallbackCount: {_mpFrameCount} | LatencyMs: {LastInferenceLatencyMs:F1} | DroppedFrames: {DroppedInferenceFrames} | IsSimulated: {IsSimulated}");
                }
                return;
            }

            NormalizedLandmarks face = result.faceLandmarks[0];
            if (face.landmarks == null || face.landmarks.Count != LANDMARK_COUNT)
            {
                Debug.LogWarning(
                    $"[FaceLandmarkBridge] Expected {LANDMARK_COUNT} landmarks, " +
                    $"got {face.landmarks?.Count ?? 0}. Frame discarded.");
                _landmarks = null;
                return;
            }

            // Fill inactive buffer and atomically swap pointer (thread-safe without locks)
            float[] target = _useBufferA ? _bufferA : _bufferB;
            _useBufferA = !_useBufferA;

            for (int i = 0; i < LANDMARK_COUNT; i++)
            {
                var lm           = face.landmarks[i];
                target[i * 3]     = lm.x;   // [0,1] left→right
                target[i * 3 + 1] = lm.y;   // [0,1] top→bottom
                target[i * 3 + 2] = lm.z;   // depth (not used by AffectFeatureExtractor)
            }

            _landmarks = target;  // atomic reference swap

            if (_mpFrameCount % 30 == 0)
            {
                Debug.Log($"[A3R_DIAG] FaceDetected: true | LandmarkCount: {face.landmarks.Count} | CallbackCount: {_mpFrameCount} | LatencyMs: {LastInferenceLatencyMs:F1} | DroppedFrames: {DroppedInferenceFrames} | IsSimulated: {IsSimulated}");
            }
        }
#endif  // MEDIAPIPE_UNITY_PLUGIN

        // ── Simulated Mode ───────────────────────────────────────────────────────
        /// <summary>
        /// SIMULATION MODE — offline/Editor testing only.
        /// Generates plausible neutral face geometry for verifying AffectFeatureExtractor
        /// and AffectScorer logic without requiring hardware.
        ///
        /// MUST NEVER be used for real affect inference claims.
        /// AffectScorer reads IsSimulated and stamps all samples accordingly.
        /// </summary>
        private IEnumerator SimulatedLoop()
        {
            Debug.Log(
                "[FaceLandmarkBridge] *** SIMULATION MODE *** Landmarks are SYNTHETIC. " +
                "Affect scores derived from these MUST NOT be reported as real results.");

            var wait = new WaitForSeconds(1f / maxInferenceHz);
            while (true)
            {
                yield return wait;
                GenerateSimulatedLandmarks();
                _mpFrameCount++;
            }
        }

        private void GenerateSimulatedLandmarks()
        {
            float[] target = _useBufferA ? _bufferA : _bufferB;
            _useBufferA = !_useBufferA;

            float t = Time.time;

            // Default all to image center
            for (int i = 0; i < LANDMARK_COUNT; i++)
            {
                target[i * 3]     = 0.5f;
                target[i * 3 + 1] = 0.5f;
                target[i * 3 + 2] = 0.0f;
            }

            // Inter-ocular distance anchors: IOD = 0.20 (landmarks 33↔263)
            Set(target, 33,  0.40f, 0.50f);    // L eye outer
            Set(target, 263, 0.60f, 0.50f);    // R eye outer
            Set(target, 10,  0.50f, 0.10f);    // face top
            Set(target, 152, 0.50f, 0.90f);    // face bottom

            // Brow furrow: slow sinusoidal cycle [0.2–0.8]
            float furrow   = 0.5f + 0.3f * Mathf.Sin(t * 0.2f);
            float browOff  = (1f - furrow) * 0.02f;
            Set(target, 105, 0.46f + browOff, 0.38f);   // L inner brow
            Set(target, 334, 0.54f - browOff, 0.38f);   // R inner brow

            // Left eye — neutral open (EAR ≈ 0.28, within calibrated [0.15, 0.35])
            Set(target, 133, 0.430f, 0.500f);    // L eye inner
            Set(target, 159, 0.415f, 0.491f);    // L eye top
            Set(target, 145, 0.415f, 0.509f);    // L eye bottom

            // Right eye
            Set(target, 362, 0.570f, 0.500f);    // R eye inner
            Set(target, 386, 0.585f, 0.491f);    // R eye top
            Set(target, 374, 0.585f, 0.509f);    // R eye bottom

            // Outer brows
            Set(target, 65,  0.42f, 0.38f);      // L outer brow
            Set(target, 295, 0.58f, 0.38f);      // R outer brow

            // Mouth: neutral (corners level with lip midpoint → low tension)
            Set(target, 13,  0.50f, 0.72f);      // upper lip center
            Set(target, 14,  0.50f, 0.74f);      // lower lip center
            Set(target, 61,  0.45f, 0.73f);      // L mouth corner
            Set(target, 291, 0.55f, 0.73f);      // R mouth corner

            _landmarks = target;  // atomic reference swap
        }

        private static void Set(float[] arr, int idx, float x, float y)
        {
            arr[idx * 3]     = x;
            arr[idx * 3 + 1] = y;
        }
    }
}
