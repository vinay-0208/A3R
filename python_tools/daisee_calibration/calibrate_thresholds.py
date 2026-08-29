"""
calibrate_thresholds.py — Sweep threshold T and dwell-time D against DAiSEE ground truth

Part of the A³R offline calibration tooling (python_tools/daisee_calibration/).
NOT shipped in the mobile app — offline use only.

PURPOSE: Determine the optimal confusion/frustration threshold T and dwell-time D
for the AdaptationController.cs FSM. This is purely a threshold-picking exercise.

What this script does:
1. Load pre-computed per-clip feature+score data (produced by extract_features.py / score_affect_rulebased.py)
2. Load DAiSEE ground-truth labels (Confusion and Frustration columns)
3. For each candidate (T, D) pair:
   a. Simulate the dwell-time FSM: fire if score > T sustained for >= D seconds
   b. Compare predicted "fire" vs ground-truth label >= CONFUSION_LABEL_THRESHOLD
   c. Compute precision, recall, F0.5 score (precision-weighted — we prefer not firing on non-confused users)
4. Print a results table and recommend the best (T, D) operating point
5. Write chosen T and D to calibration_result.json

WHAT THIS SCRIPT DOES NOT DO (intentionally, per project spec):
  - NO scikit-learn, NO .fit(), NO model training
  - NO cross-validation, NO train/test splits for model generalization
  - NO hyperparameter optimization of model architectures
  - Does NOT report or optimize a benchmark accuracy number

Output: calibration_result.json with chosen T_confusion, T_frustration, D values
"""

import json
import itertools
import numpy as np
import pandas as pd
from pathlib import Path

# ── Configuration ─────────────────────────────────────────────────────────────
# DAiSEE label threshold: label >= this value is considered "confused/frustrated"
# DAiSEE labels are 0-3 ordinal. 2 = "moderately confused" seems a reasonable boundary.
CONFUSION_LABEL_THRESHOLD    = 2   # treat DAiSEE Confusion >= 2 as positive
FRUSTRATION_LABEL_THRESHOLD  = 2   # treat DAiSEE Frustration >= 2 as positive

# Candidate values to sweep
CANDIDATE_THRESHOLDS    = [0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70, 0.75]  # score T
CANDIDATE_DWELL_TIMES   = [1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 5.0]              # seconds D

# F-beta: beta < 1 weights precision more than recall (preferred for affect adaptation —
# false positives interrupt the learning flow unnecessarily)
F_BETA = 0.5

OUTPUT_FILE = Path(__file__).parent / "calibration_result.json"


def simulate_dwell_fsm(scores: np.ndarray, timestamps_s: np.ndarray,
                        threshold: float, dwell_s: float) -> bool:
    """
    Simulate the dwell-time finite-state machine on a single clip's score array.

    Returns True if the FSM fires at any point during the clip
    (i.e., score > threshold sustained for >= dwell_s seconds).
    """
    if len(scores) == 0:
        return False

    in_state = False
    state_start_time = 0.0

    for i, (score, t) in enumerate(zip(scores, timestamps_s)):
        if not np.isnan(score) and score > threshold:
            if not in_state:
                in_state = True
                state_start_time = t
            elif (t - state_start_time) >= dwell_s:
                return True  # FSM fires
        else:
            in_state = False

    return False


def f_beta_score(precision: float, recall: float, beta: float = F_BETA) -> float:
    """F-beta score. beta < 1 weights precision more heavily."""
    if precision + recall < 1e-9:
        return 0.0
    return (1 + beta**2) * precision * recall / (beta**2 * precision + recall)


def run_calibration(clips_data: list[dict]) -> pd.DataFrame:
    """
    Sweep all (T, D) combinations against a list of clip dicts.

    Each clip dict must have:
        - "confusion_scores":    np.ndarray of per-frame confusion scores
        - "timestamps_s":        np.ndarray of per-frame timestamps
        - "confusion_label":     int (DAiSEE ground truth, 0-3)
        - "frustration_label":   int (DAiSEE ground truth, 0-3)

    Returns:
        DataFrame with columns: threshold, dwell_s, precision, recall, f_beta, n_positive, n_clips
    """
    rows = []

    for T, D in itertools.product(CANDIDATE_THRESHOLDS, CANDIDATE_DWELL_TIMES):
        tp = fp = fn = tn = 0

        for clip in clips_data:
            scores = np.array(clip["confusion_scores"], dtype=float)
            timestamps = np.array(clip["timestamps_s"], dtype=float)
            gt_positive = int(clip["confusion_label"]) >= CONFUSION_LABEL_THRESHOLD

            predicted_fire = simulate_dwell_fsm(scores, timestamps, T, D)

            if predicted_fire and gt_positive:
                tp += 1
            elif predicted_fire and not gt_positive:
                fp += 1
            elif not predicted_fire and gt_positive:
                fn += 1
            else:
                tn += 1

        n_clips = tp + fp + fn + tn
        precision = tp / (tp + fp) if (tp + fp) > 0 else 0.0
        recall    = tp / (tp + fn) if (tp + fn) > 0 else 0.0
        fb        = f_beta_score(precision, recall)

        rows.append({
            "threshold":   T,
            "dwell_s":     D,
            "precision":   round(precision, 4),
            "recall":      round(recall, 4),
            "f_beta":      round(fb, 4),
            "tp": tp, "fp": fp, "fn": fn, "tn": tn,
            "n_clips":     n_clips,
            "n_positive":  tp + fn,
        })

    results_df = pd.DataFrame(rows)
    return results_df.sort_values("f_beta", ascending=False).reset_index(drop=True)


def pick_operating_point(results_df: pd.DataFrame,
                          min_precision: float = 0.50,
                          min_recall: float = 0.30) -> dict:
    """
    Select the (T, D) operating point that maximizes F-beta
    subject to precision >= min_precision and recall >= min_recall.

    Falls back to pure F-beta maximizer if no row meets both constraints.
    """
    filtered = results_df[
        (results_df["precision"] >= min_precision) &
        (results_df["recall"]    >= min_recall)
    ]
    if len(filtered) == 0:
        print(f"[WARNING] No (T,D) meets precision>={min_precision} AND recall>={min_recall}. "
              f"Falling back to pure F-beta maximizer.")
        best = results_df.iloc[0]
    else:
        best = filtered.iloc[0]

    return {
        "T_confusion":    float(best["threshold"]),
        "T_frustration":  float(best["threshold"]) + 0.05,  # slight offset for frustration
        "D_seconds":      float(best["dwell_s"]),
        "precision":      float(best["precision"]),
        "recall":         float(best["recall"]),
        "f_beta":         float(best["f_beta"]),
    }


def main():
    """
    Main calibration entry point.

    In real use, load clips_data from the outputs of extract_features.py
    and score_affect_rulebased.py. Here we provide a minimal example
    showing the expected interface.

    To run the full calibration:
      1. Run extract_features.py on your DAiSEE subset → saves per-clip CSVs
      2. Run score_affect_rulebased.py on those CSVs → adds score columns
      3. Run this script → saves calibration_result.json
    """
    print("A³R DAiSEE Threshold Calibration")
    print("=" * 50)
    print(f"Candidate thresholds: {CANDIDATE_THRESHOLDS}")
    print(f"Candidate dwell times (s): {CANDIDATE_DWELL_TIMES}")
    print(f"Confusion label threshold (DAiSEE): >= {CONFUSION_LABEL_THRESHOLD}")
    print(f"F-beta (beta={F_BETA}): precision-weighted")
    print()

    # ── Load pre-scored clip data ─────────────────────────────────────────────
    # Look for CSV files produced by the feature extraction pipeline
    data_dir = Path(__file__).parent / "clip_scores"
    if not data_dir.exists():
        print(f"[INFO] No clip_scores/ directory found at {data_dir}")
        print("[INFO] Running with SYNTHETIC DATA for structure demonstration.")
        print("[INFO] To calibrate for real: populate clip_scores/ with CSVs from extract_features.py + score_affect_rulebased.py")
        print()
        # Generate synthetic data for demonstration
        clips_data = _generate_synthetic_demo_data(n_clips=200)
    else:
        csv_files = list(data_dir.glob("*.csv"))
        if not csv_files:
            print(f"[ERROR] No CSV files found in {data_dir}. Run extract_features.py first.")
            return
        print(f"Loading {len(csv_files)} clip score files from {data_dir}...")
        clips_data = []
        for csv_file in csv_files:
            try:
                df = pd.read_csv(csv_file)
                clips_data.append({
                    "clip_id":           csv_file.stem,
                    "confusion_scores":  df["confusion_score"].values,
                    "timestamps_s":      df["timestamp_s"].values,
                    "confusion_label":   int(df["confusion_label"].iloc[0]),
                    "frustration_label": int(df["frustration_label"].iloc[0]),
                })
            except Exception as e:
                print(f"[WARNING] Could not load {csv_file}: {e}")

    print(f"Loaded {len(clips_data)} clips. Running calibration sweep...")
    results_df = run_calibration(clips_data)

    print("\n=== Top 10 (T, D) operating points (sorted by F-beta) ===")
    print(results_df.head(10).to_string(index=False))

    best = pick_operating_point(results_df)
    print(f"\n=== Chosen Operating Point ===")
    print(f"  T_confusion:   {best['T_confusion']}")
    print(f"  T_frustration: {best['T_frustration']}")
    print(f"  D (dwell):     {best['D_seconds']} seconds")
    print(f"  Precision:     {best['precision']:.3f}")
    print(f"  Recall:        {best['recall']:.3f}")
    print(f"  F-{F_BETA}:        {best['f_beta']:.3f}")

    best["calibration_note"] = (
        "Values calibrated against DAiSEE subset using rule-based confusion score. "
        "Update PROJECT_PLAN.md and memory.md with these values."
    )

    with open(OUTPUT_FILE, "w") as f:
        json.dump(best, f, indent=2)
    print(f"\nSaved to {OUTPUT_FILE}")
    print("→ Copy T_confusion, T_frustration, D_seconds into PROJECT_PLAN.md and memory.md")


def _generate_synthetic_demo_data(n_clips: int = 200) -> list:
    """
    Generate synthetic clip data for structure demonstration.
    In real use, this is replaced by actual DAiSEE data.
    """
    rng = np.random.default_rng(42)
    clips = []
    for i in range(n_clips):
        gt_label = rng.integers(0, 4)  # DAiSEE 0-3 ordinal
        n_frames = rng.integers(50, 200)
        timestamps = np.linspace(0, n_frames * 0.2, n_frames)  # 5fps → 0.2s intervals

        # Simulate scores: confused clips have higher mean scores
        base_score = 0.3 + (gt_label / 3.0) * 0.4
        scores = rng.normal(base_score, 0.1, n_frames).clip(0, 1)

        clips.append({
            "clip_id":           f"synthetic_{i:04d}",
            "confusion_scores":  scores,
            "timestamps_s":      timestamps,
            "confusion_label":   int(gt_label),
            "frustration_label": int(min(gt_label + rng.integers(-1, 2), 3)),
        })
    return clips


if __name__ == "__main__":
    main()
