# -*- coding: utf-8 -*-
"""
A³R Phase 8: Adaptation Policy Module
Deterministic closed-loop adaptation policy and content manager mirror of C# implementation.

Invariants Preserved:
- Confusion threshold = 0.60
- Frustration threshold = 0.65
- Dwell time = 3.0 s
- Cooldown window = 15.0 s
- Maximum adaptations per step = 3
- Recovery floor = 0.30
- Action Sequence: [InjectCue, InsertHint, SlowPace, SimplifyStep]
"""

from enum import Enum
from dataclasses import dataclass, field
from typing import List, Dict, Optional, Any
import math
import uuid


class AdaptationAction(Enum):
    SimplifyStep = "SimplifyStep"
    InjectCue    = "InjectCue"
    SlowPace     = "SlowPace"
    InsertHint   = "InsertHint"


CONFUSION_SEQUENCE = [
    AdaptationAction.InjectCue,     # 1st: visual arrow cue
    AdaptationAction.InsertHint,    # 2nd: hint text panel
    AdaptationAction.SlowPace,      # 3rd: slow down pacing / show details
    AdaptationAction.SimplifyStep,  # 4th: full step breakdown (unreachable in 1 step due to max 3 ceiling)
]

FRUSTRATION_SEQUENCE = [
    AdaptationAction.SlowPace,      # 1st: slow down pacing / show details
    AdaptationAction.SimplifyStep,  # 2nd: full step breakdown
]

# Backward compatibility alias
ACTION_SEQUENCE = CONFUSION_SEQUENCE


@dataclass
class CanonicalEvent:
    session_id: str
    worker_id: str
    step_id: str
    step_index: int
    object_id: str
    pdi_target: str
    behavior: str = "NormalProgress"
    event_id: str = field(default_factory=lambda: uuid.uuid4().hex)
    is_prediction: bool = False
    lead_time_seconds: float = 0.0


@dataclass
class AdaptationEvent:
    action: AdaptationAction
    step_id: str
    confusion_score: float
    frustration_score: float
    timestamp: float
    canonical_context: Optional[CanonicalEvent] = None
    accepted: bool = True
    suppression_reason: str = ""
    triggering_behavior: str = "ConfusionStruggle"
    event_id: str = field(default_factory=lambda: uuid.uuid4().hex)


class AdaptationController:
    """
    Python mirror of Unity C# AdaptationController.cs with identical deterministic FSM semantics.
    """

    def __init__(
        self,
        confusion_threshold: float = 0.60,
        frustration_threshold: float = 0.65,
        dwell_time_seconds: float = 3.0,
        cooldown_seconds: float = 15.0,
        max_adaptations_per_step: int = 3,
        recovery_score_floor: float = 0.30
    ):
        self.confusion_threshold = confusion_threshold
        self.frustration_threshold = frustration_threshold
        self.dwell_time_seconds = dwell_time_seconds
        self.cooldown_seconds = cooldown_seconds
        self.max_adaptations_per_step = max_adaptations_per_step
        self.recovery_score_floor = recovery_score_floor

        # FSM State
        self.dwell_timer: float = 0.0
        self.elevated_start_time: float = 0.0
        self.in_elevated_state: bool = False
        self.fsm_fired: bool = False

        # Cooldown & Step State
        self.last_adaptation_time: float = -999.0
        self.adaptations_this_step: int = 0
        self.last_step_id: str = ""

        # Suppression Tracking
        self.suppressed_cooldown_count: int = 0
        self.suppressed_max_limit_count: int = 0

        # Event History
        self.fired_events: List[AdaptationEvent] = []
        self.suppressed_events: List[AdaptationEvent] = []

        # Content Manager Reference
        self.content_manager: Optional['ARContentManager'] = None

    def reset_session(self):
        """Resets all FSM, adaptation counters, and cooldown state at session boundaries."""
        self.reset_fsm_state()
        self.adaptations_this_step = 0
        self.last_step_id = ""
        self.last_adaptation_time = -999.0
        self.suppressed_cooldown_count = 0
        self.suppressed_max_limit_count = 0
        self.fired_events.clear()
        self.suppressed_events.clear()

    def reset_fsm_state(self):
        self.in_elevated_state = False
        self.elevated_start_time = 0.0
        self.dwell_timer = 0.0
        self.fsm_fired = False

    def update_affect_scores(
        self,
        current_time: float,
        confusion_score: float,
        frustration_score: float,
        engagement_score: float,
        current_step_id: str,
        is_tracking_valid: bool = True
    ) -> Optional[AdaptationEvent]:
        # Guard: missing/invalid values or invalid tracking
        if math.isnan(confusion_score) or math.isnan(frustration_score) or math.isinf(confusion_score) or math.isinf(frustration_score):
            return None

        if not is_tracking_valid:
            # During tracking loss, do not update emotional dwell timer
            return None

        # Step transition check
        if current_step_id != self.last_step_id:
            self.reset_fsm_state()
            self.adaptations_this_step = 0
            self.last_step_id = current_step_id
            if self.content_manager:
                self.content_manager.set_step_by_id(current_step_id)

        # Threshold check
        above_threshold = (confusion_score > self.confusion_threshold or
                           frustration_score > self.frustration_threshold)

        if above_threshold:
            if not self.in_elevated_state:
                self.in_elevated_state = True
                self.elevated_start_time = current_time
                self.dwell_timer = 0.0
                self.fsm_fired = False
            else:
                self.dwell_timer = current_time - self.elevated_start_time

            # Dwell time satisfied check
            if not self.fsm_fired and self.dwell_timer >= self.dwell_time_seconds:
                triggering_behavior = (
                    "FrustrationDisengage"
                    if (frustration_score > confusion_score and frustration_score > self.frustration_threshold)
                    else "ConfusionStruggle"
                )
                seq = FRUSTRATION_SEQUENCE if triggering_behavior == "FrustrationDisengage" else CONFUSION_SEQUENCE
                action = seq[min(self.adaptations_this_step, len(seq) - 1)]

                # Check cooldown
                if (current_time - self.last_adaptation_time) < self.cooldown_seconds:
                    self.suppressed_cooldown_count += 1
                    suppressed_evt = AdaptationEvent(
                        action=action,
                        step_id=current_step_id,
                        confusion_score=confusion_score,
                        frustration_score=frustration_score,
                        timestamp=current_time,
                        accepted=False,
                        suppression_reason="cooldown_active",
                        triggering_behavior=triggering_behavior
                    )
                    self.suppressed_events.append(suppressed_evt)
                    return None

                # Check max adaptations per step
                if self.adaptations_this_step >= self.max_adaptations_per_step:
                    self.suppressed_max_limit_count += 1
                    suppressed_evt = AdaptationEvent(
                        action=action,
                        step_id=current_step_id,
                        confusion_score=confusion_score,
                        frustration_score=frustration_score,
                        timestamp=current_time,
                        accepted=False,
                        suppression_reason="max_adaptations_per_step_reached",
                        triggering_behavior=triggering_behavior
                    )
                    self.suppressed_events.append(suppressed_evt)
                    return None

                # Fire Adaptation
                return self._fire_adaptation(confusion_score, frustration_score, current_step_id, current_time)
        else:
            # Dropped below threshold - recovery
            self.in_elevated_state = False
            self.elevated_start_time = 0.0
            self.dwell_timer = 0.0
            self.fsm_fired = False

        return None

    def _fire_adaptation(
        self,
        confusion_score: float,
        frustration_score: float,
        step_id: str,
        current_time: float
    ) -> AdaptationEvent:
        triggering_behavior = (
            "FrustrationDisengage"
            if (frustration_score > confusion_score and frustration_score > self.frustration_threshold)
            else "ConfusionStruggle"
        )

        seq = FRUSTRATION_SEQUENCE if triggering_behavior == "FrustrationDisengage" else CONFUSION_SEQUENCE
        action = seq[min(self.adaptations_this_step, len(seq) - 1)]

        canonical_ctx = self.content_manager.current_canonical_event if self.content_manager else None

        evt = AdaptationEvent(
            action=action,
            step_id=step_id,
            confusion_score=confusion_score,
            frustration_score=frustration_score,
            timestamp=current_time,
            canonical_context=canonical_ctx,
            accepted=True,
            suppression_reason="",
            triggering_behavior=triggering_behavior
        )

        self.fsm_fired = True
        self.last_adaptation_time = current_time
        self.adaptations_this_step += 1
        self.fired_events.append(evt)

        if self.content_manager:
            self.content_manager.apply_adaptation(action, triggering_behavior, current_time)

        return evt


class ARContentManager:
    """
    Python mirror of Unity C# ARContentManager.cs.
    """

    def __init__(self, steps: Optional[List[Dict[str, Any]]] = None, worker_id: str = "learner_01"):
        self.worker_id = worker_id
        self.steps = steps or [
            {"id": "step_01_workbench", "title": "Setup Workbench", "detail": "Place breadboard and components.", "highlight": "breadboard", "target": "workbench_marker"},
            {"id": "step_02_esp32", "title": "Place ESP32", "detail": "Insert ESP32 into center of breadboard.", "highlight": "esp32", "target": "breadboard_center"},
            {"id": "step_03_led", "title": "Insert LED", "detail": "Anode (long leg) to GPIO2, Cathode to GND.", "highlight": "led_red", "target": "breadboard_row_15"},
            {"id": "step_04_resistor", "title": "Insert Resistor", "detail": "Connect 220 Ohm resistor from cathode to GND rail.", "highlight": "resistor", "target": "breadboard_row_16"},
            {"id": "step_05_power", "title": "Power On", "detail": "Connect USB cable to power rail.", "highlight": "usb_cable", "target": "esp32_usb_port"}
        ]
        self.current_step_index: int = 0
        self.is_cue_active: bool = False
        self.is_hint_active: bool = False
        self.is_slow_pace_active: bool = False
        self.is_simplify_active: bool = False
        self.current_instruction_text: str = ""
        self.current_detail_text: str = ""
        self.current_hint_text: str = ""
        self.current_canonical_event: Optional[CanonicalEvent] = None
        self.predictor = None

        self._display_current_step()

    def _display_current_step(self):
        step = self.steps[self.current_step_index]
        self.current_instruction_text = step.get("instruction", step.get("title", ""))
        self.current_detail_text = step.get("detail", "")
        self.current_canonical_event = CanonicalEvent(
            session_id=uuid.uuid4().hex,
            worker_id=self.worker_id,
            step_id=step["id"],
            step_index=self.current_step_index,
            object_id=step.get("highlight", "breadboard"),
            pdi_target=step.get("target", "workbench_marker"),
            behavior="NormalProgress"
        )

    def set_step_by_id(self, step_id: str):
        for idx, s in enumerate(self.steps):
            if s["id"] == step_id:
                self.current_step_index = idx
                self.hide_all_adaptation_overlays()
                self._display_current_step()
                return

    def apply_adaptation(self, action: AdaptationAction, triggering_behavior: str = "ConfusionStruggle", current_time: float = 0.0):
        step = self.steps[self.current_step_index]
        if self.current_canonical_event:
            self.current_canonical_event.behavior = triggering_behavior
            if self.predictor:
                if hasattr(self.predictor, 'notify_actual_event_fired'):
                    try:
                        self.predictor.notify_actual_event_fired(triggering_behavior, current_time)
                    except TypeError:
                        self.predictor.notify_actual_event_fired(triggering_behavior, self.current_canonical_event)

        if action == AdaptationAction.SimplifyStep:
            self.is_simplify_active = True
            self.current_instruction_text = f"[SIMPLIFIED] {step['title']}: Step-by-step breakdown."
        elif action == AdaptationAction.InjectCue:
            self.is_cue_active = True
            self.current_instruction_text += f"\n[CUE] Follow 3D arrow to {step.get('highlight', 'component')}."
        elif action == AdaptationAction.SlowPace:
            self.is_slow_pace_active = True
            self.current_detail_text = f"[SLOWED] Detailed instructions: {step['detail']}"
        elif action == AdaptationAction.InsertHint:
            self.is_hint_active = True
            self.current_hint_text = f"[HINT] Check alignment and polarity for {step.get('highlight', 'component')}."

    def next_step(self):
        if self.current_step_index < len(self.steps) - 1:
            if self.predictor and hasattr(self.predictor, 'invalidate_pending_prediction'):
                self.predictor.invalidate_pending_prediction()
            self.current_step_index += 1
            self.hide_all_adaptation_overlays()
            self._display_current_step()

    def previous_step(self):
        if self.current_step_index > 0:
            if self.predictor and hasattr(self.predictor, 'invalidate_pending_prediction'):
                self.predictor.invalidate_pending_prediction()
            self.current_step_index -= 1
            self.hide_all_adaptation_overlays()
            self._display_current_step()

    def hide_all_adaptation_overlays(self):
        self.is_cue_active = False
        self.is_hint_active = False
        self.is_slow_pace_active = False
        self.is_simplify_active = False
        self.current_hint_text = ""
