"""
A3R Phase 11 — Software End-to-End Unit & Integration Tests.
Rigorously tests complete closed-loop orchestration:
  1. Full nominal 8-step circuit assembly workflow.
  2. Confusion struggle precursor anticipation & escalation sequence.
  3. Frustration disengagement precursor anticipation & escalation sequence.
  4. 15.0s Cooldown enforcement.
  5. Step-level max 3 adaptations ceiling.
  6. Recovery floor (<0.30) behavior.
  7. Hardware StepSync, Step 8 BlinkLED, and completion ResetCircuit.
  8. Hardware fault injection and auto-recovery handling.
"""

import sys
from pathlib import Path
_PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

import pytest
from python_tools.e2e.software_e2e_pipeline import (
    E2EClosedLoopSystem, compute_affect_scores,
    CONFUSION_THRESHOLD, FRUSTRATION_THRESHOLD
)
from python_tools.hardware.mock_esp32 import HardwareConnectionState, LedState, LedIdentifier

def test_frozen_affect_math_invariants():
    # Confusion = 0.40*brow + 0.30*(1-ear) + 0.30*mouth
    # brow=0.8, ear=0.2, mouth=0.7 -> 0.40*0.8 + 0.30*0.8 + 0.30*0.7 = 0.32 + 0.24 + 0.21 = 0.77
    conf, frust, eng = compute_affect_scores(brow=0.8, ear=0.2, mouth=0.7, brow_height=0.5)
    assert abs(conf - 0.77) < 1e-5
    # Frustration = 0.35*0.8 + 0.25*0.8 + 0.40*0.7 = 0.28 + 0.20 + 0.28 = 0.76
    assert abs(frust - 0.76) < 1e-5
    # Engagement = 0.50*0.2 + 0.30*(1-0.8) + 0.20*0.5 = 0.10 + 0.06 + 0.10 = 0.26
    assert abs(eng - 0.26) < 1e-5

def test_full_nominal_8_step_workflow():
    sys = E2EClosedLoopSystem()
    sys.start_session(t=0.0)
    assert sys.hardware.current_state == HardwareConnectionState.Connected
    assert len(sys.steps) == 8

    # Learner proceeds through steps 1 to 7 with nominal affect (brow=0.1, ear=0.8, mouth=0.1)
    sim_time = 1.0
    for step_idx in range(7):
        assert sys.current_step_index == step_idx
        # Send a few nominal samples
        for _ in range(3):
            rec = sys.process_affect(t=sim_time, brow=0.1, ear=0.8, mouth=0.1)
            sim_time += 0.5
            assert rec['confusion'] < CONFUSION_THRESHOLD
            assert rec['frustration'] < FRUSTRATION_THRESHOLD
            assert rec['adaptation_action'] is None

        sys.next_step()

    # Verify we are at Step 8 (verify_blink)
    assert sys.current_step_index == 7
    assert sys.steps[7]['id'] == 'verify_blink'

    # Verify Step 8 triggered BlinkLED on Led1 (Pin 13)
    assert sys.hardware.led1_state == LedState.Blinking

    # Complete step 8
    sys.next_step()
    assert sys.task_complete is True
    # Verify completion reset circuit (LEDs off)
    assert sys.hardware.led1_state == LedState.Off
    assert sys.hardware.led2_state == LedState.Off

def test_step3_polarity_confusion_struggle_and_adaptation():
    sys = E2EClosedLoopSystem()
    sys.start_session(t=0.0)

    # Fast forward to Step 3 (insert_led)
    sys.current_step_index = 2
    sys.sync_step_to_hardware()
    assert sys.steps[sys.current_step_index]['id'] == 'insert_led'

    # 1. Precursor detection
    t = 1.0
    precursor_detected = False
    for brow_val in [0.35, 0.45, 0.55, 0.65]:
        rec = sys.process_affect(t=t, brow=brow_val, ear=0.5, mouth=0.3)
        if rec['prediction_state'] == 'PREDICTED':
            precursor_detected = True
        t += 0.5

    assert precursor_detected is True
    assert sys.active_cue_target == '' # No adaptation yet (precursor only)

    # 2. Confusion elevates above 0.60 threshold -> 1st adaptation: InjectCue after 3.0s dwell
    t_start_elevated = t
    adaptation_fired = False
    while t < t_start_elevated + 3.2:
        rec = sys.process_affect(t=t, brow=0.8, ear=0.2, mouth=0.6)
        if rec['adaptation_action'] == 'InjectCue':
            adaptation_fired = True
            break
        t += 0.5

    assert adaptation_fired is True
    assert sys.active_cue_target == 'led_slot_row14_colD'
    assert 'InjectCue' in sys.adaptation_toast

    # 3. Cooldown suppression: during next 15s, even if elevated, cannot refire
    rec_cooldown = sys.process_affect(t=t + 2.0, brow=0.8, ear=0.2, mouth=0.6)
    assert rec_cooldown['adaptation_action'] is None

    # 4. Learner tries cue, affect drops below threshold during cooldown
    t += 3.0
    sys.process_affect(t=t, brow=0.2, ear=0.7, mouth=0.2) # drops below threshold

    # 5. Advance past cooldown (15.0s), learner struggles again -> 2nd adaptation: InsertHint
    t += 15.0
    t_start2 = t
    rec_hint = None
    while t < t_start2 + 3.5:
        rec_hint = sys.process_affect(t=t, brow=0.8, ear=0.2, mouth=0.6)
        if rec_hint['adaptation_action'] == 'InsertHint':
            break
        t += 0.5

    assert rec_hint['adaptation_action'] == 'InsertHint'
    assert sys.active_hint != ''

    # 6. Learner tries hint, drops below threshold
    t += 1.0
    sys.process_affect(t=t, brow=0.2, ear=0.7, mouth=0.2)

    # 7. Advance past cooldown, learner struggles again -> 3rd adaptation: SlowPace
    t += 16.0
    t_start3 = t
    rec_slow = None
    while t < t_start3 + 3.5:
        rec_slow = sys.process_affect(t=t, brow=0.8, ear=0.2, mouth=0.6)
        if rec_slow['adaptation_action'] == 'SlowPace':
            break
        t += 0.5

    assert rec_slow['adaptation_action'] == 'SlowPace'
    assert sys.detail_expanded is True
    # Led2 (Pin 12) turned on for pacing alert
    assert sys.hardware.led2_state == LedState.On

    # 8. Max 3 adaptations ceiling: drop below and re-elevate after cooldown -> 4th is blocked
    t += 1.0
    sys.process_affect(t=t, brow=0.2, ear=0.7, mouth=0.2)
    t += 16.0
    t_start4 = t
    while t < t_start4 + 4.0:
        rec_ceiling = sys.process_affect(t=t, brow=0.8, ear=0.2, mouth=0.6)
        assert rec_ceiling['adaptation_action'] is None
        t += 0.5

def test_step7_frustration_disengagement():
    sys = E2EClosedLoopSystem()
    sys.start_session(t=0.0)

    # Jump to Step 7 (upload_blink_sketch)
    sys.current_step_index = 6
    sys.sync_step_to_hardware()
    assert sys.steps[6]['id'] == 'upload_blink_sketch'

    # Frustration precursor trend
    t = 1.0
    for m in [0.4, 0.55, 0.7, 0.85]:
        sys.process_affect(t=t, brow=0.5, ear=0.4, mouth=m)
        t += 0.5

    # Sustained elevation > 3.0s -> 1st frustration action: SlowPace
    t_start = t
    fired_action = None
    while t < t_start + 3.5:
        rec = sys.process_affect(t=t, brow=0.7, ear=0.2, mouth=0.9)
        if rec['adaptation_action']:
            fired_action = rec['adaptation_action']
            break
        t += 0.5

    assert fired_action == 'SlowPace'
    assert sys.detail_expanded is True

    # Drop below threshold during cooldown
    t += 1.0
    sys.process_affect(t=t, brow=0.1, ear=0.8, mouth=0.1)

    # Advance past cooldown (15s) and sustain frustration -> 2nd frustration action: SimplifyStep
    t += 16.0
    t_start2 = t
    second_action = None
    while t < t_start2 + 3.5:
        rec = sys.process_affect(t=t, brow=0.7, ear=0.2, mouth=0.9)
        if rec['adaptation_action']:
            second_action = rec['adaptation_action']
            break
        t += 0.5

    assert second_action == 'SimplifyStep'
    assert sys.simplified_text != ''

def test_precursor_recovery_floor():
    sys = E2EClosedLoopSystem()
    sys.start_session(t=0.0)

    t = 1.0
    # Cause precursor
    for b in [0.4, 0.5, 0.6]:
        sys.process_affect(t=t, brow=b, ear=0.5, mouth=0.3)
        t += 0.5

    assert sys.prediction_engine.state == 'PREDICTED'

    # Affect drops to calm baseline (brow=0.05, ear=0.9, mouth=0.05 -> conf ~ 0.065 < 0.30)
    for _ in range(4):
        sys.process_affect(t=t, brow=0.05, ear=0.9, mouth=0.05)
        t += 0.5

    # Should recover to IDLE and clear pending prediction
    assert sys.prediction_engine.state == 'IDLE'
    assert sys.prediction_engine.pending_prediction is None

def test_hardware_fault_injection_and_recovery():
    sys = E2EClosedLoopSystem()
    sys.start_session(t=0.0)
    assert sys.hardware.current_state == HardwareConnectionState.Connected

    # Force timeout
    sys.hardware.force_timeout(current_time=5.0)
    # Bridge auto-recovers to Connected
    assert sys.hardware.current_state == HardwareConnectionState.Connected
    assert sys.hardware.is_connected

    # Set LED on after recovery
    sys.hardware.set_led(LedIdentifier.Led1, LedState.On)
    assert sys.hardware.led1_state == LedState.On
