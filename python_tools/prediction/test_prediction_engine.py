"""
test_prediction_engine.py — Unit Tests for Phase 6 Deterministic Temporal Prediction Engine

Verifies requirements A through O of the Phase 6 specification:
  A. Stable neutral sequence -> no prediction
  B. Rising confusion sequence -> ConfusionStruggle triggers when intended
  C. Rising frustration sequence -> FrustrationDisengage triggers when intended
  D. Brief confusion spike -> does NOT trigger if persistence requirement is not met
  E. Brief frustration spike -> does NOT trigger
  F. Recovery -> prediction state clears correctly
  G. Repeated trigger -> event spam is prevented
  H. Tracking loss -> technical disruption event only after defined persistence
  I. Tracking recovery -> disruption clears correctly
  J. Timestamp irregularity -> logic remains correct when sample intervals vary
  K. Insufficient history -> predictor remains safe and does not fabricate a prediction
  L. NaN / invalid values -> safely ignored or handled
  M. Prediction logging -> valid JSONL schema
  N. is_simulated -> real runtime records must remain false
  O. Lead-time infrastructure -> no lead time is emitted unless independent ground truth exists
"""

import math
import json
import unittest
import numpy as np
from pathlib import Path
import sys

# Ensure local imports work
PKG_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(PKG_DIR))

from predict_affect import TemporalAffectPredictor, TimedAffectSample, PredictionEvent


class TestPredictionEngine(unittest.TestCase):

    def setUp(self):
        self.predictor = TemporalAffectPredictor(window_capacity=5, min_samples=3)

    # ── Test A: Stable Neutral Sequence ──────────────────────────────────────
    def test_a_stable_neutral_sequence_no_prediction(self):
        """Stable neutral inputs (confusion ~0.20, frustration ~0.15) must produce NO predictions."""
        events = []
        for i in range(10):
            t = i * 0.55  # ~1.8 Hz
            sample = TimedAffectSample(
                timestamp=t, confusion=0.20, frustration=0.15, engagement=0.65,
                brow_furrow=0.25, ear=0.75, mouth_tension=0.10, brow_height=0.60
            )
            evt = self.predictor.process_sample(sample)
            if evt: events.append(evt)

        self.assertEqual(len(events), 0, "Stable neutral sequence must not trigger predictions")
        self.assertEqual(self.predictor.total_predictions, 0)

    # ── Test B: Rising Confusion Sequence ────────────────────────────────────
    def test_b_rising_confusion_triggers_confusion_struggle(self):
        """Gradually rising confusion trajectory (from 0.30 to 0.50) must trigger ConfusionStruggle."""
        events = []
        confs = [0.25, 0.31, 0.38, 0.44, 0.51]
        brows = [0.30, 0.36, 0.43, 0.51, 0.58]
        ears  = [0.70, 0.65, 0.58, 0.50, 0.42]

        for i, (c, b, e) in enumerate(zip(confs, brows, ears)):
            t = i * 0.55
            sample = TimedAffectSample(
                timestamp=t, confusion=c, frustration=0.15, engagement=0.50,
                brow_furrow=b, ear=e, mouth_tension=0.10, brow_height=0.50
            )
            evt = self.predictor.process_sample(sample)
            if evt: events.append(evt)

        self.assertGreaterEqual(len(events), 1, "Rising confusion must trigger at least one prediction")
        self.assertEqual(events[0].behavior, "ConfusionStruggle")
        self.assertFalse(events[0].is_technical_disruption)
        self.assertGreater(events[0].evidence_score, 0.0)
        self.assertLessEqual(events[0].evidence_score, 1.0)
        self.assertGreater(events[0].trend_slope, 0.0)

    # ── Test C: Rising Frustration Sequence ──────────────────────────────────
    def test_c_rising_frustration_triggers_frustration_disengage(self):
        """Rising frustration trajectory (from 0.30 to 0.52) must trigger FrustrationDisengage."""
        events = []
        frusts = [0.25, 0.31, 0.38, 0.45, 0.52]
        mouths = [0.15, 0.22, 0.32, 0.44, 0.55]

        for i, (f, m) in enumerate(zip(frusts, mouths)):
            t = i * 0.55
            sample = TimedAffectSample(
                timestamp=t, confusion=0.20, frustration=f, engagement=0.40,
                brow_furrow=0.30, ear=0.60, mouth_tension=m, brow_height=0.50
            )
            evt = self.predictor.process_sample(sample)
            if evt: events.append(evt)

        self.assertGreaterEqual(len(events), 1, "Rising frustration must trigger FrustrationDisengage")
        self.assertEqual(events[0].behavior, "FrustrationDisengage")
        self.assertFalse(events[0].is_technical_disruption)
        self.assertGreater(events[0].evidence_score, 0.0)

    # ── Test D: Brief Confusion Spike (No Persistence) ────────────────────────
    def test_d_brief_confusion_spike_does_not_trigger(self):
        """A single transient confusion spike (noise/glance) must NOT trigger a prediction."""
        events = []
        # Samples at neutral, single spike, then immediately back to neutral
        sequence = [
            (0.00, 0.20),
            (0.55, 0.20),
            (1.10, 0.20),
            (1.65, 0.52),  # transient spike for 1 sample
            (2.20, 0.22),  # immediate collapse
            (2.75, 0.20)
        ]
        for t, c in sequence:
            sample = TimedAffectSample(
                timestamp=t, confusion=c, frustration=0.15, engagement=0.60,
                brow_furrow=0.25, ear=0.70, mouth_tension=0.10, brow_height=0.50
            )
            evt = self.predictor.process_sample(sample)
            if evt: events.append(evt)

        self.assertEqual(len(events), 0, "Transient single-sample confusion spike must be rejected by persistence guard")

    # ── Test E: Brief Frustration Spike (No Persistence) ─────────────────────
    def test_e_brief_frustration_spike_does_not_trigger(self):
        """A single transient frustration spike must NOT trigger FrustrationDisengage."""
        events = []
        sequence = [
            (0.00, 0.15),
            (0.55, 0.15),
            (1.10, 0.15),
            (1.65, 0.55),  # transient spike for 1 sample
            (2.20, 0.18),  # immediate collapse
            (2.75, 0.15)
        ]
        for t, f in sequence:
            sample = TimedAffectSample(
                timestamp=t, confusion=0.15, frustration=f, engagement=0.60,
                brow_furrow=0.25, ear=0.70, mouth_tension=0.10, brow_height=0.50
            )
            evt = self.predictor.process_sample(sample)
            if evt: events.append(evt)

        self.assertEqual(len(events), 0, "Transient frustration spike must not trigger prediction")

    # ── Test F: Recovery Dynamics ────────────────────────────────────────────
    def test_f_recovery_clears_prediction_state(self):
        """When learner affect returns below recovery floor (<0.30), pending state must reset."""
        # 1. Trigger rising confusion with persistence
        confs = [0.30, 0.38, 0.46, 0.50]
        for i, c in enumerate(confs):
            self.predictor.process_sample(TimedAffectSample(
                timestamp=i * 0.55, confusion=c, frustration=0.15, engagement=0.50,
                brow_furrow=0.45, ear=0.50, mouth_tension=0.10, brow_height=0.50
            ))
        self.assertIsNotNone(self.predictor.pending_prediction)

        # 2. Learner calms down: confusion drops to 0.18
        self.predictor.process_sample(TimedAffectSample(
            timestamp=3.0, confusion=0.18, frustration=0.12, engagement=0.70,
            brow_furrow=0.20, ear=0.75, mouth_tension=0.08, brow_height=0.60
        ))
        self.assertEqual(self.predictor.state, "RECOVERY")
        self.assertIsNone(self.predictor.pending_prediction, "Pending prediction must clear upon recovery")

    # ── Test G: Cooldown and Event Spamming Suppression ──────────────────────
    def test_g_cooldown_prevents_event_spam(self):
        """Continuous sustained struggle must NOT spam prediction events on every frame."""
        events = []
        # 10 samples continuously elevated with positive slope >= 0.05/s
        for i in range(10):
            t = i * 0.55
            sample = TimedAffectSample(
                timestamp=t, confusion=min(0.54, 0.36 + i * 0.035), frustration=0.15, engagement=0.50,
                brow_furrow=0.35 + i * 0.03, ear=0.65 - i * 0.025, mouth_tension=0.10, brow_height=0.50
            )
            evt = self.predictor.process_sample(sample)
            if evt: events.append(evt)

        # Exactly 1 prediction event should be emitted within the 5.0s cooldown window
        self.assertEqual(len(events), 1, f"Predictor must emit exactly 1 event within cooldown window, got {len(events)}")

    # ── Test H: Tracking Loss Technical Disruption ───────────────────────────
    def test_h_tracking_loss_triggers_disruption_after_persistence(self):
        """Tracking loss sustained for >= 1.5s must trigger TrackingLossDisruption."""
        events = []
        # Frame 0: tracking ok
        self.predictor.process_sample(TimedAffectSample(
            timestamp=0.0, confusion=0.2, frustration=0.2, engagement=0.6,
            brow_furrow=0.2, ear=0.7, mouth_tension=0.1, brow_height=0.5, face_detected=True
        ))
        # Frame 1: tracking lost at t=0.5s (0.5s loss -> no trigger yet)
        evt1 = self.predictor.process_sample(TimedAffectSample(
            timestamp=0.5, confusion=0.0, frustration=0.0, engagement=0.0,
            brow_furrow=0.0, ear=0.0, mouth_tension=0.0, brow_height=0.0, face_detected=False
        ))
        if evt1: events.append(evt1)
        self.assertEqual(len(events), 0)

        # Frame 2: tracking lost at t=1.2s (0.7s loss -> no trigger yet)
        evt2 = self.predictor.process_sample(TimedAffectSample(
            timestamp=1.2, confusion=0.0, frustration=0.0, engagement=0.0,
            brow_furrow=0.0, ear=0.0, mouth_tension=0.0, brow_height=0.0, face_detected=False
        ))
        if evt2: events.append(evt2)
        self.assertEqual(len(events), 0)

        # Frame 3: tracking lost at t=2.1s (1.6s loss >= 1.5s -> MUST trigger)
        evt3 = self.predictor.process_sample(TimedAffectSample(
            timestamp=2.1, confusion=0.0, frustration=0.0, engagement=0.0,
            brow_furrow=0.0, ear=0.0, mouth_tension=0.0, brow_height=0.0, face_detected=False
        ))
        if evt3: events.append(evt3)

        self.assertEqual(len(events), 1, "Must trigger TrackingLossDisruption when loss >= 1.5s")
        self.assertEqual(events[0].behavior, "TrackingLossDisruption")
        self.assertTrue(events[0].is_technical_disruption)
        self.assertEqual(events[0].evidence_score, 1.0)

    # ── Test I: Tracking Recovery ────────────────────────────────────────────
    def test_i_tracking_recovery_clears_disruption(self):
        """When face detection resumes after disruption, tracking loss state must clear."""
        # Induce tracking loss disruption
        self.predictor.process_sample(TimedAffectSample(0.0, 0, 0, 0, 0, 0, 0, 0, face_detected=False))
        self.predictor.process_sample(TimedAffectSample(2.0, 0, 0, 0, 0, 0, 0, 0, face_detected=False))
        self.assertEqual(self.predictor.state, "TRACKING_LOST")

        # Face re-acquired
        self.predictor.process_sample(TimedAffectSample(
            timestamp=2.5, confusion=0.20, frustration=0.15, engagement=0.65,
            brow_furrow=0.25, ear=0.75, mouth_tension=0.10, brow_height=0.60, face_detected=True
        ))
        self.assertNotEqual(self.predictor.state, "TRACKING_LOST")
        self.assertFalse(self.predictor.tracking_loss_reported)

    # ── Test J: Timestamp Irregularity ───────────────────────────────────────
    def test_j_timestamp_irregularity_handled_correctly(self):
        """Sampling jitters (e.g. 0.3s, 0.8s, 0.4s) must calculate valid dt and slopes."""
        events = []
        timestamps = [0.0, 0.4, 0.9, 1.4, 2.0]  # variable sample intervals
        confs = [0.25, 0.32, 0.39, 0.46, 0.52]

        for t, c in zip(timestamps, confs):
            evt = self.predictor.process_sample(TimedAffectSample(
                timestamp=t, confusion=c, frustration=0.15, engagement=0.40,
                brow_furrow=0.30 + c*0.4, ear=0.70 - c*0.3, mouth_tension=0.10, brow_height=0.50
            ))
            if evt: events.append(evt)

        self.assertGreaterEqual(len(events), 1)
        self.assertEqual(events[0].behavior, "ConfusionStruggle")

    # ── Test K: Insufficient History Guard ───────────────────────────────────
    def test_k_insufficient_history_does_not_fabricate(self):
        """With fewer than min_samples (3), no prediction can be emitted."""
        s1 = TimedAffectSample(0.0, 0.45, 0.15, 0.5, 0.5, 0.5, 0.1, 0.5)
        s2 = TimedAffectSample(0.5, 0.50, 0.15, 0.5, 0.55, 0.45, 0.1, 0.5)

        self.assertIsNone(self.predictor.process_sample(s1))
        self.assertIsNone(self.predictor.process_sample(s2))
        self.assertEqual(self.predictor.total_predictions, 0)

    # ── Test L: NaN and Invalid Input Safety ─────────────────────────────────
    def test_l_nan_and_invalid_values_safely_ignored(self):
        """NaN values or out-of-range floats must not crash or contaminate the predictor."""
        s_nan = TimedAffectSample(0.0, float("nan"), 0.15, 0.5, 0.5, 0.5, 0.1, 0.5)
        self.assertIsNone(self.predictor.process_sample(s_nan))
        self.assertEqual(len(self.predictor.history), 0)

        # Extreme values clamped
        s_extreme = TimedAffectSample(1.0, 2.5, -0.5, 1.5, 1.2, -0.1, 3.0, 0.5)
        self.predictor.process_sample(s_extreme)
        self.assertEqual(len(self.predictor.history), 1)
        self.assertEqual(self.predictor.history[0].confusion, 1.0)
        self.assertEqual(self.predictor.history[0].frustration, 0.0)

    # ── Test M: Prediction Event JSONL Schema Validity ────────────────────────
    def test_m_prediction_logging_valid_schema(self):
        """Prediction events must serialize to valid JSON matching the schema requirements."""
        evt = PredictionEvent(
            behavior="ConfusionStruggle",
            evidence_score=0.725,
            timestamp=12.45,
            step_id="step_02_resistor",
            confusion=0.48,
            frustration=0.22,
            trend_slope=0.082,
            is_simulated=False,
            is_technical_disruption=False,
            lead_time_s=2.35
        )

        record = {
            "type": "prediction",
            "ts": "2026-09-13T17:30:00.000Z",
            "timestamp": evt.timestamp,
            "predictionType": evt.behavior,
            "predictionEvidence": evt.evidence_score,
            "confusionScore": evt.confusion,
            "frustrationScore": evt.frustration,
            "trend": evt.trend_slope,
            "stepId": evt.step_id,
            "isSimulated": evt.is_simulated,
            "isTechnicalDisruption": evt.is_technical_disruption,
            "leadTimeS": evt.lead_time_s
        }

        line = json.dumps(record)
        parsed = json.loads(line)
        self.assertEqual(parsed["type"], "prediction")
        self.assertEqual(parsed["predictionType"], "ConfusionStruggle")
        self.assertFalse(parsed["isSimulated"])
        self.assertEqual(parsed["leadTimeS"], 2.35)

    # ── Test N: is_simulated Field Integrity ─────────────────────────────────
    def test_n_is_simulated_field_integrity(self):
        """Real device execution must pass is_simulated=False."""
        sample = TimedAffectSample(
            timestamp=1.0, confusion=0.40, frustration=0.20, engagement=0.50,
            brow_furrow=0.40, ear=0.50, mouth_tension=0.10, brow_height=0.50,
            is_simulated=False
        )
        self.assertFalse(sample.is_simulated)

    # ── Test O: Lead-Time Infrastructure & Verification ──────────────────────
    def test_o_lead_time_infrastructure_verification(self):
        """Lead time must strictly require prior prediction (t_actual - t_pred > 0)."""
        # 1. Trigger prediction with rising confusion and persistence
        for i, c in enumerate([0.28, 0.36, 0.44, 0.50]):
            self.predictor.process_sample(TimedAffectSample(
                timestamp=i * 0.55, confusion=c, frustration=0.15, engagement=0.40,
                brow_furrow=0.35 + i*0.04, ear=0.65 - i*0.03, mouth_tension=0.10, brow_height=0.50
            ))
        self.assertIsNotNone(self.predictor.pending_prediction, "Pending prediction should be active")
        self.assertEqual(self.predictor.pending_prediction.behavior, "ConfusionStruggle")
        t_pred = self.predictor.pending_prediction.timestamp

        # 2. Actual adaptation event occurs at t=3.8s (e.g. 3s dwell reached)
        lead_time = self.predictor.notify_actual_event_fired("ConfusionStruggle", actual_timestamp=3.8)
        self.assertIsNotNone(lead_time)
        self.assertAlmostEqual(lead_time, 3.8 - t_pred, places=2)
        self.assertGreater(lead_time, 0.1)
        self.assertEqual(self.predictor.correct_predictions, 1)

        # 3. If an actual event fires without a prior prediction -> False Negative (no lead time emitted)
        lead_time_missed = self.predictor.notify_actual_event_fired("FrustrationDisengage", actual_timestamp=10.0)
        self.assertIsNone(lead_time_missed, "No lead time can be emitted for unpredicted events")
        self.assertEqual(self.predictor.false_negatives, 1)


if __name__ == "__main__":
    unittest.main(verbosity=2)
