using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Dresses scene objects as Flutter / Bop / Boom from the meshes built by
    /// ArtSource/Characters/build_characters.py (Blender, from the owner-approved reference).
    /// Each part is its own child with a project material, so colours stay editable and face parts
    /// can move for expressions. Built at edit time only: nothing is created at runtime.
    /// </summary>
    public static class CharacterArt
    {
        public const string Dir = "Assets/RoomOrchestra/Art/Characters";
        const string MaterialDir = "Assets/RoomOrchestra/Materials";

        /// <summary>Rim, face (eyes/smile), shine and motif colours sampled from the reference (sRGB).</summary>
        static (Color rim, Color face, Color shine, Color motif) Palette(OrbType type) => type switch
        {
            OrbType.Flutter => (new Color(0.30f, 0.60f, 0.65f), new Color(0.18f, 0.33f, 0.35f), new Color(0.86f, 0.96f, 0.97f), new Color(0.93f, 0.80f, 0.40f)),
            OrbType.Bop => (new Color(0.55f, 0.39f, 0.78f), new Color(0.28f, 0.20f, 0.43f), new Color(0.90f, 0.84f, 0.98f), new Color(0.55f, 0.39f, 0.78f)),
            _ => (new Color(0.75f, 0.53f, 0.26f), new Color(0.44f, 0.28f, 0.15f), new Color(0.96f, 0.86f, 0.66f), new Color(0.75f, 0.53f, 0.26f)),
        };

        static string FbxPath(OrbType type) => $"{Dir}/{type}.fbx";

        public static bool Has(OrbType type) => Part(type, "Body") != null;

        public static Mesh Part(OrbType type, string part)
        {
            string path = FbxPath(type);
            EnsureImportSettings(path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == $"{type}_{part}");
        }

        // Meshes only: project materials colour them, and no animation, cameras or lights come along.
        static void EnsureImportSettings(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
        }

        static Material EnsureMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Ball.mat"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Gives <paramref name="root"/> the character's body mesh and adds the outline, face, shine
        /// and motif as children. The surprised "oh" mouth is parked inside the body until an
        /// expression brings it forward (PICO Spatial draws inactive objects, so parking hides it).
        /// </summary>
        public static void Dress(GameObject root, OrbType type, Material body)
        {
            var (rimColor, faceColor, shineColor, motifColor) = Palette(type);
            var rim = EnsureMaterial($"Rim_{type}", rimColor);
            var face = EnsureMaterial($"Face_{type}", faceColor);
            var shine = EnsureMaterial($"Shine_{type}", shineColor);
            var motif = EnsureMaterial($"Motif_{type}", motifColor);

            if (!root.TryGetComponent<MeshFilter>(out var filter)) filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = Part(type, "Body");
            if (!root.TryGetComponent<MeshRenderer>(out var renderer)) renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = body;

            Child(root.transform, type, "Outline", rim, Vector3.zero);
            Child(root.transform, type, "EyeL", face, Vector3.zero);
            Child(root.transform, type, "EyeR", face, Vector3.zero);
            Child(root.transform, type, "Smile", face, Vector3.zero);
            Child(root.transform, type, "Mouth_O", face, MouthParked);
            Child(root.transform, type, "Highlight", shine, Vector3.zero);
            Child(root.transform, type, "Motif", motif, Vector3.zero);
        }

        /// <summary>Offset that sinks the "oh" mouth (built on the face at z ~ -0.41) wholly inside the body.</summary>
        public static readonly Vector3 MouthParked = new Vector3(0f, 0f, 0.25f);

        static Transform Child(Transform parent, OrbType type, string part, Material material, Vector3 localPosition)
        {
            var go = new GameObject(part);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = Part(type, part);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
