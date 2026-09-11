using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulsevania.EditorTools
{
    public sealed class IosTestFlightLaunchFix : IPreprocessBuildWithReport
    {
        private const string BootScenePath = "Assets/Scenes/AppBootstrap.unity";
        private const string GameScenePath = "Assets/Scenes/SampleScene.unity";
        private const string UrpAssetPath = "Assets/Settings/UniversalRP.asset";
        private const string UrpGlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";

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

            SanitizeUrpGlobalSettingsForMobile();

            if (File.Exists(BootScenePath))
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(BootScenePath, true),
                    new EditorBuildSettingsScene(GameScenePath, true)
                };
            }
            else
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene(GameScenePath, true)
                };
            }

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

            Debug.Log("[Pulsevania] iOS TestFlight launch settings applied: AppBootstrap + SampleScene, URP mobile sanitize, IL2CPP.");
        }

        private static void SanitizeUrpGlobalSettingsForMobile()
        {
            if (!File.Exists(UrpGlobalSettingsPath))
            {
                return;
            }

            string text = File.ReadAllText(UrpGlobalSettingsPath);
            string[] forbiddenAssemblies =
            {
                "Unity.UnifiedRayTracing.Runtime",
                "Unity.PathTracing.Runtime",
                "Unity.RenderPipelines.GPUDriven.Runtime"
            };

            bool changed = false;
            foreach (string assembly in forbiddenAssemblies)
            {
                MatchCollection matches = Regex.Matches(
                    text,
                    @"    - rid: (\d+)\r?\n      type: \{class: [^,]+, ns: [^,]+, asm: " + Regex.Escape(assembly) + @"\}",
                    RegexOptions.Multiline);

                foreach (Match match in matches)
                {
                    string rid = match.Groups[1].Value;
                    string withoutListEntry = Regex.Replace(text, @"\r?\n      - rid: " + rid, string.Empty);
                    if (withoutListEntry != text)
                    {
                        text = withoutListEntry;
                        changed = true;
                    }
                }

                string strippedBlocks = Regex.Replace(
                    text,
                    @"    - rid: \d+\r?\n      type: \{class: [^,]+, ns: [^,]+, asm: " + Regex.Escape(assembly) + @"\}[\s\S]*?(?=    - rid: |\z)",
                    string.Empty,
                    RegexOptions.Multiline);
                if (strippedBlocks != text)
                {
                    text = strippedBlocks;
                    changed = true;
                }
            }

            if (changed)
            {
                File.WriteAllText(UrpGlobalSettingsPath, text);
                AssetDatabase.ImportAsset(UrpGlobalSettingsPath);
                Debug.Log("[Pulsevania] Stripped iOS-incompatible URP SerializeReference types from global settings.");
            }
        }
    }
}
