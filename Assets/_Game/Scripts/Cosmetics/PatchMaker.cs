using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Mission patch images: Resources/Patches/{id}.png if there is one, otherwise a round badge made in code
    /// (patch color, white ring, the item's icon in the middle). Cached.
    /// </summary>
    public static class PatchMaker
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cache.Clear();

        public static Texture2D Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var t) && t) return t;
            t = Resources.Load<Texture2D>("Patches/" + id);
            if (!t)
            {
                var c = AchievementManager.Find(id);
                if (c == null) return null;
                t = Make(c.color, c.icon);
            }
            cache[id] = t;
            return t;
        }

        static Texture2D Make(Color bg, string icon)
        {
            const int S = 128;
            var px = new Color32[S * S];
            var sprite = Icons.Get(icon);
            var src = sprite ? sprite.texture : null;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = x - S / 2f + 0.5f, dy = y - S / 2f + 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = Color.clear;
                if (r < 62f)
                {
                    float shade = 0.85f + 0.15f * (1f - r / 62f);
                    c = new Color(bg.r * shade, bg.g * shade, bg.b * shade, 1f);
                    if (r > 53f) c = Color.Lerp(Color.white, new Color(0.9f, 0.9f, 0.92f), (r - 53f) / 9f);   // ring
                    else if (src && Mathf.Abs(dx) < 30 && Mathf.Abs(dy) < 30)
                    {
                        var ic = src.GetPixelBilinear((dx + 30f) / 60f, (dy + 30f) / 60f);
                        c = Color.Lerp(c, Color.white, ic.a);
                    }
                    c.a = Mathf.Clamp01(62f - r);   // soft edge
                }
                px[y * S + x] = c;
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
