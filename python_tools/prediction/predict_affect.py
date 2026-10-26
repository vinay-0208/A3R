"""
predict_affect.py — Deterministic Rule-Based Temporal Affect Predictor for A³R (Phase 6)

Part of the A³R prediction tooling (python_tools/prediction/).
Implements the EXACT same deterministic precursor dynamics and temporal FSM logic
as AffectPredictor.cs in Unity.

NO machine learning. NO neural networks. NO scikit-learn. NO PyTorch/TensorFlow.
Purely deterministic temporal feature extraction, rolling-window slope estimation,
and interpretable behavioral prediction.

Taxonomy of States:
  Behavioral Predictions:
    1. ConfusionStruggle      — Precursor trajectory indicating impending cognitive struggle
    2. FrustrationDisengage   — Precursor trajectory indicating disengagement risk
    3. PolarityHesitation     — Competing affect signals or micro-squint during polarity steps
  Technical Disruption Detections (NOT affect predictions):
    4. TrackingLossDisruption — Front camera face tracking loss sustained for >= 1.5s
    5. HardwareDisconnection  — ESP32 connection loss abstraction (pending Phase 9)
"""

import math
from collections import deque
from dataclasses import dataclass
from typing import List, Optional, Tuple, Dict, Any
import numpy as np


@dataclass
class TimedAffectSample:
    timestamp: float
    confusion: float
    frustration: float
    engagement: float
    brow_furrow: float
    ear: float
    mouth_tension: float
    brow_height: float
    step_id: str = ""
    is_simulated: bool = False
    face_detected: bool = True


@dataclass
class PredictionEvent:
    behavior: str
    evidence_score: float
    timestamp: float
    step_id: str
    confusion: float
    frustration: float
    trend_slope: float
    is_simulated: bool
    is_technical_disruption: bool
    lead_time_s: Optional[float] = None
    ground_truth_timestamp: Optional[float] = None


class TemporalAffectPredictor:
    """
    Deterministic rule-based temporal predictor using bounded rolling windows.
    Matches AffectPredictor.cs in Unity.
    """

    # Gating & Precursor Thresholds
    CONFUSION_PRECURSOR_FLOOR = 0.35
    CONFUSION_PRECURSOR_CEILING = 0.55
    CONFUSION_SLOPE_THRESH = 0.05       # /sec
    BROW_VELOCITY_THRESH = 0.10         # /sec
    EAR_VELOCITY_THRESH = -0.05         # /sec

    FRUSTRATION_PRECURSOR_FLOOR = 0.35
    FRUSTRATION_PRECURSOR_CEILING = 0.60
    FRUSTRATION_SLOPE_THRESH = 0.05     # /sec
    MOUTH_VELOCITY_THRESH = 0.08        # /sec

    RECOVERY_FLOOR = 0.30
    TRACKING_LOSS_DURATION_THRESH = 1.5 # seconds
    PREDICTION_COOLDOWN_SECONDS = 5.0   # seconds
    MIN_PERSISTENCE_SAMPLES = 2         # requires at least 2 consecutive precursor evaluations

    def __init__(self, window_capacity: int = 5, min_samples: int = 3):
        self.window_capacity = window_capacity
        self.min_samples = min_samples
        self.history = deque(maxlen=window_capacity)

        # FSM States: "IDLE", "PRECURSOR", "PREDICTED", "RECOVERY", "TRACKING_LOST"
        self.state = "IDLE"
        self.last_prediction_time = -999.0
        self.last_predicted_behavior = ""
        self.pending_prediction: Optional[PredictionEvent] = None
        self.precursor_persistence_count = 0
        self.active_precursor_candidate: Optional[str] = None

        # Tracking Loss State
        self.tracking_loss_start_time: Optional[float] = None
        self.tracking_loss_reported = False

        # Metrics Tracking
        self.total_predictions = 0
        self.correct_predictions = 0
        self.false_positives = 0
        self.false_negatives = 0
        self.measured_lead_times: List[float] = []

    def reset(self):
        """Clears all history and state."""
        self.history.clear()
        self.state = "IDLE"
        self.last_prediction_time = -999.0
        self.last_predicted_behavior = ""
        self.pending_prediction = None
        self.precursor_persistence_count = 0
        self.active_precursor_candidate = None
        self.tracking_loss_start_time = None
        self.tracking_loss_reported = False

    def process_sample(self, sample: TimedAffectSample) -> Optional[PredictionEvent]:
        """
        Ingests a new affect sample and evaluates deterministic temporal precursor rules.
        Returns a PredictionEvent if a state transition triggers, or None.
        """
        # ── 1. Technical Disruption Check (Tracking Loss) ───────────────────
        if not sample.face_detected:
            if self.tracking_loss_start_time is None:
                self.tracking_loss_start_time = sample.timestamp
            elapsed_loss = sample.timestamp - self.tracking_loss_start_time

            if elapsed_loss >= self.TRACKING_LOSS_DURATION_THRESH and not self.tracking_loss_reported:
                self.tracking_loss_reported = True
                self.state = "TRACKING_LOST"
                evt = PredictionEvent(
                    behavior="TrackingLossDisruption",
                    evidence_score=1.0,
                    timestamp=sample.timestamp,
                    step_id=sample.step_id,
                    confusion=sample.confusion if not math.isnan(sample.confusion) else 0.0,
                    frustration=sample.frustration if not math.isnan(sample.frustration) else 0.0,
                    trend_slope=0.0,
                    is_simulated=sample.is_simulated,
                    is_technical_disruption=True
                )
                return evt
            return None
        else:
            # Tracking recovered
            if self.tracking_loss_start_time is not None:
                self.tracking_loss_start_time = None
                self.tracking_loss_reported = False
                if self.state == "TRACKING_LOST":
                    self.state = "IDLE"

        # ── 2. Sanitize & Enqueue Sample ────────────────────────────────────
        if any(math.isnan(v) for v in [sample.confusion, sample.frustration, sample.engagement, sample.brow_furrow, sample.ear]):
            return None

        # Clamp values to valid [0, 1] range
        s_clean = TimedAffectSample(
            timestamp=sample.timestamp,
            confusion=float(np.clip(sample.confusion, 0.0, 1.0)),
            frustration=float(np.clip(sample.frustration, 0.0, 1.0)),
            engagement=float(np.clip(sample.engagement, 0.0, 1.0)),
            brow_furrow=float(np.clip(sample.brow_furrow, 0.0, 1.0)),
            ear=float(np.clip(sample.ear, 0.0, 1.0)),
            mouth_tension=float(np.clip(sample.mouth_tension, 0.0, 1.0)),
            brow_height=float(np.clip(sample.brow_height, 0.0, 1.0)),
            step_id=sample.step_id,
            is_simulated=sample.is_simulated,
            face_detected=True
        )
        self.history.append(s_clean)

        # ── 3. Insufficient History Guard ───────────────────────────────────
        if len(self.history) < self.min_samples:
            return None

        dt_window = self.history[-1].timestamp - self.history[0].timestamp
        if dt_window < 0.4:
            return None

        # ── 4. Compute Temporal Dynamics over Window ─────────────────────────
        ts = np.array([s.timestamp for s in self.history])
        confs = np.array([s.confusion for s in self.history])
        frusts = np.array([s.frustration for s in self.history])
        engs = np.array([s.engagement for s in self.history])
        brows = np.array([s.brow_furrow for s in self.history])
        ears = np.array([s.ear for s in self.history])
        mouths = np.array([s.mouth_tension for s in self.history])

        # Slopes via linear regression (dY/dt)
        def _calc_slope(x, y):
            x_mean = np.mean(x)
            y_mean = np.mean(y)
            denom = np.sum((x - x_mean)**2)
            return float(np.sum((x - x_mean) * (y - y_mean)) / denom) if denom > 1e-6 else 0.0

        slope_conf = _calc_slope(ts, confs)
        slope_frust = _calc_slope(ts, frusts)
        slope_brow = _calc_slope(ts, brows)
        slope_ear = _calc_slope(ts, ears)
        slope_mouth = _calc_slope(ts, mouths)

        current_c = confs[-1]
        current_f = frusts[-1]
        current_e = engs[-1]

        # ── 5. Recovery Detection ───────────────────────────────────────────
        if current_c < self.RECOVERY_FLOOR and current_f < self.RECOVERY_FLOOR:
            if self.state in ["PRECURSOR", "PREDICTED"]:
                self.state = "RECOVERY"
                self.pending_prediction = None
                self.precursor_persistence_count = 0
                self.active_precursor_candidate = None
            return None

        # ── 6. Cooldown Guard ───────────────────────────────────────────────
        in_cooldown = (sample.timestamp - self.last_prediction_time) < self.PREDICTION_COOLDOWN_SECONDS

        # ── 7. Evaluate Precursor Conditions ────────────────────────────────
        is_led_step = (sample.step_id.lower() in ["insert_led", "step_03_led", "step_3"])
        std_conf = float(np.std(confs))

        candidate = None
        evidence = 0.0
        trend = 0.0

        # Condition A: ConfusionStruggle
        is_conf = (
            self.CONFUSION_PRECURSOR_FLOOR <= current_c <= self.CONFUSION_PRECURSOR_CEILING
            and (slope_conf >= self.CONFUSION_SLOPE_THRESH or (slope_brow >= self.BROW_VELOCITY_THRESH and slope_ear <= self.EAR_VELOCITY_THRESH))
        )

        # Condition B: FrustrationDisengage
        is_frust = (
            self.FRUSTRATION_PRECURSOR_FLOOR <= current_f <= self.FRUSTRATION_PRECURSOR_CEILING
            and (slope_frust >= self.FRUSTRATION_SLOPE_THRESH or slope_mouth >= self.MOUTH_VELOCITY_THRESH)
        )

        # Condition C: PolarityHesitation
        is_polarity = False
        if is_led_step and (0.30 <= current_c <= 0.55) and (slope_brow >= 0.08 and slope_ear <= -0.04):
            is_polarity = True
        elif (not is_conf and not is_frust and abs(current_c - current_e) <= 0.12 and current_c >= 0.35 and std_conf >= 0.05 and current_f < 0.35):
            is_polarity = True

        if is_polarity:
            candidate = "PolarityHesitation"
            evidence = float(np.clip(0.60 + 0.40 * (std_conf / 0.10), 0.0, 1.0))
            trend = slope_conf
        elif is_conf:
            candidate = "ConfusionStruggle"
            norm_c = (current_c - self.CONFUSION_PRECURSOR_FLOOR) / (self.CONFUSION_PRECURSOR_CEILING - self.CONFUSION_PRECURSOR_FLOOR)
            norm_slope = slope_conf / 0.20
            evidence = float(np.clip(0.50 * norm_c + 0.50 * norm_slope, 0.0, 1.0))
            trend = slope_conf
        elif is_frust:
            candidate = "FrustrationDisengage"
            norm_f = (current_f - self.FRUSTRATION_PRECURSOR_FLOOR) / (self.FRUSTRATION_PRECURSOR_CEILING - self.FRUSTRATION_PRECURSOR_FLOOR)
            norm_slope = slope_frust / 0.20
            evidence = float(np.clip(0.50 * norm_f + 0.50 * norm_slope, 0.0, 1.0))
            trend = slope_frust

        # ── 8. Persistence Evaluation (Guards Against Spikes) ────────────────
        if candidate is not None:
            if candidate == self.active_precursor_candidate:
                self.precursor_persistence_count += 1
            else:
                self.active_precursor_candidate = candidate
                self.precursor_persistence_count = 1

            self.state = "PRECURSOR"

            # Must satisfy persistence threshold (>= 2 samples) to transition to PREDICTED
            if self.precursor_persistence_count >= self.MIN_PERSISTENCE_SAMPLES:
                if not in_cooldown or (candidate != self.last_predicted_behavior):
                    self.state = "PREDICTED"
                    self.last_prediction_time = sample.timestamp
                    self.last_predicted_behavior = candidate
                    self.total_predictions += 1

                    evt = PredictionEvent(
                        behavior=candidate,
                        evidence_score=round(evidence, 4),
                        timestamp=sample.timestamp,
                        step_id=sample.step_id,
                        confusion=round(current_c, 4),
                        frustration=round(current_f, 4),
                        trend_slope=round(trend, 4),
                        is_simulated=sample.is_simulated,
                        is_technical_disruption=False
                    )
                    self.pending_prediction = evt
                    return evt
        else:
            self.precursor_persistence_count = 0
            self.active_precursor_candidate = None
            if self.state == "PRECURSOR":
                self.state = "IDLE"

        return None

    def notify_actual_event_fired(self, actual_behavior: str, actual_timestamp: float) -> Optional[float]:
        """
        Called when an independent ground-truth or detection event occurs.
        Computes lead time: lead_time = actual_timestamp - prediction_timestamp.
        Strictly returns lead_time > 0 only if a valid prior prediction exists.
        """
        if self.pending_prediction is not None:
            lead_time = actual_timestamp - self.pending_prediction.timestamp
            # Valid anticipation window: 0.1s <= lead_time <= 8.0s
            if 0.1 <= lead_time <= 8.0 and (self.pending_prediction.behavior == actual_behavior):
                self.correct_predictions += 1
                self.measured_lead_times.append(lead_time)
                self.pending_prediction.lead_time_s = round(lead_time, 3)
                self.pending_prediction.ground_truth_timestamp = actual_timestamp
                self.pending_prediction = None
                return lead_time
            else:
                self.false_positives += 1
                self.pending_prediction = None
                return None
        else:
            self.false_negatives += 1
            return None

    def invalidate_pending_prediction(self):
        """Called on step change or task reset."""
        if self.pending_prediction is not None:
            self.false_positives += 1
            self.pending_prediction = None
        self.state = "IDLE"
        self.precursor_persistence_count = 0
        self.active_precursor_candidate = None
