# A³R Runtime Architecture

**Document ID:** `A3R-DOC-ARCH-001`  
**Date:** 2026-09-17  
**Engine:** Unity 6.3 LTS (`6000.3.23f1`)  
**Target Environment:** Android Mobile AR (`arm64-v8a`)  

---

## 1. Architectural Pipeline

The A³R system follows an end-to-end multi-rate closed-loop adaptive architecture:

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CAMERA / AR SENSING                                │
│  Rear Camera (ARCore Image Tracking) ──▶ Tabletop Pose (15×15cm marker)     │
│  Front Camera (WebCamTexture / Android) ──▶ Face Landmark Extraction (30 Hz)│
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       PERCEPTION & AFFECT PIPELINE                          │
│  MediaPipe Tasks API (LIVE_STREAM, GPU Delegate) ──▶ 478 3D Facial Landmarks │
│  AffectFeatureExtractor ──▶ Brow Furrow, EAR, Mouth Tension, Jaw Clench     │
│  AffectScorer (2.0 Hz) ──▶ Confusion, Frustration, Engagement ∈ [0.0, 1.0]  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                 TEMPORAL DECISION & ADAPTATION LOGIC                        │
│  AffectPredictor (K=5 rolling window) ──▶ Precursor Trend Detection         │
│  AdaptationController (FSM) ──▶ Dwell Time (3.0s), Thresholds (0.60, 0.65) │
│                                  Cooldown (15.0s), Max 3 Adaptations / Step │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       ADAPTIVE AR INTERVENTIONS                             │
│  ARContentManager & ARGuidanceController                                    │
│  - InjectCue: Accelerate chevron pulse (3.5 Hz) & reticle laser glow        │
│  - InsertHint: Pop up procedural micro-guidance banner                      │
│  - SlowPace: Decelerate animation & enlarge target reticle ring              │
│  - SimplifyStep: Hide non-essential components & isolate target pin         │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      SESSION LOGGING & TELEMETRY HUD                        │
│  SessionLogger ──▶ Structured JSONL logs in Application.persistentDataPath │
│  UIManager ──▶ Multi-Rate Telemetry HUD (Render, Camera, MediaPipe FPS)     │
│               Interactive Session Control (Start / Stop / Summary Modal)    │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Timing & Rate Decomposition

| Layer | Execution Rate | Latency Budget | Mechanism |
| :--- | :--- | :--- | :--- |
| **ARCore SLAM / Rendering** | 60 Hz | $<16.6\text{ ms}$ | Unity Engine Main Loop & AR Tracked Pose Driver |
| **Front Camera Ingestion** | 30 Hz | $<33.3\text{ ms}$ | `WebCamTexture` on Android API 30+ |
| **MediaPipe Landmark Detection** | 20–30 Hz | $10–15\text{ ms}$ | Native C++ library via `mediapipe_android.aar` GPU delegate |
| **Affect Inference** | 2.0 Hz ($\Delta t = 500\text{ ms}$) | $<1.0\text{ ms}$ | Monotonic linear combination equations |
| **Predictive Precursor Analysis** | 2.0 Hz | $<0.5\text{ ms}$ | Ordinary Least Squares regression ($K=5$ buffer) |
| **Temporal Decision FSM** | 2.0 Hz | $<0.1\text{ ms}$ | Dwell state machine with anti-jitter persistence |
| **Telemetry HUD Stream** | 4.0 Hz ($\Delta t = 250\text{ ms}$) | $<1.0\text{ ms}$ | TextMeshProUGUI text formatting |

---

## 3. Mathematical Specifications (Frozen Invariants)

### Affect Scoring Formulas
$$\text{Confusion} = 0.40 \cdot \text{brow\_furrow} + 0.30 \cdot (1 - \text{EAR}) + 0.30 \cdot \text{mouth}$$
$$\text{Frustration} = 0.70 \cdot \text{jaw\_clench} + 0.30 \cdot \text{brow\_furrow}$$
$$\text{Engagement} = 0.50 \cdot \text{head\_pitch\_stability} + 0.50 \cdot \text{blink\_regularity}$$

### Decision Thresholds & Constraints
- Confusion Activation Threshold: $T_{\text{confusion}} = 0.60$
- Frustration Activation Threshold: $T_{\text{frustration}} = 0.65$
- Required Dwell Time: $D = 3.0\text{ s}$ continuous persistence
- Adaptation Cooldown: $15.0\text{ s}$ refractory window
- Maximum Adaptations: $3$ interventions per assembly step

### Predictive Engine Constraints
- Rolling Window Size: $K = 5$ samples (minimum $3$ required)
- Slope Threshold: $0.05\text{ units/s}$
- Recovery Threshold: $0.30$ (minimum drop from peak to clear state)
- Predictive Cooldown: $5.0\text{ s}$ suppression window

---

## 4. Key Runtime Components

- [`ARContentManager.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/ContentManager/ARContentManager.cs): Central coordinator managing assembly steps, session state transitions, hardware bridging, and UI telemetry.
- [`FaceLandmarkBridge.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/Perception/FaceLandmarkBridge.cs): Native MediaPipe bridge running live face landmark extraction with automated fallback to simulated perception in desktop editor mode.
- [`AffectScorer.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/Affect/AffectScorer.cs): Real-time affect evaluation implementing frozen equations.
- [`AffectPredictor.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/Affect/AffectPredictor.cs): Precursor state trend detection.
- [`AdaptationController.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/Adaptation/AdaptationController.cs): Finite State Machine triggering tiered pedagogical interventions.
- [`ARGuidanceController.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/ARGuidance/ARGuidanceController.cs): Holographic 3D AR guidance consisting of directional chevrons (`A3R_ChevronPath`) and multi-ring reticle (`A3R_TargetRing`).
- [`SessionLogger.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/Logging/SessionLogger.cs): User-driven session recording emitting JSONL records.
- [`UIManager.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Scripts/UI/UIManager.cs): Mobile HUD controlling step navigation, session lifecycle, telemetry streaming, and post-session review.
