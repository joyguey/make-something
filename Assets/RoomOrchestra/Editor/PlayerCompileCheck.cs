using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Compiles the Android player scripts (no APK) to catch device-only compile errors quickly.
    /// Batch: Unity -batchmode -quit -executeMethod RoomOrchestra.EditorTools.PlayerCompileCheck.CompileAndroid
    /// </summary>
    public static class PlayerCompileCheck
    {
        [MenuItem("RoomOrchestra/Check Android Player Script Compile")]
        public static void CompileAndroid()
        {
            var settings = new ScriptCompilationSettings
            {
                target = BuildTarget.Android,
                group = BuildTargetGroup.Android,
                options = ScriptCompilationOptions.None
            };
            var result = PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/RoomOrchestraPlayerCompile");
            bool ok = result.assemblies != null && result.assemblies.Contains("RoomOrchestra.Runtime.dll");
            Debug.Log($"[RoomOrchestra] Android player compile {(ok ? "OK" : "FAILED")}: {string.Join(", ", result.assemblies)}");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
