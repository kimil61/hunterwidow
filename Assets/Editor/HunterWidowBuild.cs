using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HunterWidow.Editor
{
    public static class HunterWidowBuild
    {
        [MenuItem("Hunter Widow/Build Windows MVP")]
        public static void BuildWindowsMvp()
        {
            HunterWidowSceneSetup.EnsureMvpScene();
            var configuredPath = Environment.GetEnvironmentVariable("HUNTERWIDOW_BUILD_PATH");
            var outputPath = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.GetFullPath(Path.Combine("Builds", "HunterWidowMvp.exe"))
                : Path.GetFullPath(configuredPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var scenes = new List<string>();
            for (var sceneIndex = 0; sceneIndex < EditorBuildSettings.scenes.Length; sceneIndex++)
            {
                var scene = EditorBuildSettings.scenes[sceneIndex];
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            if (scenes.Count == 0)
            {
                throw new BuildFailedException("At least one enabled scene is required for the MVP build.");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException("Windows MVP build failed: " + report.summary.result);
            }

            Debug.Log("Hunter Widow MVP build completed: " + outputPath);
        }
    }
}
