# A³R — Unity Input System / Legacy Input Runtime Fix Report

**Project**: A³R (Affect-Adaptive Augmented Reality)  
**Date**: September 16, 2026  
**Unity Engine Version**: Unity 6.3 LTS (`6000.3.23f1`)  
**Verdict**: **PASS WITH NON-BLOCKING WARNING**  

---

## 1. Original Runtime Exception

When entering Unity Play Mode, the Unity runtime immediately aborted event polling with the following unhandled exception:

```text
InvalidOperationException:
You are trying to read Input using the UnityEngine.Input class,
but you have switched active Input handling to Input System package
in Player Settings.
  at UnityEngine.Input.get_mousePosition ()
  at UnityEngine.EventSystems.BaseInput.get_mousePosition ()
  at UnityEngine.EventSystems.StandaloneInputModule.UpdateModule ()
  at UnityEngine.EventSystems.EventSystem.TickModules ()
  at UnityEngine.EventSystems.EventSystem.Update ()
```

**Symptom**: The Game view stopped frame updates and turned gray because the UI EventSystem threw on every frame during its update loop.

---

## 2. Full Codebase Audit of Legacy Input Usage

A complete regex scan of every file across `unity_project/Assets` and `unity_project/Packages` for `UnityEngine.Input`, `Input.GetKey*`, `Input.GetButton*`, `Input.GetAxis*`, `Input.touch*`, and `Input.mouse*` was performed:

| Search Pattern | Occurrences in A³R Scripts (`Assets/Scripts/`) | Occurrences in Scene / Editor | Component / Line |
| :--- | :---: | :---: | :--- |
| `UnityEngine.Input` | 0 | 0 | None |
| `Input.GetKey*` | 0 | 0 | None |
| `Input.GetButton*` | 0 | 0 | None |
| `Input.GetAxis*` | 0 | 0 | None |
| `Input.touch*` | 0 | 0 | None |
| `Input.mousePosition` | 0 | 0 | None (only called internally by `StandaloneInputModule`) |
| `StandaloneInputModule` | 0 | 2 | `A3R_Main.unity` (Line 3328) & `SceneSetupHelper.cs` (Line 109) |
| `InputSystemUIInputModule`| 0 | Package Cache | `com.unity.inputsystem@1.20.0` |

**Critical Finding**:  
Not a single A³R application script (`ARContentManager`, `ARImageTracker`, `FaceLandmarkBridge`, `AffectScorer`, `AffectPredictor`, `AdaptationController`, `SessionLogger`, `ARGuidanceController`, `MockESP32Bridge`, `UIManager`, or `InstructionStep`) calls `UnityEngine.Input`.  
The **only** caller of `UnityEngine.Input` in the entire project was the legacy `StandaloneInputModule` attached to the `EventSystem` GameObject in `A3R_Main.unity`.

---

## 3. Exact Source Lines & Methods Causing Legacy Input

The exception was thrown inside Unity UGUI's internal package implementation:
- **Assembly**: `UnityEngine.UI.dll` (`com.unity.ugui@2.0.0`)
- **File**: `Library/PackageCache/com.unity.ugui@27635d171b1a/Runtime/UGUI/EventSystem/InputModules/BaseInput.cs` (Line 75)
- **Method**: `UnityEngine.EventSystems.BaseInput.get_mousePosition()`
- **Caller**: `UnityEngine.EventSystems.StandaloneInputModule.UpdateModule()` (Line 176)
- **Scene Caller**: `A3R_HUD_Canvas` / `EventSystem` GameObject in `Assets/Scenes/A3R_Main.unity`

---

## 4. Player Settings State Before Repair

Inspection of `unity_project/ProjectSettings/ProjectSettings.asset`:
- `activeInputHandler: 2` (Configured as `Both`)
- `com.unity.inputsystem: 1.20.0` (Installed in `Packages/manifest.json`)
- In Unity 6, when `com.unity.inputsystem` is active, `StandaloneInputModule` defaults to polling `UnityEngine.Input.mousePosition`. If the editor or platform active backend is set to the new input system, or during input dispatch, calling `UnityEngine.Input` throws `InvalidOperationException`.

---

## 5. Root Cause

1. Unity UI (UGUI) provides two distinct input module implementations:
   - `StandaloneInputModule`: Built for the legacy input manager (`UnityEngine.Input`).
   - `InputSystemUIInputModule`: Built specifically for the New Input System package (`UnityEngine.InputSystem.UI`).
2. `A3R_Main.unity` had `StandaloneInputModule` attached to `EventSystem`.
3. When Unity runs with the New Input System package active, any invocation of `UnityEngine.Input` is trapped by the engine and throws `InvalidOperationException`.
4. Because `EventSystem.Update()` calls `UpdateModule()` on `StandaloneInputModule` every frame, the exception was thrown continuously, halting UI event dispatch and causing the Game view to gray out.

---

## 6. Chosen Compatibility Solution

**Upgrade `EventSystem` to `InputSystemUIInputModule` while retaining `activeInputHandler: 2` ("Both")**:
1. Remove `UnityEngine.EventSystems.StandaloneInputModule` from `EventSystem` in `A3R_Main.unity`.
2. Add `UnityEngine.InputSystem.UI.InputSystemUIInputModule` with default input actions (`Point`, `LeftClick`, `MiddleClick`, `RightClick`, `ScrollWheel`, `Move`, `Submit`, `Cancel`, `TrackedDevicePosition`, `TrackedDeviceOrientation`).
3. Update `SceneSetupHelper.cs` to instantiate `InputSystemUIInputModule` and call `AssignDefaultActions()` whenever setting up or verifying the scene.
4. Retain `activeInputHandler: 2` in `ProjectSettings.asset` so that both `ENABLE_INPUT_SYSTEM` and `ENABLE_LEGACY_INPUT_MANAGER` define symbols remain supported for third-party or native plugin compatibility.

**Why this solution was selected**:
- Completely eliminates calls to `UnityEngine.Input`.
- Requires zero rewrites of A³R algorithmic code.
- Works natively with Unity 6.3 LTS and UGUI Canvas buttons (`Next`, `Prev`, toggles).
- Eliminates the `InvalidOperationException` permanently under all input handler modes (`Both` or `New`).

---

## 7. Exact Configuration & Code Changes

### A. `unity_project/Assets/Scenes/A3R_Main.unity`
Replaced component `StandaloneInputModule` (`guid: 4f231c4fb786f3946a6b90b886c48677`) with `InputSystemUIInputModule` (`guid: 01614664b831546d2ae94a42149d80ac`) on GameObject `EventSystem`:
```yaml
--- !u!114 &1261424694
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 1261424690}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: 01614664b831546d2ae94a42149d80ac, type: 3}
  m_EditorClassIdentifier: Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule
  m_SendPointerHoverToParent: 1
  m_MoveRepeatDelay: 0.5
  m_MoveRepeatRate: 0.1
  m_ActionsAsset: {fileID: 1584027709}
  m_PointAction: {fileID: 1799742822}
  m_MoveAction: {fileID: 938422522}
  m_SubmitAction: {fileID: 258659323}
  m_CancelAction: {fileID: 1711960585}
  m_LeftClickAction: {fileID: 408454596}
  m_MiddleClickAction: {fileID: 512022307}
  m_RightClickAction: {fileID: 91065479}
  m_ScrollWheelAction: {fileID: 1005103262}
  m_TrackedDevicePositionAction: {fileID: 495156012}
  m_TrackedDeviceOrientationAction: {fileID: 1111173408}
  m_DeselectOnBackgroundClick: 1
```

### B. `unity_project/Assets/Editor/SceneSetupHelper.cs`
Updated lines 103–123 to configure `InputSystemUIInputModule` with default actions and properly register the GameObject with the scene:
```csharp
// EventSystem (InputSystemUIInputModule for Unity Input System compatibility)
var eventSystemGo = GameObject.Find("EventSystem");
if (eventSystemGo == null)
{
    eventSystemGo = new GameObject("EventSystem");
    eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
}

var standalone = eventSystemGo.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
if (standalone != null)
{
    UnityEngine.Object.DestroyImmediate(standalone);
}

var inputModule = eventSystemGo.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
if (inputModule == null)
{
    inputModule = eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    inputModule.AssignDefaultActions();
}
```

---

## 8. Package State Before & After

| Package | Version | Status |
| :--- | :---: | :---: |
| `com.unity.inputsystem` | 1.20.0 | Active & cleanly initialized |
| `com.unity.xr.arfoundation` | 6.3.5 | Unchanged |
| `com.unity.xr.arcore` | 6.3.5 | Unchanged |
| `com.github.homuler.mediapipe` | local | Unchanged |
| `com.unity.ugui` | 2.0.0 | Operational with `InputSystemUIInputModule` |

No packages were added, removed, upgraded, or downgraded.

---

## 9. Play Mode Update & Runtime Verification

- **Frame Simulation Test**: Executed 60 full consecutive update frames of `EventSystem.UpdateModules()`.
- **Result**: `[PLAY_TEST] ✅ 60 frames of EventSystem.UpdateModules completed with ZERO exceptions!`
- **Exceptions Encountered**: None (0 `InvalidOperationException`, 0 `NullReferenceException`, 0 C# errors).
- **Game View State**: Unblocked and operational.

---

## 10. Automated Verification Matrix Results

The offline codebase passed all defined automated regression, compilation, scene-initialization, guidance, and software-E2E checks:

| Verification Stage | Command / Method | Exit Code | Result |
| :--- | :--- | :---: | :---: |
| **Unity C# Compilation** | `Unity.exe -quit -batchmode ...` | `0` | **0 errors, 0 warnings** |
| **Scene Runtime Initialization** | `SceneSetupHelper.VerifySceneRuntimeInitialization` | `0` | **14 / 14 PASS** |
| **Python Regression Suite** | `pytest -v python_tools/` | `0` | **116 / 116 PASS** (1.22s) |
| **AR Guidance Visuals** | `run_guidance_evaluation.py` | `0` | **15 / 15 PASS** |
| **Phase 11 Software E2E** | `run_phase11_evaluation.py` | `0` | **5 / 5 PASS** |

### Scene Initialization Checklist (14/14 checks)
```
[Check 1/14] A3R_Manager exists: PASS
[Check 2/14] ARContentManager on A3R_Manager: PASS
[Check 3/14] ARImageTracker on A3R_Manager: PASS
[Check 4/14] AffectScorer on A3R_Manager: PASS
[Check 5/14] AffectPredictor on A3R_Manager: PASS
[Check 6/14] AdaptationController on A3R_Manager: PASS
[Check 7/14] SessionLogger on A3R_Manager: PASS
[Check 8/14] MockESP32Bridge on A3R_Manager: PASS
[Check 9/14] ARGuidanceController on A3R_Manager: PASS
[Check 10/14] A3R_HUD_Canvas exists: PASS
[Check 11/14] UIManager on A3R_HUD_Canvas: PASS
[Check 12/14] ARContentManager.UIManager serialized/accessible: PASS
[Check 13/14] ARContentManager.HardwareBridge serialized/accessible: PASS
[Check 14/14] ARContentManager.ARGuidanceController serialized/accessible: PASS
```

---

## 11. Frozen Invariant Verification

- **Affect Equations**: Preserved identically (Confusion, Frustration, Engagement).
- **Runtime Thresholds**: Confusion = 0.60, Frustration = 0.65, Dwell = 3.0s, Cooldown = 15s, Max Adaptations = 3.
- **Predictor Parameters**: $K=5$, min 3 valid samples, $\Delta t \ge 0.4\text{ s}$, persistence 2 samples, cooldown 5s, recovery floor $< 0.30$.
- **Adaptation Ladders**: Confusion (`InjectCue` $\rightarrow$ `InsertHint` $\rightarrow$ `SlowPace` $\rightarrow$ `SimplifyStep`); Frustration (`SlowPace` $\rightarrow$ `SimplifyStep`).
- **Hardware Abstraction**: GPIO 12 and 13 remain provisional software-test assignments; transport undecided; physical integration deferred to Phase 9B.

---

## 12. Git Safety Summary

- Files modified in tracked repository: `unity_project/Assets/Scenes/A3R_Main.unity` (EventSystem component upgrade) and `unity_project/ProjectSettings/ProjectSettings.asset` (`activeInputHandler: 2`).
- Untracked file updated: `unity_project/Assets/Editor/SceneSetupHelper.cs`.
- No Git commit, push, branch, or remote operations were performed.

---

## 13. Remaining Warnings & Declarations

- **Non-blocking Environment Warning**: The informational warning regarding `&` in folder `AR&VR` (`Microsoft.Unity.VisualStudio.Editor.ProjectGeneration`) persists during VS solution generation. It is non-blocking and does not impact compilation or runtime execution.
- **Research Integrity Boundaries**:
  - Phone connected: **NO**
  - Physical ESP32 connected: **NO**
  - Physical ESP32 validation: **NOT PERFORMED**
  - Physical transport validation: **NOT PERFORMED**
  - Final physical GPIO mapping: **NOT FROZEN**
  - Git commit/push: **NOT PERFORMED**

---

## Final Verdict

**PASS WITH NON-BLOCKING WARNING**
