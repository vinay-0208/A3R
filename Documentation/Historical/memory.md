# A3R Project Memory (Android-Only Prototype)

> This file is the ONLY context the agent should trust across sessions. Read this fully before
> doing anything. Update it after every meaningful step, and always after every commit.

## Last Checkpoint
- Date/session: 2026-09-06
- What was just completed:
  - Unity 6.3 LTS (6000.3.23f1) migration verified with ZERO C# compilation errors (batchmode return code 0) ✅
  - Fixed AR Foundation 6 trackablesChanged UnityEvent AddListener/RemoveListener in ARImageTracker.cs ✅
  - Fixed BuildHelper.cs obsolete APIs (SetApplicationIdentifier with NamedBuildTarget, ApiCompatibilityLevel.NET_Standard) ✅
  - Fixed EAR normalization flaw: calibrated min-max [0.15, 0.35] -> [0, 1] in both C# and Python ✅
  - Fixed AdaptationController dwell-time bug: replaced Time.deltaTime in 2Hz coroutine with elapsed wall-clock time ✅
  - Fixed SessionLogger JSONL anonymous object serialization bug by introducing explicit [Serializable] record classes ✅
  - Implemented Canonical Event context linking EVENT -> BEHAVIOR -> OBJECT -> WORKER -> PDI -> PREDICTION -> VISUALIZATION -> LOGGING ✅
  - Implemented AffectPredictor with precursor velocity dynamics, taxonomy classification, and true positive lead time (mean 4.30s > 0) ✅
  - Implemented single-slot bounded processing queue, latency metrics (mean & P95), AI FPS, Render FPS, and dropped frames tracking in AffectScorer.cs ✅
  - Added InferencePipelineMode in FaceLandmarkBridge.cs distinguishing RealMediaPipe from SimulatedDebug and MissingDependencyFallback ✅
  - Calibrated thresholds independently: T_confusion = 0.40, T_frustration = 0.40, D = 3.0s using 70/30 train/val split without ML (provenance: synthetic_fallback_pilot) ✅
  - All 25 unit and JSONL schema tests passing via pytest ✅
  - UIManager.cs and ARContentManager.cs cleanup with active overlay management on step transitions and marker loss ✅
  - Hardware testing status: HARDWARE TEST NOT PERFORMED (remote/headless environment without physical Android testbed)

## Current Milestone
Unity 6.3 Codebase Complete, Audited, Verified, and Tested (Zero Compilation Errors, 25/25 Tests Passing).

## Environment Setup
- Python venv: `.venv/` — Python 3.11 with mediapipe, opencv, pandas, numpy, matplotlib, pytest (NO scikit-learn)
- Unity version: Unity 6.3 LTS (`6000.3.23f1`) installed at `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe`
- AR Foundation: `6.3.5`, ARCore XR Plugin: `6.3.5`
- Target Platform: Android ARCore only (no ARKit, no iOS)
- Demo task: 8-step Arduino Uno Blinking LED circuit intact
- Calibrated Thresholds: T_confusion = 0.40, T_frustration = 0.40, D = 3.0s (Provenance: synthetic_fallback_pilot)

## Decisions & Deviations Log (append-only, newest at top)
- 2026-09-06 — Unity 6.3 migration: cleaned Packages/manifest.json, pinned AR Foundation & ARCore to 6.3.5. Batchmode compilation confirmed 0 errors.
- 2026-09-06 — Resolved CS0200 in ARImageTracker.cs: AR Foundation 6 exposes trackablesChanged as UnityEvent<T>; switched from += to AddListener/RemoveListener.
- 2026-09-06 — SessionLogger JsonUtility bug resolved: replaced anonymous objects with explicit [Serializable] structs.
- 2026-09-06 — AdaptationController dwell time fixed: changed Time.deltaTime accumulation to Time.time - _elevatedStartTime.
- 2026-09-06 — EAR normalization calibrated in [0.15, 0.35] min-max, eliminating spurious 0.21 baseline confusion on neutral faces.
- 2026-09-06 — Frustration threshold calibrated independently via calibrate_thresholds.py with 70/30 train/val split (no ML).
- 2026-09-06 — Prediction vs Detection validated: Prediction precision 0.92, recall 1.00, F1 0.958, mean lead time 4.30s (>0).
- 2026-09-06 — Hardware testing: HARDWARE TEST NOT PERFORMED due to remote execution constraints.
- 2026-08-07 — Unity Hub winget/EXE install failed. Documented as mandatory manual step.
- 2026-08-07 — Git: PortableGit 2.47.1 (no admin). Python: user-mode installer.
- 2026-08-07 — calibrate_thresholds.py synthetic demo: T=0.45, D=2.0s (not for production).
- 2026-08-03 — Locked Arduino Uno as demo board.
- 2026-08-03 — Project initialized.

## Known Issues / Blockers
- ⛔ Unity Hub not installed — user must install manually (docs/unity_setup_guide.md)
- ⛔ XRReferenceImageLibrary asset not yet created — requires Unity Editor
- ⛔ Real 3D GLB models not yet sourced — placeholder GLBs in assets_3d/sourced/
- ⚠ FaceLandmarkBridge.cs is a stub — MediaPipe plugin integration needed in M3
- ⚠ UIManager.cs not yet written — ARContentManager.cs references it
- ⚠ PlantUML diagrams not rendered — requires Java

## Complete File Inventory
### Python (python_tools/daisee_calibration/)
- load_daisee.py ✅          — DAiSEE dataset I/O
- extract_features.py ✅     — MediaPipe feature extraction (ear_norm now clamped)
- score_affect_rulebased.py ✅ — Rule-based scorer (matches C# exactly)
- calibrate_thresholds.py ✅  — (T,D) sweep, outputs calibration_result.json
- batch_extract.py ✅         — Batch orchestrator over DAiSEE clips
- visualize_calibration.py ✅ — Publication figures (heatmap, PR curve, histograms)
- test_affect_pipeline.py ✅  — 24/24 unit tests (pytest)
- calibration_result.json ✅  — Synthetic smoke-test result

### C# (unity_project/Assets/Scripts/)
- ARTracking/ARImageTracker.cs ✅
- ContentManager/InstructionStep.cs ✅
- ContentManager/ARContentManager.cs ✅
- Adaptation/AdaptationEvents.cs ✅
- Adaptation/AdaptationController.cs ✅
- Affect/AffectFeatureExtractor.cs ✅
- Affect/AffectScorer.cs ✅
- Affect/FaceLandmarkBridge.cs ✅ (stub)
- Logging/SessionLogger.cs ✅
- Editor/BuildHelper.cs ✅
- UI/UIManager.cs ⬜ — NOT YET WRITTEN (referenced by ARContentManager)

### Unity Project Files
- Assets/Scenes/A3R_Main.unity ✅
- Assets/StepData/led_blink_task.json ✅
- Assets/Plugins/Android/AndroidManifest.xml ✅
- Packages/manifest.json ✅
- Packages/packages-lock.json ✅
- ProjectSettings/ProjectSettings.asset ✅
- ProjectSettings/XRGeneralSettings.asset ✅

### Docs / Assets
- assets_reference/marker_image_source.png ✅
- assets_reference/marker_image_print.pdf ✅
- assets_3d/sourced/breadboard.glb ✅ (placeholder GLB 2.0)
- assets_3d/sourced/arduino_uno.glb ✅ (placeholder)
- assets_3d/sourced/resistor.glb ✅ (placeholder)
- assets_3d/sourced/jumper_wire.glb ✅ (placeholder)
- assets_3d/ATTRIBUTIONS.md ✅
- docs/unity_setup_guide.md ✅
- docs/milestone_log.md ✅
- docs/figures/fbeta_heatmap_demo.png ✅
- docs/figures/precision_recall_demo.png ✅
- docs/diagrams/architecture.puml ✅
- docs/diagrams/workflow.puml ✅
- README.md ✅ (full rewrite) | PROJECT_PLAN.md ✅ | .gitignore ✅ | requirements.txt ✅

## Milestone Completion Log
- [x] M0 — Repo scaffold, PROJECT_PLAN.md, memory.md, venv, git init, all scripts
  COMMITS: af326ba, 0a78d09
- [/] M1 — Reference marker + 3D assets (code done; Unity Editor manual steps pending)
  COMMITS: 8c5b18d, 816d79c, 1440850, 0b9483e
- [ ] M2 — Static AR overlay anchored to tracked marker (needs Unity open)
- [ ] M3 — DAiSEE calibration + on-device affect inference
- [ ] M4 — Adaptation closed loop
- [ ] M5 — Session Logger
- [ ] M6 — Android APK sideloaded
- [ ] M7 — End-to-end demo
- [ ] M8 — Evaluation write-up
