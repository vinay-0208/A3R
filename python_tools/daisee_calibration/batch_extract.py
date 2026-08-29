"""
batch_extract.py — Batch feature extraction orchestrator for DAiSEE clips

Part of the A³R offline calibration tooling (python_tools/daisee_calibration/).
NOT shipped in the mobile app — offline use only.

Runs extract_features.py and score_affect_rulebased.py over a directory of DAiSEE
video clips and a matching label CSV, producing per-clip scored CSV files in
clip_scores/ ready for calibrate_thresholds.py.

Usage:
    python batch_extract.py \\
        --daisee_root "D:/datasets/DAiSEE" \\
        --split test \\
        --model_path face_landmarker.task \\
        --sample_fps 5 \\
        --n_clips 200

DAiSEE directory structure expected:
    <daisee_root>/
        DataSet/
            Test/
                <subject_id>/
                    <clip_id>/
                        <clip_id>.avi
        Labels/
            TestLabels.csv   (or AllLabels.csv)
"""

import argparse
import os
import sys
from pathlib import Path

import pandas as pd

# Add parent directory to path so we can import from the same package
sys.path.insert(0, str(Path(__file__).parent))

from load_daisee import load_labels, find_clip_path
from extract_features import extract_features_from_clip
from score_affect_rulebased import score_dataframe, compute_clip_level_stats


# ── Constants ──────────────────────────────────────────────────────────────────
SPLIT_LABEL_MAP = {
    "train": "TrainLabels.csv",
    "val":   "ValidationLabels.csv",
    "test":  "TestLabels.csv",
}

SPLIT_VIDEO_MAP = {
    "train": "Train",
    "val":   "Validation",
    "test":  "Test",
}


def process_clip(clip_id: str, clip_path: str, label_row: dict,
                 model_path: str, sample_fps: float,
                 output_dir: Path) -> bool:
    """
    Extract features, score, and save a single clip's data to CSV.

    Args:
        clip_id:    Clip identifier string.
        clip_path:  Path to the .avi file.
        label_row:  Dict with DAiSEE label values (Confusion, Frustration, Boredom, Engagement).
        model_path: Path to MediaPipe Face Landmarker .task model.
        sample_fps: Frames per second to sample from the video.
        output_dir: Directory to save the output CSV.

    Returns:
        True if successful, False otherwise.
    """
    output_path = output_dir / f"{clip_id}.csv"
    if output_path.exists():
        print(f"  [SKIP] {clip_id} — already processed")
        return True

    try:
        print(f"  [PROC] {clip_id} — {clip_path}")
        features_df = extract_features_from_clip(clip_path, sample_fps, model_path)

        if features_df.empty:
            print(f"  [WARN] {clip_id} — no frames extracted (empty video or detection failure)")
            return False

        # Add per-clip ground truth labels (constant per row)
        for label_key in ("Confusion", "Frustration", "Boredom", "Engagement"):
            features_df[f"{label_key.lower()}_label"] = label_row.get(label_key, 0)

        # Score each frame
        scored_df = score_dataframe(features_df)

        # Save
        scored_df.to_csv(output_path, index=False)

        # Print clip stats
        stats = compute_clip_level_stats(scored_df)
        face_pct = scored_df["face_detected"].mean() * 100
        print(f"    → {len(scored_df)} frames, face detected {face_pct:.0f}%")
        print(f"    → confusion mean={stats.get('confusion_score_mean', 0):.3f}, "
              f"p90={stats.get('confusion_score_p90', 0):.3f}")

        return True

    except Exception as ex:
        print(f"  [ERROR] {clip_id}: {ex}")
        return False


def batch_extract(daisee_root: str, split: str, model_path: str,
                  sample_fps: float, n_clips: int | None) -> None:
    """
    Main batch extraction loop.
    """
    daisee_root = Path(daisee_root)
    output_dir  = Path(__file__).parent / "clip_scores"
    output_dir.mkdir(exist_ok=True)

    # Load labels
    label_file = daisee_root / "Labels" / SPLIT_LABEL_MAP[split]
    if not label_file.exists():
        # Try AllLabels.csv fallback
        label_file = daisee_root / "Labels" / "AllLabels.csv"
        if not label_file.exists():
            raise FileNotFoundError(f"Label file not found: {label_file}\n"
                                    "Expected: <daisee_root>/Labels/TestLabels.csv")

    labels_df = load_labels(str(label_file))
    video_dir = daisee_root / "DataSet" / SPLIT_VIDEO_MAP[split]

    print(f"A³R Batch Feature Extraction")
    print(f"  Split:      {split} ({len(labels_df)} clips in labels)")
    print(f"  Video dir:  {video_dir}")
    print(f"  Output dir: {output_dir}")
    print(f"  Model:      {model_path}")
    print(f"  Sample FPS: {sample_fps}")
    print(f"  Max clips:  {n_clips or 'all'}")
    print()

    if not Path(model_path).exists():
        print(f"[ERROR] MediaPipe model not found: {model_path}")
        print("Download from: https://storage.googleapis.com/mediapipe-models/"
              "face_landmarker/face_landmarker/float16/1/face_landmarker.task")
        return

    success = 0
    fail    = 0
    skip    = 0
    total   = min(n_clips, len(labels_df)) if n_clips else len(labels_df)

    for i, (_, row) in enumerate(labels_df.iterrows()):
        if n_clips and i >= n_clips:
            break

        clip_id = str(row.get("ClipID", row.get("Clip", f"clip_{i:05d}")))
        clip_path = find_clip_path(str(video_dir), clip_id)

        if clip_path is None:
            print(f"  [MISS] {clip_id} — video file not found")
            fail += 1
            continue

        label_row = {
            "Confusion":   int(row.get("Confusion",  0)),
            "Frustration": int(row.get("Frustration", 0)),
            "Boredom":     int(row.get("Boredom",    0)),
            "Engagement":  int(row.get("Engagement", 0)),
        }

        result = process_clip(clip_id, clip_path, label_row,
                              model_path, sample_fps, output_dir)
        if result:
            success += 1
        else:
            fail += 1

        if (i + 1) % 20 == 0:
            print(f"Progress: {i+1}/{total} clips — {success} OK, {fail} failed")

    print(f"\nDone. {success}/{total} clips processed successfully. "
          f"{fail} failed. Output: {output_dir}")
    print(f"\nNext step: python calibrate_thresholds.py")


def main():
    parser = argparse.ArgumentParser(
        description="A3R batch DAiSEE feature extraction and scoring")
    parser.add_argument("--daisee_root", required=True,
                        help="Root directory of the DAiSEE dataset")
    parser.add_argument("--split", default="test",
                        choices=["train", "val", "test"],
                        help="Dataset split to process")
    parser.add_argument("--model_path", default="face_landmarker.task",
                        help="Path to MediaPipe Face Landmarker .task model")
    parser.add_argument("--sample_fps", type=float, default=5.0,
                        help="Frames per second to sample from each video")
    parser.add_argument("--n_clips", type=int, default=None,
                        help="Max number of clips to process (default: all)")
    args = parser.parse_args()

    batch_extract(
        daisee_root=args.daisee_root,
        split=args.split,
        model_path=args.model_path,
        sample_fps=args.sample_fps,
        n_clips=args.n_clips,
    )


if __name__ == "__main__":
    main()
