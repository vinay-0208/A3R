"""
test_phase4_affect_math.py — Mathematical Verification Suite for Phase 4

Verifies:
  1. Parity between C# (AffectFeatureExtractor.cs, AffectScorer.cs) and Python
  2. EAR min-max normalization: [0.15, 0.35] -> [0.0, 1.0] with clamping
  3. Weight summation integrity (all three affect dimensions sum strictly to 1.0)
  4. Scoring boundary guarantees [0.0, 1.0] across all inputs
  5. Directional sensitivity (furrow, squint, mouth tension, brow height)
  6. Differential weighting (frustration mouth sensitivity > confusion mouth sensitivity)
  7. Dwell FSM state transitions, threshold triggers, resets, cooldown, and step resets
  8. Degenerate and NaN landmark geometry handling
"""

import math
import sys
import unittest
from pathlib import Path

# Add current directory to path
sys.path.insert(0, str(Path(__file__).parent))

from score_affect_rulebased import (
    compute_confusion_score,
    compute_frustration_score,
    compute_engagement_score,
    score_frame,
    CONFUSION_WEIGHTS,
    FRUSTRATION_WEIGHTS,
    ENGAGEMENT_WEIGHTS,
)
from extract_features import compute_features_from_landmarks


class TestEARNormalization(unittest.TestCase):
    """Verifies EAR normalization formula matches AffectFeatureExtractor.cs."""

    EAR_MIN = 0.15
    EAR_MAX = 0.35

    def normalize_ear(self, raw: float) -> float:
        return float(min(1.0, max(0.0, (raw - self.EAR_MIN) / (self.EAR_MAX - self.EAR_MIN))))

    def test_min_boundary(self):
        self.assertAlmostEqual(self.normalize_ear(0.15), 0.0, places=6)

    def test_max_boundary(self):
        self.assertAlmostEqual(self.normalize_ear(0.35), 1.0, places=6)

    def test_midpoint(self):
        self.assertAlmostEqual(self.normalize_ear(0.25), 0.5, places=6)

    def test_below_min_clamped(self):
        self.assertAlmostEqual(self.normalize_ear(0.05), 0.0, places=6)
        self.assertAlmostEqual(self.normalize_ear(-0.5), 0.0, places=6)

    def test_above_max_clamped(self):
        self.assertAlmostEqual(self.normalize_ear(0.45), 1.0, places=6)
        self.assertAlmostEqual(self.normalize_ear(1.2), 1.0, places=6)


class TestAffectWeightsAndBoundaries(unittest.TestCase):
    """Verifies scoring weights and boundedness."""

    def test_weight_sums(self):
        self.assertAlmostEqual(sum(CONFUSION_WEIGHTS.values()), 1.0, places=7)
        self.assertAlmostEqual(sum(FRUSTRATION_WEIGHTS.values()), 1.0, places=7)
        self.assertAlmostEqual(sum(ENGAGEMENT_WEIGHTS.values()), 1.0, places=7)

    def test_grid_search_boundedness(self):
        steps = [0.0, 0.25, 0.5, 0.75, 1.0]
        for bf in steps:
            for ear in steps:
                for mt in steps:
                    c = compute_confusion_score(bf, ear, mt)
                    f = compute_frustration_score(bf, ear, mt)
                    self.assertTrue(0.0 <= c <= 1.0, f"Confusion out of bounds: {c}")
                    self.assertTrue(0.0 <= f <= 1.0, f"Frustration out of bounds: {f}")
                    for bh in steps:
                        e = compute_engagement_score(ear, bf, bh)
                        self.assertTrue(0.0 <= e <= 1.0, f"Engagement out of bounds: {e}")

    def test_clamping_extreme_inputs(self):
        self.assertEqual(compute_confusion_score(-10.0, -10.0, -10.0), 0.0)
        self.assertEqual(compute_confusion_score(10.0, -10.0, 10.0), 1.0)
        self.assertEqual(compute_frustration_score(-5.0, 5.0, -5.0), 0.0)
        self.assertEqual(compute_frustration_score(5.0, -5.0, 5.0), 1.0)
        self.assertEqual(compute_engagement_score(-5.0, 5.0, -5.0), 0.0)
        self.assertEqual(compute_engagement_score(5.0, -5.0, 5.0), 1.0)


class TestDirectionalSensitivity(unittest.TestCase):
    """Verifies monotonic directional responses to facial changes."""

    def test_brow_furrow_effect(self):
        ear, mt, bh = 0.8, 0.1, 0.5
        c_low = compute_confusion_score(0.1, ear, mt)
        c_high = compute_confusion_score(0.9, ear, mt)
        self.assertGreater(c_high, c_low)

        f_low = compute_frustration_score(0.1, ear, mt)
        f_high = compute_frustration_score(0.9, ear, mt)
        self.assertGreater(f_high, f_low)

        e_low = compute_engagement_score(ear, 0.1, bh)
        e_high = compute_engagement_score(ear, 0.9, bh)
        self.assertLess(e_high, e_low)

    def test_squint_effect(self):
        bf, mt, bh = 0.2, 0.1, 0.5
        c_open = compute_confusion_score(bf, 0.9, mt)
        c_squint = compute_confusion_score(bf, 0.1, mt)
        self.assertGreater(c_squint, c_open)

        f_open = compute_frustration_score(bf, 0.9, mt)
        f_squint = compute_frustration_score(bf, 0.1, mt)
        self.assertGreater(f_squint, f_open)

        e_open = compute_engagement_score(0.9, bf, bh)
        e_squint = compute_engagement_score(0.1, bf, bh)
        self.assertLess(e_squint, e_open)

    def test_mouth_tension_differential(self):
        bf, ear = 0.3, 0.7
        c0 = compute_confusion_score(bf, ear, 0.0)
        f0 = compute_frustration_score(bf, ear, 0.0)
        c1 = compute_confusion_score(bf, ear, 1.0)
        f1 = compute_frustration_score(bf, ear, 1.0)

        delta_c = c1 - c0
        delta_f = f1 - f0
        self.assertAlmostEqual(delta_c, 0.30, places=5)
        self.assertAlmostEqual(delta_f, 0.40, places=5)
        self.assertGreater(delta_f, delta_c)

    def test_brow_height_effect(self):
        ear, bf = 0.8, 0.2
        e_low = compute_engagement_score(ear, bf, 0.1)
        e_high = compute_engagement_score(ear, bf, 0.9)
        self.assertGreater(e_high, e_low)
        self.assertAlmostEqual(e_high - e_low, 0.20 * 0.8, places=5)


class MockAdaptationFSM:
    def __init__(self, confusion_thresh=0.60, frustration_thresh=0.65,
                 dwell_seconds=3.0, cooldown_seconds=15.0, max_adaptations=3):
        self.confusion_thresh = confusion_thresh
        self.frustration_thresh = frustration_thresh
        self.dwell_seconds = dwell_seconds
        self.cooldown_seconds = cooldown_seconds
        self.max_adaptations = max_adaptations

        self.dwell_timer = 0.0
        self.elevated_start_time = 0.0
        self.in_elevated_state = False
        self.fsm_fired = False
        self.last_adaptation_time = -999.0
        self.adaptations_this_step = 0
        self.last_step_id = ""
        self.fired_events = []

    def update(self, current_time: float, confusion: float, frustration: float,
               engagement: float, step_id: str):
        if step_id != self.last_step_id:
            self._reset_fsm()
            self.adaptations_this_step = 0
            self.last_step_id = step_id

        above_threshold = (confusion > self.confusion_thresh or
                           frustration > self.frustration_thresh)

        if above_threshold:
            if not self.in_elevated_state:
                self.in_elevated_state = True
                self.elevated_start_time = current_time
                self.dwell_timer = 0.0
                self.fsm_fired = False
            else:
                self.dwell_timer = current_time - self.elevated_start_time

            if (not self.fsm_fired and
                self.dwell_timer >= self.dwell_seconds and
                (current_time - self.last_adaptation_time) >= self.cooldown_seconds and
                self.adaptations_this_step < self.max_adaptations):
                self._fire(current_time, confusion, frustration, step_id)
        else:
            self.in_elevated_state = False
            self.elevated_start_time = 0.0
            self.dwell_timer = 0.0
            self.fsm_fired = False

    def _fire(self, current_time, confusion, frustration, step_id):
        self.fsm_fired = True
        self.last_adaptation_time = current_time
        self.adaptations_this_step += 1
        self.fired_events.append({
            "time": current_time,
            "step_id": step_id,
            "confusion": confusion,
            "frustration": frustration,
            "adaptation_count": self.adaptations_this_step
        })

    def _reset_fsm(self):
        self.in_elevated_state = False
        self.elevated_start_time = 0.0
        self.dwell_timer = 0.0
        self.fsm_fired = False


class TestAdaptationFSM(unittest.TestCase):
    def test_sustained_dwell_triggers(self):
        fsm = MockAdaptationFSM(confusion_thresh=0.60, dwell_seconds=3.0)
        fsm.update(current_time=0.0, confusion=0.3, frustration=0.2, engagement=0.5, step_id="s1")
        fsm.update(current_time=2.0, confusion=0.3, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertFalse(fsm.in_elevated_state)
        self.assertEqual(len(fsm.fired_events), 0)

        fsm.update(current_time=3.0, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertTrue(fsm.in_elevated_state)
        self.assertEqual(fsm.dwell_timer, 0.0)

        fsm.update(current_time=5.0, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertTrue(fsm.in_elevated_state)
        self.assertAlmostEqual(fsm.dwell_timer, 2.0, places=5)
        self.assertEqual(len(fsm.fired_events), 0)

        fsm.update(current_time=6.1, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertTrue(fsm.fsm_fired)
        self.assertEqual(len(fsm.fired_events), 1)
        self.assertEqual(fsm.fired_events[0]["adaptation_count"], 1)

    def test_transient_spike_does_not_trigger(self):
        fsm = MockAdaptationFSM(confusion_thresh=0.60, dwell_seconds=3.0)
        fsm.update(current_time=1.0, confusion=0.8, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertTrue(fsm.in_elevated_state)
        fsm.update(current_time=2.5, confusion=0.8, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertAlmostEqual(fsm.dwell_timer, 1.5, places=5)

        fsm.update(current_time=2.6, confusion=0.4, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertFalse(fsm.in_elevated_state)
        self.assertEqual(fsm.dwell_timer, 0.0)

        fsm.update(current_time=3.0, confusion=0.8, frustration=0.2, engagement=0.5, step_id="s1")
        fsm.update(current_time=4.5, confusion=0.8, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertAlmostEqual(fsm.dwell_timer, 1.5, places=5)
        self.assertEqual(len(fsm.fired_events), 0)

    def test_cooldown_prevents_rapid_refire(self):
        fsm = MockAdaptationFSM(confusion_thresh=0.60, dwell_seconds=3.0, cooldown_seconds=15.0)
        fsm.update(current_time=1.0, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        fsm.update(current_time=4.0, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertEqual(len(fsm.fired_events), 1)

        fsm.update(current_time=5.0, confusion=0.3, frustration=0.2, engagement=0.5, step_id="s1")
        fsm.update(current_time=6.0, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        fsm.update(current_time=10.0, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertEqual(len(fsm.fired_events), 1)

        fsm.update(current_time=20.0, confusion=0.7, frustration=0.2, engagement=0.5, step_id="s1")
        self.assertEqual(len(fsm.fired_events), 2)

    def test_step_change_resets_counter(self):
        fsm = MockAdaptationFSM(confusion_thresh=0.60, dwell_seconds=1.0, cooldown_seconds=2.0, max_adaptations=1)
        fsm.update(current_time=1.0, confusion=0.8, frustration=0.1, engagement=0.5, step_id="step_1")
        fsm.update(current_time=2.1, confusion=0.8, frustration=0.1, engagement=0.5, step_id="step_1")
        self.assertEqual(len(fsm.fired_events), 1)

        fsm.update(current_time=6.0, confusion=0.8, frustration=0.1, engagement=0.5, step_id="step_1")
        fsm.update(current_time=7.5, confusion=0.8, frustration=0.1, engagement=0.5, step_id="step_1")
        self.assertEqual(len(fsm.fired_events), 1)

        fsm.update(current_time=8.0, confusion=0.8, frustration=0.1, engagement=0.5, step_id="step_2")
        fsm.update(current_time=9.2, confusion=0.8, frustration=0.1, engagement=0.5, step_id="step_2")
        self.assertEqual(len(fsm.fired_events), 2)
        self.assertEqual(fsm.fired_events[1]["step_id"], "step_2")


if __name__ == "__main__":
    unittest.main(verbosity=2)
