# A³R — Affect-Aware Adaptive Guidance for Markerless Mobile Augmented Reality Assembly Training

A closed-loop mobile augmented reality (AR) assembly training system that dynamically adapts step-by-step physical electronics assembly instructions in response to real-time learner affect (confusion and frustration) inferred non-intrusively from front-camera facial landmark geometry.

---

## 1. Problem Statement & Motivation

Novice learners assembling physical electronics on a solderless breadboard encounter substantial cognitive split-attention friction when translating 2D circuit schematics into 3D physical pin coordinates ($0.1" / 2.54\text{ mm}$ pitch). Conventional mobile AR training applications provide uniform, static visual overlays regardless of whether a student is progressing smoothly or experiencing persistent hesitation and confusion.

A³R addresses this limitation by coupling a concurrent dual-camera mobile architecture with a real-time, deterministic affect perception pipeline. When sustained cognitive friction is detected, the system dynamically escalates visual scaffolding—rendering 3D animated target rings, directional pointer arrows, and wire routes rigidly registered to the physical breadboard without requiring physical screen touches.

---

## 2. Key Features & System Architecture

```
                       ┌──────────────────────────────────────┐
                       │        Single Mobile Device          │
                       └──────────────────┬───────────────────┘
                                          │
            ┌─────────────────────────────┴─────────────────────────────┐
            ▼                                                           ▼
┌───────────────────────┐                                   ┌───────────────────────┐
│      Rear Camera      │                                   │     Front Camera      │
│   (ARCore Tracking)   │                                   │ (MediaPipe Landmarks) │
└───────────┬───────────┘                                   └───────────┬───────────┘
            │                                                           │
            ▼                                                           ▼
┌───────────────────────┐                                   ┌───────────────────────┐
│ Horizontal Plane &    │                                   │ 478 3D Facial Mesh    │
│ 4-Corner Calibration  │                                   │ Landmark Extraction   │
└───────────┬───────────┘                                   └───────────┬───────────┘
            │                                                           │
            ▼                                                           ▼
┌───────────────────────┐                                   ┌───────────────────────┐
│ Breadboard Local      │                                   │ Geometric Metrics:    │
│ Coordinate Frame      │                                   │ Brow, Eye (EAR), Mouth│
└───────────┬───────────┘                                   └───────────┬───────────┘
            │                                                           │
            ▼                                                           ▼
┌───────────────────────┐       Adaptation Triggers         ┌───────────────────────┐
│ Dynamic 3D Overlays:  │ ◄──────────────────────────────── │ Affect Scorer & FSM   │
│ Pins, Cues, Wires     │    (Dwell 3.0s, Cooldown 15s)     │ Confusion/Frustration │
└───────────────────────┘                                   └───────────────────────┘
```

- **Markerless 4-Point Calibration**: Fast, fiducial-free plane registration anchoring digital overlays to the physical breadboard coordinate system ($0.1" / 2.54\text{ mm}$ pitch).
- **Concurrent Dual-Camera Processing**: Real-time rear-camera AR tracking and front-camera facial landmark extraction executing concurrently on a single mobile device.
- **Deterministic Heuristic Affect Scoring**: Sub-millisecond ($0.54\text{ ms}$) evaluation of Brow Furrow distance, Eye Aspect Ratio (EAR), and Mouth Tension ratios.
- **Temporal State-Gating Logic**: A $3.0\text{ s}$ dwell window, $15\text{ s}$ cooldown period, and maximum of 3 adaptations per assembly step eliminate jitter, transient blinks, and visual disruption.
- **ESP32 Assembly Guidance**: 4-step guided tutorial with complete component positioning, wiring cues, and forensic session logging.

---

## 3. Technology Stack

- **Game Engine & Runtime**: Unity 6.3 LTS (`6000.3.23f1`), C# (.NET Standard 2.1)
- **Augmented Reality Framework**: Unity AR Foundation `6.3.5` + Google ARCore XR Plugin `6.3.5`
- **Vision Perception Pipeline**: Google MediaPipe FaceLandmarker via `homuler` Unity Plugin (478 3D landmarks @ 30 FPS camera stream)
- **Target Platform**: Android 15 / Xiaomi HyperOS (validated on POCO X7 Pro `2412DPC0AI`, MediaTek Dimensity 8300-Ultra)
- **Telemetry & Logging**: Local CSV / JSONL event logging and Android Logcat diagnostic integration

---

## 4. Project Setup & Getting Started

### Prerequisites
1. **Unity Hub**: Install Unity Hub from [unity.com](https://unity.com/download).
2. **Unity Editor**: Install **Unity 6000.3.23f1 (Unity 6.3 LTS)** with the **Android Build Support** module (including Android SDK & NDK tools and OpenJDK).
3. **Git LFS**: Ensure Git Large File Storage is installed (`git lfs version`).

### Cloning the Repository
```bash
git clone https://github.com/vinay-0208/A3R.git
cd A3R
git lfs pull
```

### Opening in Unity
1. Open **Unity Hub** and click **Add** $\rightarrow$ **Add project from disk**.
2. Select the `unity_project` directory inside the cloned repository (`A3R/unity_project`).
3. Allow Unity to resolve and compile the project packages (AR Foundation, ARCore, and embedded MediaPipe plugin).
4. In the Unity Project browser, navigate to `Assets/Scenes/` and double-click `A3R_Main.unity`.

### Building for Android
1. Go to **File $\rightarrow$ Build Profiles** (or **Build Settings**).
2. Ensure the platform is set to **Android**.
3. Under **Project Settings $\rightarrow$ XR Plug-in Management**, confirm **ARCore** is checked.
4. Connect an ARCore-compatible Android device via USB with USB Debugging enabled.
5. Click **Build and Run**.

---

## 5. Device, Camera & Runtime Requirements

- **Operating System**: Android 10.0 (API Level 29) or higher (tested and validated on Android 15).
- **Hardware**: Dual-camera Android device supported by Google Play Services for AR (ARCore).
- **Permissions**: `android.permission.CAMERA` declared and granted at runtime.
- **Graphics API**: OpenGL ES 3.0+ or Vulkan.

---

## 6. Known Limitations & Future Work

- **Heuristic Affect Scoring**: Affect scores represent geometric kinematic proxies, not clinical ground-truth emotion states.
- **Manual Step Progression**: Tutorial step transitions are currently learner-driven; automated computer-vision circuit continuity verification is part of the future research roadmap.
- **Single-User Baseline Calibration**: Facial metric baselines calibrate to individual geometry over the initial 60 frames.
- **Future Directions**: Multimodal sensor fusion (acoustic hesitation cues), automated electrical continuity sensing, and broader user evaluations.

---

## 7. Team Contributions

- **Vinay Vishwanathan** ([@vinay-0208](https://github.com/vinay-0208)) — Project Developer
- **Aarif Mohammed JK** ([@Aarif987](https://github.com/Aarif987)) — Team Member
- **Harshavardhan R** — Team Member

---

## 8. Academic Attribution & References

- **DAiSEE Dataset**: Gupta et al. (2016). *DAiSEE: Towards User Engagement Recognition in the Wild.* arXiv:1609.01885.
- **AR Foundation & ARCore**: Unity Technologies & Google.
- **MediaPipe**: Lugaresi et al. (2019). *MediaPipe: A Framework for Building Perception Pipelines.* CVPR Workshop.
- **3D Assets**: Attributions and source licenses are cataloged in [`assets_3d/ATTRIBUTIONS.md`](assets_3d/ATTRIBUTIONS.md).
