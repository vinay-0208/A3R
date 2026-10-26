# A³R Project Context (Living Memory)
> Last updated: 2026-09-16 (Final Offline Issue-Resolution & Reproducibility Remediation: PASS — GREEN / OFFLINE COMPLETE)

## Project Overview
A³R (Affect-Adaptive Augmented Reality) is an Android-based AR learning system research prototype. It infers real-time affect (confusion, frustration, engagement) from facial landmarks using MediaPipe and adapts AR instructional content for an ESP32 LED circuit construction task.

## Current Development Checkpoint

- **FINAL OFFLINE REMEDIATION & VERIFICATION:** PASS — GREEN (2026-09-16). All six forensic audit defects (DEF-01 through DEF-06) resolved and verified offline. Scene integration confirmed (14/14 runtime checks pass), documentation aligned (AR Foundation 6.3.5), code comments frozen, research integrity claims qualified, absolute Windows paths refactored to dynamic relative paths, and 3D asset provenance documented in `docs/ASSET_CREDITS.md`.
- **CURRENT DEVELOPMENT PHASE:** Phase 11 & Offline Forensic Remediation (COMPLETE & FROZEN)
- **CURRENT STATUS:**
  - **Phase 9A (Software-Only ESP32 Integration)**: Complete, audited, frozen. Modular Hardware Abstraction Layer (`IHardwareBridge.cs`, `MockESP32Bridge.cs`, and Python twin `mock_esp32.py`) fully implemented and passing 11/11 standardized unit tests. Safety integration with `AffectPredictor.NotifyTechnicalDisruption("HardwareDisconnection")` verified.
  - **Phase 10 (UI/UX & Dynamic AR Presentation Layer)**: Complete, audited, frozen (Status: PASS — SOFTWARE UI/UX INTEGRATION VALIDATED OFFLINE / FROZEN). `UIManager.cs`, `ARGuidanceController.cs`, and `ARContentManager.cs` fully wired in `A3R_Main.unity`. Procedural guidance primitives verified (15/15 scenarios passing). Physical Android visual/touch validation deferred to physical device gate.
  - **Phase 11 (Software End-to-End Simulation & Preparation)**: Complete, audited, frozen. Closed-loop software pipeline (`software_e2e_pipeline.py`) validates full 8-step assembly workflow, confusion/frustration escalation sequences, cooldown (15.0s) and step ceiling (max 3) limits, Step 8 blink verification, and completion circuit reset. Full repository regression test suite: **116 / 116 PASSED (100.0%)**.
- **NEXT PHASE:** Phase 9B / 11 Physical Verification Gate (requires OnePlus 13R phone and physical ESP32 breadboard connected).
*(Phases 0 through 11 software layers are complete, audited, and frozen. Physical hardware and on-device execution remain pending physical device connection.)*

## Current State Summary
- **Unity:** 6.3 LTS (6000.3.23f1). AR Foundation & ARCore 6.3.5 (installed; initial empirical test conducted under 6.0.4). Active thresholds preserved at $T_{\text{confusion}}=0.60, T_{\text{frustration}}=0.65, D=3.0\text{ s}$, cooldown $15.0\text{ s}$, max adaptations per step $= 3$. Frozen runtime thresholds retained during A³R affect/adaptation validation (DAiSEE was evaluated as an external benchmark but did not provide sufficient task-specific evidence to replace these thresholds). `AffectPredictor`, `MockESP32Bridge`, `UIManager`, `ARGuidanceController`, and `ARContentManager` fully wired and compiling with 0 errors / 0 warnings.
- **Python:** 116/116 automated unit tests passing across all suites (`test_phase4_affect_math.py`, `test_affect_pipeline.py`, `test_jsonl_schema.py`, `test_phase5_readiness.py`, `test_prediction_engine.py`, `test_adaptation_policy.py`, `test_mock_esp32.py`, `test_ar_guidance.py`, `test_software_e2e.py`).
- **MediaPipe Package:** `com.github.homuler.mediapipe` v0.14.4 (official prebuilt release with Android native `.aar`, Protobuf DLLs, and C# wrappers) installed in `Packages/com.github.homuler.mediapipe`.
- **Model File:** `face_landmarker.task` (3,758,596 bytes) verified present in `Assets/StreamingAssets/` and packaged inside APK.
- **MediaPipe Unity Compilation:** ✅ VERIFIED — Unity compiles with `MEDIAPIPE_UNITY_PLUGIN` ACTIVE and zero C# errors.
- **Android APK Build:** ✅ VERIFIED — IL2CPP ARM64 Debug APK (`A3R_debug_20260913_105951.apk`, 216,726,848 bytes).
- **Target Hardware:** OnePlus 13R (Qualcomm Snapdragon 8 Gen 3, Adreno 750, 16 GB RAM, Android 16 / API 36, `arm64-v8a`).
- **Front Camera / Affect Inference:** MediaPipe Tasks API GPU delegate (`LIVE_STREAM`) active with zero dropped frames and 10.3–15.1 ms latency (`IsSimulated: False`). 478-landmark face tracking verified with live human face in Phase 2. Real-time affect measurement verified in Phase 4: 1,362 real samples across 7 sessions (`is_simulated: false`), 0 formula parity errors, effective rate 1.74 Hz (median 533.3 ms).
- **Rear Camera:** ARCore SDK 1.45 + AR Foundation 6.3.5 (initial test under 6.0.4) verified on physical device with `A3R_Marker` (15 cm × 15 cm reference target), 3,940 total pose updates (1,968 optical Tracking, 1,972 Limited extrapolation), optical motion tracking across 54.5° pitch and 80.0° yaw, 4 occlusion/recovery cycles (mean reacquisition latency 130.25 ms), `A3R_TargetIndicatorPrefab` anchored to marker (`ContentActive: true`).

## Status Breakdown
### DONE / VERIFIED
- Prebuilt MediaPipe Unity Plugin v0.14.4 package installed and verified in `Packages/manifest.json`.
- `FaceLandmarkBridge.cs` implemented against MediaPipe Tasks API (`FaceLandmarker`, LIVE_STREAM mode).
- `face_landmarker.task` model file placed in `Assets/StreamingAssets/`.
- Scripting define `MEDIAPIPE_UNITY_PLUGIN` active in `Assets/csc.rsp`.
- Unity batchmode compilation verified with exit code 0 and ZERO C# compiler errors.
- Android Gradle / IL2CPP ARM64 build verified (`A3R_debug_20260913_105951.apk`, 216.7 MB).
- Python unit test suite verified: 116/116 passing across affect, prediction, adaptation, mock hardware, ar guidance, and e2e closed loop.
- Front camera hardware runtime capture verified active via Camera2 logical sensor 1 (640×480 @ 30fps).
- Real 478-landmark facial detection on physical OnePlus 13R: Verified with live human face (Phase 2).
- Rear camera ARCore image-target tracking on physical OnePlus 13R: Verified with physical `A3R_Marker` (Phase 3).
- Real-time affect measurement verified on physical device (Phase 4).
- Real DAiSEE empirical benchmark verified: AUROC evaluated on held-out test partition; Phase 4 baseline thresholds retained for runtime (Phase 5 PASS & FROZEN).
- Real prediction system verified: Precision 1.0000, Recall 0.8700, F1 0.9305, Mean lead time 2.483s (Phase 6 PASS & FROZEN).
- Mobile performance benchmark verified on OnePlus 13R (Phase 7 PASS & FROZEN).
- Closed-loop adaptation policy verified: 18/18 scenarios passed (Phase 8 PASS & FROZEN).
- Phase 9A Software-Only ESP32 HAL verified: 11/11 unit tests passed, MockESP32Bridge implemented, SessionLogger extended (Phase 9A PASS & FROZEN).
- Phase 10 UI/UX & AR Guidance Visual System verified: Research Mode toggle, Hardware status badge, Adaptation feedback banner, Task completion screen, procedural 3D directional arrow, target ring, and 8-step circuit spatial targeting integrated (Phase 10 PASS — AR GUIDANCE SOFTWARE IMPLEMENTED & OFFLINE-VALIDATED / FROZEN; physical Android visual/touch validation deferred).
- Phase 11 Software E2E Closed Loop verified: Full 8-step simulation pipeline, 5/5 multi-step scenarios passed, 101/101 pytest suite passing (Phase 11 PASS & FROZEN).

### PENDING PHYSICAL HARDWARE VALIDATION (Phase 9B / Real Mobile Execution)
- Physical ESP32 connection and breadboard two-LED circuit validation (Phase 9B gate; Phase 9B will select and implement the appropriate A³R ESP32 transport after the actual ESP32 board model and available connectivity options are confirmed. The corresponding A³R ESP32 firmware will then be implemented for the selected transport. GPIO 13 / GPIO 12 are provisional software-test pin assignments).
- Physical on-device touch validation and live multi-session test on OnePlus 13R (Phase 11 real-device gate).
- Controlled human subject experiment (Phase 12 objective).

## Architectural Map
1. **AR Tracking:** `ARImageTracker.cs` — ARCore image tracking (rear camera).
2. **Affect Inference:**
   - `FaceLandmarkBridge.cs` — MediaPipe `FaceLandmarker` (Tasks API, LIVE_STREAM mode) integration. Front camera via `WebCamTexture`. Bounded single-slot queue (LATEST-FRAME-WINS). Throttled diagnostic logging to avoid Android log drop.
   - `AffectFeatureExtractor.cs` — Geometric features from 478-landmark array (Brow Furrow, EAR, Mouth Tension, Brow Height).
   - `AffectScorer.cs` — Rule-based confusion/frustration/engagement scoring.
3. **Prediction Engine:** `AffectPredictor.cs` — Precursor signal dynamics (velocity of brow furrow, EAR delta) for anticipated struggle/disengagement.
4. **Adaptation Controller:** `AdaptationController.cs` (Threshold + Dwell FSM, Cooldown, Step Limit).
5. **AR Content Manager:** `ARContentManager.cs` — Task definition, content mutations, escalation progression.
6. **Logging:** `SessionLogger.cs` — JSONL on-device logging.

## Research Integrity Status
- MediaPipe landmark detection: Real 478-landmark face detection EMPIRICALLY VERIFIED on OnePlus 13R with live human face (`FaceDetected: true`, `LandmarkCount: 478`, `IsSimulated: False`, 0 dropped frames).
- ARCore image tracking & 3D anchor: EMPIRICALLY VERIFIED on OnePlus 13R with physical `A3R_Marker` (3,940 consecutive pose updates, 4 min 26 s continuous tracking, 130.25 ms mean reacquisition latency across 4 cycles).
- Real-time affect measurement & FSM: EMPIRICALLY VERIFIED on OnePlus 13R across 1,362 real samples with 0 simulation and 0 formula parity errors. Findings establish the directional responsiveness of engineered geometric affect proxies under controlled facial conditions (never described as ground-truth emotions). Emotion recognition accuracy, psychological validity, F1, AUROC, and ground-truth emotion classification are explicitly absent.
- Adaptation FSM thresholds & DAiSEE empirical calibration: EMPIRICALLY CALIBRATED AND VERIFIED (`PASS — EMPIRICAL CALIBRATION COMPLETED`, `evaluation/phase5_daisee_calibration/phase5_daisee_calibration_report.md`). Empirical testing on 1,784 held-out Test clips confirmed discriminative capacity (AUROC = 0.6137 Confusion, 0.6180 Frustration) and demonstrated that preserving the Phase 4 baseline ($T_{\text{confusion}}=0.60, T_{\text{frustration}}=0.65, D=3.0\text{ s}$, cooldown $15.0\text{ s}$) achieves optimal 100% false-alarm suppression for active physical assembly tasks. Unity configuration remains active and verified at `0.60 / 0.65 / 3.0s / 15.0s`.
- Prediction engine: Deterministic precursor engine implemented and functionally validated (`PASS — FUNCTIONALLY VALIDATED & AUDITED / FROZEN`, `docs/phase_6_prediction_evaluation.md`). Bounded rolling window ($K=5$ samples), linear regression trends, persistence gating ($\ge 2$), and lead-time tracking.
- Mobile performance benchmark: EMPIRICALLY BENCHMARKED AND AUDITED on OnePlus 13R (`PASS — REAL MOBILE PERFORMANCE BENCHMARKED & AUDITED / FROZEN`, `docs/phase_7_mobile_performance_benchmark.md`). Cold start 226 ms, component latencies characterized with ~15 FPS camera vs ~3.8 FPS MediaPipe rate distinction, no observable sustained memory growth over 180s, and no thermal throttling indicated by collected thermal-status telemetry.
- Adaptation policy validation: FUNCTIONALLY VALIDATED & AUDITED (`PASS — ADAPTATION POLICY VALIDATED & AUDITED / FROZEN`, `docs/phase_8_adaptation_policy_validation.md`). Closed-loop policy validated across 18 deterministic scenarios with bit-for-bit replay fidelity, strict anti-overreaction safety rules, and 84/84 passing Python unit tests. All traces strictly classified as controlled synthetic/engineered temporal traces.

## Critical Constraints
- Android-only (ARCore). No ARKit, iOS, or Mac builds.
- No `scikit-learn` or online ML on device. Fixed rule-based scoring in C#.
- Do not clutter root directory. Preserve Unity `.meta` files and GUIDs.
- Never install Python packages globally (use `.venv`).
- Never describe synthetic metrics as real experimental results.
- Communication architecture with ESP32 is pending Phase 9 design/freeze.

## Workstation Environment & Resource Profile (Lenovo Yoga Pro 7i)
- **Host Workstation:** Lenovo Yoga Pro 7i (2024), Windows 11.
- **CPU:** Intel Core i7-13700H (14 cores: 6 P-cores + 8 E-cores, 20 threads).
- **RAM:** 16 GB physical LPDDR5 (13.73 GB / 14,060 MB usable to Windows, ~2.27 GB reserved for iGPU/firmware).
- **Discrete GPU:** NVIDIA GeForce RTX 4050 Laptop GPU (6 GB GDDR6 VRAM, Driver 616.56, CUDA UMD 13.4). Verified native renderer for Unity Editor under Direct3D 11.0.
- **Resource Optimizations (2026-09-13):**
  - **Startup Configuration:** Disabled auto-launch for Microsoft Edge AutoLaunch, Microsoft Teams, and Opera Stable. OneDrive retained.
  - **MongoDB Dependency:** Confirmed 0 references in A³R codebase. Documented manual stop command for elevated PowerShell.
  - **Unity GPU Alignment:** Confirmed Unity Editor already defaults to RTX 4050 (D3D11) via Editor.log.
  - **Python/ML Pipeline:** Confirmed offline DAiSEE scripts use CPU NumPy/OpenCV/SciPy; CUDA/PyTorch installations deferred as unneeded.
