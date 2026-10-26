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
# Configurable DAiSEE root: DAISEE_ROOT env var -> local path fallbacks -> repo datasets/DAiSEE
_repo_datasets = Path(__file__).resolve().parents[2] / "datasets" / "DAiSEE"
if os.environ.get("DAISEE_ROOT"):
    _default_root = Path(os.environ["DAISEE_ROOT"])
elif Path(r"C:\Dataset\DAiSEE").exists():
    _default_root = Path(r"C:\Dataset\DAiSEE")
elif Path(r"C:\datasets\DAiSEE").exists():
    _default_root = Path(r"C:\datasets\DAiSEE")
elif _repo_datasets.exists():
    _default_root = _repo_datasets
else:
    _default_root = Path(r"C:\Dataset\DAiSEE")

DAISEE_ROOT = Path(os.environ.get("DAISEE_ROOT", _default_root))

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


def find_clip_path(clip_dir: str | Path, clip_id: str) -> str | None:
    """Find full path for clip_id in clip_dir."""
    clip_dir = Path(clip_dir)
    stem = Path(clip_id).stem
    # 1. Direct file check
    for ext in ("", ".avi", ".mp4", ".AVI", ".MP4"):
        f = clip_dir / (clip_id if not ext else f"{stem}{ext}")
        if f.is_file():
            return str(f)
    # 2. Hierarchical structure: <clip_dir>/<SubjectID>/<ClipID>/<ClipID>.<ext>
    # DAiSEE subject ID is typically the 4-6 digit prefix of the clip stem
    for sub_len in (6, 5, 4):
        if len(stem) >= sub_len:
            sub = stem[:sub_len]
            cand_dir = clip_dir / sub / stem
            if cand_dir.is_dir():
                for ext in (".avi", ".mp4", ".AVI", ".MP4"):
                    cand_file = cand_dir / f"{stem}{ext}"
                    if cand_file.is_file():
                        return str(cand_file)
    # 3. Fallback recursive search
    matches = list(clip_dir.rglob(f"{stem}.*"))
    for m in matches:
        if m.suffix.lower() in (".avi", ".mp4"):
            return str(m)
    return None


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

    df = labels_df.copy()
    df["clip_path"] = df[COL_CLIP].apply(lambda cid: find_clip_path(clip_dir, str(cid)))

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
