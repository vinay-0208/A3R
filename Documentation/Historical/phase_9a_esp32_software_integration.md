# A³R Phase 9A — Software-Only ESP32 Integration & Hardware Abstraction Layer
> **Phase Status**: PASS — SOFTWARE HAL & DETERMINISTIC TEST SUITE COMPLETE / FROZEN  
> **Date**: 2026-09-15 / 2026-09-16  
> **Scope**: Hardware Abstraction Layer (HAL), Two-LED Circuit Protocol, Mock ESP32 Bridge, Session Logger Integration, Fault Injection, and Deterministic Python Twin.

---

## 1. Executive Summary & Objective

Phase 9A establishes a modular, transport-agnostic **Hardware Abstraction Layer (HAL)** for the A³R system. It allows the AR application (`ARContentManager`, `AffectPredictor`, and `SessionLogger`) to communicate seamlessly with the circuit construction hardware (an ESP32 microcontroller controlling target LEDs) without binding higher-level cognitive and adaptation logic directly to low-level physical transport implementations (USB Serial, Bluetooth Low Energy, or Wi-Fi sockets).

Because the physical ESP32 was unavailable during this offline work session, Phase 9A implemented a complete **deterministic software mock bridge** (`MockESP32Bridge.cs` and Python twin `mock_esp32.py`) with realistic simulated transmission latencies (5–25 ms), heartbeat keepalive monitoring, automatic reconnection recovery, fault injection hooks, and telemetry tagging (`is_mock: true`).

All **11 standardized hardware integration unit tests** passed with a **100.0% pass rate** in Python, and the Unity C# implementation compiled with **0 errors and 0 warnings**.

---

## 2. Hardware Architecture & Two-LED Circuit Specification

The instructional circuit assembly task (`led_blink_task.json`) guides the learner in assembling an LED circuit on a breadboard. The hardware interface governs two distinct LED roles:

| Identifier | Pin | Functional Role |
| :--- | :--- | :--- |
| `LedIdentifier.Led1` | GPIO 13 (Provisional) | Circuit Target LED (assembled by user on breadboard; verified blinking in Step 8) |
| `LedIdentifier.Led2` | GPIO 12 (Provisional) | Secondary Auxiliary / Status Alert LED (activated during pacing/frustration AR) |

> [!NOTE]
> GPIO 13 / GPIO 12 are provisional software-test pin assignments. Final physical GPIO mapping will be confirmed against the actual ESP32 board model during Phase 9B.

### Transport-Agnostic Interface (`IHardwareBridge.cs`)
The HAL decouples transport details from AR logic via the `IHardwareBridge` C# interface:
- **Connection Lifecycle**: `Connect(string address)`, `Disconnect()`, `CurrentState`, `IsConnected`, `IsMock`.
- **Command Dispatch**: `SendCommand(HardwareCommand command)`.
- **High-Level Control**: `SetLED(LedIdentifier led, LedState state)`, `BlinkLED(LedIdentifier led, int rateMs)`, `ResetCircuit()`.
- **Tick & Keepalive**: `Tick(float deltaTime)` for heartbeat keepalive and connection timeout detection.
- **Events**: `OnConnectionStateChanged`, `OnCommandAckReceived`, `OnHardwareEventReceived`, `OnHardwareError`.

---

## 3. Communication Protocol & Command Schema

Commands dispatched to the bridge follow a structured, verifiable schema:

```json
{
  "command_id": "a1b2c3d4",
  "type": "SetLED",
  "target_led": "Led1",
  "state": "On",
  "blink_rate_ms": 1000,
  "payload": "step_03_insert_led",
  "timestamp": 12.450
}
```

### Supported Command Types
1. **`Ping`**: Heartbeat check dispatched every 1.0s to verify link liveness.
2. **`SetLED`**: Turn specified LED `On` or `Off`.
3. **`BlinkLED`**: Configure specified LED to toggle periodically at a defined interval (default 1000 ms).
4. **`ResetCircuit`**: Turn all outputs off and restore nominal circuit state.
5. **`StepSync`**: Broadcast the current active AR instructional step ID to the hardware controller.

### Command Acknowledgement Schema (`CommandAck`)
Every command produces a typed acknowledgement:
```json
{
  "command_id": "a1b2c3d4",
  "status": "Success",
  "latency_ms": 12.4,
  "message": "Command executed successfully",
  "timestamp": 12.462
}
```
Possible status codes: `Success`, `Failure`, `Timeout`, `InvalidCommand`, `Busy`.

---

## 4. Connection State Machine & Fault Injection

The bridge manages an explicit connection state machine:
- `Disconnected`: Bridge initialized or shut down.
- `Connecting`: Connection handshake in progress.
- `Connected`: Link live; heartbeats exchange every 1.0s; commands execute.
- `Timeout`: Silence exceeding 3.0s triggers timeout and initiates recovery.
- `Recovering`: Auto-reconnection attempt active (up to 3 retries).
- `Error`: Fatal hardware communication failure if recovery fails.

### Safety Integration with Affect Engine
When communication drops or times out, the bridge invokes:
```csharp
affectPredictor.NotifyTechnicalDisruption("HardwareDisconnection");
```
This freezes emotional dwell accumulation in `AdaptationController`, preventing spurious "confusion" adaptations caused by hardware disconnects.

---

## 5. Test Suite & Validation Results

The deterministic test suite `python_tools/hardware/test_mock_esp32.py` tests all 11 required scenarios:

| Test ID | Scenario Description | Status | Latency / Metric |
| :--- | :--- | :---: | :---: |
| `test_01_connect` | Initial connection transition Disconnected -> Connected | **PASS** | State verified |
| `test_02_successful_command` | Send Ping command and receive Success ACK | **PASS** | Latency 5–25 ms |
| `test_03_led1_command` | SetLED on Led1 (Pin 13) changes Led1 only | **PASS** | Led1=On, Led2=Off |
| `test_04_led2_command` | SetLED, BlinkLED, and ResetCircuit on Led2 | **PASS** | Led2 verified |
| `test_05_ack_handling` | ACK callback invocation with matching command ID | **PASS** | Exact ID match |
| `test_06_timeout_handling` | Heartbeat silence > 3.0s triggers Timeout state | **PASS** | Timeout transition |
| `test_07_disconnect` | Graceful disconnection turns off outputs | **PASS** | LEDs turned Off |
| `test_08_reconnect` | Automatic recovery re-establishes Connected state | **PASS** | Connected |
| `test_09_failed_recovery` | Exceeded retry limit transitions to Error | **PASS** | Error state verified |
| `test_10_invalid_command` | Injected malformed command returns InvalidCommand ACK | **PASS** | Status verified |
| `test_11_session_logging` | Hardware command and state records in SessionLogger | **PASS** | Schema verified |

**Overall Pass Rate: 11 / 11 (100.0%)**

---

## 6. Research Integrity & Real Hardware Boundary

> [!IMPORTANT]
> **RESEARCH INTEGRITY NOTICE**:
> - All tests in Phase 9A were conducted using the software-only `MockESP32Bridge` (`is_mock: true`).
> - No claims are made regarding physical wireless signal propagation, physical breadboard wire impedance, or real ESP32 USB COM port stability.
> - **Provisional Pin Assignments**: GPIO 13 / GPIO 12 are provisional software-test pin assignments. Final physical GPIO mapping will be confirmed against the actual ESP32 board model during Phase 9B.
> - **Transport Selection**: Phase 9B will select and implement the appropriate A³R ESP32 transport after the actual ESP32 board model and available connectivity options are confirmed. The corresponding A³R ESP32 firmware will then be implemented for the selected transport.
> - Physical hardware validation is formally deferred to **Phase 9B**, to be conducted when physical ESP32 and breadboard hardware are physically attached.
