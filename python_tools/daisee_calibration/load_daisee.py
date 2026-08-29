"""
load_daisee.py — DAiSEE dataset loader

Part of the A³R offline calibration tooling (python_tools/daisee_calibration/).
NOT shipped in the mobile app — offline use only.

Loads DAiSEE video clips and their ground-truth labels.
DAiSEE dataset: https://people.iith.ac.in/vineethnb/resources/daisee/index.html

Expected dataset layout (adjust DAISEE_ROOT in config below):
  DAiSEE/
    DataSet/
      Train/ Validation/ Test/
        <SubjectID>/<ClipID>.avi
    Labels/
      TrainLabels.csv
      ValidationLabels.csv
      TestLabels.csv

CSV format (per DAiSEE documentation):
  ClipID, Boredom, Engagement, Confusion, Frustration
  All labels are on 0-3 ordinal scale (0=none, 3=very high).

Usage:
  from load_daisee import load_labels, get_clip_paths
  labels_df = load_labels(split="train")
  clips = get_clip_paths(labels_df, split="train", daisee_root=DAISEE_ROOT)
"""

import os
import pandas as pd
from pathlib import Path

# ── Configuration ──────────────────────────────────────────────────────────────
# Set this to the root of your local DAiSEE dataset download.
DAISEE_ROOT = Path(os.environ.get("DAISEE_ROOT", r"C:\datasets\DAiSEE"))

LABEL_FILES = {
    "train":      DAISEE_ROOT / "Labels" / "TrainLabels.csv",
    "validation": DAISEE_ROOT / "Labels" / "ValidationLabels.csv",
    "test":       DAISEE_ROOT / "Labels" / "TestLabels.csv",
}

CLIP_DIRS = {
    "train":      DAISEE_ROOT / "DataSet" / "Train",
    "validation": DAISEE_ROOT / "DataSet" / "Validation",
    "test":       DAISEE_ROOT / "DataSet" / "Test",
}

# Column names in the DAiSEE label CSVs
COL_CLIP    = "ClipID"
COL_BOREDOM = "Boredom"
COL_ENGAGE  = "Engagement"
COL_CONFUSE = "Confusion"
COL_FRUSTRO = "Frustration"


def load_labels(split: str = "train") -> pd.DataFrame:
    """
    Load ground-truth labels for the given split.

    Returns:
        DataFrame with columns: ClipID, Boredom, Engagement, Confusion, Frustration
    """
    split = split.lower()
    if split not in LABEL_FILES:
        raise ValueError(f"split must be one of {list(LABEL_FILES.keys())}, got '{split}'")

    label_path = LABEL_FILES[split]
    if not label_path.exists():
        raise FileNotFoundError(
            f"Label file not found: {label_path}\n"
            f"Set the DAISEE_ROOT environment variable to your dataset root."
        )

    df = pd.read_csv(label_path)

    # Normalise column names (strip whitespace)
    df.columns = [c.strip() for c in df.columns]

    required_cols = {COL_CLIP, COL_BOREDOM, COL_ENGAGE, COL_CONFUSE, COL_FRUSTRO}
    missing = required_cols - set(df.columns)
    if missing:
        raise ValueError(f"Label CSV missing expected columns: {missing}")

    return df[[COL_CLIP, COL_BOREDOM, COL_ENGAGE, COL_CONFUSE, COL_FRUSTRO]].copy()


def get_clip_paths(labels_df: pd.DataFrame, split: str = "train",
                   daisee_root: Path = DAISEE_ROOT) -> pd.DataFrame:
    """
    Resolve full filesystem paths for each clip in labels_df.

    Returns:
        DataFrame with all original columns plus a 'clip_path' column (str).
        Rows where the clip file is not found on disk are dropped with a warning.
    """
    split = split.lower()
    clip_dir = daisee_root / "DataSet" / split.capitalize()

    def _resolve(clip_id: str) -> str | None:
        # DAiSEE clip IDs look like "1100011001.avi"
        # They live under SubjectID subdirectories.
        # Try direct path first, then recursive glob.
        direct = clip_dir / clip_id
        if direct.exists():
            return str(direct)
        # Recursive search (slow for large dataset — use sparingly on subsets)
        matches = list(clip_dir.rglob(clip_id))
        if matches:
            return str(matches[0])
        return None

    df = labels_df.copy()
    df["clip_path"] = df[COL_CLIP].apply(_resolve)

    missing = df["clip_path"].isna().sum()
    if missing > 0:
        print(f"[WARNING] {missing} clips not found on disk — they will be dropped.")
    df = df.dropna(subset=["clip_path"]).reset_index(drop=True)
    return df


def sample_clips(df: pd.DataFrame, n: int = 100, random_state: int = 42) -> pd.DataFrame:
    """
    Stratified sample of n clips, balanced across confusion label levels (0–3).
    Useful for a fast calibration run on a subset of DAiSEE.
    """
    # Bin confusion into levels 0-3 (DAiSEE ordinal scale)
    groups = df.groupby(COL_CONFUSE, group_keys=False)
    n_per_group = max(1, n // df[COL_CONFUSE].nunique())
    sampled = groups.apply(
        lambda g: g.sample(min(len(g), n_per_group), random_state=random_state)
    )
    return sampled.reset_index(drop=True)


if __name__ == "__main__":
    print("DAiSEE Loader — sanity check")
    print(f"Looking for dataset at: {DAISEE_ROOT}")
    if not DAISEE_ROOT.exists():
        print("Dataset root not found. Set DAISEE_ROOT environment variable.")
    else:
        for split in ("train", "validation"):
            try:
                df = load_labels(split)
                print(f"  {split}: {len(df)} clips loaded. Confusion label distribution:")
                print(df[COL_CONFUSE].value_counts().sort_index().to_string())
            except FileNotFoundError as e:
                print(f"  {split}: {e}")
