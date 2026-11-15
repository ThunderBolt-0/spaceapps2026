using System.Collections.Generic;
using UnityEngine;

namespace MissionTrainer
{
    /// <summary>
    /// Shows the saved cosmetics on a suit: trim color, visor material, mission patch decals, commander stripes.
    /// Works with two kinds of suit:
    ///  - the simple built-in suit (separate trim / visor renderers), or
    ///  - the NASA 3D suit model (one textured mesh): trim recolors the suit's real stripe band in the texture,
    ///    the visor changes the visor material slot. The suit body always stays white.
    /// Call Apply() after changing what is equipped. Patch images live in Resources/Patches/{id}.png.
    /// </summary>
    public class AstronautSuit : MonoBehaviour
    {
        [Header("Simple built-in suit")]
        public Renderer[] trimRenderers;
        public Renderer[] visorRenderers;
        public GameObject commanderStripes;
        [Tooltip("Old single patch object (before decals). Shown/hidden only if there are no patch decals.")]
        public GameObject patchSlot;

        [Header("Patches (both suits)")]
        public Renderer[] patchRenderers;     // decal quads

        [Header("NASA 3D suit model")]
        public Renderer modelRenderer;
        [Tooltip("Readable copy of the suit texture (logo-free). Its red stripe pixels take the trim color.")]
        public Texture2D modelSuitTexture;
        public int suitMaterialIndex = 0;
        public int visorMaterialIndex = -1;

        Material trimMat, visorMat, patchMat, modelSuitMat, modelVisorMat;
        readonly Dictionary<string, Texture2D> recolored = new Dictionary<string, Texture2D>();

        void Start() => Apply();

        public void Apply()
        {
            var trim = AchievementManager.Equipped("trim");
            var visor = AchievementManager.Equipped("visor");
            var patch = AchievementManager.Equipped("patch");
            var stripes = AchievementManager.Equipped("stripes");

            if (trim != null && trimRenderers != null && trimRenderers.Length > 0)
            {
                trimMat = Shared(trimMat, trimRenderers);
                if (trimMat) trimMat.color = trim.color;
            }
            if (visor != null && visorRenderers != null && visorRenderers.Length > 0)
            {
                visorMat = Shared(visorMat, visorRenderers);
                SetVisor(visorMat, visor);
            }
            if (modelRenderer) ApplyModel(trim, visor, stripes != null);

            bool hasDecals = patchRenderers != null && patchRenderers.Length > 0;
            if (hasDecals)
            {
                var tex = patch != null ? PatchMaker.Get(patch.id) : null;
                patchMat = Shared(patchMat, patchRenderers);
                if (patchMat && tex) patchMat.SetTexture("_BaseMap", tex);
                foreach (var r in patchRenderers) if (r) r.enabled = tex != null;
            }
            else if (patchSlot) patchSlot.SetActive(patch != null);

            if (commanderStripes) commanderStripes.SetActive(stripes != null);
        }

        void ApplyModel(Cosmetic trim, Cosmetic visor, bool commander)
        {
            var mats = modelRenderer.sharedMaterials;
            if (suitMaterialIndex >= 0 && suitMaterialIndex < mats.Length && modelSuitTexture)
            {
                if (!modelSuitMat) { modelSuitMat = new Material(mats[suitMaterialIndex]); mats[suitMaterialIndex] = modelSuitMat; }
                // The whole suit takes the chosen color. The real red stripes stay (on real suits they mark the commander!).
                var tex = Recolor(trim != null ? trim.color : Color.white, commander);
                SetTex(modelSuitMat, tex);
            }
            if (visor != null && visorMaterialIndex >= 0 && visorMaterialIndex < mats.Length)
            {
                if (!modelVisorMat) { modelVisorMat = new Material(mats[visorMaterialIndex]); mats[visorMaterialIndex] = modelVisorMat; }
                SetVisor(modelVisorMat, visor);
            }
            modelRenderer.sharedMaterials = mats;
        }

        /// <summary>
        /// Copy of the suit texture in a new suit color (cached per color): the white fabric takes the color but keeps
        /// its folds and shading; dark parts, metal and the red stripes stay as they are.
        /// </summary>
        Texture2D Recolor(Color c, bool redStripes)
        {
            string key = ColorUtility.ToHtmlStringRGB(c) + (redStripes ? "R" : "");
            if (recolored.TryGetValue(key, out var t) && t) return t;
            if (!modelSuitTexture.isReadable) return modelSuitTexture;
            var px = modelSuitTexture.GetPixels32();
            bool white = c.r > 0.93f && c.g > 0.93f && c.b > 0.93f;
            if (!white || !redStripes)
                for (int i = 0; i < px.Length; i++)
                {
                    var p = px[i];
                    if (p.r > 150 && p.g < 110 && p.b < 110)   // the red stripes: Commander keeps them red, otherwise a darker suit color
                    {
                        if (redStripes) continue;
                        float sh = p.r / 255f * 0.55f;
                        var acc = white ? new Color(0.25f, 0.27f, 0.3f) : c;
                        px[i] = new Color32((byte)(acc.r * 255 * sh), (byte)(acc.g * 255 * sh), (byte)(acc.b * 255 * sh), p.a);
                        continue;
                    }
                    if (white) continue;
                    int max = Mathf.Max(p.r, Mathf.Max(p.g, p.b)), min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                    if (max < 90 || max - min > 60) continue;          // dark parts and colored details stay
                    float lum = (0.3f * p.r + 0.59f * p.g + 0.11f * p.b) / 255f;
                    float k = Mathf.Clamp01((max - 90) / 60f);          // soft edge between fabric and dark parts
                    var tinted = new Color(c.r * lum * 1.08f, c.g * lum * 1.08f, c.b * lum * 1.08f);
                    var o = Color.Lerp(new Color(p.r / 255f, p.g / 255f, p.b / 255f), tinted, 0.88f * k);
                    px[i] = new Color32((byte)(Mathf.Clamp01(o.r) * 255), (byte)(Mathf.Clamp01(o.g) * 255), (byte)(Mathf.Clamp01(o.b) * 255), p.a);
                }
            t = new Texture2D(modelSuitTexture.width, modelSuitTexture.height, TextureFormat.RGBA32, true);
            t.SetPixels32(px);
            t.Apply(true, true);
            recolored[key] = t;
            return t;
        }

        static void SetTex(Material m, Texture tex)
        {
            if (m.HasProperty("baseColorTexture")) m.SetTexture("baseColorTexture", tex);   // glTFast shaders
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);                    // URP Lit
        }

        static void SetVisor(Material m, Cosmetic visor)
        {
            if (!m || visor == null) return;
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", visor.color);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", visor.color);
            if (m.HasProperty("metallicFactor")) m.SetFloat("metallicFactor", visor.metallic);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", visor.metallic);
            if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", 1f - visor.smoothness);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", visor.smoothness);
        }

        /// <summary>One material instance shared by a group of renderers (so play mode never edits the asset).</summary>
        static Material Shared(Material existing, Renderer[] rs)
        {
            if (existing || rs == null || rs.Length == 0 || !rs[0]) return existing;
            var m = new Material(rs[0].sharedMaterial);
            foreach (var r in rs) if (r) r.sharedMaterial = m;
            return m;
        }
    }
}
