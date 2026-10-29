using System.Collections.Generic;
using System.IO;
using ByteDance.PICO.IconConfigurator.Editor;
using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// The app's identity on the PICO OS 6 home screen (Version 1 feedback: "missing a custom icon").
    /// The OS 6 launcher shows a layered spatial icon (background plus two foreground layers that lift on
    /// hover) and ignores the flat Android icon, so a Spatial app without one shows a placeholder.
    /// PICO's Icon Configurator does not support PICO Spatial mode yet, and its Apply also rewrites the
    /// Android manifests that SpatialAdapter's Setup Project check requires to stay stock. So this runs
    /// only the Configurator's resource export (layers, SDF maps, drawables_3d.xml) into its own Android
    /// library, and declares the launcher entries in that library's manifest, which Gradle merges into the
    /// app. The flat icon (Legacy and Round) is the same artwork for anything that shows Android icons.
    /// Layers and artwork: ArtSource/Icon (build_icons.py, add_title.py).
    /// </summary>
    public static class AppIconSetup
    {
        public const string ProductName = "Little Echoes";
        const string ArtDir = "ArtSource/Icon";
        const string FlatIconPath = "Assets/RoomOrchestra/Art/Icon/AppIcon.png";
        const string ConfigGuid = "little-echoes-app-icon";
        const string LibraryManifestPath = "Assets/Plugins/Android/IconConfigurator.androidlib/AndroidManifest.xml";

        [MenuItem("RoomOrchestra/Little Echoes/Apply App Icon")]
        public static void Apply()
        {
            PlayerSettings.productName = ProductName;
            ApplyFlatIcon();
            ApplyLayeredIcon();
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomOrchestra] App icon applied: '{ProductName}', flat icon and layered spatial icon");
        }

        static void ApplyFlatIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(FlatIconPath);
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var kind in new PlatformIconKind[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(UnityEditor.Build.NamedBuildTarget.Android, kind);
                foreach (var size in icons) size.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(UnityEditor.Build.NamedBuildTarget.Android, kind, icons);
            }
        }

        static void ApplyLayeredIcon()
        {
            var config = ScriptableObject.CreateInstance<IconConfiguratorConfigAsset>();
            try
            {
                config.LastMode = IconConfiguratorMode.Manual;
                config.Manual.Layers = new List<IconLayerConfig>
                {
                    Layer(IconLayerKind.Background, "AppIcon_background.png"), // coral and glow
                    Layer(IconLayerKind.Foreground1, "AppIcon_layer_art.png"), // echo ring, leaf, trio, notes
                    Layer(IconLayerKind.Foreground2, "AppIcon_title.png"),     // the title, lifted furthest
                };
                config.Localizations = new List<LocalizationEntry>
                {
                    new LocalizationEntry { LocaleCode = "en-US", AppName = ProductName, IsDefault = true, CanRemove = false },
                };
                var service = new IconApplyService(
                    new IconConfiguratorValidator(),
                    new IconCompositePreviewService(),
                    new List<IIconExportAdapter> { new LayeredIconExportAdapter(), new AndroidAppNameExportAdapter() });
                service.Apply(config, ConfigGuid);
            }
            finally
            {
                foreach (var layer in config.Manual.Layers)
                    if (layer.Texture != null) Object.DestroyImmediate(layer.Texture);
                Object.DestroyImmediate(config);
            }

            // The launcher finds the layers through these two entries (as the Configurator writes them).
            File.WriteAllText(LibraryManifestPath,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.iconfeature.iconconfigurator\">\n" +
                "    <application>\n" +
                "        <meta-data android:name=\"icon.3d.list\" android:resource=\"@array/icon_3d_list\" />\n" +
                "        <meta-data android:name=\"icon.sdf.list\" android:resource=\"@array/icon_sdf_list\" />\n" +
                "    </application>\n" +
                "</manifest>\n");
            AssetDatabase.ImportAsset(LibraryManifestPath);
        }

        static IconLayerConfig Layer(IconLayerKind kind, string file)
        {
            string path = Path.Combine(ArtDir, file);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path)); // readable, uncompressed, as the Configurator imports them
            return new IconLayerConfig
            {
                LayerKind = kind,
                AssetGuid = $"{ConfigGuid}-{kind}",
                AssetPath = path,
                OriginalFileName = file,
                Texture = texture,
            };
        }
    }
}
