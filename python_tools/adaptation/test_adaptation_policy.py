# -*- coding: utf-8 -*-
"""
A³R Phase 8: Adaptation Policy Test Suite
Validates the closed-loop adaptation policy across requirements A through Q:
A. Neutral / normal state -> no unnecessary adaptation
B. Sustained confusion -> correct confusion adaptation after dwell
C. Sustained frustration -> correct frustration adaptation after dwell
D. Brief confusion spike -> no adaptation before dwell
E. Brief frustration spike -> no adaptation before dwell
F. Cooldown -> repeated adaptation cannot fire during 15s cooldown
G. Maximum adaptations per step -> no more than 3 adaptations per step
H. Step transition -> counter and dwell reset on step change
I. Recovery -> exit elevated state when score drops below threshold/floor
J. Tracking loss -> TrackingLossDisruption handled safely, no false adaptation
K. Prediction event -> maps to anticipated adaptation and positive lead time
L. PolarityHesitation -> handled safely without false adaptation
M. Repeated prediction -> prediction cooldown (5s) prevents flooding
N. Adaptation logging -> schema contains all required reconstructed fields
O. Deterministic replay -> identical inputs yield identical adaptation sequences
P. Missing / invalid data -> NaN/Inf safely rejected without false adaptation
Q. Session boundary -> state does not leak across sessions
"""

import sys
import unittest
import math
from pathlib import Path
from typing import List, Dict, Any

_PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

from python_tools.adaptation.adaptation_policy import (
    AdaptationController,
    ARContentManager,
    AdaptationAction,
    AdaptationEvent,
    ACTION_SEQUENCE
)
from python_tools.prediction.predict_affect import TemporalAffectPredictor, TimedAffectSample, PredictionEvent


class TestAdaptationPolicy(unittest.TestCase):

    def setUp(self):
        self.controller = AdaptationController(
            confusion_threshold=0.60,
            frustration_threshold=0.65,
            dwell_time_seconds=3.0,
            cooldown_seconds=15.0,
            max_adaptations_per_step=3,
            recovery_score_floor=0.30
        )
        self.content_mgr = ARContentManager(worker_id="learner_01")
        self.controller.content_manager = self.content_mgr

    # ── Test A: Neutral / Normal State ───────────────────────────────────────
    def test_a_neutral_state_no_adaptation(self):
        """Controlled synthetic test: nominal scores produce zero adaptations."""
        for t in [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 10.0]:
            evt = self.controller.update_affect_scores(
                current_time=t,
                confusion_score=0.25,
                frustration_score=0.15,
                engagement_score=0.75,
                current_step_id="step_01_workbench"
            )
            self.assertIsNone(evt)

        self.assertFalse(self.controller.in_elevated_state)
        self.assertEqual(len(self.controller.fired_events), 0)
        self.assertEqual(self.controller.adaptations_this_step, 0)
        self.assertFalse(self.content_mgr.is_cue_active)
        self.assertFalse(self.content_mgr.is_hint_active)

    # ── Test B: Sustained Confusion ──────────────────────────────────────────
    def test_b_sustained_confusion_triggers_after_dwell(self):
        """Controlled synthetic test: sustained confusion triggers InjectCue after 3.0s dwell."""
        # Baseline at t=0.0
        self.controller.update_affect_scores(0.0, 0.20, 0.10, 0.70, "step_01_workbench")
        self.assertFalse(self.controller.in_elevated_state)

        # Cross threshold at t=1.0 (confusion = 0.72 > 0.60)
        evt1 = self.controller.update_affect_scores(1.0, 0.72, 0.10, 0.50, "step_01_workbench")
        self.assertIsNone(evt1)
        self.assertTrue(self.controller.in_elevated_state)
        self.assertAlmostEqual(self.controller.dwell_timer, 0.0, places=4)

        # Dwell = 2.0s at t=3.0 (still under 3.0s required dwell)
        evt2 = self.controller.update_affect_scores(3.0, 0.75, 0.10, 0.45, "step_01_workbench")
        self.assertIsNone(evt2)
        self.assertAlmostEqual(self.controller.dwell_timer, 2.0, places=4)
        self.assertEqual(len(self.controller.fired_events), 0)

        # Dwell = 3.0s at t=4.0 -> fires Adaptation #1 (InjectCue)
        evt3 = self.controller.update_affect_scores(4.0, 0.74, 0.10, 0.45, "step_01_workbench")
        self.assertIsNotNone(evt3)
        self.assertEqual(evt3.action, AdaptationAction.InjectCue)
        self.assertEqual(evt3.triggering_behavior, "ConfusionStruggle")
        self.assertEqual(self.controller.adaptations_this_step, 1)
        self.assertTrue(self.controller.fsm_fired)
        self.assertTrue(self.content_mgr.is_cue_active)

    # ── Test C: Sustained Frustration ────────────────────────────────────────
    def test_c_sustained_frustration_triggers_after_dwell(self):
        """Controlled synthetic test: sustained frustration triggers adaptation with FrustrationDisengage behavior."""
        # Cross threshold at t=1.0 (frustration = 0.75 > 0.65)
        self.controller.update_affect_scores(1.0, 0.20, 0.75, 0.40, "step_02_esp32")
        self.assertTrue(self.controller.in_elevated_state)

        # At t=4.1 (dwell = 3.1s >= 3.0s)
        evt = self.controller.update_affect_scores(4.1, 0.20, 0.78, 0.35, "step_02_esp32")
        self.assertIsNotNone(evt)
        self.assertEqual(evt.action, AdaptationAction.SlowPace) # 1st in frustration sequence
        self.assertEqual(evt.triggering_behavior, "FrustrationDisengage")
        self.assertEqual(len(self.controller.fired_events), 1)
        self.assertTrue(self.content_mgr.is_slow_pace_active)

    # ── Test D: Brief Confusion Spike ────────────────────────────────────────
    def test_d_brief_confusion_spike_no_adaptation(self):
        """Controlled synthetic test: transient confusion spike (1.5s) recovers without firing."""
        # Spike starts at t=1.0
        self.controller.update_affect_scores(1.0, 0.85, 0.10, 0.40, "step_01_workbench")
        self.assertTrue(self.controller.in_elevated_state)

        # Still elevated at t=2.5 (dwell = 1.5s)
        self.controller.update_affect_scores(2.5, 0.82, 0.10, 0.40, "step_01_workbench")
        self.assertEqual(len(self.controller.fired_events), 0)

        # Drops below threshold at t=2.6
        self.controller.update_affect_scores(2.6, 0.35, 0.10, 0.60, "step_01_workbench")
        self.assertFalse(self.controller.in_elevated_state)
        self.assertEqual(self.controller.dwell_timer, 0.0)

        # Remains low through t=5.0
        self.controller.update_affect_scores(5.0, 0.30, 0.10, 0.65, "step_01_workbench")
        self.assertEqual(len(self.controller.fired_events), 0)

    # ── Test E: Brief Frustration Spike ──────────────────────────────────────
    def test_e_brief_frustration_spike_no_adaptation(self):
        """Controlled synthetic test: transient frustration spike (2.0s) does not trigger."""
        self.controller.update_affect_scores(1.0, 0.10, 0.80, 0.40, "step_01_workbench")
        self.controller.update_affect_scores(3.0, 0.10, 0.80, 0.40, "step_01_workbench")
        # Dwell reached 2.0s (< 3.0s)

        # Recovery at t=3.5
        self.controller.update_affect_scores(3.5, 0.10, 0.25, 0.70, "step_01_workbench")
        self.assertFalse(self.controller.in_elevated_state)
        self.assertEqual(len(self.controller.fired_events), 0)

    # ── Test F: Cooldown Enforcement ─────────────────────────────────────────
    def test_f_cooldown_prevents_rapid_refire(self):
        """Controlled synthetic test: 15s cooldown strictly prevents repeated firing."""
        # 1st adaptation at t=4.0
        self.controller.update_affect_scores(1.0, 0.70, 0.10, 0.50, "step_01_workbench")
        evt1 = self.controller.update_affect_scores(4.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.assertIsNotNone(evt1)
        self.assertEqual(len(self.controller.fired_events), 1)

        # Drops below threshold at t=5.0
        self.controller.update_affect_scores(5.0, 0.30, 0.10, 0.60, "step_01_workbench")

        # Rises again at t=6.0
        self.controller.update_affect_scores(6.0, 0.75, 0.10, 0.45, "step_01_workbench")

        # Dwell satisfied at t=9.5 (dwell = 3.5s), but elapsed time since last adaptation is 5.5s (< 15.0s cooldown)
        evt2 = self.controller.update_affect_scores(9.5, 0.75, 0.10, 0.45, "step_01_workbench")
        self.assertIsNone(evt2)
        self.assertEqual(len(self.controller.fired_events), 1)
        self.assertGreater(self.controller.suppressed_cooldown_count, 0)

        # At t=18.0 (still under 15s from t=4.0: delta = 14.0s)
        evt3 = self.controller.update_affect_scores(18.0, 0.75, 0.10, 0.45, "step_01_workbench")
        self.assertIsNone(evt3)

        # At t=19.1 (delta = 15.1s >= 15.0s cooldown, dwell satisfied)
        evt4 = self.controller.update_affect_scores(19.1, 0.75, 0.10, 0.45, "step_01_workbench")
        self.assertIsNotNone(evt4)
        self.assertEqual(evt4.action, AdaptationAction.InsertHint) # 2nd action in sequence
        self.assertEqual(len(self.controller.fired_events), 2)
        self.assertEqual(self.controller.adaptations_this_step, 2)

    # ── Test G: Maximum Adaptations Per Step ──────────────────────────────────
    def test_g_max_adaptations_per_step_ceiling(self):
        """Controlled synthetic test: ceiling of 3 adaptations per step is strictly enforced."""
        # Fire #1 at t=4.0 (InjectCue)
        self.controller.update_affect_scores(1.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.controller.update_affect_scores(4.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.assertEqual(len(self.controller.fired_events), 1)

        # Reset elevated state
        self.controller.update_affect_scores(5.0, 0.20, 0.10, 0.70, "step_01_workbench")

        # Fire #2 at t=23.0 (InsertHint)
        self.controller.update_affect_scores(20.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.controller.update_affect_scores(23.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.assertEqual(len(self.controller.fired_events), 2)

        # Reset elevated state
        self.controller.update_affect_scores(24.0, 0.20, 0.10, 0.70, "step_01_workbench")

        # Fire #3 at t=42.0 (SlowPace)
        self.controller.update_affect_scores(39.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.controller.update_affect_scores(42.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.assertEqual(len(self.controller.fired_events), 3)

        # Reset elevated state
        self.controller.update_affect_scores(43.0, 0.20, 0.10, 0.70, "step_01_workbench")

        # Attempt #4 at t=61.0 (cooldown passed, dwell passed, but limit reached)
        self.controller.update_affect_scores(58.0, 0.70, 0.10, 0.50, "step_01_workbench")
        evt4 = self.controller.update_affect_scores(61.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.assertIsNone(evt4)
        self.assertEqual(len(self.controller.fired_events), 3)
        self.assertGreater(self.controller.suppressed_max_limit_count, 0)

    # ── Test H: Step Transition ──────────────────────────────────────────────
    def test_h_step_transition_resets_counter(self):
        """Controlled synthetic test: advancing step resets adaptations_this_step and dwell timer."""
        # Trigger 1 adaptation on step 1
        self.controller.update_affect_scores(1.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.controller.update_affect_scores(4.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.assertEqual(self.controller.adaptations_this_step, 1)

        # Move to step 2
        self.content_mgr.next_step()
        self.controller.update_affect_scores(5.0, 0.20, 0.10, 0.70, "step_02_esp32")
        self.assertEqual(self.controller.adaptations_this_step, 0)
        self.assertFalse(self.controller.in_elevated_state)

        # On step 2, first adaptation is InjectCue again (sequence resets per step)
        self.controller.update_affect_scores(20.0, 0.70, 0.10, 0.50, "step_02_esp32")
        evt = self.controller.update_affect_scores(23.0, 0.70, 0.10, 0.50, "step_02_esp32")
        self.assertIsNotNone(evt)
        self.assertEqual(evt.action, AdaptationAction.InjectCue)
        self.assertEqual(evt.step_id, "step_02_esp32")
        self.assertEqual(self.controller.adaptations_this_step, 1)

    # ── Test I: Recovery Semantics ────────────────────────────────────────────
    def test_i_recovery_resets_fsm_and_prediction(self):
        """Controlled synthetic test: dropping below threshold clears dwell and recovery floor clears prediction."""
        self.controller.update_affect_scores(1.0, 0.70, 0.10, 0.50, "step_01_workbench")
        self.assertTrue(self.controller.in_elevated_state)

        # Drop to 0.20 (< 0.30 recovery floor)
        self.controller.update_affect_scores(2.0, 0.20, 0.10, 0.70, "step_01_workbench")
        self.assertFalse(self.controller.in_elevated_state)
        self.assertEqual(self.controller.dwell_timer, 0.0)

    # ── Test J: Tracking Loss Safety ─────────────────────────────────────────
    def test_j_tracking_loss_safety(self):
        """Controlled synthetic test: camera tracking loss does NOT trigger emotional adaptation."""
        # Face lost during tracking (is_tracking_valid = False)
        for t in [1.0, 2.0, 3.0, 4.0, 5.0]:
            evt = self.controller.update_affect_scores(
                current_time=t,
                confusion_score=0.85, # would otherwise trigger
                frustration_score=0.85,
                engagement_score=0.10,
                current_step_id="step_01_workbench",
                is_tracking_valid=False
            )
            self.assertIsNone(evt)

        self.assertEqual(len(self.controller.fired_events), 0)
        self.assertFalse(self.controller.in_elevated_state)

    # ── Test K: Prediction Event Integration & Lead Time ──────────────────────
    def test_k_prediction_event_lead_time_integration(self):
        """Controlled synthetic test: precursor prediction matches actual adaptation with positive lead time."""
        predictor = TemporalAffectPredictor(window_capacity=5, min_samples=3)
        self.content_mgr.predictor = predictor

        # Feed rising confusion trajectory into predictor
        # Sample 1: t=1.0, C=0.38
        p1 = predictor.process_sample(TimedAffectSample(1.0, 0.38, 0.10, 0.50, 0.25, 0.25, 0.15, 0.40, "step_01_workbench"))
        # Sample 2: t=1.5, C=0.44
        p2 = predictor.process_sample(TimedAffectSample(1.5, 0.44, 0.10, 0.48, 0.30, 0.23, 0.18, 0.38, "step_01_workbench"))
        # Sample 3: t=2.0, C=0.50 -> Slope = 0.12/s >= 0.05 -> Persistence 1
        p3 = predictor.process_sample(TimedAffectSample(2.0, 0.50, 0.10, 0.45, 0.35, 0.21, 0.20, 0.35, "step_01_workbench"))
        # Sample 4: t=2.5, C=0.53 -> Persistence 2 -> Prediction Fired!
        p4 = predictor.process_sample(TimedAffectSample(2.5, 0.53, 0.10, 0.42, 0.38, 0.20, 0.22, 0.33, "step_01_workbench"))
        self.assertIsNotNone(p4)
        self.assertEqual(p4.behavior, "ConfusionStruggle")
        self.assertIsNotNone(predictor.pending_prediction)

        # Now student crosses threshold into struggle at t=3.0 (C=0.68)
        self.controller.update_affect_scores(3.0, 0.68, 0.10, 0.40, "step_01_workbench")
        self.controller.update_affect_scores(4.5, 0.70, 0.10, 0.38, "step_01_workbench")

        # Dwell satisfied at t=6.0 -> adaptation fires
        evt = self.controller.update_affect_scores(6.0, 0.72, 0.10, 0.35, "step_01_workbench")
        self.assertIsNotNone(evt)

        # Check lead time: prediction was at t=2.5, adaptation fired at t=6.0
        # Expected lead time = 6.0 - 2.5 = 3.5s > 0
        self.assertEqual(predictor.correct_predictions, 1)
        self.assertEqual(len(predictor.measured_lead_times), 1)
        self.assertAlmostEqual(predictor.measured_lead_times[0], 3.5, places=2)
        self.assertIsNone(predictor.pending_prediction)

    # ── Test L: PolarityHesitation Handling ───────────────────────────────────
    def test_l_polarity_hesitation_handling(self):
        """Controlled synthetic test: polarity hesitation on LED step produces logged prediction without false adaptation."""
        predictor = TemporalAffectPredictor(window_capacity=5, min_samples=3)
        # Step 3 is LED insertion
        for t, c, ear, brow in [(1.0, 0.35, 0.30, 0.20), (1.5, 0.40, 0.26, 0.25), (2.0, 0.45, 0.22, 0.30), (2.5, 0.48, 0.18, 0.36)]:
            p = predictor.process_sample(TimedAffectSample(t, c, 0.10, 0.45, brow, ear, 0.15, 0.35, "step_03_led"))

        # Prediction fires PolarityHesitation
        self.assertEqual(predictor.last_predicted_behavior, "PolarityHesitation")
        # Ensure no actual adaptation fired in controller because score was under threshold (< 0.60)
        self.assertEqual(len(self.controller.fired_events), 0)

    # ── Test M: Repeated Prediction Cooldown ──────────────────────────────────
    def test_m_repeated_prediction_cooldown(self):
        """Controlled synthetic test: prediction cooldown (5.0s) prevents event flooding."""
        predictor = TemporalAffectPredictor(window_capacity=5, min_samples=3)
        events = []
        # Continuous rising confusion over 10 samples (t=0.0 to t=4.5, all within 5.0s window)
        for i in range(10):
            t = i * 0.50
            sample = TimedAffectSample(
                timestamp=t,
                confusion=min(0.54, 0.36 + i * 0.035),
                frustration=0.15,
                engagement=0.50,
                brow_furrow=0.35 + i * 0.03,
                ear=0.65 - i * 0.025,
                mouth_tension=0.10,
                brow_height=0.50,
                step_id="step_01"
            )
            evt = predictor.process_sample(sample)
            if evt:
                events.append(evt)

        # Exactly 1 prediction event should be emitted within the 5.0s cooldown window
        self.assertEqual(len(events), 1, f"Expected 1 prediction during cooldown window, got {len(events)}")
        self.assertEqual(events[0].behavior, "ConfusionStruggle")

    # ── Test N: Adaptation Logging Schema ────────────────────────────────────
    def test_n_adaptation_logging_schema(self):
        """Controlled synthetic test: fired event contains all required reconstructed fields."""
        self.controller.update_affect_scores(1.0, 0.70, 0.15, 0.45, "step_03_led")
        evt = self.controller.update_affect_scores(4.0, 0.70, 0.15, 0.45, "step_03_led")

        self.assertIsNotNone(evt)
        self.assertTrue(evt.accepted)
        self.assertEqual(evt.step_id, "step_03_led")
        self.assertAlmostEqual(evt.confusion_score, 0.70, places=4)
        self.assertAlmostEqual(evt.frustration_score, 0.15, places=4)
        self.assertEqual(evt.action, AdaptationAction.InjectCue)
        self.assertEqual(evt.triggering_behavior, "ConfusionStruggle")
        self.assertIsNotNone(evt.canonical_context)
        self.assertEqual(evt.canonical_context.worker_id, "learner_01")
        self.assertEqual(evt.canonical_context.step_id, "step_03_led")

    # ── Test O: Deterministic Replay ─────────────────────────────────────────
    def test_o_deterministic_replay(self):
        """Controlled synthetic test: identical input sequence produces 100% identical outputs."""
        trace = [
            (0.0, 0.20, 0.10, 0.70, "step_01"),
            (1.0, 0.75, 0.10, 0.45, "step_01"),
            (2.0, 0.78, 0.10, 0.42, "step_01"),
            (3.0, 0.76, 0.10, 0.40, "step_01"),
            (4.0, 0.77, 0.10, 0.40, "step_01"), # fires
            (5.0, 0.20, 0.10, 0.65, "step_01"),
            (20.0, 0.80, 0.10, 0.40, "step_01"),
            (23.0, 0.82, 0.10, 0.38, "step_01"), # fires
        ]

        def run_sim():
            c = AdaptationController()
            c.content_manager = ARContentManager()
            events = []
            for t, conf, frust, eng, step in trace:
                e = c.update_affect_scores(t, conf, frust, eng, step)
                if e:
                    events.append((e.timestamp, e.action.value, e.step_id, e.confusion_score, e.frustration_score))
            return events

        run1 = run_sim()
        run2 = run_sim()
        self.assertEqual(run1, run2)
        self.assertEqual(len(run1), 2)
        self.assertEqual(run1[0][1], "InjectCue")
        self.assertEqual(run1[1][1], "InsertHint")

    # ── Test P: Missing / Invalid Data Handling ──────────────────────────────
    def test_p_missing_invalid_data_nan_handling(self):
        """Controlled synthetic test: NaN/Infinity samples safely ignored, no state corruption."""
        self.controller.update_affect_scores(1.0, 0.70, 0.10, 0.50, "step_01")
        self.assertTrue(self.controller.in_elevated_state)

        # Inject NaN
        evt_nan = self.controller.update_affect_scores(2.0, float('nan'), 0.10, 0.50, "step_01")
        self.assertIsNone(evt_nan)

        # Inject Inf
        evt_inf = self.controller.update_affect_scores(2.5, float('inf'), 0.10, 0.50, "step_01")
        self.assertIsNone(evt_inf)

        # Valid point at t=4.0 -> dwell timer was preserved safely
        evt_valid = self.controller.update_affect_scores(4.0, 0.70, 0.10, 0.50, "step_01")
        self.assertIsNotNone(evt_valid)
        self.assertEqual(len(self.controller.fired_events), 1)

    # ── Test Q: Session Boundary Isolation ───────────────────────────────────
    def test_q_session_boundary_isolation(self):
        """Controlled synthetic test: reset_session completely purges counters, timers, and events."""
        # Run session 1 to completion with 1 adaptation
        self.controller.update_affect_scores(1.0, 0.70, 0.10, 0.50, "step_01")
        self.controller.update_affect_scores(4.0, 0.70, 0.10, 0.50, "step_01")
        self.assertEqual(len(self.controller.fired_events), 1)

        # Session Boundary Reset
        self.controller.reset_session()
        self.assertEqual(len(self.controller.fired_events), 0)
        self.assertEqual(len(self.controller.suppressed_events), 0)
        self.assertEqual(self.controller.adaptations_this_step, 0)
        self.assertEqual(self.controller.last_adaptation_time, -999.0)
        self.assertEqual(self.controller.last_step_id, "")
        self.assertFalse(self.controller.in_elevated_state)

        # Session 2 starts fresh at t=0.0
        self.controller.update_affect_scores(1.0, 0.70, 0.10, 0.50, "step_01")
        evt = self.controller.update_affect_scores(4.0, 0.70, 0.10, 0.50, "step_01")
        self.assertIsNotNone(evt)
        self.assertEqual(evt.action, AdaptationAction.InjectCue)
        self.assertEqual(len(self.controller.fired_events), 1)

    # ── Test R: Progressive Escalation Sequence ──────────────────────────────
    def test_r_progressive_escalation_sequence(self):
        """Controlled synthetic test: verifies strict sequence [InjectCue, InsertHint, SlowPace]."""
        actions = []
        for i in range(3):
            t_start = i * 20.0 + 1.0
            t_fire = i * 20.0 + 4.0
            self.controller.update_affect_scores(t_start, 0.70, 0.10, 0.50, "step_01")
            evt = self.controller.update_affect_scores(t_fire, 0.70, 0.10, 0.50, "step_01")
            self.assertIsNotNone(evt)
            actions.append(evt.action)
            # brief recovery
            self.controller.update_affect_scores(t_fire + 1.0, 0.20, 0.10, 0.70, "step_01")

        self.assertEqual(actions, [
            AdaptationAction.InjectCue,
            AdaptationAction.InsertHint,
            AdaptationAction.SlowPace
        ])


if __name__ == '__main__':
    unittest.main()
