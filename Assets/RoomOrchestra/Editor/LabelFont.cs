using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// The label font: a static TextMesh Pro atlas holding exactly the characters the controls show in
    /// English and Chinese (<see cref="UiText.AllCharacters"/>), from Fonts/LittleEchoesLabels.ttf
    /// (Noto Sans SC subset, SIL OFL 1.1; see Fonts/LICENSES.md). Static, so nothing is rasterized on
    /// the headset and switching language only changes text.
    /// </summary>
    public static class LabelFont
    {
        public const string SourcePath = "Assets/RoomOrchestra/Fonts/LittleEchoesLabels.ttf";
        public const string AssetPath = "Assets/RoomOrchestra/Fonts/LittleEchoesLabels SDF.asset";

        /// <summary>The font asset, rebuilt when a label character is missing from it.</summary>
        public static TMP_FontAsset Ensure()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (existing != null && Missing(existing).Length == 0) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(AssetPath);

            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            // 2048 x 2048 at 56: story mode's Chinese (2026-11-26) brought the label characters to about 640, more than
            // 2048 x 1024 at 64 holds; this holds about 900. (2048 wide since about 230 characters outgrew 1024 x 1024.)
            var font = TMP_FontAsset.CreateFontAsset(source, 56, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            font.name = "LittleEchoesLabels SDF";
            font.TryAddCharacters(UiText.AllCharacters(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogError($"[RoomOrchestra] Label font lacks: {missing} (re-run ArtSource/Fonts/subset_label_font.py)");
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(font, AssetPath);
            font.atlasTexture.name = "LittleEchoesLabels Atlas";
            font.material.name = "LittleEchoesLabels Material";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        }

        /// <summary>Label characters the font asset cannot draw.</summary>
        public static string Missing(TMP_FontAsset font)
        {
            var missing = new System.Text.StringBuilder();
            foreach (char c in UiText.AllCharacters())
                if (c != ' ' && !font.characterLookupTable.ContainsKey(c)) missing.Append(c);
            return missing.ToString();
        }
    }
}
