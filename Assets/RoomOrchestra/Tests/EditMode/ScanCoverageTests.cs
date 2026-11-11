using NUnit.Framework;
using UnityEngine;

namespace RoomOrchestra.Tests
{
    /// <summary>
    /// Owner, headset 2026-11-12: "there should be some indicator of a bar to show how much of the room has been
    /// scanned". The share of directions around the player the scan covers.
    /// </summary>
    public class ScanCoverageTests
    {
        static Vector3[] Patch(Vector3 at, int triangles)
        {
            var t = new Vector3[triangles * 3];
            for (int i = 0; i < triangles; i++)
            {
                var c = at + new Vector3(0.01f * i, 0f, 0.01f * (i % 5));
                t[i * 3] = c;
                t[i * 3 + 1] = c + Vector3.right * 0.01f;
                t[i * 3 + 2] = c + Vector3.forward * 0.01f;
            }
            return t;
        }

        [Test]
        public void CoverageCountsTheDirectionsTheScanHasSeen()
        {
            var go = new GameObject("Walls");
            try
            {
                var walls = go.AddComponent<RealWalls>();
                Assert.AreEqual(0f, walls.Coverage(Vector3.zero), "nothing scanned yet");
                int sectors = RealWalls.CoverageSectors;
                for (int k = 0; k < sectors * 3 / 4; k++)
                {
                    float a = (k + 0.5f) / sectors * Mathf.PI * 2f - Mathf.PI;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    walls.SetPieceForTest(k, Patch(dir * 2.5f, RealWalls.TrianglesPerSector + 2));
                }
                Assert.AreEqual(0.75f, walls.Coverage(Vector3.zero), 1e-4f, "three quarters of the way round");
                walls.SetPieceForTest(99, Patch(new Vector3(20f, 0f, 0f), 100));
                Assert.AreEqual(0.75f, walls.Coverage(Vector3.zero), 1e-4f, "far away doesn't count");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
