using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// The lesson's meadow (V3 review: "the room environment is just a box, what about a little meadow"): the pieces
    /// modelled in Blender (ArtSource/Meadow/build_meadow.py, AO baked per piece into the vertex colours) laid out
    /// around the stage: grass underfoot, flowers, grass tufts, bushes and rocks around the clearing, trees beyond,
    /// rolling hills further out, clouds overhead and a painted sky. Every piece is a "panel" of the virtual room, so
    /// the meadow comes apart after the lesson exactly as the room did, revealing the player's own room. Seeded: the
    /// same meadow every time.
    /// </summary>
    public static class MeadowArt
    {
        public const string FbxPath = "Assets/RoomOrchestra/Art/Meadow/Meadow.fbx";
        /// <summary>The same pieces in the dusk palette (<see cref="MeadowTimeOfDay"/> swaps them in by the headset's clock).</summary>
        public const string DuskFbxPath = "Assets/RoomOrchestra/Art/Meadow/Meadow_Dusk.fbx";
        const string MaterialDir = "Assets/RoomOrchestra/Materials";
        /// <summary>Kept clear for the lesson's furniture, the leaf and the seat (stage space, x and z).</summary>
        static readonly Rect Clearing = new(-3.0f, -2.0f, 6.0f, 6.2f);
        public const float GroundTile = 2f, GroundRadius = 11f, SkyScale = 0.7f;

        static Dictionary<string, Mesh> s_Meshes, s_DuskMeshes;
        // every piece built, with its dusk mesh (MeadowTimeOfDay), and the clouds (their own look at dusk)
        static readonly List<MeshFilter> s_Filters = new();
        static readonly List<Mesh> s_Day = new(), s_Dusk = new();
        static readonly List<Renderer> s_Clouds = new();

        public static bool Available => System.IO.File.Exists(FbxPath);

        static Mesh M(string name)
        {
            if (s_Meshes == null)
            {
                if (AssetImporter.GetAtPath(FbxPath) is ModelImporter importer && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation))
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.SaveAndReimport();
                }
                s_Meshes = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Mesh>().ToDictionary(m => m.name);
            }
            return s_Meshes.TryGetValue(name, out var mesh) ? mesh : null;
        }

        static Mesh Dusk(string name)
        {
            if (s_DuskMeshes == null)
            {
                if (AssetImporter.GetAtPath(DuskFbxPath) is ModelImporter importer && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation))
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.SaveAndReimport();
                }
                s_DuskMeshes = AssetDatabase.LoadAllAssetsAtPath(DuskFbxPath).OfType<Mesh>().ToDictionary(m => m.name);
            }
            return s_DuskMeshes.TryGetValue(name, out var mesh) ? mesh : null;
        }

        public static Material Lit() => EnsureMaterial("MeadowBakedAO", "RoomOrchestra/LitBakedAO", Color.white);

        // The same look, swaying in the wind (owner, Fable refinement 2026-11-11): how far the top moves (m) and how tall
        // the piece is (m). Grass and flowers, bushes, and trees each have their own.
        static Material Swaying(string name, float amount, float height)
        {
            var m = EnsureMaterial(name, "RoomOrchestra/LitBakedAO", Color.white);
            m.SetFloat("_SwayAmount", amount);
            m.SetFloat("_SwayHeight", height);
            return m;
        }
        static Material Grass() => Swaying("MeadowSwayGrass", 0.035f, 0.35f);
        static Material Shrub() => Swaying("MeadowSwayBush", 0.025f, 0.9f);
        static Material Tree() => Swaying("MeadowSwayTree", 0.06f, 4f);
        static Material Sky() => EnsureMaterial("MeadowSky", "RoomOrchestra/UnlitVertexColor", Color.white);
        // clouds glow softly so their undersides stay light, not storm grey (less at dusk, so they sit in the evening sky)
        static Material CloudLook(bool dusk = false)
        {
            var m = EnsureMaterial(dusk ? "MeadowCloudDusk" : "MeadowCloud", "RoomOrchestra/LitBakedAO", Color.white);
            m.SetColor("_EmissionColor", dusk ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.45f, 0.47f, 0.5f));
            return m;
        }

        static Material EnsureMaterial(string name, string shader, Color colour)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(shader);
            material.SetColor("_BaseColor", colour);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform Piece(string mesh, Transform parent, Vector3 at, float yaw, float scale, Material material, List<Transform> panels)
        {
            var m = M(mesh);
            if (m == null) return null;
            var go = new GameObject(mesh.Replace("Meadow_", ""));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // the meadow casts no shadows on the stage
            panels.Add(go.transform);
            s_Filters.Add(filter);
            s_Day.Add(m);
            s_Dusk.Add(Dusk(mesh) ?? m);
            if (mesh.StartsWith("Meadow_Cloud")) s_Clouds.Add(r);
            return go.transform;
        }

        // A copy of a mesh built in place, moved so its own middle is its origin (saved beside the other meshes).
        static (Mesh mesh, Vector3 middle) Centred(string name)
        {
            var source = M(name);
            var middle = source.bounds.center;
            middle.y = 0f;
            return (CentredCopy(source, name, middle, name), middle);
        }

        static Mesh CentredCopy(Mesh source, string name, Vector3 middle, string assetName)
        {
            string path = $"Assets/RoomOrchestra/Meshes/Meadow/{assetName}.asset";
            System.IO.Directory.CreateDirectory("Assets/RoomOrchestra/Meshes/Meadow");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.vertices = source.vertices.Select(v => v - middle).ToArray();
            mesh.normals = source.normals;
            mesh.colors = source.colors;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static bool InClearing(Vector3 p, float margin) =>
            p.x > Clearing.xMin - margin && p.x < Clearing.xMax + margin && p.z > Clearing.yMin - margin && p.z < Clearing.yMax + margin;

        static Vector3 Around(System.Random rng, float rMin, float rMax, Vector3 centre)
        {
            double a = rng.NextDouble() * Mathf.PI * 2;
            double r = rMin + (rMax - rMin) * System.Math.Sqrt(rng.NextDouble());
            return centre + new Vector3((float)(System.Math.Cos(a) * r), 0f, (float)(System.Math.Sin(a) * r));
        }

        /// <summary>Lays the meadow out under <paramref name="room"/>; returns its pieces (panels), nearest first not required.</summary>
        public static List<Transform> Build(Transform room)
        {
            s_Meshes = null;
            s_DuskMeshes = null;
            s_Filters.Clear();
            s_Day.Clear();
            s_Dusk.Clear();
            s_Clouds.Clear();
            var panels = new List<Transform>();
            if (!Available) return panels;
            var lit = Lit();
            var sky = Sky();
            var rng = new System.Random(20261111);
            var centre = new Vector3(0f, 0f, 1.2f);

            // grass underfoot: the floor's ring sectors, built in place in Blender with one noise across them (soft
            // patches, no checkerboard), each moved to its own middle so the floor still comes apart piece by piece
            M("Meadow_SkyWedge"); // loads the meadow's meshes
            foreach (var name in s_Meshes.Keys.Where(k => k.StartsWith("Meadow_GroundS")).OrderBy(k => k).ToList())
            {
                var (mesh, middle) = Centred(name);
                var piece = Piece(name, room, centre + middle + Vector3.down * 0.005f, 0f, 1f, lit, panels);
                if (piece != null)
                {
                    piece.GetComponent<MeshFilter>().sharedMesh = mesh;
                    int k = s_Filters.Count - 1;
                    s_Day[k] = mesh;
                    var dusk = Dusk(name);
                    s_Dusk[k] = dusk != null ? CentredCopy(dusk, name, middle, name + "_Dusk") : mesh; // the day's middle: the same place
                }
            }
            // flowers and grass tufts, around the clearing
            for (int i = 0; i < 16; i++)
            {
                var at = Around(rng, 3.2f, 8f, centre);
                if (InClearing(at, 0.3f)) continue;
                Piece($"Meadow_Flowers{rng.Next(3)}", room, at, (float)rng.NextDouble() * 360f, 1f, Grass(), panels);
            }
            for (int i = 0; i < 70; i++)
            {
                var at = Around(rng, 2.6f, 9.5f, centre);
                if (InClearing(at, 0f)) continue;
                Piece($"Meadow_Grass{rng.Next(2)}", room, at, (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.6f, Grass(), panels);
            }
            // the bush the friends hide in until Start, beside the leaf (LittleEchoesSceneSetup.FriendsBushAt)
            var hide = Piece("Meadow_Bush2", room, LittleEchoesSceneSetup.FriendsBushAt, 20f, 1.15f, lit, panels);
            if (hide != null) hide.name = "FriendsBush";
            // bushes and rocks at the clearing's edge
            for (int i = 0; i < 11; i++)
            {
                var at = Around(rng, 4.2f, 7f, centre);
                if (InClearing(at, 0.6f)) continue;
                Piece($"Meadow_Bush{rng.Next(3)}", room, at, (float)rng.NextDouble() * 360f, 0.9f + (float)rng.NextDouble() * 0.4f, Shrub(), panels);
            }
            for (int i = 0; i < 7; i++)
            {
                var at = Around(rng, 4f, 9f, centre);
                if (InClearing(at, 0.5f)) continue;
                Piece($"Meadow_Rock{rng.Next(2)}", room, at, (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.7f, lit, panels);
            }
            // trees beyond
            string[] trees = { "Meadow_TreeRound0", "Meadow_TreeRound1", "Meadow_TreePine0", "Meadow_TreePine1", "Meadow_TreeBlossom0" };
            for (int i = 0; i < 16; i++)
            {
                var at = Around(rng, 6.5f, 10.5f, centre);
                Piece(trees[rng.Next(trees.Length)], room, at, (float)rng.NextDouble() * 360f, 0.9f + (float)rng.NextDouble() * 0.5f, Tree(), panels);
            }
            // rolling hills on the horizon
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * 360f + (float)rng.NextDouble() * 20f;
                var at = centre + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (14f + (float)rng.NextDouble() * 4f);
                Piece($"Meadow_Hill{rng.Next(3)}", room, at, a + 90f, 1f, lit, panels);
            }
            // clouds overhead, and the sky
            for (int i = 0; i < 7; i++)
            {
                var at = Around(rng, 11f, 20f, centre) + Vector3.up * (8f + (float)rng.NextDouble() * 4f); // high and far: never a blob overhead
                Piece($"Meadow_Cloud{rng.Next(2)}", room, at, (float)rng.NextDouble() * 360f, 1.2f + (float)rng.NextDouble(), CloudLook(), panels);
            }
            for (int w = 0; w < 8; w++)
                Piece("Meadow_SkyWedge", room, centre, w * 45f, SkyScale, sky, panels);
            // the sun (the moon at dusk), turned to where the scene's light comes from, and the dusk's stars
            var sun = Piece("Meadow_Sun", room, centre, 0f, SkyScale * 0.99f, sky, panels);
            var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null && light != null)
            {
                var drawn = sun.GetComponent<MeshFilter>().sharedMesh.bounds.center + Vector3.up * 2f; // built 2 m under the dome's middle
                sun.rotation = Quaternion.FromToRotation(room.TransformDirection(drawn.normalized), -light.transform.forward) * sun.rotation;
            }
            Piece("Meadow_Stars", room, centre, 0f, SkyScale * 0.99f, sky, panels);

            var clock = room.GetComponent<MeadowTimeOfDay>();
            if (clock == null) clock = room.gameObject.AddComponent<MeadowTimeOfDay>();
            clock.Configure(s_Filters.ToArray(), s_Day.ToArray(), s_Dusk.ToArray(), s_Clouds.ToArray(), CloudLook(), CloudLook(dusk: true));
            return panels;
        }

        /// <summary>The title's wooden sign around the card (a child of it, so it tucks into the cloud with it).</summary>
        public static Transform TitleSign(Transform card, float cardCentreHeight)
        {
            var m = M("Meadow_TitleSign");
            if (m == null) return null;
            var go = new GameObject("TitleSign");
            go.transform.SetParent(card, false);
            // its top rail just above the card, the lower rail below it; stands on the ground behind the card
            go.transform.localPosition = new Vector3(0f, -cardCentreHeight, 0.045f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the vine side toward the player
            go.transform.localScale = new Vector3(1f, 1.173f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = Lit();
            return go.transform;
        }
    }
}
