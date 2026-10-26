#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using A3R.ARTracking;

namespace A3R.Editor
{
    public static class A3R_Phase2Setup
    {
        [MenuItem("A3R/Setup Phase 2 (Markerless Placement)")]
        public static void SetupPhase2()
        {
            var scenePath = "Assets/Scenes/A3R_Main.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            if (!scene.IsValid())
            {
                Debug.LogError($"[A3R_PHASE2_SETUP] Could not open scene {scenePath}");
                return;
            }

            var xrOrigin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogError("[A3R_PHASE2_SETUP] No XROrigin found in scene!");
                return;
            }

            // 1. Ensure Phase 2 Controller on XROrigin
            var placementController = xrOrigin.GetComponent<A3R_MarkerlessFoundationController>();
            if (placementController == null)
            {
                placementController = xrOrigin.gameObject.AddComponent<A3R_MarkerlessFoundationController>();
                Debug.Log("[A3R_PHASE2_SETUP] Added A3R_MarkerlessFoundationController to XROrigin.");
            }
            placementController.enabled = true;

            // 2. Ensure ARPlaneManager on XROrigin (Horizontal only)
            var planeManager = xrOrigin.GetComponent<ARPlaneManager>();
            if (planeManager == null)
            {
                planeManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
                Debug.Log("[A3R_PHASE2_SETUP] Added ARPlaneManager to XROrigin.");
            }
            planeManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
            planeManager.enabled = true;

            // 3. Ensure ARRaycastManager on XROrigin
            var raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
            if (raycastManager == null)
            {
                raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
                Debug.Log("[A3R_PHASE2_SETUP] Added ARRaycastManager to XROrigin.");
            }
            raycastManager.enabled = true;

            // 4. Archive/Disable legacy image tracking components in the scene
            var imageTrackers = Object.FindObjectsByType<ARImageTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in imageTrackers) t.enabled = false;

            var imageManagers = Object.FindObjectsByType<ARTrackedImageManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in imageManagers) m.enabled = false;

            // 5. Ensure legacy ARWorkbenchTracker cannot compete with Phase 2
            var workbenchTrackers = Object.FindObjectsByType<ARWorkbenchTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var w in workbenchTrackers)
            {
                w.enabled = false;
                w.enableMarkerless = false;
            }

            // 6. Verify single ARSession
            var sessions = Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (sessions.Length > 1)
            {
                Debug.LogWarning($"[A3R_PHASE2_SETUP] Found {sessions.Length} ARSessions. Disabling extra sessions.");
                for (int i = 1; i < sessions.Length; i++) sessions[i].enabled = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[A3R_PHASE2_SETUP] Phase 2 Setup Verified and Scene Saved Successfully.");
        }
    }
}
#endif
