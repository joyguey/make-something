using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Finds the real object the player points at in the room scan, and its shape (owner, headset 2026-11-08: "use the
    /// mesh shape of the scanned object ... refine so that it's more accurate"). With a labelled piece of furniture
    /// there (the scan's own table, chair, lamp...), its box is tightened to the scanned triangles inside it. Without
    /// one, the surface pointed at grows outwards: a top (a desk, a shelf) through the flat triangles at its height,
    /// a side (a lamp, a cabinet) through the triangles near it; the object then stands on the floor. Pure geometry over
    /// scene-space triangles, so tests can feed a pretend room. Memory only.
    /// </summary>
    public static class RoomObjects
    {
        /// <summary>Grid size for growing a surface (m); a top's triangles within this of its height count.</summary>
        public const float Cell = 0.08f, SameHeight = 0.06f;
        /// <summary>How far a surface may grow from the point (m), and the most an object may measure across.</summary>
        public const float MaxReach = 1.2f, MaxSide = 2.5f;
        /// <summary>Triangles this close above the floor are floor, not the object (m).</summary>
        public const float FloorClear = 0.04f;
        /// <summary>A box this close to the floor stands on it (its legs may not be scanned).</summary>
        public const float StandsOnFloor = 0.35f;
        /// <summary>The size of the patch used when nothing around the point was scanned.</summary>
        public static readonly Vector3 PatchSize = new(0.45f, 0.3f, 0.45f);

        struct CellInfo
        {
            public bool Up;
            public int UpCount, SideCount;
            public Bounds Box;
        }

        static Vector3Int Key(Vector3 p) => new(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));

        static bool IsUp(Vector3 a, Vector3 b, Vector3 c, out Vector3 centre)
        {
            centre = (a + b + c) / 3f;
            var n = Vector3.Cross(b - a, c - a);
            return n.sqrMagnitude > 1e-12f && Mathf.Abs(n.normalized.y) > 0.7f;
        }

        /// <summary>The object at <paramref name="point"/> (on its surface), as a box standing in the room (scene space).</summary>
        public static Bounds Find(Vector3 point, IEnumerable<Vector3[]> pieces, IReadOnlyList<FurniturePiece> labelled, float floorY)
        {
            // a labelled piece of furniture around the point: the smallest that holds it
            if (labelled != null)
            {
                FurniturePiece? best = null;
                float bestVolume = float.MaxValue;
                foreach (var piece in labelled)
                {
                    if (piece.Kind == FurnitureKind.None) continue;
                    var near = piece.Box;
                    near.Expand(0.2f);
                    if (!near.Contains(point)) continue;
                    float v = piece.Box.size.x * piece.Box.size.y * piece.Box.size.z;
                    if (v < bestVolume) { bestVolume = v; best = piece; }
                }
                if (best.HasValue)
                {
                    var loose = best.Value.Box;
                    loose.Expand(0.12f);
                    var tight = Tighten(loose, pieces, floorY, out int count);
                    var box = count >= 3 ? tight : best.Value.Box;
                    box.Encapsulate(point);
                    return Settle(box, floorY, onFloor: false);
                }
            }
            var grown = Grow(point, pieces, floorY, out bool top);
            return Settle(grown, floorY, onFloor: top);
        }

        // The scanned triangles inside a box, as their own tighter box.
        static Bounds Tighten(Bounds within, IEnumerable<Vector3[]> pieces, float floorY, out int count)
        {
            count = 0;
            var box = new Bounds();
            if (pieces == null) return box;
            foreach (var tris in pieces)
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    var c = (tris[i] + tris[i + 1] + tris[i + 2]) / 3f;
                    if (c.y < floorY + FloorClear || !within.Contains(c)) continue;
                    if (count == 0) box = new Bounds(c, Vector3.zero);
                    box.Encapsulate(c);
                    count++;
                }
            return box;
        }

        // The surface at the point, grown outwards over the scan.
        static Bounds Grow(Vector3 point, IEnumerable<Vector3[]> pieces, float floorY, out bool top)
        {
            top = false;
            var cells = new Dictionary<Vector3Int, CellInfo>();
            if (pieces != null)
                foreach (var tris in pieces)
                    for (int i = 0; i + 2 < tris.Length; i += 3)
                    {
                        bool up = IsUp(tris[i], tris[i + 1], tris[i + 2], out var c);
                        if (c.y < floorY + FloorClear) continue;
                        var flat = c - point;
                        flat.y = 0f;
                        if (flat.sqrMagnitude > MaxReach * MaxReach) continue;
                        var key = Key(c);
                        if (!cells.TryGetValue(key, out var cell)) cell = new CellInfo { Box = new Bounds(c, Vector3.zero) };
                        cell.Box.Encapsulate(tris[i]);
                        cell.Box.Encapsulate(tris[i + 1]);
                        cell.Box.Encapsulate(tris[i + 2]);
                        if (up) cell.UpCount++; else cell.SideCount++;
                        cell.Up = cell.UpCount >= cell.SideCount;
                        cells[key] = cell;
                    }
            // the cell the pointer touched (or the nearest scanned one beside it)
            var start = Key(point);
            if (!cells.ContainsKey(start))
            {
                float bestD = float.MaxValue;
                foreach (var k in cells.Keys)
                {
                    float d = (cells[k].Box.center - point).sqrMagnitude;
                    if (d < bestD && d < Cell * Cell * 4f) { bestD = d; start = k; }
                }
            }
            if (!cells.TryGetValue(start, out var first))
                return new Bounds(point - Vector3.up * PatchSize.y / 2f, PatchSize);
            top = first.Up;
            var seen = new HashSet<Vector3Int> { start };
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            var box = first.Box;
            while (queue.Count > 0)
            {
                var k = queue.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var n = new Vector3Int(k.x + dx, k.y + dy, k.z + dz);
                    if (seen.Contains(n) || !cells.TryGetValue(n, out var cell)) continue;
                    var c = cell.Box.center;
                    var flat = c - point;
                    flat.y = 0f;
                    if (top)
                    {
                        // a top: its flat triangles at the same height
                        if (!cell.Up || Mathf.Abs(c.y - point.y) > SameHeight) continue;
                    }
                    else if (c.y > point.y + 0.6f || flat.sqrMagnitude > 0.6f * 0.6f) continue; // a side: what is near it
                    seen.Add(n);
                    box.Encapsulate(cell.Box);
                    queue.Enqueue(n);
                }
            }
            if (seen.Count < 2) return new Bounds(point - Vector3.up * PatchSize.y / 2f, PatchSize);
            return box;
        }

        // Stands on the floor when it nearly does (a top always does: its legs are rarely scanned), and is never bigger
        // than furniture.
        static Bounds Settle(Bounds box, float floorY, bool onFloor)
        {
            var min = box.min;
            var max = box.max;
            if (onFloor || min.y - floorY < StandsOnFloor) min.y = floorY;
            max.y = Mathf.Max(max.y, min.y + 0.05f);
            var c = (min + max) / 2f;
            var size = max - min;
            size.x = Mathf.Min(size.x, MaxSide);
            size.z = Mathf.Min(size.z, MaxSide);
            size.x = Mathf.Max(size.x, 0.1f);
            size.z = Mathf.Max(size.z, 0.1f);
            return new Bounds(c, size);
        }

        /// <summary>Room kept clear around the player's head across the floor (m).</summary>
        public const float PlayerClear = 0.35f;

        /// <summary>
        /// Owner, headset 2026-11-08: the chair "was extra large because of its proximity to me". The scan does not
        /// tell the player apart from the chair they sit on, so a box that reaches the player is cut back on the side
        /// toward them (the cut that keeps more of it and still holds the point pointed at). Unchanged otherwise.
        /// </summary>
        public static Bounds ClearOfPlayer(Bounds box, Vector3 point, Vector3 head)
        {
            var min = box.min;
            var max = box.max;
            bool reaches = head.x > min.x - PlayerClear && head.x < max.x + PlayerClear && head.z > min.z - PlayerClear && head.z < max.z + PlayerClear;
            var flat = point - head;
            flat.y = 0f;
            if (!reaches || flat.magnitude < PlayerClear) return box;
            // cut across x or across z, on the point's side of the head
            var xMin = min; var xMax = max;
            if (point.x >= head.x) xMin.x = Mathf.Max(min.x, head.x + PlayerClear); else xMax.x = Mathf.Min(max.x, head.x - PlayerClear);
            var zMin = min; var zMax = max;
            if (point.z >= head.z) zMin.z = Mathf.Max(min.z, head.z + PlayerClear); else zMax.z = Mathf.Min(max.z, head.z - PlayerClear);
            float xArea = Mathf.Max(0f, xMax.x - xMin.x) * (xMax.z - xMin.z);
            float zArea = (zMax.x - zMin.x) * Mathf.Max(0f, zMax.z - zMin.z);
            bool xOk = xMax.x - xMin.x >= 0.1f && Mathf.Abs(point.x - head.x) >= PlayerClear;
            bool zOk = zMax.z - zMin.z >= 0.1f && Mathf.Abs(point.z - head.z) >= PlayerClear;
            Vector3 lo, hi;
            if (xOk && (!zOk || xArea >= zArea)) { lo = xMin; hi = xMax; }
            else if (zOk) { lo = zMin; hi = zMax; }
            else return box;
            var cut = new Bounds();
            cut.SetMinMax(lo, hi);
            return cut;
        }

        /// <summary>The object's shape: the scanned triangles inside its box, above the floor (scene space, three per triangle).</summary>
        public static List<Vector3> ShapeIn(Bounds box, IEnumerable<Vector3[]> pieces, float floorY)
        {
            var shape = new List<Vector3>();
            if (pieces == null) return shape;
            var within = box;
            within.Expand(0.04f);
            foreach (var tris in pieces)
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    var c = (tris[i] + tris[i + 1] + tris[i + 2]) / 3f;
                    if (c.y < floorY + FloorClear || !within.Contains(c)) continue;
                    shape.Add(tris[i]);
                    shape.Add(tris[i + 1]);
                    shape.Add(tris[i + 2]);
                }
            return shape;
        }
    }
}
