# A³R — Affect-Adaptive Augmented Reality
### Android-Only · Marker-Tracked · Academic Research Prototype

---

## What This Is

A³R is a **research prototype** for an affect-adaptive AR-assisted electronics assembly learning system.

Using a **front-facing camera** to infer learner affect (confusion, frustration, engagement) from facial geometry, the system adapts live AR instructional overlays in response — injecting visual cues, simplifying steps, or slowing the instructional pace when the learner appears stuck.

**Target use-case**: A learner assembles a blinking-LED circuit on a breadboard using an Arduino Uno. Printed AR markers on the workbench anchor 3D component overlays and step-by-step instructions in real space. A dwell-time FSM monitors affect scores and triggers one of four escalating adaptation actions.

This is **not a shipping product** — it is a controlled research prototype for academic evaluation.

---

## System Architecture

```
Front Camera (MediaPipe Face Landmarker)
    ↓
AffectFeatureExtractor  ← brow furrow, EAR, mouth tension, brow height
    ↓
AffectScorer            ← confusion, frustration, engagement scores (2 Hz)
    ↓
AdaptationController    ← threshold + dwell-time FSM (calibrated via DAiSEE)
    ↓
ARContentManager        ← mutates AR overlays: InjectCue / InsertHint / SlowPace / SimplifyStep
    ↓
SessionLogger           ← .jsonl log: affect trajectory + adaptation events
```

AR tracking: **AR Foundation 5.1.5 + ARCore XR Plugin 5.1.5** (Android only — no ARKit)

---

## Repository Layout

```
A3R/
├── python_tools/
│   └── daisee_calibration/      # Offline calibration pipeline (run on PC, not on device)
│       ├── load_daisee.py         # DAiSEE dataset I/O
│       ├── extract_features.py    # Per-frame facial feature extraction (MediaPipe)
│       ├── score_affect_rulebased.py  # Rule-based weighted-sum affect scorer
│       ├── calibrate_thresholds.py    # (T, D) grid sweep → calibration_result.json
│       ├── batch_extract.py       # Batch orchestrator over DAiSEE clips
│       └── visualize_calibration.py   # Publication figures (heatmap, PR curve, etc.)
│
├── unity_project/               # Unity 2022.3 LTS project (Android target)
│   ├── Assets/
│   │   ├── Scenes/A3R_Main.unity        # Main scene
│   │   ├── Scripts/
│   │   │   ├── ARTracking/              # ARImageTracker.cs
│   │   │   ├── Affect/                  # AffectScorer, AffectFeatureExtractor, FaceLandmarkBridge
│   │   │   ├── Adaptation/              # AdaptationController, AdaptationEvents
│   │   │   ├── ContentManager/          # ARContentManager, InstructionStep
│   │   │   └── Logging/                 # SessionLogger
│   │   ├── Editor/BuildHelper.cs        # A3R → Build menu (Android APK/AAB)
│   │   ├── StepData/led_blink_task.json # 8-step task definition
│   │   ├── Models/                      # Imported .glb models
│   │   ├── Prefabs/                     # LedPrefab, ArCueArrowPrefab, etc.
│   │   └── ReferenceImageLibrary/       # XRReferenceImageLibrary asset (created in Editor)
│   └── Packages/manifest.json           # AR Foundation + ARCore only, no ARKit
│
├── assets_reference/
│   ├── marker_image_source.png  # 1000×1000 px AR tracking marker — PRINT AT 15 cm × 15 cm
│   └── marker_image_print.pdf   # Print-ready PDF (100% scale)
│
├── assets_3d/
│   ├── sourced/                 # Downloaded .glb models (or placeholders)
│   └── ATTRIBUTIONS.md          # Source URLs + licenses (mandatory for academic use)
│
├── build/android/               # APK/AAB output (git-ignored)
├── evaluation/                  # Pilot study data and results
├── docs/
│   ├── milestone_log.md         # Append-only project history
│   ├── unity_setup_guide.md     # Step-by-step Unity installation + project setup
│   ├── evaluation_plan.md       # Pilot study design (M8)
│   ├── figures/                 # Generated calibration plots
│   └── diagrams/                # PlantUML architecture + workflow diagrams
│
├── PROJECT_PLAN.md              # Project constitution — all design decisions
├── memory.md                    # Agent state file (cross-session continuity)
├── requirements.txt             # Python dependencies
└── .venv/                       # Python 3.11.9 virtual environment (git-ignored)
```

---

## Quickstart

### 1. Python Calibration Tooling

```powershell
# Activate venv (Windows)
.\.venv\Scripts\activate

# Run calibration smoke test (synthetic data)
python python_tools\daisee_calibration\calibrate_thresholds.py

# Full DAiSEE calibration (requires dataset + MediaPipe model)
python python_tools\daisee_calibration\batch_extract.py `
    --daisee_root "D:\datasets\DAiSEE" `
    --split test `
    --model_path face_landmarker.task `
    --n_clips 200

python python_tools\daisee_calibration\calibrate_thresholds.py
python python_tools\daisee_calibration\visualize_calibration.py
```

### 2. Unity Project Setup

See [`docs/unity_setup_guide.md`](docs/unity_setup_guide.md) for the full 9-step setup.

**Short version**:
1. Install Unity Hub → https://unity.com/download
2. Install Unity **2022.3 LTS** with Android Build Support
3. Open `unity_project/` in Unity Hub
4. XR Plug-in Management → Android tab → tick **ARCore**
5. Menu: **A3R → Verify Build Settings**

### 3. Build Android APK

```
Unity Editor → A3R → Build Android APK (Debug)
Output: build/android/A3R_debug_<timestamp>.apk
```

### 4. Print the AR Marker

Print `assets_reference/marker_image_print.pdf` at **100% scale** (no fit/shrink).  
The marker must be **exactly 15 cm × 15 cm** after printing.  
Measure with a ruler. Place flat on the workbench beside the breadboard.

---

## Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| AR platform | Android only (ARCore) | Lab devices are Android; ARKit excluded by requirement |
| Affect inference | Rule-based geometric features (no ML model) | No training data needed on-device; calibrated thresholds from DAiSEE |
| Feature pipeline | MediaPipe Face Landmarker 478-pt | State-of-art accuracy, runs at ~15fps on mid-range Android |
| Adaptation trigger | Threshold + dwell-time FSM | Simple, interpretable, calibrate-able; no online learning |
| Task domain | Arduino Uno LED blink circuit (8 steps) | Universal intro electronics task; well-documented errors |
| 3D overlays | Mixed (sourced CC0 GLBs + Unity primitives) | Avoids licensing issues; primitives sufficient for prototype |
| Logging | Line-delimited JSON (.jsonl) | Streamable, trivially parseable in Python for analysis |

---

## Constraints (Non-Negotiable)

- ❌ **No ARKit** — Android-only project
- ❌ **No scikit-learn** — not in requirements, not in venv, not in any script
- ✅ **Virtual environment MUST be `.venv/`** (not `venv/`)
- ✅ **Affect scoring is rule-based** — no trained classifiers

---

## Academic Attribution

If this work is cited or published, reference the following:

- **DAiSEE dataset**: Gupta et al. (2016). "DAiSEE: Towards User Engagement Recognition in the Wild." *arXiv:1609.01885*
- **AR Foundation / ARCore**: Unity Technologies + Google
- **MediaPipe**: Lugaresi et al. (2019). "MediaPipe: A Framework for Building Perception Pipelines." *CVPR Workshop*

3D asset attributions are in [`assets_3d/ATTRIBUTIONS.md`](assets_3d/ATTRIBUTIONS.md).
