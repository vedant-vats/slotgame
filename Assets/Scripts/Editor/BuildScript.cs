using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// One-click / command-line WebGL build into Build/WebGL.
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod SlotGame.EditorTools.BuildScript.BuildWebGL
    /// </summary>
    public static class BuildScript
    {
        private const string OutputPath = "Build/WebGL";

        [MenuItem("Tools/Slot Game/Build WebGL")]
        public static void BuildWebGL()
        {
            ConfigurePlayerSettings();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Fail("No scenes in Build Settings. Run Tools > Slot Game > Rebuild Scene first.");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] WebGL build succeeded: {OutputPath} ({summary.totalSize / (1024 * 1024)} MB)");
            }
            else
            {
                Fail($"WebGL build failed: {summary.result}, {summary.totalErrors} errors.");
            }
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Vedant Vats";
            PlayerSettings.productName = "Lucky Slots";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            // Gzip + decompression fallback: small files that still load on any static host
            // (GitHub Pages, python -m http.server) that doesn't send Content-Encoding headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
        }

        private static void Fail(string message)
        {
            Debug.LogError("[BuildScript] " + message);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
