# Phase 5 Report: DAiSEE Calibration Readiness & Empirical Audit

## 1. Objective

Phase 5 addresses the empirical threshold and temporal parameter calibration for the **A³R — Affect-Adaptive Augmented Reality** system. Specifically, it audits whether the existing heuristic affect-scoring thresholds ($T_{\text{confusion}} = 0.60, T_{\text{frustration}} = 0.65$) and temporal dwell parameter ($D = 3.0\text{ s}$) can be empirically calibrated and optimized against the ground-truth behavioral annotations of the **DAiSEE (Dataset for Affective States in E-Environments)** video benchmark.

Per strict research integrity rules:
- No synthetic datasets or fabricated labels are permitted.
- If the real DAiSEE dataset is not present in the local environment, empirical fitting must be halted immediately.
- A comprehensive Calibration-Readiness Report must be generated detailing the verified tooling, mathematical foundations, protocol, and exact commands required once the dataset is mounted.
- **Phase 5 status is evaluated as BLOCKED / CALIBRATION-READINESS VERIFIED**, preserving the frozen heuristic baseline from Phase 4.

---

## 2. DAiSEE Dataset Provenance & Specification

- **Dataset Name**: DAiSEE (Dataset for Affective States in E-Environments)
- **Creators / Citation**: A. Gupta, A. D'Cunha, K. Awasthi, V. Balasubramanian (Indian Institute of Technology Hyderabad).
- **Official Repository**: `https://people.iith.ac.in/vineethnb/resources/daisee/index.html`
- **Dataset Composition**: 9,068 video snippets (10 seconds each, 1920×1080 @ 30 fps, AVI format) recorded from 112 unique Asian-Indian human subjects in natural e-learning environments under unconstrained real-world illumination and pose variations.
- **Official Partitions**:
  - **Train**: ~5,358 clips
  - **Validation**: ~1,429 clips
  - **Test**: ~1,784 clips
- **Local Filesystem Status**: **UNAVAILABLE / NOT DETECTED ON LOCAL STORAGE**
  - Audited paths: `C:\datasets\DAiSEE`, `D:\datasets\DAiSEE`, user profile folders (`Downloads`, `Documents`), and workspace subdirectories.
  - Environment variable `DAISEE_ROOT`: Unset.
  - Research Action: Empirical fitting halted; zero synthetic data fabricated.

---

## 3. Dataset Size & Structural Requirements

When mounted, the DAiSEE dataset must adhere to the standardized directory layout supported by `python_tools/daisee_calibration/load_daisee.py`:

```
<DAISEE_ROOT>/
├── DataSet/
│   ├── Train/
│   │   └── <SubjectID>/<ClipID>.avi
│   ├── Validation/
│   │   └── <SubjectID>/<ClipID>.avi
│   └── Test/
│       └── <SubjectID>/<ClipID>.avi
└── Labels/
    ├── TrainLabels.csv
    ├── ValidationLabels.csv
    └── TestLabels.csv
```

---

## 4. Label Definitions & Distinction from Psychological Emotions

DAiSEE annotators provided four affective/behavioral ratings on an ordinal scale:
- **Available Annotations**: `Boredom`, `Engagement`, `Confusion`, `Frustration`
- **Ordinal Scale**:
  - `0`: Very Low / None
  - `1`: Low
  - `2`: Moderate
  - `3`: High

### Crucial Scientific Distinction
A³R distinguishes between **annotated behavioral states** and **engineered geometric affect proxies**:
1. DAiSEE labels represent **observer-annotated behavioral manifestations** during video lecture watching, subject to inter-rater subjectivity and cultural display norms.
2. A³R affect scores are **deterministic linear combinations of normalized facial landmark distances** (corrugator contraction, palpebral opening, zygomatic/depressor corner tilt).
3. The calibration exercise evaluates whether engineered geometric proxies correlate usefully with external behavioral annotations; it **does NOT claim to detect internal psychological ground truth**.

---

## 5. Label Mapping & Ground-Truth Binarization

To optimize the decision boundary for adaptive interventions, ordinal DAiSEE labels must be mapped to binary trigger ground-truth without inventing synthetic semantics:
- **Target Dimensions**: `Confusion` and `Frustration`
- **Binarization Rule**:
  $$\text{TargetPositive} = \begin{cases} 1, & \text{if } \text{LabelValue} \ge 2 \text{ (Moderate or High)} \\ 0, & \text{if } \text{LabelValue} \le 1 \text{ (None or Low)} \end{cases}$$
- **Scientific Justification**: In an intelligent tutoring system, instructional adaptations (e.g. `InjectCue`, `InsertHint`) should trigger only when a learner exhibits sustained moderate-to-high struggle, avoiding disruptive interruptions during low/fleeting curiosity or momentary eye twitches.

---

## 6. Data Preprocessing & Video Frame Pipeline

The offline extraction orchestrator (`batch_extract.py` and `phase5_calibrate.py`) processes raw videos through the following verified pipeline:
1. **Frame Ingestion**: Video reader decodes video at `sample_fps = 5.0 Hz` (one frame every 200 ms).
2. **Color Space**: BGR frames converted to sRGB for MediaPipe Image buffers.
3. **Face Mesh Detection**: MediaPipe Face Landmarker task executes in `IMAGE` mode.
4. **Failure Policy**:
   - Multiple faces: Primary face with largest bounding box selected.
   - Missing faces / tracking loss: Marked as `face_detected = False`; NaN assigned to features; sample preserved in CSV.
   - Degenerate landmarks (IOD $< 10^{-6}$ or Face Height $< 10^{-6}$): Excluded from scoring.

---

## 7. Face & Landmark Extraction Methodology

The pipeline utilizes the identical dense 478-point landmark mesh topology deployed on-device:
- **Model**: `face_landmarker.task` (Float16 dense mesh model, 3,758,596 bytes, verified in `Assets/StreamingAssets/`).
- **Inference Delegate**: CPU / XNNPACK delegate for reproducible deterministic numerical evaluation across platforms.

---

## 8. Existing A³R Feature Definitions

Features extracted per frame strictly match `AffectFeatureExtractor.cs`:

1. **Inter-Ocular Distance (IOD Anchor)**:
   $$\text{IOD} = \|\mathbf{p}_{33} - \mathbf{p}_{263}\|_2$$
2. **Brow Furrow Distance (Inverted & Normalized)**:
   $$\text{BrowFurrow}_{\text{norm}} = \text{clamp}_{0,1}\left(1.0 - \frac{\|\mathbf{p}_{105} - \mathbf{p}_{334}\|_2}{\text{IOD}}\right)$$
3. **Eye Aspect Ratio (EAR)**:
   $$\text{EAR}_{\text{raw}} = \frac{1}{2}\left(\frac{\|\mathbf{p}_{159} - \mathbf{p}_{145}\|_2}{2 \|\mathbf{p}_{133} - \mathbf{p}_{33}\|_2} + \frac{\|\mathbf{p}_{386} - \mathbf{p}_{374}\|_2}{2 \|\mathbf{p}_{362} - \mathbf{p}_{263}\|_2}\right)$$
   $$\text{EAR}_{\text{norm}} = \text{clamp}_{0,1}\left(\frac{\text{EAR}_{\text{raw}} - 0.15}{0.35 - 0.15}\right)$$
4. **Mouth Corner Tension**:
   $$\text{Lip}_{\text{mid\_y}} = \frac{\mathbf{p}_{13,y} + \mathbf{p}_{14,y}}{2}, \quad \text{Disp} = \frac{(\text{Lip}_{\text{mid\_y}} - \mathbf{p}_{61,y}) + (\text{Lip}_{\text{mid\_y}} - \mathbf{p}_{291,y})}{2}$$
   $$\text{MouthTension}_{\text{norm}} = \text{clamp}_{0,1}\left(\frac{-\text{Disp}}{\text{IOD} + 10^{-6}} \cdot 3.0\right)$$
5. **Brow Height**:
   $$\text{BrowHeight}_{\text{norm}} = \text{clamp}_{0,1}\left(\frac{|\mathbf{p}_{65,y} - \mathbf{p}_{159,y}| + |\mathbf{p}_{295,y} - \mathbf{p}_{386,y}|}{2 \cdot (|\mathbf{p}_{152,y} - \mathbf{p}_{10,y}| + 10^{-6})} \cdot 5.0\right)$$

---

## 9. Formula Parity Verification

The scoring equations in `score_affect_rulebased.py` are mathematically identical to `AffectScorer.cs`:
- **Confusion**:
  $$S_{\text{confusion}} = \text{clamp}_{0,1}(0.40 \cdot \text{Furrow} + 0.30 \cdot (1.0 - \text{EAR}_{\text{norm}}) + 0.30 \cdot \text{MouthTension})$$
- **Frustration**:
  $$S_{\text{frustration}} = \text{clamp}_{0,1}(0.35 \cdot \text{Furrow} + 0.25 \cdot (1.0 - \text{EAR}_{\text{norm}}) + 0.40 \cdot \text{MouthTension})$$
- **Engagement**:
  $$S_{\text{engagement}} = \text{clamp}_{0,1}(0.50 \cdot \text{EAR}_{\text{norm}} + 0.30 \cdot (1.0 - \text{Furrow}) + 0.20 \cdot \text{BrowHeight})$$

All scoring weights strictly sum to $1.00$. Parity was verified across 47 automated tests with zero floating-point discrepancies.

---

## 10. Subject-Independent Split Methodology

To guarantee zero data leakage:
1. **Subject Isolation**: Partitioning strictly follows DAiSEE's official subject boundaries (defined by leading subject identification numbers in clip names, e.g. `110001XXXX.avi`).
2. **Leakage Prohibition**: All frames and video snippets of any given subject reside exclusively in **one** partition (Train, Validation, or Test).
3. **Partition Allocation**:
   - **Training Set**: Used to generate candidate operating points.
   - **Validation Set**: Used to tune and freeze optimal $(T^*, D^*)$ parameters.
   - **Held-Out Test Set**: Kept completely untouched until parameter freeze; evaluated once for unbiased final reporting.

---

## 11. Calibration Protocol & Dwell FSM Simulation

For each video clip:
1. Per-frame affect scores $S(t)$ are computed at 5 Hz.
2. The dwell finite-state machine is simulated:
   $$\text{State}(t) = \begin{cases} \text{Elevated}, & \text{if } S(t) > T \\ \text{Neutral}, & \text{if } S(t) \le T \end{cases}$$
   $$\text{Timer}(t) = \begin{cases} \text{Timer}(t - \Delta t) + \Delta t, & \text{if Elevated} \\ 0, & \text{if Neutral} \end{cases}$$
3. The FSM registers a trigger if $\text{Timer}(t) \ge D$ at any point during the 10-second clip.
4. Clip prediction (`FSM_Fired` $\in \{0, 1\}$) is compared against ground truth (`Label` $\ge 2$).

---

## 12. Candidate Search Grids

- **Threshold Candidates ($T$)**:
  $$T \in [0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70, 0.75]$$
- **Dwell Time Candidates ($D$)**:
  $$D \in [1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 5.0]\text{ seconds}$$
- **Optimization Metric**: $F_{\beta}$ with $\beta = 0.5$:
  $$F_{0.5} = \frac{(1 + 0.5^2) \cdot \text{Precision} \cdot \text{Recall}}{0.5^2 \cdot \text{Precision} + \text{Recall}}$$
  *Weighting precision twice as heavily as recall ensures the learning system prioritizes pedagogical non-intrusiveness.*

---

## 13. Calibration Metrics Summary & Status

Because the physical DAiSEE dataset is unavailable locally, empirical numerical values cannot be calculated:

| Evaluation Phase | Confusion Threshold | Frustration Threshold | Dwell Time ($D$) | $F_{0.5}$ Score | Precision | Recall | Status |
|:---|:---|:---|:---|:---|:---|:---|:---|
| **Phase 4 Heuristic Baseline** | **0.60** | **0.65** | **3.0 s** | *Uncalibrated* | *Uncalibrated* | *Uncalibrated* | **ACTIVE / FROZEN** |
| **Phase 5 Calibrated (Train)** | Pending Dataset | Pending Dataset | Pending Dataset | Pending | Pending | Pending | **BLOCKED** |
| **Phase 5 Calibrated (Val)** | Pending Dataset | Pending Dataset | Pending Dataset | Pending | Pending | Pending | **BLOCKED** |
| **Phase 5 Held-Out Test** | Pending Dataset | Pending Dataset | Pending Dataset | Pending | Pending | Pending | **BLOCKED** |

---

## 14. Research Boundaries & Scientific Non-Claims

In adherence to absolute research integrity:
1. **NO Emotion Accuracy Claimed**: A³R makes zero claims regarding human emotion classification accuracy on DAiSEE.
2. **NO Synthetic Compromises**: The system refuses to fabricate synthetic DAiSEE annotations or invent calibration outcomes.
3. **Preserved Baseline**: The Phase 4 heuristic baseline ($T_{\text{confusion}}=0.60, T_{\text{frustration}}=0.65, D=3.0\text{s}$) remains active in `AdaptationController.cs` and `A3R_Main.unity`.
4. **Explicit Negative / Blocked Declaration**: The failure to locate the physical dataset is reported directly and transparently without evasion.

---

## 15. Reproducibility & Execution Instructions

Once the authenticated DAiSEE dataset is mounted:

```bash
# 1. Set environment variable
set DAISEE_ROOT=D:\datasets\DAiSEE

# 2. Run end-to-end automated calibration pipeline
python ARVR/python_tools/daisee_calibration/phase5_calibrate.py \
    --daisee_root "D:/datasets/DAiSEE" \
    --model_path "ARVR/unity_project/Assets/StreamingAssets/face_landmarker.task" \
    --sample_fps 5.0 \
    --output_dir "ARVR/evaluation"

# 3. Verify automated test suite
python -m pytest ARVR/python_tools/daisee_calibration -v
```

---

## 16. Phase Verdict & Transition Gate

- **Phase Objective**: Empirical calibration of affect thresholds against DAiSEE ground truth.
- **Empirical Dataset Status**: **BLOCKED (Dataset not present on host workstation)**.
- **Readiness Verification**: **PASS (Pipeline, test suite, and scripts verified and ready)**.
- **Phase 5 Overall Status**: **BLOCKED / CALIBRATION-READINESS VERIFIED**.
- **Unity Configuration**: **UNCHANGED** (Heuristic baselines $0.60 / 0.65 / 3.0\text{s}$ preserved).
- **Phase 6 Status**: **NOT STARTED**.
