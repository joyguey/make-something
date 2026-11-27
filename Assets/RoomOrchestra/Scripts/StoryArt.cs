using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Story mode's look, all built at runtime (story mode is a separate feature and never edits the scene): the portal
    /// with its painted side-scrolling meadow inside (parallax layers that slide past at different speeds), the
    /// Discords (each kind its own shape, face and motion, never colour alone), the Grumble, stars and signs. The
    /// friends are copies of their real models. Materials copy ones the game already ships with, so their shader
    /// variants are in the build.
    /// </summary>
    public static class StoryArt
    {
        /// <summary>Story mode's own layer (unused by the sandbox). While story plays the headset camera draws only this
        /// layer, so the sandbox is hidden without touching any of its objects (headset 2026-11-25: switching the
        /// sandbox's pictures off one by one lost to scripts that switch theirs back on every frame).</summary>
        public const int Layer = 29;

        /// <summary>Puts <paramref name="go"/> and everything under it on <paramref name="layer"/>.</summary>
        public static void SetLayer(GameObject go, int layer = Layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }

        // ---------- materials ----------
        static Material s_LitTemplate;

        /// <summary>A lit, softly shiny material like the friends' toys.</summary>
        public static Material Lit(Color c, float smooth = 0.45f)
        {
            if (s_LitTemplate == null)
            {
                // the toy buttons' material (render check 2026-11-25: a random lit material could be a glowing one and
                // wash every colour out); else any plain, opaque, non-glowing lit material
                Material fallback = null;
                foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var m = r.sharedMaterial;
                    if (m == null || m.shader == null || m.shader.name != "Universal Render Pipeline/Lit") continue;
                    if (m.name.StartsWith("ToyCap")) { s_LitTemplate = m; break; }
                    if (fallback == null && !m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") && !m.IsKeywordEnabled("_EMISSION") && m.mainTexture == null) fallback = m;
                }
                if (s_LitTemplate == null) s_LitTemplate = fallback;
            }
            Material mat;
            if (s_LitTemplate != null) mat = new Material(s_LitTemplate);
            else { var sh = Shader.Find("Universal Render Pipeline/Lit"); mat = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit")); }
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.DisableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            return mat;
        }

        /// <summary>An unlit (glowing) material, optionally see-through.</summary>
        public static Material Glow(Color c, bool transparent = false, int queueOffset = 0, bool cullFront = false, bool twoSided = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { color = c };
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                // keep the picture's alpha (what PICO uses to show the real room): blend it, never overwrite it. URP's
                // default for alpha is One Zero, so a nearly clear effect (the mist round the head) wiped the alpha to 0 and
                // the real room covered the whole level (headset 2026-11-25: seen only "through the outline of my hand").
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0); m.renderQueue = 3000 + queueOffset; m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            if (cullFront) m.SetFloat("_Cull", 1f);
            if (twoSided) m.SetFloat("_Cull", 0f);
            return m;
        }

        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            if (name != null) go.name = name;
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // ---------- text ----------
        public static TMP_Text Words(Transform parent, string text, Vector3 at, float height, Color color, float width = 1.6f)
        {
            var go = new GameObject("Words") { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>();
            var style = RealFurniture.NoteLabelStyle();
            if (style != null) { t.font = style.font; t.fontSharedMaterial = style.fontSharedMaterial; }
            t.fontSize = height * 10f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(width, height * 6f);
            t.text = text;
            return t;
        }

        // ---------- the friends ----------
        static readonly Dictionary<OrbType, GameObject> s_Friends = new();

        /// <summary>Copies each friend's real model once (from the leaf's tokens), while the sandbox is still showing.</summary>
        public static void PrepareFriends(Transform keep)
        {
            foreach (OrbType friend in new[] { OrbType.Flutter, OrbType.Bop, OrbType.Boom })
            {
                if (s_Friends.TryGetValue(friend, out var have) && have != null) continue;
                s_Friends[friend] = BuildFriend(friend, keep);
            }
        }

        static GameObject BuildFriend(OrbType friend, Transform keep)
        {
            var holder = new GameObject($"StoryFriend_{friend}");
            holder.transform.SetParent(keep, false);
            OrbToken token = null;
            foreach (var t in Object.FindObjectsByType<OrbToken>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.Orb == friend) { token = t; break; }
            var renderers = token != null ? token.GetComponentsInChildren<MeshRenderer>(true) : new MeshRenderer[0];
            if (renderers.Length == 0)
            {
                Part(PrimitiveType.Sphere, holder.transform, Vector3.zero, Vector3.one, Lit(FriendColor(friend)));
            }
            else
            {
                // bounds from the meshes themselves (renderer bounds are empty while a renderer is off)
                var bounds = new Bounds(token.transform.position, Vector3.zero);
                bool first = true;
                foreach (var r in renderers)
                {
                    var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    var b = mf.sharedMesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = r.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((c & 1) * 2 - 1, (c & 2) - 1, ((c & 4) >> 1) - 1)));
                        if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; } else bounds.Encapsulate(corner);
                    }
                }
                float k = 1f / Mathf.Max(0.01f, bounds.size.y);
                var inv = Quaternion.Inverse(token.transform.rotation);
                foreach (var r in renderers)
                {
                    var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                    var part = new GameObject(r.name).transform;
                    part.SetParent(holder.transform, false);
                    part.localPosition = inv * (r.transform.position - bounds.center) * k;
                    part.localRotation = inv * r.transform.rotation;
                    part.localScale = r.transform.lossyScale * k;
                    part.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var mr = part.gameObject.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = r.sharedMaterials;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            // the friend's own face, alive: it blinks, beams and looks surprised (headset 2026-11-25: "give them the
            // animated faces as well"). The same CharacterFace the sandbox uses, on the copy's own eye and mouth parts.
            Transform Find(string n) { var t = holder.transform.Find(n); return t; }
            var eyeL = Find("EyeL"); var eyeR = Find("EyeR"); var smile = Find("Smile");
            if (eyeL != null && eyeR != null && smile != null) holder.AddComponent<CharacterFace>().Configure(eyeL, eyeR, smile, Find("Mouth_O"));
            holder.SetActive(false);
            return holder;
        }

        /// <summary>A copy of a friend, <paramref name="size"/> m tall. Like the leaf's token its face is on its -z side:
        /// turn its +z away from whoever should see the face.</summary>
        public static Transform Friend(OrbType friend, float size, Transform parent)
        {
            Transform t;
            if (s_Friends.TryGetValue(friend, out var src) && src != null)
            {
                t = Object.Instantiate(src, parent).transform;
                t.gameObject.SetActive(true);
            }
            else
            {
                t = new GameObject($"StoryFriend_{friend}").transform;
                t.SetParent(parent, false);
                Part(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one, Lit(FriendColor(friend)));
            }
            t.name = $"Friend_{friend}";
            SetLayer(t.gameObject);
            t.localScale = Vector3.one * size;
            return t;
        }

        /// <summary>Each friend's colour (with its shape: Flutter tall, Bop round, Boom wide).</summary>
        public static Color FriendColor(OrbType f) => f == OrbType.Flutter ? new Color(0.25f, 0.85f, 0.82f) : f == OrbType.Bop ? new Color(0.62f, 0.42f, 0.95f) : new Color(1f, 0.6f, 0.2f);

        // ---------- the portal ----------
        public sealed class Portal
        {
            public Transform Root;
            public LineRenderer Ring, Halo, HarmonyArc, DiscordArc;
            public Transform[] Leaves;
        }

        /// <summary>A disc (circle of triangles) with UVs across it: u 0..1 left to right, v 0..1 bottom to top.</summary>
        static Mesh Disc(int segments = 64)
        {
            var v = new Vector3[segments + 1]; var uv = new Vector2[segments + 1]; var tri = new int[segments * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f;
                uv[i + 1] = new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f);
                tri[i * 3] = 0; tri[i * 3 + 1] = (i + 1) % segments + 1; tri[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { name = "StoryDisc", vertices = v, uv = uv, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public static LineRenderer Circle(Transform parent, string name, float radius, float width, Color c, int queueOffset, float from = 0f, float to = 1f, int points = 72)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = from <= 0f && to >= 1f; lr.positionCount = points;
            lr.widthMultiplier = width; lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = Glow(c, transparent: c.a < 1f, queueOffset: queueOffset);
            lr.startColor = lr.endColor = c;
            SetArc(lr, radius, from, to);
            return lr;
        }

        /// <summary>Draws <paramref name="lr"/> as an arc of a circle from fraction <paramref name="from"/> to <paramref name="to"/>
        /// (0 = the bottom, going round toward the left as seen by the player).</summary>
        public static void SetArc(LineRenderer lr, float radius, float from, float to)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float f = lr.loop ? i / (float)n : Mathf.Lerp(from, to, i / (float)(n - 1));
                float a = -Mathf.PI * 0.5f + f * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }
        }

        /// <summary>The portal: its painted meadow (five layers) inside a glowing ring with leaves. Give it the rotation
        /// LookRotation(away from the player): the player is on its -z side and its +x is the player's right.</summary>
        public static Portal BuildPortal(Transform parent)
        {
            var p = new Portal { Root = new GameObject("Portal").transform };
            p.Root.SetParent(parent, false);
            // the opening: marks where the world behind may be seen (StoryPortalMask); the world itself is StoryDiorama
            var opening = new GameObject("Opening") { layer = Layer };
            opening.transform.SetParent(p.Root, false);
            opening.AddComponent<MeshFilter>().sharedMesh = Disc();
            var or = opening.AddComponent<MeshRenderer>();
            or.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mask = Resources.Load<Material>("StoryPortalMask");
            or.sharedMaterial = mask != null ? mask : Glow(new Color(0.9f, 0.9f, 0.85f));
            // like the approved preview: a bright, white glowing ring with a soft warm halo, cream petals and sparkles round it
            p.Halo = Circle(p.Root, "Halo", 0.515f, 0.075f, new Color(1f, 0.95f, 0.82f, 0.35f), 10);
            p.Ring = Circle(p.Root, "Ring", 0.5f, 0.028f, new Color(1f, 0.98f, 0.93f), 0);
            Circle(p.Root, "InnerGlow", 0.488f, 0.012f, new Color(1f, 0.95f, 0.82f, 0.6f), 11);
            p.HarmonyArc = Circle(p.Root, "HarmonyArc", 0.58f, 0.024f, new Color(1f, 0.78f, 0.25f), 0, 0f, 0.001f, 40);
            p.DiscordArc = Circle(p.Root, "DiscordArc", 0.58f, 0.024f, new Color(0.55f, 0.5f, 0.62f), 0, 0f, 0.001f, 40);
            foreach (var lr in new[] { p.Halo, p.Ring, p.HarmonyArc, p.DiscordArc }) lr.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            var petal = Lit(new Color(0.95f, 0.87f, 0.7f), 0.55f);
            var bead = Glow(new Color(1f, 0.98f, 0.9f));
            var leaves = new List<Transform>();
            var random = new System.Random(3);
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f + (float)random.NextDouble() * 0.12f;
                float r = 0.5f + ((float)random.NextDouble() - 0.4f) * 0.05f;
                var at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r + new Vector3(0f, 0f, -0.02f + (float)random.NextDouble() * 0.03f);
                var leaf = Part(PrimitiveType.Sphere, p.Root, at, new Vector3(0.034f, 0.016f, 0.006f), petal, "Petal");
                leaf.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f + ((float)random.NextDouble() - 0.5f) * 70f);
                leaves.Add(leaf.transform);
                if (i % 4 == 1) Part(PrimitiveType.Sphere, p.Root, at * 1.06f + new Vector3(0f, 0f, -0.01f), Vector3.one * 0.011f, bead, "Sparkle");
            }
            p.Leaves = leaves.ToArray();
            return p;
        }

        // ---------- the world behind the portal ----------
        static readonly Dictionary<Material, Material> s_PortalVersions = new();
        static Material s_PortalBase;

        /// <summary>The same look drawn only through the portal's opening (StoryPortalWorld, stencil): for things that are
        /// inside the meadow (Discords walking up the path, the friends on their journey).</summary>
        public static Material PortalVersion(Material src)
        {
            if (src == null) return null;
            if (s_PortalVersions.TryGetValue(src, out var have) && have != null) return have;
            if (s_PortalBase == null) s_PortalBase = Resources.Load<Material>("StoryPortalWorld");
            if (s_PortalBase == null) return src;
            var m = new Material(s_PortalBase);
            var c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.color : Color.white;
            c.a = 1f;
            m.SetColor("_BaseColor", c);
            bool unlit = src.shader != null && src.shader.name.Contains("Unlit");
            m.SetFloat("_Unlit", unlit ? 1f : 0f);
            m.SetFloat("_AOStrength", 0.4f);
            s_PortalVersions[src] = m;
            return m;
        }

        /// <summary>Puts everything under <paramref name="root"/> into the portal's world; returns how to put it back.</summary>
        public static List<(Renderer r, Material[] mats)> ToPortal(Transform root)
        {
            var back = new List<(Renderer, Material[])>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is TrailRenderer || r is LineRenderer || r.GetComponent<TMPro.TMP_Text>() != null) continue;
                var mats = r.sharedMaterials;
                back.Add((r, mats));
                var portal = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++) portal[i] = PortalVersion(mats[i]);
                r.sharedMaterials = portal;
            }
            return back;
        }

        public static void Restore(List<(Renderer r, Material[] mats)> back)
        {
            if (back == null) return;
            foreach (var (r, mats) in back) if (r != null) r.sharedMaterials = mats;
        }

        // ---------- the Discords ----------
        public static readonly Color DiscordBody = new Color(0.36f, 0.3f, 0.42f);

        /// <summary>A Discord: grumpy noise made solid. Squeak: small, spiky, with buzzing wings. Wobbler: a tall
        /// jelly that sways. Clang: a square body in bolted armour. Each wears a little badge of the friend that beats it
        /// (shape, not only colour). Returns the root; the body to animate is its first child.</summary>
        public static Transform Discord(DiscordKind kind, Transform parent, out Transform body, out Transform[] wings)
        {
            var root = new GameObject($"Discord_{kind}").transform;
            root.SetParent(parent, false);
            body = new GameObject("Body").transform;
            body.SetParent(root, false);
            wings = null;
            var skin = Lit(kind == DiscordKind.Squeak ? new Color(0.55f, 0.3f, 0.5f) : kind == DiscordKind.Wobbler ? new Color(0.35f, 0.45f, 0.38f) : new Color(0.4f, 0.4f, 0.46f), 0.35f);
            var dark = Lit(new Color(0.18f, 0.15f, 0.22f), 0.2f);
            switch (kind)
            {
                case DiscordKind.Squeak:
                    Part(PrimitiveType.Sphere, body, Vector3.zero, Vector3.one * 0.09f, skin);
                    for (int i = 0; i < 8; i++) // spikes
                    {
                        var dir = Quaternion.Euler(i * 45f, i * 67f, 0f) * Vector3.up;
                        var spike = Part(PrimitiveType.Cube, body, dir * 0.048f, new Vector3(0.012f, 0.03f, 0.012f), dark);
                        spike.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, 45f, 0f);
                    }
                    var wingMat = Glow(new Color(0.9f, 0.85f, 1f, 0.6f), transparent: true, twoSided: true);
                    wings = new Transform[2];
                    for (int s = 0; s < 2; s++)
                    {
                        var hinge = new GameObject("Wing").transform; hinge.SetParent(body, false);
                        hinge.localPosition = new Vector3((s * 2 - 1) * 0.035f, 0.03f, 0.01f);
                        Part(PrimitiveType.Sphere, hinge, new Vector3((s * 2 - 1) * 0.035f, 0.01f, 0f), new Vector3(0.07f, 0.035f, 0.004f), wingMat);
                        wings[s] = hinge;
                    }
                    Face(body, 0.045f, 0.022f, 0.016f);
                    break;
                case DiscordKind.Wobbler:
                    Part(PrimitiveType.Capsule, body, new Vector3(0f, 0.02f, 0f), new Vector3(0.1f, 0.085f, 0.1f), skin);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.105f, 0f), new Vector3(0.03f, 0.03f, 0.03f), dark); // a bobble on top
                    Part(PrimitiveType.Cube, body, new Vector3(0f, 0.09f, 0f), new Vector3(0.006f, 0.03f, 0.006f), dark);
                    Face(body, 0.05f, 0.05f, 0.018f);
                    break;
                default:
                    Part(PrimitiveType.Cube, body, Vector3.zero, Vector3.one * 0.12f, skin);
                    var plate = Lit(new Color(0.62f, 0.64f, 0.7f), 0.8f);
                    Part(PrimitiveType.Cube, body, new Vector3(0f, 0.045f, -0.062f), new Vector3(0.13f, 0.035f, 0.01f), plate); // a helmet band
                    Part(PrimitiveType.Cube, body, new Vector3(0f, -0.045f, -0.062f), new Vector3(0.13f, 0.03f, 0.01f), plate);
                    for (int i = 0; i < 4; i++)
                        Part(PrimitiveType.Sphere, body, new Vector3((i & 1) == 0 ? -0.055f : 0.055f, (i & 2) == 0 ? -0.045f : 0.045f, -0.068f), Vector3.one * 0.014f, dark);
                    Face(body, 0.062f, 0.005f, 0.02f);
                    break;
            }
            return root;
        }

        /// <summary>Grumpy eyes (white, with dark pupils and slanted brows) and a frown, on the -z side (toward the player).</summary>
        public static void Face(Transform body, float front, float up, float eye)
        {
            var white = Glow(new Color(0.98f, 0.97f, 0.95f));
            var pupil = Glow(new Color(0.1f, 0.08f, 0.12f));
            for (int s = 0; s < 2; s++)
            {
                float x = (s * 2 - 1) * eye * 1.1f;
                Part(PrimitiveType.Sphere, body, new Vector3(x, up, -front), new Vector3(eye, eye, eye * 0.5f), white);
                Part(PrimitiveType.Sphere, body, new Vector3(x * 0.9f, up - eye * 0.1f, -front - eye * 0.2f), Vector3.one * eye * 0.45f, pupil);
                var brow = Part(PrimitiveType.Cube, body, new Vector3(x, up + eye * 0.75f, -front - eye * 0.1f), new Vector3(eye * 1.1f, eye * 0.22f, eye * 0.2f), pupil);
                brow.transform.localRotation = Quaternion.Euler(0f, 0f, (s * 2 - 1) * -20f);
            }
            var mouth = Part(PrimitiveType.Cube, body, new Vector3(0f, up - eye * 1.3f, -front - eye * 0.05f), new Vector3(eye * 1.2f, eye * 0.2f, eye * 0.2f), pupil);
            mouth.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        // ---------- the Grumble (Level 1's boss) ----------
        public sealed class Grumble
        {
            public Transform Root, Body;
            public readonly GameObject[] Shells = new GameObject[3];
            public Transform Badge;
        }

        public static Grumble BuildGrumble(Transform parent)
        {
            // big (half a metre), horned, with a toothy frown: clearly not just another Discord
            var g = new Grumble { Root = new GameObject("Grumble").transform };
            g.Root.SetParent(parent, false);
            g.Body = new GameObject("Body").transform; g.Body.SetParent(g.Root, false);
            var skin = Lit(new Color(0.3f, 0.2f, 0.38f), 0.3f);
            var dark = Lit(new Color(0.16f, 0.12f, 0.2f), 0.2f);
            Part(PrimitiveType.Sphere, g.Body, Vector3.zero, new Vector3(0.5f, 0.44f, 0.44f), skin);
            for (int s = 0; s < 2; s++) // horns
            {
                var horn = Part(PrimitiveType.Capsule, g.Body, new Vector3((s * 2 - 1) * 0.15f, 0.22f, 0f), new Vector3(0.06f, 0.09f, 0.06f), dark, "Horn");
                horn.transform.localRotation = Quaternion.Euler(0f, 0f, (s * 2 - 1) * -30f);
            }
            for (int s = 0; s < 2; s++) // stubby arms
                Part(PrimitiveType.Sphere, g.Body, new Vector3((s * 2 - 1) * 0.27f, -0.06f, -0.02f), new Vector3(0.11f, 0.08f, 0.08f), skin, "Arm");
            Face(g.Body, 0.215f, 0.06f, 0.075f);
            var tooth = Glow(new Color(0.98f, 0.97f, 0.95f));
            for (int k = 0; k < 4; k++) Part(PrimitiveType.Cube, g.Body, new Vector3((k - 1.5f) * 0.035f, -0.075f, -0.215f), new Vector3(0.022f, 0.028f, 0.01f), tooth, "Tooth");
            // the three shells, one shown at a time
            var spiky = new GameObject("Shell_Squeak"); spiky.transform.SetParent(g.Body, false);
            var spikeMat = Lit(new Color(0.6f, 0.32f, 0.55f), 0.4f);
            for (int i = 0; i < 22; i++)
            {
                var dir = Quaternion.Euler(i * 41f, i * 73f, 0f) * Vector3.up;
                if (dir.z < -0.45f) continue; // keep the face clear
                var sp = Part(PrimitiveType.Cube, spiky.transform, dir * 0.24f, new Vector3(0.04f, 0.13f, 0.04f), spikeMat);
                sp.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, 45f, 0f);
            }
            var jelly = new GameObject("Shell_Wobbler"); jelly.transform.SetParent(g.Body, false);
            Part(PrimitiveType.Sphere, jelly.transform, new Vector3(0f, 0f, 0.03f), new Vector3(0.64f, 0.58f, 0.52f), Glow(new Color(0.45f, 0.85f, 0.55f, 0.35f), transparent: true, queueOffset: 20));
            var armour = new GameObject("Shell_Clang"); armour.transform.SetParent(g.Body, false);
            var plate = Lit(new Color(0.62f, 0.64f, 0.7f), 0.85f);
            Part(PrimitiveType.Cube, armour.transform, new Vector3(0f, 0.17f, -0.02f), new Vector3(0.52f, 0.11f, 0.44f), plate);
            Part(PrimitiveType.Cube, armour.transform, new Vector3(-0.25f, -0.03f, 0f), new Vector3(0.07f, 0.34f, 0.38f), plate);
            Part(PrimitiveType.Cube, armour.transform, new Vector3(0.25f, -0.03f, 0f), new Vector3(0.07f, 0.34f, 0.38f), plate);
            for (int k = 0; k < 4; k++) Part(PrimitiveType.Sphere, armour.transform, new Vector3((k - 1.5f) * 0.12f, 0.17f, -0.245f), Vector3.one * 0.03f, dark);
            g.Shells[0] = spiky; g.Shells[1] = jelly; g.Shells[2] = armour;
            return g;
        }

        // ---------- the instrument's card ----------
        public static readonly Color CardInk = new Color(0.227f, 0.176f, 0.333f), CardFace = new Color(0.98f, 0.95f, 0.88f), CardGold = new Color(1f, 0.78f, 0.3f);

        /// <summary>
        /// An instrument's card (owner, 2026-11-25: introduce the instrument before the stage starts; "after you win the
        /// level, you can collect the card where we can read the other historical facts"): a cream card with a gold edge,
        /// the instrument itself turning at the top (its 3D icon from the game's library), its name, and a body of text.
        /// Faces the player when given a rotation whose +z is away from them. Its turning model is the child "CardModel".
        /// </summary>
        public static Transform Card(Transform parent, InstrumentType instrument, string title, string body, float width, float height, int layer = Layer, float bodySize = 0.034f)
        {
            var root = new GameObject($"Card_{instrument}") { layer = layer }.transform;
            root.SetParent(parent, false);
            Part(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.006f), new Vector3(width + 0.03f, height + 0.03f, 0.01f), Lit(CardGold, 0.6f), "Edge");
            Part(PrimitiveType.Cube, root, Vector3.zero, new Vector3(width, height, 0.012f), Lit(CardFace, 0.3f), "Face");
            var library = InstrumentIconLibrary.Instance;
            var mesh = library != null ? library.MeshFor(instrument) : null;
            float top = height * 0.5f;
            if (mesh != null)
            {
                var model = new GameObject("CardModel") { layer = Layer }.transform;
                model.SetParent(root, false);
                float s = 0.16f / Mathf.Max(0.01f, mesh.bounds.size.y);
                var holder = new GameObject("Icon") { layer = Layer }.transform; holder.SetParent(model, false);
                holder.localScale = Vector3.one * s;
                holder.localPosition = -mesh.bounds.center * s;
                holder.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = holder.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = library.Look; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                model.localPosition = new Vector3(0f, top - 0.13f, -0.09f);
            }
            var t = Words(root, title, new Vector3(0f, top - 0.27f, -0.012f), 0.06f, CardInk, width - 0.06f);
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            var b = Words(root, body, Vector3.zero, bodySize, CardInk, width - 0.08f);
            b.alignment = TMPro.TextAlignmentOptions.Top;
            b.rectTransform.sizeDelta = new Vector2(width - 0.08f, height - 0.4f);
            b.transform.localPosition = new Vector3(0f, (top - 0.33f) - (height - 0.4f) * 0.5f, -0.012f);
            SetLayer(root.gameObject, layer);
            return root;
        }

        // ---------- stars ----------
        public static Transform Star(Transform parent, Vector3 at, float size, bool lit)
        {
            var go = new GameObject("Star") { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var v = new Vector3[11]; var tri = new int[30];
            v[0] = Vector3.zero;
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0 ? 0.5f : 0.22f) * size;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                tri[i * 3] = 0; tri[i * 3 + 1] = i + 1; tri[i * 3 + 2] = (i + 1) % 10 + 1;
            }
            var mesh = new Mesh { vertices = v, triangles = tri }; mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Glow(lit ? new Color(1f, 0.84f, 0.3f) : new Color(0.5f, 0.48f, 0.55f), twoSided: true);
            return go.transform;
        }
    }
}
