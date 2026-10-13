using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pulsevania.EditorTools
{
    public static class LaunchValidation
    {
        public static void ValidateScenes()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int failures = 0;
            try
            {
                foreach (string path in new[] { "Assets/Scenes/AppBootstrap.unity", "Assets/Scenes/SampleScene.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                        if (missing > 0)
                        {
                            Debug.LogError($"[LaunchValidation] {path}: {transform.name}: {missing} missing scripts");
                            failures += missing;
                        }
                        foreach (var component in transform.GetComponents<MonoBehaviour>())
                        {
                            if (component == null) continue;
                            var script = MonoScript.FromMonoBehaviour(component);
                            var resolved = script == null ? null : script.GetClass();
                            string scriptPath = AssetDatabase.GetAssetPath(script);
                            // Scene-embedded MonoScripts can resolve in the editor but lose
                            // their serialized fields during a clean player build.
                            bool persistentScript = scriptPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase)
                                || scriptPath.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase);
                            if (resolved != component.GetType() || !persistentScript)
                            {
                                Debug.LogError($"[LaunchValidation] {path}: {transform.name}: {component.GetType().FullName} resolves to {resolved} via {AssetDatabase.GetAssetPath(script)}");
                                failures++;
                            }
                        }
                    }
                }
            }
            finally
            {
                if (setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            if (failures != 0) throw new BuildFailedException($"Launch validation failed: {failures} invalid script bindings.");
            Debug.Log("[LaunchValidation] Both launch scenes have valid script bindings.");
        }

        public static void ReserializeAndValidate()
        {
            ValidateScenes();
            AssetDatabase.ForceReserializeAssets(new[]
            {
                "Assets/Scenes/AppBootstrap.unity", "Assets/Scenes/SampleScene.unity",
                "Assets/UniversalRenderPipelineGlobalSettings.asset"
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[LaunchValidation] Launch assets reserialized by Unity " + Application.unityVersion);
        }

        public static void BuildWindowsSmokeTest()
        {
            RuntimeVisualsBuilder.Prepare();
            ValidateScenes();
            Directory.CreateDirectory("Builds/LaunchSmoke");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/AppBootstrap.unity", "Assets/Scenes/SampleScene.unity" },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/LaunchSmoke/Pulsevania.exe",
                options = BuildOptions.StrictMode | BuildOptions.CleanBuildCache
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException("Windows launch smoke build failed.");
        }
    }
}
