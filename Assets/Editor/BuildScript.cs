using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pulsevania.EditorTools
{
    public static class BuildScript
    {
        [MenuItem("Pulsevania/Build/iOS (Export Xcode)")]
        public static void BuildiOS()
        {
            string[] scenes = GetBuildScenes();
            string buildPath = "Builds/iOS";

            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            Debug.Log("[BuildScript] Starting iOS build (Xcode export) to path: " + buildPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] iOS build succeeded! Size: {summary.totalSize} bytes, Time: {summary.totalTime}");
            }
            else
            {
                Debug.LogError($"[BuildScript] iOS build failed with status: {summary.result}, Errors: {summary.totalErrors}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static string[] GetBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var scenePaths = new List<string>();

            foreach (var scene in scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    scenePaths.Add(scene.path);
                }
            }

            if (scenePaths.Count == 0)
            {
                string defaultScene = "Assets/Scenes/SampleScene.unity";
                if (File.Exists(defaultScene))
                {
                    scenePaths.Add(defaultScene);
                }
                else
                {
                    string[] foundScenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);
                    if (foundScenes.Length > 0)
                    {
                        scenePaths.Add(foundScenes[0]);
                    }
                }
            }

            return scenePaths.ToArray();
        }
    }
}
