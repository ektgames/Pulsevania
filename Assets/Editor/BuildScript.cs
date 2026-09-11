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

            string envBuildNumber = System.Environment.GetEnvironmentVariable("BUILD_NUMBER")
                ?? System.Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            if (!string.IsNullOrEmpty(envBuildNumber))
            {
                PlayerSettings.iOS.buildNumber = envBuildNumber;
            }
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
            string bootScene = "Assets/Scenes/AppBootstrap.unity";
            string gameScene = "Assets/Scenes/SampleScene.unity";
            if (!File.Exists(gameScene))
            {
                throw new FileNotFoundException("iOS build scene missing: " + gameScene);
            }

            if (File.Exists(bootScene))
            {
                string logScenes = "[BuildScript] Including boot + game scenes: " + bootScene + ", " + gameScene;
                Debug.Log(logScenes);
                Console.WriteLine(logScenes);
                return new[] { bootScene, gameScene };
            }

            string logGameOnly = "[BuildScript] Including game scene: " + gameScene;
            Debug.Log(logGameOnly);
            Console.WriteLine(logGameOnly);
            return new[] { gameScene };
        }
    }
}
