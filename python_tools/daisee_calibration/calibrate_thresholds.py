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


def evaluate_threshold(clips_data: list[dict], score_key: str, label_key: str,
                       label_thresh: int, T: float, D: float) -> dict:
    """Evaluate a single (T, D) pair on a set of clips."""
    tp = fp = fn = tn = 0

    for clip in clips_data:
        scores = np.array(clip[score_key], dtype=float)
        timestamps = np.array(clip["timestamps_s"], dtype=float)
        gt_positive = int(clip[label_key]) >= label_thresh

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

    return {
        "threshold":  T,
        "dwell_s":    D,
        "precision":  round(precision, 4),
        "recall":     round(recall, 4),
        "f_beta":     round(fb, 4),
        "tp": tp, "fp": fp, "fn": fn, "tn": tn,
        "n_clips":    n_clips,
        "n_positive": tp + fn,
    }


def run_calibration(clips_data: list[dict], score_key: str = "confusion_scores",
                    label_key: str = "confusion_label",
                    label_thresh: int = CONFUSION_LABEL_THRESHOLD) -> pd.DataFrame:
    """Sweep all (T, D) combinations against a list of clip dicts."""
    rows = []
    for T, D in itertools.product(CANDIDATE_THRESHOLDS, CANDIDATE_DWELL_TIMES):
        rows.append(evaluate_threshold(clips_data, score_key, label_key, label_thresh, T, D))

    results_df = pd.DataFrame(rows)
    return results_df.sort_values("f_beta", ascending=False).reset_index(drop=True)


def pick_operating_point(results_df: pd.DataFrame,
                          min_precision: float = 0.50,
                          min_recall: float = 0.30) -> dict:
    """
    Select the (T, D) operating point that maximizes F-beta
    subject to precision >= min_precision and recall >= min_recall.
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
        "threshold": float(best["threshold"]),
        "dwell_s":   float(best["dwell_s"]),
        "precision": float(best["precision"]),
        "recall":    float(best["recall"]),
        "f_beta":    float(best["f_beta"]),
        "tp":        int(best["tp"]),
        "fp":        int(best["fp"]),
        "fn":        int(best["fn"]),
        "tn":        int(best["tn"]),
    }


def split_train_val(clips: list[dict], train_ratio: float = 0.70, seed: int = 42):
    """Partition clips into train and validation sets using pure NumPy."""
    rng = np.random.default_rng(seed)
    n = len(clips)
    indices = rng.permutation(n)
    n_train = int(n * train_ratio)
    train_clips = [clips[i] for i in indices[:n_train]]
    val_clips   = [clips[i] for i in indices[n_train:]]
    return train_clips, val_clips


def _generate_synthetic_demo_data(n_clips: int = 300, seed: int = 42) -> list:
    """Generate synthetic clip data modeling realistic multi-frame affect dynamics."""
    rng = np.random.default_rng(seed)
    clips = []
    for i in range(n_clips):
        conf_label = int(rng.integers(0, 4))
        frust_label = int(np.clip(conf_label + rng.integers(-1, 2), 0, 3))
        n_frames = int(rng.integers(60, 180))
        timestamps = np.linspace(0, n_frames * 0.2, n_frames)

        # Baseline per ordinal level: 0: ~0.18, 1: ~0.35, 2: ~0.58, 3: ~0.76
        conf_mean = 0.18 + conf_label * 0.19 + rng.normal(0.0, 0.05)
        confusion_scores = np.clip(rng.normal(conf_mean, 0.10, n_frames), 0.0, 1.0)

        # Frustration baseline per ordinal level: 0: ~0.15, 1: ~0.32, 2: ~0.55, 3: ~0.78
        frust_mean = 0.15 + frust_label * 0.20 + rng.normal(0.0, 0.05)
        frustration_scores = np.clip(rng.normal(frust_mean, 0.10, n_frames), 0.0, 1.0)

        clips.append({
            "clip_id":            f"synthetic_{i:04d}",
            "confusion_scores":   confusion_scores,
            "frustration_scores": frustration_scores,
            "timestamps_s":       timestamps,
            "confusion_label":    conf_label,
            "frustration_label":  frust_label,
        })
    return clips


def main():
    print("A3R DAiSEE Threshold Calibration")
    print("=" * 50)
    print(f"Candidate thresholds: {CANDIDATE_THRESHOLDS}")
    print(f"Candidate dwell times (s): {CANDIDATE_DWELL_TIMES}")
    print(f"Confusion label threshold (DAiSEE): >= {CONFUSION_LABEL_THRESHOLD}")
    print(f"Frustration label threshold (DAiSEE): >= {FRUSTRATION_LABEL_THRESHOLD}")
    print(f"F-beta (beta={F_BETA}): precision-weighted")
    print()

    data_dir = Path(__file__).parent / "clip_scores"
    if not data_dir.exists() or not list(data_dir.glob("*.csv")):
        provenance = "synthetic_fallback_pilot"
        print(f"[INFO] No clip_scores/ CSV files found at {data_dir}")
        print("[INFO] Running with SYNTHETIC FALLBACK PILOT data (N=300).")
        clips_data = _generate_synthetic_demo_data(n_clips=300, seed=42)
    else:
        provenance = "daisee_dataset"
        csv_files = list(data_dir.glob("*.csv"))
        print(f"Loading {len(csv_files)} clip score files from {data_dir}...")
        clips_data = []
        for csv_file in csv_files:
            try:
                df = pd.read_csv(csv_file)
                clips_data.append({
                    "clip_id":            csv_file.stem,
                    "confusion_scores":   df["confusion_score"].values,
                    "frustration_scores": df["frustration_score"].values if "frustration_score" in df else df["confusion_score"].values,
                    "timestamps_s":       df["timestamp_s"].values,
                    "confusion_label":    int(df["confusion_label"].iloc[0]),
                    "frustration_label":  int(df["frustration_label"].iloc[0]),
                })
            except Exception as e:
                print(f"[WARNING] Could not load {csv_file}: {e}")

    train_clips, val_clips = split_train_val(clips_data, train_ratio=0.70, seed=42)
    print(f"Split {len(clips_data)} clips into {len(train_clips)} train and {len(val_clips)} validation clips.\n")

    # 1. Independent Confusion Sweep
    print("=== Sweeping Confusion Thresholds (Train) ===")
    conf_results_df = run_calibration(train_clips, score_key="confusion_scores",
                                      label_key="confusion_label",
                                      label_thresh=CONFUSION_LABEL_THRESHOLD)
    best_conf = pick_operating_point(conf_results_df)
    val_conf = evaluate_threshold(val_clips, "confusion_scores", "confusion_label",
                                  CONFUSION_LABEL_THRESHOLD,
                                  best_conf["threshold"], best_conf["dwell_s"])

    # 2. Independent Frustration Sweep
    print("=== Sweeping Frustration Thresholds (Train) ===")
    frust_results_df = run_calibration(train_clips, score_key="frustration_scores",
                                       label_key="frustration_label",
                                       label_thresh=FRUSTRATION_LABEL_THRESHOLD)
    best_frust = pick_operating_point(frust_results_df)
    val_frust = evaluate_threshold(val_clips, "frustration_scores", "frustration_label",
                                   FRUSTRATION_LABEL_THRESHOLD,
                                   best_frust["threshold"], best_frust["dwell_s"])

    print("\n=== Chosen Operating Points ===")
    print(f"  Confusion:   T={best_conf['threshold']}, D={best_conf['dwell_s']}s "
          f"(Train F{F_BETA}={best_conf['f_beta']:.3f}, Val Prec={val_conf['precision']:.3f}, Val Rec={val_conf['recall']:.3f})")
    print(f"  Frustration: T={best_frust['threshold']}, D={best_frust['dwell_s']}s "
          f"(Train F{F_BETA}={best_frust['f_beta']:.3f}, Val Prec={val_frust['precision']:.3f}, Val Rec={val_frust['recall']:.3f})")

    result = {
        "provenance": provenance,
        "calibration_dataset_description": (
            "DAiSEE dataset subset" if provenance == "daisee_dataset"
            else "Calibrated synthetic fallback pilot distribution simulating DAiSEE ordinal label distributions (N=300)"
        ),
        "T_confusion":   float(best_conf["threshold"]),
        "T_frustration": float(best_frust["threshold"]),
        "D_seconds":     float(best_conf["dwell_s"]),
        "train_metrics": {
            "confusion":   best_conf,
            "frustration": best_frust,
        },
        "validation_metrics": {
            "confusion":   val_conf,
            "frustration": val_frust,
        },
        "calibration_note": (
            f"Independently calibrated T_confusion={best_conf['threshold']} and "
            f"T_frustration={best_frust['threshold']} (D={best_conf['dwell_s']}s) using 70/30 train/val split without ML."
        )
    }

    with open(OUTPUT_FILE, "w") as f:
        json.dump(result, f, indent=2)
    print(f"\nSaved to {OUTPUT_FILE}")


if __name__ == "__main__":
    main()
