using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using ByteDance.PICO.XR;
#endif

namespace RoomOrchestra
{
    /// <summary>
    /// The headset's labelled room scan (PICO Spatial: each scanned piece of mesh comes with semantic labels such as
    /// table, chair, sofa, lamp). Keeps each piece's points in scene space, sorted into furniture by
    /// <see cref="FurnitureClusters"/>, so instruments can move onto real furniture once the lesson is over.
    /// Memory only: nothing is drawn, saved or sent (CLAUDE.md: no room-tracking data leaves the device).
    /// PICO delivers the pieces from its own thread; they are queued and handled on the main thread.
    /// </summary>
    public class RoomScan : MonoBehaviour
    {
        const float SummarySeconds = 5f;

        struct Piece
        {
            public Vector3[] Points;
            public FurnitureKind[] Kinds;
        }

        readonly object m_Lock = new();
        readonly List<(Guid id, bool removed, Vector3[] points, FurnitureKind[] kinds, int[] labelCounts)> m_Incoming = new();
        readonly Dictionary<Guid, Piece> m_Pieces = new();
        readonly FurnitureClusters m_Clusters = new();
        readonly int[] m_LabelTotals = new int[32];
        List<FurniturePiece> m_Furniture = new();
        bool m_Dirty;
        float m_NextSummary;
        int m_Updates, m_LoggedPieces = -1, m_LoggedFurniture = -1;

        public static RoomScan Instance { get; private set; }
        /// <summary>Scanned pieces of mesh so far.</summary>
        public int PieceCount => m_Pieces.Count;
        /// <summary>Real furniture found so far, biggest first.</summary>
        public IReadOnlyList<FurniturePiece> Furniture
        {
            get
            {
                Refresh();
                return m_Furniture;
            }
        }

        public static event Action Updated;

        void OnEnable()
        {
            Instance = this;
#if UNITY_ANDROID
            PXR_Manager.SpatialMeshDataUpdated += OnMeshes;
#endif
        }

        void OnDisable()
        {
#if UNITY_ANDROID
            PXR_Manager.SpatialMeshDataUpdated -= OnMeshes;
#endif
            if (Instance == this) Instance = null;
        }

        /// <summary>What a scan label means for the instruments.</summary>
        public static FurnitureKind KindOf(int label) => label switch
        {
            7 => FurnitureKind.Table,
            8 => FurnitureKind.Sofa,
            9 => FurnitureKind.Chair,
            14 => FurnitureKind.Cabinet,
            15 => FurnitureKind.Bed,
            22 => FurnitureKind.Lamp,
            19 or 20 or 21 or 16 or 17 => FurnitureKind.Other, // fridge, washer, air conditioner, plant, screen
            _ => FurnitureKind.None
        };

#if UNITY_ANDROID
        // PICO's thread: copy into scene space and queue.
        void OnMeshes(List<PxrSpatialMeshInfo> meshes)
        {
            if (meshes == null) return;
            foreach (var m in meshes)
            {
                if (m.state == UnityEngine.XR.MeshChangeState.Unchanged) continue;
                if (m.state == UnityEngine.XR.MeshChangeState.Removed)
                {
                    lock (m_Lock) m_Incoming.Add((m.uuid, true, null, null, null));
                    continue;
                }
                var verts = m.vertices ?? Array.Empty<Vector3>();
                var labels = m.labels ?? Array.Empty<PxrSemanticLabel>();
                var counts = new int[32];
                foreach (var l in labels) counts[Mathf.Clamp((int)l, 0, 31)]++;
                Vector3[] points;
                FurnitureKind[] kinds;
                if (labels.Length == verts.Length)
                {
                    points = new Vector3[verts.Length];
                    kinds = new FurnitureKind[verts.Length];
                    for (int i = 0; i < verts.Length; i++)
                    {
                        points[i] = m.position + m.rotation * verts[i];
                        kinds[i] = KindOf((int)labels[i]);
                    }
                }
                else if (m.indices != null && labels.Length * 3 == m.indices.Length)
                {
                    // one label per triangle: its middle stands for it
                    points = new Vector3[labels.Length];
                    kinds = new FurnitureKind[labels.Length];
                    for (int t = 0; t < labels.Length; t++)
                    {
                        var c = (verts[m.indices[t * 3]] + verts[m.indices[t * 3 + 1]] + verts[m.indices[t * 3 + 2]]) / 3f;
                        points[t] = m.position + m.rotation * c;
                        kinds[t] = KindOf((int)labels[t]);
                    }
                }
                else
                {
                    points = Array.Empty<Vector3>();
                    kinds = Array.Empty<FurnitureKind>();
                    if (labels.Length > 0)
                        Debug.Log($"[RoomOrchestra] Room scan: piece with {verts.Length} points, {m.indices?.Length ?? 0} indices and {labels.Length} labels (layout not understood)");
                }
                lock (m_Lock) m_Incoming.Add((m.uuid, false, points, kinds, counts));
            }
        }
#endif

        /// <summary>A scanned piece in scene space (tests feed a pretend room this way).</summary>
        public void SetPiece(Guid id, Vector3[] points, FurnitureKind[] kinds)
        {
            m_Pieces[id] = new Piece { Points = points, Kinds = kinds };
            m_Dirty = true;
        }

        void Update()
        {
            lock (m_Lock)
            {
                foreach (var (id, removed, points, kinds, counts) in m_Incoming)
                {
                    m_Updates++;
                    if (removed)
                    {
                        if (m_Pieces.Remove(id)) m_Dirty = true;
                        continue;
                    }
                    if (counts != null)
                        for (int i = 0; i < counts.Length; i++) m_LabelTotals[i] += counts[i];
                    SetPiece(id, points, kinds);
                }
                m_Incoming.Clear();
            }
            if (m_Dirty && Time.time >= m_NextSummary)
            {
                m_NextSummary = Time.time + SummarySeconds;
                Refresh();
                Updated?.Invoke();
                Summarize();
            }
        }

        void Refresh()
        {
            if (!m_Dirty) return;
            m_Dirty = false;
            m_Clusters.Clear();
            foreach (var piece in m_Pieces.Values)
                for (int i = 0; i < piece.Points.Length; i++)
                    m_Clusters.Add(piece.Kinds[i], piece.Points[i]);
            m_Furniture = m_Clusters.Pieces();
        }

        void Summarize()
        {
            if (m_Pieces.Count == m_LoggedPieces && m_Furniture.Count == m_LoggedFurniture) return;
            m_LoggedPieces = m_Pieces.Count;
            m_LoggedFurniture = m_Furniture.Count;
            var labels = new System.Text.StringBuilder();
            for (int i = 0; i < m_LabelTotals.Length; i++)
                if (m_LabelTotals[i] > 0) labels.Append($" {i}:{m_LabelTotals[i]}");
            Debug.Log($"[RoomOrchestra] Room scan: {m_Pieces.Count} pieces ({m_Updates} updates), labels seen{labels}; furniture: {m_Furniture.Count}");
            for (int i = 0; i < Mathf.Min(8, m_Furniture.Count); i++)
                Debug.Log($"[RoomOrchestra] Room scan:   {m_Furniture[i]}");
        }
    }
}
