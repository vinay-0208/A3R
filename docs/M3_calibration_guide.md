# M3 Calibration Guide — DAiSEE Threshold Calibration

> **Goal**: Replace the placeholder thresholds (T_confusion=0.60, T_frustration=0.65, D=3.0s)
> in `AdaptationController.cs` and `memory.md` with values calibrated from real DAiSEE data.

---

## What This Calibration Does

The A³R adaptation FSM fires when a learner's confusion or frustration score stays
**above threshold T for at least D seconds** (dwell-time criterion).

Calibration answers: *"What T and D minimize false alarms while catching genuine confusion?"*

Method: grid sweep over (T, D) candidates, evaluated using DAiSEE ground-truth labels,
optimizing F-beta (β=0.5, precision-weighted to minimise false alarms in classroom settings).

---

## Pre-conditions

- [ ] DAiSEE dataset downloaded (requires academic request from IIT Roorkee):
  - https://people.iith.ac.in/vineethnb/resources/daisee/index.html
  - Extract to a local drive: `D:\datasets\DAiSEE\` (or update `--daisee_root` argument)
- [ ] MediaPipe Face Landmarker model downloaded:
  ```powershell
  Invoke-WebRequest -Uri "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task" -OutFile "python_tools\daisee_calibration\face_landmarker.task"
  ```
- [ ] Python venv active: `.\.venv\Scripts\activate`
- [ ] `clip_scores/` directory does NOT exist yet (or is empty):
  ```powershell
  Remove-Item "python_tools\daisee_calibration\clip_scores" -Recurse -Force -ErrorAction SilentlyContinue
  ```

---

## Step 1: Batch Feature Extraction (~2–4 hours on CPU)

Processes DAiSEE test-set clips: extracts per-frame facial landmarks → computes features →
scores each frame → saves per-clip CSVs to `clip_scores/`.

```powershell
Set-Location "C:\Users\black\Documents\arvr\A3R"
.\.venv\Scripts\activate

python python_tools\daisee_calibration\batch_extract.py `
    --daisee_root "D:\datasets\DAiSEE" `
    --split test `
    --model_path "python_tools\daisee_calibration\face_landmarker.task" `
    --sample_fps 5 `
    --n_clips 500
```

**Expected output**: `python_tools/daisee_calibration/clip_scores/<clip_id>.csv` for each clip.

> **Time estimate**: ~3–5 seconds per clip × 500 clips ≈ 25–40 minutes.
> For the full test set (~2,000 clips), expect ~2–4 hours.

---

## Step 2: Run Threshold Calibration (~1 minute)

```powershell
python python_tools\daisee_calibration\calibrate_thresholds.py
```

This reads all `clip_scores/*.csv` files, sweeps over candidate (T, D) pairs,
computes precision/recall/F-beta for each, and outputs:

- **Console**: Top-10 operating points table
- **File**: `python_tools/daisee_calibration/calibration_result.json`

---

## Step 3: Generate Visualization Figures

```powershell
python python_tools\daisee_calibration\visualize_calibration.py
```

Saves to `docs/figures/`:
- `fbeta_heatmap.png` — F-beta grid (T × D)
- `precision_recall.png` — P-R curve for all T values
- `score_histograms.png` — Confusion score distribution by DAiSEE label (0–3)
- `example_trajectory.png` — Single clip affect trajectory with threshold line

---

## Step 4: Read Calibration Results

Open `calibration_result.json` and read the chosen operating point:

```json
{
  "T_confusion":   0.XX,
  "T_frustration": 0.XX,
  "D_seconds":     X.X,
  ...
}
```

---

## Step 5: Update AdaptationController.cs

Open [`unity_project/Assets/Scripts/Adaptation/AdaptationController.cs`](file:///c:/Users/black/Documents/arvr/A3R/unity_project/Assets/Scripts/Adaptation/AdaptationController.cs)

Find the threshold constants (near the top of the class) and replace the placeholders:

```csharp
// BEFORE (placeholder):
private const float CONFUSION_THRESHOLD   = 0.60f;
private const float FRUSTRATION_THRESHOLD = 0.65f;
private const float DWELL_TIME_SECONDS    = 3.0f;

// AFTER (replace X.XX with calibration_result.json values):
private const float CONFUSION_THRESHOLD   = X.XXf;
private const float FRUSTRATION_THRESHOLD = X.XXf;
private const float DWELL_TIME_SECONDS    = X.Xf;
```

---

## Step 6: Update memory.md

Replace the placeholder values in `memory.md` → "Calibrated thresholds" section with the
real values from `calibration_result.json`. Mark M3 as complete.

---

## Step 7: Update PROJECT_PLAN.md

In `PROJECT_PLAN.md` → Section 7 (threshold table), replace placeholder values with
the real calibrated values and note the date and dataset split used.

---

## Validation

After updating `AdaptationController.cs`, rebuild the APK and run a controlled test:

1. Sit in front of the device camera
2. Deliberately make a confused expression (furrowed brows, squinted eyes, tense mouth)
3. Hold it for D+1 seconds
4. Verify that the adaptation triggers (hint panel appears, or arrow cue appears)
5. Relax face → verify adaptation resets after the reset dwell time

---

## Troubleshooting

| Issue | Fix |
|---|---|
| `batch_extract.py` — "Video file not found" | Check DAiSEE directory structure matches expected layout (see `load_daisee.py` docstring) |
| `batch_extract.py` — "Face Landmarker model not found" | Download the .task file (see Pre-conditions above) |
| `calibrate_thresholds.py` — "No clip_scores/ directory found" | Run `batch_extract.py` first |
| Poor calibration (low F-beta < 0.6) | Increase `n_clips` to ≥ 1000; verify `sample_fps ≥ 5` |
| All thresholds cluster near 0.40 | Expected if DAiSEE confusion labels skew high — check label distribution in `score_histograms.png` |

---

## Expected Calibration Results (Rough Ranges)

Based on the rule-based formula and DAiSEE annotation style:

| Parameter | Expected Range | Placeholder |
|---|---|---|
| T_confusion | 0.40 – 0.60 | 0.60 |
| T_frustration | 0.45 – 0.65 | 0.65 |
| D (dwell time) | 1.5 – 4.0 s | 3.0 s |
| F-beta (β=0.5) | 0.70 – 0.92 | N/A |

> These are estimates based on the feature-formula design. Real calibration will determine
> the actual operating point.
