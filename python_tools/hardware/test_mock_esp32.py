# -*- coding: utf-8 -*-
"""
A³R Phase 9A Unit Tests: Mock ESP32 Bridge & Hardware Abstraction Layer
Tests all 11 required scenarios deterministically:
  1. connect
  2. successful command
  3. LED1 command
  4. LED2 command
  5. ACK
  6. timeout
  7. disconnect
  8. reconnect
  9. failed recovery
  10. invalid command
  11. session logging
"""

import sys
import unittest
from pathlib import Path

_PROJECT_ROOT = Path(__file__).resolve().parent.parent.parent
if str(_PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(_PROJECT_ROOT))

from python_tools.hardware.mock_esp32 import (
    MockESP32Bridge,
    HardwareConnectionState,
    HardwareCommand,
    HardwareCommandType,
    LedIdentifier,
    LedState,
    AckStatus,
    CommandAck
)


class TestMockESP32Bridge(unittest.TestCase):
    """Deterministic test suite for Phase 9A Hardware Abstraction Layer."""

    def setUp(self):
        self.bridge = MockESP32Bridge(
            device_address="ESP32_TEST_01",
            connect_delay_s=0.1,
            sim_latency_min_ms=10.0,
            sim_latency_max_ms=20.0,
            heartbeat_interval_s=1.0,
            timeout_duration_s=3.0,
            max_recovery_attempts=3
        )

    # ── Test 1: Connect ───────────────────────────────────────────────────────
    def test_01_connect(self):
        """Verify initial Disconnected state and transition to Connected."""
        self.assertEqual(self.bridge.current_state, HardwareConnectionState.Disconnected)
        self.assertFalse(self.bridge.is_connected)

        state_changes = []
        self.bridge.on_state_changed = lambda old, new: state_changes.append((old, new))

        self.bridge.connect(current_time=1.0)
        self.assertTrue(self.bridge.is_connected)
        self.assertEqual(self.bridge.current_state, HardwareConnectionState.Connected)
        self.assertEqual(len(state_changes), 2)
        self.assertEqual(state_changes[0], (HardwareConnectionState.Disconnected, HardwareConnectionState.Connecting))
        self.assertEqual(state_changes[1], (HardwareConnectionState.Connecting, HardwareConnectionState.Connected))

    # ── Test 2: Successful Command ────────────────────────────────────────────
    def test_02_successful_command(self):
        """Verify that valid command dispatched in Connected state returns Success ACK."""
        self.bridge.connect(current_time=0.0)
        cmd = HardwareCommand(type=HardwareCommandType.Ping)
        ack = self.bridge.send_command(cmd, current_time=2.0)

        self.assertIsNotNone(ack)
        self.assertEqual(ack.status, AckStatus.Success)
        self.assertEqual(ack.command_id, cmd.command_id)
        self.assertGreaterEqual(ack.latency_ms, 10.0)
        self.assertLessEqual(ack.latency_ms, 20.0)

    # ── Test 3: LED1 Command ──────────────────────────────────────────────────
    def test_03_led1_command(self):
        """Verify SetLED targeting Led1 mutates Led1 without affecting Led2."""
        self.bridge.connect(current_time=0.0)
        self.assertEqual(self.bridge.led1_state, LedState.Off)
        self.assertEqual(self.bridge.led2_state, LedState.Off)

        ack = self.bridge.set_led(LedIdentifier.Led1, LedState.On, current_time=1.0)
        self.assertEqual(ack.status, AckStatus.Success)
        self.assertEqual(self.bridge.led1_state, LedState.On)
        self.assertEqual(self.bridge.led2_state, LedState.Off)

    # ── Test 4: LED2 Command ──────────────────────────────────────────────────
    def test_04_led2_command(self):
        """Verify commands targeting Led2 (On, Blinking, Off)."""
        self.bridge.connect(current_time=0.0)
        self.bridge.set_led(LedIdentifier.Led2, LedState.On, current_time=1.0)
        self.assertEqual(self.bridge.led2_state, LedState.On)

        self.bridge.blink_led(LedIdentifier.Led2, rate_ms=500, current_time=2.0)
        self.assertEqual(self.bridge.led2_state, LedState.Blinking)

        self.bridge.reset_circuit(current_time=3.0)
        self.assertEqual(self.bridge.led1_state, LedState.Off)
        self.assertEqual(self.bridge.led2_state, LedState.Off)

    # ── Test 5: Command Acknowledgement Handling ──────────────────────────────
    def test_05_ack_handling(self):
        """Verify ACK callback fires with matching command and ACK metadata."""
        self.bridge.connect(current_time=0.0)
        received_pairs = []
        self.bridge.on_command_ack = lambda c, a: received_pairs.append((c, a))

        cmd = HardwareCommand(HardwareCommandType.SetLED, LedIdentifier.Led1, LedState.On)
        ack = self.bridge.send_command(cmd, current_time=5.0)

        self.assertEqual(len(received_pairs), 1)
        self.assertEqual(received_pairs[0][0].command_id, cmd.command_id)
        self.assertEqual(received_pairs[0][1].status, AckStatus.Success)

    # ── Test 6: Timeout Handling ──────────────────────────────────────────────
    def test_06_timeout_handling(self):
        """Verify heartbeat timeout detection when silence exceeds threshold."""
        self.bridge.connect(current_time=0.0)
        # Advance time past timeout duration (3.0s) without communication
        self.bridge.tick(current_time=4.5, delta_time=4.5)

        # Timeout should trigger transition to Timeout, then Recovering -> Connected
        self.assertIn(HardwareConnectionState.Timeout, self.bridge.state_history)
        self.assertIn(HardwareConnectionState.Recovering, self.bridge.state_history)

    # ── Test 7: Disconnect ────────────────────────────────────────────────────
    def test_07_disconnect(self):
        """Verify graceful disconnection turns off LEDs and signals Disconnected."""
        self.bridge.connect(current_time=0.0)
        self.bridge.set_led(LedIdentifier.Led1, LedState.On, current_time=1.0)
        self.assertEqual(self.bridge.led1_state, LedState.On)

        self.bridge.disconnect(current_time=2.0)
        self.assertEqual(self.bridge.current_state, HardwareConnectionState.Disconnected)
        self.assertFalse(self.bridge.is_connected)
        self.assertEqual(self.bridge.led1_state, LedState.Off)

    # ── Test 8: Reconnect & Recovery ──────────────────────────────────────────
    def test_08_reconnect_and_recovery(self):
        """Verify automatic recovery returns bridge to Connected state."""
        self.bridge.connect(current_time=0.0)
        self.bridge.force_timeout(current_time=5.0)

        # Bridge should have recovered back to Connected
        self.assertEqual(self.bridge.current_state, HardwareConnectionState.Connected)
        self.assertTrue(self.bridge.is_connected)

    # ── Test 9: Failed Recovery ───────────────────────────────────────────────
    def test_09_failed_recovery(self):
        """Verify that exhausting recovery attempts transitions state to Error."""
        self.bridge.connect(current_time=0.0)
        self.bridge.inject_recovery_failure(True)

        errors = []
        self.bridge.on_error = lambda err: errors.append(err)

        self.bridge.force_timeout(current_time=5.0)

        self.assertEqual(self.bridge.current_state, HardwareConnectionState.Error)
        self.assertFalse(self.bridge.is_connected)
        self.assertEqual(len(errors), 1)
        self.assertIn("Recovery failed after 3 attempts", errors[0])

    # ── Test 10: Invalid Command Handling ─────────────────────────────────────
    def test_10_invalid_command_handling(self):
        """Verify that malformed or injected invalid command returns InvalidCommand ACK."""
        self.bridge.connect(current_time=0.0)
        self.bridge.inject_invalid_next()

        cmd = HardwareCommand(HardwareCommandType.SetLED, LedIdentifier.Led1, LedState.On)
        ack = self.bridge.send_command(cmd, current_time=1.0)

        self.assertIsNotNone(ack)
        self.assertEqual(ack.status, AckStatus.InvalidCommand)
        self.assertIn("invalid command syntax", ack.message)

    # ── Test 11: Session Logging Integration ──────────────────────────────────
    def test_11_session_logging_integration(self):
        """Verify that all hardware actions create structured records with is_mock=True."""
        self.bridge.connect(current_time=1.0)
        self.bridge.set_led(LedIdentifier.Led1, LedState.On, current_time=2.0)
        self.bridge.disconnect(current_time=3.0)

        records = self.bridge.logged_records
        self.assertGreaterEqual(len(records), 3)

        for rec in records:
            self.assertEqual(rec["type"], "hardware")
            self.assertTrue(rec["is_mock"])
            self.assertIn("state", rec)
            self.assertIn("ts", rec)


if __name__ == "__main__":
    unittest.main()
