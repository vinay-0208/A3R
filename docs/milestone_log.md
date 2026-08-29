# Milestone Log (Append-Only)

> Human-readable history of milestone completions and key events.
> Newest entries at the top. Do NOT edit or delete existing entries.

---

## 2026-08-07 — M1 IN PROGRESS (partial completion)

**Status**: Marker image done; 3D assets stubbed; Unity Editor steps blocked on manual Unity Hub install.

**Completed sub-tasks**:
- `assets_reference/marker_image_source.png` generated (1000×1000 px, high-contrast asymmetric pattern)
- `assets_reference/marker_image_print.pdf` generated (print at exactly 15 cm × 15 cm)
- Placeholder GLB files written for all 4 components (valid GLB 2.0 binary):
  - `assets_3d/sourced/breadboard.glb`
  - `assets_3d/sourced/arduino_uno.glb`
  - `assets_3d/sourced/resistor.glb`
  - `assets_3d/sourced/jumper_wire.glb`
- `assets_3d/ATTRIBUTIONS.md` updated with source URLs and acquisition checklist
- Unity project files pre-written (scene, ProjectSettings, manifest) — awaiting Editor open
- `docs/unity_setup_guide.md` written for manual Unity Hub setup

**Commits**: `8c5b18d`, `816d79c`, `1440850`

**Remaining for M1 completion** (requires user to open Unity Editor):
1. Import `marker_image_source.png` into `XRReferenceImageLibrary` (physicalSize = 0.15 m)
2. Set `ARTrackedImageManager.referenceLibrary` in Inspector
3. Replace placeholder GLBs with real sourced models (Poly Pizza / Sketchfab CC0)

**Blocker**: Unity Hub not installed. User must install from https://unity.com/download.

---

## 2026-08-07 — M0 COMPLETE ✅

**What was built**:
- Full project directory tree (19 folders, scaffold files)
- `PROJECT_PLAN.md` — project constitution (all sections per build prompt §2)
- `memory.md` — state file for cross-session continuity
- `README.md` — project overview
- `.gitignore` — Unity + Python + IDE patterns
- `requirements.txt` — Python dependencies (mediapipe, opencv, pandas, numpy, matplotlib, jupyter; NO scikit-learn)
- `docs/diagrams/architecture.puml` — system architecture (PlantUML)
- `docs/diagrams/workflow.puml` — session workflow (PlantUML)
- All Python calibration tools: `load_daisee.py`, `extract_features.py`, `score_affect_rulebased.py`, `calibrate_thresholds.py`, `batch_extract.py`, `visualize_calibration.py`
- All C# Unity scripts: `ARImageTracker.cs`, `InstructionStep.cs`, `ARContentManager.cs`, `AdaptationEvents.cs`, `AdaptationController.cs`, `AffectFeatureExtractor.cs`, `AffectScorer.cs`, `FaceLandmarkBridge.cs`, `SessionLogger.cs`, `BuildHelper.cs` (Editor)
- Unity project scaffold: `manifest.json`, `packages-lock.json`, `ProjectSettings.asset`, `XRGeneralSettings.asset`, `A3R_Main.unity`
- Task data: `led_blink_task.json` (8 steps × 4 adaptation hints)
- Python venv (`.venv`) with all requirements installed (Python 3.11.9)
- PortableGit 2.47.1 extracted and operational
- Git repo initialized, first commit made

**Tool installation deviations** (no admin):
- Git: PortableGit 2.47.1 (extracted to `%LOCALAPPDATA%\PortableTools\git\`)
- Python: installer run with `InstallAllUsers=0` (user-mode, no admin required)
- Unity Hub: blocked — MSIX format unsupported; marked as mandatory manual step

**Commits**: `af326ba`, `0a78d09`

---
