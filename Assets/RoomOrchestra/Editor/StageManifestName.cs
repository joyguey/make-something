using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// The app opens straight into its full-room Stage (George, 2026-11-03: a Shared Space window appeared first and
    /// stayed visible all through play). PICO's SpatialAdapter writes the startup container from the project's default
    /// spatial camera configuration, now our Unconstrained one (Settings/RoomOrchestraFullSpace), so the manifest
    /// declares a Stage instead of a window. It names the Stage "0", which Android reads as a number and PICO's system
    /// then sees no name at all ("Key pico.spatial.stage.id expected String but value was a java.lang.Integer").
    /// This runs after PICO's step and gives the Stage a text name.
    /// </summary>
    public class StageManifestName : IPostGenerateGradleAndroidProject
    {
        public const string StageId = "little_echoes_stage";
        const string Numeric = "<meta-data android:name=\"pico.spatial.stage.id\" android:value=\"0\" />";

        public int callbackOrder => 100; // after SpatialAdapterManifestProcessor (0)

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;
            string text = File.ReadAllText(manifestPath);
            if (!text.Contains(Numeric)) return;
            File.WriteAllText(manifestPath, Rename(text));
            Debug.Log($"[RoomOrchestra] Startup Stage named \"{StageId}\"");
        }

        public static string Rename(string manifest) =>
            manifest.Replace(Numeric, $"<meta-data android:name=\"pico.spatial.stage.id\" android:value=\"{StageId}\" />");
    }
}
