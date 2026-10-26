# -*- coding: utf-8 -*-
"""
A³R Phase 9A: Hardware Abstraction Layer & Mock ESP32 Simulator
Provides deterministic simulation of the physical two-LED circuit hardware:
  - Disconnected, Connecting, Connected, Recovering, Timeout, Error states
  - Command execution (Ping, SetLED, BlinkLED, ResetCircuit, StepSync)
  - Acknowledgement handling (ACK, NACK, Timeout, InvalidCommand)
  - Heartbeat & Timeout recovery
  - Technical disruption signaling for HardwareDisconnection
  - Strict is_mock = True metadata separation

Note: GPIO 13 / GPIO 12 are provisional software-test pin assignments.
Final physical GPIO mapping will be confirmed against the actual ESP32 board model during Phase 9B.
"""

import time
import uuid
from enum import Enum
from dataclasses import dataclass, field
from typing import Optional, List, Dict, Callable, Any


class HardwareConnectionState(str, Enum):
    Disconnected = "Disconnected"
    Connecting = "Connecting"
    Connected = "Connected"
    Recovering = "Recovering"
    Timeout = "Timeout"
    Error = "Error"


class HardwareCommandType(str, Enum):
    Ping = "Ping"
    SetLED = "SetLED"
    BlinkLED = "BlinkLED"
    ResetCircuit = "ResetCircuit"
    StepSync = "StepSync"


class LedIdentifier(str, Enum):
    # Provisional software-test pin assignments:
    # Final physical GPIO mapping will be confirmed against the actual ESP32 board model during Phase 9B.
    Led1 = "Led1"   # Provisional GPIO 13 (circuit target)
    Led2 = "Led2"   # Provisional GPIO 12 (status/auxiliary)
    Both = "Both"


class LedState(str, Enum):
    Off = "Off"
    On = "On"
    Blinking = "Blinking"


class AckStatus(str, Enum):
    Success = "Success"
    Failure = "Failure"
    Timeout = "Timeout"
    InvalidCommand = "InvalidCommand"
    Busy = "Busy"


@dataclass
class HardwareCommand:
    type: HardwareCommandType
    target_led: LedIdentifier = LedIdentifier.Led1
    state: LedState = LedState.Off
    blink_rate_ms: int = 1000
    payload: str = ""
    timestamp: float = 0.0
    command_id: str = field(default_factory=lambda: uuid.uuid4().hex[:8])


@dataclass
class CommandAck:
    command_id: str
    status: AckStatus
    latency_ms: float
    message: str = ""
    timestamp: float = 0.0


@dataclass
class HardwareEvent:
    event_type: str
    data: str
    timestamp: float
    is_mock: bool = True


class MockESP32Bridge:
    """
    Deterministic software simulation of the physical ESP32 two-LED task bridge.
    Operates on simulated time (t_sim) for cycle-accurate, bit-for-bit test reproducibility.
    """

    def __init__(
        self,
        device_address: str = "ESP32_MOCK_DEV_01",
        connect_delay_s: float = 0.2,
        sim_latency_min_ms: float = 5.0,
        sim_latency_max_ms: float = 25.0,
        heartbeat_interval_s: float = 1.0,
        timeout_duration_s: float = 3.0,
        max_recovery_attempts: int = 3
    ):
        self.device_address = device_address
        self.connect_delay_s = connect_delay_s
        self.sim_latency_min_ms = sim_latency_min_ms
        self.sim_latency_max_ms = sim_latency_max_ms
        self.heartbeat_interval_s = heartbeat_interval_s
        self.timeout_duration_s = timeout_duration_s
        self.max_recovery_attempts = max_recovery_attempts

        # State
        self.current_state: HardwareConnectionState = HardwareConnectionState.Disconnected
        self.last_heartbeat_time: float = 0.0
        self.last_ping_time: float = 0.0
        self.led1_state: LedState = LedState.Off
        self.led2_state: LedState = LedState.Off
        self.recovery_attempts: int = 0
        self.is_recovering: bool = False
        self.is_mock: bool = True

        # Telemetry records
        self.logged_records: List[Dict[str, Any]] = []
        self.command_history: List[HardwareCommand] = []
        self.ack_history: List[CommandAck] = []
        self.state_history: List[HardwareConnectionState] = [HardwareConnectionState.Disconnected]

        # Callbacks
        self.on_state_changed: Optional[Callable[[HardwareConnectionState, HardwareConnectionState], None]] = None
        self.on_command_ack: Optional[Callable[[HardwareCommand, CommandAck], None]] = None
        self.on_hardware_event: Optional[Callable[[HardwareEvent], None]] = None
        self.on_error: Optional[Callable[[str], None]] = None
        self.on_technical_disruption: Optional[Callable[[str], None]] = None

        # Fault Injection Flags
        self._force_next_drop: bool = False
        self._force_next_timeout: bool = False
        self._force_next_invalid: bool = False
        self._force_recovery_failure: bool = False

    @property
    def is_connected(self) -> bool:
        return self.current_state == HardwareConnectionState.Connected

    # ── Connection Lifecycle ──────────────────────────────────────────────────
    def connect(self, current_time: float = 0.0, address: str = ""):
        if self.current_state in (HardwareConnectionState.Connected, HardwareConnectionState.Connecting):
            return

        if address:
            self.device_address = address

        self._set_state(HardwareConnectionState.Connecting, current_time)

        # In discrete simulation, complete connection at current_time + connect_delay_s
        # For immediate simulation call, transition directly or via tick
        self.recovery_attempts = 0
        self.is_recovering = False
        self.last_heartbeat_time = current_time + self.connect_delay_s
        self.last_ping_time = current_time + self.connect_delay_s
        self._set_state(HardwareConnectionState.Connected, current_time + self.connect_delay_s)

    def disconnect(self, current_time: float = 0.0, unexpected: bool = False):
        if self.current_state == HardwareConnectionState.Disconnected:
            return

        self.reset_circuit(current_time)
        self._set_state(HardwareConnectionState.Disconnected, current_time)

        if unexpected and self.on_technical_disruption:
            self.on_technical_disruption("HardwareDisconnection")

    # ── Command Dispatch ──────────────────────────────────────────────────────
    def send_command(self, command: HardwareCommand, current_time: float = 0.0) -> Optional[CommandAck]:
        command.timestamp = current_time
        self.command_history.append(command)

        # If not connected, fail immediately
        if self.current_state != HardwareConnectionState.Connected:
            nack = CommandAck(
                command_id=command.command_id,
                status=AckStatus.Failure,
                latency_ms=0.0,
                message=f"Cannot execute command while in state {self.current_state.value}",
                timestamp=current_time
            )
            self.ack_history.append(nack)
            self._log_command(command, nack, current_time)
            if self.on_command_ack:
                self.on_command_ack(command, nack)
            return nack

        # Fault injection: packet drop
        if self._force_next_drop:
            self._force_next_drop = False
            return None

        # Fault injection: timeout
        if self._force_next_timeout:
            self._force_next_timeout = False
            timeout_ack = CommandAck(
                command_id=command.command_id,
                status=AckStatus.Timeout,
                latency_ms=self.timeout_duration_s * 1000.0,
                message="Simulated command timeout",
                timestamp=current_time + self.timeout_duration_s
            )
            self.ack_history.append(timeout_ack)
            self._log_command(command, timeout_ack, current_time + self.timeout_duration_s)
            if self.on_command_ack:
                self.on_command_ack(command, timeout_ack)
            return timeout_ack

        # Simulated latency
        sim_latency = (self.sim_latency_min_ms + self.sim_latency_max_ms) / 2.0

        # Fault injection: invalid command
        if self._force_next_invalid:
            self._force_next_invalid = False
            invalid_ack = CommandAck(
                command_id=command.command_id,
                status=AckStatus.InvalidCommand,
                latency_ms=sim_latency,
                message="Simulated invalid command syntax",
                timestamp=current_time + (sim_latency / 1000.0)
            )
            self.ack_history.append(invalid_ack)
            self._log_command(command, invalid_ack, current_time + (sim_latency / 1000.0))
            if self.on_command_ack:
                self.on_command_ack(command, invalid_ack)
            return invalid_ack

        # Process valid command state mutation
        if command.type == HardwareCommandType.SetLED:
            if command.target_led in (LedIdentifier.Led1, LedIdentifier.Both):
                self.led1_state = command.state
            if command.target_led in (LedIdentifier.Led2, LedIdentifier.Both):
                self.led2_state = command.state

        elif command.type == HardwareCommandType.BlinkLED:
            if command.target_led in (LedIdentifier.Led1, LedIdentifier.Both):
                self.led1_state = LedState.Blinking
            if command.target_led in (LedIdentifier.Led2, LedIdentifier.Both):
                self.led2_state = LedState.Blinking

        elif command.type == HardwareCommandType.ResetCircuit:
            self.led1_state = LedState.Off
            self.led2_state = LedState.Off

        elif command.type == HardwareCommandType.Ping:
            self.last_heartbeat_time = current_time

        ack_time = current_time + (sim_latency / 1000.0)
        self.last_heartbeat_time = ack_time

        ack = CommandAck(
            command_id=command.command_id,
            status=AckStatus.Success,
            latency_ms=sim_latency,
            message=f"OK - {command.type.value}",
            timestamp=ack_time
        )
        self.ack_history.append(ack)
        self._log_command(command, ack, ack_time)
        if self.on_command_ack:
            self.on_command_ack(command, ack)
        return ack

    # ── Convenience Methods ──────────────────────────────────────────────────
    def set_led(self, led: LedIdentifier, state: LedState, current_time: float = 0.0) -> Optional[CommandAck]:
        return self.send_command(HardwareCommand(HardwareCommandType.SetLED, led, state), current_time)

    def blink_led(self, led: LedIdentifier, rate_ms: int = 1000, current_time: float = 0.0) -> Optional[CommandAck]:
        return self.send_command(HardwareCommand(HardwareCommandType.BlinkLED, led, LedState.Blinking, rate_ms), current_time)

    def reset_circuit(self, current_time: float = 0.0) -> Optional[CommandAck]:
        return self.send_command(HardwareCommand(HardwareCommandType.ResetCircuit, LedIdentifier.Both, LedState.Off), current_time)

    # ── Heartbeat & Tick ─────────────────────────────────────────────────────
    def tick(self, current_time: float, delta_time: float):
        if self.current_state == HardwareConnectionState.Connected:
            # Check timeout
            if (current_time - self.last_heartbeat_time) > self.timeout_duration_s:
                self._set_state(HardwareConnectionState.Timeout, current_time)
                self._start_recovery(current_time)

    def force_timeout(self, current_time: float):
        self.last_heartbeat_time = current_time - (self.timeout_duration_s + 1.0)
        self.tick(current_time, 0.1)

    def _start_recovery(self, current_time: float):
        self.is_recovering = True
        self._set_state(HardwareConnectionState.Recovering, current_time)

        if self.on_technical_disruption:
            self.on_technical_disruption("HardwareDisconnection")

        # Execute recovery attempts
        while self.recovery_attempts < self.max_recovery_attempts:
            self.recovery_attempts += 1
            if self._force_recovery_failure:
                continue

            # Successful recovery
            self.is_recovering = False
            self.last_heartbeat_time = current_time
            self.last_ping_time = current_time
            self.recovery_attempts = 0
            self._set_state(HardwareConnectionState.Connected, current_time)
            return

        # Recovery failed
        self.is_recovering = False
        self._set_state(HardwareConnectionState.Error, current_time)
        if self.on_error:
            self.on_error(f"Connection lost. Recovery failed after {self.max_recovery_attempts} attempts.")

    # ── Fault Injection ───────────────────────────────────────────────────────
    def inject_drop_next(self):
        self._force_next_drop = True

    def inject_timeout_next(self):
        self._force_next_timeout = True

    def inject_invalid_next(self):
        self._force_next_invalid = True

    def inject_recovery_failure(self, fail: bool = True):
        self._force_recovery_failure = fail

    # ── State and Logging Helpers ─────────────────────────────────────────────
    def _set_state(self, new_state: HardwareConnectionState, current_time: float):
        if self.current_state == new_state:
            return

        old_state = self.current_state
        self.current_state = new_state
        self.state_history.append(new_state)

        if self.on_state_changed:
            self.on_state_changed(old_state, new_state)

        record = {
            "type": "hardware",
            "ts": current_time,
            "state": new_state.value,
            "command_type": "STATE_CHANGE",
            "is_mock": True,
            "details": f"Transition from {old_state.value} to {new_state.value}"
        }
        self.logged_records.append(record)

    def _log_command(self, cmd: HardwareCommand, ack: CommandAck, current_time: float):
        record = {
            "type": "hardware",
            "ts": current_time,
            "state": "ACK_RECEIVED" if ack.status == AckStatus.Success else "NACK_OR_TIMEOUT",
            "command_id": cmd.command_id,
            "command_type": cmd.type.value,
            "target_led": cmd.target_led.value,
            "led_state": cmd.state.value,
            "ack_status": ack.status.value,
            "latency_ms": ack.latency_ms,
            "is_mock": True,
            "details": ack.message
        }
        self.logged_records.append(record)
