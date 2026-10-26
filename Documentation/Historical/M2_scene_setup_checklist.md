# M2 Scene Setup Checklist — Static AR Overlay

> Complete this checklist after installing Unity Hub and opening the project.
> M2 goal: A static AR step-by-step overlay that anchors correctly to the printed
> reference marker with NO affect sensing yet.

---

## Pre-conditions

- [ ] Unity Hub installed (https://unity.com/download)
- [ ] Unity 2022.3 LTS installed with Android Build Support module
- [ ] Project opened: `unity_project/` in Unity Hub
- [ ] All packages compiled (no red errors in Console after first open)
- [ ] **A3R → Verify Build Settings** clicked — no errors

---

## Step 1: Import the Reference Marker Image

1. Drag `assets_reference/marker_image_source.png` into `Assets/ReferenceImageLibrary/` in the Project panel
2. Select the imported texture → Inspector:
   - Texture Type: **Default** (not Sprite)
   - Compression: **None** (for AR accuracy)
   - Click **Apply**

---

## Step 2: Create the XRReferenceImageLibrary

1. `Assets/ReferenceImageLibrary/` → right-click → **Create → XR → Reference Image Library**
2. Name it: `A3R_ReferenceImageLibrary`
3. Select the asset → Inspector → **Add Image**:
   - **Texture**: drag `marker_image_source` from Project panel
   - **Name**: `A3R_Marker` ← exact string, must match `ARImageTracker.referenceImageName`
   - **Physical Size (m)**: `0.15` (15 cm)
   - **Keep Texture at Runtime**: ✅

---

## Step 3: Wire the Tracked Image Manager

1. Open `Assets/Scenes/A3R_Main.unity`
2. Select `XR Origin` in the Hierarchy
3. Add component: **AR Tracked Image Manager** (from AR Foundation)
4. Inspector:
   - `Reference Library`: drag `A3R_ReferenceImageLibrary`
   - `Max Number Of Moving Images`: `1`
   - `Tracked Image Prefab`: leave empty (ARImageTracker spawns content dynamically)

---

## Step 4: Wire the A3R_Manager Components

Select `A3R_Manager` in Hierarchy.

### ARImageTracker
- `Reference Image Name`: `A3R_Marker`
- `Content Root Prefab`: create a new empty GameObject Prefab named `ContentRoot`, save to `Assets/Prefabs/ContentRoot.prefab`, drag here

### ARContentManager
- `Task Json File`: drag `Assets/StepData/led_blink_task.json`
- `AR Image Tracker`: drag `A3R_Manager` (self-reference via component)
- `UI Manager`: drag `UICanvas` → `UIManager` component (see Step 6)
- `Next Step Button`: wire to Canvas next button
- `Prev Step Button`: wire to Canvas prev button
- `AR Cue Arrow`: leave empty for now (M4)
- `Hint Panel`: wire to Canvas hint panel (see Step 6)

---

## Step 5: Build the ContentRoot Prefab

1. Right-click Hierarchy → **Create Empty** → name it `ContentRoot`
2. Position: (0, 0, 0), all scales = 1
3. Add child GameObjects (one per step, initially just placeholder cubes):
   - `StepOverlay_01` through `StepOverlay_08` — empty GameObjects
   - For now, add a small colored sphere to each to verify AR anchoring
4. Right-click `ContentRoot` in Hierarchy → **Prefab → Create Variant**, save to `Assets/Prefabs/ContentRoot.prefab`
5. Delete from Hierarchy (it will be instantiated at runtime by `ARImageTracker`)

---

## Step 6: Build the Canvas UI

1. Right-click Hierarchy → **UI → Canvas** → name it `UICanvas`
   - Canvas Scaler: **Scale With Screen Size**, Reference = 1080×1920, Match = 0.5
2. Create the following child panels (use `UI → Panel`):

   | Panel Name | Initial Active | Purpose |
   |---|---|---|
   | `StepPanel` | Active | Step title + instruction + counter + nav buttons |
   | `HintPanel` | **Inactive** | Hint/simplified text card |
   | `DebugHudPanel` | **Inactive** | Affect scores + marker status |
   | `NoMarkerOverlay` | **Inactive** | "Point camera at marker" message |

3. Add **TMP_Text** components inside each panel as specified in `UIManager.cs` Inspector fields
4. Add a `UIManager` component to `UICanvas` → wire all Inspector references
5. Wire `ARContentManager.UIManager` → drag `UICanvas`

---

## Step 7: First Play-Mode Test (Editor)

1. Select `A3R_Manager` → `FaceLandmarkBridge` → ✅ **Debug Simulate Output**
2. Press **Play**
3. Expected:
   - No errors in Console (warnings OK)
   - Step 1 text appears in the step panel
   - Next/Prev buttons respond
   - Debug HUD shows slowly varying simulated affect scores
4. If "ARCore not supported in Editor" warning appears — expected, ignore

---

## Step 8: Device Test (AR Tracking)

1. Connect Android device (USB debug enabled, ARCore-compatible device)
2. **A3R → Build Android APK (ADB Install)**
3. Open the app on device, point at the printed marker (15cm × 15cm on flat surface)
4. Expected:
   - ContentRoot Prefab appears anchored to the marker
   - Overlay stays locked as you move the phone around the marker
   - Step panel visible in 2D overlay
5. If tracking is unstable: check lighting (needs > 200 lux), ensure marker is printed flat

---

## Step 9: M2 Completion Criteria

- [ ] Marker is detected and tracked on a physical Android device
- [ ] ContentRoot prefab anchors correctly to marker in world space
- [ ] Step panel shows step 1 text on app launch
- [ ] Next/Prev navigation works on-device
- [ ] No affect sensing yet (FaceLandmarkBridge can remain in simulate mode)
- [ ] Session is logged to `persistentDataPath/session_logs/*.jsonl`
- [ ] Record Android device model and Unity version in `memory.md`

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| Marker never detected | Wrong physicalSize in library | Verify size = 0.15m; re-measure printed marker |
| ContentRoot prefab drifts | ARCoreEnabled not set | A3R → Verify Build Settings |
| Black screen on device | ARCore init failed | Check XR Management → Android → ARCore ticked |
| Step panel text missing | TMP essential resources not imported | Window → TextMeshPro → Import TMP Essential Resources |
| "NullReferenceException: ARImageTracker" | contentRootPrefab not assigned | Assign ContentRoot.prefab in Inspector |
