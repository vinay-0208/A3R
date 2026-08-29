"""
test_affect_pipeline.py — Unit tests for the affect scoring pipeline

Tests the Python calibration tools without requiring any dataset, camera, or MediaPipe.
All tests run offline using synthetic / manually constructed inputs.

Run from project root:
    python -m pytest python_tools/daisee_calibration/test_affect_pipeline.py -v

Or directly:
    python python_tools/daisee_calibration/test_affect_pipeline.py
"""

import sys
import math
import unittest
from pathlib import Path

# Make sure imports resolve from this directory
sys.path.insert(0, str(Path(__file__).parent))

from score_affect_rulebased import (
    compute_confusion_score,
    compute_frustration_score,
    compute_engagement_score,
    score_frame,
    score_dataframe,
    compute_clip_level_stats,
    CONFUSION_WEIGHTS,
    FRUSTRATION_WEIGHTS,
    ENGAGEMENT_WEIGHTS,
)
from extract_features import compute_features_from_landmarks


# ── Helpers ────────────────────────────────────────────────────────────────────

def make_fake_landmarks(iod=0.20, brow_furrow_gap=0.12,
                         eye_open=True, mouth_down=False,
                         brow_height=0.08, face_height=0.80):
    """
    Build a list of 478 FakeLM objects with controlled geometry.
    All non-key landmarks sit at (0.5, 0.5, 0.0).

    Args:
        iod:             Inter-ocular distance (L_EYE_OUTER to R_EYE_OUTER), x-axis.
        brow_furrow_gap: Distance between L and R inner brow, x-axis.
        eye_open:        If True, eyes are open (EAR ~0.30). If False, squinted (~0.10).
        mouth_down:      If True, mouth corners are pulled below lip midline.
        brow_height:     Vertical distance from brow to eye-center (y-axis).
        face_height:     Total face height (top to chin), y-axis.
    """
    class FakeLM:
        def __init__(self, x=0.5, y=0.5, z=0.0):
            self.x, self.y, self.z = x, y, z

    lms = [FakeLM() for _ in range(478)]

    # IOD anchors
    cx = 0.5
    lms[33]  = FakeLM(cx - iod / 2, 0.5)  # L_EYE_OUTER
    lms[263] = FakeLM(cx + iod / 2, 0.5)  # R_EYE_OUTER

    # Face height
    lms[10]  = FakeLM(cx, 0.5 - face_height / 2)  # FACE_TOP
    lms[152] = FakeLM(cx, 0.5 + face_height / 2)  # FACE_BOT

    # Inner brows
    lms[105] = FakeLM(cx - brow_furrow_gap / 2, 0.5 - 0.12)  # L_INNER_BROW
    lms[334] = FakeLM(cx + brow_furrow_gap / 2, 0.5 - 0.12)  # R_INNER_BROW

    # Outer brows (for brow height)
    brow_y = 0.5 - 0.10 - brow_height
    lms[65]  = FakeLM(cx - 0.08, brow_y)   # L_OUTER_BROW
    lms[295] = FakeLM(cx + 0.08, brow_y)   # R_OUTER_BROW

    # Eyes
    eye_v = 0.04 if eye_open else 0.01   # vertical extent of open/squinted eye
    lms[133] = FakeLM(cx - 0.09, 0.5)    # L_EYE_INNER
    lms[159] = FakeLM(cx - 0.05, 0.5 - eye_v)  # L_EYE_TOP / center
    lms[145] = FakeLM(cx - 0.05, 0.5 + eye_v)  # L_EYE_BOT
    lms[362] = FakeLM(cx + 0.09, 0.5)    # R_EYE_INNER
    lms[386] = FakeLM(cx + 0.05, 0.5 - eye_v)  # R_EYE_TOP / center
    lms[374] = FakeLM(cx + 0.05, 0.5 + eye_v)  # R_EYE_BOT

    # Mouth
    lip_y = 0.5 + 0.20
    lms[13] = FakeLM(cx, lip_y - 0.01)  # UPPER_LIP
    lms[14] = FakeLM(cx, lip_y + 0.01)  # LOWER_LIP
    corner_y = lip_y + (0.03 if mouth_down else 0.0)
    lms[61]  = FakeLM(cx - 0.05, corner_y)  # L_MOUTH
    lms[291] = FakeLM(cx + 0.05, corner_y)  # R_MOUTH

    return lms


# ── Test Classes ──────────────────────────────────────────────────────────────

class TestWeights(unittest.TestCase):
    """Weights must sum to 1.0 (sanity check on the formula constants)."""

    def test_confusion_weights_sum(self):
        total = sum(CONFUSION_WEIGHTS.values())
        self.assertAlmostEqual(total, 1.0, places=5,
                               msg="Confusion weights must sum to 1.0")

    def test_frustration_weights_sum(self):
        total = sum(FRUSTRATION_WEIGHTS.values())
        self.assertAlmostEqual(total, 1.0, places=5,
                               msg="Frustration weights must sum to 1.0")

    def test_engagement_weights_sum(self):
        total = sum(ENGAGEMENT_WEIGHTS.values())
        self.assertAlmostEqual(total, 1.0, places=5,
                               msg="Engagement weights must sum to 1.0")


class TestScorerOutputRange(unittest.TestCase):
    """All scores must be in [0, 1] for any valid input."""

    def _check_range(self, score, name):
        self.assertGreaterEqual(score, 0.0, f"{name} below 0")
        self.assertLessEqual(score, 1.0, f"{name} above 1")

    def test_all_zeros(self):
        c = compute_confusion_score(0.0, 0.0, 0.0)
        f = compute_frustration_score(0.0, 0.0, 0.0)
        e = compute_engagement_score(0.0, 0.0, 0.0)
        self._check_range(c, "confusion(0,0,0)")
        self._check_range(f, "frustration(0,0,0)")
        self._check_range(e, "engagement(0,0,0)")

    def test_all_ones(self):
        c = compute_confusion_score(1.0, 1.0, 1.0)
        f = compute_frustration_score(1.0, 1.0, 1.0)
        e = compute_engagement_score(1.0, 1.0, 1.0)
        self._check_range(c, "confusion(1,1,1)")
        self._check_range(f, "frustration(1,1,1)")
        self._check_range(e, "engagement(1,1,1)")

    def test_clamp_on_extreme_inputs(self):
        # Even with out-of-range floats the output must be clamped
        c = compute_confusion_score(2.0, -1.0, 3.0)
        self._check_range(c, "confusion(extreme)")


class TestScorerFormulas(unittest.TestCase):
    """Verify the formula values match hand-calculated expectations."""

    def test_neutral_face_low_confusion(self):
        # Relaxed brows, open eyes, neutral mouth → low confusion
        brow_furrow = 0.05
        ear         = 0.30
        mouth       = 0.05
        c = compute_confusion_score(brow_furrow, ear, mouth)
        self.assertLess(c, 0.25, "Neutral face should give low confusion score")

    def test_confused_face_high_confusion(self):
        # Furrowed brows + squinted eyes + tense mouth → high confusion
        brow_furrow = 0.80
        ear         = 0.10
        mouth       = 0.70
        c = compute_confusion_score(brow_furrow, ear, mouth)
        self.assertGreater(c, 0.55, "Confused face should give high confusion score")

    def test_engaged_face(self):
        # Open eyes + relaxed brows + raised brows → high engagement
        ear         = 0.35
        brow_furrow = 0.05
        brow_height = 0.80
        e = compute_engagement_score(ear, brow_furrow, brow_height)
        self.assertGreater(e, 0.55, "Engaged face should give high engagement score")

    def test_formula_exact_values(self):
        # Hand-calculate: confusion(0.5, 0.5, 0.5)
        # = 0.40*0.5 + 0.30*(1-0.5) + 0.30*0.5 = 0.20 + 0.15 + 0.15 = 0.50
        c = compute_confusion_score(0.5, 0.5, 0.5)
        self.assertAlmostEqual(c, 0.50, places=5)

    def test_frustration_gt_confusion_high_mouth(self):
        # Frustration weights mouth more heavily (0.40 vs 0.30)
        # so with high mouth_tension, frustration should > confusion
        brow = 0.5
        ear  = 0.5
        mouth = 0.8
        c = compute_confusion_score(brow, ear, mouth)
        f = compute_frustration_score(brow, ear, mouth)
        self.assertGreater(f, c,
            "Frustration should exceed confusion when mouth_tension is high")


class TestScoreFrame(unittest.TestCase):
    """score_frame() with valid and invalid inputs."""

    def test_valid_input(self):
        features = {
            "brow_furrow_norm":   0.5,
            "ear_norm":           0.25,
            "mouth_tension_norm": 0.3,
            "brow_height_norm":   0.5,
        }
        result = score_frame(features)
        self.assertIsNotNone(result)
        self.assertIn("confusion_score", result)
        self.assertIn("frustration_score", result)
        self.assertIn("engagement_score", result)

    def test_none_input(self):
        self.assertIsNone(score_frame(None))

    def test_missing_key(self):
        features = {"brow_furrow_norm": 0.5, "ear_norm": 0.25}
        self.assertIsNone(score_frame(features))

    def test_none_value_in_features(self):
        features = {
            "brow_furrow_norm":   None,
            "ear_norm":           0.25,
            "mouth_tension_norm": 0.3,
            "brow_height_norm":   0.5,
        }
        self.assertIsNone(score_frame(features))


class TestFeatureExtractor(unittest.TestCase):
    """compute_features_from_landmarks() with controlled synthetic geometry."""

    def test_neutral_face_features_valid(self):
        lms = make_fake_landmarks()
        feats = compute_features_from_landmarks(lms)
        self.assertIsNotNone(feats, "Should return features for valid neutral face")
        self.assertIn("brow_furrow_norm", feats)
        self.assertIn("ear_norm", feats)
        self.assertIn("mouth_tension_norm", feats)
        self.assertIn("brow_height_norm", feats)

    def test_feature_range(self):
        lms = make_fake_landmarks()
        feats = compute_features_from_landmarks(lms)
        # Raw features are normalised (0-1) but NOT hard-clamped in extract_features.
        # Only the *scorer* clamps to [0,1]. So we only assert >= 0 here.
        # brow_furrow_norm, ear_norm, mouth_tension_norm are always in [0,1] by construction.
        # brow_height_norm may slightly exceed 1.0 for extreme synthetic geometries.
        for key in ("brow_furrow_norm", "ear_norm", "mouth_tension_norm"):
            v = feats[key]
            self.assertGreaterEqual(v, 0.0, f"{key} < 0")
            self.assertLessEqual(v, 1.0, f"{key} > 1")
        # brow_height_norm: just check it is a non-negative finite float
        bh = feats["brow_height_norm"]
        self.assertGreaterEqual(bh, 0.0, "brow_height_norm < 0")
        self.assertFalse(bh != bh, "brow_height_norm is NaN")  # NaN check

    def test_furrowed_brows_increase_furrow_score(self):
        # Narrow brow gap → higher brow_furrow_norm
        relaxed = make_fake_landmarks(brow_furrow_gap=0.18)
        furrowed = make_fake_landmarks(brow_furrow_gap=0.04)
        f_relaxed  = compute_features_from_landmarks(relaxed)
        f_furrowed = compute_features_from_landmarks(furrowed)
        self.assertGreater(f_furrowed["brow_furrow_norm"],
                           f_relaxed["brow_furrow_norm"],
                           "Narrower brow gap should produce higher brow_furrow_norm")

    def test_open_vs_squinted_ear(self):
        """Open eyes must produce higher EAR than squinted, using realistic geometry."""
        class LM:
            def __init__(self, x, y, z=0.0): self.x, self.y, self.z = x, y, z

        def make_realistic_lms(eye_v):
            """eye_v = vertical extent of eye opening. IOD = 0.20."""
            lms = [LM(0.5, 0.5) for _ in range(478)]
            cx = 0.5
            # IOD anchors
            lms[33]  = LM(cx - 0.10, 0.50)   # L_EYE_OUTER
            lms[263] = LM(cx + 0.10, 0.50)   # R_EYE_OUTER
            # Face
            lms[10]  = LM(cx, 0.10)
            lms[152] = LM(cx, 0.90)
            # Inner brows
            lms[105] = LM(cx - 0.04, 0.38)
            lms[334] = LM(cx + 0.04, 0.38)
            # Left eye: inner at (0.41,0.50), outer at (0.40,0.50), top/bot set by eye_v
            # h = dist(inner=133, outer=33) = dist((cx-0.09,0.50),(cx-0.10,0.50)) = 0.01  <- too small
            # Use wider inner/outer to get realistic EAR:
            lms[133] = LM(cx - 0.07, 0.50)   # L inner (h = 0.03 from outer)
            lms[159] = LM(cx - 0.085, 0.50 - eye_v)  # L top
            lms[145] = LM(cx - 0.085, 0.50 + eye_v)  # L bot
            lms[362] = LM(cx + 0.07, 0.50)   # R inner
            lms[386] = LM(cx + 0.085, 0.50 - eye_v)  # R top
            lms[374] = LM(cx + 0.085, 0.50 + eye_v)  # R bot
            # Outer brows
            lms[65]  = LM(cx - 0.08, 0.38)
            lms[295] = LM(cx + 0.08, 0.38)
            # Mouth
            lms[13] = LM(cx, 0.72); lms[14] = LM(cx, 0.74)
            lms[61] = LM(cx - 0.05, 0.73); lms[291] = LM(cx + 0.05, 0.73)
            return lms

        # Open eye: v=0.010 → EAR = 0.020/(2*0.03) = 0.333
        # Squinted: v=0.003 → EAR = 0.006/(2*0.03) = 0.100
        open_lms   = make_realistic_lms(eye_v=0.010)
        squint_lms = make_realistic_lms(eye_v=0.003)
        f_open   = compute_features_from_landmarks(open_lms)
        f_squint = compute_features_from_landmarks(squint_lms)
        self.assertIsNotNone(f_open,   "open face features must not be None")
        self.assertIsNotNone(f_squint, "squinted face features must not be None")
        self.assertGreater(f_open["ear_norm"], f_squint["ear_norm"],
                           "Open eyes should have higher EAR than squinted")

    def test_degenerate_iod_returns_none(self):
        lms = make_fake_landmarks(iod=0.0)
        feats = compute_features_from_landmarks(lms)
        self.assertIsNone(feats, "Zero IOD should return None (degenerate input)")

    def test_too_few_landmarks_returns_none(self):
        short_lms = [type('LM', (), {'x': 0.5, 'y': 0.5, 'z': 0.0})() for _ in range(10)]
        feats = compute_features_from_landmarks(short_lms)
        self.assertIsNone(feats, "< 478 landmarks should return None")


class TestClipLevelStats(unittest.TestCase):
    """compute_clip_level_stats() with a synthetic scored DataFrame."""

    def _make_df(self):
        import pandas as pd
        import numpy as np
        rng = np.random.default_rng(0)
        n = 50
        return pd.DataFrame({
            "face_detected":      [True] * n,
            "confusion_score":    rng.uniform(0.3, 0.8, n),
            "frustration_score":  rng.uniform(0.2, 0.7, n),
            "engagement_score":   rng.uniform(0.4, 0.9, n),
        })

    def test_stats_keys_present(self):
        df = self._make_df()
        stats = compute_clip_level_stats(df)
        for dim in ("confusion_score", "frustration_score", "engagement_score"):
            for suffix in ("mean", "std", "max", "p90", "n"):
                self.assertIn(f"{dim}_{suffix}", stats,
                              f"Missing key: {dim}_{suffix}")

    def test_stats_range(self):
        df = self._make_df()
        stats = compute_clip_level_stats(df)
        for dim in ("confusion_score", "frustration_score", "engagement_score"):
            self.assertGreaterEqual(stats[f"{dim}_mean"], 0.0)
            self.assertLessEqual(stats[f"{dim}_mean"], 1.0)
            self.assertGreaterEqual(stats[f"{dim}_p90"], stats[f"{dim}_mean"])

    def test_no_scikit_learn_import(self):
        """Verify scikit-learn is not actually imported in the scoring module."""
        import sys
        # Import the module fresh
        import score_affect_rulebased  # noqa: F401
        # Check that sklearn did not end up in sys.modules as a side effect
        sklearn_modules = [k for k in sys.modules if "sklearn" in k]
        self.assertEqual(sklearn_modules, [],
            f"scikit-learn must not be imported; found: {sklearn_modules}")


if __name__ == "__main__":
    unittest.main(verbosity=2)
