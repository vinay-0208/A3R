# A³R PROJECT PLAN — Affect-Adaptive AR (Android-Only Research Prototype)

> **This is the project's constitution.** Do not silently deviate from it. If a deviation is
> necessary, update this document and log the change in `memory.md` under "Decisions & Deviations."

---

## 1. Project Goal

> *"An Android-based AR learning system that uses real-time facial affect inference, calibrated
> offline using the DAiSEE dataset, to dynamically adapt AR instructional content during an
> electronics assembly task."*

### Core Thesis / Research Contribution

An Android AR application that watches the learner's face through the phone's front camera while
they follow AR-overlaid step-by-step instructions for a physical electronics-assembly task, infers
real-time confusion/frustration/engagement using on-device facial landmark analysis, and
autonomously adapts the AR instructional content itself — simplifying the current step, injecting an
extra visual cue, slowing pacing, or inserting a hint — the moment it detects the learner
struggling. No EEG, no eye-tracking hardware, no wearables — camera-only, both front (learner's
face) and rear (AR tracking of the physical assembly).

### Research Gap (Validated)

A structured literature and patent search (50 verified 2021–2026 papers via Consensus AI + Google
Patents) confirmed no existing published system or patent combines: (a) AR step-by-step procedural
instruction, (b) RGB-camera-only affect/cognitive-load inference, and (c) a same-session closed
feedback loop that mutates the AR content itself via defined actions (simplify / cue / slow / hint).

### Fixed Demo Task: Blinking-LED Circuit on Arduino Uno

**Board:** Arduino Uno (locked choice).

**Components Required:**
- 1× Arduino Uno board
- 1× Breadboard (half-size or full-size)
- 1× LED (any standard 5mm, e.g., red)
- 1× 220Ω resistor (¼W, carbon film)
- 2–3× Male-to-male jumper wires

**Exact Step-by-Step Sequence** (each step becomes one entry in `led_blink_task.json`):

| Step | ID | Instruction | Detail | Common Error (for adaptation) |
|------|----|-------------|--------|-------------------------------|
| 1 | `place_breadboard` | Place the breadboard flat on the workbench | Orient so row numbers are visible, with the power rails at top and bottom | Breadboard orientation confusion |
| 2 | `insert_resistor` | Insert the 220Ω resistor into the breadboard | Place one lead in row 10 column E, the other lead in row 14 column E. Both leads must be firmly seated. | Wrong rows, leads not fully inserted |
| 3 | `insert_led` | Insert the LED into the breadboard with correct polarity | **Anode (long leg)** in row 14 column D, **cathode (short leg)** in row 15 column D. The anode connects to the resistor via the shared row 14 bus. | **Polarity reversed** (most common confusion point) |
| 4 | `wire_resistor_to_pin13` | Connect a jumper wire from digital pin 13 to the resistor | One end in Arduino digital pin 13, other end in breadboard row 10 column A (same row as resistor lead). | Wrong Arduino pin, wrong breadboard row |
| 5 | `wire_gnd` | Connect a jumper wire from Arduino GND to the LED cathode row | One end in any Arduino GND pin, other end in breadboard row 15 column A (same row as LED cathode). | Wrong GND pin, wrong row |
| 6 | `connect_usb` | Connect the Arduino Uno to a computer via USB cable | Use the USB-B connector on the Arduino. The power LED on the Arduino should light up. | Cable not fully seated |
| 7 | `upload_blink_sketch` | Upload the Blink sketch to the Arduino | In Arduino IDE: File → Examples → 01.Basics → Blink. Click Upload (→ arrow). Wait for "Done uploading." | Wrong board/port selected in IDE |
| 8 | `verify_blink` | Verify the LED is blinking | The LED should blink on/off at 1-second intervals. If not blinking, check all connections. | LED doesn't blink → re-check polarity, wiring, resistor value |

---

## 2. Locked Tech Stack

| Layer | Choice | Pinned Version |
|-------|--------|---------------|
| Game engine | Unity 2022 LTS | **2022.3.x** (exact patch TBD on first install — will be recorded here) |
| AR framework | AR Foundation (Unity package) + ARCore XR Plugin | AR Foundation **5.1.x**, ARCore XR Plugin **5.1.x** (exact TBD) |
| AR tracking mode | AR Foundation Image Tracking (`ARTrackedImageManager` + `XRReferenceImageLibrary`) | N/A |
| Runtime language | C# | .NET Standard 2.1 (Unity default) |
| On-device face landmarks | MediaPipe Face Landmarker via community Unity plugin | `com.github.homuler.MediaPipeUnityPlugin` **v0.14.x** (or Sentis fallback — TBD) |
| Offline calibration | Python 3.11 inside `.venv` | See `requirements.txt` |
| 3D/AR assets | Free CC0/permissive `.glb` + Unity built-in primitives | N/A |
| Version control | Git | Local repo |
| Diagrams | PlantUML | CLI, latest |
| Build/deploy | Unity → Gradle → APK → `adb install` | Android SDK/NDK via Unity Hub |

**Explicitly removed:** iOS, ARKit XR Plugin, Xcode, Mac build steps, `.ipa`, TestFlight, cross-platform abstractions.

**Python constraint:** `scikit-learn` must NOT appear in `requirements.txt`. No `.fit()` calls anywhere in `python_tools/`.

---

## 3. Folder Structure

```
A3R/
├── memory.md
├── PROJECT_PLAN.md
├── README.md
├── .gitignore
├── requirements.txt
├── .venv/                           (gitignored)
├── docs/
│   ├── diagrams/
│   │   ├── architecture.puml
│   │   ├── architecture.png
│   │   ├── workflow.puml
│   │   └── workflow.png
│   ├── evaluation_plan.md
│   └── milestone_log.md
├── python_tools/
│   └── daisee_calibration/
│       ├── load_daisee.py
│       ├── extract_features.py
│       ├── score_affect_rulebased.py
│       ├── calibrate_thresholds.py
│       └── notebooks/
│           └── threshold_calibration.ipynb
├── assets_reference/
│   ├── marker_image_source.png
│   └── marker_image_print.pdf
├── assets_3d/
│   ├── sourced/
│   │   ├── breadboard.glb
│   │   ├── arduino_uno.glb
│   │   ├── resistor.glb
│   │   └── jumper_wire.glb
│   ├── primitives/
│   │   ├── LedPrefab
│   │   └── ArCueArrowPrefab
│   └── ATTRIBUTIONS.md
├── unity_project/
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   └── A3R_Main.unity
│   │   ├── Scripts/
│   │   │   ├── ARTracking/
│   │   │   │   └── ARImageTracker.cs
│   │   │   ├── Affect/
│   │   │   │   ├── FaceLandmarkBridge.cs
│   │   │   │   ├── AffectFeatureExtractor.cs
│   │   │   │   └── AffectScorer.cs
│   │   │   ├── Adaptation/
│   │   │   │   ├── AdaptationController.cs
│   │   │   │   └── AdaptationEvents.cs
│   │   │   ├── ContentManager/
│   │   │   │   ├── ARContentManager.cs
│   │   │   │   └── InstructionStep.cs
│   │   │   └── Logging/
│   │   │       └── SessionLogger.cs
│   │   ├── Models/
│   │   ├── Prefabs/
│   │   ├── ReferenceImageLibrary/
│   │   │   └── A3R_ReferenceImageLibrary.asset
│   │   ├── StepData/
│   │   │   └── led_blink_task.json
│   │   └── Editor/
│   ├── Packages/
│   │   └── manifest.json
│   └── ProjectSettings/
├── build/
│   └── android/
└── evaluation/
    ├── pilot_protocol.md
    └── results/
```

---

## 4. Reference Marker Image

- **Source image:** `assets_reference/marker_image_source.png`
- **Design:** High-contrast, asymmetric pattern (NOT a simple QR code — a richly textured reference image for ARCore's image tracking, which works best with high feature density)
- **Printed dimensions:** 15cm × 15cm (recorded in `XRReferenceImageLibrary` as physical width = 0.15m)
- **Placement:** Flat on the workbench, directly beside the breadboard. All AR content is anchored relative to this tracked image's pose.
- **Unity setup:** Added to `XRReferenceImageLibrary` asset at `Assets/ReferenceImageLibrary/A3R_ReferenceImageLibrary.asset` with correct real-world width.

---

## 5. System Architecture (5 Modules)

### Module 1: AR Image Tracker (`ARImageTracker.cs`)
**Responsibility:** Detects and tracks the printed reference image using AR Foundation's `ARTrackedImageManager`. When the image is first detected, instantiates a content root GameObject anchored to the tracked image's pose. Continuously updates the content root's position/rotation as the tracked pose updates. Handles tracking-lost events (pauses content rendering).

**Inputs:** Rear camera feed → ARCore image tracking → `ARTrackedImage` events
**Outputs:** A world-anchored `Transform` (the content root) that all instructional content is parented to.

### Module 2: Affect Inference Engine (`FaceLandmarkBridge.cs`, `AffectFeatureExtractor.cs`, `AffectScorer.cs`)
**Responsibility:** Captures front-camera frames, runs MediaPipe Face Landmarker to extract 468 facial landmarks, computes geometric features (brow-furrow distance, eye-aspect ratio, mouth-corner tension), and produces confusion/frustration/engagement scores using a fixed rule-based formula.

**Inputs:** Front camera frames (sampled every ~200ms)
**Outputs:** `AffectState` struct containing confusion score, frustration score, engagement score, confidence value, and timestamp.

### Module 3: Adaptation Controller (`AdaptationController.cs`, `AdaptationEvents.cs`)
**Responsibility:** Finite-state machine that monitors the affect scores over time. When a confusion or frustration score exceeds the calibrated threshold $T$ for a sustained dwell-time $D$, emits an adaptation action event. Tracks which step the learner is on and selects the appropriate adaptation type.

**Inputs:** `AffectState` stream from Module 2 (every 500ms)
**Outputs:** `AdaptationEvent` (one of: `SimplifyStep`, `InjectCue`, `SlowPace`, `InsertHint`) dispatched via C# events.

### Module 4: AR Content Manager (`ARContentManager.cs`, `InstructionStep.cs`)
**Responsibility:** Manages the instructional step sequence defined in `led_blink_task.json`. Renders the current step's AR overlay (text, 3D model highlight, arrow cue) parented to the content root from Module 1. Subscribes to `AdaptationEvent`s from Module 3 and mutates the rendered content accordingly (e.g., breaks a complex step into sub-steps, adds a highlighting arrow, shows a hint panel).

**Inputs:** Content root transform (from Module 1), adaptation events (from Module 3), user "next step" input
**Outputs:** Rendered AR instructional overlays visible through the phone's rear camera view.

### Module 5: Session Logger (`SessionLogger.cs`)
**Responsibility:** Records the full affect trajectory (timestamped affect scores) and all adaptation events to a local JSON file on the device for later evaluation. Writes a session summary at task completion.

**Inputs:** `AffectState` stream, `AdaptationEvent` stream, step-change events
**Outputs:** JSON log file at `Application.persistentDataPath/session_logs/session_<timestamp>.json`

---

## 6. Affect-Scoring Design

### MediaPipe Landmark Indices → Geometric Features

| Feature | Landmark Indices | Computation |
|---------|-----------------|-------------|
| **Brow Furrow Distance** | Left inner brow: 105, Right inner brow: 334 | Euclidean distance between landmarks 105 and 334, normalized by inter-ocular distance (landmark 33 to 263) |
| **Eye Aspect Ratio (EAR)** | Left eye: 159 (top), 145 (bottom), 33 (inner), 133 (outer); Right eye: 386 (top), 374 (bottom), 263 (inner), 362 (outer) | $\text{EAR} = \frac{\|p_{top} - p_{bottom}\|}{2 \cdot \|p_{inner} - p_{outer}\|}$ averaged over both eyes |
| **Mouth Corner Tension** | Left mouth corner: 61, Right mouth corner: 291, Upper lip center: 13, Lower lip center: 14 | Angle of mouth corners relative to lip center midpoint; downward pull indicates frustration |
| **Brow Height** | Left outer brow: 65, Right outer brow: 295, Left eye center: 159, Right eye center: 386 | Vertical distance from brow to eye center, normalized by face height (landmark 10 to 152) |

### Fixed Rule-Based Scoring Formulas

**Confusion Score** (range 0.0–1.0):
$$S_{confusion} = 0.40 \cdot \text{brow\_furrow\_norm} + 0.30 \cdot (1.0 - \text{EAR\_norm}) + 0.30 \cdot \text{mouth\_tension\_norm}$$

**Frustration Score** (range 0.0–1.0):
$$S_{frustration} = 0.35 \cdot \text{brow\_furrow\_norm} + 0.25 \cdot (1.0 - \text{EAR\_norm}) + 0.40 \cdot \text{mouth\_downturn\_norm}$$

**Engagement Score** (range 0.0–1.0):
$$S_{engagement} = 0.50 \cdot \text{EAR\_norm} + 0.30 \cdot (1.0 - \text{brow\_furrow\_norm}) + 0.20 \cdot \text{brow\_height\_norm}$$

Where `_norm` denotes min-max normalization to [0,1] based on baseline ranges established during DAiSEE calibration.

### Threshold and Dwell-Time Values

**Placeholder values** (to be overwritten after M3 DAiSEE calibration):
- Confusion threshold: $T_{confusion} = 0.60$
- Frustration threshold: $T_{frustration} = 0.65$
- Dwell time: $D = 3.0$ seconds

**Adaptation trigger rule:** If $S_{confusion} > T_{confusion}$ OR $S_{frustration} > T_{frustration}$ sustained for $\geq D$ consecutive seconds → emit `AdaptationEvent`.

---

## 7. Milestone Breakdown

### M0 — Scaffold
- Create full folder tree, `.gitignore`, `requirements.txt` (no scikit-learn)
- Write `memory.md` template and `PROJECT_PLAN.md`
- `git init` + first commit
- Set up `.venv` and install Python deps
- Install Unity 2022 LTS (pin version), create Unity project inside `unity_project/`
- Add only AR Foundation + ARCore XR Plugin packages (NOT ARKit)
- Set build target to Android in Player Settings
- Commit `Packages/manifest.json`

### M1 — Reference Marker + AR Assets
- Finalize printed reference image in `assets_reference/`
- Add to `XRReferenceImageLibrary` in Unity with correct real-world width (0.15m)
- Source free CC0/permissive `.glb` assets: breadboard, arduino_uno, resistor, jumper_wire
- Log each source URL and license in `ATTRIBUTIONS.md`
- Build LED prefab (distinct anode/cathode leg lengths) from Unity primitives
- Build AR cue arrow prefab from Unity primitives
- Import all into `unity_project/Assets/Models/` and `Assets/Prefabs/`

### M2 — Static AR Instruction Flow (No Affect Yet)
- Implement `ARImageTracker.cs` (wraps `ARTrackedImageManager`)
- Implement `InstructionStep.cs` + `led_blink_task.json` (8-step sequence from Section 1)
- Implement first version of `ARContentManager.cs` (manual "next" button, all content parented to tracked-image anchor)
- Demo: marker detection → step-by-step AR overlay visible, advancing via UI button

### M3 — Affect Inference Engine
- Run offline DAiSEE calibration in Python (`python_tools/daisee_calibration/`)
  - Extract features from DAiSEE subset using MediaPipe
  - Compute rule-based scores
  - Sweep threshold/dwell-time candidates against ground truth
  - Record calibrated $T$ and $D$ values in PROJECT_PLAN.md and memory.md
- Implement on-device C#:
  - `FaceLandmarkBridge.cs` (MediaPipe/Sentis integration)
  - `AffectFeatureExtractor.cs` (same geometric formulas as Python, ported to C#)
  - `AffectScorer.cs` (scores + confidence, using calibrated values)
- Add on-screen debug HUD for real-time affect score visibility

### M4 — Adaptation Controller + Closed Loop
- Implement `AdaptationController.cs` (threshold + dwell-time FSM)
- Implement `AdaptationEvents.cs` (event definitions)
- Extend `ARContentManager.cs` to subscribe to adaptation events
- Implement content mutations: simplify step / inject cue / slow pace / insert hint
- Test with deliberate "confused" expressions

### M5 — Session Logger
- Implement `SessionLogger.cs` (JSON/CSV on-device logging)
- Log affect trajectory + adaptation events + step transitions

### M6 — Android Build
- Configure Android build settings (min API level, package name, camera permissions)
- Build APK
- `adb install` to physical Android phone
- Verify full closed loop on-device (front camera affect + rear camera tracking)

### M7 — End-to-End Demo
- Real breadboard + Arduino Uno + LED + resistor + jumper wires + printed marker card
- Full app running: rear camera tracks marker, renders step overlays
- Front camera detects real confusion, system adapts visibly
- Capture adaptation event in Session Logger

### M8 — Docs & Wrap-Up
- Finalize README.md with complete setup/run instructions
- Complete `docs/evaluation_plan.md`
- Complete `evaluation/pilot_protocol.md`
- Final commit, all milestones marked complete in memory.md

---

## 8. Final Acceptance Criteria

- [ ] Unity project builds cleanly for Android with ONLY AR Foundation + ARCore XR Plugin (no ARKit XR Plugin in `manifest.json`)
- [ ] App installed and running on physical Android phone via `adb install`, using both front and rear cameras simultaneously
- [ ] Rear camera detects and tracks printed reference marker via `ARTrackedImageManager`, renders step-by-step AR overlays anchored to it (no plane detection or markerless tracking)
- [ ] Front camera Affect Inference Engine produces real-time confusion/frustration/engagement scores (visible in debug HUD or Session Logger), using calibrated $T$ and $D$ values from DAiSEE (not arbitrary placeholders)
- [ ] At least one genuine sustained confusion/frustration signal triggers a real, visible AR content adaptation — captured in Session Logger
- [ ] `assets_3d/ATTRIBUTIONS.md` lists valid source URL and license for every sourced 3D asset
- [ ] `memory.md` accurately reflects final state, all milestones marked complete (or documented as blocked with reasons), git history tells a coherent story

---

## Appendix: DAiSEE Calibration Scope (Section 2a)

The Python tooling under `python_tools/daisee_calibration/` answers exactly one question:
**"At what confusion/frustration score, sustained for how long, should the Adaptation Controller fire?"**

**In scope:**
- Load DAiSEE clips + ground-truth engagement/confusion/frustration/boredom labels
- Run MediaPipe Face Landmarker over sampled frames
- Compute the same geometric features the C# code uses (brow-furrow, EAR, mouth tension)
- Combine features into rule-based scores using the fixed formula (NOT a learned model)
- Sweep candidate threshold and dwell-time values against ground truth
- Report precision/recall at each candidate to pick the operating point
- Record chosen $T$ and $D$ values

**Out of scope (DO NOT DO):**
- Fitting a scikit-learn classifier or any ML model
- Train/test splits for model generalization
- Hyperparameter grid search over model architectures
- Cross-validation
- Reporting/chasing accuracy/F1 "benchmark" numbers
- Any `.fit()` call anywhere in `python_tools/`
