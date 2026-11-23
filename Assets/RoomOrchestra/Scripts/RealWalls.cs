using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace RoomOrchestra
{
    /// <summary>
    /// The real room as walls for the balls (Version 1 feedback: the virtual room tears down to show "that the musical
    /// balls interact with real-world geometry", and "could smoothly disguise the room-scanning phase"). PICO Spatial
    /// supports AR Foundation meshing in Full Space; with AR Foundation installed and PICO's Spatial Mesh setting on,
    /// the PICO loader provides an XR mesh subsystem. While the virtual room stands, the headset scans; the room's mesh
    /// becomes invisible colliders, keeping only steep surfaces (walls and the sides of real furniture: floors and table
    /// tops are left out, so a ball still lands on the stage's own floor). Each carries <see cref="BounceSurface"/>, so a
    /// ball bounces off a real wall with its own sound. Nothing is drawn, stored or sent anywhere: the mesh lives only in
    /// memory for physics (CLAUDE.md: no room-tracking data leaves the device). Without a mesh subsystem (the Editor, or a
    /// device that gives no mesh) it does nothing.
    /// </summary>
    public class RealWalls : MonoBehaviour
    {
        /// <summary>A triangle is kept if its normal is this close to horizontal (|y| below it): about 60 degrees or steeper.</summary>
        public const float SteepNormalY = 0.5f;
        public const string SpatialDataPermission = "com.picovr.permission.SPATIAL_DATA";
        /// <summary>
        /// The layer of the room's whole surface (PICO XR mode): drawn as a see-through glow while the player chooses where
        /// the instruments live (owner, headset 2026-11-08: "shouldn't there be a visual that shows me when the room is
        /// being mapped"), and pointed at to choose. Balls never touch it (only the steep walls bounce them).
        /// </summary>
        public const int SurfaceLayer = 30;

        /// <summary>The room hides nothing this close to the player's seat (m, across the floor): the chair they sit in
        /// and their own body (which the scan sees as room) must never hide the leaf and controls in front of them.
        /// Was 1.5 m while PICO's depth did the near hiding; the scanned mesh now does it all (owner, 2026-11-08).</summary>
        public const float NearClear = 0.6f;

        [Tooltip("How the scanned room looks while it is shown (see-through).")]
        [SerializeField] Material scanLook;
        [Tooltip("The scanned room drawn into depth only, so real walls and furniture hide what is behind them.")]
        [SerializeField] Material occluderLook;
        readonly Dictionary<string, MeshCollider> m_Surfaces = new();
        readonly Dictionary<string, MeshFilter> m_Occluders = new();
        static bool s_ShowScan, s_Occlude;

        /// <summary>The see-through look of the scanned room (real objects' glows start from it).</summary>
        public Material ScanLook => scanLook;

        public void ConfigureScanLook(Material look, Material occluder = null)
        {
            scanLook = look;
            occluderLook = occluder;
        }

        /// <summary>
        /// Real walls and furniture hide the virtual things behind them (owner, 2026-11-07: "occlusion needs to be much
        /// better"). On once the virtual room has come down (it would otherwise cut the lesson's room apart).
        /// </summary>
        public static bool Occlude
        {
            get => s_Occlude;
            set
            {
                if (s_Occlude == value) return;
                s_Occlude = value;
                if (Instance != null)
                    foreach (var o in Instance.m_Occluders.Values)
                        if (o != null && o.TryGetComponent<MeshRenderer>(out var r)) r.enabled = value;
                Debug.Log($"[RoomOrchestra] Real room hides what is behind it: {value}");
            }
        }

        public int OccluderCount => m_Occluders.Count;

        // The pieces of the room's surface farther than NearClear from the seat (scene space, across the floor).
        public static Mesh FarOnly(Mesh mesh, Transform at, Vector3 seat)
        {
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var kept = new List<int>(tris.Length);
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                var c = at.TransformPoint((verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f) - seat;
                c.y = 0f;
                if (c.sqrMagnitude >= NearClear * NearClear)
                {
                    kept.Add(tris[i]);
                    kept.Add(tris[i + 1]);
                    kept.Add(tris[i + 2]);
                }
            }
            var far = new Mesh { name = "RoomOccluder", indexFormat = mesh.indexFormat };
            far.vertices = verts;
            far.SetTriangles(kept, 0);
            far.RecalculateBounds();
            return far;
        }

        static Vector3 Seat() => StageAnchor.Instance != null ? StageAnchor.Instance.transform.position : Vector3.zero;

        void Shield(string key, MeshCollider surface)
        {
            if (occluderLook == null || surface == null || surface.sharedMesh == null) return;
            if (!m_Occluders.TryGetValue(key, out var filter) || filter == null)
            {
                var go = new GameObject($"RoomOccluder_{key}") { layer = SurfaceLayer };
                go.transform.SetParent(surface.transform, false);
                filter = go.AddComponent<MeshFilter>();
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = occluderLook;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                m_Occluders[key] = filter;
            }
            var old = filter.sharedMesh;
            filter.sharedMesh = FarOnly(surface.sharedMesh, surface.transform, Seat());
            if (old != null) Destroy(old);
            filter.GetComponent<MeshRenderer>().enabled = s_Occlude;
        }

        // The seat moved (Recenter): what counts as near the player changed.
        void Reshield()
        {
            foreach (var pair in m_Surfaces) Shield(pair.Key, pair.Value);
        }

        /// <summary>
        /// Should real things hide virtual ones now: after the lesson's virtual room is down (it would otherwise be cut
        /// apart), not while the scanned room glows for choosing places (the shield at the glow's own depth hid the glow,
        /// emulator 2026-11-08), and while the player wants it (Menu).
        /// </summary>
        public static bool OcclusionWanted { get; private set; }

        /// <summary>
        /// The real room bounces balls only once the lesson's virtual room is down (owner, headset 2026-11-08: every throw
        /// at the virtual chair bounced back off the real desk in front of her; the lesson is played in the virtual room).
        /// The scan goes on meanwhile (the leaf's desk height, choosing places), it just doesn't touch the balls.
        /// </summary>
        public static bool WallsActive { get; private set; }

        void UpdateWalls()
        {
            var room = VirtualRoom.Instance;
            bool active = room == null || room.Gone;
            if (active == WallsActive) return;
            WallsActive = active;
            foreach (var wall in m_Walls.Values)
                if (wall != null) wall.enabled = active && wall.sharedMesh != null;
            Debug.Log($"[RoomOrchestra] Real walls {(active ? "bounce the balls now (the virtual room is down)" : "wait: the lesson's virtual room is the stage")}");
        }

        void UpdateOcclusion()
        {
            var room = VirtualRoom.Instance;
            OcclusionWanted = (room == null || room.Gone) && !s_ShowScan && EnvironmentOcclusion.Wanted;
            // the scanned room's own shapes do the hiding (PICO's depth image is off: it was not accurate enough)
            Occlude = OcclusionWanted && !EnvironmentOcclusion.Active;
        }

        /// <summary>Show the scanned room (while choosing places), or hide it.</summary>
        public static bool ShowScan
        {
            get => s_ShowScan;
            set
            {
                if (s_ShowScan == value) return;
                s_ShowScan = value;
                if (Instance != null)
                    foreach (var surface in Instance.m_Surfaces.Values)
                        if (surface != null && surface.TryGetComponent<MeshRenderer>(out var r)) r.enabled = value && Instance.scanLook != null;
                Debug.Log($"[RoomOrchestra] Room scan shown: {value}");
            }
        }

        public int SurfaceCount => m_Surfaces.Count;

        /// <summary>A scanned piece's whole surface (the headset's mesh; tests feed a pretend one).</summary>
        public void SetSurface(string key, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!m_Surfaces.TryGetValue(key, out var surface) || surface == null)
            {
                var go = new GameObject($"RoomSurface_{key}") { layer = SurfaceLayer };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>();
                var look = go.AddComponent<MeshRenderer>();
                look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                look.receiveShadows = false;
                surface = go.AddComponent<MeshCollider>();
                m_Surfaces[key] = surface;
            }
            surface.transform.SetPositionAndRotation(position, rotation);
            surface.transform.localScale = scale;
            var filter = surface.GetComponent<MeshFilter>();
            if (filter.sharedMesh != null && filter.sharedMesh != mesh) Destroy(filter.sharedMesh);
            filter.sharedMesh = mesh;
            surface.sharedMesh = mesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = scanLook;
            renderer.enabled = s_ShowScan && scanLook != null;
            Shield(key, surface);
        }

        void Awake()
        {
            for (int layer = 0; layer < 32; layer++) Physics.IgnoreLayerCollision(layer, SurfaceLayer); // balls pass through
        }
        const float PollSeconds = 1f;

        readonly List<MeshInfo> m_Infos = new();
        readonly Dictionary<MeshId, MeshCollider> m_Walls = new();
        readonly HashSet<MeshId> m_Pending = new();
        // Each scanned piece's triangles in scene space, for finding where flowers may grow (memory only, never saved).
        readonly Dictionary<MeshId, Vector3[]> m_Pieces = new();
        readonly FlatSpots m_Spots = new();
        bool m_SpotsDirty, m_KeepOutSet;
        XRMeshSubsystem m_Meshing;
        /// <summary>This device can scan the room (its meshing subsystem is there), whether or not any of the scan has
        /// arrived yet. The editor and devices without room meshing cannot.</summary>
        public bool CanScan => m_Meshing != null || m_Surfaces.Count > 0; // a scan already in (or the tests' pretend room) counts
        float m_NextPoll;
        bool m_Logged;

        public int WallCount => m_Walls.Count;
        public static RealWalls Instance { get; private set; }

        void OnEnable()
        {
            Instance = this;
            s_ShowScan = false;
            s_Occlude = false;
            WallsActive = false;
            StageAnchor.Recentered += Reshield;
        }
        void OnDisable()
        {
            StageAnchor.Recentered -= Reshield;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// A free spot on a flat, clear real surface near <paramref name="near"/> for a flower to grow on (George,
        /// 2026-11-03), now taken; false with no scan or no room left (the flower keeps its own place).
        /// </summary>
        public bool TryTakeFlowerSpot(Vector3 near, out Vector3 spot)
        {
            spot = default;
            if (m_Pieces.Count == 0) return false;
            if (!m_KeepOutSet)
            {
                m_KeepOutSet = true;
                m_Spots.KeepClearOf(Vector3.zero, 0.7f); // the player's seat (the stage origin)
                foreach (var target in FindObjectsByType<InstrumentTarget>(FindObjectsSortMode.None))
                    m_Spots.KeepClearOf(target.transform.position, 0.45f);
            }
            if (m_SpotsDirty)
            {
                m_SpotsDirty = false;
                m_Spots.Clear();
                foreach (var tris in m_Pieces.Values)
                    for (int i = 0; i + 2 < tris.Length; i += 3) m_Spots.Add(tris[i], tris[i + 1], tris[i + 2]);
            }
            return m_Spots.TryTake(near, out spot, Eye());
        }

        // The player's eye now (the headset), or the seated eye at the stage origin.
        static Vector3 Eye()
        {
            if (StageAnchor.Instance != null && StageAnchor.Instance.TryGetHeadPose(out var pose)) return pose.position;
            return new Vector3(0f, 1.15f, 0f);
        }

        /// <summary>The song was cleared: every flower spot is free again.</summary>
        public void ReleaseFlowerSpots() => m_Spots.ReleaseAll();

        /// <summary>Every scanned piece's triangles in scene space (three corners each), for finding a real object's shape.</summary>
        public IEnumerable<Vector3[]> PieceTriangles => m_Pieces.Values;

        // ---- How much of the room is scanned (owner, headset 2026-11-12: "there should be some indicator of a bar to
        // show how much of the room has been scanned") ----
        /// <summary>Directions around the player; one counts once this many scanned triangles lie that way, near enough.</summary>
        public const int CoverageSectors = 16, TrianglesPerSector = 24;
        public const float CoverageNear = 0.4f, CoverageFar = 7f;

        /// <summary>Test hook: a scanned piece under its own number (counted by <see cref="Coverage"/> only).</summary>
        public void SetPieceForTest(int n, Vector3[] worldTriangles) => m_TestPieces[n] = worldTriangles;

        readonly Dictionary<int, Vector3[]> m_TestPieces = new();

        /// <summary>The share (0..1) of directions around <paramref name="centre"/> the scan already covers.</summary>
        public float Coverage(Vector3 centre)
        {
            var counts = new int[CoverageSectors];
            foreach (var tris in System.Linq.Enumerable.Concat(m_Pieces.Values, m_TestPieces.Values))
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    var c = (tris[i] + tris[i + 1] + tris[i + 2]) / 3f - centre;
                    c.y = 0f;
                    float d = c.magnitude;
                    if (d < CoverageNear || d > CoverageFar) continue;
                    float a = Mathf.Atan2(c.z, c.x) / (Mathf.PI * 2f) + 0.5f;
                    counts[Mathf.Clamp((int)(a * CoverageSectors), 0, CoverageSectors - 1)]++;
                }
            int covered = 0;
            foreach (var n in counts) if (n >= TrianglesPerSector) covered++;
            return covered / (float)CoverageSectors;
        }

        /// <summary>A scanned piece's triangles in scene space (tests feed a pretend room this way).</summary>
        public void SetPiece(MeshId id, Vector3[] worldTriangles)
        {
            m_Pieces[id] = worldTriangles;
            m_SpotsDirty = true;
        }

        bool m_WasAllowed;

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(SpatialDataPermission))
                UnityEngine.Android.Permission.RequestUserPermission(SpatialDataPermission);
#endif
        }

        void Update()
        {
            UpdateWalls();
            UpdateOcclusion();
            if (Time.time < m_NextPoll) return;
            m_NextPoll = Time.time + PollSeconds;
            if (m_Meshing == null && !FindMeshing()) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            // meshing started before the player allowed room data delivers nothing: start it again once allowed
            bool allowed = UnityEngine.Android.Permission.HasUserAuthorizedPermission(SpatialDataPermission);
            if (allowed && !m_WasAllowed && m_Meshing.running)
            {
                m_Meshing.Stop();
                Debug.Log("[RoomOrchestra] Room data allowed: the room scan starts again");
            }
            m_WasAllowed = allowed;
#endif
            if (!m_Meshing.running) m_Meshing.Start();
            m_Infos.Clear();
            if (!m_Meshing.TryGetMeshInfos(m_Infos)) return;
            foreach (var info in m_Infos)
            {
                switch (info.ChangeState)
                {
                    case MeshChangeState.Added:
                    case MeshChangeState.Updated:
                        Generate(info.MeshId);
                        break;
                    case MeshChangeState.Removed:
                        Remove(info.MeshId);
                        break;
                }
            }
        }

        bool FindMeshing()
        {
            var found = new List<XRMeshSubsystem>();
            SubsystemManager.GetSubsystems(found);
            if (found.Count == 0)
            {
                if (!m_Logged) Debug.Log("[RoomOrchestra] Real walls: no room meshing on this device (balls bounce off the virtual room only)");
                m_Logged = true;
                return false;
            }
            m_Meshing = found[0];
            Debug.Log($"[RoomOrchestra] Real walls: room meshing found ({m_Meshing.subsystemDescriptor?.id}); scanning while the virtual room stands");
            return true;
        }

        void Generate(MeshId id)
        {
            if (m_Pending.Contains(id)) return;
            m_Pending.Add(id);
            var scratch = new Mesh { name = $"RoomScan_{id}" };
            m_Meshing.GenerateMeshAsync(id, scratch, null, MeshVertexAttributes.Normals, result =>
            {
                m_Pending.Remove(id);
                if (result.Status != MeshGenerationStatus.Success)
                {
                    Destroy(scratch);
                    return;
                }
                Apply(id, scratch, XrRig.ToScene(result.Position), XrRig.ToScene(result.Rotation), result.Scale); // tracking -> scene
            });
        }

        /// <summary>Test hook: a scanned piece arrives (as from the headset's meshing).</summary>
        public void ApplyForTest(MeshId id, Mesh scanned, Vector3 position) => Apply(id, scanned, position, Quaternion.identity, Vector3.one);

        /// <summary>The bounce wall made from a scanned piece, if any.</summary>
        public MeshCollider WallFor(MeshId id) => m_Walls.TryGetValue(id, out var c) ? c : null;

        // Keeps only the steep triangles of a scanned piece of the room, as an invisible bounce wall.
        void Apply(MeshId id, Mesh scanned, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var walls = SteepOnly(scanned, rotation);
            SetPiece(id, WorldTriangles(scanned, position, rotation, scale));
            SetSurface(id.ToString(), scanned, position, rotation, scale); // kept: shown while choosing, and pointed at
            bool isNew = !m_Walls.TryGetValue(id, out var collider);
            if (isNew)
            {
                var go = new GameObject($"RealWall_{id}");
                go.transform.SetParent(transform, false);
                go.AddComponent<BounceSurface>();
                collider = go.AddComponent<MeshCollider>();
                m_Walls[id] = collider;
            }
            collider.transform.SetPositionAndRotation(position, rotation);
            collider.transform.localScale = scale;
            if (collider.sharedMesh != null) Destroy(collider.sharedMesh);
            bool any = walls.triangles.Length > 0;
            collider.sharedMesh = any ? walls : null; // a piece with no steep surface (a bit of floor) is no wall
            if (!any) Destroy(walls);
            collider.enabled = any && WallsActive;
            // new pieces only: updates arrive every second (headset 2026-11-08: 700 lines a minute)
            if (any && isNew) Debug.Log($"[RoomOrchestra] Real walls: piece {id}: {walls.triangles.Length / 3} wall triangles, {collider.bounds.min:F1} to {collider.bounds.max:F1} ({m_Walls.Count} pieces)");
        }

        /// <summary>The triangles of <paramref name="mesh"/> that face sideways in the world (walls, furniture sides).</summary>
        public static Mesh SteepOnly(Mesh mesh, Quaternion rotation)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var kept = new List<int>(triangles.Length);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var normal = rotation * Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) continue;
                if (Mathf.Abs(normal.normalized.y) < SteepNormalY)
                {
                    kept.Add(triangles[i]);
                    kept.Add(triangles[i + 1]);
                    kept.Add(triangles[i + 2]);
                }
            }
            var result = new Mesh { name = mesh.name + "_Walls", indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            result.vertices = vertices;
            result.triangles = kept.ToArray();
            result.RecalculateBounds();
            return result;
        }

        static Vector3[] WorldTriangles(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var world = new Vector3[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
                world[i] = position + rotation * Vector3.Scale(vertices[triangles[i]], scale);
            return world;
        }

        void Remove(MeshId id)
        {
            if (m_Pieces.Remove(id)) m_SpotsDirty = true;
            if (!m_Walls.TryGetValue(id, out var collider)) return;
            m_Walls.Remove(id);
            if (collider != null)
            {
                if (collider.sharedMesh != null) Destroy(collider.sharedMesh);
                Destroy(collider.gameObject);
            }
        }
    }
}
