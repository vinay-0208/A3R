# A³R Phase 10 — UI/UX & Dynamic AR Presentation Layer
> **Phase Status**: PASS — SOFTWARE UI/UX INTEGRATION VALIDATED OFFLINE / FROZEN  
> **Date**: 2026-09-15 / 2026-09-16  
> **Scope**: Research Demonstration UI, Step Progress, AR Instruction Area, Adaptation Feedback Banner, Hardware Connection Badge, Research Mode Toggle, Task Completion Screen, and 3D Visual Cues.

---

## 1. Executive Summary & Objective

Phase 10 implements the complete user experience, AR presentation layer, and research visualization system for A³R. The primary objective was to transition the application from a raw engineering prototype into a clean, intuitive, and scientifically rigorous research demonstration platform.

Key enhancements delivered:
1. **Research Mode vs. Learner Mode**: A toggleable presentation architecture that allows learners to experience a distraction-free instructional environment while allowing researchers to observe real-time affect scores, predictive states, and marker telemetry.
2. **Hardware Connection Status Badge**: Real-time HUD badge providing immediate visual feedback on ESP32 communication status (`Connected [MOCK]`, `Connecting...`, `Disconnected`, `Timeout`, `Error`).
3. **Adaptation Feedback Banner / Toast**: A dedicated transient notification banner informing the learner whenever an adaptive intervention occurs (e.g., arrow cue injected, hint revealed, pacing slowed, step simplified).
4. **Task Completion Screen**: End-of-task summary screen detailing total assembly time, steps completed, and verification confirmation upon finishing Step 8.
5. **3D Visual Elements**: Integration of 3D instructional models (`arduino_uno.glb` — solely a visual asset representation, not physical hardware; `breadboard.glb`, `jumper_wire.glb`, `resistor.glb`) and dynamic AR arrow positioning pointing to target components.

All Unity C# scripts (`UIManager.cs`, `ARContentManager.cs`) compile cleanly with **0 errors and 0 warnings** in Unity 6000.3.23f1 batchmode.

---

## 2. Canvas UI Architecture (`UIManager.cs`)

The UI system is managed by `UIManager.cs` which controls dedicated 2D canvas layers:

```
+-------------------------------------------------------------------------+
|                              A³R Canvas HUD                             |
+-------------------------------------------------------------------------+
| [HW: CONNECTED [MOCK]]                       [Research Mode: ON / OFF]  |
|                                                                         |
|                          Step 3 / 8 [ProgressBar]                       |
|                       "Insert the LED (Polarity)"                       |
|             "Insert the LED into breadboard with CORRECT..."             |
|                                                                         |
|  +-------------------------------------------------------------------+  |
|  | [TOAST] Visual Cue: Highlight arrow placed on target component.   |  |
|  +-------------------------------------------------------------------+  |
|                                                                         |
|  +-------------------------------------------------------------------+  |
|  | [HINT CARD] Polarity matters here. Long leg = positive = row 14   |  |
|  +-------------------------------------------------------------------+  |
|                                                                         |
|  [RESEARCH HUD: Conf: 0.42 | Frust: 0.18 | Eng: 0.71 | Pred: NOMINAL]   |
|                                                                         |
|  [< Previous Step]                                     [Next Step >]    |
+-------------------------------------------------------------------------+
```

### Key UI Features
- **Progress Tracking**: Shows `Step X / 8` and drives a normalized `progressSlider` (0.0 to 1.0). Prev button disabled on Step 1; Next button triggers completion screen on Step 8.
- **Contextual Hint Panel**: Auto-collapsible card injected dynamically on `InsertHint`. Features manual dismiss and optional auto-dismiss timers.
- **Adaptation Feedback Banner**: Displays a floating banner when interventions fire, providing clear cognitive feedback without obscuring instructional text.
- **Hardware Connection Badge**: Dynamically color-coded:
  - Green: `HW: CONNECTED [MOCK]`
  - Yellow: `HW: CONNECTING...` or `HW: RECOVERING...`
  - Soft Red: `HW: TIMEOUT`
  - Red: `HW: ERROR`
  - Gray: `HW: DISCONNECTED`
- **Research Mode Toggle**: Hides the raw debug metrics by default for learner evaluations, but enables researchers to monitor underlying cognitive state and MediaPipe tracking telemetry on demand.
- **Task Completion Screen**: Replaces instructional panels upon finishing Step 8 (`verify_blink`), presenting total session elapsed time and circuit verification status.

---

## 3. AR Content Manager Integration (`ARContentManager.cs`)

`ARContentManager.cs` orchestrates the instructional task (`led_blink_task.json`) and synchronizes with hardware:
1. **Step Transitions**:
   - Updates `UIManager.ShowStep(...)`.
   - Sends `HardwareCommandType.StepSync` to the hardware bridge.
   - Clears transient adaptation overlays.
2. **Step 8 Circuit Verification**:
   - Upon entering Step 8 (`verify_blink`), automatically dispatches `BlinkLED(LedIdentifier.Led1, 1000)` to the hardware bridge, activating physical/mock LED blinking for circuit verification.
3. **Adaptation Triggers**:
   - `InjectCue`: Activates the 3D arrow (`arCueArrow`) anchored over the target breadboard row/column and shows the banner.
   - `InsertHint`: Populates contextual guidance in the UI hint panel.
   - `SlowPace`: Expands detailed technical notes and illuminates the secondary alert LED (`Led2`, provisional software-test assignment: GPIO 12).
   - `SimplifyStep`: Rewrites instructions into concrete sub-steps.
4. **Task Completion**:
   - Fired when completing Step 8.
   - Displays `UIManager.ShowCompletionScreen(...)`.
   - Dispatches `HardwareCommandType.ResetCircuit` to safely power down hardware outputs.

---

## 4. Compilation & Verification

The updated scripts were verified via Unity batchmode compilation:
```
Unity Command:
& 'C:\Program Files\UnityHub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -quit -projectPath ... -logFile unity_compile_check_phase10.log
Result: Exit Code 0 (0 errors, 0 warnings).
```

> [!IMPORTANT]
> **RESEARCH INTEGRITY NOTICE**:
> - Visual cues, badges, and completion screens were verified in Unity Editor batchmode and automated simulation test suites.
> - **Physical Visual/Touch Validation Deferred**: Physical Android visual and touch validation on the OnePlus 13R has NOT yet occurred and is formally deferred until physical device testing.
> - **Provisional Pin Assignments**: GPIO 13 / GPIO 12 are provisional software-test pin assignments. Final physical GPIO mapping will be confirmed against the actual ESP32 board model during Phase 9B.

---

## 5. AR Guidance Visual System (`ARGuidanceController.cs`)

Phase 10 includes a dedicated procedural 3D visual guidance subsystem that eliminates external asset dependencies while providing smooth, jitter-free AR instructional cues:

### Component Architecture
1. **`A3R_DirectionalArrow`**: Procedural 3D arrow mesh (cylinder shaft + conical head; 64 vertices, 1 draw call). Features `Quaternion.Slerp` rotational damping (`smoothSpeed = 8.0f`) to smoothly track the active component without camera jitter.
2. **`A3R_TargetRing`**: Procedural flat annulus ring mesh (32 segments, 64 vertices, 1 draw call). Anchors above target pins/components with a subtle breathing pulse (2.5 Hz, ±8% scale) and billboard orientation.
3. **`ARGuidanceController`**: Central coordinator that synchronizes step targets, adaptation actions, and AR tracking state. Pre-instantiates and caches all visual components at initialization, ensuring zero steady-state runtime garbage collection allocations.

### Step Target Spatial Mapping
| Step | ID | Target Anchor | Offset relative to Marker (X, Y, Z) | Visual Elements | Provisional GPIO Notice |
| :---: | :--- | :--- | :--- | :--- | :--- |
| 1 | `place_breadboard` | `breadboard_center` | `(0.12, 0.005, 0.00)` | Ring (Cyan), Arrow | Provisional software-test GPIO assignment. |
| 2 | `insert_resistor` | `resistor_slot_row10_colE` | `(0.12, 0.012, 0.02)` | Ring (Yellow), Arrow | Provisional software-test GPIO assignment. |
| 3 | `insert_led` | `led_slot_row14_colD` | `(0.12, 0.015, -0.01)` | Ring (Red), Arrow | Provisional software-test GPIO assignment. |
| 4 | `wire_resistor_to_pin13` | `board_gpio13` | `(0.24, 0.015, 0.03)` | Ring (Green), Arrow | Provisional software-test GPIO assignment: GPIO 13 is a provisional software-test assignment only. Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available. |
| 5 | `wire_gnd` | `board_gnd` | `(0.24, 0.015, 0.01)` | Ring (Blue), Arrow | Provisional software-test GPIO assignment. Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available. |
| 6 | `connect_usb` | `board_usb_port` | `(0.24, 0.015, -0.05)` | Ring (White), Arrow | Provisional software-test GPIO assignment. Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available. |
| 7 | `upload_blink_sketch` | *(IDE/Screen step)* | `(0.00, 0.000, 0.00)` | *Hidden* | Provisional software-test GPIO assignment. |
| 8 | `verify_blink` | `led_position` | `(0.12, 0.020, -0.01)` | Ring (Yellow), Arrow | Provisional software-test GPIO assignment: GPIO 13 is a provisional software-test assignment only (LED1 on provisional GPIO 13). Final ESP32 GPIO mapping will be frozen after the actual ESP32 board model/pinout is available. |

### Adaptive Visual Presentations
- **Normal**: Standard pulse (2.5 Hz), scale $1.0\times$, target aligned.
- **`InjectCue`**: Arrow scale increased to $1.30\times$, ring enlarged to $1.35\times$ with intensified pulsing ($3.75\text{ Hz}$).
- **`InsertHint`**: Target anchor emphasized ($1.15\times$ scale, $3.0\text{ Hz}$ pulse).
- **`SlowPace`**: Visual pulse slowed to relaxed breathing frequency ($1.5\text{ Hz}$), rotation and position damping halved to give learner more cognitive processing time.
- **`SimplifyStep`**: Target isolated with steady focus ($1.20\times$ scale), nonessential visuals concealed.

### Tracking Loss & Safety
Upon marker loss (`OnMarkerLost`), all target guidance visuals are immediately suppressed (`gameObject.SetActive(false)`), preventing floating phantom arrows. Guidance is automatically restored upon valid tracking recovery (`OnMarkerDetected`).
