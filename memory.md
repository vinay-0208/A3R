# A3R Project Memory (Android-Only Prototype)

> This file is the ONLY context the agent should trust across sessions. Read this fully before
> doing anything. Update it after every meaningful step, and always after every commit.

## Last Checkpoint
- Commit hash: 0b9483e
- Date/session: 2026-08-12
- What was just completed:
  - ALL 24 unit tests pass (test_affect_pipeline.py): scorer weights, formula values, feature
    extractor geometry, score_frame edge cases, clip-level stats, no-scikit-learn check ✅
  - ear_norm clamp fix applied to extract_features.py (now clips to [0,1]) ✅
  - README.md fully rewritten — architecture diagram, repo layout, quickstart, decisions table ✅
  - AndroidManifest.xml written (CAMERA permission, ARCore required, front camera required) ✅
  - docs/milestone_log.md populated — M0 complete entry, M1 in-progress entry ✅
  - 6 commits total: af326ba, 0a78d09, 8c5b18d, 816d79c, 1440850, 0b9483e ✅
- Next action (extremely specific):
  Write docs/M2_scene_setup_checklist.md and docs/M3_calibration_guide.md.
  Then write the remaining Unity C# piece: UIManager.cs (HUD panel controller for the step
  display, hint panel, and debug overlay — referenced by ARContentManager but not yet written).
  Then verify all C# files compile-check (grep for obvious syntax errors).

## Current Milestone
M1 — in progress (code 100% done; Unity Editor manual steps still pending)
Next coded milestone: M2 documentation + UIManager.cs

## MANUAL STEP REQUIRED FROM USER
> ⚠ Unity Hub cannot be installed automatically in this environment.
>
> **User must**:
> 1. Download and install Unity Hub from https://unity.com/download
> 2. Install Unity 2022.3 LTS with Android Build Support module
> 3. Open unity_project/ in Unity Hub
> 4. Follow docs/unity_setup_guide.md Steps 1–9
>
> All code and config files are written. Only Inspector-wiring steps require Unity Editor.

## Environment Setup
- Python venv: `.venv/` — activate: `.venv\Scripts\activate`
- Python 3.11.9: `C:\Users\black\AppData\Local\Programs\Python\Python311\python.exe`
- PortableGit 2.47.1: `C:\Users\black\AppData\Local\PortableTools\git\cmd\git.exe`
  - Refresh PATH each session: `$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")`
  - Then prepend Git: `$env:Path = "C:\Users\black\AppData\Local\PortableTools\git\cmd;" + $env:Path`
- Unity version: 2022.3 LTS (user must install — not yet done)
- MediaPipe Unity plugin: FaceLandmarkBridge.cs stub ready for `com.github.homuler.MediaPipeUnityPlugin` v0.14.x
- Arduino board: Arduino Uno (locked)
- Android SDK/NDK: TBD (via Unity Hub Android module)
- Reference marker: `assets_reference/marker_image_source.png` ✅ (1000×1000px, print at 15cm×15cm)
  - physicalSize.x = 0.15m, XRReferenceImageLibrary name = "A3R_Marker"
- Calibrated thresholds (PLACEHOLDER — replace after real DAiSEE calibration in M3):
  - T_confusion = 0.60, T_frustration = 0.65, D = 3.0s
  - Synthetic smoke-test pilot: T=0.45, D=2.0s (NOT for production)

## Decisions & Deviations Log (append-only, newest at top)
- 2026-08-12 — ear_norm was not clamped in extract_features.py; synthetic geometry produced
  values > 1.0. Fixed by adding np.clip(ear_norm, 0.0, 1.0). No impact on real-video pipeline
  since real faces produce EAR < 0.5. Unit test geometry also updated to use realistic h values.
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
