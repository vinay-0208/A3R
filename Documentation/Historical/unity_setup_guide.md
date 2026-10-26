# Unity Setup Guide — A³R Project

> **Who this is for**: Anyone picking up this project for the first time, or any session agent
> that needs to know what manual steps the user must complete before Unity-dependent milestones
> can proceed.

---

## Step 1 — Install Unity Hub

Unity Hub is **required** and cannot be installed unattended in this environment (requires user interaction for account sign-in and license activation).

**Download**: https://unity.com/download  
**Installer filename**: `UnityHubSetup.exe`  
**Install to default path**: `C:\Program Files\Unity Hub\`

After installing, launch Unity Hub and sign in with a Unity account (free Personal license is sufficient).

---

## Step 2 — Install Unity 2022.3 LTS via Unity Hub

1. Open Unity Hub → **Installs** tab → **Install Editor**
2. Select **Unity 2022.3 LTS** (any 2022.3.x patch release — e.g., 2022.3.55f1)
3. Under **Modules**, check:
   - ✅ **Android Build Support**
   - ✅ **Android SDK & NDK Tools** (sub-option under Android Build Support)
   - ✅ **OpenJDK** (sub-option under Android Build Support)
4. Click **Install** — this takes ~30 minutes

> **Note**: Record the exact Unity version installed and update `memory.md` under "Unity version pinned".

---

## Step 3 — Open the A3R Unity Project

1. Unity Hub → **Projects** tab → **Open** → browse to:
   ```
   C:\Users\black\Documents\arvr\A3R\unity_project\
   ```
2. Unity will detect `Packages/manifest.json` and auto-import all packages:
   - AR Foundation 5.1.5
   - ARCore XR Plugin 5.1.5
   - TextMeshPro 3.0.9
   - Input System 1.7.0
3. Wait for **package compilation** to finish (may take 3–5 minutes on first open)

> **Expected warnings**: "ARKit XR Plugin not installed" — this is correct and expected (Android-only).

---

## Step 4 — Verify XR Management Settings

1. **Edit → Project Settings → XR Plug-in Management**
2. Select the **Android** tab (robot icon)
3. Ensure **ARCore** checkbox is ticked
4. iOS tab: all unchecked (intentional — Android only)

---

## Step 5 — Import the Reference Image into XRReferenceImageLibrary

1. In the **Project** panel, navigate to `Assets/ReferenceImageLibrary/`
2. Right-click → **Create → XR → Reference Image Library**
3. Name it: `A3R_ReferenceImageLibrary`
4. Click the library asset → **Add Image** button
5. Drag `assets_reference/marker_image_source.png` into the **Texture** slot
   - (First import the PNG: drag from File Explorer into `Assets/ReferenceImageLibrary/`)
6. Set **Physical Size (m)**: `0.15` (15cm × 15cm)
7. Set **Name**: `A3R_Marker` (must match `ARImageTracker.referenceImageName`)
8. Check **Keep Texture at Runtime**: ✅

---

## Step 6 — Assign the Scene Components

Open `Assets/Scenes/A3R_Main.unity`.

The scene hierarchy should contain:
- `XR Origin` with AR camera
- `AR Session`
- `A3R_Manager` with `ARImageTracker` + `ARContentManager`

Wire up the remaining Inspector references in `A3R_Manager`:
- `ARImageTracker.contentRootPrefab` → create and assign a `ContentRoot` prefab
- `ARContentManager.taskJsonFile` → drag `Assets/StepData/led_blink_task.json`
- `ARTrackedImageManager.referenceLibrary` → drag `A3R_ReferenceImageLibrary`
- `ARTrackedImageManager.maxNumberOfMovingImages` → set to `1`

---

## Step 7 — Configure Android Build Settings

Click **A3R → Verify Build Settings** in the menu bar.  
This applies all locked settings automatically:
- Platform: Android
- Bundle ID: `com.a3rproject.affectivear`  
- Min SDK: 28 (Android 9)
- Architecture: ARM64
- ARCore: enabled

---

## Step 8 — Test in Editor (Simulation Mode)

1. Open `A3R_Main.unity`
2. Select the `A3R_Manager` GameObject
3. On `FaceLandmarkBridge`, tick **Debug Simulate Output** ✅
4. Press **Play** — the affect pipeline will run with simulated face data
5. Watch the **Debug HUD** for confusion/frustration/engagement values updating

---

## Step 9 — Build Android APK

1. Connect Android phone via USB (USB debugging enabled)
2. Click **A3R → Build Android APK (ADB Install)**
3. APK is built and pushed to the connected device automatically
4. Output is saved to `build/android/A3R_adb_<timestamp>.apk`

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| "ARKit plugin not found" warning | Expected — ignore, Android-only project |
| Package compile errors | Delete `Library/` folder and reopen project |
| ARCore not available on device | Device must support ARCore (check https://developers.google.com/ar/devices) |
| Black screen on device | Check XR Management → Android tab has ARCore ticked |
| `A3R_Marker` not detected | Check physical print is exactly 15cm × 15cm; lighting must be good |
| Affect scores always 0 | `FaceLandmarkBridge.debugSimulateOutput` must be false on device; front camera permission granted |

---

## SDK / NDK Paths (update after Unity install)

After Unity install, record these paths in `memory.md`:

```
Android SDK: C:\Users\black\AppData\Local\Android\Sdk    (or from Unity Hub install)
Android NDK: C:\Users\black\AppData\Local\Android\Sdk\ndk\<version>
JDK:         Bundled with Unity (set automatically)
adb:         C:\Users\black\AppData\Local\Android\Sdk\platform-tools\adb.exe
```
