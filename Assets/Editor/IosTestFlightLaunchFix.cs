using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulsevania.EditorTools
{
    public sealed class IosTestFlightLaunchFix : IPreprocessBuildWithReport
    {
        private const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        private const string UrpAssetPath = "Assets/Settings/UniversalRP.asset";

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            if (!File.Exists(GameScenePath))
            {
                throw new BuildFailedException("Pulsevania iOS build requires " + GameScenePath);
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.iOS, ManagedStrippingLevel.Disabled);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetArchitecture(BuildTargetGroup.iOS, 1);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;

            RenderPipelineAsset urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (urp != null)
            {
                GraphicsSettings.defaultRenderPipeline = urp;
            }

            Debug.Log("[Pulsevania] iOS TestFlight launch settings applied: SampleScene only, IL2CPP, stripping disabled.");
        }
    }
}
