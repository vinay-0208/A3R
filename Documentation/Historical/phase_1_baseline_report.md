# AÂ³R Phase 1 Baseline Report

## 1. Date
- **Audit Date:** 2026-09-13
- **Phase:** Phase 1 â€” Project Baseline + Documentation + Configuration Audit
- **Audit Type:** Comprehensive Static & Environment Baseline Audit

## 2. Project Version/Baseline
- **Repository:** AÂ³R (Affect-Adaptive Augmented Reality)
- **Branch/Baseline:** Main development branch
- **Instructional Task:** ESP32-based LED circuit construction (8-step breadboard assembly).
- **Physical Communication:** Android â†” ESP32 communication mechanism pending Phase 9 design/freeze.

## 3. Unity Environment
- **Unity Editor Version:** `6000.3.23f1` (Unity 6.3 LTS), Revision `09d2ecc7fb28`.
- **Render Pipeline:** Built-in / Universal Render Pipeline compatible (Linear color space).
- **Target Platform:** Android (`BuildTarget.Android`).
- **Scripting Backend:** IL2CPP (`ScriptingImplementation.IL2CPP`).
- **Target Architectures:** ARM64 (`arm64-v8a`, target architectures bitmask `2`).
- **API Compatibility Level:** .NET Standard 2.1 / .NET Core.
- **Minimum Android SDK:** API Level 29 (Android 10.0).
- **Target Android SDK:** Automatic / Highest Installed (`API 36` compatible).
- **Active Scene:** `Assets/Scenes/A3R_Main.unity` (GUID `7b5098a5511e56f4cbe8887bd654438c`).
- **Compiler Define Symbols:** `MEDIAPIPE_UNITY_PLUGIN` active via `Assets/csc.rsp`.

## 4. Android Environment
- **Target Device:** OnePlus 13R (Physical hardware).
- **SoC / GPU:** Qualcomm Snapdragon 8 Gen 3 (Qualcomm Adreno GPU).
- **Device RAM:** 16 GB LPDDR5X.
- **Operating System:** Android 16 (API 36 preview/release), ABI `arm64-v8a`.
- **ADB Authorization:** Device serial `97741778` verified in device mode.

## 5. Hardware (Development Workstation)
- **Model:** Lenovo Yoga Pro 7i (2024).
- **CPU:** Intel Core i7-13700H (14 physical cores: 6 P-cores + 8 E-cores, 20 logical threads).
- **System RAM:** 16 GB physical LPDDR5 (13.73 GB visible to Windows, ~2.27 GB hardware-reserved for Intel Iris Xe iGPU).
- **Discrete GPU:** NVIDIA GeForce RTX 4050 Laptop GPU (6,141 MiB GDDR6 VRAM, Driver 616.56, WDDM 3.2, CUDA UMD 13.4).
- **Unity Editor Graphics Binding:** Verified Direct3D 11.0 on NVIDIA GeForce RTX 4050 Laptop GPU (Device ID `0x28a1`).

## 6. Unity Packages
Verified from `unity_project/Packages/manifest.json`:
- `com.github.homuler.mediapipe`: `file:com.github.homuler.mediapipe` (MediaPipe Unity Plugin v0.14.4).
- `com.unity.xr.arfoundation`: `6.3.5` (AR Foundation).
- `com.unity.xr.arcore`: `6.3.5` (Google ARCore XR Plugin).
- `com.unity.xr.management`: `4.6.1` (XR Plugin Management).
- `com.unity.xr.legacyinputhelpers`: `2.1.13`.
- `com.unity.inputsystem`: `1.20.0` (New Input System).
- `com.unity.ugui`: `2.0.0` (Unity UI).
- `com.unity.burst`: `1.8.30`.
- `com.unity.mathematics`: `1.3.3`.
- `com.unity.collections`: `2.6.8`.

## 7. MediaPipe Configuration
- **Package Path:** `unity_project/Packages/com.github.homuler.mediapipe/`.
- **Plugin Version:** `v0.14.4` (Homuler prebuilt package).
- **Native Android AAR:** `mediapipe_android.aar` (14,911,432 bytes) containing `libmediapipe_jni.so` (23.58 MB) and `libopencv_java3.so` (18.23 MB).
- **Native Windows Host Libraries:** `libmediapipe_c.so` (29.36 MB), `mediapipe_c.dll` (14.72 MB).
- **Managed Wrappers:** `Google.Protobuf.dll` (408 KB), `System.Memory.dll`, `System.Buffers.dll`.
- **Model File:** `Assets/StreamingAssets/face_landmarker.task` (exactly 3,758,596 bytes, ~3.76 MB).
- **Execution Mode:** `RunningMode.LIVE_STREAM` via `FaceLandmarker.CreateFromOptions`.
- **Inference Delegate:** GPU Delegate (`GpuDelegate`).
- **Inference Call:** `DetectAsync(image, timestamp)`.
- **Frame Processing Strategy:** Bounded latest-frame-wins single-slot buffer (`_isProcessingFrame` flag drops intermediate frames if inference is busy; 0 dropped frames observed at 30 FPS).
- **Camera Input:** Front camera acquired via `WebCamTexture` (Camera2 Sensor 1, 640Ã—480 @ 30 FPS).

## 8. Affect System
- **Component:** `AffectFeatureExtractor.cs` (static extractor) & `AffectScorer.cs` (orchestrator).
- **Geometric Landmark Features:**
  1. *Brow Furrow Distance:* Landmarks 105 (L inner brow) & 334 (R inner brow), normalized by Inter-Ocular Distance (IOD, landmarks 33 & 263).
  2. *Eye Aspect Ratio (EAR):* 6-point EAR computed for left (159, 145, 133) and right (386, 374, 362) eyes. Min-max normalized from [0.15, 0.35] to [0.0, 1.0].
  3. *Mouth Corner Tension:* Angle of mouth corners (61, 291) relative to lip center (13, 14).
  4. *Brow Height:* Vertical distance from outer brows (65, 295) to eye centers, normalized by total face height (landmarks 10 to 152).
- **Scoring Formulas (Rule-Based):**
  - $\text{Confusion} = 0.40 \cdot \text{BrowFurrow} + 0.30 \cdot (1 - \text{EAR}_{\text{norm}}) + 0.30 \cdot \text{MouthTension}$
  - $\text{Frustration} = 0.35 \cdot \text{BrowFurrow} + 0.25 \cdot (1 - \text{EAR}_{\text{norm}}) + 0.40 \cdot \text{MouthTension}$
  - $\text{Engagement} = 0.50 \cdot \text{EAR}_{\text{norm}} + 0.30 \cdot (1 - \text{BrowFurrow}) + 0.20 \cdot \text{BrowHeight}$
- **Update Frequency:** Coroutine running every 0.5 seconds (2 Hz sampling).

## 9. Prediction System
- **Component:** `AffectPredictor.cs`.
- **Behavior Taxonomy:**
  - *Predictive:*
    1. `ConfusionStruggle` (precursor: brow velocity $v_{\text{brow}} \ge 0.15\text{/s}$ and declining EAR before 3s dwell).
    2. `FrustrationDisengage` (precursor: mouth corner tension acceleration $> 0.20\text{/s}$ after unresolved confusion).
    3. `PolarityHesitation` (precursor: micro-squinting and elevated brow furrow during LED insertion step).
  - *Detection-Only:*
    1. `TrackingLossDisruption` (sudden camera occlusion / marker loss; external to facial geometry).
    2. `HardwareDisconnection` (electrical disconnection; external to facial geometry).
- **Scene Attachment Status:** `AffectPredictor.cs` exists in codebase but is **NOT yet instantiated or attached to any GameObject in `A3R_Main.unity`**.
- **Metric Integrity:** All prediction lead times, F1 scores, and accuracies are currently **SYNTHETIC / PLACEHOLDER**.

## 10. Adaptation System
- **Component:** `AdaptationController.cs`.
- **FSM Architecture:** Dual-threshold dwell timer with escalating action sequence.
- **Action Sequence:**
  1. `InjectCue` (AR arrow highlight).
  2. `InsertHint` (contextual text panel).
  3. `SlowPace` (expanded instructional detail).
  4. `SimplifyStep` (granular sub-step breakdown).
- **Threshold Discrepancy Recorded:**
  - Code default fields: $T_{\text{confusion}} = 0.60$, $T_{\text{frustration}} = 0.65$, $D = 3.0\text{s}$, Cooldown $= 15.0\text{s}$.
  - Scene serialization (`A3R_Main.unity`): $T_{\text{confusion}} = 0.50$, $T_{\text{frustration}} = 0.50$, $D = 2.0\text{s}$, Cooldown $= 5.0\text{s}$.
  - Empirical status: Uncalibrated placeholders (DAiSEE empirical calibration scheduled for Phase 5).

## 11. Session Logging
- **Component:** `SessionLogger.cs`.
- **Log Format:** JSON Lines (JSONL) with UTF-8 encoding.
- **Storage Location (Android):**
  `/storage/emulated/0/Android/data/com.a3rproject.affectivear/files/session_logs/session_yyyyMMdd_HHmmss.jsonl`
- **Record Schema:**
  - `session_start` (device info, session ID, task version, inference mode).
  - `affect` (timestamped 2 Hz affect scores and raw/normalized features).
  - `adaptation` (event ID, action, triggering affect values, dwell duration).
  - `prediction` (behavior, lead time, confidence).
  - `step_change` (step index, step ID, object ID).
  - `session_end` (total duration, sample counts, event totals).

## 12. Scene/Component Configuration
- **Scene File:** `Assets/Scenes/A3R_Main.unity`.
- **Root Hierarchy & MonoBehaviours:**
  1. `XR Origin` (AR Foundation XR Origin, Camera Offset).
  2. `AR Camera` (AR Camera Manager, AR Camera Background, Tracked Pose Driver).
  3. `AR Session` (AR Session, AR Input Manager).
  4. `A3R_Manager`:
     - `ARImageTracker` (Image marker tracking).
     - `ARContentManager` (`taskJsonFile` unassigned in inspector; loads fallback from `Resources/led_blink_task.json`).
     - `FaceLandmarkBridge` (Front camera MediaPipe bridge).
     - `AffectScorer` (2 Hz affect inference loop).
     - `AdaptationController` (Dwell FSM).
     - `SessionLogger` (JSONL logger).
- **Missing Scene Bindings:**
  - `AffectPredictor` is not attached to `A3R_Manager`.
  - UI Canvas elements (`stepPanel`, `hintPanel`, `arCueArrow`) are currently null in inspector (pending Phase 10 UI pass).

## 13. Automated Tests
- **Python Test Suite:** `python_tools/daisee_calibration/`.
  - Command: `pytest -v python_tools/daisee_calibration`
  - Total Tests: 25
  - Passed: **25 / 25 (100%)**
  - Execution Time: 5.17 seconds
  - Coverage: Scorer formulas, weight sums, clamp limits, feature bounds, IOD normalization, JSONL schema validation.
- **Unity C# Compilation:**
  - Command: `Unity.exe -batchmode -quit`
  - Compiler Errors: **0**
  - Assembly Build: `Library/ScriptAssemblies/Assembly-CSharp.dll` verified.

## 14. Android Build
- **Active APK:** `build/android/A3R_debug_20260906_231927.apk`.
- **File Size:** 144,428,502 bytes (~144.4 MB).
- **Package ID:** `com.a3rproject.affectivear`.
- **Verified Internal Contents:**
  - `lib/arm64-v8a/libil2cpp.so` (248 MB uncompressed)
  - `lib/arm64-v8a/libmediapipe_jni.so` (23.58 MB)
  - `lib/arm64-v8a/libopencv_java3.so` (18.23 MB)
  - `lib/arm64-v8a/libunity.so` (97.56 MB)
  - `lib/arm64-v8a/libUnityARCore.so` (728 KB)
  - `assets/face_landmarker.task` (3,758,596 bytes)

## 15. Runtime Evidence
- **Verified Runtime Observations (OnePlus 13R Logcat):**
  - Camera2 logical sensor 1 (front camera) opened at 640Ã—480 @ 30 FPS.
  - MediaPipe Tasks API GPU delegate initialized successfully.
  - Callback loop received frames (`IsSimulated: False`, 0 dropped frames).
  - Callback latency measured at 11.3â€“15.4 ms.
  - Dual-camera pipeline initialized: ARCore tracking rear sensor while WebCamTexture captures front sensor.
  - `SessionLogger` created valid session file in app sandboxed storage.
- **Unverified Runtime Items:**
  - Detection was tested with device facing the ceiling; **no live human face was positioned in the front camera frame**.
  - No real 478-point landmark array was output or converted to non-zero affect scores on mobile hardware.
  - Full ARCore marker tracking of the physical circuit target was not validated.

## 16. Verified Capabilities
1. Zero-error C# compilation with `MEDIAPIPE_UNITY_PLUGIN` define.
2. Complete IL2CPP ARM64 Android packaging with all required native `.so` shared libraries and model tasks.
3. MediaPipe Tasks API GPU delegate initialization on Android 16 hardware.
4. Concurrent front-camera WebCamTexture and rear-camera ARCore subsystem instantiation.
5. Mathematical integrity of rule-based affect scoring and normalization (25/25 unit tests).
6. Non-destructive workstation optimization (startup apps tuned, zero MongoDB dependency confirmed).

## 17. Unverified Capabilities
1. Live on-device face detection returning 478 valid facial landmark coordinates.
2. Real-time affect trajectory calculation driven by live learner facial expressions.
3. Closed-loop FSM adaptation firing content mutations in response to real confusion.
4. ARCore image marker tracking and 3D overlay anchoring.
5. Android â†” ESP32 hardware communication.
6. Empirical DAiSEE threshold calibration.
7. Predictive lead-time evaluation ($t_{\text{lead}} > 0$).

## 18. Known Limitations
1. **Device Orientation during Test:** Previous on-device test did not face a human subject.
2. **Predictor Unbound in Scene:** `AffectPredictor` is uninstantiated in `A3R_Main.unity`.
3. **UI Elements Unassigned:** UI TextMeshPro references on `ARContentManager` are null in inspector.
4. **Duplicate Task File:** `led_blink_task.json` exists in both `Assets/Resources/` and `Assets/StepData/`.
5. **ARCore ML Depth Warnings:** Logcat previously reported ARCore ML depth provider initialization warnings.

## 19. Research Integrity Notes
- All claims of predictive accuracy, precision, recall, and lead time are **SYNTHETIC DEMONSTRATIONS ONLY**.
- All affect thresholds are **FALLBACK VALUES**; no claims of empirical DAiSEE calibration may be made until Phase 5 is executed.
- Previous MediaPipe runtime evidence proves **API initialization and callback receipt**, NOT proven face detection.

## 20. Phase Boundaries & Entry Criteria

### Phase 1 Prerequisites Completed
- [x] Phase 1 baseline report recorded
- [x] Evidence ledger recorded
- [x] Zero C# compilation errors verified
- [x] Existing Android APK verified
- [x] Project documentation synchronized

### Phase 2 Execution Prerequisites (Pending Phase 2 Authorization)
- [ ] OnePlus 13R connected through authorized ADB connection
- [ ] Live human face positioned within front-camera field of view

## 21. Phase 1 Status
**OVERALL STATUS: PASS**  
Phase 1 is frozen. Phase 2 has NOT been started.
The project baseline, configuration, documentation, and research integrity boundaries have been fully established and verified with zero architectural regressions.
