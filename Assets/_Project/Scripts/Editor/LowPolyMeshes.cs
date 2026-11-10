using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Offshore.EditorTools
{
    /// <summary>
    /// Flat-shaded placeholder meshes (every face has its own vertices, so lighting is faceted).
    /// Saved as assets under Meshes/ and updated in place on rebuild, so references survive.
    /// </summary>
    static class LowPolyMeshes
    {
        public const string Folder = "Assets/_Project/Meshes";

        /// <summary>
        /// A tapered N-sided column (islands, rocks, buoys, lighthouse) with a capped top. Optional jitter makes
        /// the outline irregular, deterministic per seed.
        /// </summary>
        public static Mesh Frustum(string name, int sides, float bottomRadius, float topRadius, float bottom, float top, float jitter = 0f, int seed = 0)
        {
            var random = new System.Random(seed);
            var lower = new Vector3[sides];
            var upper = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = (i + (float)random.NextDouble() * jitter * 0.4f) / sides * Mathf.PI * 2f;
                float scale = 1f + ((float)random.NextDouble() * 2f - 1f) * jitter;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                lower[i] = dir * bottomRadius * scale + Vector3.up * bottom;
                upper[i] = dir * topRadius * scale + Vector3.up * top;
            }

            var builder = new FacetBuilder();
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                builder.Quad(lower[i], upper[i], upper[j], lower[j]);
            }

            if (topRadius > 0.001f)
            {
                Vector3 center = Vector3.zero;
                foreach (var p in upper) center += p;
                center /= sides;
                for (int i = 0; i < sides; i++) builder.Triangle(center, upper[(i + 1) % sides], upper[i]);
            }

            return Save(name, builder.ToMesh(name));
        }

        /// <summary>Gable roof: a triangular prism along Z, base on y = 0.</summary>
        public static Mesh Roof(string name, float width, float height, float length)
        {
            float w = width * 0.5f, l = length * 0.5f;
            var a = new Vector3(-w, 0f, -l); var b = new Vector3(w, 0f, -l); var c = new Vector3(0f, height, -l);
            var d = new Vector3(-w, 0f, l); var e = new Vector3(w, 0f, l); var f = new Vector3(0f, height, l);

            var builder = new FacetBuilder();
            builder.Triangle(a, c, b);
            builder.Triangle(d, e, f);
            builder.Quad(a, d, f, c);
            builder.Quad(b, c, f, e);
            return Save(name, builder.ToMesh(name));
        }

        /// <summary>
        /// Boat hull seen from above: flat-topped, pointed bow at +Z, slightly flared sides, deck at y = top.
        /// </summary>
        public static Mesh Hull(string name, float length, float beam, float bottom, float top)
        {
            float halfL = length * 0.5f, halfB = beam * 0.5f;
            float shoulder = halfL - beam * 0.9f;
            Vector3[] deck =
            {
                new Vector3(-halfB, top, -halfL),
                new Vector3(-halfB, top, shoulder),
                new Vector3(0f, top, halfL),
                new Vector3(halfB, top, shoulder),
                new Vector3(halfB, top, -halfL),
            };
            Vector3[] keel = new Vector3[deck.Length];
            for (int i = 0; i < deck.Length; i++)
            {
                Vector3 p = deck[i];
                keel[i] = new Vector3(p.x * 0.7f, bottom, p.z * 0.92f);
            }

            var builder = new FacetBuilder();
            for (int i = 0; i < deck.Length; i++)
            {
                int j = (i + 1) % deck.Length;
                builder.Quad(keel[i], deck[i], deck[j], keel[j]);
            }
            // Deck (top) and bottom caps as fans.
            for (int i = 1; i < deck.Length - 1; i++)
            {
                builder.Triangle(deck[0], deck[i], deck[i + 1]);
                builder.Triangle(keel[0], keel[i + 1], keel[i]);
            }
            return Save(name, builder.ToMesh(name));
        }

        static Mesh Save(string name, Mesh mesh)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project", "Meshes");
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        class FacetBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int> triangles = new List<int>();

            /// <summary>Clockwise when seen from the front (Unity's front face).</summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            public Mesh ToMesh(string name)
            {
                // Shapes are convex, so face every triangle away from the centre instead of trusting winding.
                Vector3 centre = Vector3.zero;
                foreach (var v in vertices) centre += v;
                centre /= Mathf.Max(1, vertices.Count);
                for (int t = 0; t < triangles.Count; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (Vector3.Dot(normal, (a + b + c) / 3f - centre) < 0f)
                    {
                        (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
                    }
                }

                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
