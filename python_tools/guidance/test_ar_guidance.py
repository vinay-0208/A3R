# -*- coding: utf-8 -*-
import sys
from pathlib import Path

_PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

import pytest
from python_tools.guidance.ar_guidance_model import (
    ARGuidanceModel,
    GuidanceVisualState,
    STEP_GUIDANCE_DEFINITIONS,
    Vector3D
)


@pytest.fixture
def guidance():
    model = ARGuidanceModel()
    model.on_marker_detected(Vector3D(0.0, 0.0, 0.0))
    return model


def test_01_step_1_guidance_appears(guidance):
    guidance.on_step_changed(0)
    assert guidance.is_tracking is True
    assert guidance.current_step_index == 0
    assert guidance.arrow_visible is True
    assert guidance.ring_visible is True
    assert guidance.highlight_active is True
    assert guidance.step_definitions[0].target_name == "breadboard_center"


def test_02_step_transitions_update_target(guidance):
    guidance.on_step_changed(0)
    pos_step1 = (guidance.ring_position.x, guidance.ring_position.y, guidance.ring_position.z)

    guidance.on_step_changed(1)
    pos_step2 = (guidance.ring_position.x, guidance.ring_position.y, guidance.ring_position.z)
    assert pos_step2 != pos_step1
    assert guidance.step_definitions[1].target_name == "resistor_slot_row10_colE"

    guidance.on_step_changed(2)
    pos_step3 = (guidance.ring_position.x, guidance.ring_position.y, guidance.ring_position.z)
    assert pos_step3 != pos_step2
    assert guidance.step_definitions[2].target_name == "led_slot_row14_colD"


def test_03_arrow_points_toward_target(guidance):
    guidance.on_step_changed(1)
    target = guidance.ring_position
    arrow_pos = guidance.arrow_position
    direction = guidance.arrow_pointing_dir

    expected_dir = (target - arrow_pos).normalized()
    dot = direction.x * expected_dir.x + direction.y * expected_dir.y + direction.z * expected_dir.z
    assert dot > 0.999


def test_04_target_ring_activates(guidance):
    guidance.on_step_changed(2)
    assert guidance.ring_visible is True
    assert guidance.ring_scale == 1.0
    assert guidance.ring_position.x == pytest.approx(0.12, abs=1e-3)
    assert guidance.ring_position.z == pytest.approx(-0.01, abs=1e-3)


def test_05_guidance_hides_when_target_invalid(guidance):
    guidance.on_step_changed(6)
    assert guidance.step_definitions[6].step_id == "upload_blink_sketch"
    assert guidance.step_definitions[6].target_name == ""
    assert guidance.arrow_visible is False
    assert guidance.ring_visible is False
    assert guidance.highlight_active is False


def test_06_tracking_loss_suppresses_guidance(guidance):
    guidance.on_step_changed(2)
    assert guidance.arrow_visible is True
    assert guidance.ring_visible is True

    guidance.on_marker_lost()
    assert guidance.is_tracking is False
    assert guidance.arrow_visible is False
    assert guidance.ring_visible is False
    assert guidance.highlight_active is False


def test_07_tracking_recovery_restores_guidance(guidance):
    guidance.on_step_changed(3)
    guidance.on_marker_lost()
    assert guidance.arrow_visible is False

    guidance.on_marker_detected(Vector3D(0.05, 0.0, 0.02))
    assert guidance.is_tracking is True
    assert guidance.arrow_visible is True
    assert guidance.ring_visible is True
    assert guidance.ring_position.x == pytest.approx(0.05 + 0.24, abs=1e-3)


def test_08_confusion_inject_cue_changes_presentation(guidance):
    guidance.on_step_changed(2)
    guidance.apply_visual_adaptation("InjectCue")
    assert guidance.visual_state == GuidanceVisualState.ProminentCue
    assert guidance.arrow_scale == 1.30
    assert guidance.ring_scale == 1.35
    assert guidance.ring_pulse_frequency > 2.5


def test_09_confusion_insert_hint_changes_presentation(guidance):
    guidance.on_step_changed(2)
    guidance.apply_visual_adaptation("InsertHint")
    assert guidance.visual_state == GuidanceVisualState.HintEmphasized
    assert guidance.arrow_scale == 1.15
    assert guidance.ring_scale == 1.15


def test_10_frustration_slow_pace_changes_presentation(guidance):
    guidance.on_step_changed(3)
    guidance.apply_visual_adaptation("SlowPace")
    assert guidance.visual_state == GuidanceVisualState.PacedSlow
    assert guidance.ring_pulse_frequency < 2.0


def test_11_frustration_simplify_step_isolates_target(guidance):
    guidance.on_step_changed(3)
    guidance.apply_visual_adaptation("SimplifyStep")
    assert guidance.visual_state == GuidanceVisualState.IsolatedStep
    assert guidance.arrow_scale == 1.20
    assert guidance.ring_scale == 1.20


def test_12_engagement_returns_to_normal_guidance(guidance):
    guidance.on_step_changed(2)
    guidance.apply_visual_adaptation("InjectCue")
    assert guidance.visual_state == GuidanceVisualState.ProminentCue

    guidance.reset_to_normal_guidance()
    assert guidance.visual_state == GuidanceVisualState.Normal
    assert guidance.arrow_scale == 1.0
    assert guidance.ring_scale == 1.0
    assert guidance.ring_pulse_frequency == 2.5


def test_13_completion_clears_guidance(guidance):
    guidance.on_step_changed(7)
    assert guidance.arrow_visible is True
    guidance.on_task_complete()
    assert guidance.visual_state == GuidanceVisualState.Hidden
    assert guidance.arrow_visible is False
    assert guidance.ring_visible is False
    assert guidance.highlight_active is False


def test_14_no_duplicate_guidance_objects(guidance):
    initial_count = guidance.instantiated_object_count
    for _ in range(5):
        for step in range(8):
            guidance.on_step_changed(step)
    assert guidance.instantiated_object_count == initial_count


def test_15_provisional_gpio_wording_preserved(guidance):
    for step_idx, step_def in guidance.step_definitions.items():
        assert "Provisional software-test GPIO assignment" in step_def.provisional_gpio_notice, (
            f"Step {step_idx} missing provisional GPIO notice"
        )
