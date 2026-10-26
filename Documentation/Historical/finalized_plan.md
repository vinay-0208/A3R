# A³R Authoritative Implementation Roadmap (16-Phase Sequence)

> Current Phase: **PHASE 11 — Software End-to-End Simulation & Preparation (COMPLETE — FROZEN)**  
> Next Phase: **PHASE 9B / 11 Physical Verification Gate (Requires OnePlus 13R & Physical ESP32)**  
> Rule: Complete each phase with empirical evidence before proceeding. GitHub operations occur ONLY after Phase 15.

---

## Authoritative Development Phases

### PHASE 0: Development Environment & Resource Cleanup (COMPLETE — FROZEN)
- [x] Audit laptop CPU/GPU/RAM baseline.
- [x] Disable background autostart for Microsoft Edge AutoLaunch, Teams, Opera.
- [x] Identify and isolate unused services (MongoDB service set to Manual; verified 0 dependency in A³R).
- [x] Verify Unity Editor GPU binding (RTX 4050 D3D11).

### PHASE 1: Project Baseline + Documentation + Configuration Audit (COMPLETE — FROZEN)
- [x] Perform full repository and directory structure audit.
- [x] Audit Unity version (6000.3.23f1) and packages (`manifest.json`: AR Foundation 6.3.5, ARCore 6.3.5, MediaPipe 0.14.4; initial Phase 3 test telemetry under 6.0.4).
- [x] Audit MediaPipe native binaries (`.aar`, `.so`, `.dll`), compiler defines (`-define:MEDIAPIPE_UNITY_PLUGIN`), model file (`face_landmarker.task`, 3.76 MB).
- [x] Audit Affect System, formulas, normalization, and dwell FSM thresholds.
- [x] Audit Prediction System taxonomy (`AffectPredictor.cs`: 3 predictive behaviors, 2 detection-only behaviors).
- [x] Audit Session Logger schema, record structures, and Android log path.
- [x] Audit Scene connections (`A3R_Main.unity`: `XR Origin`, `AR Camera`, `AR Session`, `A3R_Manager`).
- [x] Execute automated tests (Python pytest: 25/25 PASS).
- [x] Inspect existing Android build (`A3R_debug_20260906_231927.apk`, 144.4 MB).
- [x] Terminology alignment: Update Arduino Uno to ESP32 (communication architecture pending Phase 9 design/freeze).
- [x] Generate Phase 1 Baseline Report and Evidence Ledger.
- [x] Final documentation correction and baseline freeze completed.

### PHASE 2: Real MediaPipe 478-Landmark Android Validation (COMPLETE — VERIFIED ON HARDWARE)
**Primary Objective:** Validate real on-device 478 facial landmark output from a human face using the OnePlus 13R front camera and MediaPipe Face Landmarker.
- [x] Connect target physical hardware (OnePlus 13R, Qualcomm Snapdragon 8 Gen 3, Android 16 / API 36).
- [x] Execute front-facing camera validation with a live learner face.
- [x] Empirically capture non-zero 478-landmark arrays on device (verified `LandmarkCount: 478`).
- [x] Verify `IsSimulated == false`, dropped frames (`0`), and inference latency (`19.8–31.2 ms`, mean `23.88 ms`) under active face detection (160 consecutive observations across 42.6s).
- [x] Verify real non-zero affect score calculation from live face features (275 affect records logged to `session_20260913_043203.jsonl`).

### PHASE 3: ARCore / AR Interaction Validation (COMPLETE — VERIFIED ON HARDWARE)
**Primary Objective:** Validate real rear-camera ARCore image-target tracking, 3D content spawning, and tracking stability on the physical Android device.
- [x] Validate rear camera ARCore tracking and image detection with physical reference target (`A3R_Marker`, 15 cm × 15 cm).
- [x] Anchor AR overlay content (`A3R_TargetIndicatorPrefab` / `A3R_ContentRoot`) to physical marker target (`ContentActive: true`).
- [x] Verify continuous pose tracking: 3,940 total updates (1,968 full Tracking, 1,972 Limited extrapolation) over 4 min 26.4 s.
- [x] Verify 6-DoF pose stability across dynamic ±20° tilt/pan movement test (measured optical span: Δ=54.5° pitch, Δ=80.0° yaw).
- [x] Verify occlusion handling (`Event: LOST`) and fast reacquisition recovery (mean latency 130.25 ms across 4 cycles).
- [x] Verify coexistence of concurrent front-camera MediaPipe GPU delegate execution (10.3–15.1 ms latency, 0 dropped frames) and rear-camera ARCore tracking.

### PHASE 4: Affect Measurement Validation (COMPLETE — VERIFIED ON HARDWARE)
**Primary Objective:** Validate the real affect-measurement pipeline (landmarks → geometric features → affect scores → temporal dwell FSM) on physical Android hardware.
- [x] Audit feature extraction math, EAR normalization ($[0.15, 0.35] \to [0.0, 1.0]$), heuristic weight sums ($1.00$), and dwell timing logic.
- [x] Execute automated unit tests (41/41 passing pytest suite) verifying formula parity, output boundedness $[0.0, 1.0]$, directional monotonicity, and FSM mechanics.
- [x] Collect live telemetry on OnePlus 13R (1,362 real affect samples across 7 sessions, 0 simulated samples).
- [x] Verify exact formula parity on physical hardware ($0/1,362$ discrepancies $> 10^{-3}$).
- [x] Observe controlled facial state transitions (Neutral, Brow-Furrow, Mouth Tension, Recovery).
- [x] Verify real-time temporal dynamics: 1.74 Hz effective rate (median 533.3 ms, P95 546.9 ms) without queue congestion or frame drops.
- [x] Export raw telemetry to `evaluation/phase4_affect_measurements.jsonl` and `evaluation/phase4_affect_diag_raw.txt`.
- [x] Perform configuration consistency audit: confirmed actual thresholds across project, preserved intentional heuristic baseline ($T_{\text{confusion}}=0.60, T_{\text{frustration}}=0.65, D=3.0\text{ s}$), and synchronized `A3R_Main.unity` serialized fields to match.

### PHASE 5: Real DAiSEE Calibration (PASS — AUDITED / EMPIRICAL BENCHMARK COMPLETE)
- [x] Audit extraction, split isolation, temporal FSM simulation, and $F_{0.5}$ metric code (`load_daisee.py`, `batch_extract.py`, `calibrate_thresholds.py`, `phase5_calibrate.py`).
- [x] Verify complete test suite (51/51 passing tests across `test_phase5_readiness.py`, `test_affect_pipeline.py`, and `test_calibration_metrics.py`).
- [x] Locate authentic DAiSEE archive (`DAiSEE.zip`, 15.33 GB, SHA-256 `e2b4db9c...`) and extracted directory (`C:\Dataset\DAiSEE`, 15.48 GB, 9,067 video clips across 113 filesystem subject directories; official `AllLabels.csv` covers 112 unique subjects).
- [x] Perform dataset verification audit: 8,570/8,571 clips matched to labels (99.988%), strict subject independence (0 subject leakage), 100% video decoding integrity via OpenCV.
- [x] Resolve subject-count discrepancy: 112 labeled subjects in official metadata (8,925 rows in `AllLabels.csv`; 8,571 across split CSVs) vs 113 physical subject directories on disk; identified subject `998826` in `DataSet/Test/998826` (86 clips, female contributor) present on disk and in `Test.txt` but omitted from `AllLabels.csv`/`TestLabels.csv`; safely quarantined as unlabeled without fabricating annotations.
- [x] Verify subject split disjointness: Train (69), Validation (19), Test (21) are 100% disjoint (0 subject intersection between all pairs).
- [x] Document missing video handling: `Train/210055/2100552061` (empty directory on disk) excluded without data fabrication.
- [x] Verify formula parity: evaluated 10,000 synthetic vectors across Python and C# `Mathf.Clamp01` affect implementations; max discrepancy $0.00\times 10^0$ (exact bit-level parity with frozen Phase 4 equations).
- [x] Execute empirical feature extraction on authentic video frames: processed 4,028 video clips (201,728 frames) across Train exploration (815), Validation (1,429), and Test (1,784) partitions with MediaPipe 478 landmarks at 5 fps (99.95% face detection success).
- [x] Perform Validation-stage grid search over 112 combinations ($T \in [0.40..0.75]$, $D \in [1.0..4.0\text{ s}]$): selected $T=0.45, D=2.0\text{ s}$ for Confusion; preserved $T=0.65, D=3.0\text{ s}$ for Frustration.
- [x] Evaluate on held-out Test partition (1,784 clips, 20 labeled subjects): verified AUROC ($0.6137$ Confusion, $0.6180$ Frustration) against independent Mann-Whitney U cross-check (discrepancy $\le 1.11 \times 10^{-16}$).
- [x] Evaluate subject-clustered uncertainty: 1,000-iteration cluster bootstrap yields 95% CIs: Confusion $[0.5055, 0.6867]$, Frustration $[0.5011, 0.6986]$.
- [x] Hardware recommendation & domain mismatch: DAiSEE passive video viewing did not establish a superior runtime threshold for active physical AR breadboard assembly ($\Delta F_{0.5} = 0.0000$ on Test). Retained frozen runtime thresholds ($T_{\text{confusion}}=0.60, T_{\text{frustration}}=0.65, D=3.0\text{ s}$, cooldown $15.0\text{ s}$, max adaptations per step $= 3$) during A³R validation (DAiSEE was evaluated as external benchmark but did not provide sufficient task-specific evidence to replace them).
- [x] Document calibration results in `evaluation/phase5_daisee_calibration/phase5_daisee_calibration_report.md` (**PASS — AUDITED / EMPIRICAL BENCHMARK COMPLETE**).

### PHASE 6: Real Prediction System (COMPLETE — FROZEN)
- [x] Integrate `AffectPredictor.cs` into active scene (`A3R_Main.unity` on `A3R_Manager`) and connect to `ARContentManager` and `SessionLogger`.
- [x] Extend `SessionLogger.cs` with full schema (`is_simulated`, `is_technical_disruption`, `trend_slope`, `evidence_score`, `lead_time_seconds`).
- [x] Implement deterministic, rule-based precursor dynamics over bounded rolling window ($K=5$ samples, $\Delta t \approx 2.5\text{ s}$) in `predict_affect.py` and `AffectPredictor.cs`.
- [x] Classify 5 states: `ConfusionStruggle`, `FrustrationDisengage`, `PolarityHesitation` (behavioral predictions), `TrackingLossDisruption`, and `HardwareDisconnection` (technical disruptions).
- [x] Implement and verify persistence gating ($\ge 2$ samples), recovery floor ($0.30$), and cooldown suppression ($5.0\text{ s}$).
- [x] Implement lead-time measurement infrastructure ($t_{\text{lead}} = t_{\text{actual}} - t_{\text{pred}} > 0$).
- [x] Execute automated unit tests covering conditions A through O (15/15 PASS in `test_prediction_engine.py`; 66/66 overall).
- [x] Execute evaluation across 180 controlled engineered temporal sequences: Precision 1.0000, Recall 0.8700, F1 0.9305, Specificity 1.0000, Mean evaluation anticipation interval 2.483s (characterizes rule engine under scripted conditions; human empirical lead time not established).
- [x] Verify Unity batchmode compilation with 0 C# compiler errors.
- [x] Preserve Phase 4/5 thresholds ($0.60 / 0.65 / 3.0\text{ s}$, cooldown $15.0\text{ s}$, max adaptations 3).
- [x] Document complete evaluation report in `docs/phase_6_prediction_evaluation.md` and `evaluation/phase6_prediction/phase6_prediction_report.md`.

### PHASE 7: Real Mobile Performance Benchmark (COMPLETE — FROZEN)
- [x] Complete Phase 7 Readiness Audit across items A–L.
- [x] Implement lightweight non-intrusive in-engine diagnostics (`AffectPredictor` timing, `AffectScorer` 5s `[A3R_BENCHMARK]` periodic aggregator).
- [x] Build and deploy Android APK with ARM64 and IL2CPP (`A3R_debug_20260913_203030.apk`, 216.8 MB) to OnePlus 13R over ADB.
- [x] Execute benchmark protocol across Runs A through F on physical target hardware:
  - Run A (Baseline/Startup, 30s): Cold-start launch measured at 226.0 ms, PSS 528.3 MB.
  - Run B (Front Camera + MediaPipe Face Tracking, 60s): MediaPipe latency 11.27 ms, AffectScorer latency 0.083 ms, Predictor latency 0.025 ms, 0 queue frame drops.
  - Run C (Sustained Face Tracking & Affect Inference, 120s): 100% face detection stability, MediaPipe latency 23.30 ms, PSS 593.0 MB, no observable sustained memory growth.
  - Run D (Rear AR Marker Tracking, 60s): MediaPipe latency 25.97 ms, ARCore image tracking continuous pose updates, PSS 590.3 MB.
  - Run E (Combined Dual-Camera Pipeline, 60s): Front WebCamTexture and rear ARCore run concurrently with 171 AR updates recorded and no observed application frame-drop indicators.
  - Run F (Sustained 180s Thermal Soak, 180s): 3-minute continuous stress test, PSS stable at 582.6 MB, battery temp delta +0.9°C (35.9°C -> 36.8°C), no thermal throttling indicated by thermal-status telemetry.
- [x] Generate comprehensive 22-section performance benchmark report in `docs/phase_7_mobile_performance_benchmark.md` and `evaluation/phase7_mobile_performance/`.

### PHASE 8: Adaptation Policy Validation (COMPLETE — FROZEN)
- [x] Audit Unity architecture and scene graph (`A3R_Main.unity` components `300000003` through `300000010`).
- [x] Enhance `AdaptationController.cs`, `ARContentManager.cs`, and `SessionLogger.cs` with defensive NaN/Inf guards, suppression counters, dynamic behavior mapping, and clean session reset.
- [x] Verify Unity batchmode compilation with 0 C# compiler errors.
- [x] Implement Python deterministic simulation twin (`adaptation_policy.py`).
- [x] Validate 18 standardized policy scenarios (TEST_A through TEST_R) in `test_adaptation_policy.py` (18/18 PASS).
- [x] Verify anti-overreaction dynamics: 3.0s dwell gating, 15.0s cooldown lock out, and max 3 adaptations per step ceiling.
- [x] Verify step transition resets and tracking loss technical disruption isolation.
- [x] Verify bit-for-bit deterministic replay and telemetry logging schema completeness.
- [x] Run full pytest suite: 84/84 tests passing.
- [x] Document comprehensive validation report in `docs/phase_8_adaptation_policy_validation.md` and `evaluation/phase8_adaptation/`.

### PHASE 9: ESP32 Integration (Phase 9A COMPLETE — FROZEN; Phase 9B PENDING PHYSICAL DEVICE)
- [x] Phase 9A: Implement modular transport-agnostic Hardware Abstraction Layer (`IHardwareBridge.cs`, `HardwareTypes.cs`).
- [x] Phase 9A: Implement two-LED circuit protocol: `Led1` (provisional GPIO 13 circuit target) and `Led2` (provisional GPIO 12 secondary alert). Note: GPIO 13 / GPIO 12 are provisional software-test pin assignments. Final physical GPIO mapping will be confirmed against the actual ESP32 board model during Phase 9B.
- [x] Phase 9A: Implement `MockESP32Bridge.cs` in C# and deterministic twin `mock_esp32.py` with 5–25ms latency, 1.0s heartbeat, 3.0s timeout, auto-recovery, and fault injection.
- [x] Phase 9A: Implement and pass 11/11 standardized hardware unit tests (`test_mock_esp32.py`).
- [x] Phase 9A: Extend `SessionLogger.cs` with hardware record schema (`is_mock: true`).
- [ ] Phase 9B: Physical Android ↔ ESP32 communication on real breadboard (pending physical hardware). Phase 9B will select and implement the appropriate A³R ESP32 transport after the actual ESP32 board model and available connectivity options are confirmed. The corresponding A³R ESP32 firmware will then be implemented for the selected transport.

### PHASE 10: UI/UX + 3D AR Content (COMPLETE — SOFTWARE UI/UX INTEGRATION VALIDATED OFFLINE / FROZEN)
- [x] Implement Research Mode vs. Learner Mode toggle in `UIManager.cs`.
- [x] Implement real-time Hardware Connection Status Badge (`Connected [MOCK]`, `Connecting`, `Disconnected`, `Timeout`, `Error`).
- [x] Implement floating Adaptation Feedback Banner for non-intrusive cognitive feedback.
- [x] Implement Step Progress indicator and Task Completion Screen with total elapsed time.
- [x] Wire `UIManager` and `IHardwareBridge` into `ARContentManager.cs` (StepSync, Step 8 BlinkLED, completion ResetCircuit).
- [x] Integrate 3D instructional circuit assets (`assets_3d/sourced/`) and dynamic AR arrow positioning.
- [x] Verify Unity 6000.3.23f1 batchmode compilation with 0 C# compiler errors and 0 warnings.

### PHASE 11: Full Android End-to-End Preparation & Validation (Phase 11 Software COMPLETE — FROZEN; Physical Device Gate PENDING)
- [x] Phase 11 Software: Implement closed-loop simulation pipeline `software_e2e_pipeline.py`.
- [x] Phase 11 Software: Execute multi-step validation scenarios across all 8 assembly steps (5/5 PASS in `run_phase11_evaluation.py`).
- [x] Phase 11 Software: Execute full regression test suite: **101 / 101 PASSED (100.0%)** across all project phases.
- [ ] Phase 11 Physical Device Gate: Deploy and execute full 8-step assembly session with live learner on OnePlus 13R with physical ESP32 connected (pending physical device connection).

### PHASE 12: Controlled Experiment
- [ ] Execute formal user study comparing Static AR vs. Affect-Adaptive AR conditions.

### PHASE 13: Statistical Analysis
- [ ] Run statistical tests (ANOVA / t-tests) on completion time, error rate, cognitive load, and affect trajectories.

### PHASE 14: Reproducibility & Research Documentation
- [ ] Document complete build steps, dependencies, datasets, and analysis scripts.

### PHASE 15: Final Evidence Gate
- [ ] Final audit of all experimental data, logs, and artifacts before any GitHub release/commit operations.


### FORENSIC AUDIT & OFFLINE REMEDIATION (COMPLETE — GREEN / FROZEN)
- [x] DEF-01 Scene Integration Fix: Instantiated and serialized `UIManager`, `Canvas/HUD`, `ARGuidanceController`, and `MockESP32Bridge` in `A3R_Main.unity`. Runtime scene-load verified in Unity batchmode with 14/14 checks passing.
- [x] DEF-02 Version Alignment: Aligned documentation and manifest citations to AR Foundation 6.3.5 / ARCore 6.3.5.
- [x] DEF-03 Code Comment Cleanliness: Documented frozen runtime thresholds retained during validation (0.60 / 0.65 / 3.0s; DAiSEE evaluated as external benchmark but did not replace baseline) and purged obsolete M3 placeholder comments from `AdaptationController.cs`.
- [x] DEF-04 Claims Qualification: Replaced ungrounded 84% headroom claim with measured 116.5%–125.9% CPU utilization; qualified head movement as observational test envelope (approximately ±35° yaw / ±25° pitch without instrumented angular bounds).
- [x] DEF-05 Reproducibility & Cross-Platform Paths: Removed hardcoded Windows absolute paths; refactored to dynamic repository-relative paths.
- [x] DEF-06 3D Asset Provenance: Documented placeholder GLB status and licensing disclaimers in `docs/ASSET_CREDITS.md`.
- [x] Full Verification Matrix: 116/116 Pytest PASS, 15/15 AR Guidance PASS, 5/5 Phase 11 E2E PASS, Unity 0 errors / 0 warnings, 14/14 Scene Runtime checks PASS.

