using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MissionTrainer.EditorTools
{
    /// <summary>
    /// Editor-only procedural art: tileable noise, surface textures (+ normal maps), rock meshes,
    /// lathe (revolved) meshes. Everything is saved as assets under Assets/_Game/Art/Generated
    /// so builds (WebGL) just load them; nothing is generated at runtime.
    /// </summary>
    public static class ProcArt
    {
        public const string GenDir = "Assets/_Game/Art/Generated";
        static string TexDir => GenDir + "/Textures";
        static string MeshDir => GenDir + "/Meshes";

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(MeshDir);
        }

        // ───────────────────────── noise ─────────────────────────

        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)seed * 0xcb1ab31fu;
                h ^= h >> 15; h *= 0x2c1b3c6du;
                h ^= h >> 12; h *= 0x297a2d39u;
                h ^= h >> 15;
                return (h & 0xFFFFFFu) / 16777215f;
            }
        }

        static int Wrap(int i, int p) => ((i % p) + p) % p;
        static float Smooth(float t) => t * t * (3 - 2 * t);

        /// <summary>Value noise that repeats every p cells (for seamless textures).</summary>
        public static float ValueTile(float x, float y, int p, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0), fy = Smooth(y - y0);
            int xa = Wrap(x0, p), xb = Wrap(x0 + 1, p), ya = Wrap(y0, p), yb = Wrap(y0 + 1, p);
            float a = Hash01(xa, ya, seed), b = Hash01(xb, ya, seed), c = Hash01(xa, yb, seed), d = Hash01(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Seamless fractal noise, u/v in [0,1). Result ~0..1.</summary>
        public static float FbmTile(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int p = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueTile(u * p, v * p, p, seed + o * 31);
                norm += amp;
                amp *= 0.5f;
                p *= 2;
            }
            return sum / norm;
        }

        /// <summary>Seamless Voronoi: x = distance to nearest point, y = to second nearest (in cell units).</summary>
        public static Vector2 VoronoiTile(float u, float v, int cells, int seed, out int cellHash)
        {
            float x = u * cells, y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float f1 = 9, f2 = 9;
            cellHash = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy;
                int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                float px = gx + Hash01(wx, wy, seed), py = gy + Hash01(wx, wy, seed + 7);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; cellHash = wx * 7919 + wy; }
                else if (d < f2) f2 = d;
            }
            return new Vector2(f1, f2);
        }

        /// <summary>Non-tiling fractal Perlin for terrain. Result roughly -0.5..0.5.</summary>
        public static float Fbm(float x, float y, int octaves, float offset)
        {
            float sum = 0, amp = 0.5f, freq = 1, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * (Mathf.PerlinNoise(x * freq + offset + o * 17.3f, y * freq - offset + o * 9.1f) - 0.5f);
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return sum / norm;
        }

        // ───────────────────────── textures ─────────────────────────

        public struct TexSet { public Texture2D albedo, normal; }

        public delegate void PixelFunc(float u, float v, out Color color, out float height);

        /// <summary>Builds a seamless albedo + normal map pair from a per-pixel function.</summary>
        public static TexSet Surface(string name, int size, float normalStrength, PixelFunc f)
        {
            var cols = new Color[size * size];
            var hs = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                f((x + 0.5f) / size, (y + 0.5f) / size, out var c, out var h);
                cols[y * size + x] = c;
                hs[y * size + x] = h;
            }
            return new TexSet
            {
                albedo = SavePng(name + "_Albedo", cols, size, false, TextureWrapMode.Repeat),
                normal = SavePng(name + "_Normal", NormalsFromHeight(hs, size, normalStrength), size, true, TextureWrapMode.Repeat),
            };
        }

        /// <summary>Single texture (e.g. alpha gradients, emissive screens).</summary>
        public static Texture2D Image(string name, int w, int h, Func<float, float, Color> f, TextureWrapMode wrap, bool alpha)
        {
            var cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                cols[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            return SavePng(name, cols, w, false, wrap, alpha, h);
        }

        static Color[] NormalsFromHeight(float[] h, int size, float strength)
        {
            var n = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = h[y * size + Wrap(x + 1, size)] - h[y * size + Wrap(x - 1, size)];
                float dy = h[Wrap(y + 1, size) * size + x] - h[Wrap(y - 1, size) * size + x];
                var v = new Vector3(-dx * strength, -dy * strength, 1).normalized;
                n[y * size + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1);
            }
            return n;
        }

        static Texture2D SavePng(string name, Color[] cols, int w, bool isNormal, TextureWrapMode wrap, bool alpha = false, int h = -1)
        {
            if (h < 0) h = w;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, isNormal);
            tex.SetPixels(cols);
            tex.Apply();
            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !isNormal;
            imp.wrapMode = wrap;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = Mathf.Max(w, h);
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.alphaIsTransparency = alpha;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ───────────────────────── meshes ─────────────────────────

        public static Mesh SaveMesh(Mesh m, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(m, existing);
                Object.DestroyImmediate(m);
                return existing;
            }
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// Faceted rock: noisy icosphere with a few flat "fracture" planes, flat shaded.
        /// flatten < 1 makes slabs (bedrock pieces).
        /// </summary>
        public static Mesh Rock(int seed, float flatten)
        {
            Icosphere(2, out var verts, out var tris);
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float ox = R() * 100, oy = R() * 100;

            var cuts = new List<Vector4>();
            int cutCount = 3 + rng.Next(4);
            for (int i = 0; i < cutCount; i++)
            {
                var n = new Vector3(R() * 2 - 1, R() * 2 - 1, R() * 2 - 1).normalized;
                cuts.Add(new Vector4(n.x, n.y, n.z, 0.55f + R() * 0.3f));
            }
            var stretch = new Vector3(0.8f + R() * 0.5f, flatten * (0.7f + R() * 0.4f), 0.8f + R() * 0.5f);

            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                float n = (Mathf.PerlinNoise(v.x * 1.4f + ox, v.y * 1.4f + v.z * 0.8f + oy) - 0.5f) * 0.7f
                        + (Mathf.PerlinNoise(v.z * 3.3f + oy, v.x * 3.3f + v.y * 1.9f + ox) - 0.5f) * 0.25f;
                v *= 1 + n;
                foreach (var c in cuts)
                {
                    var cn = new Vector3(c.x, c.y, c.z);
                    float d = Vector3.Dot(v, cn);
                    if (d > c.w) v -= cn * (d - c.w);   // flatten onto the fracture plane
                }
                verts[i] = Vector3.Scale(v, stretch);
            }

            // Flat shading: unique vertices per triangle; box-projected UVs.
            var fv = new List<Vector3>(tris.Count);
            var fuv = new List<Vector2>(tris.Count);
            var ft = new int[tris.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                var nrm = Vector3.Cross(b - a, c - a);
                var an = new Vector3(Mathf.Abs(nrm.x), Mathf.Abs(nrm.y), Mathf.Abs(nrm.z));
                foreach (var p in new[] { a, b, c })
                {
                    ft[fv.Count] = fv.Count;
                    fv.Add(p);
                    fuv.Add(an.y >= an.x && an.y >= an.z ? new Vector2(p.x, p.z) : an.x >= an.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(fv);
            mesh.SetUVs(0, fuv);
            mesh.SetTriangles(ft, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Icosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var vs = verts;
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int idx)) return idx;
                    vs.Add(((vs[a] + vs[b]) * 0.5f).normalized);
                    cache[key] = vs.Count - 1;
                    return vs.Count - 1;
                }
                var nt = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = nt;
            }
        }

        /// <summary>
        /// Revolves a profile (x = radius, y = height, bottom to top) around the Y axis.
        /// skip(segment, centerAngleDeg, midY) lets you cut openings (doors). UVs: u wraps uRepeat times,
        /// v follows the profile length in meters / vMeters.
        /// </summary>
        public static Mesh Lathe(Vector2[] profile, int segments, bool inward, float uRepeat, float vMeters,
            Func<int, float, float, bool> skip = null, float angleOffsetDeg = 0)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var len = new float[profile.Length];
            for (int j = 1; j < profile.Length; j++) len[j] = len[j - 1] + Vector2.Distance(profile[j], profile[j - 1]);

            int rows = profile.Length;
            for (int s = 0; s <= segments; s++)
            {
                float a = (s / (float)segments * 360f + angleOffsetDeg) * Mathf.Deg2Rad;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int j = 0; j < rows; j++)
                {
                    verts.Add(new Vector3(ca * profile[j].x, profile[j].y, sa * profile[j].x));
                    uvs.Add(new Vector2(s / (float)segments * uRepeat, len[j] / vMeters));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                float center = ((s + 0.5f) / segments * 360f + angleOffsetDeg);
                for (int j = 0; j < rows - 1; j++)
                {
                    float midY = (profile[j].y + profile[j + 1].y) * 0.5f;
                    if (skip != null && skip(s, center, midY)) continue;
                    int a = s * rows + j, b = (s + 1) * rows + j, c = s * rows + j + 1, d = (s + 1) * rows + j + 1;
                    if (inward) tris.AddRange(new[] { a, b, c, b, d, c });
                    else tris.AddRange(new[] { a, c, b, b, c, d });
                }
            }
            var mesh = new Mesh();
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Wall + dome profile (bottom to top).</summary>
        public static Vector2[] WallDome(float radius, float wallHeight, float doorTop, float domeHeight, int domeSteps)
        {
            var p = new List<Vector2> { new Vector2(radius, 0), new Vector2(radius, doorTop), new Vector2(radius, wallHeight) };
            for (int i = 1; i <= domeSteps; i++)
            {
                float phi = i / (float)domeSteps * Mathf.PI * 0.5f;
                p.Add(new Vector2(Mathf.Max(0.001f, radius * Mathf.Cos(phi)), wallHeight + domeHeight * Mathf.Sin(phi)));
            }
            return p.ToArray();
        }

        /// <summary>Smooth mound (bottom outer edge to top center), for regolith piles.</summary>
        public static Vector2[] Mound(float radius, float height, int steps, float baseY)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                p.Add(new Vector2(Mathf.Max(0.001f, radius * (1 - t)), baseY + height * Mathf.Sin(t * Mathf.PI * 0.5f) * (1 - 0.15f * t)));
            }
            return p.ToArray();
        }
    }
}
