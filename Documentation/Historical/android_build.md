# A³R Android Build Guide

**Document ID:** `A3R-DOC-BUILD-001`  
**Date:** 2026-09-17  
**Engine:** Unity 6.3 LTS (`6000.3.23f1`)  
**Target Platform:** Android (`arm64-v8a`, API level 28 minimum, API level 34+ target)  
**Primary Target Device:** OnePlus 13R (Snapdragon 8 Gen 3, Android 16)  

---

## 1. Overview

The A³R application is configured as a fully standalone mobile AR application using:
- **Unity AR Foundation 6.3.5** with Google ARCore XR Plugin
- **MediaPipe Unity Plugin** (homuler v0.14.4) with precompiled native Android binaries (`mediapipe_android.aar`)
- **IL2CPP Scripting Backend** targeting 64-bit ARM (`arm64-v8a`)
- **Text Mesh Pro** and modern Unity Input System (`com.unity.inputsystem`)

The build pipeline produces a standalone Android Package (`.apk`) ready for research sideloading and empirical evaluation.

---

## 2. Build Artifacts

Upon running the build, the APK is produced at two canonical locations:
- Primary: `build/android/A3R.apk`
- Alternate: `builds/android/A3R.apk`

| Metric | Specification |
| :--- | :--- |
| **Package / Bundle ID** | `com.a3rproject.affectivear` |
| **Version** | `1.0.0` (Build `1`) |
| **APK File Size** | ~144.4 MB (144,429,721 bytes) |
| **Architectures** | ARM64 (`arm64-v8a`) only |
| **Graphics API** | OpenGLES3 / Vulkan compatible |
| **Included Scenes** | `Assets/Scenes/A3R_Main.unity` |

---

## 3. Build Prerequisites

1. **Unity Editor**: Unity 6.3 LTS (`6000.3.23f1`) with Android Build Support installed (Android SDK & NDK Tools, OpenJDK).
2. **Android SDK Location**:
   - Bundled: `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK`
3. **Android ADB Path**:
   - `C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`
4. **MediaPipe Assets**:
   - `mediapipe_android.aar` in `Packages/com.github.homuler.mediapipe/Runtime/Plugins/Android/`
   - `face_landmarker.task` model file in `Assets/StreamingAssets/`
   - `-define:MEDIAPIPE_UNITY_PLUGIN` flag in `Assets/csc.rsp`

---

## 4. How to Build the APK

### Option A: Automated PowerShell / Batchmode Command (Headless)

Close any active Unity GUI instances to prevent lockfile conflicts, then run:

```powershell
# From the project root (C:\Fall Semister\2026\AR&VR\project\ARVR):
Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" `
  -ArgumentList "-batchmode -quit -projectPath `"$PWD\unity_project`" -executeMethod A3R.Editor.BuildHelper.BuildAndroidApkStandard -logFile `"$PWD\build_android.log`"" `
  -Wait -NoNewWindow
```

The build will complete in 4–6 minutes and write its log to `build_android.log`.

### Option B: Unity Editor GUI

1. Open the project in Unity Editor (`6000.3.23f1`).
2. Verify that `Assets/Scenes/A3R_Main.unity` is open.
3. In the top menu bar, select:
   **`A3R` → `Build Android APK (Standard)`**
4. Unity will compile all C# assemblies, run IL2CPP code generation, package the Gradle project, and output to `build/android/A3R.apk`.

---

## 5. Build Configuration Checklist

The build settings are strictly locked in [`BuildHelper.cs`](file:///C:/Fall%20Semister/2026/AR&VR/project/ARVR/unity_project/Assets/Editor/BuildHelper.cs):

- [x] **Scripting Backend**: `ScriptingImplementation.IL2CPP`
- [x] **Target Architectures**: `AndroidArchitecture.ARM64` (`ARMv7` disabled)
- [x] **Min Android API Level**: `AndroidSdkVersions.AndroidApiLevel28` (Android 9.0 Pie)
- [x] **Target Android API Level**: `AndroidSdkVersions.AndroidApiLevelAuto`
- [x] **ARCore Support**: `XRGeneralSettingsPerBuildTarget` configured with `ARCoreLoader`
- [x] **Camera Permissions**: Declared in Android Manifest (`CAMERA`, `android.hardware.camera.ar`, `android.hardware.camera.front`)
- [x] **Managed Stripping Level**: `ManagedStrippingLevel.Low` with explicit `link.xml` preserving `Mediapipe.Runtime`
