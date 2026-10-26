#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using A3R.ARTracking;

namespace A3R.Editor
{
    public static class A3R_Phase1Setup
    {
        [MenuItem("A3R/Setup Phase 1 (Markerless)")]
        public static void SetupPhase1()
        {
            var scenePath = "Assets/Scenes/A3R_Main.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            if (!scene.IsValid())
            {
                Debug.LogError($"Could not open {scenePath}");
                return;
            }

            var xrOrigin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogError("No XROrigin found in scene.");
                return;
            }

            // Disable Image Tracking everywhere
            var imageTrackers = Object.FindObjectsByType<ARImageTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in imageTrackers) t.enabled = false;
            
            var imageManagers = Object.FindObjectsByType<ARTrackedImageManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var m in imageManagers) m.enabled = false;

            // Add Phase 1 Controller
            var phase1Controller = xrOrigin.GetComponent<A3R_MarkerlessFoundationController>();
            if (phase1Controller == null)
            {
                phase1Controller = xrOrigin.gameObject.AddComponent<A3R_MarkerlessFoundationController>();
            }
            phase1Controller.enabled = true;

            // Ensure Plane Manager
            var planeManager = xrOrigin.GetComponent<ARPlaneManager>();
            if (planeManager == null)
            {
                planeManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
            }
            planeManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
            planeManager.enabled = true;

            // Ensure Raycast Manager
            var raycastManager = xrOrigin.GetComponent<ARRaycastManager>();
            if (raycastManager == null)
            {
                raycastManager = xrOrigin.gameObject.AddComponent<ARRaycastManager>();
            }
            raycastManager.enabled = true;
            
            // Disable old workbench tracker everywhere
            var workbenchTrackers = Object.FindObjectsByType<ARWorkbenchTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var w in workbenchTrackers) w.enabled = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[A3R] Phase 1 Setup Complete. Scene saved.");
        }
    }
}
#endif
