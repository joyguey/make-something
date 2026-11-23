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

            // The owner's release key (2026-11-22, store-ready): kept outside the project; its password comes from the
            // environment (LITTLE_ECHOES_KEYSTORE_PASS), never from a file in the repo, and is never logged.
            // -releaseKey makes a missing key an error instead of a quiet debug-signed build.
            bool requireKey = args.Contains("-releaseKey");
            string home = System.Environment.GetEnvironmentVariable("HOME") ?? "";
            string keystore = System.Environment.GetEnvironmentVariable("LITTLE_ECHOES_KEYSTORE") ?? System.IO.Path.Combine(home, "Keys/little-echoes-release.keystore");
            string pass = System.Environment.GetEnvironmentVariable("LITTLE_ECHOES_KEYSTORE_PASS");
            bool signed = System.IO.File.Exists(keystore) && !string.IsNullOrEmpty(pass);
            if (requireKey && !signed)
            {
                Debug.LogError($"[RoomOrchestra] -releaseKey: no release key ({(System.IO.File.Exists(keystore) ? "password not set" : "no keystore at " + keystore)})");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            bool hadCustom = PlayerSettings.Android.useCustomKeystore;
            string hadName = PlayerSettings.Android.keystoreName, hadAlias = PlayerSettings.Android.keyaliasName;
            if (signed)
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = pass;
                PlayerSettings.Android.keyaliasName = System.Environment.GetEnvironmentVariable("LITTLE_ECHOES_KEY_ALIAS") ?? "littleechoes";
                PlayerSettings.Android.keyaliasPass = System.Environment.GetEnvironmentVariable("LITTLE_ECHOES_KEY_PASS") ?? pass;
            }
            Debug.Log($"[RoomOrchestra] Signing with {(signed ? "the release key" : "the DEBUG key (not for the store)")}");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Debug.Log($"[RoomOrchestra] Building {path} from scenes: {string.Join(", ", scenes)}");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = path,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                });
            }
            finally
            {
                // nothing about the key stays in ProjectSettings (which is in git): back exactly as it was
                if (signed)
                {
                    PlayerSettings.Android.useCustomKeystore = hadCustom;
                    PlayerSettings.Android.keystoreName = hadName;
                    PlayerSettings.Android.keyaliasName = hadAlias;
                    PlayerSettings.Android.keystorePass = "";
                    PlayerSettings.Android.keyaliasPass = "";
                }
            }
            var summary = report.summary;
            Debug.Log($"[RoomOrchestra] Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
