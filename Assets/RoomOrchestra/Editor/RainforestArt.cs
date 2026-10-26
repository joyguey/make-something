using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Turns the stage into a magical rainforest: vines with leaves and flowers climbing the furniture,
    /// butterflies with sparkle trails, and twinkling sparkles that flare with the music. Meshes come
    /// from ArtSource/Rainforest/build_rainforest.py (Blender, original shapes in a bright, cute,
    /// outlined 2D-game look). Decoration only: no colliders, so taps and balls pass straight through.
    /// </summary>
    public static class RainforestArt
    {
        const string FbxPath = "Assets/RoomOrchestra/Art/Rainforest/Rainforest.fbx";
        const string MaterialDir = "Assets/RoomOrchestra/Materials";

        // Where each vine grows (stage space): pole axes at floor level, and the top of the chair back.
        static readonly (string name, Vector3 at)[] Vines =
        {
            ("Lamp", new Vector3(1.10f, 0f, 2.30f)),
            ("Stand", new Vector3(0.62f, 0f, 1.15f)),
            ("TableL", new Vector3(-1.615f, 0f, 2.005f)),
            ("TableR", new Vector3(-0.595f, 0f, 2.005f)),
            ("Chair", new Vector3(0f, 1.08f, 2.21f)),
        };

        static readonly (Color wing, Vector3 centre, Vector3 size, float lap, float phase)[] Butterflies =
        {
            (new Color(1.00f, 0.60f, 0.84f), new Vector3(-0.3f, 1.55f, 2.4f), new Vector3(1.4f, 0.25f, 0.45f), 26f, 0.00f), // pink
            (new Color(0.56f, 0.85f, 1.00f), new Vector3(0.4f, 1.75f, 2.6f), new Vector3(1.2f, 0.2f, 0.4f), 31f, 0.35f),   // sky blue
            (new Color(1.00f, 0.89f, 0.42f), new Vector3(0f, 1.35f, 2.9f), new Vector3(1.6f, 0.2f, 0.3f), 22f, 0.70f),     // lemon
        };

        const int TrailDots = 8, SparklesPerInstrument = 5;

        public static bool Available => Mesh("Sparkle") != null;

        public static Transform Build(Transform parent, InstrumentTarget[] targets)
        {
            if (!Available) return null;
            EnsureImport();
            var root = new GameObject("Rainforest").transform;
            root.SetParent(parent, false);

            var stem = Material("Rainforest_Stem", new Color(0.30f, 0.54f, 0.18f));
            var leaf = Material("Rainforest_Leaf", new Color(0.42f, 0.80f, 0.24f));
            var leafRim = Material("Rainforest_LeafRim", new Color(0.17f, 0.40f, 0.12f));
            var petal = Material("Rainforest_Petal", new Color(1.00f, 0.55f, 0.78f), glow: 0.2f);
            var heart = Material("Rainforest_FlowerHeart", new Color(1.00f, 0.85f, 0.29f), glow: 0.4f);
            foreach (var (name, at) in Vines)
            {
                var vine = new GameObject($"Vine_{name}").transform;
                vine.SetParent(root, false);
                vine.localPosition = at;
                Part(vine, $"Vine_{name}_Stem", stem);
                Part(vine, $"Vine_{name}_Leaves", leaf);
                Part(vine, $"Vine_{name}_LeafRims", leafRim);
                Part(vine, $"Vine_{name}_Flowers", petal);
                Part(vine, $"Vine_{name}_FlowerHearts", heart);
            }

            var body = Material("Rainforest_ButterflyBody", new Color(0.36f, 0.23f, 0.45f));
            var wingRim = Material("Rainforest_WingRim", new Color(0.42f, 0.24f, 0.55f));
            var spots = Material("Rainforest_WingSpots", new Color(1f, 1f, 1f), glow: 0.3f);
            var trail = Material("Rainforest_Trail", new Color(1.00f, 0.96f, 0.70f), glow: 0.8f);
            for (int b = 0; b < Butterflies.Length; b++)
            {
                var (wingColor, centre, size, lap, phase) = Butterflies[b];
                var wing = Material($"Rainforest_Wing{b}", wingColor, glow: 0.25f);
                var bf = new GameObject($"Butterfly{b}").transform;
                bf.SetParent(root, false);
                var flier = bf.gameObject.AddComponent<Butterfly>();
                bf.localScale = Vector3.one * 2.2f; // ~22 cm wingspan: readable from the seat
                Part(bf, "Butterfly_Body", body);
                var wings = new Transform[2];
                for (int s = 0; s < 2; s++)
                {
                    string side = s == 0 ? "L" : "R";
                    wings[s] = new GameObject($"Wing{side}").transform;
                    wings[s].SetParent(bf, false);
                    Part(wings[s], $"Butterfly_Wing{side}", wing);
                    Part(wings[s], $"Butterfly_WingRim{side}", wingRim);
                    Part(wings[s], $"Butterfly_Spots{side}", spots);
                }
                var dots = new Transform[TrailDots];
                for (int i = 0; i < TrailDots; i++)
                {
                    dots[i] = Part(root, "Sparkle", trail);
                    dots[i].name = $"Butterfly{b}_Trail{i}";
                    dots[i].localScale = Vector3.one * 0.45f;
                }
                flier.Configure(wings[0], wings[1], dots, centre, size, lap, phase);
                flier.SetWings(Butterfly.RestAngle); // authored resting in a V
                // Authored where the loop starts, with its trail bunched behind it: nothing jumps at startup.
                bf.localPosition = flier.PathAt(0f);
                var ahead = flier.PathAt(0.2f) - flier.PathAt(0f);
                ahead.y *= 0.3f;
                bf.localRotation = Quaternion.LookRotation(ahead, Vector3.up);
                foreach (var d in dots) d.localPosition = bf.localPosition;
                EditorUtility.SetDirty(flier);
            }

            var star = Material("Rainforest_Sparkle", new Color(1.00f, 0.95f, 0.62f), glow: 0.7f);
            var starRim = Material("Rainforest_SparkleRim", new Color(1.00f, 0.75f, 0.25f), glow: 0.3f);
            int seed = 0;
            foreach (var target in targets)
            {
                for (int i = 0; i < SparklesPerInstrument; i++, seed++)
                {
                    // A loose ring around the instrument, behind and above it, facing the player.
                    float a = Mathf.Lerp(-150f, 150f, i / (SparklesPerInstrument - 1f)) * Mathf.Deg2Rad;
                    var at = target.AimPosition + new Vector3(Mathf.Sin(a) * 0.5f, 0.25f + 0.2f * ((seed * 7) % 3), 0.25f + Mathf.Cos(a) * 0.1f);
                    var sparkle = new GameObject($"Sparkle_{target.Instrument}_{i}").transform;
                    sparkle.SetParent(root, false);
                    sparkle.position = at;
                    sparkle.rotation = Quaternion.LookRotation(sparkle.position - Viewer.SeatedEye, Vector3.up);
                    sparkle.localScale = Vector3.one * (0.8f + 0.15f * (seed % 3));
                    Part(sparkle, "Sparkle", star);
                    Part(sparkle, "Sparkle_Rim", starRim);
                    var twinkle = sparkle.gameObject.AddComponent<SparkleTwinkle>();
                    twinkle.Configure(target, seed * 0.37f);
                    EditorUtility.SetDirty(twinkle);
                }
            }
            return root;
        }

        static Mesh Mesh(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Mesh>().FirstOrDefault(m => m.name == name);

        static void EnsureImport()
        {
            if (!(AssetImporter.GetAtPath(FbxPath) is ModelImporter importer)) return;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
        }

        static Transform Part(Transform parent, string mesh, Material material)
        {
            var go = new GameObject(mesh);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        // Matte, bright colours; a soft self-glow on the magical bits (URP keeps _EMISSION only with emissive GI flags).
        static Material Material(string name, Color color, float glow = 0f)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Ball.mat"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
            if (material.HasProperty("_EnvironmentReflections")) material.SetFloat("_EnvironmentReflections", 0f);
            if (glow > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", color * glow);
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
