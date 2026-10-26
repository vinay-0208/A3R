using System;

namespace A3R.Hardware
{
    /// <summary>
    /// IHardwareBridge — Transport-agnostic Hardware Abstraction Layer (HAL).
    /// Decouples higher-level AR components (ARContentManager, SessionLogger, AffectPredictor)
    /// from the underlying communication channel (Mock, Serial USB, BLE, or WiFi).
    /// </summary>
    public interface IHardwareBridge
    {
        /// <summary>Current state of the hardware connection lifecycle.</summary>
        HardwareConnectionState CurrentState { get; }

        /// <summary>True only when connection is fully established and ready for commands.</summary>
        bool IsConnected { get; }

        /// <summary>True if running a simulated/mock hardware bridge.</summary>
        bool IsMock { get; }

        /// <summary>Identifier or address of the target device (COM port, MAC address, IP, etc.).</summary>
        string DeviceAddress { get; }

        /// <summary>Timestamp of last verified communication (heartbeat/ack).</summary>
        float LastHeartbeatTime { get; }

        /// <summary>Current state of LED 1 (circuit LED; provisional software-test assignment: GPIO 13).</summary>
        LedState Led1State { get; }

        /// <summary>Current state of LED 2 (status LED; provisional software-test assignment: GPIO 12).</summary>
        LedState Led2State { get; }

        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>Fired whenever connection state transitions (oldState, newState).</summary>
        event Action<HardwareConnectionState, HardwareConnectionState> OnConnectionStateChanged;

        /// <summary>Fired when a command acknowledgement is processed (command, ack).</summary>
        event Action<HardwareCommand, CommandAck> OnCommandAckReceived;

        /// <summary>Fired on unsolicited hardware event from device.</summary>
        event Action<HardwareEvent> OnHardwareEventReceived;

        /// <summary>Fired when a fatal or non-fatal communication error occurs.</summary>
        event Action<string> OnHardwareError;

        // ── Lifecycle & Control API ──────────────────────────────────────────────
        /// <summary>Initiate connection sequence to the target device.</summary>
        void Connect(string address = "");

        /// <summary>Gracefully disconnect from the hardware device.</summary>
        void Disconnect();

        /// <summary>Send a structured command to the hardware bridge.</summary>
        void SendCommand(HardwareCommand command);

        /// <summary>Reset circuit outputs (both LEDs off, clear state).</summary>
        void ResetCircuit();

        /// <summary>Set specific LED state.</summary>
        void SetLED(LedIdentifier led, LedState state);

        /// <summary>Set specific LED to blink at specified interval in milliseconds.</summary>
        void BlinkLED(LedIdentifier led, int rateMs = 1000);

        /// <summary>Periodic tick method for heartbeat, timeout detection, and recovery.</summary>
        void Tick(float deltaTime);
    }
}
