using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.SpatialTracking;
using Unity.XR.CoreUtils;

public class ARCameraFixer
{
    [MenuItem("A3R/Fix AR Camera Pose Driver")]
    public static void FixCamera()
    {
        var scenePath = "Assets/Scenes/A3R_Main.unity";
        var scene = EditorSceneManager.OpenScene(scenePath);
        
        var originObj = GameObject.Find("XR Origin") ?? GameObject.Find("AR Session Origin");
        if (originObj == null)
        {
            var origins = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (origins.Length > 0) originObj = origins[0].gameObject;
        }
        
        if (originObj != null)
        {
            var cam = originObj.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                // Remove existing drivers to ensure only ONE authoritative driver exists
                var inputSystemDriver = cam.GetComponent("UnityEngine.InputSystem.XR.TrackedPoseDriver");
                if (inputSystemDriver != null) Object.DestroyImmediate(inputSystemDriver);
                
                var arpd = cam.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                if (arpd != null) Object.DestroyImmediate(arpd);
                
                var tpd = cam.gameObject.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
                tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
                
                EditorUtility.SetDirty(cam.gameObject);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[ARCameraFixer] Successfully added and configured TrackedPoseDriver on {cam.gameObject.name}");
            }
            else
            {
                Debug.LogError("[ARCameraFixer] Found origin but no Camera!");
            }
        }
        else
        {
            Debug.LogError("[ARCameraFixer] Could not find XROrigin or Camera!");
        }
    }
}
