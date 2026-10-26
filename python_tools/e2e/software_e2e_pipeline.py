"""
A3R Phase 11 — Software End-to-End Simulation Pipeline.
Validates the complete closed-loop software architecture across all 8 steps of the circuit assembly task:
  Step Progression -> Affect Scoring -> Precursor Prediction -> Adaptation FSM -> AR Mutation -> Mock ESP32 HAL -> UI State -> Session Logging.

Preserves all frozen Phase 1-8 invariants:
  - Affect Equations:
      Confusion = 0.40*brow + 0.30*(1-ear) + 0.30*mouth
      Frustration = 0.35*brow + 0.25*(1-ear) + 0.40*mouth
      Engagement = 0.50*ear + 0.30*(1-brow) + 0.20*brow_height
  - Runtime Thresholds:
      Confusion: 0.60, Frustration: 0.65
      Dwell: 3.0s, Cooldown: 15.0s, Max adaptations/step: 3
  - Prediction:
      Rolling window K=5, min 3 samples, dt >= 0.4s, persistence 2, cooldown 5.0s, recovery floor < 0.30
  - Hardware:
      Mock ESP32 bridge (Led1 = Pin 13, Led2 = Pin 12), StepSync, BlinkLED at Step 8, ResetCircuit at completion.
"""

import sys
from pathlib import Path
_PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

import json
import os
import time
from typing import Dict, List, Optional, Tuple
from python_tools.hardware.mock_esp32 import (
    MockESP32Bridge, HardwareCommand, HardwareCommandType,
    LedIdentifier, LedState, HardwareConnectionState, AckStatus
)

TASK_JSON_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.dirname(__file__))),
    'unity_project', 'Assets', 'Resources', 'led_blink_task.json'
)

# ── Frozen Formulas & Invariants ─────────────────────────────────────────────
def compute_affect_scores(brow: float, ear: float, mouth: float, brow_height: float = 0.5) -> Tuple[float, float, float]:
    brow = max(0.0, min(1.0, brow))
    ear = max(0.0, min(1.0, ear))
    mouth = max(0.0, min(1.0, mouth))
    brow_height = max(0.0, min(1.0, brow_height))

    confusion = 0.40 * brow + 0.30 * (1.0 - ear) + 0.30 * mouth
    frustration = 0.35 * brow + 0.25 * (1.0 - ear) + 0.40 * mouth
    engagement = 0.50 * ear + 0.30 * (1.0 - brow) + 0.20 * brow_height

    return (
        max(0.0, min(1.0, confusion)),
        max(0.0, min(1.0, frustration)),
        max(0.0, min(1.0, engagement))
    )

CONFUSION_THRESHOLD = 0.60
FRUSTRATION_THRESHOLD = 0.65
DWELL_TIME_SECONDS = 3.0
COOLDOWN_SECONDS = 15.0
MAX_ADAPTATIONS_PER_STEP = 3

CONFUSION_SEQUENCE = ['InjectCue', 'InsertHint', 'SlowPace', 'SimplifyStep']
FRUSTRATION_SEQUENCE = ['SlowPace', 'SimplifyStep']

class E2EPredictionEngine:
    def __init__(self):
        self.history: List[Dict[str, float]] = []
        self.capacity = 5
        self.min_samples = 3
        self.cooldown = 5.0
        self.last_prediction_time = -999.0
        self.pending_prediction: Optional[Dict] = None
        self.state = 'IDLE'

    def update(self, t: float, conf: float, frust: float, eng: float, brow: float, ear: float, mouth: float) -> Optional[Dict]:
        self.history.append({
            'time': t, 'conf': conf, 'frust': frust, 'eng': eng,
            'brow': brow, 'ear': ear, 'mouth': mouth
        })
        if len(self.history) > self.capacity:
            self.history.pop(0)

        if len(self.history) < self.min_samples:
            return None

        dt_window = self.history[-1]['time'] - self.history[0]['time']
        if dt_window < 0.4:
            return None

        # Linear regression slope
        def get_slope(key):
            n = len(self.history)
            sum_t = sum(h['time'] for h in self.history)
            sum_v = sum(h[key] for h in self.history)
            sum_tv = sum(h['time'] * h[key] for h in self.history)
            sum_t2 = sum(h['time'] ** 2 for h in self.history)
            denom = n * sum_t2 - sum_t ** 2
            return 0.0 if abs(denom) < 1e-9 else (n * sum_tv - sum_t * sum_v) / denom

        slope_c = get_slope('conf')
        slope_f = get_slope('frust')
        slope_m = get_slope('mouth')

        cur_c = self.history[-1]['conf']
        cur_f = self.history[-1]['frust']

        # Recovery floor
        if cur_c < 0.30 and cur_f < 0.30:
            if self.state in ('PRECURSOR', 'PREDICTED'):
                self.state = 'IDLE'
                self.pending_prediction = None

        if (t - self.last_prediction_time) < self.cooldown:
            return None

        # Confusion Precursor: slope >= 0.05, 0.35 <= conf < 0.60
        if slope_c >= 0.05 and 0.35 <= cur_c < 0.60:
            self.last_prediction_time = t
            self.state = 'PREDICTED'
            self.pending_prediction = {
                'behavior': 'ConfusionStruggle',
                'timestamp': t,
                'evidence': cur_c,
                'slope': slope_c
            }
            return self.pending_prediction

        # Frustration Precursor: slope >= 0.05, mouth_slope >= 0.08, 0.35 <= frust < 0.65
        if slope_f >= 0.05 and slope_m >= 0.08 and 0.35 <= cur_f < 0.65:
            self.last_prediction_time = t
            self.state = 'PREDICTED'
            self.pending_prediction = {
                'behavior': 'FrustrationDisengage',
                'timestamp': t,
                'evidence': cur_f,
                'slope': slope_f
            }
            return self.pending_prediction

        return None


class E2EAdaptationFSM:
    def __init__(self):
        self.dwell_timer = 0.0
        self.elevated_start_time = 0.0
        self.in_elevated_state = False
        self.fsm_fired = False
        self.last_adaptation_time = -999.0
        self.adaptations_this_step = 0
        self.last_step_id = ''

    def update(self, t: float, conf: float, frust: float, step_id: str) -> Optional[Tuple[str, str]]:
        if step_id != self.last_step_id:
            self.in_elevated_state = False
            self.dwell_timer = 0.0
            self.fsm_fired = False
            self.adaptations_this_step = 0
            self.last_step_id = step_id

        above_threshold = (conf > CONFUSION_THRESHOLD) or (frust > FRUSTRATION_THRESHOLD)

        if above_threshold:
            if not self.in_elevated_state:
                self.in_elevated_state = True
                self.elevated_start_time = t
                self.dwell_timer = 0.0
                self.fsm_fired = False
            else:
                self.dwell_timer = t - self.elevated_start_time

            if not self.fsm_fired and self.dwell_timer >= DWELL_TIME_SECONDS:
                if (t - self.last_adaptation_time) < COOLDOWN_SECONDS:
                    return None
                if self.adaptations_this_step >= MAX_ADAPTATIONS_PER_STEP:
                    return None

                behavior = 'FrustrationDisengage' if (frust > conf and frust > FRUSTRATION_THRESHOLD) else 'ConfusionStruggle'
                if behavior == 'FrustrationDisengage':
                    action_idx = min(self.adaptations_this_step, len(FRUSTRATION_SEQUENCE) - 1)
                    action = FRUSTRATION_SEQUENCE[action_idx]
                else:
                    action_idx = min(self.adaptations_this_step, len(CONFUSION_SEQUENCE) - 1)
                    action = CONFUSION_SEQUENCE[action_idx]

                self.fsm_fired = True
                self.last_adaptation_time = t
                self.adaptations_this_step += 1
                return (action, behavior)
        else:
            self.in_elevated_state = False
            self.elevated_start_time = 0.0
            self.dwell_timer = 0.0
            self.fsm_fired = False

        return None


class E2EClosedLoopSystem:
    def __init__(self, task_path: str = TASK_JSON_PATH):
        with open(task_path, 'r', encoding='utf-8') as f:
            self.task_data = json.load(f)
        self.steps = self.task_data['steps']
        self.current_step_index = 0
        self.session_start_time = 0.0

        # Components
        self.prediction_engine = E2EPredictionEngine()
        self.adaptation_fsm = E2EAdaptationFSM()
        self.hardware = MockESP32Bridge(device_address='ESP32_MOCK_DEV_01')

        # UI & AR Mutation state
        self.research_mode = False
        self.active_hint = ''
        self.active_cue_target = ''
        self.detail_expanded = False
        self.simplified_text = ''
        self.adaptation_toast = ''
        self.task_complete = False

        # Session Telemetry
        self.telemetry_log: List[Dict] = []

    def start_session(self, t: float = 0.0):
        self.session_start_time = t
        self.hardware.connect()
        self.sync_step_to_hardware()

    def sync_step_to_hardware(self):
        step = self.steps[self.current_step_index]
        self.hardware.send_command(HardwareCommand(
            type=HardwareCommandType.StepSync,
            payload=step['id']
        ))
        if step['id'] == 'verify_blink':
            self.hardware.blink_led(LedIdentifier.Led1, 1000)

    def process_affect(self, t: float, brow: float, ear: float, mouth: float, brow_height: float = 0.5) -> Dict:
        conf, frust, eng = compute_affect_scores(brow, ear, mouth, brow_height)
        step = self.steps[self.current_step_index]

        # 1. Prediction update
        pred_event = self.prediction_engine.update(t, conf, frust, eng, brow, ear, mouth)

        # 2. Adaptation FSM update
        adaptation_result = self.adaptation_fsm.update(t, conf, frust, step['id'])

        if adaptation_result:
            action, behavior = adaptation_result
            self.apply_adaptation(action, behavior, step)

        # Telemetry record
        record = {
            't': t,
            'step_index': self.current_step_index,
            'step_id': step['id'],
            'confusion': conf,
            'frustration': frust,
            'engagement': eng,
            'prediction_state': self.prediction_engine.state,
            'pending_prediction': self.prediction_engine.pending_prediction is not None,
            'adaptation_action': adaptation_result[0] if adaptation_result else None,
            'hw_state': self.hardware.current_state.name,
            'led1_state': self.hardware.led1_state.name,
            'led2_state': self.hardware.led2_state.name
        }
        self.telemetry_log.append(record)
        return record

    def apply_adaptation(self, action: str, behavior: str, step: Dict):
        hints = step.get('adaptation_hints', {})
        self.adaptation_toast = f'Adaptation: {action}'

        if action == 'InjectCue':
            self.active_cue_target = step.get('ar_arrow_target', 'target')
        elif action == 'InsertHint':
            self.active_hint = hints.get('InsertHint', '')
        elif action == 'SlowPace':
            self.detail_expanded = True
            # Pulse Led2 as status indicator during pacing adjustment
            self.hardware.set_led(LedIdentifier.Led2, LedState.On)
        elif action == 'SimplifyStep':
            self.simplified_text = hints.get('SimplifyStep', '')

    def next_step(self):
        if self.current_step_index >= len(self.steps) - 1:
            self.task_complete = True
            self.hardware.send_command(HardwareCommand(type=HardwareCommandType.ResetCircuit))
            return
        self.current_step_index += 1
        self.active_hint = ''
        self.active_cue_target = ''
        self.detail_expanded = False
        self.simplified_text = ''
        self.adaptation_toast = ''
        self.sync_step_to_hardware()
