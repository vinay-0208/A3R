# A?R ? Offline AR Guidance Visual Preview Documentation

**Document ID:** `A3R-DOC-PREVIEW-01`  
**Version:** 1.0  
**Date:** September 16, 2026  
**Status:** Certified & Verified (12/12 Tests Passing)  
**Target Scene:** `Assets/Scenes/A3R_AR_Guidance_Preview.unity`  
**Primary Controller:** `Assets/Scripts/ARGuidance/A3R_ARGuidancePreviewController.cs`  

---

## 1. Executive Summary & Objective

The **Offline AR Guidance Visual Preview** provides a dedicated, interactive, standalone Unity environment allowing researchers and developers to visually inspect and validate the complete **A?R AR Guidance System** directly inside the Unity Editor on a standard desktop/laptop without requiring physical mobile or microcontroller hardware.

### Operational Boundaries (Strict Negative Declarations)
- **No Physical Hardware Required:** Runs without the OnePlus 13R phone, physical ESP32 microcontroller, breadboard, physical LEDs, or USB cables.
- **No Physical AR Tracking Required:** Does not require live ARCore camera tracking or physical marker cards. Marker detection and tracking stability are simulated deterministically in-engine.
- **No Hardware Transport Required:** No BLE, physical Serial, USB, Wi-Fi, or Firmata transport layers are executed or simulated.
- **No Participant Claims:** This is a software visual inspection and automated unit verification tool; it does NOT constitute human participant validation.
- **Zero Modification of Frozen Logic:** Frozen affect formulas (Confusion, Frustration, Engagement), runtime thresholds (0.60, 0.65, 3.0s dwell, 15.0s cooldown, max 3 adaptations), linear trend predictor ($K=5$, $\Delta t \ge 0.4\text{s}$, persistence 2, cooldown 5.0s, floor $<0.30$), and adaptation policy ladder are 100% preserved without modification.
- **Provisional Pinout Retained:** GPIO 13 (LED1) and GPIO 12 (LED2) remain documented and treated strictly as provisional software-test assignments.

---

## 2. Architecture & Scene Hierarchy

The preview environment is encapsulated in `Assets/Scenes/A3R_AR_Guidance_Preview.unity`, structured cleanly into specialized GameObjects:

```
A3R_AR_Guidance_Preview.unity
??? Main Camera (Perspective, 50? FOV, Isometric 3/4 Workbench View)
??? Directional Light (Soft Warm Shadows, 1.2 Intensity)
??? EventSystem (InputSystemUIInputModule with DefaultInputActions)
??? A3R_TabletopContentRoot (Simulated Physical Workbench Surface)
?   ??? TableSurface (Neutral Dark Slate Workbench Material)
?   ??? A3R_MarkerCard (Simulated AR Tracking Reference Card)
?   ??? Breadboard (Half-size breadboard with center divider channel)
?   ??? ESP32_Board_Placeholder (PCB slab, MCU chip, pin headers, USB port)
?   ?   ??? ESP32_Label_3D ("ESP32 / BOARD PLACEHOLDER" TextMeshPro 3D Label)
?   ??? Resistor_220 (220? resistor bridging Rows 10 and 14)
?   ??? LED_Red_5mm (5mm Red LED dome across Rows 14 and 15)
?   ??? Wire_GPIO13_Row10 (Green jumper wire: provisional GPIO 13 to Row 10 Col A)
?   ??? Wire_GND_Row15 (Blue jumper wire: board GND to Row 15 Col A)
?   ??? USB_Cable (Black USB cable representation connected to board port)
?   ??? breadboard_center (Target Anchor: (0.12, 0.005, 0.00))
?   ??? resistor_slot_row10_colE (Target Anchor: (0.12, 0.012, 0.02))
?   ??? led_slot_row14_colD (Target Anchor: (0.12, 0.015, -0.01))
?   ??? board_gpio13 (Target Anchor: (0.24, 0.015, 0.03) [Provisional])
?   ??? board_gnd (Target Anchor: (0.24, 0.015, 0.01))
?   ??? board_usb_port (Target Anchor: (0.24, 0.015, -0.05))
?   ??? led_position (Target Anchor: (0.12, 0.020, -0.01))
??? ARGuidanceManager (Hosts ARGuidanceController & A3R_ARGuidancePreviewController)
?   ??? A3R_DirectionalArrow (Procedural mesh 3D arrow pointer)
?   ??? A3R_TargetRing (Procedural mesh animated target ring)
?   ??? A3R_HighlightAnchor (Fallback target positioning anchor)
??? Canvas_PreviewHUD (Screen Space Overlay UI with Scaler & Raycaster)
    ??? ControlPanel (Left panel: navigation, adaptations, tracking toggle)
    ??? HUDPanel (Right panel: real-time telemetry, state, provisional note)
    ??? CompletionModal (Center modal toast on task completion)
```

---

## 3. AR Guidance Visual Components & Behaviors

### 3.1 Directional AR Arrow (`A3R_DirectionalArrow.cs`)
- **Mesh Generation:** Procedural 3D arrow composed of a cylindrical shaft ($r=0.005\text{m}$, $l=0.07\text{m}$) and conical head ($r=0.014\text{m}$, $l=0.03\text{m}$).
- **Elevation & Orientation:** Floats at an elevation offset of $+0.06\text{m}$ above the target, dynamically pointing towards the target anchor with smoothed spherical interpolation (`Slerp`, speed $8.0\text{s}^{-1}$).
- **Gentle Bobbing:** Sinusoidal bobbing ($\pm 4\text{mm}$, frequency $3.0\text{rad/s}$) providing visual liveliness without distracting jitter.
- **Tracking & State Response:** Immediately hides when tracking is lost or target is invalid; smoothly scales and shifts tint under adaptive interventions.

### 3.2 Target Ring (`A3R_TargetRing.cs`)
- **Mesh Generation:** Procedural planar annulus ($r_{\text{in}}=0.020\text{m}$, $r_{\text{out}}=0.035\text{m}$, 32 radial segments).
- **Planar Rotation & Pulse:** Continuously rotates at $20^\circ/\text{s}$ while pulsing radially with sinusoidal breathing amplitude ($0.08$) at base frequency $2.5\text{Hz}$.
- **Target Circling:** Centers directly around the active step's physical connection point on the breadboard or microcontroller board.

### 3.3 Target Isolation & Dimming (`SimplifyStep`)
When `SimplifyStep` is triggered:
- The active target component retains its bright distinctive color.
- All non-active background and distractor components (other wires, unrelated components, board) are dimmed to $35\%$ luminance and $45\%$ opacity.
- Directional arrow scales up by $+20\%$ and target ring adopts a steady, high-visibility state.
- Returning to `Normal` immediately restores all component materials to default appearance.

---

## 4. Visual Adaptation Matrix

The preview controller faithfully demonstrates all four adaptation actions defined in the frozen A?R adaptation ladder:

| Adaptation Action | Visual State | Arrow Behavior | Target Ring Behavior | Tabletop Focus Behavior |
|---|---|---|---|---|
| **Normal** (Baseline) | `GuidanceVisualState.Normal` | Scale $1.0\times$, step color tint, smooth speed $1.0\times$ | Scale $1.0\times$, pulse freq $2.5\text{Hz}$, step ring color | Default component materials |
| **InjectCue** | `GuidanceVisualState.ProminentCue` | Prominence scale **$1.30\times$**, $1.3\times$ brightness tint boost | Prominence scale **$1.35\times$**, pulse freq **$3.75\text{Hz}$** ($1.5\times$) | Default component materials |
| **InsertHint** | `GuidanceVisualState.HintEmphasized` | Prominence scale **$1.15\times$**, step tint | Prominence scale **$1.15\times$**, pulse freq **$3.00\text{Hz}$** ($1.2\times$) | Default component materials |
| **SlowPace** | `GuidanceVisualState.PacedSlow` | Smoothing speeds halved (**$0.5\times$**), calm motion | Pulse freq reduced to **$1.50\text{Hz}$** ($0.6\times$) | Default component materials |
| **SimplifyStep** | `GuidanceVisualState.IsolatedStep` | Prominence scale **$1.20\times$**, target focus | Prominence scale **$1.20\times$**, steady ring opacity | **Non-target components dimmed to 35% luminance** |

---

## 5. Step Progression & Spatial Target Mapping

All 8 canonical assembly steps from `Assets/StepData/led_blink_task.json` are fully supported:

| Step # | Step ID | Target Anchor Name | Target Coordinates $(x, y, z)$ | Arrow Active | Ring Active | Component Highlight |
|:---:|---|---|:---:|:---:|:---:|---|
| **0** | `place_breadboard` | `breadboard_center` | $(0.12, 0.005, 0.00)$ | Yes | Yes | Breadboard |
| **1** | `insert_resistor` | `resistor_slot_row10_colE` | $(0.12, 0.012, 0.02)$ | Yes | Yes | Resistor |
| **2** | `insert_led` | `led_slot_row14_colD` | $(0.12, 0.015, -0.01)$ | Yes | Yes | LED |
| **3** | `wire_resistor_to_pin13` | `board_gpio13` | $(0.24, 0.015, 0.03)$ | Yes | Yes | Jumper Wire / GPIO 13* |
| **4** | `wire_gnd` | `board_gnd` | $(0.24, 0.015, 0.01)$ | Yes | Yes | Jumper Wire / GND* |
| **5** | `connect_usb` | `board_usb_port` | $(0.24, 0.015, -0.05)$ | Yes | Yes | Development Board / USB* |
| **6** | `upload_blink_sketch` | *(Off-Marker IDE Step)* | $(0.00, 0.000, 0.00)$ | **No (Hidden)** | **No (Hidden)** | None (Off-marker IDE) |
| **7** | `verify_blink` | `led_position` | $(0.12, 0.020, -0.01)$ | Yes | Yes | LED |

*\* Note: GPIO 13 and GPIO 12 are provisional software-test assignments.*

---

## 6. Automated In-Engine Verification Results

Automated verification was performed in Unity 6.3 LTS batchmode via `A3R.EditorTools.PreviewSceneVerifier.RunVerification`. All 12 verification criteria passed with zero errors and zero warnings:

| Test ID | Test Description | Condition Tested | Result |
|---|---|---|:---:|
| `PREVIEW_01` | Scene Structure and Hierarchy | Camera, Light, EventSystem, TabletopRoot, GuidanceManager, Canvas present | **PASS** |
| `PREVIEW_02` | Tabletop Environment & Anchors | All 7 named spatial anchors exist, ESP32 placeholder labeled | **PASS** |
| `PREVIEW_03` | Step 0 Initial Guidance Display | Arrow & Ring active, targeting `breadboard_center` | **PASS** |
| `PREVIEW_04` | Step Progression (Steps 0?5) | Sequential transitions update target position and maintain active guidance | **PASS** |
| `PREVIEW_05` | Step 6 IDE Off-Marker Suppression | Arrow and Ring hidden for off-marker sketch upload | **PASS** |
| `PREVIEW_06` | InjectCue Visual Adaptation | State = `ProminentCue`, Arrow scale 1.30, Ring scale 1.35 & 1.5x pulse | **PASS** |
| `PREVIEW_07` | InsertHint Visual Adaptation | State = `HintEmphasized`, Arrow scale 1.15, Ring scale 1.15 & 1.2x pulse | **PASS** |
| `PREVIEW_08` | SlowPace Visual Adaptation | State = `PacedSlow`, smoothing speed halved (0.5x), pulse freq 0.6x | **PASS** |
| `PREVIEW_09` | SimplifyStep Focus Isolation | State = `IsolatedStep`, non-target components dimmed, target isolated | **PASS** |
| `PREVIEW_10` | Tracking Loss Visual Suppression | Arrow and Ring immediately deactivated upon tracking loss | **PASS** |
| `PREVIEW_11` | Tracking Recovery Visual Restoration | Arrow and Ring restored smoothly upon tracking recovery | **PASS** |
| `PREVIEW_12` | Task Completion Clean Clearance | Arrow and Ring cleanly hidden, Completion UI modal displayed | **PASS** |

**Summary Artifact:** `evaluation/offline_ar_guidance_preview/summary.json`  
**Overall Verdict:** `PASS ? OFFLINE AR GUIDANCE PREVIEW IMPLEMENTED & VERIFIED`

---

## 7. Operational Instructions for Unity Play Mode

1. Launch **Unity Hub** and open the project: `ARVR/unity_project`.
2. In the Project window, navigate to `Assets/Scenes/A3R_AR_Guidance_Preview.unity` and double-click to open.
3. Click the **Play** button at the top of the Unity Editor.
4. **Inspect Visuals:**
   - Observe the 3D tabletop environment, breadboard, labeled ESP32 placeholder, components, and wires.
   - Observe the yellow directional arrow hovering and pointing towards the first target (`breadboard_center`).
   - Observe the cyan target ring pulsing around the active target.
5. **Interactive Controls:**
   - Click **`Next Step ?`** to advance through each step and watch the arrow and ring glide smoothly to the next component.
   - Click **`InjectCue`**, **`InsertHint`**, **`SlowPace`**, or **`SimplifyStep`** to inspect the distinct visual adaptation responses.
   - Click **`Simulate Tracking Loss`** to verify that AR guidance vanishes cleanly, and click again to watch it recover smoothly.
   - Advance through Step 7 or click **`Complete Task`** to inspect the completion modal.
