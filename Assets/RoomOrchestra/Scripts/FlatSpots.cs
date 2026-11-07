using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// Where a flower may grow in the real room (George, 2026-11-03: "I'd expect them to grow only on flat, unobstructed
    /// surfaces. Use the room's spatial mesh for placement"). The scanned room's triangles are binned into a grid of
    /// <see cref="Cell"/>-sized squares seen from above. A square is a spot when enough of it is level, upward-facing
    /// surface (a floor, a table top, a seat) and nothing of the scan hangs over that surface within
    /// <see cref="Headroom"/> (a shelf, a table above the floor, a chair seat over its own legs' footprint). Spots keep
    /// clear of the player's seat and the instruments, and of each other. Pure geometry: nothing is stored or sent.
    /// </summary>
    public sealed class FlatSpots
    {
        /// <summary>Grid square, metres: about one flower's footprint with room around it.</summary>
        public const float Cell = 0.2f;
        /// <summary>A triangle counts as flat when its normal is within about 18 degrees of straight up.</summary>
        public const float FlatNormalY = 0.95f;
        /// <summary>Share of a square that must be flat surface.</summary>
        public const float MinFlatShare = 0.6f;
        /// <summary>Clear space a flower needs above its surface (a grown flower is about 25 cm).</summary>
        public const float Headroom = 0.35f;
        /// <summary>Surfaces within this of each other in height count as one level (scan noise).</summary>
        public const float LevelTolerance = 0.03f;
        /// <summary>Flowers stand at least this far apart.</summary>
        public const float MinSpacing = 0.35f;

        struct Square
        {
            public float FlatArea, FlatHeight, FlatWeight;
            public Vector3 Centroid;
            public float HighestOther; // the highest scanned point above the flat surface (within headroom), if any
            public bool Blocked;
        }

        readonly List<(Vector3 a, Vector3 b, Vector3 c)> m_Triangles = new();
        readonly List<(Vector3 centre, float radius)> m_KeepOut = new();
        readonly List<Vector3> m_Taken = new();

        public int TriangleCount => m_Triangles.Count;

        public void Clear() => m_Triangles.Clear();

        /// <summary>A scanned triangle, in world (scene) space.</summary>
        public void Add(Vector3 a, Vector3 b, Vector3 c) => m_Triangles.Add((a, b, c));

        /// <summary>No spot within <paramref name="radius"/> of <paramref name="centre"/> (seat, instruments), seen from above.</summary>
        public void KeepClearOf(Vector3 centre, float radius) => m_KeepOut.Add((centre, radius));

        public void ReleaseAll() => m_Taken.Clear();

        /// <summary>Every spot the scan offers now, nearest the <paramref name="near"/> point first.</summary>
        public List<Vector3> Spots(Vector3 near)
        {
            var squares = new Dictionary<(int, int), Square>();
            // 1. Flat, upward-facing surface per square: area and area-weighted height and centre.
            foreach (var (a, b, c) in m_Triangles)
            {
                var cross = Vector3.Cross(b - a, c - a);
                float twiceArea = cross.magnitude;
                if (twiceArea < 1e-8f || cross.y / twiceArea < FlatNormalY) continue;
                float area = twiceArea * 0.5f;
                var centre = (a + b + c) / 3f;
                var key = Key(centre);
                squares.TryGetValue(key, out var sq);
                if (sq.FlatArea > 0f && Math.Abs(centre.y - sq.FlatHeight / sq.FlatWeight) > LevelTolerance && centre.y < sq.FlatHeight / sq.FlatWeight)
                    continue; // a lower level under the one found first (floor under a table top): the higher one wins
                if (sq.FlatArea > 0f && centre.y > sq.FlatHeight / sq.FlatWeight + LevelTolerance)
                    sq = default; // a higher level: start the square again from it
                sq.FlatArea += area;
                sq.FlatHeight += centre.y * area;
                sq.FlatWeight += area;
                sq.Centroid += centre * area;
                squares[key] = sq;
            }
            // 2. Anything scanned above a square's surface within the headroom blocks it.
            foreach (var (a, b, c) in m_Triangles)
                foreach (var p in new[] { a, b, c, (a + b + c) / 3f })
                {
                    var key = Key(p);
                    if (!squares.TryGetValue(key, out var sq) || sq.FlatWeight <= 0f) continue;
                    float level = sq.FlatHeight / sq.FlatWeight;
                    if (p.y > level + LevelTolerance && p.y < level + Headroom)
                    {
                        sq.Blocked = true;
                        squares[key] = sq;
                    }
                }
            // 3. Keep the clear, mostly flat squares away from the seat, the instruments and taken spots.
            var spots = new List<Vector3>();
            foreach (var sq in squares.Values)
            {
                if (sq.Blocked || sq.FlatArea < MinFlatShare * Cell * Cell) continue;
                var spot = sq.Centroid / sq.FlatWeight;
                spot.y = sq.FlatHeight / sq.FlatWeight;
                if (Kept(spot)) spots.Add(spot);
            }
            spots.Sort((p, q) => Flat(p - near).sqrMagnitude.CompareTo(Flat(q - near).sqrMagnitude));
            return spots;
        }

        /// <summary>The nearest free spot to <paramref name="near"/>, now taken (others keep their distance); false if none.</summary>
        public bool TryTake(Vector3 near, out Vector3 spot)
        {
            foreach (var candidate in Spots(near))
            {
                bool spaced = true;
                foreach (var taken in m_Taken)
                    if (Flat(candidate - taken).magnitude < MinSpacing) { spaced = false; break; }
                if (!spaced) continue;
                m_Taken.Add(candidate);
                spot = candidate;
                return true;
            }
            spot = default;
            return false;
        }

        bool Kept(Vector3 p)
        {
            foreach (var (centre, radius) in m_KeepOut)
                if (Flat(p - centre).magnitude < radius) return false;
            return true;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static (int, int) Key(Vector3 p) => (Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
    }
}
