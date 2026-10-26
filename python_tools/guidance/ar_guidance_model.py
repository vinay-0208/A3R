# -*- coding: utf-8 -*-
"""
A³R AR Guidance Visual System — Python Simulation Model & Twin
Mirrors the C# ARGuidanceController, A3R_DirectionalArrow, and A3R_TargetRing:
- 8-step circuit task spatial definitions
- Directional vector and rotation calculations
- Target ring pulsing and prominence scaling
- Adaptation visual states: Normal, ProminentCue, HintEmphasized, PacedSlow, IsolatedStep
- Safe tracking loss suppression and recovery
- Strict preservation of provisional software-test GPIO assignment notices
"""

import math
from dataclasses import dataclass, field
from enum import Enum
from typing import Optional, Dict, List, Tuple


class GuidanceVisualState(str, Enum):
    Hidden = "Hidden"
    Normal = "Normal"
    ProminentCue = "ProminentCue"       # InjectCue
    HintEmphasized = "HintEmphasized"   # InsertHint
    PacedSlow = "PacedSlow"             # SlowPace
    IsolatedStep = "IsolatedStep"       # SimplifyStep


@dataclass
class Vector3D:
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0

    def __add__(self, other: "Vector3D") -> "Vector3D":
        return Vector3D(self.x + other.x, self.y + other.y, self.z + other.z)

    def __sub__(self, other: "Vector3D") -> "Vector3D":
        return Vector3D(self.x - other.x, self.y - other.y, self.z - other.z)

    def __mul__(self, scalar: float) -> "Vector3D":
        return Vector3D(self.x * scalar, self.y * scalar, self.z * scalar)

    def magnitude(self) -> float:
        return math.sqrt(self.x * self.x + self.y * self.y + self.z * self.z)

    def normalized(self) -> "Vector3D":
        m = self.magnitude()
        if m < 1e-6:
            return Vector3D(0.0, 0.0, 0.0)
        return Vector3D(self.x / m, self.y / m, self.z / m)


@dataclass
class ARGuidanceStepDefinition:
    step_index: int
    step_id: str
    target_name: str
    target_local_offset: Vector3D
    show_arrow: bool
    show_ring: bool
    ring_radius: float
    highlight_component: str
    instruction_summary: str
    completion_condition: str
    provisional_gpio_notice: str = "Provisional software-test GPIO assignment."


# Canonical 8-step guidance definitions
STEP_GUIDANCE_DEFINITIONS: List[ARGuidanceStepDefinition] = [
    ARGuidanceStepDefinition(
        step_index=0,
        step_id="place_breadboard",
        target_name="breadboard_center",
        target_local_offset=Vector3D(0.12, 0.005, 0.0),
        show_arrow=True,
        show_ring=True,
        ring_radius=0.05,
        highlight_component="breadboard",
        instruction_summary="Place breadboard flat beside marker.",
        completion_condition="Breadboard placed beside marker",
        provisional_gpio_notice="Provisional software-test GPIO assignment."
    ),
    ARGuidanceStepDefinition(
        step_index=1,
        step_id="insert_resistor",
        target_name="resistor_slot_row10_colE",
        target_local_offset=Vector3D(0.12, 0.012, 0.02),
        show_arrow=True,
        show_ring=True,
        ring_radius=0.025,
        highlight_component="resistor",
        instruction_summary="Insert 220Ω resistor: row 10 col E to row 14 col E.",
        completion_condition="Resistor seated firmly across rows 10 and 14",
        provisional_gpio_notice="Provisional software-test GPIO assignment."
    ),
    ARGuidanceStepDefinition(
        step_index=2,
        step_id="insert_led",
        target_name="led_slot_row14_colD",
        target_local_offset=Vector3D(0.12, 0.015, -0.01),
        show_arrow=True,
        show_ring=True,
        ring_radius=0.025,
        highlight_component="led",
        instruction_summary="Insert LED: Long leg (anode) row 14, short leg (cathode) row 15.",
        completion_condition="LED inserted with correct polarity",
        provisional_gpio_notice="Provisional software-test GPIO assignment."
    ),
    ARGuidanceStepDefinition(
        step_index=3,
        step_id="wire_resistor_to_pin13",
        target_name="board_gpio13",
        target_local_offset=Vector3D(0.24, 0.015, 0.03),
        show_arrow=True,
        show_ring=True,
        ring_radius=0.025,
        highlight_component="jumper_wire",
        instruction_summary="Connect wire from board GPIO 13 to row 10 col A.",
        completion_condition="Wire connected from GPIO 13 to row 10",
        provisional_gpio_notice="Provisional software-test GPIO assignment: GPIO 13 is a provisional software-test assignment only. Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available."
    ),
    ARGuidanceStepDefinition(
        step_index=4,
        step_id="wire_gnd",
        target_name="board_gnd",
        target_local_offset=Vector3D(0.24, 0.015, 0.01),
        show_arrow=True,
        show_ring=True,
        ring_radius=0.025,
        highlight_component="jumper_wire",
        instruction_summary="Connect wire from board GND to row 15 col A.",
        completion_condition="Wire connected from GND to row 15",
        provisional_gpio_notice="Provisional software-test GPIO assignment. Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available."
    ),
    ARGuidanceStepDefinition(
        step_index=5,
        step_id="connect_usb",
        target_name="board_usb_port",
        target_local_offset=Vector3D(0.24, 0.015, -0.05),
        show_arrow=True,
        show_ring=True,
        ring_radius=0.035,
        highlight_component="arduino_uno",
        instruction_summary="Connect development board to PC via USB cable.",
        completion_condition="Power LED illuminates on board",
        provisional_gpio_notice="Provisional software-test GPIO assignment. Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available."
    ),
    ARGuidanceStepDefinition(
        step_index=6,
        step_id="upload_blink_sketch",
        target_name="",
        target_local_offset=Vector3D(0.0, 0.0, 0.0),
        show_arrow=False,
        show_ring=False,
        ring_radius=0.0,
        highlight_component="",
        instruction_summary="Upload Blink sketch from Arduino IDE.",
        completion_condition="Done uploading status in IDE",
        provisional_gpio_notice="Provisional software-test GPIO assignment."
    ),
    ARGuidanceStepDefinition(
        step_index=7,
        step_id="verify_blink",
        target_name="led_position",
        target_local_offset=Vector3D(0.12, 0.02, -0.01),
        show_arrow=True,
        show_ring=True,
        ring_radius=0.03,
        highlight_component="led",
        instruction_summary="Observe LED blinking at 1-second intervals.",
        completion_condition="Blinking verified on physical circuit",
        provisional_gpio_notice="Provisional software-test GPIO assignment: GPIO 13 is a provisional software-test assignment only (LED1 on provisional GPIO 13). Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available."
    )
]


class ARGuidanceModel:
    """
    Python simulation of ARGuidanceController:
    - Step transitions
    - Tracking loss and recovery
    - Directional arrow pointing calculation
    - Target ring activation & scale
    - Adaptation visual responses
    - Instance recycling (no duplicate creation)
    """
    def __init__(self):
        self.step_definitions = {d.step_index: d for d in STEP_GUIDANCE_DEFINITIONS}
        self.current_step_index: int = 0
        self.is_tracking: bool = False
        self.visual_state: GuidanceVisualState = GuidanceVisualState.Normal

        # Simulated visual component state
        self.arrow_visible: bool = False
        self.arrow_position: Vector3D = Vector3D(0, 0, 0)
        self.arrow_pointing_dir: Vector3D = Vector3D(0, 0, 1)
        self.arrow_scale: float = 1.0

        self.ring_visible: bool = False
        self.ring_position: Vector3D = Vector3D(0, 0, 0)
        self.ring_scale: float = 1.0
        self.ring_pulse_frequency: float = 2.5

        self.highlight_active: bool = False
        self.instantiated_object_count: int = 2  # Pre-instantiated Arrow & Ring
        self.content_root_position: Vector3D = Vector3D(0, 0, 0)
        self.camera_position: Vector3D = Vector3D(0.0, 0.30, -0.25)

    def on_marker_detected(self, root_pos: Vector3D = Vector3D(0, 0, 0)):
        self.is_tracking = True
        self.content_root_position = root_pos
        self.update_guidance_visuals()

    def on_marker_lost(self):
        self.is_tracking = False
        self.arrow_visible = False
        self.ring_visible = False
        self.highlight_active = False

    def on_step_changed(self, step_index: int):
        self.current_step_index = step_index
        self.visual_state = GuidanceVisualState.Normal
        self.update_guidance_visuals()

    def on_task_complete(self):
        self.visual_state = GuidanceVisualState.Hidden
        self.arrow_visible = False
        self.ring_visible = False
        self.highlight_active = False

    def apply_visual_adaptation(self, action: str):
        if action == "InjectCue":
            self.visual_state = GuidanceVisualState.ProminentCue
            self.arrow_scale = 1.30
            self.ring_scale = 1.35
            self.ring_pulse_frequency = 3.75
        elif action == "InsertHint":
            self.visual_state = GuidanceVisualState.HintEmphasized
            self.arrow_scale = 1.15
            self.ring_scale = 1.15
            self.ring_pulse_frequency = 3.0
        elif action == "SlowPace":
            self.visual_state = GuidanceVisualState.PacedSlow
            self.arrow_scale = 1.0
            self.ring_scale = 1.0
            self.ring_pulse_frequency = 1.5
        elif action == "SimplifyStep":
            self.visual_state = GuidanceVisualState.IsolatedStep
            self.arrow_scale = 1.20
            self.ring_scale = 1.20
            self.ring_pulse_frequency = 2.25

    def reset_to_normal_guidance(self):
        self.visual_state = GuidanceVisualState.Normal
        self.arrow_scale = 1.0
        self.ring_scale = 1.0
        self.ring_pulse_frequency = 2.5
        self.update_guidance_visuals()

    def update_guidance_visuals(self):
        if not self.is_tracking:
            self.arrow_visible = False
            self.ring_visible = False
            self.highlight_active = False
            return

        def_step = self.step_definitions.get(self.current_step_index)
        if def_step is None or not def_step.target_name or (not def_step.show_arrow and not def_step.show_ring):
            self.arrow_visible = False
            self.ring_visible = False
            self.highlight_active = False
            return

        target_world_pos = self.content_root_position + def_step.target_local_offset

        if def_step.show_arrow:
            self.arrow_visible = (self.visual_state != GuidanceVisualState.Hidden)
            self.arrow_position = target_world_pos + Vector3D(0.0, 0.06, -0.04)
            direction = (target_world_pos - self.arrow_position).normalized()
            self.arrow_pointing_dir = direction
        else:
            self.arrow_visible = False

        if def_step.show_ring:
            self.ring_visible = (self.visual_state != GuidanceVisualState.Hidden)
            self.ring_position = target_world_pos
        else:
            self.ring_visible = False

        self.highlight_active = bool(def_step.highlight_component)
