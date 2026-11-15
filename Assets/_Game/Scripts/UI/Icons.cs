using System;
using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Simple white icons drawn in code (tint them with Image.color), so the UI can speak in pictures
    /// for 7-12 year olds without importing art. Anti-aliased by supersampling. Cached after first use.
    /// Names: battery, air, shield, sun, dust, snow, heat, radio, plant, lab, rover, rock, moon, check, box,
    /// alert, satellite, home, shelter, up, down, hab, play, map, book, suit, globe, star.
    /// </summary>
    public static class Icons
    {
        const int S = 96;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s) && s) return s;
            var shape = Shape(name);
            var px = new Color32[S * S];
            const int ss = 3;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var p = new Vector2((x + (sx + 0.5f) / ss) / S * 2 - 1, (y + (sy + 0.5f) / ss) / S * 2 - 1);
                    if (shape(p)) hits++;
                }
                px[y * S + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (ss * ss)));
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100);
            cache[name] = s;
            return s;
        }

        // ── shape helpers (p in -1..1, y up) ──
        static bool Circle(Vector2 p, float cx, float cy, float r) => (p - new Vector2(cx, cy)).sqrMagnitude <= r * r;
        static bool Ring(Vector2 p, float cx, float cy, float r, float w) { float d = (p - new Vector2(cx, cy)).magnitude; return d <= r + w && d >= r - w; }
        static bool Box(Vector2 p, float cx, float cy, float hx, float hy) => Mathf.Abs(p.x - cx) <= hx && Mathf.Abs(p.y - cy) <= hy;
        static bool RBox(Vector2 p, float cx, float cy, float hx, float hy, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - cx) - (hx - r), Mathf.Abs(p.y - cy) - (hy - r));
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) <= r;
        }
        static bool Seg(Vector2 p, float ax, float ay, float bx, float by, float w)
        {
            var a = new Vector2(ax, ay); var ba = new Vector2(bx, by) - a; var pa = p - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude <= w;
        }
        static bool Poly(Vector2 p, params float[] xy)
        {
            bool c = false;
            int n = xy.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                if ((yi > p.y) != (yj > p.y) && p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi) c = !c;
            }
            return c;
        }
        static bool Ellipse(Vector2 p, float cx, float cy, float rx, float ry, float angDeg)
        {
            float a = angDeg * Mathf.Deg2Rad;
            var d = p - new Vector2(cx, cy);
            var q = new Vector2(d.x * Mathf.Cos(a) + d.y * Mathf.Sin(a), -d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a));
            return (q.x * q.x) / (rx * rx) + (q.y * q.y) / (ry * ry) <= 1;
        }

        static Func<Vector2, bool> Shape(string name)
        {
            switch (name)
            {
                case "battery":
                    return p => (RBox(p, -0.05f, 0, 0.66f, 0.42f, 0.1f) && !RBox(p, -0.05f, 0, 0.53f, 0.29f, 0.04f))
                                || Box(p, 0.7f, 0, 0.08f, 0.17f) || Box(p, -0.22f, 0, 0.27f, 0.2f);
                case "air":
                    return p => Ring(p, -0.28f, -0.28f, 0.3f, 0.08f) || Ring(p, 0.36f, 0.02f, 0.22f, 0.07f) || Ring(p, -0.05f, 0.5f, 0.16f, 0.06f);
                case "shield":
                    return p => Poly(p, -0.6f, 0.7f, 0.6f, 0.7f, 0.6f, 0.05f, 0f, -0.8f, -0.6f, 0.05f) && !Poly(p, -0.08f, 0.55f, 0.08f, 0.55f, 0.08f, -0.35f, -0.08f, -0.35f) || Box(p, 0, 0.1f, 0.3f, 0.08f) && Poly(p, -0.6f, 0.7f, 0.6f, 0.7f, 0.6f, 0.05f, 0f, -0.8f, -0.6f, 0.05f);
                case "sun":
                    return p =>
                    {
                        if (Circle(p, 0, 0, 0.36f)) return true;
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4;
                            if (Seg(p, Mathf.Cos(a) * 0.52f, Mathf.Sin(a) * 0.52f, Mathf.Cos(a) * 0.82f, Mathf.Sin(a) * 0.82f, 0.07f)) return true;
                        }
                        return false;
                    };
                case "dust":
                    return p => Circle(p, -0.35f, 0.05f, 0.3f) || Circle(p, 0.08f, 0.22f, 0.4f) || Circle(p, 0.45f, 0.02f, 0.28f) || Box(p, 0.05f, -0.1f, 0.62f, 0.18f)
                                || Circle(p, -0.4f, -0.62f, 0.08f) || Circle(p, 0.05f, -0.7f, 0.08f) || Circle(p, 0.5f, -0.6f, 0.08f);
                case "snow":
                    return p =>
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float a = i * Mathf.PI / 3 + Mathf.PI / 2;
                            float cx = Mathf.Cos(a) * 0.8f, cy = Mathf.Sin(a) * 0.8f;
                            if (Seg(p, -cx, -cy, cx, cy, 0.075f)) return true;
                            for (int k = -1; k <= 1; k += 2)
                            {
                                float bx = Mathf.Cos(a) * 0.5f * k, by = Mathf.Sin(a) * 0.5f * k;
                                float b1 = a + 0.6f * k, b2 = a - 0.6f * k;
                                if (Seg(p, bx, by, bx + Mathf.Cos(b1) * 0.22f * k, by + Mathf.Sin(b1) * 0.22f * k, 0.06f)) return true;
                                if (Seg(p, bx, by, bx + Mathf.Cos(b2) * 0.22f * k, by + Mathf.Sin(b2) * 0.22f * k, 0.06f)) return true;
                            }
                        }
                        return false;
                    };
                case "heat":
                    return p =>
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            float x0 = i * 0.42f;
                            if (Mathf.Abs(p.y) < 0.62f && Mathf.Abs(p.x - (x0 + 0.13f * Mathf.Sin(p.y * 7f))) < 0.085f) return true;
                        }
                        return Box(p, 0, -0.78f, 0.7f, 0.08f);
                    };
                case "radio":
                    return p => Poly(p, -0.35f, -0.85f, 0.05f, -0.85f, -0.1f, 0.1f, -0.2f, 0.1f) || Circle(p, -0.15f, 0.22f, 0.13f)
                                || (Ring(p, -0.15f, 0.22f, 0.38f, 0.06f) && p.x > 0.05f && Mathf.Abs(p.y - 0.22f) < 0.3f)
                                || (Ring(p, -0.15f, 0.22f, 0.66f, 0.06f) && p.x > 0.18f && Mathf.Abs(p.y - 0.22f) < 0.5f);
                case "plant":
                    return p => Seg(p, 0, -0.8f, 0, 0.1f, 0.07f) || Ellipse(p, -0.32f, 0.1f, 0.36f, 0.17f, 35) || Ellipse(p, 0.32f, 0.32f, 0.36f, 0.17f, -35)
                                || Box(p, 0, -0.78f, 0.45f, 0.1f);
                case "lab":
                    return p => (Poly(p, -0.62f, -0.8f, 0.62f, -0.8f, 0.18f, 0.0f, 0.18f, 0.6f, -0.18f, 0.6f, -0.18f, 0.0f)
                                 && !Poly(p, -0.48f, -0.7f, 0.48f, -0.7f, 0.12f, -0.05f, 0.12f, 0.5f, -0.12f, 0.5f, -0.12f, -0.05f))
                                || Poly(p, -0.48f, -0.7f, 0.48f, -0.7f, 0.25f, -0.32f, -0.25f, -0.32f) || Box(p, 0, 0.66f, 0.3f, 0.07f);
                case "rover":
                    return p => RBox(p, 0, 0.0f, 0.62f, 0.2f, 0.06f) || Box(p, 0, 0.27f, 0.72f, 0.04f) || Seg(p, 0.35f, 0.3f, 0.35f, 0.72f, 0.05f) || Box(p, 0.35f, 0.75f, 0.14f, 0.07f)
                                || Circle(p, -0.5f, -0.45f, 0.2f) || Circle(p, 0, -0.45f, 0.2f) || Circle(p, 0.5f, -0.45f, 0.2f);
                case "rock":
                    return p => Poly(p, -0.75f, -0.5f, -0.55f, 0.15f, -0.15f, 0.45f, 0.35f, 0.38f, 0.72f, 0.0f, 0.65f, -0.5f) && !Circle(p, 0.2f, 0.0f, 0.1f) && !Circle(p, -0.25f, -0.2f, 0.08f);
                case "moon":
                    return p => Circle(p, 0, 0, 0.7f) && !Circle(p, 0.32f, 0.22f, 0.58f);
                case "check":
                    return p => Seg(p, -0.6f, 0.0f, -0.18f, -0.45f, 0.13f) || Seg(p, -0.18f, -0.45f, 0.65f, 0.5f, 0.13f);
                case "box":
                    return p => RBox(p, 0, 0, 0.72f, 0.72f, 0.18f) && !RBox(p, 0, 0, 0.56f, 0.56f, 0.08f);
                case "alert":
                    return p => Poly(p, 0, 0.85f, 0.85f, -0.7f, -0.85f, -0.7f) && !(Box(p, 0, 0.12f, 0.08f, 0.3f) || Circle(p, 0, -0.42f, 0.09f));
                case "satellite":
                    return p => Box(p, 0, 0, 0.18f, 0.18f) || Box(p, -0.52f, 0, 0.28f, 0.2f) || Box(p, 0.52f, 0, 0.28f, 0.2f) || Seg(p, 0, 0.18f, 0, 0.55f, 0.05f) || Circle(p, 0, 0.6f, 0.1f);
                case "home":
                    return p => Poly(p, 0, 0.82f, 0.8f, 0.05f, -0.8f, 0.05f) || (Box(p, 0, -0.38f, 0.55f, 0.45f) && !Box(p, 0, -0.55f, 0.15f, 0.3f));
                case "shelter":
                    return p => (Circle(p, 0, -0.55f, 0.85f) && p.y > -0.55f && !(Box(p, 0, -0.3f, 0.18f, 0.26f) || Circle(p, 0, -0.04f, 0.18f))) || Box(p, 0, -0.72f, 0.9f, 0.08f);
                case "up":
                    return p => Poly(p, 0, 0.75f, 0.7f, -0.35f, -0.7f, -0.35f);
                case "down":
                    return p => Poly(p, 0, -0.75f, 0.7f, 0.35f, -0.7f, 0.35f);
                case "hab":   // HAB's friendly face: a screen with two eyes and a smile
                    return p => (RBox(p, 0, 0.02f, 0.82f, 0.68f, 0.22f) && !RBox(p, 0, 0.02f, 0.68f, 0.54f, 0.14f))
                                || Circle(p, -0.27f, 0.15f, 0.12f) || Circle(p, 0.27f, 0.15f, 0.12f)
                                || (Ring(p, 0, 0.0f, 0.36f, 0.06f) && p.y < -0.12f && Mathf.Abs(p.x) < 0.3f)
                                || Box(p, 0, -0.8f, 0.3f, 0.06f);
                case "play":
                    return p => Poly(p, -0.45f, 0.7f, 0.7f, 0f, -0.45f, -0.7f);
                case "map":
                    return p => Poly(p, -0.75f, 0.55f, -0.25f, 0.7f, 0.25f, 0.55f, 0.75f, 0.7f, 0.75f, -0.55f, 0.25f, -0.7f, -0.25f, -0.55f, -0.75f, -0.7f)
                                && !Seg(p, -0.25f, 0.62f, -0.25f, -0.62f, 0.035f) && !Seg(p, 0.25f, 0.62f, 0.25f, -0.62f, 0.035f);
                case "book":
                    return p => (Poly(p, -0.8f, 0.6f, -0.05f, 0.5f, -0.05f, -0.7f, -0.8f, -0.6f) || Poly(p, 0.05f, 0.5f, 0.8f, 0.6f, 0.8f, -0.6f, 0.05f, -0.7f));
                case "suit":
                    return p => Circle(p, 0, 0.42f, 0.34f) && !Ellipse(p, 0, 0.42f, 0.22f, 0.15f, 0) || RBox(p, 0, -0.38f, 0.48f, 0.42f, 0.14f);
                case "globe":
                    return p => Ring(p, 0, 0, 0.72f, 0.07f) || Seg(p, -0.72f, 0, 0.72f, 0, 0.05f) || (Ellipse(p, 0, 0, 0.32f, 0.72f, 0) && !Ellipse(p, 0, 0, 0.22f, 0.64f, 0));
                case "star":
                    return p =>
                    {
                        var pts = new float[20];
                        for (int i = 0; i < 10; i++)
                        {
                            float a = Mathf.PI / 2 + i * Mathf.PI / 5, r = i % 2 == 0 ? 0.9f : 0.38f;
                            pts[i * 2] = Mathf.Cos(a) * r; pts[i * 2 + 1] = Mathf.Sin(a) * r;
                        }
                        return Poly(p, pts);
                    };
                default:
                    return p => Circle(p, 0, 0, 0.6f);
            }
        }
    }
}
