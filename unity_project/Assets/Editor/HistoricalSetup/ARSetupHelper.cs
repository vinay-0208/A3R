using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEditor.XR.ARSubsystems;
using A3R.ARTracking;

namespace A3R.Editor
{
    public static class ARSetupHelper
    {
        private const string TEXTURE_PATH = "Assets/Textures/A3R_Marker.png";
        private const string LIBRARY_PATH = "Assets/XR/A3R_ReferenceImageLibrary.asset";
        private const string PREFAB_PATH  = "Assets/Prefabs/A3R_TargetIndicatorPrefab.prefab";
        private const string SCENE_PATH   = "Assets/Scenes/A3R_Main.unity";

        [MenuItem("A3R/Setup AR Target and Scene")]
        public static void SetupAR()
        {
            Debug.Log("[ARSetupHelper] Starting AR Pipeline Configuration...");

            // 1. Configure Texture Importer
            ConfigureTexture();

            // 2. Create Reference Image Library
            var library = CreateReferenceImageLibrary();

            // 3. Create 3D Indicator Prefab
            var prefab = CreateIndicatorPrefab();

            // 4. Configure Scene
            ConfigureScene(library, prefab);

            Debug.Log("[ARSetupHelper] ✅ AR Pipeline Configuration Completed Successfully.");
        }

        private static void ConfigureTexture()
        {
            AssetDatabase.ImportAsset(TEXTURE_PATH, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(TEXTURE_PATH) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                Debug.Log("[ARSetupHelper] Configured TextureImporter for A3R_Marker.png");
            }
        }

        private static XRReferenceImageLibrary CreateReferenceImageLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LIBRARY_PATH);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LIBRARY_PATH);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_PATH);
            if (texture == null)
            {
                throw new InvalidOperationException("Could not load texture at " + TEXTURE_PATH);
            }

            if (library.count == 0)
            {
                library.Add();
            }

            library.SetName(0, "A3R_Marker");
            library.SetTexture(0, texture, true);
            library.SetSpecifySize(0, true);
            library.SetSize(0, new Vector2(0.15f, 0.15f)); // 15 cm x 15 cm

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[ARSetupHelper] Created XRReferenceImageLibrary with " + library.count + " image at " + LIBRARY_PATH);
            return library;
        }

        private static GameObject CreateIndicatorPrefab()
        {
            var root = new GameObject("A3R_TargetIndicator");

            // Base plate (15cm x 15cm x 2mm)
            var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePlate.name = "BasePlate";
            basePlate.transform.SetParent(root.transform, false);
            basePlate.transform.localPosition = new Vector3(0, -0.001f, 0);
            basePlate.transform.localScale = new Vector3(0.15f, 0.002f, 0.15f);

            var baseRenderer = basePlate.GetComponent<MeshRenderer>();
            var baseMat = new Material(Shader.Find("Standard")) { color = new Color(0.1f, 0.5f, 0.8f, 0.8f) };
            AssetDatabase.CreateAsset(baseMat, "Assets/Prefabs/BasePlateMat.mat");
            baseRenderer.sharedMaterial = baseMat;

            // Coordinate frame: X (Red), Y (Green), Z (Blue)
            var xMat = new Material(Shader.Find("Standard")) { color = Color.red };
            var yMat = new Material(Shader.Find("Standard")) { color = Color.green };
            var zMat = new Material(Shader.Find("Standard")) { color = Color.blue };
            var beaconMat = new Material(Shader.Find("Standard")) { color = new Color(1.0f, 0.84f, 0.0f) }; // Gold

            AssetDatabase.CreateAsset(xMat, "Assets/Prefabs/AxisXMat.mat");
            AssetDatabase.CreateAsset(yMat, "Assets/Prefabs/AxisYMat.mat");
            AssetDatabase.CreateAsset(zMat, "Assets/Prefabs/AxisZMat.mat");
            AssetDatabase.CreateAsset(beaconMat, "Assets/Prefabs/BeaconMat.mat");

            // X-axis (points right along +X)
            var xAxis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            xAxis.name = "Axis_X_Red";
            xAxis.transform.SetParent(root.transform, false);
            xAxis.transform.localPosition = new Vector3(0.04f, 0.005f, 0);
            xAxis.transform.localRotation = Quaternion.Euler(0, 0, -90);
            xAxis.transform.localScale = new Vector3(0.006f, 0.04f, 0.006f);
            xAxis.GetComponent<MeshRenderer>().sharedMaterial = xMat;

            // Y-axis (points UP normal to marker +Y)
            var yAxis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            yAxis.name = "Axis_Y_Green";
            yAxis.transform.SetParent(root.transform, false);
            yAxis.transform.localPosition = new Vector3(0, 0.045f, 0);
            yAxis.transform.localRotation = Quaternion.identity;
            yAxis.transform.localScale = new Vector3(0.006f, 0.04f, 0.006f);
            yAxis.GetComponent<MeshRenderer>().sharedMaterial = yMat;

            // Z-axis (points forward along +Z)
            var zAxis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zAxis.name = "Axis_Z_Blue";
            zAxis.transform.SetParent(root.transform, false);
            zAxis.transform.localPosition = new Vector3(0, 0.005f, 0.04f);
            zAxis.transform.localRotation = Quaternion.Euler(90, 0, 0);
            zAxis.transform.localScale = new Vector3(0.006f, 0.04f, 0.006f);
            zAxis.GetComponent<MeshRenderer>().sharedMaterial = zMat;

            // Beacon / Target sphere
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "IndicatorBeacon";
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localPosition = new Vector3(0, 0.09f, 0);
            beacon.transform.localScale = new Vector3(0.025f, 0.025f, 0.025f);
            beacon.GetComponent<MeshRenderer>().sharedMaterial = beaconMat;

            // Save Prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            GameObject.DestroyImmediate(root);
            Debug.Log("[ARSetupHelper] Saved 3D indicator prefab at " + PREFAB_PATH);
            return prefab;
        }

        private static void ConfigureScene(XRReferenceImageLibrary library, GameObject indicatorPrefab)
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH);

            // Find XR Origin
            var xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin == null)
            {
                throw new InvalidOperationException("Could not find 'XR Origin' in scene");
            }

            // Ensure ARTrackedImageManager is on XR Origin
            var trackedManager = xrOrigin.GetComponent<ARTrackedImageManager>();
            if (trackedManager == null)
            {
                trackedManager = xrOrigin.AddComponent<ARTrackedImageManager>();
            }

            // Assign reference library via SerializedObject to ensure persistent serialization
            var so = new SerializedObject(trackedManager);
            var libProp = so.FindProperty("m_SerializedLibrary");
            if (libProp != null)
            {
                libProp.objectReferenceValue = library;
            }
            var maxMoving = so.FindProperty("m_MaxNumberOfMovingImages");
            if (maxMoving != null)
            {
                maxMoving.intValue = 1;
            }
            so.ApplyModifiedProperties();

            // Find A3R_Manager
            var a3rManager = GameObject.Find("A3R_Manager");
            if (a3rManager != null)
            {
                // Remove redundant ARTrackedImageManager from A3R_Manager if present
                var oldManager = a3rManager.GetComponent<ARTrackedImageManager>();
                if (oldManager != null)
                {
                    GameObject.DestroyImmediate(oldManager);
                    Debug.Log("[ARSetupHelper] Removed redundant ARTrackedImageManager from A3R_Manager");
                }

                // Configure ARImageTracker
                var imageTracker = a3rManager.GetComponent<ARImageTracker>();
                if (imageTracker != null)
                {
                    var trackerSo = new SerializedObject(imageTracker);
                    var prefabProp = trackerSo.FindProperty("contentRootPrefab");
                    if (prefabProp != null)
                    {
                        prefabProp.objectReferenceValue = indicatorPrefab;
                    }
                    var nameProp = trackerSo.FindProperty("referenceImageName");
                    if (nameProp != null)
                    {
                        nameProp.stringValue = "A3R_Marker";
                    }
                    trackerSo.ApplyModifiedProperties();
                    Debug.Log("[ARSetupHelper] Configured ARImageTracker on A3R_Manager");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ARSetupHelper] Scene saved successfully: " + SCENE_PATH);
        }
    }
}
