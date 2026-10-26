#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEditor.XR.ARSubsystems; // Extension methods: Add, SetTexture, SetName, SetSpecifySize, SetSize

namespace A3R.Editor
{
    /// <summary>
    /// Rebuilds the XRReferenceImageLibrary asset before every build.
    ///
    /// WHY THIS EXISTS:
    /// XRReferenceImageLibrary.m_DataStore is only populated by Unity's Editor API
    /// (XRReferenceImageLibraryExtensions.SetTexture). Manually editing the YAML
    /// leaves m_DataStore.m_Storage = [], so ARCore bakes an empty image database
    /// and Image Count = 0 at runtime. This script fixes that properly.
    ///
    /// APPROACH:
    /// 1. Delete the .asset file ONLY (preserve .meta to keep the same GUID so the
    ///    scene's ARTrackedImageManager reference remains valid).
    /// 2. Create a fresh XRReferenceImageLibrary ScriptableObject at the same path.
    ///    Unity uses the existing .meta GUID.
    /// 3. Call XRReferenceImageLibraryExtensions APIs to add the marker image.
    ///    This triggers Unity's internal processing that fills m_DataStore.
    /// 4. Save and verify.
    /// </summary>
    public static class A3R_LibraryBuilder
    {
        private const string LIBRARY_ASSET_PATH = "Assets/XR/A3R_ReferenceImageLibrary.asset";
        private const string MARKER_TEXTURE_PATH = "Assets/Textures/marker.png";
        private const string MARKER_NAME         = "marker";
        private const float  MARKER_WIDTH        = 0.085f; // 8.5 cm
        private const float  MARKER_HEIGHT       = 0.055f; // 5.5 cm

        /// <summary>
        /// Rebuild the reference image library. Returns true if successful.
        /// Called automatically from BuildAndroidApkStandard before every build.
        /// </summary>
        public static bool RebuildLibrary()
        {
            Debug.Log("[A3R_LIBRARY] ===== REBUILDING XR REFERENCE IMAGE LIBRARY =====");

            // ── Step 1: Fix marker.png import settings ──────────────────────
            var importer = AssetImporter.GetAtPath(MARKER_TEXTURE_PATH) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[A3R_LIBRARY] FAIL: Cannot find TextureImporter for {MARKER_TEXTURE_PATH}. Does the file exist?");
                return false;
            }

            bool needsReimport = false;
            if (importer.textureType != TextureImporterType.Default)
            {
                Debug.LogWarning($"[A3R_LIBRARY] Fixing textureType: {importer.textureType} -> Default");
                importer.textureType = TextureImporterType.Default;
                needsReimport = true;
            }
            if (!importer.isReadable)
            {
                Debug.LogWarning("[A3R_LIBRARY] Fixing isReadable: false -> true");
                importer.isReadable = true;
                needsReimport = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                needsReimport = true;
            }
            if (needsReimport)
            {
                AssetDatabase.ImportAsset(MARKER_TEXTURE_PATH, ImportAssetOptions.ForceUpdate);
                Debug.Log("[A3R_LIBRARY] marker.png reimported with corrected settings.");
            }

            // ── Step 2: Verify texture loads ────────────────────────────────
            var markerTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MARKER_TEXTURE_PATH);
            if (markerTex == null)
            {
                Debug.LogError($"[A3R_LIBRARY] FAIL: LoadAssetAtPath<Texture2D>(\"{MARKER_TEXTURE_PATH}\") returned null after reimport!");
                return false;
            }
            Debug.Log($"[A3R_LIBRARY] PASS: marker.png loaded. {markerTex.width}x{markerTex.height} format={markerTex.format}");

            // ── Step 3: Delete .asset only (preserve .meta / GUID) ──────────
            string fullAssetPath = Path.GetFullPath(LIBRARY_ASSET_PATH.Replace("Assets/",
                Application.dataPath + "/"));

            if (File.Exists(fullAssetPath))
            {
                // Unload from AssetDatabase first
                AssetDatabase.DeleteAsset(LIBRARY_ASSET_PATH);
                AssetDatabase.Refresh();
                Debug.Log("[A3R_LIBRARY] Deleted old library asset (meta/GUID preserved).");
            }

            // ── Step 4: Create fresh XRReferenceImageLibrary ─────────────────
            var lib = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
            lib.name = "A3R_ReferenceImageLibrary";
            AssetDatabase.CreateAsset(lib, LIBRARY_ASSET_PATH);
            Debug.Log($"[A3R_LIBRARY] Created fresh library at {LIBRARY_ASSET_PATH}");

            // ── Step 5: Add marker image via official extension API ───────────
            // XRReferenceImageLibraryExtensions.Add() adds a new empty entry
            lib.Add();
            Debug.Log("[A3R_LIBRARY] Added empty slot to library.");

            // SetName — sets reference image name (must match ARImageTracker.referenceImageName)
            lib.SetName(0, MARKER_NAME);

            // SetTexture — CRITICAL: this triggers Unity's internal image processing
            // and populates m_DataStore so ARCore can build the native database.
            // keepTextureAtRuntime=true means texture is also available at runtime.
            lib.SetTexture(0, markerTex, true);
            Debug.Log($"[A3R_LIBRARY] SetTexture called with {markerTex.name} (keepAtRuntime=true).");

            // SetSpecifySize — required for correct AR pose
            lib.SetSpecifySize(0, true);

            // SetSize — physical dimensions of the marker in meters
            lib.SetSize(0, new Vector2(MARKER_WIDTH, MARKER_HEIGHT));
            Debug.Log($"[A3R_LIBRARY] Physical size set: {MARKER_WIDTH*100f}cm x {MARKER_HEIGHT*100f}cm");

            // ── Step 6: Save ─────────────────────────────────────────────────
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[A3R_LIBRARY] Library saved.");

            // ── Step 7: Verify ────────────────────────────────────────────────
            var verifyLib = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LIBRARY_ASSET_PATH);
            if (verifyLib == null)
            {
                Debug.LogError("[A3R_LIBRARY] FAIL: Cannot reload library after save!");
                return false;
            }

            int count = verifyLib.count;
            Debug.Log($"[A3R_LIBRARY] Verification: library.count = {count}");

            if (count != 1)
            {
                Debug.LogError($"[A3R_LIBRARY] FAIL: Expected 1 image, got {count}!");
                return false;
            }

            var entry = verifyLib[0];
            bool nameOk    = entry.name == MARKER_NAME;
            bool texOk     = entry.texture != null;
            bool sizeOk    = entry.specifySize && entry.size.x > 0 && entry.size.y > 0;
            bool texPathOk = texOk && AssetDatabase.GetAssetPath(entry.texture) == MARKER_TEXTURE_PATH;

            Debug.Log($"[A3R_LIBRARY] Image[0]: name='{entry.name}' nameOk={nameOk}");
            Debug.Log($"[A3R_LIBRARY] Image[0]: texture='{(entry.texture != null ? entry.texture.name : "NULL")}' texOk={texOk} pathOk={texPathOk}");
            Debug.Log($"[A3R_LIBRARY] Image[0]: size={entry.size} sizeOk={sizeOk}");

            bool allOk = nameOk && texOk && sizeOk && texPathOk;

            if (allOk)
            {
                Debug.Log("[A3R_LIBRARY] ✓ LIBRARY VERIFICATION PASSED");
                Debug.Log("================================");
                Debug.Log(" A3R IMAGE LIBRARY VERIFICATION ");
                Debug.Log("================================");
                Debug.Log($" Library:       {LIBRARY_ASSET_PATH}");
                Debug.Log($" Images:        {count}");
                Debug.Log($" Image[0]:      {entry.name}");
                Debug.Log($" Texture:       {MARKER_TEXTURE_PATH}");
                Debug.Log($" Texture Dims:  {(entry.texture != null ? entry.texture.width + "x" + entry.texture.height : "N/A")}");
                Debug.Log($" Physical Size: {MARKER_WIDTH} x {MARKER_HEIGHT}");
                Debug.Log(" VALID:         TRUE");
                Debug.Log("================================");
            }
            else
            {
                Debug.LogError($"[A3R_LIBRARY] FAIL: Verification failed. nameOk={nameOk} texOk={texOk} sizeOk={sizeOk} texPathOk={texPathOk}");
            }

            return allOk;
        }

        [MenuItem("A3R/Rebuild Image Library")]
        public static void RebuildLibraryMenuItem()
        {
            bool ok = RebuildLibrary();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "A3R Image Library",
                    ok ? "Library rebuilt successfully!\nImage Count: 1\nMarker: marker.png" : "Library rebuild FAILED. Check console.",
                    "OK");
            }
        }
    }
}
#endif
