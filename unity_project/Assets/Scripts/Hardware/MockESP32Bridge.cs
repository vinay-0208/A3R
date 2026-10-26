using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using A3R.Logging;
using A3R.Affect;

namespace A3R.Hardware
{
    /// <summary>
    /// MockESP32Bridge — Software-only mock implementation of IHardwareBridge.
    /// Provides deterministic simulated hardware responses for the two-LED physical task:
    ///   - Connect/Disconnect lifecycle simulation
    ///   - Simulated command latencies (5–25ms)
    ///   - Timeout & heartbeat keepalive
    ///   - Automatic & configurable recovery attempts
    ///   - Fault injection hooks for automated testing
    ///   - Explicitly marked as isMock = true
    ///   - Seamless SessionLogger and AffectPredictor integration
    /// </summary>
    public class MockESP32Bridge : MonoBehaviour, IHardwareBridge
    {
        // ── Inspector Configuration ─────────────────────────────────────────────
        [Header("Mock Hardware Settings")]
        [Tooltip("Simulated device address / port name.")]
        [SerializeField] private string mockDeviceAddress = "ESP32_MOCK_DEV_01";

        [Tooltip("Artificial connection delay in seconds.")]
        [SerializeField] private float connectDelaySeconds = 0.2f;

        [Tooltip("Simulated round-trip latency range in milliseconds.")]
        [SerializeField] private float minLatencyMs = 5.0f;
        [SerializeField] private float maxLatencyMs = 25.0f;

        [Header("Heartbeat & Timeout")]
        [Tooltip("Interval between heartbeat ping checks in seconds.")]
        [SerializeField] private float heartbeatInterval = 1.0f;

        [Tooltip("Timeout threshold in seconds before declaring a connection lost.")]
        [SerializeField] private float timeoutDuration = 3.0f;

        [Tooltip("Maximum reconnection recovery attempts before declaring Error.")]
        [SerializeField] private int maxRecoveryAttempts = 3;

        [Header("External References")]
        [SerializeField] private SessionLogger sessionLogger;
        [SerializeField] private AffectPredictor affectPredictor;

        // ── IHardwareBridge Properties ──────────────────────────────────────────
        public HardwareConnectionState CurrentState { get; private set; } = HardwareConnectionState.Disconnected;
        public bool IsConnected => CurrentState == HardwareConnectionState.Connected;
        public bool IsMock => true;
        public string DeviceAddress => mockDeviceAddress;
        public float LastHeartbeatTime { get; private set; } = 0f;
        public LedState Led1State { get; private set; } = LedState.Off;
        public LedState Led2State { get; private set; } = LedState.Off;

        // ── Events ───────────────────────────────────────────────────────────────
        public event Action<HardwareConnectionState, HardwareConnectionState> OnConnectionStateChanged;
        public event Action<HardwareCommand, CommandAck> OnCommandAckReceived;
#pragma warning disable CS0067
        public event Action<HardwareEvent> OnHardwareEventReceived;
#pragma warning restore CS0067
        public event Action<string> OnHardwareError;

        public void TriggerHardwareEvent(HardwareEvent evt) => OnHardwareEventReceived?.Invoke(evt);

        // ── Private State ────────────────────────────────────────────────────────
        private float _lastPingTime = 0f;
        private int _recoveryAttempts = 0;
        private bool _isRecovering = false;

        // ── Fault Injection Flags (for automated deterministic testing) ───────────
        private bool _forceNextCommandDrop = false;
        private bool _forceNextCommandTimeout = false;
        private bool _forceNextCommandInvalid = false;
        private bool _forceRecoveryFailure = false;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (sessionLogger == null)
                sessionLogger = FindFirstObjectByType<SessionLogger>();
            if (affectPredictor == null)
                affectPredictor = FindFirstObjectByType<AffectPredictor>();
        }

        private void Start()
        {
            LastHeartbeatTime = Time.time;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // ── IHardwareBridge API ──────────────────────────────────────────────────
        public void Connect(string address = "")
        {
            if (CurrentState == HardwareConnectionState.Connected || CurrentState == HardwareConnectionState.Connecting)
                return;

            if (!string.IsNullOrEmpty(address))
                mockDeviceAddress = address;

            SetState(HardwareConnectionState.Connecting);
            StartCoroutine(SimulateConnectRoutine());
        }

        public void Disconnect()
        {
            if (CurrentState == HardwareConnectionState.Disconnected)
                return;

            StopAllCoroutines();
            ResetCircuit();
            SetState(HardwareConnectionState.Disconnected);

            // Notify AffectPredictor of technical disruption if disconnected unexpectedly while active
            affectPredictor?.NotifyTechnicalDisruption("HardwareDisconnection");
        }

        public void SendCommand(HardwareCommand command)
        {
            if (command == null)
            {
                OnHardwareError?.Invoke("Cannot dispatch null HardwareCommand");
                return;
            }

            command.timestamp = Time.time;

            if (CurrentState != HardwareConnectionState.Connected)
            {
                var nack = new CommandAck(command.commandId, AckStatus.Failure, 0f,
                    $"Cannot execute command while in state {CurrentState}", Time.time);
                OnCommandAckReceived?.Invoke(command, nack);
                sessionLogger?.LogHardwareCommand(command, nack, isMock: true);
                return;
            }

            StartCoroutine(ExecuteCommandRoutine(command));
        }

        public void ResetCircuit()
        {
            Led1State = LedState.Off;
            Led2State = LedState.Off;
            SendCommand(new HardwareCommand(HardwareCommandType.ResetCircuit, LedIdentifier.Both, LedState.Off));
        }

        public void SetLED(LedIdentifier led, LedState state)
        {
            SendCommand(new HardwareCommand(HardwareCommandType.SetLED, led, state));
        }

        public void BlinkLED(LedIdentifier led, int rateMs = 1000)
        {
            SendCommand(new HardwareCommand(HardwareCommandType.BlinkLED, led, LedState.Blinking, rateMs));
        }

        public void Tick(float deltaTime)
        {
            if (CurrentState == HardwareConnectionState.Connected)
            {
                // Heartbeat keepalive check
                if (Time.time - _lastPingTime >= heartbeatInterval)
                {
                    _lastPingTime = Time.time;
                    LastHeartbeatTime = Time.time;
                }

                // Timeout detection
                if (Time.time - LastHeartbeatTime > timeoutDuration)
                {
                    Debug.LogWarning($"[MockESP32Bridge] Heartbeat timeout! Last seen {Time.time - LastHeartbeatTime:F2}s ago.");
                    SetState(HardwareConnectionState.Timeout);
                    StartRecovery();
                }
            }
        }

        // ── Fault Injection Test Hooks ───────────────────────────────────────────
        public void InjectDropNextCommand() => _forceNextCommandDrop = true;
        public void InjectTimeoutNextCommand() => _forceNextCommandTimeout = true;
        public void InjectInvalidNextCommand() => _forceNextCommandInvalid = true;
        public void InjectRecoveryFailure(bool fail) => _forceRecoveryFailure = fail;
        public void ForceTimeoutTrigger()
        {
            LastHeartbeatTime = Time.time - (timeoutDuration + 1f);
        }

        // ── Private Helpers & Coroutines ─────────────────────────────────────────
        private IEnumerator SimulateConnectRoutine()
        {
            yield return new WaitForSeconds(connectDelaySeconds);

            _recoveryAttempts = 0;
            _isRecovering = false;
            LastHeartbeatTime = Time.time;
            _lastPingTime = Time.time;

            SetState(HardwareConnectionState.Connected);
            Debug.Log($"[MockESP32Bridge] Successfully connected to {mockDeviceAddress} (MOCK)");
        }

        private IEnumerator ExecuteCommandRoutine(HardwareCommand cmd)
        {
            // Check fault injection: command drop
            if (_forceNextCommandDrop)
            {
                _forceNextCommandDrop = false;
                Debug.LogWarning($"[MockESP32Bridge] Simulating packet drop for command {cmd.commandId}");
                yield break;
            }

            // Check fault injection: timeout
            if (_forceNextCommandTimeout)
            {
                _forceNextCommandTimeout = false;
                yield return new WaitForSeconds(timeoutDuration + 0.1f);
                var timeoutAck = new CommandAck(cmd.commandId, AckStatus.Timeout, timeoutDuration * 1000f, "Simulated command timeout", Time.time);
                OnCommandAckReceived?.Invoke(cmd, timeoutAck);
                sessionLogger?.LogHardwareCommand(cmd, timeoutAck, isMock: true);
                yield break;
            }

            // Simulate realistic microsecond/millisecond transmission latency
            float simLatencyMs = UnityEngine.Random.Range(minLatencyMs, maxLatencyMs);
            yield return new WaitForSeconds(simLatencyMs / 1000f);

            // Check fault injection: invalid command
            if (_forceNextCommandInvalid)
            {
                _forceNextCommandInvalid = false;
                var invalidAck = new CommandAck(cmd.commandId, AckStatus.InvalidCommand, simLatencyMs, "Simulated invalid command syntax", Time.time);
                OnCommandAckReceived?.Invoke(cmd, invalidAck);
                sessionLogger?.LogHardwareCommand(cmd, invalidAck, isMock: true);
                yield break;
            }

            // Process command state
            switch (cmd.type)
            {
                case HardwareCommandType.SetLED:
                    if (cmd.targetLed == LedIdentifier.Led1 || cmd.targetLed == LedIdentifier.Both)
                        Led1State = cmd.state;
                    if (cmd.targetLed == LedIdentifier.Led2 || cmd.targetLed == LedIdentifier.Both)
                        Led2State = cmd.state;
                    break;

                case HardwareCommandType.BlinkLED:
                    if (cmd.targetLed == LedIdentifier.Led1 || cmd.targetLed == LedIdentifier.Both)
                        Led1State = LedState.Blinking;
                    if (cmd.targetLed == LedIdentifier.Led2 || cmd.targetLed == LedIdentifier.Both)
                        Led2State = LedState.Blinking;
                    break;

                case HardwareCommandType.ResetCircuit:
                    Led1State = LedState.Off;
                    Led2State = LedState.Off;
                    break;

                case HardwareCommandType.Ping:
                    LastHeartbeatTime = Time.time;
                    break;

                case HardwareCommandType.StepSync:
                    // Acknowledge step advancement sync
                    break;
            }

            LastHeartbeatTime = Time.time;

            var ack = new CommandAck(cmd.commandId, AckStatus.Success, simLatencyMs, $"OK - {cmd.type}", Time.time);
            OnCommandAckReceived?.Invoke(cmd, ack);
            sessionLogger?.LogHardwareCommand(cmd, ack, isMock: true);
        }

        private void StartRecovery()
        {
            if (_isRecovering) return;
            _isRecovering = true;
            SetState(HardwareConnectionState.Recovering);

            // Inform affect predictor of technical disruption during recovery
            affectPredictor?.NotifyTechnicalDisruption("HardwareDisconnection");

            StartCoroutine(RecoveryRoutine());
        }

        private IEnumerator RecoveryRoutine()
        {
            while (_recoveryAttempts < maxRecoveryAttempts)
            {
                _recoveryAttempts++;
                Debug.Log($"[MockESP32Bridge] Recovery attempt {_recoveryAttempts}/{maxRecoveryAttempts}...");

                yield return new WaitForSeconds(connectDelaySeconds * 1.5f);

                if (_forceRecoveryFailure)
                {
                    Debug.LogWarning($"[MockESP32Bridge] Recovery attempt {_recoveryAttempts} failed (simulated).");
                    continue;
                }

                // Successful recovery
                _isRecovering = false;
                LastHeartbeatTime = Time.time;
                _lastPingTime = Time.time;
                _recoveryAttempts = 0;
                SetState(HardwareConnectionState.Connected);
                Debug.Log("[MockESP32Bridge] Connection recovered successfully!");
                yield break;
            }

            // All recovery attempts exhausted
            _isRecovering = false;
            SetState(HardwareConnectionState.Error);
            OnHardwareError?.Invoke($"Connection lost. Failed to recover after {maxRecoveryAttempts} attempts.");
        }

        private void SetState(HardwareConnectionState newState)
        {
            if (CurrentState == newState) return;

            var oldState = CurrentState;
            CurrentState = newState;
            Debug.Log($"[MockESP32Bridge] State changed: {oldState} -> {newState} (MOCK)");

            OnConnectionStateChanged?.Invoke(oldState, newState);
            sessionLogger?.LogHardwareStateChange(oldState.ToString(), newState.ToString(), isMock: true);
        }
    }
}
