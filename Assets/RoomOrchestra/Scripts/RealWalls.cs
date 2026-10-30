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
        const float PollSeconds = 1f;

        readonly List<MeshInfo> m_Infos = new();
        readonly Dictionary<MeshId, MeshCollider> m_Walls = new();
        readonly HashSet<MeshId> m_Pending = new();
        XRMeshSubsystem m_Meshing;
        float m_NextPoll;
        bool m_Logged;

        public int WallCount => m_Walls.Count;

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(SpatialDataPermission))
                UnityEngine.Android.Permission.RequestUserPermission(SpatialDataPermission);
#endif
        }

        void Update()
        {
            if (Time.time < m_NextPoll) return;
            m_NextPoll = Time.time + PollSeconds;
            if (m_Meshing == null && !FindMeshing()) return;
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
                Apply(id, scratch, result.Position, result.Rotation, result.Scale);
            });
        }

        // Keeps only the steep triangles of a scanned piece of the room, as an invisible bounce wall.
        void Apply(MeshId id, Mesh scanned, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var walls = SteepOnly(scanned, rotation);
            Destroy(scanned);
            if (!m_Walls.TryGetValue(id, out var collider))
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
            collider.enabled = any;
            if (any) Debug.Log($"[RoomOrchestra] Real walls: piece {id}: {walls.triangles.Length / 3} wall triangles, {collider.bounds.min:F1} to {collider.bounds.max:F1} ({m_Walls.Count} pieces)");
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

        void Remove(MeshId id)
        {
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
