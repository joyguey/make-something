using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RoomOrchestra.EditorTools
{
    /// <summary>
    /// Softer, more finished shapes (Version 1 feedback: "the graphics could use a pass for higher fidelity ... more
    /// detailed 3D models"). Image textures did not draw reliably under PICO Spatial on the emulator, so fidelity comes
    /// from geometry: every block (the controls' slabs and tiles, the title card, Flutter's bubble, the furniture) gets
    /// rounded edges and corners of a real-world radius, and the lamp gets a flared shade instead of a ball. Each mesh
    /// keeps its object's exact bounds (a unit box, like Unity's cube), so sizes, colliders, taps and vines are unchanged.
    /// Meshes are saved under Meshes/Rounded, one per size.
    /// </summary>
    public static class RoundedShapes
    {
        const string Folder = "Assets/RoomOrchestra/Meshes/Rounded";
        /// <summary>Blocks thinner than this (icons, marks) keep their crisp edges.</summary>
        const float MinRounded = 0.015f;
        const int Steps = 3; // segments around each rounded edge

        /// <summary>Rounds every cube under <paramref name="root"/> at most <paramref name="maxRadius"/> metres.</summary>
        public static int RoundBlocks(Transform root, float maxRadius)
        {
            if (root == null) return 0;
            int count = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null || !(IsBuiltinCube(mesh) || mesh.name.StartsWith("RoundedBox_"))) continue;
                var size = Abs(filter.transform.lossyScale);
                float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                if (smallest < MinRounded) continue;
                float radius = Mathf.Min(maxRadius, smallest * 0.3f);
                // the toy buttons (owner, 2026-11-19): a pillowy cap, a soft ring, and name tags rounded like pills
                if (filter.name.EndsWith("Slab") || filter.name.EndsWith("Ring")) radius = Mathf.Min(0.012f, smallest * 0.4f);
                if (filter.name == "LabelBacking") radius = smallest * 0.49f;
                filter.sharedMesh = RoundedBox(size, radius);
                count++;
            }
            return count;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static bool IsBuiltinCube(Mesh mesh) => mesh.name == "Cube" && AssetDatabase.GetAssetPath(mesh).StartsWith("Library/");

        /// <summary>A box of world size <paramref name="size"/> with edges rounded to <paramref name="radius"/>, stored in a
        /// unit box (-0.5..0.5) so it replaces a scaled cube exactly.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius)
        {
            string key = $"RoundedBox_{Mm(size.x)}x{Mm(size.y)}x{Mm(size.z)}_r{Mm(radius)}";
            string path = $"{Folder}/{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var half = size * 0.5f;
            var inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    var us = Stops(half[u], radius);
                    var vs = Stops(half[v], radius);
                    int start = vertices.Count;
                    foreach (float a in us)
                        foreach (float b in vs)
                        {
                            var p = Vector3.zero;
                            p[axis] = sign * half[axis];
                            p[u] = a;
                            p[v] = b;
                            var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                            var d = p - q;
                            Vector3 vertex, normal;
                            if (d.sqrMagnitude > 1e-12f)
                            {
                                normal = d.normalized;
                                vertex = q + normal * radius;
                            }
                            else
                            {
                                normal = Vector3.zero;
                                normal[axis] = sign;
                                vertex = p;
                            }
                            vertices.Add(new Vector3(vertex.x / size.x, vertex.y / size.y, vertex.z / size.z));
                            normals.Add(normal);
                        }
                    int n = vs.Count;
                    for (int i = 0; i + 1 < us.Count; i++)
                        for (int j = 0; j + 1 < n; j++)
                        {
                            int a = start + i * n + j, b = a + n, c = b + 1, d = a + 1;
                            Quad(vertices, triangles, a, b, c, d, normals[a] + normals[c]);
                        }
                }

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Save(mesh, path);
            return mesh;
        }

        // Grid positions along one axis: dense around the rounded edges, one span across the flat middle.
        static List<float> Stops(float half, float radius)
        {
            var stops = new List<float>();
            for (int i = 0; i <= Steps; i++)
            {
                float angle = Mathf.PI * 0.5f * (Steps - i) / Steps;
                stops.Add(-(half - radius) - radius * Mathf.Sin(angle));
            }
            for (int i = Steps; i >= 0; i--)
            {
                float angle = Mathf.PI * 0.5f * (Steps - i) / Steps;
                stops.Add((half - radius) + radius * Mathf.Sin(angle));
            }
            return stops;
        }

        // Two triangles, wound so Unity's normal points the way the surface faces.
        static void Quad(List<Vector3> vertices, List<int> triangles, int a, int b, int c, int d, Vector3 outward)
        {
            var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(n, outward) >= 0f) triangles.AddRange(new[] { a, b, c, a, c, d });
            else triangles.AddRange(new[] { a, c, b, a, d, c });
        }

        /// <summary>
        /// A flared drum lampshade in a unit box: wider at the bottom, an inside, a rolled top rim and a closed diffuser
        /// underneath, so it reads as a lamp from any side (it replaces the sphere shade at the same size).
        /// </summary>
        public static Mesh LampShade()
        {
            string path = $"{Folder}/LampShade.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int around = 40;
            const float bottom = 0.5f, top = 0.31f, low = -0.34f, high = 0.34f, wall = 0.025f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 At(float r, float y, int i)
            {
                float a = 2f * Mathf.PI * i / around;
                return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            // One band between two circles, with its own edge (crisp rims): outside, top rim, inside.
            void Band(float r0, float y0, float r1, float y1, int facing)
            {
                for (int i = 0; i < around; i++)
                {
                    int a = vertices.Count;
                    vertices.Add(At(r0, y0, i));
                    vertices.Add(At(r1, y1, i));
                    vertices.Add(At(r1, y1, i + 1));
                    vertices.Add(At(r0, y0, i + 1));
                    var mid = (vertices[a] + vertices[a + 2]) * 0.5f;
                    var radial = new Vector3(mid.x, 0f, mid.z).normalized;
                    var outward = facing == 0 ? radial + Vector3.up * 0.3f : facing == 1 ? Vector3.up : -radial;
                    Quad(vertices, triangles, a, a + 1, a + 2, a + 3, outward);
                }
            }
            Band(bottom, low, top, high, 0);               // the outside
            Band(top, high, top - wall, high, 1);          // the rolled top rim
            Band(top - wall, high, bottom - wall, low, 2); // the inside
            // A closed diffuser underneath (a real lamp's, and what hides the butterflies waiting inside).
            int centre = vertices.Count;
            vertices.Add(new Vector3(0f, low, 0f));
            for (int i = 0; i <= around; i++) vertices.Add(At(bottom, low, i));
            for (int i = 0; i < around; i++)
            {
                int a = centre + 1 + i, b = a + 1;
                var n = Vector3.Cross(vertices[a] - vertices[centre], vertices[b] - vertices[centre]);
                if (n.y <= 0f) triangles.AddRange(new[] { centre, a, b });
                else triangles.AddRange(new[] { centre, b, a });
            }
            var mesh = new Mesh { name = "LampShade" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); // each band and the diffuser have their own vertices: crisp edges
            mesh.RecalculateBounds();
            Save(mesh, path);
            return mesh;
        }

        static string Mm(float metres) => Mathf.RoundToInt(metres * 1000f).ToString();

        static void Save(Mesh mesh, string path)
        {
            if (!AssetDatabase.IsValidFolder("Assets/RoomOrchestra/Meshes")) AssetDatabase.CreateFolder("Assets/RoomOrchestra", "Meshes");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/RoomOrchestra/Meshes", "Rounded");
            AssetDatabase.CreateAsset(mesh, path);
        }
    }
}
