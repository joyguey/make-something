using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The world inside story mode's portal (owner, 2026-11-25: "what happened to the aesthetic that I loved from the
    /// preview image? ... please perfect the level 1"). Like the approved preview (ArtSource/StoryMode/
    /// sidescroll_preview.py): a misty meadow seen through a round window, a path running across it, flowers and grass in
    /// front, round, blossom and pine trees behind, hills, drifting clouds, and the great World Tree glowing on the horizon.
    /// Built from the game's own meadow pieces (the opening's, read from the scene, never changed) at a small scale, in 3D
    /// behind the opening: the StoryPortalWorld shader draws it only through the opening (stencil), so it has real depth
    /// as the head moves. It scrolls slowly sideways like a side-scroller (pieces wrap round), so the journey moves.
    /// </summary>
    public sealed class StoryDiorama
    {
        public const float Wrap = 4.2f;      // pieces scroll between -Wrap and +Wrap (portal space, x)
        public const float TravellerSize = 0.11f;
        // the meadow's floor. Just behind the opening it dips down into a hollow, so the ring is green to its bottom however
        // wide it opens (headset 2026-11-25: "cut off or see-through below the bottom"); nothing comes out of the ring
        // toward the player, so cards in front of the portal are never cut by the ground (headset 2026-11-25)
        public const float GroundY = -0.42f;

        readonly List<Transform> m_Scrolling = new();
        readonly List<Material> m_Materials = new();
        readonly List<Transform> m_Sparkles = new();
        public Transform Root { get; private set; }
        public Transform WorldTree { get; private set; }
        readonly Transform[] m_Travellers = new Transform[3];
        Transform m_Instrument;
        Material m_InstrumentMat;
        float m_TreeZ = 5.2f, m_TreeLift, m_Progress, m_Walk;
        const float TreeX = 0.35f;
        public int Pieces => m_Scrolling.Count;
        public bool HasMeadow { get; private set; }

        static Material s_Base;

        static Material Base()
        {
            if (s_Base == null) s_Base = Resources.Load<Material>("StoryPortalWorld");
            return s_Base;
        }

        Material Mat(Color tint, float sway = 0f, float swayHeight = 1f, float glow = 0f, bool unlit = false)
        {
            var b = Base();
            var m = b != null ? new Material(b) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_SwayAmount", sway);
            m.SetFloat("_SwayHeight", swayHeight);
            m.SetFloat("_Glow", glow);
            m.SetFloat("_Unlit", unlit ? 1f : 0f);
            m_Materials.Add(m);
            return m;
        }

        /// <summary>The meadow's pieces from the scene (the opening's meadow), by kind.</summary>
        static Dictionary<string, List<Mesh>> Harvest(bool dusk)
        {
            var kinds = new[] { "TreeRound", "TreePine", "TreeBlossom", "Bush", "Flowers", "Grass", "Rock", "Hill", "Cloud" };
            var found = new Dictionary<string, List<Mesh>>();
            foreach (var k in kinds) found[k] = new List<Mesh>();
            var seen = new HashSet<Mesh>();
            foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = f.sharedMesh;
                if (m == null || !m.name.StartsWith("Meadow_") || m.name.Contains("Dusk") != dusk || !seen.Add(m)) continue;
                foreach (var k in kinds) if (m.name.Contains(k)) { found[k].Add(m); break; }
            }
            foreach (var list in found.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // the same meadow every time
            return found;
        }

        bool m_Dusk, m_Night;

        /// <summary>Builds the meadow: by day (Level 1), or at dusk on the way to the mountains (Level 2: the game's own
        /// dusk pieces, a warm evening sky).</summary>
        public void Build(Transform parent, bool dusk = false, bool night = false)
        {
            m_Dusk = dusk || night; m_Night = night; // Level 3: night at the World Tree (the dusk pieces under the stars)
            dusk = m_Dusk;
            Root = new GameObject("PortalWorld").transform;
            Root.SetParent(parent, false);
            var meshes = Harvest(dusk);
            int found = 0; foreach (var l in meshes.Values) found += l.Count;
            if (found == 0 && dusk) { meshes = Harvest(false); foreach (var l in meshes.Values) found += l.Count; } // no dusk set: the day one
            HasMeadow = found > 0;
            var random = new System.Random(5);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            Mesh Pick(string kind) { var l = meshes[kind]; return l.Count == 0 ? null : l[random.Next(l.Count)]; }

            Sky();
            Ground();

            var still = Mat(Color.white);
            var swayTree = Mat(Color.white, 0.012f, 4f);
            var swayLow = Mat(Color.white, 0.006f, 0.6f);
            var cloudMat = Mat(new Color(1f, 0.97f, 0.92f), unlit: false);

            // the World Tree on the horizon: the journey's goal, still, softly glowing
            var tree = Pick("TreeRound") ?? Pick("TreeBlossom");
            if (tree != null)
            {
                // planted on the ground (headset 2026-11-26: it floated as it came nearer); it never scrolls
                WorldTree = Piece(tree, Root, new Vector3(TreeX, GroundY, 5.2f), 20f, 2.3f, Mat(new Color(0.92f, 0.97f, 0.95f), glow: m_Night ? 0.55f : 0.3f), scroll: false);
                m_TreeLift = WorldTree.localPosition.y - TreeGround(5.2f);
                // its sparkles: little lights drifting round the canopy (drawn only through the opening too)
                var spark = Mat(new Color(1f, 0.96f, 0.8f), glow: 0f, unlit: true);
                var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
                for (int i = 0; i < 26; i++)
                {
                    var go = new GameObject("TreeSparkle") { layer = StoryArt.Layer };
                    go.transform.SetParent(Root, false);
                    go.transform.localPosition = new Vector3(0.35f + R(-0.9f, 0.9f), GroundY + R(1.2f, 2.6f), 5.1f + R(-0.4f, 0.3f));
                    go.transform.localScale = Vector3.one * R(0.018f, 0.035f);
                    if (sphere != null) go.AddComponent<MeshFilter>().sharedMesh = sphere;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = spark; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    m_Sparkles.Add(go.transform);
                }
            }
            // hills far away
            for (int i = 0; i < 6; i++)
            {
                var hill = Pick("Hill"); if (hill == null) break;
                Piece(hill, Root, new Vector3(-Wrap + i * (2f * Wrap / 6f) + R(-0.3f, 0.3f), GroundY + R(-0.05f, 0.05f), R(4.4f, 5.2f)), R(0f, 360f), 0f, still, width: R(1.6f, 2.4f));
            }
            // pines and round trees at the back, round and blossom trees in the middle
            for (int i = 0; i < 16; i++)
            {
                var m = Pick(random.NextDouble() < 0.5 ? "TreePine" : "TreeRound") ?? Pick("TreeRound");
                if (m == null) break;
                Piece(m, Root, new Vector3(R(-Wrap, Wrap), GroundY + 0.02f + R(0f, 0.12f), R(2.6f, 4.2f)), R(0f, 360f), R(0.42f, 0.62f), swayTree);
            }
            for (int i = 0; i < 12; i++)
            {
                var m = Pick(random.NextDouble() < 0.45 ? "TreeBlossom" : "TreeRound") ?? Pick("TreeRound");
                if (m == null) break;
                Piece(m, Root, new Vector3(R(-Wrap, Wrap), GroundY, R(1.45f, 2.4f)), R(0f, 360f), R(0.3f, 0.44f), swayTree);
            }
            // big trees near the front, framing the view like the preview's
            for (int i = 0; i < 7; i++)
            {
                var m = Pick(random.NextDouble() < 0.6 ? "TreeRound" : "TreePine") ?? Pick("TreeRound");
                if (m == null) break;
                Piece(m, Root, new Vector3(-Wrap + i * (2f * Wrap / 7f) + R(-0.25f, 0.25f), GroundY, R(1.5f, 1.9f)), R(0f, 360f), R(0.36f, 0.5f), swayTree);
            }
            // bushes and rocks between the trees and the path
            for (int i = 0; i < 14; i++)
            {
                var m = random.NextDouble() < 0.7 ? Pick("Bush") : Pick("Rock");
                if (m == null) continue;
                Piece(m, Root, new Vector3(R(-Wrap, Wrap), GroundY, R(1.15f, 1.6f)), R(0f, 360f), R(0.07f, 0.13f), m.name.Contains("Bush") ? swayLow : still);
            }
            // flowers and grass along both sides of the path and in front
            for (int i = 0; i < 46; i++)
            {
                bool flower = random.NextDouble() < 0.6;
                var m = flower ? Pick("Flowers") : Pick("Grass");
                if (m == null) continue;
                float z = random.NextDouble() < 0.5 ? R(0.25f, 0.72f) : R(1.08f, 1.3f);
                Piece(m, Root, new Vector3(R(-Wrap, Wrap), GroundY, z), R(0f, 360f), R(0.045f, 0.08f), swayLow);
            }
            // stepping stones along the path (they show the journey moving)
            var stoneMat = Mat(new Color(0.86f, 0.82f, 0.74f));
            for (int i = 0; i < 18; i++)
            {
                var stone = Pick("Rock");
                Transform s;
                if (stone != null) s = Piece(stone, Root, new Vector3(-Wrap + i * (2f * Wrap / 18f), GroundY - 0.005f, R(0.82f, 0.98f)), R(0f, 360f), 0.025f, stoneMat);
                else
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(go.GetComponent<Collider>()); go.layer = StoryArt.Layer;
                    go.transform.SetParent(Root, false);
                    go.transform.localPosition = new Vector3(-Wrap + i * (2f * Wrap / 18f), GroundY, R(0.82f, 0.98f));
                    go.transform.localScale = new Vector3(0.07f, 0.015f, 0.05f);
                    go.GetComponent<MeshRenderer>().sharedMaterial = stoneMat;
                    s = go.transform; m_Scrolling.Add(s);
                }
            }
            // clouds drifting overhead
            for (int i = 0; i < 7; i++)
            {
                var m = Pick("Cloud"); if (m == null) break;
                Piece(m, Root, new Vector3(R(-Wrap, Wrap), R(0.55f, 1.2f), R(2.5f, 5f)), R(0f, 360f), 0f, cloudMat, width: R(0.5f, 0.9f));
            }
            // the night sky's stars (Level 3)
            if (m_Night)
            {
                var star = Mat(new Color(1f, 0.97f, 0.85f), unlit: true);
                var ball = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
                for (int i = 0; i < 70; i++)
                {
                    var go = new GameObject("Star") { layer = StoryArt.Layer };
                    go.transform.SetParent(Root, false);
                    go.transform.localPosition = new Vector3(R(-5f, 5f), R(0.5f, 4f), R(5.5f, 7.5f));
                    go.transform.localScale = Vector3.one * R(0.012f, 0.03f);
                    if (ball != null) go.AddComponent<MeshFilter>().sharedMesh = ball;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = star; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            // Flutter, Bop and Boom on their journey along the path to the World Tree (drawn only through the opening)
            var friends = new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom };
            for (int i = 0; i < 3; i++)
            {
                var f = StoryArt.Friend(friends[i], TravellerSize, Root);
                f.localPosition = new Vector3(-0.42f + i * 0.17f, GroundY, 0.92f);
                StoryArt.ToPortal(f);
                m_Travellers[i] = f;
            }
            SetBloom(0.12f);
            SetLight();
            Debug.Log($"[RoomOrchestra] Story: the portal's meadow, {m_Scrolling.Count} pieces from the game's meadow ({found} kinds of piece found)");
        }

        /// <summary>A meadow piece, <paramref name="height"/> m tall (or <paramref name="width"/> m wide).</summary>
        Transform Piece(Mesh mesh, Transform parent, Vector3 at, float yaw, float height, Material m, bool scroll = true, float width = 0f)
        {
            var go = new GameObject(mesh.name) { layer = StoryArt.Layer };
            go.transform.SetParent(parent, false);
            var b = mesh.bounds;
            float scale = width > 0f ? width / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z)) : height / Mathf.Max(0.01f, b.size.y);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // stand it on the ground: its lowest point at 'at'
            if (Mathf.Abs(at.y - GroundY) < 0.001f) at.y = GroundY + GroundAt(at.x, at.z) - 0.005f; // on the ground, not in it
            go.transform.localPosition = at - go.transform.localRotation * new Vector3(0f, b.min.y * scale, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (scroll) m_Scrolling.Add(go.transform);
            return go.transform;
        }

        void Sky()
        {
            // a dome round the world, warm and hazy at the horizon, soft blue above (unlit, vertex colours)
            const int seg = 32, rings = 12; const float radius = 8f;
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var zenith = m_Night ? new Color(0.06f, 0.08f, 0.22f) : m_Dusk ? new Color(0.33f, 0.3f, 0.55f) : new Color(0.6f, 0.72f, 0.88f);
            var horizon = m_Night ? new Color(0.3f, 0.28f, 0.52f) : m_Dusk ? new Color(1f, 0.62f, 0.45f) : new Color(1f, 0.9f, 0.74f);
            var low = m_Night ? new Color(0.16f, 0.16f, 0.28f) : m_Dusk ? new Color(0.62f, 0.45f, 0.45f) : new Color(0.93f, 0.86f, 0.72f);
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.Lerp(-0.6f, 1f, r / (float)rings) * Mathf.PI * 0.5f;
                for (int s = 0; s <= seg; s++)
                {
                    float lon = s / (float)seg * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)) * radius;
                    v.Add(p + new Vector3(0f, GroundY, 2f));
                    float h = Mathf.Sin(lat);
                    var col = h < 0f ? low : Color.Lerp(horizon, zenith, Mathf.SmoothStep(0f, 1f, h * 1.4f));
                    col.a = 1f; c.Add(col);
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < seg; s++)
            {
                int a = r * (seg + 1) + s, b = a + seg + 1;
                t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 }); // wound to face inward
            }
            var mesh = new Mesh { name = "StorySky" };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var sky = Mat(Color.white, unlit: true); sky.SetFloat("_Cull", 0f); // seen from inside
            Add(mesh, "Sky", sky);
        }

        void Ground()
        {
            // the meadow floor: gentle bumps rising into hills at the back, a sandy path running across
            const int nx = 48, nz = 30; const float x0 = -6f, x1 = 6f, z0 = 0f, z1 = 7.5f;
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)nx), z = Mathf.Lerp(z0, z1, j / (float)nz);
                v.Add(new Vector3(x, GroundY + GroundAt(x, z), z));
                float n = Mathf.PerlinNoise(x * 1.7f, z * 1.7f + 9f);
                var grass = Color.Lerp(new Color(0.36f, 0.62f, 0.26f), new Color(0.56f, 0.76f, 0.3f), n);
                if (z > 3.5f) grass = Color.Lerp(grass, new Color(0.6f, 0.68f, 0.42f), Mathf.Clamp01((z - 3.5f) / 3f));
                var sand = Color.Lerp(new Color(0.93f, 0.84f, 0.66f), new Color(0.86f, 0.76f, 0.58f), n);
                float pathEdge = Mathf.Clamp01(1f - (Mathf.Abs(z - 0.9f) - 0.09f) / 0.03f);
                var col = Color.Lerp(grass, sand, pathEdge);
                col.a = 0.85f + 0.15f * n; // a little baked shade
                c.Add(col);
            }
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + nx + 1;
                t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
            var mesh = new Mesh { name = "StoryGround" };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var ground = Mat(Color.white); ground.SetFloat("_Cull", 0f);
            Add(mesh, "Ground", ground);
        }

        /// <summary>The ground's height above <see cref="GroundY"/> at (x, z): gentle bumps, hills at the back, the path a
        /// little lower. Things stand on it (headset 2026-11-25: the friends were half sunk into the meadow).</summary>
        public static float GroundAt(float x, float z)
        {
            float bump = (Mathf.PerlinNoise(x * 0.6f + 3f, z * 0.6f) - 0.5f) * 0.06f;
            float rise = Mathf.Pow(Mathf.Max(0f, z - 3.2f), 1.5f) * 0.11f;
            float pathDip = Mathf.Abs(z - 0.9f) < 0.12f ? -0.008f : 0f;
            float hollow = Mathf.Pow(Mathf.Max(0f, 0.8f - z), 1.5f) * 0.65f; // the dip just behind the opening
            return bump + rise + pathDip - hollow;
        }

        void Add(Mesh mesh, string name, Material m)
        {
            var go = new GameObject(name) { layer = StoryArt.Layer };
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>The world's own light and haze (shader globals that only these shaders read).</summary>
        void SetLight()
        {
            var into = Root.forward;
            Shader.SetGlobalVector("_StoryPortalCentre", Root.position);
            Shader.SetGlobalVector("_StoryPortalInto", new Vector4(into.x, into.y, into.z, 7f * Root.lossyScale.x));
            var haze = (m_Night ? new Color(0.24f, 0.25f, 0.45f) : m_Dusk ? new Color(0.88f, 0.6f, 0.55f) : new Color(0.96f, 0.88f, 0.75f)).linear;
            Shader.SetGlobalVector("_StoryHaze", new Vector4(haze.r, haze.g, haze.b, 0.5f));
            var sun = (Root.TransformDirection(new Vector3(-0.45f, 0.75f, -0.5f))).normalized; // low, warm, from front-left
            Shader.SetGlobalVector("_StorySunDir", sun);
            Shader.SetGlobalVector("_StorySunColor", (m_Night ? new Color(0.62f, 0.72f, 1f) * 0.75f : m_Dusk ? new Color(1f, 0.68f, 0.48f) : new Color(1f, 0.9f, 0.74f)).linear * 1.05f);
            Shader.SetGlobalVector("_StoryAmbient", (m_Night ? new Color(0.36f, 0.4f, 0.62f) : m_Dusk ? new Color(0.55f, 0.5f, 0.7f) : new Color(0.62f, 0.66f, 0.74f)).linear);
        }

        /// <summary>The level's instrument (its 3D icon from the game's library) on the path ahead of the friends, the
        /// thing they are going to mend: dull at first, glowing as it is restored.</summary>
        public void AddInstrument(InstrumentType instrument)
        {
            var library = InstrumentIconLibrary.Instance;
            var mesh = library != null ? library.MeshFor(instrument) : null;
            if (mesh == null || Root == null) return;
            var look = StoryArt.PortalVersion(library.Look);
            m_InstrumentMat = look != null ? new Material(look) : null;
            if (m_InstrumentMat == null) return;
            m_Materials.Add(m_InstrumentMat);
            m_Instrument = Piece(mesh, Root, new Vector3(0.36f, GroundY, 0.95f), 200f, 0.14f, m_InstrumentMat, scroll: false);
            m_Instrument.name = $"Story_{instrument}";
        }

        /// <summary>How restored the level's instrument is (0..1): it glows gold and bobs gently when whole.</summary>
        public void SetInstrument(float restored)
        {
            if (m_InstrumentMat == null) return;
            m_InstrumentMat.SetFloat("_Glow", Mathf.Clamp01(restored) * 0.45f);
            var c = Color.Lerp(new Color(0.75f, 0.72f, 0.7f), new Color(1f, 0.9f, 0.6f), restored);
            m_InstrumentMat.SetColor("_BaseColor", c);
        }

        /// <summary>How much colour the meadow has back: 0 grey and faded (the Grumble's doing), 1 in full colour.</summary>
        public static void SetBloom(float bloom) => Shader.SetGlobalVector("_StoryBloom", new Vector4(Mathf.Clamp01(bloom), 0f, 0f, 0f));

        /// <summary>How far along the journey (0..1): the World Tree comes nearer.</summary>
        public void SetProgress(float progress)
        {
            m_Progress = Mathf.Clamp01(progress);
            if (WorldTree == null) return;
            float z = Mathf.Lerp(5.2f, 3.3f, m_Progress);
            var p = WorldTree.localPosition;
            float dz = z - m_TreeZ, y = TreeGround(z) + m_TreeLift, dy = y - p.y; // its roots stay on the ground as it nears
            p.z = z; p.y = y; WorldTree.localPosition = p;
            foreach (var s in m_Sparkles) if (s != null) { var q = s.localPosition; q.z += dz; q.y += dy; s.localPosition = q; }
            m_TreeZ = z;
        }

        /// <summary>Where the World Tree's base meets the ground at depth <paramref name="z"/> (a little sunk in, so it is
        /// rooted, never floating).</summary>
        static float TreeGround(float z) => GroundY + GroundAt(TreeX, z) - 0.03f;

        /// <summary>The friends walk (speed > 0, hopping along to the right) or stop and face the player to fight.</summary>
        void Travellers(float speed, float dt)
        {
            m_Walk += speed * dt * 12f;
            for (int i = 0; i < m_Travellers.Length; i++)
            {
                var t = m_Travellers[i]; if (t == null) continue;
                bool walking = speed > 0.01f;
                var p = t.localPosition;
                // a friend copy's pivot is its middle: half its height above the ground under it
                p.y = GroundY + GroundAt(p.x, p.z) + TravellerSize * 0.5f + 0.004f + (walking ? Mathf.Abs(Mathf.Sin(m_Walk + i * 1.3f)) * 0.035f : Mathf.Abs(Mathf.Sin(Time.time * 2f + i)) * 0.006f);
                t.localPosition = p;
                var want = walking ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity; // their faces: toward the way they go, or toward the player
                t.localRotation = Quaternion.Slerp(t.localRotation, want, dt * 5f);
            }
        }

        /// <summary>The journey: pieces slide past (portal-space metres per second) and wrap round.</summary>
        public void Scroll(float speed, float dt)
        {
            if (Root == null) return;
            SetLight();
            Travellers(speed, dt);
            float step = speed * dt;
            for (int i = 0; i < m_Sparkles.Count; i++)
            {
                var s = m_Sparkles[i]; if (s == null) continue;
                var p = s.localPosition; p.y += Mathf.Sin(Time.time * 0.8f + i) * 0.04f * dt; s.localPosition = p;
                s.localScale = Vector3.one * (0.02f + 0.012f * Mathf.Max(0f, Mathf.Sin(Time.time * 2f + i * 1.7f)));
            }
            foreach (var t in m_Scrolling)
            {
                if (t == null) continue;
                var p = t.localPosition;
                p.x -= step * (t.name.Contains("Cloud") ? 0.35f : 1f);
                if (p.x < -Wrap) p.x += 2f * Wrap;
                t.localPosition = p;
            }
        }

        public void Destroy()
        {
            foreach (var m in m_Materials) if (m != null) Object.Destroy(m);
            m_Materials.Clear();
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
