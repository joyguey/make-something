using System.Linq;
using ByteDance.PICO.XR;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// The move from PICO Spatial to PICO XR mode (owner, 2026-11-08), in batch mode with the Editor closed. It does
    /// what PICO Portal's "PICO XR > Apply All" does (PXR_PortalEditor: AppMode XR, no SpatialAdapter, its Spatial
    /// manifests removed, PICO's XR define), turns on the mixed-reality features the game needs (video seethrough,
    /// spatial mesh, scene capture, spatial anchors) and strips the PICO Spatial components from the scene while
    /// that package is still installed, so none is left as a missing script once it is removed.
    /// Batch: Unity -batchmode -quit -executeMethod RoomOrchestra.EditorTools.PicoXrMode.Apply
    /// </summary>
    public static class PicoXrMode
    {
        const string ScenePath = "Assets/RoomOrchestra/Scenes/RoomOrchestra.unity";
        public const string XrDefine = "ENABLE_PICO_XR_SDK", SpatialDefine = "PICO_MS_SDK";
        static readonly string[] SpatialObjects = { "[Building Block] Add Spatial Camera For Volume Space", "Spatial Camera For Volume Space" };
        static readonly string[] SpatialManifests = { "Assets/Plugins/Android/AndroidManifest.xml", "Assets/Plugins/Android/LauncherManifest.xml" };

        public static void Apply()
        {
            int removed = StripSpatialFromScene();
            Settings();
            Debug.Log($"[RoomOrchestra] PICO XR mode applied: {removed} PICO Spatial components and objects removed; defines " +
                      PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android));
        }

        static bool IsSpatial(Component c)
        {
            var type = c.GetType();
            return (type.Namespace ?? "").StartsWith("ByteDance.PICO.SpatialAdapter") || type.Name == "PXR_SpatialCameraSpatialModeSynchronizer";
        }

        static int StripSpatialFromScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (SpatialObjects.Contains(root.name))
                {
                    Object.DestroyImmediate(root);
                    removed++;
                    continue;
                }
                foreach (var c in root.GetComponentsInChildren<Component>(true).Where(c => c != null && IsSpatial(c)).ToArray())
                {
                    Object.DestroyImmediate(c);
                    removed++;
                }
            }
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Select(t => t.gameObject).Where(g => SpatialObjects.Contains(g.name)).ToArray())
            {
                Object.DestroyImmediate(go);
                removed++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return removed;
        }

        /// <summary>PICO's settings for the mixed-reality features the game uses (also kept on by the scene setup).</summary>
        public static void RoomFeatures()
        {
            var config = PXR_ProjectSetting.GetProjectConfig();
            config.videoSeeThrough = true;  // the real room behind the game
            config.spatialMesh = true;      // real walls for the balls, the scan glow, real objects' shapes
            config.sceneCapture = true;     // PICO's Room Capture: walls, tables, chairs, sofas
            config.spatialAnchor = true;    // the places the player picks, remembered between sessions
            config.environmentDepth = true; // PICO's depth sensing: real things hide what is behind them
            config.handTracking = true;     // point and pinch with the hands when the controllers are down
            config.handTrackingSupportType = HandTrackingSupport.ControllersAndHands;
            EditorUtility.SetDirty(config);
            PXR_ProjectSetting.SaveAssets();
        }

        static void Settings()
        {
            var settings = PXR_Settings.GetSettings();
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PXR_Settings>();
                EditorBuildSettings.AddConfigObject("ByteDance.PICO.XR.Settings", settings, true);
            }
            settings.appMode = PXR_Settings.AppMode.XR;
            EditorUtility.SetDirty(settings);

            PXR_ProjectSetting.GetProjectConfig().isSpatialAdapter = false;
            RoomFeatures();

            foreach (var path in SpatialManifests) AssetDatabase.DeleteAsset(path); // PICO Spatial's launcher manifests

            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';')
                .Where(d => d.Length > 0 && d != SpatialDefine).ToList();
            if (!defines.Contains(XrDefine)) defines.Add(XrDefine);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", defines));
            AssetDatabase.SaveAssets();
        }
    }
}
