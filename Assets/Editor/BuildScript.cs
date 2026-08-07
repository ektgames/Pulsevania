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

            PlayerSettings.iOS.buildNumber = "7";
            Debug.Log("[BuildScript] Starting iOS build (Xcode export, Build Number: " + PlayerSettings.iOS.buildNumber + ") to path: " + buildPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[BuildScript] Build Summary Result: {summary.result}, Total Errors: {summary.totalErrors}, Time: {summary.totalTime}");

            if (report.steps != null)
            {
                foreach (var step in report.steps)
                {
                    if (step.messages != null)
                    {
                        foreach (var msg in step.messages)
                        {
                            if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            {
                                string errorMsg = $"[BuildScript Error] Step '{step.name}': {msg.content}";
                                Debug.LogError(errorMsg);
                                Console.WriteLine(errorMsg);
                            }
                        }
                    }
                }
            }

            if (summary.result != BuildResult.Succeeded)
            {
                string failMsg = $"[BuildScript] iOS build failed with status: {summary.result}, Errors: {summary.totalErrors}";
                Debug.LogError(failMsg);
                Console.WriteLine(failMsg);
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

            string logScenes = $"[BuildScript] Including {scenePaths.Count} scene(s) in build: " + string.Join(", ", scenePaths);
            Debug.Log(logScenes);
            Console.WriteLine(logScenes);
            return scenePaths.ToArray();
        }
    }
}
