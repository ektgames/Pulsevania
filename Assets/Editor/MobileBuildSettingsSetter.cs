using UnityEditor;
using UnityEngine;

namespace Pulsevania.Editor
{
    public class MobileBuildSettingsSetter : EditorWindow
    {

        [MenuItem("Pulsevania / Configure Mobile Build Settings")]
        public static void ConfigureMobileBuildSettings()
        {
            Debug.Log("[Pulsevania] Configuring Unity 6 Player Settings for Mobile Export...");

            // 1. Force Landscape orientation
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // 2. Configure Android Build Settings (ARM64 target architecture)
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Debug.Log("[Pulsevania] Android architecture set to ARM64.");

            // 3. Configure iOS Target SDK
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            Debug.Log("[Pulsevania] iOS SDK version set to Device SDK.");

            // 4. Set graphics APIs (Vulkan and GLES3 for Android, Metal for iOS) for modern mobile performance
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true);

            // 5. Assign App Icon programmatically
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/AppIcon.png");
            if (icon != null)
            {
                Texture2D[] icons = new Texture2D[] { icon };
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, icons);
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, icons);
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.iOS, icons);
                Debug.Log("[Pulsevania] Custom app icon successfully assigned to player settings.");
            }
            else
            {
                Debug.LogWarning("[Pulsevania] AppIcon.png not found at Assets/Textures/AppIcon.png.");
            }

            // 6. Select active build targets
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            if (activeTarget != BuildTarget.Android && activeTarget != BuildTarget.iOS)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log("[Pulsevania] Build Target switched to Android.");
            }
            else
            {
                Debug.Log($"[Pulsevania] Active Build Target is already mobile: {activeTarget}.");
            }

            Debug.Log("[Pulsevania] Mobile configurations successfully applied! Ready for mobile builds.");
        }
    }
}
