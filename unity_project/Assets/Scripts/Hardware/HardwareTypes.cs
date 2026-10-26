using System;

namespace A3R.Hardware
{
    /// <summary>
    /// Connection state lifecycle for hardware bridge.
    /// Matches Phase 9A protocol specification.
    /// </summary>
    public enum HardwareConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Programming,
        Running,
        Success,
        Recovering,
        Timeout,
        Error
    }

    /// <summary>
    /// Supported command types for the two-LED circuit task.
    /// </summary>
    public enum HardwareCommandType
    {
        Ping,           // Heartbeat check
        SetLED,         // Turn LED On/Off
        BlinkLED,       // Set LED to blink at interval
        ResetCircuit,   // Turn all LEDs off / reset baseline
        StepSync        // Inform hardware of current AR assembly step
    }

    /// <summary>
    /// Two-LED task physical identifiers.
    /// Note: GPIO 13 / GPIO 12 are provisional software-test pin assignments.
    /// Final physical GPIO mapping will be confirmed against the actual ESP32 board model during Phase 9B.
    /// Led1: Circuit assembly target (provisional GPIO 13).
    /// Led2: Secondary status / fault indicator (provisional GPIO 12).
    /// </summary>
    public enum LedIdentifier
    {
        Led1,
        Led2,
        Both
    }

    /// <summary>
    /// Target state for an LED component.
    /// </summary>
    public enum LedState
    {
        Off,
        On,
        Blinking
    }

    /// <summary>
    /// Command acknowledgement status codes.
    /// </summary>
    public enum AckStatus
    {
        Success,
        Failure,
        Timeout,
        InvalidCommand,
        Busy
    }

    /// <summary>
    /// Structured hardware command dispatched from Unity AR to ESP32 bridge.
    /// </summary>
    [Serializable]
    public class HardwareCommand
    {
        public string commandId;
        public HardwareCommandType type;
        public LedIdentifier targetLed;
        public LedState state;
        public int blinkRateMs;
        public float timestamp;
        public string payload;

        public HardwareCommand()
        {
            commandId = Guid.NewGuid().ToString("N").Substring(0, 8);
            type = HardwareCommandType.Ping;
            targetLed = LedIdentifier.Led1;
            state = LedState.Off;
            blinkRateMs = 1000;
            timestamp = 0f;
            payload = "";
        }

        public HardwareCommand(HardwareCommandType cmdType, LedIdentifier led = LedIdentifier.Led1, LedState targetState = LedState.Off, int blinkMs = 1000, string data = "")
        {
            commandId = Guid.NewGuid().ToString("N").Substring(0, 8);
            type = cmdType;
            targetLed = led;
            state = targetState;
            blinkRateMs = blinkMs;
            timestamp = 0f;
            payload = data;
        }

        public override string ToString()
        {
            return $"[Command {commandId}: {type} target={targetLed} state={state} blink={blinkRateMs}ms]";
        }
    }

    /// <summary>
    /// Structured acknowledgement returned from ESP32 bridge to Unity AR.
    /// </summary>
    [Serializable]
    public class CommandAck
    {
        public string commandId;
        public AckStatus status;
        public float latencyMs;
        public string message;
        public float timestamp;

        public CommandAck()
        {
            commandId = "";
            status = AckStatus.Failure;
            latencyMs = 0f;
            message = "";
            timestamp = 0f;
        }

        public CommandAck(string cmdId, AckStatus ackStatus, float latency, string msg = "", float ts = 0f)
        {
            commandId = cmdId;
            status = ackStatus;
            latencyMs = latency;
            message = msg;
            timestamp = ts;
        }

        public override string ToString()
        {
            return $"[ACK {commandId}: {status} in {latencyMs:F1}ms - {message}]";
        }
    }

    /// <summary>
    /// Unsolicited hardware event originating from ESP32 (e.g. sensor probe, circuit short/open).
    /// </summary>
    [Serializable]
    public class HardwareEvent
    {
        public string eventType;
        public string data;
        public float timestamp;
        public bool isMock;

        public HardwareEvent(string type, string payload, float time, bool mock = true)
        {
            eventType = type;
            data = payload;
            timestamp = time;
            isMock = mock;
        }

        public override string ToString()
        {
            return $"[HardwareEvent {eventType} at {timestamp:F2}s: {data} (isMock={isMock})]";
        }
    }
}
