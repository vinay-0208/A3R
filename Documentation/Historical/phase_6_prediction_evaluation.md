# A³R — Phase 6: Real Prediction System (Precursor Dynamics & Lead-Time Evaluation)

**Status:** PASS — FUNCTIONALLY VALIDATED & AUDITED / FROZEN  
**Date:** 2026-09-13  
**Components:** `unity_project/Assets/Scripts/Affect/AffectPredictor.cs`, `SessionLogger.cs`, `ARContentManager.cs`, `A3R_Main.unity`, `python_tools/prediction/predict_affect.py`, `python_tools/prediction/test_prediction_engine.py`, `python_tools/prediction/run_phase6_evaluation.py`  
**Test Suite:** 66/66 Python Unit Tests Passing (15/15 Phase 6 Prediction Tests + 51/51 Phase 5 Calibration Tests). Unity Compilation: 0 C# Errors.

---

## 1. Executive Summary & Verdict

Phase 6 implements and functionally validates a **deterministic temporal prediction engine (Module 3: Anticipation & Deterministic Prediction Engine)** for the A³R project. 

In strict adherence to research integrity guidelines and scientific constraints:
- **Zero Machine Learning:** No black-box neural networks, no PyTorch/TensorFlow runtimes, and no scikit-learn models were introduced or trained.
- **Deterministic & Interpretable:** The anticipation engine uses bounded rolling-window linear regression ($K=5$ samples, spanning $\approx 2.5\text{ s}$ at $\sim 1.74\text{ Hz}$) over calibrated geometric facial features (brow furrow acceleration, EAR narrowing velocity, mouth tension rate of change, and polarity hesitation markers).
- **Clear Separation:** Scripted anticipation events (anticipation before automated detection threshold crossing with positive evaluation interval $t_{\text{lead}} = t_{\text{scripted\_event}} - t_{\text{pred}} > 0$) are strictly distinguished from detection events (threshold crossings after sustained dwell) and technical disruptions (loss of camera tracking $\ge 1.5\text{ s}$ or hardware disconnection).
- **Persistence Gating:** A 2-sample persistence guard completely eliminates false alarms from single-frame noise spikes.
- **Controlled Evaluation Scope:** The 180-sequence evaluation represents a controlled engineered temporal stress test. It validates rule behavior under explicitly scripted temporal patterns. It does not establish human affect-prediction accuracy, psychological validity, or real-world human ground-truth lead time.
- **Threshold Integrity:** Frozen Phase 4/5 thresholds ($T_{\text{confusion}}=0.60, T_{\text{frustration}}=0.65, D=3.0\text{ s}$, cooldown $15.0\text{ s}$, max adaptations per step = 3) were strictly preserved without modification.

**Verdict: PASS — FUNCTIONALLY VALIDATED & AUDITED / PHASE 6 FROZEN**

---

## 2. Audit of Previous / Existing Prediction Code

Before implementation, an exhaustive audit was conducted across the codebase:
1. `unity_project/Assets/Scripts/Affect/AffectPredictor.cs`:
   - Prior to Phase 6, contained an early 2-point derivative prototype that lacked a bounded temporal window, persistence gating, frustration precursor dynamics, recovery logic, and tracking loss monitoring.
   - It was not attached to `A3R_Manager` or wired to `ARContentManager` in `A3R_Main.unity`.
2. `unity_project/Assets/Scripts/Logging/SessionLogger.cs`:
   - Lacked full schema fields for prediction events (`evidence_score`, `trend_slope`, `confusion_score`, `frustration_score`, `is_simulated`, `is_technical_disruption`, `prediction_state`).
3. `python_tools/daisee_calibration/evaluate_prediction.py`:
   - Provided an initial offline simulation script without unit tests or scene binding.

---

## 3. Prediction Taxonomy & Behavioral States

The system establishes an unambiguous 5-state behavioral taxonomy:

| State Name | Classification | Anticipation Mode | Trigger Mechanism / Precursors |
| :--- | :--- | :--- | :--- |
| **`ConfusionStruggle`** | Behavioral Affect | **PREDICTIVE** | Precursor floor–ceiling $[0.35, 0.55]$, confusion slope $\ge 0.05/\text{s}$, or brow velocity $\ge 0.10/\text{s}$ with eye squinting $\le -0.05/\text{s}$. Persistent for $\ge 2$ samples. |
| **`FrustrationDisengage`**| Behavioral Affect | **PREDICTIVE** | Precursor floor–ceiling $[0.35, 0.60]$, frustration slope $\ge 0.05/\text{s}$, or mouth tension velocity $\ge 0.08/\text{s}$. Persistent for $\ge 2$ samples. |
| **`PolarityHesitation`**  | Behavioral Affect | **PREDICTIVE** | Step 3 (LED insertion) micro-squint or co-activation of competing affect ($|C - E| \le 0.12, \sigma_C \ge 0.05$). |
| **`TrackingLossDisruption`** | Technical Disruption | **DETECTION-ONLY** | Front camera face undetected duration $\ge 1.5\text{ s}$ (`AffectScorer.FaceDetected == false`). Not an affect precursor. |
| **`HardwareDisconnection`** | Technical Disruption | **DETECTION-ONLY** | ESP32 telemetry packet loss / connection timeout (pending Phase 9). |

---

## 4. Architecture & Temporal Dynamics

- **Rolling Window:** Bounded FIFO queue of capacity $K=5$ samples ($\approx 2.5–2.9\text{ s}$ at $1.74\text{ Hz}$).
- **Minimum Sampling Requirement:** $\ge 3$ valid samples and $\Delta t \ge 0.4\text{ s}$ required before calculating trend slopes.
- **Slope Formulation:** Linear regression ordinary least squares:
  $$\frac{dY}{dt} = \frac{\sum (t_i - \bar{t})(Y_i - \bar{Y})}{\sum (t_i - \bar{t})^2}$$
- **Persistence Gating:** Requires $\ge 2$ consecutive evaluations meeting precursor criteria before firing `PREDICTED` state, preventing transient single-frame spikes from triggering false adaptations.
- **Cooldown Suppression:** $5.0\text{ s}$ cooldown prevents event spamming for identical predictions.
- **Recovery Logic:** When scores drop below $0.30$, state transitions to `RECOVERY` and pending predictions are safely cancelled.
- **Lead-Time Formulation:** 
  - **Implementation Evaluation Interval:** $t_{\text{lead}} = t_{\text{scripted\_event}} - t_{\text{prediction}}$. Measured strictly under scripted sequence evaluation where $t_{\text{scripted\_event}} > t_{\text{prediction}}$.
  - **Human Empirical Lead Time:** Requires independent micro-onset annotations of human affect during physical assembly. **Human empirical lead time has not been established.**

---

## 5. Unit Test Coverage & Verification Results

All 15 rigorous test conditions (A through O) implemented in `python_tools/prediction/test_prediction_engine.py` passed:

- **Condition A (Stable Neutral Sequence):** Constant neutral face ($\text{conf}=0.1, \text{frust}=0.1$) -> 0 predictions fired (**PASS**).
- **Condition B (Rising Confusion Sequence):** Rising ramp triggers `ConfusionStruggle` with positive lead interval (**PASS**).
- **Condition C (Rising Frustration Sequence):** Rising mouth tension and frustration slope triggers `FrustrationDisengage` (**PASS**).
- **Condition D (Brief Confusion Spike):** Single-sample spike ($0.15 \to 0.55 \to 0.15$) rejected by persistence guard; 0 predictions (**PASS**).
- **Condition E (Brief Frustration Spike):** Single-sample spike ($0.10 \to 0.58 \to 0.10$) rejected; 0 predictions (**PASS**).
- **Condition F (Recovery Sequence):** Precursor followed by score dropping below $0.30$ clears state to `RECOVERY` (**PASS**).
- **Condition G (Cooldown Suppression):** Rapid re-trigger within $5.0\text{ s}$ suppressed (**PASS**).
- **Condition H (Tracking Loss Disruption):** Missing face for $2.0\text{ s}$ ($\ge 1.5\text{ s}$) triggers `TrackingLossDisruption` (**PASS**).
- **Condition I (Tracking Recovery):** Resumed face detection immediately resets `TRACKING_LOST` state to `IDLE` (**PASS**).
- **Condition J (Irregular Sampling):** Jittered intervals ($0.2\text{ s}$ to $0.8\text{ s}$) correctly handled via timestamped regression (**PASS**).
- **Condition K (Insufficient History):** Sequences with $<3$ samples never fabricate predictions (**PASS**).
- **Condition L (NaN / Invalid Values):** Corrupted samples safely filtered without exceptions or state corruption (**PASS**).
- **Condition M (JSONL Schema Compliance):** Emitted records match required fields and types (**PASS**).
- **Condition N (is_simulated Field Integrity):** Strict propagation of simulation flags (`is_simulated: true` for offline tests, `false` for live camera) (**PASS**).
- **Condition O (Lead-Time Infrastructure):** Correct calculation of $t_{\text{actual}} - t_{\text{pred}} > 0$ and handling of invalid lead times (**PASS**).

**Test Suite Summary:**
- Phase 6 Prediction Tests: **15 / 15 PASSED**
- Phase 5 Calibration Tests: **51 / 51 PASSED**
- Total Automated Python Tests: **66 / 66 PASSED**

---

## 6. Controlled Engineered Temporal Evaluation Results (180 Sequences)

On 180 controlled engineered temporal sequences, the deterministic predictor achieved 1.000 precision, 0.870 recall, and 0.9305 F1. These figures characterize the implemented rule engine under scripted evaluation conditions and are not estimates of human affect-prediction accuracy.

### Scripted Evaluation Event Matrix & Performance Metrics
- **Total Controlled Sequences:** 180
- **True Positives (TP):** 87 (scripted target precursor successfully anticipated before scripted event onset)
- **False Positives (FP):** 0 (zero false triggers on negative controls or single-frame noise spikes)
- **False Negatives (FN):** 13 (steep onsets crossing threshold before 2-sample persistence accumulation)
- **True Negatives (TN):** 50 (30 neutral sequences + 20 transient noise spike sequences)
- **Precision:** **1.0000** (100.0%)
- **Recall:** **0.8700** (87.0%)
- **F1 Score:** **0.9305**
- **Specificity:** **1.0000** (100.0% rejection of negative controls and transient spikes)

*Matrix Interpretation:*
- **Positive:** Scripted target temporal pattern leading to an automated threshold-crossing event.
- **Negative:** Scripted negative baseline or noise sequence.
- These do NOT represent human ground-truth labels.

### Implementation Anticipation Intervals ($t_{\text{lead}} = t_{\text{scripted\_event}} - t_{\text{pred}}$)
Among 87 controlled anticipation cases, the mean interval between predictor firing and the scripted/automated event onset was 2.483 s. This is an implementation-level anticipation interval, not human-ground-truth lead time.

- **Controlled Anticipation Count:** 87 scripted evaluations
- **Mean Anticipation Interval:** **2.483 seconds**
- **Median Anticipation Interval:** **2.000 seconds**
- **Standard Deviation:** **1.087 seconds**
- **Minimum Anticipation Interval:** **1.000 seconds**
- **Maximum Anticipation Interval:** **5.000 seconds**

### Breakdown by Category under Scripted Evaluation
1. **Confusion Struggle:** 40 scripted sequences | 27 TP, 0 FP, 13 FN | Mean Anticipation Interval: **2.074 s**
2. **Frustration Disengage:** 40 scripted sequences | 40 TP, 0 FP, 0 FN | Mean Anticipation Interval: **3.000 s**
3. **Polarity Hesitation:** 20 scripted sequences | 20 TP, 0 FP, 0 FN | Mean Anticipation Interval: **2.000 s**
4. **Neutral Negative Control:** 30 sequences | 30 TN, 0 FP (100% negative rejection)
5. **Noise Spike Rejection:** 20 sequences | 20 TN, 0 FP (100% transient spike rejection)
6. **Tracking Loss Disruption:** 20 sequences | 20/20 triggered after 1.5s, 20/20 recovered to IDLE
7. **Recovery Reset:** 10 sequences | 10/10 successfully transitioned to RECOVERY

---

## 7. Unity Integration & Compilation

1. **C# Implementation:**
   - `AffectPredictor.cs`: Complete deterministic predictor implementing bounded rolling window ($K=5$), persistence gating ($\ge 2$), recovery floor ($0.30$), tracking loss monitoring on `AffectScorer.FaceDetected`, and lead-time tracking.
   - `SessionLogger.cs`: Added `PredictionRecord` schema extension and overloaded `LogPrediction(...)`.
   - `ARContentManager.cs`: Serialized `affectPredictor` reference with automatic fallback resolution in `Awake()`.
2. **Scene Binding in `A3R_Main.unity`:**
   - Added `AffectPredictor` MonoBehaviour (`fileID: 300000010`) to GameObject `A3R_Manager` (`fileID: 300000000`).
   - Wired `ARContentManager.affectPredictor: {fileID: 300000010}`.
   - Wired `affectScorer: {fileID: 300000006}` and `sessionLogger: {fileID: 300000008}`.
3. **Compilation Verification:**
   - Batchmode compilation via Unity 6000.3.23f1: `LogAssemblyErrors (0ms)` with **0 C# compiler errors**.

---

## 8. System Limitations & Research Integrity Boundaries

1. **Deterministic Heuristic Proxy:** Precursor dynamics are rule-based linear regression models over engineered geometric facial features. They do not represent cognitive mind-reading or neurological state detection.
2. **Human Empirical Validation Status:** Human empirical lead time has not been established. DAiSEE provides clip-level macro labels but does not provide sufficiently precise temporal onset annotations for human lead-time measurement.
3. **Final Phase 6 Limitation Statement:**
   > **The Phase 6 predictor was functionally validated using deterministic unit tests and controlled engineered temporal sequences. The resulting precision, recall, F1, specificity, and anticipation intervals characterize behavior under those scripted evaluation conditions only. They are not estimates of human affect-prediction accuracy. DAiSEE provides clip-level affect annotations but does not provide sufficiently precise temporal onset annotations for human lead-time measurement. Establishing real human prediction accuracy and lead time requires a future controlled experiment with independently annotated event onset.**
4. **Threshold Preservation:** Active runtime thresholds remain frozen at $T_{\text{confusion}}=0.60, T_{\text{frustration}}=0.65, D=3.0\text{ s}$, cooldown $15.0\text{ s}$, max adaptations $3$.
5. **Phase Status:** Phases 0, 1, 2, 3, 4, 5, and 6 are complete and frozen. Phase 7 has NOT been started.
