using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// A real object an instrument lives on (owner, headset 2026-11-08: "the plan is to remove the virtual furniture
    /// once it has been assigned and use the mesh shape of the scanned object"). It is the scanned object's own shape
    /// (the room scan's triangles inside its box), at the scene's top level in the headset's space so recentring never
    /// moves it: an invisible collider takes the ball's hits for its instrument, and a soft glow in that shape shows
    /// when it is pointed at or played.
    /// The five notes run along its longest side (owner, 2026-11-08: "how can there be a height on the object for a
    /// table?"): across a table's top from one end to the other, bottom to top on a tall lamp or shelf. Pointing at it
    /// shows five bands, low to high, and the band under the pointer says its note; pressing there plays that note
    /// (no throw needed), and a ball that hits it plays the band it hit.
    /// The box is kept in this object's own frame, so a box found again from a saved spatial anchor can be turned.
    /// Memory and the device's own storage only: nothing is sent anywhere.
    /// </summary>
    public class RealHome : MonoBehaviour
    {
        public const int Bands = PitchInput.PetalCount;
        /// <summary>Gap between the bands, as a share of a band.</summary>
        const float BandGap = 0.08f;
        /// <summary>The bands and glow show this long after the pointer leaves or a note plays (s).</summary>
        const float GlowHoldSeconds = 0.35f;

        InstrumentTarget m_Target;
        Vector3 m_Size;
        MeshFilter m_Shape;
        MeshRenderer m_Glow;
        MeshCollider m_Hits;
        BoxCollider m_HitBox;
        readonly Transform[] m_Bands = new Transform[Bands];
        readonly MeshRenderer[] m_BandLooks = new MeshRenderer[Bands];
        Material[] m_BandMaterials;
        TMP_Text m_Label;
        float m_ShowUntil = -1f;
        int m_Hover = -1;

        public InstrumentTarget Target => m_Target;
        public string Furniture { get; private set; }
        /// <summary>The object's box in its own frame (centred on this transform).</summary>
        public Vector3 Size => m_Size;
        public Bounds WorldBounds { get; private set; }
        public bool HasShape => m_Shape != null && m_Shape.sharedMesh != null && m_Shape.sharedMesh.triangles.Length > 0;
        public int Hover => m_Hover;
        public bool BandsShown => m_Bands[0] != null && m_Bands[0].gameObject.activeSelf;

        /// <summary>Where on top a ball aims and the orbit floats above (scene space).</summary>
        public Vector3 Top => transform.TransformPoint(new Vector3(0f, m_Size.y / 2f, 0f));
        /// <summary>The middle of its floor footprint (scene space).</summary>
        public Vector3 Floor => transform.TransformPoint(new Vector3(0f, -m_Size.y / 2f, 0f));

        public static RealHome Create(string furniture, InstrumentTarget target, Pose pose, Vector3 size, Material look, TMP_Text labelStyle)
        {
            var go = new GameObject($"RealHome_{furniture}");
            var home = go.AddComponent<RealHome>();
            home.Furniture = furniture;
            home.m_Target = target;
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            home.Build(size, look, labelStyle);
            return home;
        }

        void Build(Vector3 size, Material look, TMP_Text labelStyle)
        {
            m_Size = Vector3.Max(size, Vector3.one * 0.05f);
            var tint = TintFor(m_Target != null ? m_Target.Instrument : InstrumentType.Udu);

            var shape = new GameObject("Shape");
            shape.transform.SetParent(transform, false);
            m_Shape = shape.AddComponent<MeshFilter>();
            m_Glow = shape.AddComponent<MeshRenderer>();
            m_Glow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Glow.receiveShadows = false;
            if (look != null)
            {
                m_Glow.sharedMaterial = new Material(look) { name = $"RealHomeGlow_{Furniture}" };
                SetColor(m_Glow.sharedMaterial, new Color(tint.r, tint.g, tint.b, 0.28f));
            }
            m_Glow.enabled = false;
            m_Hits = shape.AddComponent<MeshCollider>();
            m_Hits.enabled = false;
            // until the scan's shape is in: the box takes the hits
            m_HitBox = gameObject.AddComponent<BoxCollider>();
            m_HitBox.size = m_Size + Vector3.one * RealFurniture.HitMargin * 2f;

            m_BandMaterials = new Material[Bands];
            for (int i = 0; i < Bands; i++)
            {
                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = $"NoteBand{i}";
                Destroy(band.GetComponent<Collider>()); // the shape takes the presses
                band.transform.SetParent(transform, false);
                var r = band.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (look != null)
                {
                    m_BandMaterials[i] = new Material(look) { name = $"NoteBand{i}" };
                    r.sharedMaterial = m_BandMaterials[i];
                }
                m_Bands[i] = band.transform;
                m_BandLooks[i] = r;
                band.SetActive(false);
            }
            LayOutBands();

            if (labelStyle != null)
            {
                var label = Instantiate(labelStyle.gameObject, transform, false);
                label.name = "NoteLabel";
                foreach (var extra in label.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(extra is TMP_Text)) Destroy(extra); // its own text only, no localizing
                m_Label = label.GetComponent<TMP_Text>();
                label.transform.localScale = Vector3.one * 1.6f;
                label.SetActive(false);
            }
            WorldBounds = ComputeWorldBounds();
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>Each instrument's own glow (its orbit and sparkles share the warm/cool feel; colour is never the only cue).</summary>
        public static Color TintFor(InstrumentType instrument) => instrument switch
        {
            InstrumentType.Udu => new Color(1f, 0.62f, 0.3f),
            InstrumentType.Mbira => new Color(0.55f, 0.9f, 0.55f),
            _ => new Color(0.75f, 0.6f, 1f)
        };

        /// <summary>The scanned object's own shape, in scene space (the room scan's triangles inside its box).</summary>
        public void SetShape(IReadOnlyList<Vector3> worldTriangles)
        {
            if (worldTriangles == null || worldTriangles.Count < 9) return;
            var verts = new Vector3[worldTriangles.Count];
            var tris = new int[worldTriangles.Count];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = transform.InverseTransformPoint(worldTriangles[i]);
                tris[i] = i;
            }
            var old = m_Shape.sharedMesh;
            var mesh = new Mesh { name = $"RealHomeShape_{Furniture}", indexFormat = verts.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            m_Shape.sharedMesh = mesh;
            m_Hits.sharedMesh = mesh;
            m_Hits.enabled = true;
            // the box still takes hits on the object's open sides (a table seen from the side has no front face)
            if (old != null) Destroy(old);
        }

        // Five slices of the box along its note axis, a little apart.
        void LayOutBands()
        {
            var axis = NoteAxisLocal(out float length, out _);
            for (int i = 0; i < Bands; i++)
            {
                if (m_Bands[i] == null) continue;
                float slice = length / Bands;
                float along = -length / 2f + slice * (i + 0.5f);
                var size = m_Size + Vector3.one * 0.02f;
                var axisAbs = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
                size = Vector3.Scale(size, Vector3.one - axisAbs) + axisAbs * slice * (1f - BandGap);
                m_Bands[i].localPosition = axis * along;
                m_Bands[i].localRotation = Quaternion.identity;
                m_Bands[i].localScale = size;
                if (m_BandMaterials != null && m_BandMaterials[i] != null)
                {
                    var tint = TintFor(m_Target != null ? m_Target.Instrument : InstrumentType.Udu);
                    float lift = i / (Bands - 1f);
                    SetColor(m_BandMaterials[i], new Color(Mathf.Lerp(tint.r * 0.6f, 1f, lift), Mathf.Lerp(tint.g * 0.6f, 1f, lift), Mathf.Lerp(tint.b * 0.6f, 1f, lift), 0.16f));
                }
            }
        }

        /// <summary>
        /// The note axis in this object's frame, low to high: up when it is taller than it is long; otherwise along its
        /// longer side, from the player's left to their right (or near to far when that side points away from them).
        /// </summary>
        public Vector3 NoteAxisLocal(out float length, out bool upright)
        {
            upright = m_Size.y >= Mathf.Max(m_Size.x, m_Size.z);
            if (upright)
            {
                length = m_Size.y;
                return Vector3.up;
            }
            var axis = m_Size.x >= m_Size.z ? Vector3.right : Vector3.forward;
            length = m_Size.x >= m_Size.z ? m_Size.x : m_Size.z;
            var seat = StageAnchor.Instance != null ? StageAnchor.Instance.transform.position : Vector3.zero;
            var toObject = transform.position - seat;
            toObject.y = 0f;
            if (toObject.sqrMagnitude < 1e-4f) toObject = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, toObject.normalized);
            var world = transform.TransformDirection(axis);
            float side = Vector3.Dot(world, right);
            float sign = Mathf.Abs(side) >= 0.3f ? Mathf.Sign(side) : Mathf.Sign(Vector3.Dot(world, toObject));
            return axis * (sign == 0f ? 1f : sign);
        }

        /// <summary>The band (0 = lowest note) at a point on or near the object, in scene space.</summary>
        public int BandAt(Vector3 worldPoint)
        {
            var local = transform.InverseTransformPoint(worldPoint);
            var axis = NoteAxisLocal(out float length, out _);
            float t = Vector3.Dot(local, axis) / Mathf.Max(1e-3f, length) + 0.5f;
            return Mathf.Clamp(Mathf.FloorToInt(t * Bands), 0, Bands - 1);
        }

        /// <summary>The note at a point on the object (scene space).</summary>
        public PitchInput PitchAt(Vector3 worldPoint) => PitchInput.FromPetal(BandAt(worldPoint));

        /// <summary>The pointer is on it: the bands show, the one under the pointer brightest, with its note.</summary>
        public void Point(Vector3 worldPoint)
        {
            m_Hover = BandAt(worldPoint);
            m_ShowUntil = Time.time + GlowHoldSeconds;
            ShowBands(true);
            if (m_Label != null && m_Target != null)
            {
                m_Label.text = UiText.Tile(new MusicalNote(m_Target.Instrument, m_Hover), Localization.Current);
                var axis = NoteAxisLocal(out float length, out bool upright);
                float along = -length / 2f + length / Bands * (m_Hover + 0.5f);
                var at = axis * along;
                at.y = upright ? at.y : m_Size.y / 2f;
                var world = transform.TransformPoint(at) + Vector3.up * 0.12f;
                if (upright) world += SideToward(Viewer()) * (Mathf.Max(m_Size.x, m_Size.z) / 2f + 0.1f);
                m_Label.transform.position = world;
                var look = world - Viewer();
                look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) m_Label.transform.rotation = Quaternion.LookRotation(look);
                if (!m_Label.gameObject.activeSelf) m_Label.gameObject.SetActive(true);
            }
        }

        /// <summary>A note played on it: a soft glow of its shape, for a moment.</summary>
        public void Pulse()
        {
            m_ShowUntil = Mathf.Max(m_ShowUntil, Time.time + GlowHoldSeconds * 2f);
            if (m_Glow != null) m_Glow.enabled = HasShape;
        }

        /// <summary>Shows its whole shape glowing (while it is being chosen).</summary>
        public void Highlight(bool on)
        {
            m_Highlighted = on;
            if (m_Glow != null) m_Glow.enabled = on && HasShape;
        }

        bool m_Highlighted;

        static Vector3 Viewer()
        {
            if (StageAnchor.Instance != null && StageAnchor.Instance.TryGetHeadPose(out var pose)) return pose.position;
            return new Vector3(0f, 1.15f, 0f);
        }

        Vector3 SideToward(Vector3 viewer)
        {
            var d = viewer - transform.position;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.back;
        }

        void ShowBands(bool on)
        {
            for (int i = 0; i < Bands; i++)
            {
                if (m_Bands[i] == null) continue;
                if (m_Bands[i].gameObject.activeSelf != on) m_Bands[i].gameObject.SetActive(on);
                if (on && m_BandMaterials != null && m_BandMaterials[i] != null)
                {
                    var c = m_BandMaterials[i].HasProperty("_BaseColor") ? m_BandMaterials[i].GetColor("_BaseColor") : Color.white;
                    c.a = i == m_Hover ? 0.5f : 0.16f;
                    SetColor(m_BandMaterials[i], c);
                }
            }
            if (!on && m_Label != null && m_Label.gameObject.activeSelf) m_Label.gameObject.SetActive(false);
        }

        void Update()
        {
            if (m_ShowUntil > 0f && Time.time > m_ShowUntil)
            {
                m_ShowUntil = -1f;
                m_Hover = -1;
                ShowBands(false);
                if (m_Glow != null && !m_Highlighted) m_Glow.enabled = false;
            }
        }

        Bounds ComputeWorldBounds()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                b.Encapsulate(transform.TransformPoint(Vector3.Scale(corner, m_Size)));
            }
            return b;
        }

        void OnDestroy()
        {
            if (m_Shape != null && m_Shape.sharedMesh != null) Destroy(m_Shape.sharedMesh);
            if (m_Glow != null && m_Glow.sharedMaterial != null) Destroy(m_Glow.sharedMaterial);
            if (m_BandMaterials != null) foreach (var m in m_BandMaterials) if (m != null) Destroy(m);
        }

        /// <summary>The real home a collider belongs to, if any.</summary>
        public static RealHome Of(Component c) => c != null ? c.GetComponentInParent<RealHome>() : null;
    }
}
