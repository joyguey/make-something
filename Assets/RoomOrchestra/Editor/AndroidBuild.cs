using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Builds the Android APK from the enabled Build Settings scenes, in PICO XR mode.
    /// Batch: Unity -batchmode -quit -executeMethod RoomOrchestra.EditorTools.AndroidBuild.Build -apkPath Builds/x.apk
    /// </summary>
    public static class AndroidBuild
    {
        public static void Build()
        {
            string path = "Builds/LittleEchoes-dev.apk";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-apkPath") path = args[i + 1];

            string defines = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android);
            if (!defines.Contains("ENABLE_PICO_XR_SDK") || defines.Contains("PICO_MS_SDK"))
            {
                Debug.LogError($"[RoomOrchestra] Android defines are not PICO XR mode ({defines}): run PicoXrMode.Apply");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Debug.Log($"[RoomOrchestra] Building {path} from scenes: {string.Join(", ", scenes)}");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log($"[RoomOrchestra] Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
