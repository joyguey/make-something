using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>The kinds of real furniture the room scan can name that an instrument can live on.</summary>
    public enum FurnitureKind
    {
        None = 0,
        Table,
        Chair,
        Sofa,
        Lamp,
        Cabinet,
        Bed,
        Other
    }

    /// <summary>One real piece of furniture found in the room scan: its kind, its box, and the middle of its top.</summary>
    public readonly struct FurniturePiece
    {
        public readonly FurnitureKind Kind;
        public readonly Bounds Box;
        /// <summary>Height of the top surface (a high percentile of its points, so a stray point doesn't lift it).</summary>
        public readonly float TopY;
        public readonly int Cells;

        public FurniturePiece(FurnitureKind kind, Bounds box, float topY, int cells)
        {
            Kind = kind;
            Box = box;
            TopY = topY;
            Cells = cells;
        }

        /// <summary>The middle of its top, where an instrument sits and a ball aims.</summary>
        public Vector3 Top => new(Box.center.x, TopY, Box.center.z);
        public override string ToString() => $"{Kind} top {Top:F2} size {Box.size:F2} ({Cells} cells)";
    }

    /// <summary>
    /// Groups the room scan's labelled points into separate pieces of furniture (two tables are two pieces): points
    /// go into cells <see cref="Cell"/> on a side, touching cells of the same kind join up, and a group big enough
    /// (<see cref="MinCells"/>) is a piece. Pure geometry so it can be tested with a pretend room. Memory only.
    /// </summary>
    public sealed class FurnitureClusters
    {
        public const float Cell = 0.12f;
        public const int MinCells = 4;
        /// <summary>The top is this share of the way up the piece's points (ignores a few stray high points).</summary>
        public const float TopPercentile = 0.9f;

        readonly Dictionary<FurnitureKind, Dictionary<Vector3Int, List<float>>> m_Cells = new();

        public void Clear() => m_Cells.Clear();

        public void Add(FurnitureKind kind, Vector3 point)
        {
            if (kind == FurnitureKind.None) return;
            if (!m_Cells.TryGetValue(kind, out var cells)) m_Cells[kind] = cells = new Dictionary<Vector3Int, List<float>>();
            var key = new Vector3Int(Mathf.FloorToInt(point.x / Cell), Mathf.FloorToInt(point.y / Cell), Mathf.FloorToInt(point.z / Cell));
            if (!cells.TryGetValue(key, out var heights)) cells[key] = heights = new List<float>(4);
            if (heights.Count < 16) heights.Add(point.y);
        }

        /// <summary>Every piece found, biggest first.</summary>
        public List<FurniturePiece> Pieces()
        {
            var result = new List<FurniturePiece>();
            var seen = new HashSet<Vector3Int>();
            var queue = new Queue<Vector3Int>();
            var heights = new List<float>();
            foreach (var pair in m_Cells)
            {
                var cells = pair.Value;
                seen.Clear();
                foreach (var start in cells.Keys)
                {
                    if (!seen.Add(start)) continue;
                    queue.Enqueue(start);
                    heights.Clear();
                    int count = 0;
                    Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                    while (queue.Count > 0)
                    {
                        var c = queue.Dequeue();
                        count++;
                        heights.AddRange(cells[c]);
                        var lo = new Vector3(c.x, c.y, c.z) * Cell;
                        min = Vector3.Min(min, lo);
                        max = Vector3.Max(max, lo + Vector3.one * Cell);
                        for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            var n = new Vector3Int(c.x + dx, c.y + dy, c.z + dz);
                            if (cells.ContainsKey(n) && seen.Add(n)) queue.Enqueue(n);
                        }
                    }
                    if (count < MinCells) continue;
                    heights.Sort();
                    float top = heights[Mathf.Clamp(Mathf.FloorToInt(heights.Count * TopPercentile), 0, heights.Count - 1)];
                    var box = new Bounds();
                    box.SetMinMax(min, max);
                    result.Add(new FurniturePiece(pair.Key, box, top, count));
                }
            }
            result.Sort((a, b) => b.Cells.CompareTo(a.Cells));
            return result;
        }
    }
}
