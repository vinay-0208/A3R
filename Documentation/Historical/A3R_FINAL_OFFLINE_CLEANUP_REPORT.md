# A³R: Final Offline Issue-Resolution & Reproducibility Cleanup Report
## Complete Forensic Remediation & Offline Readiness Certification

**Document Version:** 1.0.0  
**Audit & Remediation Date:** 2026-09-16  
**Auditor / Engineer:** Senior Software Engineer + Research-Methodology Auditor + Unity/Android Reviewer  
**Target Device Profile:** OnePlus 13R (Qualcomm Snapdragon 8 Gen 3, Android 16 / API 36, `arm64-v8a`)  
**Overall Final Verdict:** **GREEN — ALL OFFLINE DEFECTS RESOLVED; 100% TEST PASS; READY FOR PHASE 9B PHYSICAL GATE**

---

## 1. Executive Summary & Final Offline Verdict

This report certifies the successful execution and verification of the **Final Offline Issue-Resolution & Reproducibility Cleanup Pass** for the **A³R (Affect-Adaptive Augmented Reality)** project.

All six defects identified during the forensic audit (**DEF-01 through DEF-06**) have been completely resolved, verified against strict research-integrity standards, and audited offline. Zero changes were made to frozen affect math, prediction thresholds, adaptation policies, or mobile benchmark data.

```text
================================================================================
FINAL OFFLINE VERDICT: GREEN
================================================================================
- DEF-01 (Unity Scene Integration):         RESOLVED (14/14 Runtime Checks PASS)
- DEF-02 (AR Foundation Package Versions):  RESOLVED (Aligned to 6.3.5)
- DEF-03 (Stale M3 Code Comments):          RESOLVED (Frozen Baseline Citations)
- DEF-04 (Research Integrity Claims):       RESOLVED (Qualified Telemetry)
- DEF-05 (Hardcoded Windows Paths):         RESOLVED (Dynamic Relative Paths)
- DEF-06 (3D Assets Provenance):            RESOLVED (docs/ASSET_CREDITS.md)
--------------------------------------------------------------------------------
- Pytest Unit Tests:                        116 / 116 PASSED (100.0%)
- AR Guidance Verification Scenarios:       15 / 15 PASSED (100.0%)
- Phase 11 Software E2E Integration:        5 / 5 PASSED (100.0%)
- Unity 6000.3.23f1 Batchmode Compile:      0 Errors / 0 Warnings
- Unity Scene Runtime Initialization:       14 / 14 Checks PASSED
================================================================================
```

---

## 2. Remediation of DEF-01 (Unity Scene Integration & Hierarchy Resolution)

- **Defect Identified:** `A3R_Main.unity` previously failed to instantiate `UIManager`, `ARGuidanceController`, `MockESP32Bridge`, and the `A3R_HUD_Canvas` hierarchy. At runtime, `ARContentManager` fell back to `FindFirstObjectByType`, which returned `null`, rendering the UI HUD, AR cues, and mock hardware bridge non-functional in-scene.
- **Remediation Implemented:**
  1. Built and executed `SceneSetupHelper.cs` to instantiate `A3R_HUD_Canvas` with all required child elements:
     - `ResearchModePanel` (with research mode toggle and status text)
     - `HardwareBadge` (status indicator with dynamic color coding)
     - `AdaptationBanner` (text display for cue/hint notifications)
     - `StepProgressText` (dynamic step counter)
     - `TaskCompletionPanel` (reset button and completion statistics)
  2. Attached `UIManager` to `A3R_HUD_Canvas` and linked all child references.
  3. Attached `ARGuidanceController` and `MockESP32Bridge` to `A3R_Manager`.
  4. Explicitly serialized direct component references on `ARContentManager`:
     - `uiManager` $\to$ `UIManager`
     - `hardwareBridge` $\to$ `MockESP32Bridge`
     - `guidanceController` $\to$ `ARGuidanceController`
  5. Configured `ARGuidanceController` material properties (`#FFD700` emissive yellow arrow, `#00E5FF` cyan target ring).
- **Verification:** Automated Unity batchmode test `SceneSetupHelper.VerifySceneRuntimeInitialization` executed with exit code 0; all 14 scene checks passed.

---

## 3. Remediation of DEF-02 (AR Foundation / ARCore Version Alignment)

- **Defect Identified:** Historical validation report `phase_3_arcore_validation_report.md` cited AR Foundation and ARCore XR Plugin `v6.0.4`, whereas `Packages/manifest.json` and `packages-lock.json` explicitly lock `v6.3.5`.
- **Remediation Implemented:**
  1. Updated `docs/phase_3_arcore_validation_report.md` (lines 8, 39, 40) to cite `6.3.5` as the installed package version, while maintaining the historical context that initial empirical test telemetry (`phase3_ar_diag_raw.txt`) was captured under `v6.0.4` prior to package consolidation.
  2. Synchronized `docs/context.md` (lines 19, 27) and `docs/finalized_plan.md` (line 19) to reflect `6.3.5 (installed; initial empirical test conducted under 6.0.4)`.
- **Verification:** Version references across documentation and package manifests are now consistent and transparent.

---

## 4. Remediation of DEF-03 (AdaptationController Comments & Parameter Clarification)

- **Defect Identified:** `AdaptationController.cs` retained obsolete comments `// placeholder — replace after M3` adjacent to fields `confusionThreshold`, `frustrationThreshold`, and `dwellRequiredSeconds`.
- **Accurate Baseline Specification:** Confusion threshold = 0.60, Frustration threshold = 0.65, and Dwell = 3.0s are frozen runtime thresholds retained during A³R affect/adaptation validation. DAiSEE was evaluated as an external benchmark but did not provide sufficient task-specific evidence to replace these thresholds.
- **Remediation Implemented:**
  1. Replaced line 27 with `// Frozen runtime threshold retained during validation`.
  2. Replaced line 31 with `// Frozen runtime threshold retained during validation`.
  3. Replaced line 34 with `// Frozen dwell time retained during validation`.
  4. Updated class docstring and header attributes to document that these are frozen runtime thresholds retained during validation, noting that DAiSEE was evaluated as an external benchmark but did not provide sufficient task-specific evidence to replace them.
- **Verification:** No occurrences of `placeholder` or `replace after M3` remain in the codebase.

---

## 5. Remediation of DEF-04 (Research Integrity & Claims Qualification)

- **Defect Identified:**
  1. `docs/phase_7_mobile_performance_benchmark.md` line 359 claimed: *"Over 84% of the Snapdragon 8 Gen 3 compute capacity remains unutilized"*, which simplified multi-core telemetry into an unweighted single figure.
  2. `docs/phase_7_mobile_performance_benchmark.md` line 332 and `docs/phase_2_mediapipe_validation.md` line 110 asserted mesh stability across $\pm 35^\circ$ yaw and $\pm 25^\circ$ pitch without raw angle-telemetry logs.
- **Remediation Implemented:**
  1. Replaced line 359 of `docs/phase_7_mobile_performance_benchmark.md` with accurate measured multi-core telemetry:
     > *"The measured $116.5\% – 125.9\%$ aggregate CPU utilization across the 8-core cluster (equivalent to $\sim 1.25$ cores of the theoretical $800\%$ multi-core capacity) leaves ample multi-core headroom, ensuring that background system tasks, audio services, and AR graphics processing do not contend with affective prediction."*
  2. Qualified head-movement assertions in `docs/phase_7_mobile_performance_benchmark.md` line 332:
     > *"The prior manual movement protocol included approximately $\pm 35^\circ$ yaw and $\pm 25^\circ$ pitch as an observational test envelope; dedicated head-pose telemetry was not recorded, so these values should not be interpreted as instrumented angular performance bounds."*
  3. Qualified dynamic behavioral testing in `docs/phase_2_mediapipe_validation.md` lines 110–111:
     > *"Head Movement (Observational Test Envelope): NOT INSTRUMENTED WITH SYNCHRONIZED HEAD-POSE TELEMETRY. The prior manual movement protocol included approximately $\pm 35^\circ$ yaw and $\pm 25^\circ$ pitch as an observational test envelope; dedicated head-pose telemetry was not recorded, so these values should not be interpreted as instrumented angular performance bounds."*
- **Verification:** All mobile performance and tracking stability claims are strictly qualified and aligned with logged raw data.

---

## 6. Remediation of DEF-05 (Hardcoded Paths & Tool Reproducibility)

- **Defect Identified:** Multiple Python calibration and prediction scripts used hardcoded Windows paths (`C:\Fall Semister\...`), preventing cross-platform execution or execution from different checkout locations.
- **Remediation Implemented:**
  1. Refactored `python_tools/prediction/run_phase6_evaluation.py` to construct paths dynamically via `Path(__file__).resolve().parents[2]`.
  2. Refactored `python_tools/daisee_calibration/run_empirical_calibration.py` to resolve project root and output paths relative to `__file__`, with `DAISEE_ROOT` environment variable support.
  3. Refactored `python_tools/daisee_calibration/load_daisee.py` to check `os.environ.get("DAISEE_ROOT")`, then local repository paths, with clear error messaging if dataset is absent.
  4. Refactored `python_tools/daisee_calibration/test_phase5_readiness.py` to resolve repository paths dynamically.
- **Verification:** `pytest -v python_tools/` executed cleanly (116/116 passed). `run_phase6_evaluation.py` executed successfully, reproducing exact $F1=0.9305$ and lead time $2.483\text{ s}$.

---

## 7. Remediation of DEF-06 (3D Assets Provenance & Procedural Architecture)

- **Defect Identified:** External 3D model files in `assets_3d/sourced/` (`arduino_uno.glb`, `breadboard.glb`, `jumper_wire.glb`, `resistor.glb`) were minimal placeholder headers ($\sim 664–672\text{ bytes}$) without clear licensing documentation.
- **Remediation Implemented:**
  1. Created `docs/ASSET_CREDITS.md` cataloging each file, its size, intended component, and provenance status.
  2. Added mandatory legal disclaimer: *"Source/license provenance not yet established; attribution must be verified before external/public distribution."*
  3. Clarified that the Unity runtime scene does not load these GLB files. All guidance visuals (directional arrows, target rings, component highlights) are procedurally constructed in C# via `ProceduralMeshUtility.cs`.
- **Verification:** `docs/ASSET_CREDITS.md` verified present and comprehensive.

---

## 8. DAiSEE Scope Reconciliation & Operational Boundaries

The documentation has been audited to ensure consistent differentiation across all four DAiSEE dataset operational scopes:

| Scope Level | Description | Item Count | Subject Count | Role / Significance in A³R |
|---|---|---|---|---|
| **Scope 0** | Full Filesystem Inventory | 9,067 raw video files | 113 subject directories | Raw uncompressed dataset directory on disk (`C:\Dataset\DAiSEE`). The "113 subjects" refers to physical filesystem subject directories/material on disk, not the official labeled-subject count. |
| **Scope 1** | Official Supervised Annotations (`AllLabels.csv`) | 8,925 annotated rows | 112 unique subjects | Ground-truth labels in official `AllLabels.csv` covering exactly 112 unique subject prefixes. The partitioned split CSVs (`TrainLabels.csv`, `ValidationLabels.csv`, `TestLabels.csv`) comprise 8,571 labeled records (5,358 Train, 1,429 Validation, 1,784 Test). Subject directory `998826` on disk contains 86 valid Test clips that are omitted from `AllLabels.csv`/`TestLabels.csv` and are therefore treated as supplemental/unlabeled/quarantined. |
| **Scope 2** | Feature-Extracted Subset | 4,028 clips (201,728 frames) | 112 labeled + 1 unannotated | Extracted at 5.0 fps via MediaPipe Face Landmarker (99.95% face detection rate): 1,429 Validation (22 subjects), 1,784 Test (20 labeled subjects), and 815 Train exploration (70 subjects). |
| **Scope 3** | Held-Out Benchmark Partition | 1,784 clips (89,481 frames) | 20 labeled subjects | Held-out test partition used for external AUROC benchmark evaluation (Confusion AUROC $= 0.6137$, Frustration AUROC $= 0.6180$). |

---

## 9. Frozen Affect Formulas, Thresholds & Policy Invariants

The mathematical and algorithmic foundation of A³R remains completely frozen and identical across Python and Unity C#:

### A. Affect Scoring Equations (Exact Parity: $0.00 \times 10^0$ Error)
$$\text{Confusion} = 0.40 \cdot \text{brow\_furrow} + 0.30 \cdot (1 - \text{EAR}) + 0.30 \cdot \text{mouth\_tension}$$
$$\text{Frustration} = 0.35 \cdot \text{brow\_furrow} + 0.25 \cdot (1 - \text{EAR}) + 0.25 \cdot \text{jaw\_clench\_proxy} + 0.15 \cdot (1 - \text{smile\_proxy})$$
$$\text{Engagement} = 0.50 \cdot \text{EAR} + 0.30 \cdot (1 - \text{brow\_furrow}) + 0.20 \cdot \text{brow\_height}$$

### B. Prediction Engine Hyperparameters
- Window Size $K = 5$ frames (minimum 3 frames required)
- Time-delta guard $\Delta t \ge 0.4\text{ s}$
- Persistence counter: 2 consecutive intervals
- Cooldown: $5.0\text{ s}$
- Precursor Recovery Floor: $0.30$

### C. Closed-Loop Adaptation Policy (Phase 8 Frozen)
- Confusion Activation Threshold: $T_{\text{confusion}} = 0.60$
- Frustration Activation Threshold: $T_{\text{frustration}} = 0.65$
- Dwell Time Required: $D = 3.0\text{ s}$  
  *(Frozen runtime thresholds retained during A³R affect/adaptation validation. DAiSEE was evaluated as an external benchmark but did not provide sufficient task-specific evidence to replace these thresholds.)*
- Adaptation Cooldown: $15.0\text{ s}$
- Maximum Adaptations per Step: 3
- Mapping Matrix:
  - Confusion $\to$ `InjectCue` (occurrence 1) $\to$ `InsertHint` (occurrence 2) $\to$ `SimplifyStep` (occurrence 3)
  - Frustration $\to$ `SlowPace` (occurrence 1) $\to$ `SimplifyStep` (occurrence 2) $\to$ `SimplifyStep` (occurrence 3)
  - Engagement $\to$ `ReturnToNormalGuidance` (resets step difficulty)

---

## 10. Hardware Abstraction Layer & Provisional Pinout Architecture

1. **Hardware Bridge Architecture:** `IHardwareBridge.cs` defines transport-agnostic interface (`ConnectAsync`, `DisconnectAsync`, `SendCommandAsync`, `IsConnected`). `MockESP32Bridge.cs` provides offline software simulation with simulated latency and fault injection.
2. **Provisional GPIO Pinout:**
   - **LED1 (Green LED):** `GPIO 13` (provisional software-test assignment)
   - **LED2 (Red LED):** `GPIO 12` (provisional software-test assignment)
   - Final pinouts remain explicitly deferred until physical ESP32 board model is provided in Phase 9B.
3. **Safety Integration:** Heartbeat timeout or connection failure immediately triggers `AffectPredictor.NotifyTechnicalDisruption("HardwareDisconnection")`, suppressing invalid affect triggers.

---

## 11. UI/UX Hierarchy & Scene Wiring Verification

All 14 programmatic checks executed by `SceneSetupHelper.VerifySceneRuntimeInitialization` passed:
1. `XR Origin` present: **PASS**
2. `AR Camera` present: **PASS**
3. `AR Session` present: **PASS**
4. `ARContentManager` on `A3R_Manager`: **PASS**
5. `AffectPredictor` on `A3R_Manager`: **PASS**
6. `ARGuidanceController` on `A3R_Manager`: **PASS**
7. `MockESP32Bridge` on `A3R_Manager`: **PASS**
8. `A3R_HUD_Canvas` present: **PASS**
9. `UIManager` on `A3R_HUD_Canvas`: **PASS**
10. `EventSystem` present: **PASS**
11. `UIManager` child references wired: **PASS**
12. `ARContentManager.UIManager` reference wired: **PASS**
13. `ARContentManager.HardwareBridge` reference wired: **PASS**
14. `ARContentManager.ARGuidanceController` reference wired: **PASS**

---

## 12. AR Guidance Visual Generation & Scenario Verification

All 15 AR Guidance verification scenarios (`python_tools/guidance/test_ar_guidance.py`) passed:
- `test_01_step_1_guidance_appears`: **PASS**
- `test_02_step_transitions_update_target`: **PASS**
- `test_03_arrow_points_toward_target`: **PASS**
- `test_04_target_ring_activates`: **PASS**
- `test_05_guidance_hides_when_target_invalid`: **PASS**
- `test_06_tracking_loss_suppresses_guidance`: **PASS**
- `test_07_tracking_recovery_restores_guidance`: **PASS**
- `test_08_confusion_inject_cue_changes_presentation`: **PASS**
- `test_09_confusion_insert_hint_changes_presentation`: **PASS**
- `test_10_frustration_slow_pace_changes_presentation`: **PASS**
- `test_11_frustration_simplify_step_isolates_target`: **PASS**
- `test_12_engagement_returns_to_normal_guidance`: **PASS**
- `test_13_completion_clears_guidance`: **PASS**
- `test_14_no_duplicate_guidance_objects`: **PASS**
- `test_15_provisional_gpio_wording_preserved`: **PASS**

---

## 13. End-to-End Software Closed-Loop Integration

All 5 Phase 11 closed-loop software scenarios (`python_tools/e2e/test_software_e2e.py`) passed:
- Scenario 1 (Nominal Workflow): Steps 1 through 8 complete with 0 adaptations; final verification pulse received; circuit resets cleanly.
- Scenario 2 (Confusion Escalation): Step 3 confusion escalation triggers `InjectCue`, cooldown enforces $15.0\text{ s}$ gate, and max adaptation ceiling (3) is respected.
- Scenario 3 (Frustration & Disengagement): Step 7 frustration triggers `SlowPace`; precursor recovery floor ($0.30$) resets prediction state.
- Scenario 4 (Hardware Disruption): Disconnection injects `HardwareDisconnection` disruption; affect predictions gated until reconnection.
- Scenario 5 (Affect Invariant Verification): All 8 steps preserve mathematical formulas, normalization bounds, and clamped outputs.

---

## 14. Complete Verification Test Matrix

```text
================================================================================
A³R VERIFICATION SUITE RESULTS (FINAL OFFLINE PASS)
================================================================================
Test Suite                                  Total    Passed    Failed    Duration
--------------------------------------------------------------------------------
1. Python Unit Tests (pytest)                 116       116         0       0.97s
   - test_phase4_affect_math.py                18        18         0
   - test_affect_pipeline.py                   16        16         0
   - test_jsonl_schema.py                       7         7         0
   - test_phase5_readiness.py                  22        22         0
   - test_prediction_engine.py                 15        15         0
   - test_adaptation_policy.py                 12        12         0
   - test_mock_esp32.py                        11        11         0
   - test_ar_guidance.py                       15        15         0
2. AR Guidance Evaluation Runner               15        15         0       0.12s
3. Phase 11 E2E Integration Pipeline            5         5         0       0.25s
4. Unity 6000.3.23f1 Batchmode Compilation    N/A         0 errors  0 warnings
5. Unity Scene Runtime Verification            14        14         0       6.20s
================================================================================
TOTAL AUTOMATED TEST PASS RATE: 100.0%
================================================================================
```

---

## 15. Strict Research Integrity Boundaries & Non-Fabrication Declarations

To maintain absolute academic and scientific integrity, the following boundaries are formally recorded:
- **Phone Connected:** NO
- **Physical ESP32 Connected:** NO
- **Physical ESP32 Validation:** NOT PERFORMED (all microcontroller interactions executed against offline software mock bridge `MockESP32Bridge.cs` and `mock_esp32.py`)
- **Physical Transport:** NOT IMPLEMENTED (BLE, USB Serial, Wi-Fi, and Firmata transports remain unimplemented until physical hardware is provided)
- **Final Physical GPIO Pinout:** NOT FROZEN (LED1 $\to$ GPIO 13 and LED2 $\to$ GPIO 12 remain provisional software-test assignments)
- **Human Study:** NOT PERFORMED (all adaptation, guidance, and prediction metrics derived from automated algorithmic evaluations and offline datasets)
- **Learning-Effectiveness Validation:** NOT PERFORMED (psychological efficacy and learning-gain hypotheses are explicitly deferred to formal future human-subject trials)
- **Phase 9A Software HAL / Mock Validation:** COMPLETE
- **Phase 10 Software UI/UX Offline Validation:** COMPLETE
- **Phase 11 Software E2E Validation:** COMPLETE
- **Phase 9B Physical ESP32 Integration:** PENDING

---

## 16. Deferred Verification Gate for Phase 9B & Physical Hardware

The following checklist must be satisfied when physical hardware becomes available:
- [ ] Connect OnePlus 13R via USB ADB and verify `adb devices`.
- [ ] Connect physical ESP32 development board (determine exact board model, e.g., ESP32-WROOM-32D, ESP32-S3).
- [ ] Select and implement physical transport (BLE GATT vs USB OTG Serial).
- [ ] Assign final verified GPIO pin numbers for Green LED and Red LED.
- [ ] Flash ESP32 firmware with JSON/binary protocol matching `IHardwareBridge`.
- [ ] Validate end-to-end on-device execution with physical breadboard, LEDs, and tracked marker.

---

## 17. Repository Hygiene, Stale Artifact Purge & Git State

- **Stale Concepts Audited:** Zero active occurrences of `Attention` as affect dimension, `jaw_clench`, `AU12`, `lip_press`, `head_pitch_level`, `ear_attentive`, `Arduino Uno GPIO`, `arduino_pin13`, `Production Ready`, or `360° tracking`.
- **Git Compliance:** Strictly read-only operations observed. Zero commits, zero pushes, and zero branch alterations performed.
- **Artifacts Preserved:** All historical raw evidence (`phase3_ar_diag_raw.txt`, `session_*.jsonl`, DAiSEE calibration curves) preserved in their original integrity.

---

## 18. Sign-off & Transition Readiness

Every defect and reproducibility issue identified by the forensic audit has been resolved, documented, and verified against the running codebase.

The offline codebase has passed the defined automated regression, formula-parity, guidance, E2E, compilation, and scene-initialization checks, and is in a fully compiled and tested offline state. It is certified as ready for the Phase 9B physical integration gate.
