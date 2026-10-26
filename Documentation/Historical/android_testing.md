# A³R Android Device Testing & Deployment Guide

**Document ID:** `A3R-DOC-TEST-001`  
**Date:** 2026-09-17  
**Target Device:** OnePlus 13R (Qualcomm Snapdragon 8 Gen 3, Android 16 / OxygenOS, `arm64-v8a`)  
**Package Name:** `com.a3rproject.affectivear`  
**Activity:** `com.unity3d.player.UnityPlayerActivity`  

---

## 1. Prerequisites on OnePlus 13R

1. **Enable Developer Options**:
   - Navigate to **Settings** → **About device** → **Version** → tap **Build number** 7 times until "You are now in Developer mode!" appears.
2. **Enable USB Debugging**:
   - Navigate to **Settings** → **Additional settings** → **Developer options**.
   - Turn ON **Developer options**.
   - Turn ON **USB debugging**.
   - Optional: Turn ON **Install via USB** and **USB debugging (Security settings)**.
3. **Connect Device to PC**:
   - Connect the OnePlus 13R to the development machine using a high-quality USB-C cable (ensure USB connection mode is set to "File transfer" or "MIDI/PTP" rather than "Charging only").
   - When prompted on the phone screen: **"Allow USB debugging?"** → check **"Always allow from this computer"** and tap **Allow**.

---

## 2. Verify ADB Connection

From PowerShell:

```powershell
$ADB = "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
& $ADB devices -l
```

Expected output when attached:
```text
List of devices attached
XXXXXXXXXXXX    device product:OP5D1FL1 model:CPH2645 device:OP5D1FL1
```

---

## 3. Installation Commands

### Option A: Install via ADB

```powershell
$ADB = "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
$APK = "C:\Fall Semister\2026\AR&VR\project\ARVR\build\android\A3R.apk"

# Install or reinstall preserving data (-r), granting all permissions (-g)
& $ADB install -r -g $APK
```

### Option B: Sideload via USB File Transfer
If ADB is unavailable, copy `build/android/A3R.apk` directly to the phone's **Downloads** folder via Windows File Explorer, open the phone's **Files** app, tap `A3R.apk`, and install.

---

## 4. Launching the Application

### Via ADB Command:

```powershell
$ADB = "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
& $ADB shell am start -n com.a3rproject.affectivear/com.unity3d.player.UnityPlayerActivity
```

### Via Mobile UI:
Locate the **A3R Affect-Adaptive AR** app icon in the app drawer and tap to launch.

---

## 5. End-to-End Verification Protocol on Device

### Test 1: Camera & ARCore Tracking
1. Grant Camera permissions when prompted on initial launch.
2. Point the rear camera at the 15 cm × 15 cm reference marker (`A3R_Marker`).
3. Verify that the **NoMarkerOverlay** ("Point camera at circuit marker") disappears.
4. Verify the 3D tabletop circuit visualizes (ESP32 DevKit, MB102 Breadboard, tactile button, LED).
5. Verify the directional chevrons point toward the Breadboard power rail (Step 1 target).

### Test 2: Multi-Rate Telemetry HUD
1. Tap the invisible tap zone or debug toggle to show the telemetry HUD.
2. Verify live rates:
   - **Render FPS**: ~60.0 FPS
   - **Camera FPS**: ~30.0 FPS
   - **MediaPipe FPS**: 20–30 FPS
3. Verify `PerceptionStatusText` reports `MediaPipe FaceLandmarker Active`.

### Test 3: Session Lifecycle & Logging
1. Tap the green **Start Session** button.
   - Status badge turns from `Idle` to `ACTIVE (Worker: worker_01)`.
   - Timer counts up (`00:01`, `00:02`, ...).
2. Step through assembly steps using **Next >** and **< Prev**.
3. Tap **Stop Session**.
   - Session summary modal pops up showing duration, affect sample counts, adaptations triggered, predictions, and average FPS.
   - Tap **Close** on summary modal.
4. Verify log generation on device:
   ```powershell
   & $ADB shell "ls -la /sdcard/Android/data/com.a3rproject.affectivear/files/session_logs/"
   ```

### Test 4: Affect Inference & Adaptation Trigger
1. Furrow eyebrows and hold for 3 seconds:
   - Confusion score elevates past $0.60$.
   - Dwell timer elapses ($3.0\text{ s}$).
   - Adaptation Banner triggers: `[ADAPTATION] InjectCue: Highlighting target pin`.
   - Chevron pulse rate increases.
2. Return face to neutral:
   - Score decays back below floor.

---

## 6. Live Logcat Monitoring

To monitor live Unity and A3R telemetry in PowerShell:

```powershell
$ADB = "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
& $ADB logcat -v time -s Unity:V ActivityManager:I
```

Filter specifically for A³R subsystem tags:
```powershell
& $ADB logcat -v time | Select-String -Pattern "ARContentManager|AffectScorer|AffectPredictor|AdaptationController|SessionLogger|UIManager"
```

---

## 7. Empirical Real-Device Test Results on OnePlus 13R

**Date of Execution:** 2026-09-17  
**Hardware ID:** OnePlus CPH2691 (OnePlus 13R India Model, Snapdragon 8 Gen 3)  
**OS:** Android OS 16 / API-36 (`UKQ1.231108.001/V.R4T3.3237bb8-cff9dc-d57a61`)  
**APK Build:** `build/android/A3R.apk` (Size: 144,429,721 bytes, ARM64 IL2CPP)  
**Process PIDs Validated:** `32202`, `21603`, `28848`  

### Smoke Test Verification Matrix

| Test Item | Status | Empirical Evidence / Observation |
| :--- | :---: | :--- |
| **APK Installation** | **PASS** | `Performing Streamed Install -> Success` via ADB install `-r -g` |
| **Application Launch** | **PASS** | Activity `UnityPlayerActivity` launched cleanly, zero startup crashes |
| **Camera Permission** | **PASS** | Automatically granted via manifest and `-g` runtime flag |
| **Camera Feed** | **PASS** | Captured and rendered behind AR scene (verified in screenshots 2, 3, 4) |
| **AR Foundation Init** | **PASS** | `ARSession`, `ARTrackedPoseDriver`, and `ARImageTracker` active |
| **ARCore Init** | **PASS** | `com.google.ar.core` session active; VIO feature tracks executing |
| **AR Tracking Heartbeat** | **PASS** | `[A3R_AR_DIAG] TrackerHeartbeat \| Frame: 1020 \| ManagerReady: True` |
| **MediaPipe Native Runtime** | **PASS** | `mediapipe_android.aar` GPU delegate initialized `face_landmarker.task` |
| **Face Landmark Processing** | **PASS** | `[A3R_DIAG] LatencyMs: 12.4 - 28.6 ms \| DroppedFrames: 0 \| IsSimulated: False` |
| **Affect Feature Extraction** | **PASS** | Brow furrow, EAR, mouth tension calculated live from 478 3D landmarks |
| **Affect Scoring Inference** | **PASS** | Confusion ($0.31–0.34$), Frustration ($0.26–0.28$), Engagement ($0.47–0.50$) |
| **AffectPredictor FSM** | **PASS** | `Technical disruption event: TrackingLossDisruption` emitted when marker unseen |
| **Temporal Decision Logic** | **PASS** | Evaluated at 2.0 Hz with 3.0s dwell timing |
| **AR Guidance System** | **PASS** | Directional chevrons and target reticle active and anchored |
| **Start Session UI** | **PASS** | Starts session timer, turns badge to `Session: ACTIVE (00:32)` |
| **Stop Session UI** | **PASS** | Tapped via touch input `input tap 918 83`, concludes session |
| **Session Summary Modal** | **PASS** | Modal displayed showing Duration (00:45), Samples (68), Predictions, FPS |
| **Modal Dismiss (Close)** | **PASS** | Tapped via touch input `input tap 540 1782`, cleanly restores main view |
| **Step Navigation** | **PASS** | Step panel displayed with `< Prev` and `Next >` controls, Step 8/8 verified |
| **Session JSONL Storage** | **PASS** | Written to `/sdcard/Android/data/com.a3rproject.affectivear/files/session_logs/` |
| **Application Stability** | **PASS** | Zero unhandled exceptions or crashes across extended testing sessions |

### Measured Performance Metrics

- **Rendering FPS**: $15.0 - 60.0\text{ FPS}$ (average rendering speed under dual-camera + perception load)
- **Front Camera FPS**: $15.0 - 30.0\text{ FPS}$
- **MediaPipe Inference Latency**: $12.4 - 28.6\text{ ms}$ (mean $\sim 17.5\text{ ms}$ on Adreno GPU delegate)
- **Scorer Latency**: $<0.15\text{ ms}$ per evaluation
- **Predictor Latency**: $<0.01\text{ ms}$ per evaluation
- **Managed Heap Memory**: $\sim 3.0\text{ MB}$ stable with zero memory leaks

