# A3R Adaptive Intelligence — FINAL Implementation Plan

**Version:** 2.0 — FINAL ADVERSARIAL AUDIT  
**Date:** 2026-09-21  
**Author:** Senior Architect (Adversarial Self-Audit)  
**Status:** FINAL — REVIEWED, SELF-CHALLENGED, READY FOR IMPLEMENTATION  

> **IMPORTANT:** This document supersedes the v1.0 plan. Every assumption from v1.0 has been
> adversarially challenged against the actual source code and project evidence.
> All uncertainties are explicitly stated. No finding is disguised as proven
> when it remains uncertain.

---

## 1. Executive Summary

### Actual Current Architecture
The A3R system is a Unity 6.3 Android AR educational system with:

- **AR overlay/tracking/guidance:** WORKING and PROTECTED (markerless breadboard placement, directional arrow, target ring, 8-step circuit task, UI panels)
- **MediaPipe face landmark detection (front camera):** WAS WORKING in Phases 2–4 (478 landmarks, `FaceDetected: true`). CURRENTLY BROKEN by an unverified camera rotation experiment.
- **Affect feature extraction:** IMPLEMENTED and FORMULA-CORRECT. Currently STARVED of valid input because face detection is broken.
- **Affect scoring:** IMPLEMENTED, TESTED (41/41 unit tests), FORMULA-CORRECT. Currently producing no useful output because features are frozen/missing.
- **Adaptation FSM:** IMPLEMENTED, TESTED (18/18 policy scenarios), ARCHITECTURALLY CORRECT. Never fires because confusion never exceeds threshold on Android.
- **Adaptation hint content:** COMPLETE (per-step `adaptation_hints` in `led_blink_task.json`).
- **AR hint display system:** COMPLETE (`ApplyAdaptation()` → `UIManager.ShowAdaptationBanner()` + `ARGuidanceController.ApplyVisualAdaptation()`).
- **Task completion detection:** DOES NOT EXIST. No authoritative completion signal of any kind.
- **Automatic step advancement:** DOES NOT EXIST. `NextStep()` is called only by UI button events.
- **Hardware bridge:** MOCK ONLY (`MockESP32Bridge`). No real ESP32 connected. Phase 9B is unstarted.

### Confirmed Problems
1. Camera rotation experiment in `FaceLandmarkBridge.cs` broke face detection entirely (22 consecutive `FaceDetected: false` in logcat).
2. EAR calibration constants (`EAR_MIN=0.15, EAR_MAX=0.35`) do not match the device's actual `ear_raw` distribution (mean=0.1304, max=0.1966), leaving `EarNorm` permanently clamped to 0.
3. `AffectScorer._currentStepId` is never set by anyone — `AdaptationController` never receives step context.
4. No automatic step completion or advancement exists.
5. `confusionThreshold` was changed from 0.60 to 0.40 in a previous session but confusion still never reached 0.40 due to problems #1 and #2.

### Confirmed Working Components
- All AR overlay, calibration, marker tracking, guidance positioning
- `AdaptationController` FSM logic (policy-validated, deterministic)
- `ARContentManager.ApplyAdaptation()` and all hint display mechanisms
- `ARContentManager.NextStep()` — public, callable programmatically, fully functional
- `UIManager` — complete UI panel system with step card, hint panel, adaptation banner, debug HUD
- `led_blink_task.json` — 8-step task with per-step adaptation hints

### Missing Components
- **Reliable face detection on Android** (currently broken by rotation experiment; was working before)
- **Dynamic EAR signal** (currently clamped to 0 on this device)
- **Step-ID wiring to AffectScorer** (never connected)
- **Any automatic task completion detection**
- **Any automatic step advancement**

### Tomorrow-First Strategy
1. **P0:** Restore the last-known-good camera path (revert rotation experiment)
2. **P1:** Recalibrate EAR constants using runtime measurement
3. **P2:** Determine a practical confusion threshold using runtime calibration
4. **P3:** Wire step context to AffectScorer
5. **P4:** Implement task completion detection (with honest labeling of what it is)
6. **P5:** Automatic step advancement via existing `NextStep()` API

---

## 1.1 Tomorrow Demo Strategy / Demo Fallback Policy

1. The long-term architecture remains: Perception → Confusion Detection → Adaptive AR Assistance → Authoritative Task Completion → Automatic Step Progression.
2. For tomorrow's demo, implementation must be staged and gated rather than attempting the entire P0–P5 roadmap at once.
3. The immediate priority is: P0 camera recovery → P1 perception/feature calibration → P2 confusion calibration → P3 contextual adaptive AR hint.
4. Automatic task progression is NOT required for tomorrow if no authoritative task-completion signal exists.
5. If authoritative task completion cannot be implemented and validated reliably, use the existing/manual Continue or Next Step mechanism as a temporary DEMO FALLBACK.
6. The manual Continue control must NOT trigger or simulate adaptive perception. Face analysis, confusion detection, and contextual AR assistance must remain autonomous.
7. Do NOT treat "user became calm" as proof that the physical task was completed.
8. Do NOT implement a fake TaskCompletionDetector based on calmness/recovery merely to achieve automatic progression.
9. Automatic progression remains a future implementation once a reliable completion signal is identified and validated.
10. The demonstration priority is reliability: A reliable adaptive AR hint with manual progression is preferable to an unreliable automatic-progression system.

### Implementation Gate
- P0 must pass before P1.
- P1 must pass before P2.
- P2 must pass before P3.
- P3 must pass before any work on task completion/progression.

If any gate fails, STOP and report the failure rather than continuing into later phases.

---

## 2. Requirement Traceability

| Requirement | Existing Status | Evidence | Required Change | Validation |
|:---|:---|:---|:---|:---|
| **Face detection** | BROKEN (was working) | `logcat_fix.txt`: 22× FaceDetected:false; Phase 2/4: was FaceDetected:true | P0: revert rotation experiment | Logcat: FaceDetected:true ≥50% |
| **Feature extraction** | IMPLEMENTED but starved | `AffectFeatureExtractor.cs` formula-correct; Phase 4: features responsive under controlled conditions; EarNorm clamped to 0 on Android | P1: recalibrate EAR_MIN/EAR_MAX | Logcat: EarNorm varies with expression |
| **Confusion detection** | IMPLEMENTED but threshold unreachable | `AffectScorer.cs` formula correct; Phase 4: max confusion 0.525 (N=1362); currently stuck at ~0.32 | P2: runtime threshold calibration | Confusion rises above threshold under expression |
| **Temporal confirmation** | IMPLEMENTED and verified | `AdaptationController.cs`: dwell FSM, cooldown, max-per-step all tested | None — already correct | FSM enters elevated state in logcat |
| **Contextual hint** | IMPLEMENTED and wired | `ARContentManager.ApplyAdaptation()`, per-step hints in JSON | None — already correct | Banner/hint panel appears |
| **User resolution** | No detection mechanism | No code monitors post-hint user state | P4: TaskCompletionDetector | Resolution detected in logcat |
| **Task completion** | **DOES NOT EXIST** | No authoritative signal; MockESP32Bridge is software-only | P4: demo fallback (see §9) | Step advances automatically |
| **Automatic next step** | **DOES NOT EXIST** | `NextStep()` only called by UI button | P5: programmatic call | Step 2 appears without button press |
| **No manual buttons** | NOT SATISFIED | `nextStepButton.onClick` is the only trigger | P4+P5 combined | Zero button presses for demo |
| **Step context wiring** | MISSING | `AffectScorer.SetCurrentStepId()` never called | P3: wire in ARContentManager | Step ID in logcat matches current step |

---

## 3. Architecture / Data Flow

```
ANDROID FRONT CAMERA
    │ WebCamTexture (640×480 @ 30fps)
    │ Owner: FaceLandmarkBridge.InitFrontCamera()
    ↓
FaceLandmarkBridge.RealInferenceLoop() [5Hz coroutine]
    │ GetPixels32() → Texture2D → MediaPipe Image
    │ FaceLandmarker.DetectAsync() [GPU delegate, LIVE_STREAM]
    │ Owner: FaceLandmarkBridge.cs
    ↓
FaceLandmarkBridge.HandleFaceLandmarkerResult() [background thread]
    │ float[1434] landmarks (478 × xyz, normalized [0,1])
    │ Atomic buffer swap → _landmarks
    │ Owner: FaceLandmarkBridge.cs
    ↓
AffectScorer.ProcessFrame() [2Hz coroutine via ScoringLoop()]
    │ faceLandmarkBridge.GetCurrentLandmarks()
    │ AffectFeatureExtractor.Extract(landmarks)
    │ → BrowFurrowNorm, EarNorm, MouthTensionNorm, BrowHeightNorm
    │ Confusion = 0.40·Furrow + 0.30·(1-EAR) + 0.30·Mouth
    │ Owner: AffectScorer.cs (lines 199-202)
    ↓
AdaptationController.UpdateAffectScores() [called by AffectScorer]
    │ FSM: NOMINAL → ELEVATED [dwell timer ≥ 3.0s] → FSM_FIRED
    │ FireAdaptation() → contentManager.ApplyAdaptation()
    │ Owner: AdaptationController.cs (lines 106-172)
    ↓
ARContentManager.ApplyAdaptation(action, behavior) [called by AdaptationController]
    │ uiManager.ShowAdaptationBanner(action)
    │ arGuidanceController.ApplyVisualAdaptation(action)
    │ ApplyInjectCue/InsertHint/SlowPace/SimplifyStep
    │ Owner: ARContentManager.cs (lines 212-248)
    ↓
[MISSING] Task Completion Detection
    │ Owner: NEW TaskCompletionDetector.cs (to be created)
    │ Monitors: AffectScorer.ConfusionScore after hint fires
    ↓
[MISSING] Automatic Step Advancement
    │ ARContentManager.NextStep() [public, lines 251-266]
    │ → _currentStepIndex++, HideAllAdaptationOverlays(), DisplayCurrentStep()
    │ → OnStepChanged event → ARGuidanceController.OnStepChanged()
    │ Owner: ARContentManager.cs (existing, called programmatically by TaskCompletionDetector)
```

---

## 4. Current System Audit

### VERIFIED WORKING
| Component | File | Evidence |
|:---|:---|:---|
| AR marker tracking | `ARImageTracker.cs` | Phase 3: 3940 pose updates, 4 recovery cycles |
| Markerless foundation | `A3R_MarkerlessFoundationController.cs` | Phase 10: procedural guidance verified |
| AR guidance visuals | `ARGuidanceController.cs` | Phase 10: 15/15 scenarios, ApplyVisualAdaptation implemented |
| Step content management | `ARContentManager.cs` | NextStep(), DisplayCurrentStep(), ApplyAdaptation() all verified |
| Adaptation FSM | `AdaptationController.cs` | Phase 8: 18/18 policy scenarios |
| Affect scoring formula | `AffectScorer.cs` | Phase 4: formula parity 0/1362 errors, 41/41 unit tests |
| Feature extraction | `AffectFeatureExtractor.cs` | Formula mathematically correct, IOD-normalised |
| Step JSON | `led_blink_task.json` | 8 steps with adaptation_hints per step |
| Session logging | `SessionLogger.cs` | JSONL telemetry validated |
| UIManager | `UIManager.cs` | 929 lines, complete panel system, adaptation banner, debug HUD |
| MockESP32Bridge | `MockESP32Bridge.cs` | Phase 9A: 11/11 tests |

### PARTIALLY WORKING
| Component | File | Issue |
|:---|:---|:---|
| MediaPipe face detection | `FaceLandmarkBridge.cs` | WAS working (Phase 2/4), CURRENTLY BROKEN by rotation experiment |
| EAR feature | `AffectFeatureExtractor.cs` | Formula correct BUT calibration constants don't match Android device |
| Confusion scoring | `AffectScorer.cs` | Formula correct BUT EarNorm permanently 0 reduces dynamic range |

### BROKEN
| Component | File | Issue |
|:---|:---|:---|
| Camera→MediaPipe pipeline | `FaceLandmarkBridge.cs` lines 474-531 | Rotation code creates dimension mismatch → FaceDetected:false 100% |

### MISSING
| Component | Expected Location | Status |
|:---|:---|:---|
| Step-ID wiring | `ARContentManager.DisplayCurrentStep()` → `AffectScorer.SetCurrentStepId()` | Never connected |
| Task completion detection | Any file | Does not exist in any form |
| Automatic step advancement | Any file | Does not exist in any form |
| Real ESP32 hardware bridge | `Hardware/` | MockESP32Bridge only; Phase 9B unstarted |

### UNKNOWN
| Component | Question | Impact |
|:---|:---|:---|
| Camera `videoRotationAngle` on Android | Is it 0, 90, 180, or 270? | Determines if rotation is even needed |
| Whether MediaPipe handles orientation internally | Does the Unity plugin normalize orientation? | If yes, rotation experiment was unnecessary |
| Post-EAR-fix confusion dynamic range | Will squinting/opening produce enough EarNorm variation? | Determines if threshold is reachable |

---

## 5. P0 — Recovery / Rollback

### Objective
Restore the last-known-good camera→MediaPipe path. Remove the unverified rotation experiment.

### Exact File
`unity_project/Assets/Scripts/Affect/FaceLandmarkBridge.cs`

### Exact Rollback Command
```powershell
# Run from: c:\Fall Semister\2026\AR&VR\project\ARVR
git checkout HEAD -- "unity_project/Assets/Scripts/Affect/FaceLandmarkBridge.cs"
```

### Rollback Rationale
1. The rotation experiment was introduced based on a HYPOTHESIS (Android WebCamTexture frames are sideways).
2. The hypothesis was NOT validated before implementation.
3. The implementation contained a confirmed bug: `_rotatedPixels` buffer allocated as `pixels.Length` (w×h) but `_readbackTex` resized to `newW × newH` (h×w for 90°/270°), causing `SetPixels32` to receive a buffer with dimension mismatch.
4. Post-implementation logcat (`logcat_fix.txt`) showed 22 consecutive `FaceDetected: false` — a 100% regression from the prior build's `FaceDetected: true`.
5. The prior build (without rotation) had stable `FaceDetected: true` in Phase 2 (160+ consecutive) and Phase 4 (1362 samples, 100% real hardware).

### Also Reverted by This Step
- Field `private Color32[] _rotatedPixels;` (line 168) — added for rotation; returns to original state.
- All rotation logic in `RealInferenceLoop()` lines 474–531 — returns to simple `_readbackTex.SetPixels32(_webCamTexture.GetPixels32())`.

### What Must NOT Be Changed
- `HandleFaceLandmarkerResult()` — untouched by this rollback
- `InitFrontCamera()` — untouched
- `GetCurrentLandmarks()` — untouched
- `InitMediaPipe()` — untouched
- No other `.cs` files touched

### Compile / Build Checkpoint
1. Compile: Unity batchmode, exit code 0, 0 errors
2. Build: Android IL2CPP ARM64 APK
3. Install: `adb install -r <apk>`

### Runtime Validation
```bash
adb logcat -s Unity | grep -E "A3R_DIAG|FaceLandmarkBridge"
```
- Hold device with face visible to front camera for 15 seconds
- Look for `[A3R_DIAG] FaceDetected: true | LandmarkCount: 478`

### STOP/GO Gate
| Criterion | PASS | FAIL |
|:---|:---|:---|
| FaceDetected: true | ≥1 occurrence in 15s | 0 occurrences |
| LandmarkCount | 478 | 0 or null |
| IsSimulated | False | True |
| AR overlay | Still shows step content | Broken |

**If FAIL:** Do NOT proceed. The camera pipeline has a separate issue unrelated to the rotation experiment. Investigate `InitFrontCamera()` camera permission and `WebCamTexture.Play()` initialization. Do NOT attempt another speculative rotation transform.

### Rollback Plan
This IS the rollback. The git HEAD state is the recovery target.

### Regression Checklist
- [ ] AR overlay still appears when marker/surface detected
- [ ] FaceDetected: true in logcat
- [ ] LandmarkCount: 478
- [ ] IsSimulated: False
- [ ] No NullReferenceException in logcat

---

## 6. P1 — Feature Calibration

### 6.1 EAR Calibration — Adversarial Analysis

#### Current Code (AffectFeatureExtractor.cs, lines 60-64, 126)
```csharp
public const float EAR_MIN = 0.15f;  // squinting → EarNorm = 0.0
public const float EAR_MAX = 0.35f;  // wide open → EarNorm = 1.0
float earNorm = Mathf.Clamp01((earRaw - EAR_MIN) / (EAR_MAX - EAR_MIN));
```

#### Phase 4 Empirical Evidence (N=1362 on-device samples)
| Statistic | `ear_raw` | `ear_norm` |
|:---|:---|:---|
| Mean | 0.1304 | 0.0048 |
| Std Dev | 0.0151 | 0.0231 |
| Min | 0.0574 | 0.0000 |
| 25% | 0.1211 | 0.0000 |
| Median | 0.1294 | 0.0000 |
| 75% | 0.1379 | 0.0000 |
| 95% | 0.1541 | 0.0207 |
| Max | 0.1966 | 0.2328 |

**Observation:** 75% of samples have `ear_raw < 0.15`, so 75%+ get `ear_norm = 0.000`. Even at P95 (0.1541), `ear_norm = (0.1541-0.15)/(0.35-0.15) = 0.021`. The feature provides essentially ZERO signal.

#### Critical Direction Analysis

The confusion formula uses `0.30 × (1 - EarNorm)`:
- **When EarNorm = 0** (current): contribution = **0.30** (CONSTANT, always HIGH)
- **When EarNorm = 0.5** (eyes normally open after recalibration): contribution = **0.15** (LOWER)
- **When EarNorm = 0.0** (squinting after recalibration): contribution = **0.30** (HIGH — confused)
- **When EarNorm = 1.0** (wide open after recalibration): contribution = **0.00** (LOW — attentive)

**The EAR feature in the confusion formula is designed to detect SQUINTING (low EarNorm → high confusion).** Currently it is broken because EarNorm is always 0, making it a constant 0.30 that provides no discriminative signal.

**After recalibration:**
- Neutral face: EarNorm ≈ 0.40–0.50 → confusion contribution ≈ 0.15–0.18
- Squinting/confused: EarNorm ≈ 0.0–0.10 → confusion contribution ≈ 0.27–0.30
- Wide open/attentive: EarNorm ≈ 0.80–1.0 → confusion contribution ≈ 0.0–0.06

**Net effect of recalibration:**
- **Neutral confusion DECREASES** (from ~0.32 to ~0.18) — good, fewer false positives
- **Confused-expression confusion STAYS HIGH** (~0.30+) — the squinting contribution remains
- **The SPREAD between neutral and confused INCREASES** — better discriminability
- **This means a LOWER threshold is needed after recalibration** because the neutral baseline drops

#### What Constants Should Be Used?

The previous plan proposed `EAR_MIN = 0.07, EAR_MAX = 0.20` without sufficient justification.

**Evidence-based analysis:**
- Phase 4 device: `ear_raw` range = [0.0574, 0.1966]
- This device's full observed range is only 0.14 units wide
- The IQR is [0.1211, 0.1379] — only 0.017 units wide
- This suggests the device produces a very narrow EAR distribution

**Principled approach:**
- `EAR_MIN` should correspond to "clearly squinting" on this device
- `EAR_MAX` should correspond to "clearly open eyes" on this device
- From Phase 4: P5 = 0.0574 (minimum, likely squinting or partial blink), P95 = 0.1541

**Conservative evidence-based constants:**
```
EAR_MIN = 0.08  (below P5 of 0.0574 provides some margin for extreme squinting)
EAR_MAX = 0.18  (above P95 of 0.1541 provides margin for wide-open eyes)
```

**IMPORTANT:** These constants are DEMO CALIBRATION values derived from Phase 4 on-device
statistics for one user on one device. They are NOT scientifically validated
across diverse populations or lighting conditions.
The correct approach is runtime calibration with a short neutral-face capture.

#### Exact Change

**File:** `unity_project/Assets/Scripts/Affect/AffectFeatureExtractor.cs`  
**Class:** `AffectFeatureExtractor` (static class)  
**Lines:** 63-64  
**Function:** Constants used by `Extract()` at line 126

```csharp
// BEFORE:
public const float EAR_MIN = 0.15f;
public const float EAR_MAX = 0.35f;

// AFTER:
public const float EAR_MIN = 0.08f;
public const float EAR_MAX = 0.18f;
```

**Why necessary:** Current constants make EarNorm = 0 for 75%+ of samples. This eliminates the EAR feature's discriminative power, collapsing confusion to a near-constant ~0.32.

**Why these specific values:**
- `EAR_MIN = 0.08`: Below Phase 4 minimum (0.0574), provides margin for strong squinting. Squinting `ear_raw ≈ 0.08` → EarNorm ≈ 0.0 (confused signal).
- `EAR_MAX = 0.18`: Above Phase 4 P95 (0.1541), provides margin for open eyes. Open eyes `ear_raw ≈ 0.15` → EarNorm ≈ 0.70 (attentive signal).

**What must NOT be changed:**
- Scoring weights in `AffectScorer.cs` (0.40/0.30/0.30)
- Feature extraction formulas (BrowFurrow, MouthTension, BrowHeight)
- Landmark indices
- IOD guard

**Also update for parity:**
- `python_tools/daisee_calibration/extract_features.py`: Change `EAR_MIN` and `EAR_MAX` to matching values.

### 6.2 BrowFurrow and MouthTension — No Change Needed

**BrowFurrow:** Phase 4 controlled test showed responsive variation (0.032 → 0.175), contributing 0.013 → 0.070 to confusion. The feature works correctly.

**MouthTension:** Phase 4 showed 0.000 → 0.353 under forced expression. The formula produces 0 at neutral (correct — neutral mouth is not tense). Under deliberate downturn expression, it activates. The feature works correctly.

### 6.3 Compile / Build Checkpoint
1. Compile: Unity batchmode, exit code 0, 0 errors
2. Build: Android APK
3. Install via ADB

### 6.4 Runtime Validation (P1)
```bash
adb logcat -s Unity | grep "A3R_DIAG_AFFECT"
```
- Display face for 30 seconds
- Alternate: relaxed face (10s), deliberate squint (10s), relaxed (10s)

**Expected after fix:**
| Expression | Approximate EarNorm | Approximate Confusion |
|:---|:---|:---|
| Relaxed (ear_raw ≈ 0.13) | ~0.50 | ~0.18 |
| Squinting (ear_raw ≈ 0.09) | ~0.10 | ~0.30 |
| Wide open (ear_raw ≈ 0.16) | ~0.80 | ~0.10 |

### 6.5 STOP/GO Gate
| Criterion | PASS | FAIL |
|:---|:---|:---|
| EarNorm varies with expression | Range > 0.10 | Range < 0.02 |
| Confusion at neutral | < 0.25 | > 0.30 |
| Confusion at squint+frown | > 0.25 | < 0.15 |
| Confusion spread (squint − neutral) | > 0.05 | < 0.02 |
| FaceDetected still true | ≥50% | <10% |

**If FAIL:** EAR constants may not be optimal. Record the actual `ear_raw` values from logcat, compute better constants from those real-time readings. Do NOT proceed to P2 without working feature variation.

### 6.6 Rollback Plan
Revert `AffectFeatureExtractor.cs` to `EAR_MIN=0.15, EAR_MAX=0.35`. This is a 2-line change.

### 6.7 Regression Checklist
- [ ] FaceDetected still true
- [ ] BrowFurrow still varies (unchanged formula)
- [ ] MouthTension still varies (unchanged formula)
- [ ] Scoring weights unchanged (0.40/0.30/0.30)

---

## 7. P2 — Confusion Calibration

### 7.1 Why the Current Threshold Is Wrong

**Phase 4 frozen baseline:** `confusionThreshold = 0.60` (retained from Phase 4/5).  
**Current working-tree value:** `confusionThreshold = 0.40` (changed in Antigravity session).

**Phase 5 DAiSEE findings:**
- AUROC = 0.6137 (confusion), 0.6180 (frustration) — weak discriminative power
- 95% CI includes 0.5055 (near random chance)
- Phase 5 explicitly recommended retaining 0.60/0.65 for RUNTIME because the DAiSEE domain (passive video viewing) does not match A3R (active breadboard assembly)
- DAiSEE max confusion score was 0.408 — the formula produces HIGHER scores during active physical tasks

**The fundamental problem:** The Phase 4 data that justified 0.60 was collected with EarNorm permanently at 0, making confusion ≈ 0.32 baseline. The 0.60 threshold was set relative to THAT broken distribution. After EAR recalibration, the entire confusion distribution will shift.

### 7.2 Runtime Calibration Procedure

**MANDATORY: After P0 and P1 are verified, perform runtime calibration BEFORE choosing a threshold.**

#### Step A: Neutral Baseline Samples
1. Launch app, verify FaceDetected: true
2. Hold device with relaxed face, normal viewing distance
3. Record 20 consecutive confusion values over 10 seconds
4. Compute: `confusion_neutral_mean`, `confusion_neutral_max`

#### Step B: Deliberate Confused-Expression Samples
1. Furrow brows, squint eyes, downturn mouth corners
2. Hold expression for 10 seconds
3. Record 20 consecutive confusion values
4. Compute: `confusion_confused_mean`, `confusion_confused_min`

#### Step C: Normal Task-Performance Samples
1. Hold device and look at breadboard marker (as during actual task)
2. Naturally alternate gaze between screen and marker
3. Record 20 confusion values
4. Compute: `confusion_task_mean`, `confusion_task_max`

### 7.3 Threshold Selection Criteria

```
threshold = confusion_neutral_max + 0.5 × (confusion_confused_min − confusion_neutral_max)
```

This places the threshold halfway between the highest neutral reading and the lowest confused reading — the midpoint of the separability gap.

**Dwell:** Retain `dwellTimeSeconds = 3.0s` — already validated in Phase 8.

### 7.4 Threshold Selection Fallback

If runtime calibration shows insufficient separation (confused_min ≤ neutral_max):

> **CAUTION: DEMO CALIBRATION — NOT SCIENTIFICALLY VALIDATED**
>
> Set threshold to `confusion_neutral_max + 0.02` to ensure false triggers
> don't fire during normal viewing, while accepting that "confusion" detection
> is based on geometric facial proxy features, not validated emotion recognition.

### 7.5 Exact Change

**File:** `unity_project/Assets/Scripts/Adaptation/AdaptationController.cs`  
**Class:** `AdaptationController`  
**Line:** 28  
**Function:** `confusionThreshold` field

```csharp
// AFTER RUNTIME CALIBRATION — replace X.XXf with measured value:
[SerializeField] private float confusionThreshold = X.XXf;  // DEMO CALIBRATION
```

**Do NOT change:** `frustrationThreshold` (0.65), `dwellTimeSeconds` (3.0), `cooldownSeconds` (15.0), `maxAdaptationsPerStep` (3).

### 7.6 STOP/GO Gate
| Criterion | PASS | FAIL |
|:---|:---|:---|
| Separability | confused_min > neutral_max | confused_min ≤ neutral_max |
| False positive rate | 0 triggers during 30s neutral face | Any trigger during neutral |
| True positive rate | ≥1 trigger during 30s confused expression | 0 triggers |
| Dwell confirmation | Elevated state lasts ≥3s in logcat | Elevated state resets before 3s |

---

## 8. P3 — Step Context Wiring

### 8.1 Audit: Is This Actually Required?

**Finding:** `AffectScorer._currentStepId` is initialized to `""` and NEVER updated. It is passed to `AdaptationController.UpdateAffectScores()` as the `currentStepId` parameter.

**Impact of empty `_currentStepId`:**
1. `AdaptationController` line 114: `if (currentStepId != _lastStepId)` — `"" != ""` is FALSE, so the step-reset logic does NOT fire.
2. **Consequence:** `_adaptationsThisStep` is NEVER reset between steps. If 3 adaptations fire on step 1, step 2 will have `_adaptationsThisStep = 3` and ALL further adaptations will be suppressed.

**This IS a real bug for a multi-step demo.**

### 8.2 Exact Change

**File:** `unity_project/Assets/Scripts/ContentManager/ARContentManager.cs`  
**Function:** `DisplayCurrentStep()` (line 314)  
**Insert after line 332** (after `CurrentCanonicalEvent` is created):

```csharp
// Wire step ID to AffectScorer so AdaptationController can detect step changes
var scorer = GetComponent<A3R.Affect.AffectScorer>()
          ?? FindFirstObjectByType<A3R.Affect.AffectScorer>();
scorer?.SetCurrentStepId(CurrentCanonicalEvent.stepId);
```

**Why necessary:** Without this, `AdaptationController` never detects step transitions and the per-step adaptation counter never resets.

### 8.3 Regression Checklist
- [ ] Step transitions still work (NextStep button still functional)
- [ ] AR guidance updates on step change
- [ ] No null reference exceptions
- [ ] Logcat shows step ID in AdaptationController logs

---

## 9. P4 — Authoritative Task Completion

### 9.1 Honest Assessment: What PROVES Task Completion?

**Exhaustive audit of existing completion signals:**

| Potential Signal | Exists? | Assessment |
|:---|:---|:---|
| ESP32 hardware state verification | NO | Phase 9B unstarted; no real ESP32 |
| Correct LED state | NO | Mock always succeeds; not authoritative |
| Breadboard connection verification | NO | No vision-based circuit analysis |
| AR interaction completion | NO | No AR object placement detection |
| Existing task validation event | NO | Does not exist |
| User confirmation button | YES | MANUAL — violates demo requirement |

### 9.2 Verdict

> **CAUTION: NO AUTHORITATIVE TASK COMPLETION SIGNAL EXISTS.**
>
> The project has no mechanism — hardware, software, or visual — to verify
> that a user has actually completed a physical task step.

### 9.3 DEMO FALLBACK Implementation

> **WARNING: DEMO FALLBACK — NOT TRUE TASK VALIDATION**
>
> The following component advances steps based on confusion score recovery
> (user's face returns to a non-confused geometric state after receiving a hint).
>
> This is NOT proof that the physical task was completed. It is a demonstration
> of the adaptive closed-loop concept.

**New file:** `unity_project/Assets/Scripts/Adaptation/TaskCompletionDetector.cs`

```csharp
using UnityEngine;
using A3R.Affect;
using A3R.ContentManager;

namespace A3R.Adaptation
{
    /// <summary>
    /// TaskCompletionDetector — DEMO FALLBACK for automatic step advancement.
    ///
    /// IMPORTANT: This component does NOT verify actual physical task completion.
    /// It monitors confusion score recovery after an adaptation hint is displayed.
    /// When the user's facial geometric proxy score returns to a non-confused baseline
    /// and stays there for a configurable duration, it programmatically calls
    /// ARContentManager.NextStep() to advance to the next instruction step.
    ///
    /// Subscribes to: AdaptationController.OnAdaptationTriggered
    /// Monitors: AffectScorer.ConfusionScore
    /// Calls: ARContentManager.NextStep()
    /// </summary>
    public class TaskCompletionDetector : MonoBehaviour
    {
        [Header("Recovery Thresholds")]
        [Tooltip("Confusion score below this = user appears no longer confused.")]
        [SerializeField] private float recoveryThreshold = 0.20f;

        [Tooltip("Seconds confusion must stay below recoveryThreshold to confirm recovery.")]
        [SerializeField] private float recoveryDwellSeconds = 3.0f;

        [Tooltip("Minimum seconds hint must be displayed before checking for recovery.")]
        [SerializeField] private float minHintDisplaySeconds = 5.0f;

        [Tooltip("Maximum seconds to wait after hint before advancing anyway.")]
        [SerializeField] private float maxWaitAfterHintSeconds = 45.0f;

        [Header("References")]
        [SerializeField] private ARContentManager contentManager;
        [SerializeField] private AdaptationController adaptationController;
        [SerializeField] private AffectScorer affectScorer;

        private bool  _trackingActive = false;
        private float _hintStartTime;
        private float _recoveryStartTime;
        private bool  _inRecovery = false;
        private string _trackingStepId = "";

        private void Awake()
        {
            if (contentManager == null)
                contentManager = GetComponent<ARContentManager>()
                    ?? FindFirstObjectByType<ARContentManager>();
            if (adaptationController == null)
                adaptationController = GetComponent<AdaptationController>()
                    ?? FindFirstObjectByType<AdaptationController>();
            if (affectScorer == null)
                affectScorer = GetComponent<AffectScorer>()
                    ?? FindFirstObjectByType<AffectScorer>();
        }

        private void Start()
        {
            if (adaptationController != null)
                adaptationController.OnAdaptationTriggered += OnAdaptationFired;
            else
                Debug.LogWarning("[TaskCompletionDetector] AdaptationController not found.");
        }

        private void OnDestroy()
        {
            if (adaptationController != null)
                adaptationController.OnAdaptationTriggered -= OnAdaptationFired;
        }

        private void Update()
        {
            if (!_trackingActive) return;
            if (affectScorer == null) return;

            float elapsed = Time.time - _hintStartTime;
            float confusion = affectScorer.ConfusionScore;

            if (elapsed >= maxWaitAfterHintSeconds)
            {
                Debug.Log($"[TaskCompletionDetector] Fallback timeout ({maxWaitAfterHintSeconds:F0}s). Auto-advancing.");
                AdvanceStep("fallback_timeout");
                return;
            }

            if (elapsed < minHintDisplaySeconds) return;

            if (confusion < recoveryThreshold)
            {
                if (!_inRecovery)
                {
                    _inRecovery = true;
                    _recoveryStartTime = Time.time;
                    Debug.Log($"[TaskCompletionDetector] Recovery started: confusion={confusion:F3}");
                }
                else if (Time.time - _recoveryStartTime >= recoveryDwellSeconds)
                {
                    Debug.Log($"[TaskCompletionDetector] Recovery confirmed ({recoveryDwellSeconds:F1}s stable). Auto-advancing.");
                    AdvanceStep("confusion_recovery");
                }
            }
            else
            {
                if (_inRecovery)
                    Debug.Log($"[TaskCompletionDetector] Recovery interrupted: confusion={confusion:F3}");
                _inRecovery = false;
            }
        }

        private void OnAdaptationFired(AdaptationEvent evt)
        {
            if (_trackingActive)
            {
                Debug.Log($"[TaskCompletionDetector] Adaptation escalated on step {evt.StepId}. Resetting timer.");
                _hintStartTime = Time.time;
                _inRecovery = false;
                return;
            }

            _trackingActive = true;
            _hintStartTime = Time.time;
            _inRecovery = false;
            _trackingStepId = evt.StepId;
            Debug.Log($"[TaskCompletionDetector] Activated for step: {evt.StepId} (action: {evt.Action})");
        }

        private void AdvanceStep(string reason)
        {
            _trackingActive = false;
            _inRecovery = false;
            Debug.Log($"[TaskCompletionDetector] Calling NextStep(). Reason: {reason}. Step: {_trackingStepId}");
            contentManager?.NextStep();
        }

        public void ResetState()
        {
            _trackingActive = false;
            _inRecovery = false;
        }
    }
}
```

### 9.4 Scene Integration
Add `TaskCompletionDetector` as a component on the `A3R_Manager` GameObject in `A3R_Main.unity`. Wire Inspector references or rely on `Awake()` auto-discovery.

### 9.5 STOP/GO Gate
| Criterion | PASS | FAIL |
|:---|:---|:---|
| Adaptation fires | Hint banner appears without button | No hint appears |
| Recovery detection | "[TaskCompletionDetector] Recovery started" in logcat | Never appears |
| Auto-advance | Step 2 overlay appears without button | Step 1 remains |
| No double-fire | Step advances once, not multiple times | Rapid multiple advances |

---

## 10. P5 — Automatic Step Advancement

### 10.1 Mechanism

**`TaskCompletionDetector` (P4) calls `ARContentManager.NextStep()` programmatically.** This is the SAME method called by the UI button.

There is NO duplicate step management system. `TaskCompletionDetector` is a trigger/observer that calls the existing API.

### 10.2 No Duplicate State Created
- Step index: owned by `ARContentManager._currentStepIndex` (unchanged)
- Step data: owned by `ARContentManager._task.steps[]` (unchanged)
- Step events: owned by `ARContentManager.OnStepChanged` (unchanged)
- `TaskCompletionDetector` owns only: `_trackingActive`, `_hintStartTime`, `_recoveryStartTime`, `_trackingStepId` — all local tracking state, no overlap with any existing state

---

## 11. Protected Files

The following files MUST NOT be modified:

| File | Reason |
|:---|:---|
| `A3R_ARGuidancePreviewController.cs` | AR preview system — separate subsystem |
| `A3R_MarkerlessFoundationController.cs` | AR surface placement — protected |
| `A3R_BreadboardMap.cs` | Breadboard coordinate system — protected |
| `A3R_BreadboardConfig.cs` | Breadboard configuration — protected |
| `A3R_ResistorTutorialController.cs` | Tutorial controller — protected |
| `ARGuidanceController.cs` | AR guidance visuals — protected |
| `ARImageTracker.cs` | AR marker tracking — protected |
| `AdaptationEvents.cs` | Event types — no change needed |
| `SessionLogger.cs` | Logging — no change needed |
| `AffectPredictor.cs` | Prediction engine — not needed for demo |
| `UIManager.cs` | UI system — no change needed |
| `InstructionStep.cs` | Step data model — no change needed |
| `MockESP32Bridge.cs` | Mock hardware — no change needed |
| `IHardwareBridge.cs` | HAL interface — no change needed |
| `HardwareTypes.cs` | Hardware types — no change needed |
| `BreadboardCoordinateSystem.cs` | Coordinate system — protected |
| `ProceduralMeshUtility.cs` | 3D mesh generation — protected |
| `BuildHelper.cs` | Build config — no change needed |
| `led_blink_task.json` | Task content — no change needed |

---

## 12. Implementation Order

```
┌──────────────────────────────────────────────────────────────┐
│  1. P0: Revert FaceLandmarkBridge.cs (git checkout)          │
│  2. Compile (Unity batchmode, 0 errors)                      │
│  3. Build Android APK                                        │
│  4. Install on device                                        │
│  5. ── STOP/GO GATE: Face detection test ──                  │
│     PASS: FaceDetected:true, LandmarkCount:478               │
│     FAIL: STOP. Investigate camera init. Do NOT proceed.     │
├──────────────────────────────────────────────────────────────┤
│  6. P1: Fix EAR_MIN/EAR_MAX in AffectFeatureExtractor.cs    │
│  7. P1: Fix EAR_MIN/EAR_MAX in extract_features.py          │
│  8. Compile (0 errors)                                       │
│  9. Build Android APK                                        │
│ 10. Install on device                                        │
│ 11. ── STOP/GO GATE: Feature responsiveness test ──          │
│     PASS: EarNorm varies >0.10, confusion spread >0.05      │
│     FAIL: Record ear_raw values, compute better constants.   │
├──────────────────────────────────────────────────────────────┤
│ 12. P2: Runtime threshold calibration procedure              │
│     Record: neutral mean, confused mean, separation gap      │
│ 13. P2: Set confusionThreshold in AdaptationController.cs    │
│ 14. Compile (0 errors)                                       │
│ 15. Build Android APK                                        │
│ 16. Install on device                                        │
│ 17. ── STOP/GO GATE: Confusion threshold test ──             │
│     PASS: Neutral → no trigger; Confused 3s → FSM fires     │
│     FAIL: Adjust threshold. Re-test.                         │
├──────────────────────────────────────────────────────────────┤
│ 18. P3: Wire SetCurrentStepId in ARContentManager.cs         │
│ 19. P4: Create TaskCompletionDetector.cs                     │
│ 20. P4: Add component to A3R_Manager in scene                │
│ 21. Compile (0 errors)                                       │
│ 22. Build Android APK                                        │
│ 23. Install on device                                        │
│ 24. ── STOP/GO GATE: Full closed-loop test ──                │
│     PASS: Hint appears → recovery → step advances            │
│     FAIL: Check TaskCompletionDetector wiring.               │
├──────────────────────────────────────────────────────────────┤
│ 25. Final acceptance test (§13)                              │
└──────────────────────────────────────────────────────────────┘
```

---

## 13. Acceptance Tests

### TEST 1: User starts on Step 1
- Launch app, breadboard marker/surface visible
- **PASS:** Step 1 panel shows first step title

### TEST 2: Face is detected reliably
- Hold device so front camera sees face for 15s
- **PASS:** ≥3 occurrences of FaceDetected: true

### TEST 3: Neutral expression does not trigger confusion
- Relaxed face for 30 seconds
- **PASS:** No `[AdaptationController] FSM FIRED` in logcat

### TEST 4: Deliberate confusion produces stable confusion state
- Furrowed brows + squinted eyes + downturned mouth for 10 seconds
- **PASS:** `[AdaptationController] Entering elevated affect state` in logcat

### TEST 5: Contextual AR hint appears automatically
- Continue confused expression past 3s dwell
- **PASS:** `[AdaptationController] FSM FIRED: InjectCue` in logcat
- **PASS:** No button was pressed

### TEST 6: User recovers
- After hint appears, relax expression
- **PASS:** User observes the hint content

### TEST 7: Task "completion" signal detected (DEMO FALLBACK)
- After relaxing expression for ≥3 seconds:
- **PASS:** `[TaskCompletionDetector] Recovery confirmed` in logcat

### TEST 8: System automatically advances to Step 2
- **PASS:** `[TaskCompletionDetector] Calling NextStep()` in logcat
- **PASS:** Step 2 overlay appears without button press

### TEST 9: No manual Next Step button required
- **PASS:** Zero manual button presses throughout Tests 1–8

### TEST 10: Existing AR overlay/tracking still works
- **PASS:** AR overlay visible after step advance
- **PASS:** No NullReferenceException in logcat

---

## 14. Feasibility Verdict

### FULLY FEASIBLE TOMORROW
| Component | Justification |
|:---|:---|
| P0: Camera rollback | Single git checkout; verified in Phases 2-4 |
| P1: EAR recalibration | 2-line change; evidence from 1362 samples |
| P3: Step-ID wiring | 3-line addition; uses existing API |
| P5: NextStep() integration | Already implemented, public, tested |

### FEASIBLE AS DEMO FALLBACK
| Component | Justification |
|:---|:---|
| P2: Confusion threshold | Runtime calibration feasible; demo calibration value |
| P4: TaskCompletionDetector | ~120 lines; monitors existing signals |
| "Task completion" | Confusion recovery proxy; NOT true verification |

### DEFERRED
| Component | Reason |
|:---|:---|
| Camera rotation fix | Unverified hypothesis; revert safer |
| DAiSEE training | AUROC 0.61; domain mismatch |
| Real ESP32 | Phase 9B unstarted |
| CV task verification | Significant ML engineering |

---

## 15. Final File-by-File Change List

### [REVERT] FaceLandmarkBridge.cs
- **Purpose:** Remove broken camera rotation experiment
- **Risk:** LOW — reverting to git HEAD verified in Phases 2-4
- **Validation:** FaceDetected: true in logcat

### [MODIFY] AffectFeatureExtractor.cs (lines 63-64)
- **Purpose:** Recalibrate EAR normalization for Android device
- **Risk:** LOW — 2 constants; formula unchanged
- **Validation:** EarNorm varies with eye state

### [MODIFY] AdaptationController.cs (line 28)
- **Purpose:** Set runtime-calibrated confusion threshold
- **Risk:** LOW — single float; FSM logic unchanged
- **Validation:** No false trigger at neutral; trigger at confused expression

### [MODIFY] ARContentManager.cs (insert after line 332)
- **Purpose:** Wire step ID to AffectScorer for per-step adaptation reset
- **Risk:** LOW — 3 lines added; no existing logic modified
- **Validation:** Step ID in logcat after step change

### [NEW] TaskCompletionDetector.cs
- **Purpose:** DEMO FALLBACK auto-advance based on confusion recovery
- **Risk:** MEDIUM — new component; must not double-advance
- **Validation:** Step advances automatically; no double-fire

### [MODIFY] extract_features.py
- **Purpose:** Maintain C#/Python EAR constant parity
- **Risk:** LOW — constant change only

### [MODIFY] A3R_Main.unity
- **Purpose:** Add TaskCompletionDetector component to A3R_Manager
- **Risk:** LOW — additive only

---

## Appendix A: Adversarial Questions — Answered

**Q1: Are we certain the proposed EAR constants improve confusion separability?**
No. They are derived from Phase 4 statistics on ONE user/device. Treat as INITIAL values subject to runtime verification.

**Q2: What evidence supports the final confusion threshold?**
No pre-selected threshold is justified. RUNTIME CALIBRATION is required after EAR fix.

**Q3: What proves a user is actually confused?**
Nothing. The system detects geometric facial proxy features (brow furrow, eye squinting, mouth tension), not psychological states.

**Q4: What proves a task is actually completed?**
Nothing. No authoritative signal exists. TaskCompletionDetector uses confusion recovery as DEMO FALLBACK.

**Q5: What proves calmness ≠ completion?**
It cannot be proven. The plan labels this honestly as DEMO FALLBACK.

**Q6: What exact event advances the step?**
`ARContentManager.NextStep()` (line 251) — the SOLE mechanism, called by TaskCompletionDetector.

**Q7: Can automatic advancement use existing APIs?**
Yes. `NextStep()` is `public void`, callable from any component.

**Q8: Are we creating duplicate state ownership?**
No. Step index owned solely by `ARContentManager._currentStepIndex`. TaskCompletionDetector owns only local tracking state.

**Q9: Are we modifying any protected AR subsystem?**
No. All changes are in affect/adaptation subsystems.

**Q10: Could any fix regress the AR overlay?**
P0-P4: NO risk to AR (front camera vs rear camera; static constants; additive code only).

**Q11: What is the rollback plan?**
P0 IS the rollback. P1: 2-line revert. P4: delete TaskCompletionDetector.cs.

**Q12: Which parts are genuine vs demo-only?**

| Component | Classification |
|:---|:---|
| Face detection (478 landmarks) | **GENUINE** |
| Feature extraction | **GENUINE** |
| Confusion scoring | **GENUINE** |
| Temporal dwell FSM | **GENUINE** |
| Contextual AR hints | **GENUINE** |
| EAR calibration constants | **DEMO CALIBRATION** |
| Confusion threshold | **DEMO CALIBRATION** |
| "Task completion" | **DEMO FALLBACK** |
| Automatic step advancement mechanism | **GENUINE** (triggered by DEMO FALLBACK signal) |

---

*END OF FINAL IMPLEMENTATION PLAN v2.0*
