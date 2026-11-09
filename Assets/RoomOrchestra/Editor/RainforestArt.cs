using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Builds the rainforest that grows as music is composed (JungleDirector): vines folded inside the
    /// furniture that creep out with each instrument's notes, butterflies hidden in the lamp shade that
    /// come out for a layered song, toadstool buds that sprout and dance to the beat, and sparkles that
    /// flare with the music. Meshes come from ArtSource/Rainforest/build_rainforest.py (Blender, original
    /// shapes in a bright, cute, outlined 2D-game look). Decoration only: no colliders, so taps and balls
    /// pass straight through. Everything is authored in its waiting pose, so nothing changes at startup.
    /// </summary>
    public static class RainforestArt
    {
        const string FbxPath = "Assets/RoomOrchestra/Art/Rainforest/Rainforest.fbx";
        const string MaterialDir = "Assets/RoomOrchestra/Materials";

        // Where each vine grows, from its piece of furniture's floor position: pole and leg axes; the chair's
        // centre line on its back's mid-plane (its vine climbs the back-left leg). Both table legs feed on udu.
        static readonly (string name, string furniture, Vector3 offset)[] Vines =
        {
            ("Lamp", "Lamp", Vector3.zero),
            ("TableL", "Table", new Vector3(-0.515f, 0f, -0.295f)),
            ("TableR", "Table", new Vector3(0.505f, 0f, -0.295f)),
            ("Chair", "Chair", new Vector3(0f, 0f, 0.235f)),
        };

        // Toadstools around the edge of the stage, in the order they sprout: (position, grown size, cap colour).
        static readonly (Vector3 at, float size, int cap)[] Toadstools =
        {
            (new Vector3(0.5f, 0f, 2.75f), 1.0f, 0),
            (new Vector3(-0.35f, 0f, 2.75f), 0.7f, 1),
            (new Vector3(1.6f, 0f, 2.8f), 1.3f, 2),
            (new Vector3(-1.05f, 0f, 3.0f), 0.9f, 0),
            (new Vector3(-1.95f, 0f, 2.75f), 1.2f, 2),
            (new Vector3(1.95f, 0f, 1.75f), 0.9f, 1),
            (new Vector3(-2.05f, 0f, 1.6f), 1.0f, 0),
        };
        static readonly Color[] CapColours = { new Color(0.93f, 0.27f, 0.24f), new Color(1.0f, 0.55f, 0.74f), new Color(0.68f, 0.5f, 0.95f) };

        static readonly (Color wing, Vector3 centre, Vector3 size, float lap, float phase)[] Butterflies =
        {
            (new Color(1.00f, 0.60f, 0.84f), new Vector3(-0.3f, 1.55f, 2.4f), new Vector3(1.4f, 0.25f, 0.45f), 26f, 0.00f), // pink
            (new Color(0.56f, 0.85f, 1.00f), new Vector3(0.4f, 1.75f, 2.6f), new Vector3(1.2f, 0.2f, 0.4f), 31f, 0.35f),   // sky blue
            (new Color(1.00f, 0.89f, 0.42f), new Vector3(0f, 1.35f, 2.9f), new Vector3(1.6f, 0.2f, 0.3f), 22f, 0.70f),     // lemon
        };

        const int TrailDots = 8, SparklesPerInstrument = 5;

        // Floor flowers scattered round the room (owner 2026-10-28), one more for every note the song adds.
        // A fixed seed, so every build has the same meadow; kept off the furniture, the player's spot, the
        // leaf palette, the ball stand, the toadstools and each other.
        public const int FloorFlowers = 20;
        const int FlowerSeed = 2026;
        static readonly Color[] PetalColours =
        {
            new Color(1.00f, 0.55f, 0.78f), new Color(0.78f, 0.60f, 1.00f), new Color(0.55f, 0.82f, 1.00f),
            new Color(1.00f, 0.62f, 0.45f), new Color(1.00f, 0.97f, 0.90f),
        };

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
            var fbx = (GameObject)AssetDatabase.LoadMainAssetAtPath(FbxPath);
            var grown = new System.Collections.Generic.Dictionary<string, VineGrowth>();
            var furnitureRoot = root.parent != null && root.parent.parent != null ? root.parent.parent.Find("Furniture") : null;
            foreach (var (name, furnitureName, offset) in Vines)
            {
                var host = furnitureRoot != null ? furnitureRoot.Find(furnitureName) : null;
                var at = (host != null ? new Vector3(host.position.x, 0f, host.position.z) : Vector3.zero) + offset;
                var vine = new GameObject($"Vine_{name}").transform;
                vine.SetParent(root, false);
                vine.localPosition = at;
                var pieces = new System.Collections.Generic.List<Transform>();
                for (int k = 0; fbx.transform.Find($"Vine_{name}_Seg{k}_Stem") != null; k++)
                {
                    var piece = Pivot(vine, fbx, $"Vine_{name}_Seg{k}", $"Vine_{name}_Seg{k}_Stem");
                    Part(piece, $"Vine_{name}_Seg{k}_Stem", stem);
                    Part(piece, $"Vine_{name}_Seg{k}_Leaves", leaf);
                    Part(piece, $"Vine_{name}_Seg{k}_LeafRims", leafRim);
                    pieces.Add(piece);
                }
                var bloom = Pivot(vine, fbx, $"Vine_{name}_Bloom", $"Vine_{name}_Flowers");
                Part(bloom, $"Vine_{name}_Flowers", petal);
                Part(bloom, $"Vine_{name}_FlowerHearts", heart);
                var growth = vine.gameObject.AddComponent<VineGrowth>();
                growth.Configure(pieces.ToArray(), new[] { bloom });
                EditorUtility.SetDirty(growth);
                grown[name] = growth;
            }

            var body = Material("Rainforest_ButterflyBody", new Color(0.36f, 0.23f, 0.45f));
            var wingRim = Material("Rainforest_WingRim", new Color(0.42f, 0.24f, 0.55f));
            var spots = Material("Rainforest_WingSpots", new Color(1f, 1f, 1f), glow: 0.3f);
            var trail = Material("Rainforest_Trail", new Color(1.00f, 0.96f, 0.70f), glow: 0.8f);
            var shade = targets.First(t => t.Instrument == InstrumentType.Erhu).GetComponentsInChildren<Renderer>()
                .First(r => r.name == "Shade").bounds.center; // butterflies wait inside the lamp shade
            var flock = new Butterfly[Butterflies.Length];
            // The butterflies and their trails under one holder (their stage), so the guided song can keep the
            // lamp's flock out of sight with the lamp (StationReveal).
            var flockRoot = new GameObject("Flock").transform;
            flockRoot.SetParent(root, false);
            for (int b = 0; b < Butterflies.Length; b++)
            {
                var (wingColor, centre, size, lap, phase) = Butterflies[b];
                var wing = Material($"Rainforest_Wing{b}", wingColor, glow: 0.25f);
                var bf = new GameObject($"Butterfly{b}").transform;
                bf.SetParent(flockRoot, false);
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
                    dots[i] = Part(flockRoot, "Sparkle", trail);
                    dots[i].name = $"Butterfly{b}_Trail{i}";
                    dots[i].localScale = Vector3.one * 0.45f;
                }
                var home = flockRoot.InverseTransformPoint(shade);
                flier.Configure(wings[0], wings[1], dots, centre, size, lap, phase, home);
                flier.SetWings(Butterfly.RestAngle); // authored resting in a V
                // Authored at home inside the lamp shade, trail tucked in with it: hidden until the song calls.
                bf.localPosition = home;
                foreach (var d in dots) d.localPosition = home;
                flock[b] = flier;
                EditorUtility.SetDirty(flier);
            }

            var star = Material("Rainforest_Sparkle", new Color(1.00f, 0.95f, 0.62f), glow: 0.7f);
            var starRim = Material("Rainforest_SparkleRim", new Color(1.00f, 0.75f, 0.25f), glow: 0.3f);
            int seed = 0;
            foreach (var target in targets)
            {
                // Each instrument's sparkles under one holder, to hide and show with it (StationReveal).
                var sparkles = new GameObject($"Sparkles_{target.Instrument}").transform;
                sparkles.SetParent(root, false);
                for (int i = 0; i < SparklesPerInstrument; i++, seed++)
                {
                    // A loose ring around the instrument, behind and above it, facing the player.
                    float a = Mathf.Lerp(-150f, 150f, i / (SparklesPerInstrument - 1f)) * Mathf.Deg2Rad;
                    var at = target.AimPosition + new Vector3(Mathf.Sin(a) * 0.5f, 0.25f + 0.2f * ((seed * 7) % 3), 0.25f + Mathf.Cos(a) * 0.1f);
                    var sparkle = new GameObject($"Sparkle_{target.Instrument}_{i}").transform;
                    sparkle.SetParent(sparkles, false);
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
            var capRim = Material("Rainforest_CapRim", new Color(0.45f, 0.12f, 0.12f));
            var spotsWhite = Material("Rainforest_CapSpots", new Color(1f, 0.98f, 0.95f), glow: 0.15f);
            var stalk = Material("Rainforest_Stalk", new Color(0.99f, 0.93f, 0.8f));
            var stalkRim = Material("Rainforest_StalkRim", new Color(0.55f, 0.4f, 0.3f));
            var sleepy = Material("Rainforest_ToadstoolFace", new Color(0.25f, 0.15f, 0.12f));
            var floor = new Toadstool[Toadstools.Length];
            for (int i = 0; i < Toadstools.Length; i++)
            {
                var (at, size, cap) = Toadstools[i];
                var shroom = new GameObject($"Toadstool{i}").transform;
                shroom.SetParent(root, false);
                shroom.localPosition = at;
                var away = at - Viewer.SeatedEye;
                away.y = 0f;
                shroom.localRotation = Quaternion.LookRotation(away, Vector3.up); // its sleepy face toward the seat
                shroom.localScale = Vector3.one * SproutSync.HiddenScale;          // out of sight until the music grows it
                var look = new GameObject("Visual").transform;
                look.SetParent(shroom, false);
                Part(look, "Toadstool_Cap", Material($"Rainforest_Cap{cap}", CapColours[cap]));
                Part(look, "Toadstool_CapRim", capRim);
                Part(look, "Toadstool_Spots", spotsWhite);
                Part(look, "Toadstool_Stem", stalk);
                Part(look, "Toadstool_StemRim", stalkRim);
                Part(look, "Toadstool_Face", sleepy);
                floor[i] = shroom.gameObject.AddComponent<Toadstool>();
                floor[i].Configure(look, size);
                EditorUtility.SetDirty(floor[i]);
            }

            // Both table legs feed on udu together; one VineGrowth drives both through a shared parent.
            var tableVines = new GameObject("Vine_Table").transform;
            tableVines.SetParent(root, false);
            var tablePieces = new System.Collections.Generic.List<Transform>();
            var tableBlooms = new System.Collections.Generic.List<Transform>();
            foreach (var leg in new[] { "TableL", "TableR" })
            {
                tablePieces.AddRange(grown[leg].Segments);
                tableBlooms.AddRange(grown[leg].Flowers);
                Object.DestroyImmediate(grown[leg]);
            }
            var udu = tableVines.gameObject.AddComponent<VineGrowth>();
            udu.Configure(Interleave(tablePieces, tablePieces.Count / 2), tableBlooms.ToArray());
            EditorUtility.SetDirty(udu);

            var director = root.gameObject.AddComponent<JungleDirector>();
            var meadow = BuildFlowers(root, stem, leaf, leafRim, heart);
            director.Configure(udu, grown["Chair"], grown["Lamp"], floor, flock, meadow); // no ball-stand vine: the stand is a cloud now
            EditorUtility.SetDirty(director);
            return root;
        }

        // Left leg piece 0, right leg piece 0, left 1, right 1, ...: both legs creep up together.
        static Transform[] Interleave(System.Collections.Generic.List<Transform> pieces, int perLeg)
        {
            var result = new Transform[pieces.Count];
            for (int i = 0; i < perLeg; i++)
            {
                result[2 * i] = pieces[i];
                result[2 * i + 1] = pieces[perLeg + i];
            }
            return result;
        }

        /// <summary>A holder at the piece's hiding pivot (inside the furniture), folded to the hidden size.</summary>
        static FloorFlower[] BuildFlowers(Transform root, Material stem, Material leaf, Material leafRim, Material heart)
        {
            if (Mesh("Flower_Petals") == null) return new FloorFlower[0]; // FBX from before the flowers
            var petals = PetalColours.Select((c, i) => Material($"Rainforest_FlowerPetal{i}", c, glow: 0.2f)).ToArray();
            var petalRim = Material("Rainforest_FlowerPetalRim", new Color(0.55f, 0.25f, 0.4f));
            var stage = root.parent != null ? root.parent.parent : null; // RoomOrchestra
            var furniture = stage != null && stage.Find("Furniture") != null
                ? stage.Find("Furniture").GetComponentsInChildren<Collider>().Select(c => c.bounds).ToArray()
                : new Bounds[0];
            var random = new System.Random(FlowerSeed);
            var placed = new System.Collections.Generic.List<Vector3>();
            var flowers = new FloorFlower[FloorFlowers];
            for (int attempt = 0; placed.Count < FloorFlowers && attempt < 5000; attempt++)
            {
                var at = new Vector3(Mathf.Lerp(-2.1f, 2.1f, (float)random.NextDouble()), 0f, Mathf.Lerp(0.6f, 3.2f, (float)random.NextDouble()));
                if (!FlowerFits(at, furniture, placed)) continue;
                int i = placed.Count;
                placed.Add(at);
                var flower = new GameObject($"Flower{i}").transform;
                flower.SetParent(root, false);
                flower.localPosition = at;
                var away = at - Viewer.SeatedEye;
                away.y = 0f;
                flower.localRotation = Quaternion.LookRotation(away, Vector3.up) * Quaternion.Euler(0f, (float)(random.NextDouble() * 50.0 - 25.0), 0f);
                flower.localScale = Vector3.one * SproutSync.HiddenScale; // out of sight until the music grows it
                var look = new GameObject("Visual").transform;
                look.SetParent(flower, false);
                Part(look, "Flower_Stem", stem);
                Part(look, "Flower_Leaves", leaf);
                Part(look, "Flower_LeafRims", leafRim);
                // Petals and bud open and close about the heart's centre: grown as a bud, then in bloom.
                var head = Mesh("Flower_Heart").bounds.center;
                var bloom = new GameObject("Bloom").transform;
                bloom.SetParent(look, false);
                bloom.localPosition = head;
                bloom.localScale = Vector3.one * FloorFlower.ClosedScale;
                Part(bloom, "Flower_Petals", petals[i % petals.Length]).localPosition = -head;
                Part(bloom, "Flower_PetalRims", petalRim).localPosition = -head;
                Part(bloom, "Flower_Heart", heart).localPosition = -head;
                var bud = new GameObject("Bud").transform;
                bud.SetParent(look, false);
                bud.localPosition = head;
                Part(bud, "Flower_Bud", stem).localPosition = -head;
                flowers[i] = flower.gameObject.AddComponent<FloorFlower>();
                flowers[i].Configure(look, 1.2f + 0.6f * (float)random.NextDouble()); // 24-36 cm: readable from the seat
                flowers[i].ConfigureBloom(bloom, bud);
                EditorUtility.SetDirty(flowers[i]);
            }
            return flowers.Where(f => f != null).ToArray();
        }

        static bool FlowerFits(Vector3 at, Bounds[] furniture, System.Collections.Generic.List<Vector3> placed)
        {
            static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
            if (Flat(at, Vector3.zero) < 0.75f) return false;                               // the player's feet and chair
            if (at.x > -0.75f && at.x < 0.35f && at.z > 0.9f && at.z < 1.45f) return false; // under the leaf palette
            if (Flat(at, new Vector3(0.62f, 0f, 1.15f)) < 0.3f) return false;              // below the ball stand
            foreach (var b in furniture)
                if (at.x > b.min.x - 0.15f && at.x < b.max.x + 0.15f && at.z > b.min.z - 0.15f && at.z < b.max.z + 0.15f) return false;
            foreach (var t in Toadstools) if (Flat(at, t.at) < 0.35f) return false;
            foreach (var p in placed) if (Flat(at, p) < 0.35f) return false;
            return true;
        }

        static Transform Pivot(Transform vine, GameObject fbx, string name, string meshObject)
        {
            var source = fbx.transform.Find(meshObject);
            var holder = new GameObject(name).transform;
            holder.SetParent(vine, false);
            // The FBX importer gives each object a 180° turn about Y and a matching mirrored position, while the
            // mesh data is already the right way round (as for the characters): undo the turn on the position
            // and keep the piece unrotated.
            holder.localPosition = Quaternion.Inverse(source.localRotation) * source.localPosition;
            holder.localScale = Vector3.one * VineGrowth.HiddenScale;
            return holder;
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
