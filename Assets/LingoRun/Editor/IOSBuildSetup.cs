#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace LingoRun.EditorTools
{
    /// <summary>
    /// One-click iOS build preparation:
    ///   Tools > LingoRun > Prepare iOS Build
    /// Sets product/bundle info, portrait-only orientation, the app icon, and
    /// adds the runtime-found shaders to Always Included Shaders so device
    /// builds don't strip them (they'd render magenta otherwise).
    /// </summary>
    public static class IOSBuildSetup
    {
        // CHANGE THIS to your own reverse-domain identifier before uploading.
        // It must match the bundle ID you register in App Store Connect.
        const string BundleId = "com.selen.lingorun";

        [MenuItem("Tools/LingoRun/Prepare iOS Build")]
        public static void Prepare()
        {
            PlayerSettings.productName = "LingoRun";
            PlayerSettings.companyName = "Selen";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.buildNumber = "1";

            // Portrait only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Default icon (Apple generates all sizes from Unity's icon set).
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LingoRun/Icons/AppIcon.png");
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
                Debug.Log("[LingoRun] App icon assigned.");
            }
            else
            {
                Debug.LogWarning("[LingoRun] Assets/LingoRun/Icons/AppIcon.png not found — icon not set.");
            }

            // Shaders found via Shader.Find at runtime must be Always Included.
            AddAlwaysIncludedShader("Universal Render Pipeline/Unlit");
            AddAlwaysIncludedShader("Universal Render Pipeline/Particles/Unlit");
            AddAlwaysIncludedShader("Skybox/Procedural");
            AddAlwaysIncludedShader("Sprites/Default"); // trail styles

            AssetDatabase.SaveAssets();
            Debug.Log("[LingoRun] iOS build settings applied. Bundle ID: " + BundleId
                + " — edit IOSBuildSetup.cs to change it.");
        }

#if UNITY_IOS
        /// <summary>Runs automatically after every iOS build: injects the
        /// microphone + speech-recognition usage descriptions (required for
        /// the pronunciation game — the app gets rejected/crashes without
        /// them) and links the Speech/AVFoundation frameworks the
        /// LingoRunSpeech.mm plugin needs.</summary>
        [UnityEditor.Callbacks.PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = System.IO.Path.Combine(buildPath, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSMicrophoneUsageDescription",
                "LingoRun listens to you through the microphone to check your pronunciation during speaking practice.");
            plist.root.SetString("NSSpeechRecognitionUsageDescription",
                "LingoRun uses speech recognition to score your pronunciation practice.");
            plist.WriteToFile(plistPath);

            string projPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(buildPath);
            var proj = new UnityEditor.iOS.Xcode.PBXProject();
            proj.ReadFromFile(projPath);
            string frameworkGuid = proj.GetUnityFrameworkTargetGuid();
            proj.AddFrameworkToProject(frameworkGuid, "Speech.framework", false);
            proj.AddFrameworkToProject(frameworkGuid, "AVFoundation.framework", false);
            proj.AddFrameworkToProject(frameworkGuid, "GameKit.framework", false);
            string mainTargetGuid = proj.GetUnityMainTargetGuid();
            proj.WriteToFile(projPath);

            // Game Center capability + entitlement on the app target
            // (leaderboards/friends — see GameCenterManager).
            var capMgr = new UnityEditor.iOS.Xcode.ProjectCapabilityManager(
                projPath, "Unity-iPhone/lingorun.entitlements", null, mainTargetGuid);
            capMgr.AddGameCenter();
            capMgr.WriteToFile();

            Debug.Log("[LingoRun] iOS post-process: speech permissions, frameworks + Game Center capability added.");
        }
#endif

        static void AddAlwaysIncludedShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[LingoRun] Shader not found: " + shaderName);
                return;
            }

            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null || graphicsSettings.Length == 0) return;

            var so = new SerializedObject(graphicsSettings[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;

            for (int i = 0; i < arr.arraySize; i++)
            {
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    return; // already included
            }

            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            Debug.Log("[LingoRun] Added to Always Included Shaders: " + shaderName);
        }
    }
}
#endif
