# A³R — Unity Input System Configuration Repair & Verification Report

**Project**: A³R (Affect-Adaptive Augmented Reality)  
**Date**: September 16, 2026  
**Unity Engine Version**: Unity 6.3 LTS (`6000.3.23f1`)  
**Verdict**: **PASS WITH NON-BLOCKING PATH WARNING**  

---

## 1. Original Console Error

The Unity Editor Console and runtime logs reported the following exception:

```text
System.ArgumentException: Invalid value of 'activeInputHandler' setting: -1
  at UnityEngine.InputSystem.Editor.EditorPlayerSettingHelpers.ActiveInputHandlerToTuple (System.Int32 value) [0x00021] 
     in Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/InputSystem/Editor/Settings/EditorPlayerSettingHelpers.cs:91
  at UnityEngine.InputSystem.Editor.EditorPlayerSettingHelpers.get_newSystemBackendsEnabled () [0x0000a] 
     in Library/PackageCache/com.unity.inputsystem@7a4e1a2a8194/InputSystem/Editor/Settings/EditorPlayerSettingHelpers.cs:24
```

Additionally, in interactive Editor play/update sessions, Unity UGUI's `StandaloneInputModule` produced secondary exceptions when attempting to evaluate legacy input without the old input backend enabled:

```text
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, 
but you have switched active Input handling to Input System package in Player Settings.
  at UnityEngine.Input.get_mousePosition ()
  at UnityEngine.EventSystems.BaseInput.get_mousePosition ()
  at UnityEngine.EventSystems.StandaloneInputModule.UpdateModule ()
```

---

## 2. Exact `activeInputHandler` Value Before Repair

Inspection of `unity_project/ProjectSettings/ProjectSettings.asset` (line 819):

```yaml
818:   captureStartupLogs: {}
819:   activeInputHandler: -1
820:   windowsGamepadBackendHint: 0
```

The exact recorded value before repair was **`-1`**.

---

## 3. Root Cause

1. **Unity Input Handler Enumeration**:  
   In Unity's `com.unity.inputsystem` package (`EditorPlayerSettingHelpers.cs`, lines 73–93), the `InputHandler` enum defines valid values as:
   ```csharp
   private enum InputHandler
   {
       OldInputManager = 0,
       NewInputSystem = 1,
       InputBoth = 2
   };
   ```
   The mapping method `ActiveInputHandlerToTuple(int value)` matches strictly on `0`, `1`, and `2`. Any other integer (such as `-1`) triggers an explicit `ArgumentException($"Invalid value of 'activeInputHandler' setting: {value}")`.

2. **Historical Migration Serialization**:  
   Older Unity versions tracked input configuration via two separate boolean fields: `useNewInputSystem` and `disableOldInputManagerSupport`. When upgrading project settings across major Unity versions to Unity 6, `activeInputHandler` was serialized to `-1` (uninitialized/invalid sentinel).

3. **Package Initialization Trigger**:  
   When the Editor or Package Manager loads assemblies, `com.unity.inputsystem` executes domain initialization routines (`newSystemBackendsEnabled`), which immediately queries `activeInputHandler`. The invalid `-1` caused the exception to trigger on every domain reload and flagged the package in the Package Manager UI.

---

## 4. Input APIs Actually Used by A³R

An audit across `unity_project/Assets` and `unity_project/Packages` identified the project's actual input dependencies:

1. **Legacy Input System (`UnityEngine.Input`)**:  
   - The scene `Assets/Scenes/A3R_Main.unity` instantiates `A3R_HUD_Canvas` with an `EventSystem` GameObject utilizing `UnityEngine.EventSystems.StandaloneInputModule`.  
   - `StandaloneInputModule` is part of Unity UI (UGUI) and directly queries `UnityEngine.Input.mousePosition` and legacy touch events.  
   - If `activeInputHandler` is set to `1` (`NewInputSystem` only), `StandaloneInputModule` throws continuous runtime `InvalidOperationException` errors.

2. **New Input System (`UnityEngine.InputSystem`)**:  
   - `Packages/manifest.json` specifies `"com.unity.inputsystem": "1.20.0"`.  
   - Modern Unity 6 XR and AR packages in the project (`com.unity.xr.arfoundation@6.3.5`, `com.unity.xr.arcore@6.3.5`, `com.unity.xr.management@4.6.1`, `com.unity.xr.core-utils@2.6.0`, and `com.unity.xr.legacyinputhelpers@2.1.13`) contain assembly definitions explicitly referencing `com.unity.inputsystem`.  
   - If `activeInputHandler` is set to `0` (`OldInputManager` only), the New Input System backends are disabled (`ENABLE_INPUT_SYSTEM` is removed), impairing XR subsystem device bindings.

3. **Conclusion**:  
   The project concurrently requires both the Legacy Input Manager (for `StandaloneInputModule` UGUI event polling) and the New Input System (for Unity 6 AR Foundation / ARCore XR plugin backends).

---

## 5. Valid Input Configuration Selected

Selected Mode: **`Both` (`activeInputHandler: 2`)**

- **Enum Mapping**: `InputHandler.InputBoth = 2`
- **Tuple Evaluation**: `(newSystemEnabled: true, oldSystemEnabled: true)`
- **Scripting Defines Active**: Both `ENABLE_INPUT_SYSTEM` and `ENABLE_LEGACY_INPUT_MANAGER`
- **Safety**: Satisfies `com.unity.inputsystem` backends, eliminates `ArgumentException`, allows `StandaloneInputModule` to run without `InvalidOperationException`, and preserves AR Foundation 6.3.5 subsystem integration.

---

## 6. Exact File Changed

- `unity_project/ProjectSettings/ProjectSettings.asset` (Line 819)

No C# source files, no scenes, no shader files, and no package manifests were modified.

---

## 7. Exact Configuration Change

```diff
--- a/unity_project/ProjectSettings/ProjectSettings.asset
+++ b/unity_project/ProjectSettings/ProjectSettings.asset
@@ -816,7 +816,7 @@
   qnxGraphicConfPath: 
   apiCompatibilityLevel: 6
   captureStartupLogs: {}
-  activeInputHandler: -1
+  activeInputHandler: 2
   windowsGamepadBackendHint: 0
   cloudProjectId: 
   framebufferDepthMemorylessMode: 0
```

---

## 8. Package State Before and After

| Package | Version | Before Repair | After Repair |
| :--- | :---: | :---: | :---: |
| `com.unity.inputsystem` | 1.20.0 | ArgumentException on init | Initialized cleanly |
| `com.unity.xr.arfoundation` | 6.3.5 | Unchanged | Unchanged (Registered) |
| `com.unity.xr.arcore` | 6.3.5 | Unchanged | Unchanged (Registered) |
| `com.github.homuler.mediapipe` | local embedded | Unchanged | Unchanged (Registered) |
| `com.unity.xr.legacyinputhelpers` | 2.1.13 | Unchanged | Unchanged (Registered) |
| `com.unity.ugui` | 2.0.0 | Runtime input errors | StandaloneInputModule operational |

All 69 project packages registered cleanly in 0.01s (`[Package Manager] Done registering packages in 0.01 seconds`).

---

## 9. Package Manager Popup Status

- **Cause of Popup**: The Package Manager popup ("Some packages have errors") was directly caused by the unhandled `System.ArgumentException` originating from `com.unity.inputsystem`'s `EditorPlayerSettingHelpers.cs` upon evaluating `activeInputHandler: -1`.
- **Resolution**: Setting `activeInputHandler: 2` allows `ActiveInputHandlerToTuple` to return valid flags without throwing. In batchmode execution, zero package errors were reported. The Package Manager popup condition is resolved.

---

## 10. `AR&VR` Path Warning Status

The Unity Console/log output displays:
```text
Project path contains special characters, which can be an issue when opening Visual Studio
Microsoft.Unity.VisualStudio.Editor.ProjectGeneration:SyncSolution(...)
  at ./Library/PackageCache/com.unity.ide.visualstudio@b5edea5f33f4/Editor/ProjectGeneration/ProjectGeneration.cs:841
```
- **Investigation**: This warning is generated solely by the `com.unity.ide.visualstudio` package when generating `.csproj` and `.sln` files due to the ampersand character (`&`) in the path directory `AR&VR`.
- **Impact Assessment**: It is completely non-blocking. Unity engine asset compilation succeeded in 0.26s (`*** Tundra build success`), scene verification ran to completion with exit code 0, and runtime execution is unimpeded.
- **Action Taken**: In accordance with instructions, the project was NOT moved or renamed. The warning is classified as a non-blocking environment warning.

---

## 11. Unity Compilation Result

- **Command**: `Unity.exe -quit -batchmode -projectPath "..." -logFile "..."`
- **Result**: **PASS**
- **Exit Code**: `0`
- **C# Compiler Errors**: `0`
- **C# Compiler Warnings**: `0`
- **Tundra Build Duration**: `0.26 seconds (1 items updated, 1262 evaluated)`

---

## 12. Scene Initialization Result

- **Command**: `Unity.exe -quit -batchmode -projectPath "..." -executeMethod A3R.Editor.SceneSetupHelper.VerifySceneRuntimeInitialization -logFile "..."`
- **Result**: **14 / 14 CHECKS PASSED**
- **Exit Code**: `0`
- **Detailed Checks**:
  1. `A3R_Manager exists`: PASS
  2. `ARContentManager on A3R_Manager`: PASS
  3. `ARImageTracker on A3R_Manager`: PASS
  4. `AffectScorer on A3R_Manager`: PASS
  5. `AffectPredictor on A3R_Manager`: PASS
  6. `AdaptationController on A3R_Manager`: PASS
  7. `SessionLogger on A3R_Manager`: PASS
  8. `MockESP32Bridge on A3R_Manager`: PASS
  9. `ARGuidanceController on A3R_Manager`: PASS
  10. `A3R_HUD_Canvas exists`: PASS
  11. `UIManager on A3R_HUD_Canvas`: PASS
  12. `ARContentManager.UIManager serialized/accessible`: PASS
  13. `ARContentManager.HardwareBridge serialized/accessible`: PASS
  14. `ARContentManager.ARGuidanceController serialized/accessible`: PASS

---

## 13. Python Regression Test Results

- **Command**: `.venv\Scripts\python.exe -m pytest -v python_tools/`
- **Result**: **PASS**
- **Summary**: **116 / 116 passed in 1.73s** (0 failures, 0 errors)

---

## 14. AR Guidance Evaluation Result

- **Command**: `.venv\Scripts\python.exe python_tools/guidance/run_guidance_evaluation.py`
- **Result**: **PASS**
- **Summary**: **15 / 15 scenarios passed** (0 failures)

---

## 15. Phase 11 Software End-to-End Evaluation Result

- **Command**: `.venv\Scripts\python.exe python_tools/e2e/run_phase11_evaluation.py`
- **Result**: **PASS**
- **Summary**: **5 / 5 scenarios passed** (Nominal, Step 3 Confusion, Step 7 Frustration, Recovery Floor, Hardware Fault)

---

## 16. Frozen A³R Invariant Verification

All core research and algorithmic invariants were verified intact:

1. **Affect Equations**:
   - $\text{Confusion} = 0.40 \times \text{brow\_furrow} + 0.30 \times (1 - \text{EAR}) + 0.30 \times \text{mouth\_tension}$
   - $\text{Frustration} = 0.35 \times \text{brow\_furrow} + 0.25 \times (1 - \text{EAR}) + 0.40 \times \text{mouth\_tension}$
   - $\text{Engagement} = 0.50 \times \text{EAR} + 0.30 \times (1 - \text{brow\_furrow}) + 0.20 \times \text{brow\_height}$
2. **Runtime Thresholds**: Confusion = 0.60, Frustration = 0.65, Dwell = 3.0s, Cooldown = 15.0s, Max Adaptations = 3.
3. **Predictor Parameters**: Rolling window $K=5$, minimum 3 valid samples, $\Delta t \ge 0.4\text{ s}$, persistence 2 samples, cooldown 5s, recovery floor $< 0.30$.
4. **Adaptation Ladders**:
   - Confusion: `InjectCue` $\rightarrow$ `InsertHint` $\rightarrow$ `SlowPace` $\rightarrow$ `SimplifyStep`
   - Frustration: `SlowPace` $\rightarrow$ `SimplifyStep`
5. **Hardware**: GPIO 12 and GPIO 13 remain provisional software-test assignments; transport undecided; no physical communication claims made.

---

## 17. Git Status and Diff Summary

- **Modified tracked file in this repair**: `unity_project/ProjectSettings/ProjectSettings.asset` (1 line modified: `activeInputHandler: -1` $\rightarrow$ `activeInputHandler: 2`).
- **No Git commits, pushes, or branch/remote modifications performed**.
- Working tree remains clean of unintended source changes.

---

## 18. Remaining Issues & Declarations

- **Remaining Issue**: Non-blocking `Microsoft.Unity.VisualStudio.Editor.ProjectGeneration` warning regarding `&` in the path folder `AR&VR`. This does not impair Unity batchmode compilation, tests, or runtime operation.
- **Research Integrity Boundaries**:
  - Phone connected: **NO**
  - Physical ESP32 connected: **NO**
  - Physical hardware validation: **NOT PERFORMED**
  - Physical transport validation: **NOT PERFORMED**
  - Final physical GPIO mapping: **NOT FROZEN**
  - Git commit/push: **NOT PERFORMED**

---

## Final Verdict

**PASS WITH NON-BLOCKING PATH WARNING**
