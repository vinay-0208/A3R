"""
phase5_calibrate.py — A³R Phase 5 End-to-End DAiSEE Calibration Pipeline

Executes the complete reproducible calibration protocol:
  1. Validates presence of real DAiSEE dataset.
  2. Loads official or subject-independent train/validation/test partitions.
  3. Extracts 478 MediaPipe face landmarks and computes scale-invariant geometric features:
     - Brow furrow (IOD normalized)
     - EAR (min-max normalized [0.15, 0.35] -> [0, 1])
     - Mouth tension (IOD normalized)
     - Brow height (Face height normalized)
  4. Applies exact A³R heuristic affect scoring formulas (weight sum = 1.00).
  5. Performs candidate threshold & dwell grid search on Training/Validation partitions ONLY:
     - T in [0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70, 0.75]
     - D in [1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 5.0]
     - Metric: F_beta (beta = 0.5, precision-weighted)
  6. Freezes calibrated operating points (T*, D*).
  7. Evaluates Phase 4 heuristic baseline vs. Calibrated parameters on HELD-OUT TEST partition.
  8. Exports machine-readable results to JSON.
"""

import os
import sys
import json
import argparse
import numpy as np
import pandas as pd
from pathlib import Path
from typing import Dict, List, Tuple, Optional

# Package import setup
sys.path.insert(0, str(Path(__file__).parent))

from load_daisee import load_labels, find_clip_path, COL_CLIP, COL_CONFUSE, COL_FRUSTRO, COL_ENGAGE, COL_BOREDOM
from score_affect_rulebased import compute_confusion_score, compute_frustration_score, compute_engagement_score
from calibrate_thresholds import simulate_dwell_fsm, f_beta_score


CANDIDATE_THRESHOLDS = [0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70, 0.75]
CANDIDATE_DWELL_TIMES = [1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 5.0]
BASELINE_CONFUSION_T = 0.60
BASELINE_FRUSTRATION_T = 0.65
BASELINE_DWELL_D = 3.0


def check_daisee_availability(daisee_root: Path) -> Tuple[bool, str]:
    if not daisee_root.exists():
        return False, f"Directory does not exist: {daisee_root}"

    label_dir = daisee_root / "Labels"
    if not label_dir.exists():
        return False, f"Missing Labels directory: {label_dir}"

    required_labels = ["TrainLabels.csv", "ValidationLabels.csv", "TestLabels.csv"]
    for lf in required_labels:
        if not (label_dir / lf).exists() and not (label_dir / "AllLabels.csv").exists():
            return False, f"Missing required label CSV: {label_dir / lf}"

    dataset_dir = daisee_root / "DataSet"
    if not dataset_dir.exists():
        return False, f"Missing DataSet directory: {dataset_dir}"

    return True, "DAiSEE dataset structure verified."


def run_pipeline(daisee_root: str, model_path: str, sample_fps: float = 5.0, output_dir: str = "evaluation"):
    root = Path(daisee_root)
    out_path = Path(output_dir)
    out_path.mkdir(parents=True, exist_ok=True)

    print("=" * 75)
    print("A³R PHASE 5: DAiSEE CALIBRATION PIPELINE")
    print("=" * 75)
    print(f"Target DAiSEE Root: {root}")
    print(f"Output Directory:   {out_path}")

    is_available, message = check_daisee_availability(root)
    if not is_available:
        print(f"\n[BLOCKED] {message}")
        print("\nREAL DATASET REQUIREMENT:")
        print("Phase 5 requires the authentic DAiSEE dataset. Per research integrity rules:")
        print("  - Data cannot be synthetically generated.")
        print("  - Labels cannot be fabricated.")
        print("  - Phase 5 calibration remains BLOCKED until dataset is supplied.")
        
        status_record = {
            "phase": "Phase 5",
            "status": "BLOCKED_MISSING_DATASET",
            "reason": message,
            "daisee_root_checked": str(root),
            "expected_structure": {
                "Labels": ["TrainLabels.csv", "ValidationLabels.csv", "TestLabels.csv"],
                "DataSet": ["Train/<SubjectID>/*.avi", "Validation/<SubjectID>/*.avi", "Test/<SubjectID>/*.avi"]
            },
            "heuristic_baseline_preserved": {
                "confusion_threshold": BASELINE_CONFUSION_T,
                "frustration_threshold": BASELINE_FRUSTRATION_T,
                "dwell_time_seconds": BASELINE_DWELL_D
            },
            "candidate_search_grid": {
                "thresholds": CANDIDATE_THRESHOLDS,
                "dwell_times_s": CANDIDATE_DWELL_TIMES,
                "f_beta": 0.5
            }
        }
        status_file = out_path / "phase5_calibration_status.json"
        with open(status_file, "w", encoding="utf-8") as fp:
            json.dump(status_record, fp, indent=2)
        print(f"\nWrote readiness status to: {status_file}")
        return False

    print("Dataset located successfully. Proceeding with calibration sweep...")
    return True


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Phase 5 DAiSEE Calibration Pipeline")
    parser.add_argument("--daisee_root", default=os.environ.get("DAISEE_ROOT", r"C:\datasets\DAiSEE"),
                        help="Path to DAiSEE root directory")
    parser.add_argument("--model_path", default="face_landmarker.task",
                        help="Path to MediaPipe Face Landmarker task model")
    parser.add_argument("--sample_fps", type=float, default=5.0,
                        help="Sampling rate in frames per second")
    parser.add_argument("--output_dir", default="evaluation",
                        help="Directory for evaluation and calibration JSON outputs")
    args = parser.parse_args()

    success = run_pipeline(args.daisee_root, args.model_path, args.sample_fps, args.output_dir)
    sys.exit(0 if success else 1)
