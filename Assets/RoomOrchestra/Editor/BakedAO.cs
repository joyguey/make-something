using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Materials for meshes that carry ambient occlusion baked in Blender (V3 review: "bake ambient occlusion on your
    /// objects individually"; ArtSource/bake_ao.py, ArtSource/Meadow/build_meadow.py): a copy of the part's own
    /// material on RoomOrchestra/LitBakedAO, same colour, shine and glow, which darkens by the occlusion in the mesh's
    /// vertex colours. Only for meshes that have those colours (a mesh without them keeps its material).
    /// </summary>
    public static class BakedAO
    {
        public const string ShaderName = "RoomOrchestra/LitBakedAO";
        const string MaterialDir = "Assets/RoomOrchestra/Materials";

        public static bool HasBake(Mesh mesh) => mesh != null && mesh.colors32 != null && mesh.colors32.Length == mesh.vertexCount && mesh.vertexCount > 0;

        /// <summary>The baked-AO copy of <paramref name="source"/> (made once, kept in step with it). Only furniture and characters
        /// come here, so each catches the rim light.</summary>
        public static Material Variant(Material source, bool rim = true)
        {
            if (source == null) return null;
            var shader = Shader.Find(ShaderName);
            if (shader == null) return source;
            if (source.shader == shader) // already a baked-AO material (a part built before): it still catches the rim light
            {
                if (source.HasProperty("_RimAmount") && source.GetFloat("_RimAmount") != (rim ? 1f : 0f)) { source.SetFloat("_RimAmount", rim ? 1f : 0f); EditorUtility.SetDirty(source); }
                return source;
            }
            string path = $"{MaterialDir}/{source.name}_AO.mat";
            var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (variant == null)
            {
                variant = new Material(shader);
                AssetDatabase.CreateAsset(variant, path);
            }
            variant.shader = shader;
            if (source.HasProperty("_BaseColor")) variant.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            if (source.HasProperty("_Smoothness")) variant.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
            bool glow = source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor");
            variant.SetColor("_EmissionColor", glow ? source.GetColor("_EmissionColor") : Color.black);
            // furniture and characters' bodies catch the rim light (LookSettings); the meadow does not, nor the small dark
            // features of a face (a pale edge on an eye or a mouth reads as a smudge)
            variant.SetFloat("_RimAmount", rim ? 1f : 0f);
            variant.enableInstancing = true;
            EditorUtility.SetDirty(variant);
            return variant;
        }

        /// <summary>The material for <paramref name="mesh"/>: its baked-AO copy if the mesh carries a bake.</summary>
        public static Material For(Mesh mesh, Material source, bool rim = true) => HasBake(mesh) ? Variant(source, rim) : source;
    }
}
