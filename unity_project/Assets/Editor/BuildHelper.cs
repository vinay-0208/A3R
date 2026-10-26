using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.IO;

namespace A3R.Editor
{
    /// <summary>
    /// BuildHelper — Editor menu for automated Android APK/AAB builds of the A3R project.
    ///
    /// Usage: In Unity Editor, click "A3R" menu → "Build Android APK (Debug)" or "Build Android AAB (Release)".
    ///
    /// All build settings are locked here to prevent accidental mis-configuration:
    ///   - Target platform: Android only
    ///   - Min SDK: 28 (AR Foundation requirement)
    ///   - Architecture: ARM64 only
    ///   - ARCore: enabled
    ///   - Output goes to build/android/ in the project root.
    /// </summary>
    public static class BuildHelper
    {
        private const string MENU_ROOT = "A3R/";
        private const string COMPANY   = "A3R Research Project";
        private const string PRODUCT   = "A3R Affect-Adaptive AR";
        private const string BUNDLE_ID  = "com.a3rproject.affectivear";

        // Scenes to include — must match what's in Build Settings
        private static readonly string[] BUILD_SCENES = new[]
        {
            "Assets/Scenes/A3R_Main.unity",
        };

        // ── Menu Items ───────────────────────────────────────────────────────────
        [MenuItem(MENU_ROOT + "Build Android APK (Debug)")]
        public static void BuildAndroidDebug()
        {
            string outputPath = GetOutputPath("debug", "apk");
            BuildAndroid(BuildOptions.Development | BuildOptions.AllowDebugging,
                         outputPath, ".apk");
        }

        [MenuItem(MENU_ROOT + "Build Android AAB (Release)")]
        public static void BuildAndroidRelease()
        {
            string outputPath = GetOutputPath("release", "aab");
            // AAB required for Google Play; APK sufficient for research sideloading
            EditorUserBuildSettings.buildAppBundle = true;
            BuildAndroid(BuildOptions.None, outputPath, ".aab");
        }

        [MenuItem(MENU_ROOT + "Build Android APK (Standard)")]
        public static void BuildAndroidApkStandard()
        {
            // Ensure Phase 2 scene wiring is verified and saved
            A3R_Phase2Setup.SetupPhase2();

            // CRITICAL: Rebuild the XRReferenceImageLibrary before every build
            // so ARCore bakes a non-empty image database into the APK.
            bool libOk = A3R_LibraryBuilder.RebuildLibrary();
            if (!libOk)
            {
                Debug.LogError("[BuildHelper] ABORTING BUILD: XR Reference Image Library rebuild failed. Check console.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "android"));
            string outputPath = Path.Combine(root, "A3R.apk");
            BuildAndroid(BuildOptions.None, outputPath, ".apk");
        }

        [MenuItem(MENU_ROOT + "Build Android APK (ADB Install)")]
        public static void BuildAndroidAdbInstall()
        {
            string outputPath = GetOutputPath("adb", "apk");
            BuildAndroid(BuildOptions.Development | BuildOptions.AutoRunPlayer,
                         outputPath, ".apk");
        }

        [MenuItem(MENU_ROOT + "Verify Build Settings")]
        public static void VerifyBuildSettings()
        {
            ApplyAndroidBuildSettings();
            Debug.Log("[BuildHelper] Android build settings applied and verified.");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("A3R Build Settings",
                    $"Build settings locked to Android:\n" +
                    $"  Bundle ID: {BUNDLE_ID}\n" +
                    $"  Min SDK: 28\n" +
                    $"  Architecture: ARM64\n" +
                    $"  Scenes: {string.Join(", ", BUILD_SCENES)}\n" +
                    $"\nSettings verified.", "OK");
            }
        }

        // ── Build Implementation ──────────────────────────────────────────────────
        private static void BuildAndroid(BuildOptions options, string outputPath, string ext)
        {
            ApplyAndroidBuildSettings();

            // Ensure output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            Debug.Log($"[BuildHelper] Starting build → {outputPath}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes           = BUILD_SCENES,
                locationPathName = outputPath,
                target           = BuildTarget.Android,
                options          = options,
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildHelper] ✅ Build succeeded in {summary.totalTime.TotalSeconds:F1}s — {outputPath}");
                try
                {
                    string altPath = outputPath.Replace("build" + Path.DirectorySeparatorChar + "android", "builds" + Path.DirectorySeparatorChar + "android");
                    if (altPath != outputPath)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(altPath)!);
                        File.Copy(outputPath, altPath, true);
                        Debug.Log($"[BuildHelper] Copied build artifact to alternate path: {altPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[BuildHelper] Failed to copy to alternate path: {ex.Message}");
                }

                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("A3R Build Complete",
                        $"Build succeeded!\n\nOutput: {outputPath}\nTime: {summary.totalTime.TotalSeconds:F0}s\nSize: {summary.totalSize / 1048576:F1} MB",
                        "OK");
                }
            }
            else
            {
                Debug.LogError($"[BuildHelper] ❌ Build FAILED: {summary.result}");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("A3R Build FAILED",
                        $"Build failed with result: {summary.result}\n\nCheck the Console for errors.",
                        "OK");
                }
            }
        }

        private static void ApplyAndroidBuildSettings()
        {
            // Switch platform to Android (no-op if already Android)
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTargetAsync(
                    BuildTargetGroup.Android, BuildTarget.Android);
            }

            // Player settings
            PlayerSettings.companyName = COMPANY;
            PlayerSettings.productName = PRODUCT;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, BUNDLE_ID);
            PlayerSettings.Android.minSdkVersion   = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,
                                               ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Android,
                                                    ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Scenes
            var editorScenes = new EditorBuildSettingsScene[BUILD_SCENES.Length];
            for (int i = 0; i < BUILD_SCENES.Length; i++)
                editorScenes[i] = new EditorBuildSettingsScene(BUILD_SCENES[i], true);
            EditorBuildSettings.scenes = editorScenes;

            // Configure XR Management for ARCore
            try
            {
                const string xrPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
                var pbt = AssetDatabase.LoadAssetAtPath<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>(xrPath);
                if (pbt == null)
                {
                    pbt = ScriptableObject.CreateInstance<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(pbt, xrPath);
                }
                if (!pbt.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                {
                    pbt.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
                }
                var gen = pbt.SettingsForBuildTarget(BuildTargetGroup.Android);
                if (gen != null && gen.AssignedSettings != null)
                {
                    UnityEditor.XR.Management.Metadata.XRPackageMetadataStore.AssignLoader(
                        gen.AssignedSettings,
                        "UnityEngine.XR.ARCore.ARCoreLoader",
                        BuildTargetGroup.Android);
                }
                EditorUtility.SetDirty(pbt);
                AssetDatabase.SaveAssets();
                Debug.Log("[BuildHelper] XR ARCoreLoader configured for Android build target.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildHelper] Could not configure XR ARCoreLoader: {ex.Message}");
            }

            Debug.Log("[BuildHelper] Android build settings applied.");
        }

        private static string GetOutputPath(string variant, string ext)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string root      = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "android"));
            return Path.Combine(root, $"A3R_{variant}_{timestamp}.{ext}");
        }
    }
}
