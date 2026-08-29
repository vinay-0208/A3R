"""
score_affect_rulebased.py — Fixed rule-based affect scorer

Part of the A³R offline calibration tooling (python_tools/daisee_calibration/).
NOT shipped in the mobile app — offline use only.

Implements the EXACT same scoring formulas as AffectScorer.cs (C#).
This ensures calibrated thresholds transfer directly to the on-device code.

Formulas (from PROJECT_PLAN.md Section 6):
  S_confusion   = 0.40 * brow_furrow_norm + 0.30 * (1 - EAR_norm) + 0.30 * mouth_tension_norm
  S_frustration = 0.35 * brow_furrow_norm + 0.25 * (1 - EAR_norm) + 0.40 * mouth_downturn_norm
  S_engagement  = 0.50 * EAR_norm + 0.30 * (1 - brow_furrow_norm) + 0.20 * brow_height_norm

NOTE: This is a rule-based weighted sum, NOT a trained classifier.
NO scikit-learn. NO .fit(). NO model weights to learn.
"""

import numpy as np
import pandas as pd
from typing import Union


# ── Scoring weights (locked — must match AffectScorer.cs) ────────────────────
CONFUSION_WEIGHTS = {
    "brow_furrow":    0.40,
    "eye_closed":     0.30,   # (1 - EAR_norm)
    "mouth_tension":  0.30,
}

FRUSTRATION_WEIGHTS = {
    "brow_furrow":    0.35,
    "eye_closed":     0.25,   # (1 - EAR_norm)
    "mouth_downturn": 0.40,   # same feature as mouth_tension_norm for frustration
}

ENGAGEMENT_WEIGHTS = {
    "ear_open":       0.50,   # EAR_norm directly
    "brow_relaxed":   0.30,   # (1 - brow_furrow_norm)
    "brow_high":      0.20,   # brow_height_norm
}


def compute_confusion_score(brow_furrow_norm: float,
                             ear_norm: float,
                             mouth_tension_norm: float) -> float:
    """
    Compute the confusion score in [0, 1].

    Args:
        brow_furrow_norm:   High when brows are furrowed (pulled together).
        ear_norm:           Eye aspect ratio (high = eyes open, low = squinting).
        mouth_tension_norm: High when mouth corners are pulled down.

    Returns:
        Confusion score in [0.0, 1.0].
    """
    s = (CONFUSION_WEIGHTS["brow_furrow"]   * brow_furrow_norm
       + CONFUSION_WEIGHTS["eye_closed"]    * (1.0 - ear_norm)
       + CONFUSION_WEIGHTS["mouth_tension"] * mouth_tension_norm)
    return float(np.clip(s, 0.0, 1.0))


def compute_frustration_score(brow_furrow_norm: float,
                               ear_norm: float,
                               mouth_tension_norm: float) -> float:
    """
    Compute the frustration score in [0, 1].
    Frustration differs from confusion in weighting (more mouth, less eye).
    """
    s = (FRUSTRATION_WEIGHTS["brow_furrow"]    * brow_furrow_norm
       + FRUSTRATION_WEIGHTS["eye_closed"]     * (1.0 - ear_norm)
       + FRUSTRATION_WEIGHTS["mouth_downturn"] * mouth_tension_norm)
    return float(np.clip(s, 0.0, 1.0))


def compute_engagement_score(ear_norm: float,
                              brow_furrow_norm: float,
                              brow_height_norm: float) -> float:
    """
    Compute the engagement score in [0, 1].
    High engagement = eyes open, brows relaxed, brows lifted.
    """
    s = (ENGAGEMENT_WEIGHTS["ear_open"]     * ear_norm
       + ENGAGEMENT_WEIGHTS["brow_relaxed"] * (1.0 - brow_furrow_norm)
       + ENGAGEMENT_WEIGHTS["brow_high"]    * brow_height_norm)
    return float(np.clip(s, 0.0, 1.0))


def score_frame(features: dict) -> dict:
    """
    Given a feature dict (output of compute_features_from_landmarks),
    return all three affect scores.

    Args:
        features: dict with keys brow_furrow_norm, ear_norm,
                  mouth_tension_norm, brow_height_norm

    Returns:
        dict with confusion_score, frustration_score, engagement_score
        or None if features is None or any required key is missing.
    """
    if features is None:
        return None
    required = {"brow_furrow_norm", "ear_norm", "mouth_tension_norm", "brow_height_norm"}
    if not required.issubset(features.keys()):
        return None
    if any(features[k] is None for k in required):
        return None

    return {
        "confusion_score":    compute_confusion_score(
                                  features["brow_furrow_norm"],
                                  features["ear_norm"],
                                  features["mouth_tension_norm"]),
        "frustration_score":  compute_frustration_score(
                                  features["brow_furrow_norm"],
                                  features["ear_norm"],
                                  features["mouth_tension_norm"]),
        "engagement_score":   compute_engagement_score(
                                  features["ear_norm"],
                                  features["brow_furrow_norm"],
                                  features["brow_height_norm"]),
    }


def score_dataframe(features_df: pd.DataFrame) -> pd.DataFrame:
    """
    Apply scoring to a full DataFrame of per-frame features
    (as produced by extract_features_from_clip).

    Returns:
        DataFrame with added columns: confusion_score, frustration_score, engagement_score.
        Rows where face was not detected (face_detected=False) get NaN scores.
    """
    df = features_df.copy()

    scores = df.apply(
        lambda row: score_frame({
            "brow_furrow_norm":   row.get("brow_furrow_norm"),
            "ear_norm":           row.get("ear_norm"),
            "mouth_tension_norm": row.get("mouth_tension_norm"),
            "brow_height_norm":   row.get("brow_height_norm"),
        }) if row.get("face_detected", False) else None,
        axis=1
    )

    df["confusion_score"]    = scores.apply(lambda s: s["confusion_score"]    if s else float("nan"))
    df["frustration_score"]  = scores.apply(lambda s: s["frustration_score"]  if s else float("nan"))
    df["engagement_score"]   = scores.apply(lambda s: s["engagement_score"]   if s else float("nan"))

    return df


def compute_clip_level_stats(scored_df: pd.DataFrame) -> dict:
    """
    Aggregate per-frame scores to clip-level stats for threshold calibration.
    Returns mean, std, max, and 90th-percentile scores per affect dimension.
    """
    result = {}
    for dim in ("confusion_score", "frustration_score", "engagement_score"):
        valid = scored_df[dim].dropna()
        result[f"{dim}_mean"] = float(valid.mean()) if len(valid) > 0 else float("nan")
        result[f"{dim}_std"]  = float(valid.std())  if len(valid) > 0 else float("nan")
        result[f"{dim}_max"]  = float(valid.max())  if len(valid) > 0 else float("nan")
        result[f"{dim}_p90"]  = float(valid.quantile(0.90)) if len(valid) > 0 else float("nan")
        result[f"{dim}_n"]    = int(valid.count())
    return result


if __name__ == "__main__":
    print("score_affect_rulebased.py — formula sanity check")

    # Test with known feature values
    test_cases = [
        # (brow_furrow, ear, mouth_tension, brow_height, label)
        (0.0, 0.30, 0.0, 0.5, "neutral/engaged"),
        (0.8, 0.10, 0.7, 0.2, "high confusion + frustration"),
        (0.0, 0.35, 0.0, 0.8, "highly engaged"),
    ]

    for bf, ear, mt, bh, label in test_cases:
        c = compute_confusion_score(bf, ear, mt)
        f = compute_frustration_score(bf, ear, mt)
        e = compute_engagement_score(ear, bf, bh)
        print(f"  [{label}] confusion={c:.3f}, frustration={f:.3f}, engagement={e:.3f}")

    print("\nExpected: high confusion/frustration for case 2, high engagement for case 3.")
