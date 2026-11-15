using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MissionTrainer
{
    /// <summary>
    /// Shared look for every runtime-built screen (HUD, cards, debrief, title, map, science page).
    /// Plain uGUI + built-in font, so no TMP import or prefabs are needed and it works in WebGL.
    /// Style: dark glass panels with soft rounded corners, warm Mars accent, quiet secondary text.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.96f, 0.95f, 0.93f);
        public static readonly Color Muted = new Color(0.96f, 0.95f, 0.93f, 0.62f);
        public static readonly Color Glass = new Color(0.05f, 0.06f, 0.09f, 0.82f);
        public static readonly Color GlassDark = new Color(0.06f, 0.065f, 0.085f, 0.99f);
        public static readonly Color CardBg = new Color(1, 1, 1, 0.06f);
        public static readonly Color Quiet = new Color(1, 1, 1, 0.10f);
        public static readonly Color Accent = new Color(0.93f, 0.52f, 0.24f);
        public static readonly Color Good = new Color(0.36f, 0.80f, 0.52f);
        public static readonly Color Warn = new Color(0.96f, 0.76f, 0.30f);
        public static readonly Color Bad = new Color(0.93f, 0.36f, 0.33f);
        public static readonly Color Shield = new Color(0.42f, 0.66f, 0.98f);
        public static readonly Color Gold = new Color(0.99f, 0.80f, 0.32f);

        // Text sizes for 7-12 year olds (at the 1280x720 reference): nothing a player must read is smaller than Small.
        public const int TextSmall = 15, TextBody = 17, TextKey = 20;
        /// <summary>Smallest height for anything you click.</summary>
        public const float MinTap = 44f;

        public static readonly Regex GlossaryTag = new Regex(@"\[\[(\w+)(?:\|([^\]]+))?\]\]");

        static Font font;
        public static Font Font
        {
            get
            {
                if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // ───────────── canvas + layout ─────────────

        public static RectTransform Canvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<AdaptiveScaler>();   // never cut off: fit by width or height depending on the window
            return (RectTransform)go.transform;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Anchor(RectTransform r, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        }

        public static void Fill(RectTransform r) => Anchor(r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static void Center(RectTransform r, Vector2 offset, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = offset;
            r.sizeDelta = size;
        }

        public static VerticalLayoutGroup VList(Component t, float spacing, int pad, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = t.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childAlignment = align;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HList(Component t, float spacing, int pad, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childAlignment = align;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement LE(Component c, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();   // no ?? on Unity objects
            if (prefW >= 0) le.preferredWidth = prefW;
            if (prefH >= 0) le.preferredHeight = prefH;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            // A fixed height means fixed: without this, a row's HorizontalLayoutGroup (childForceExpandHeight)
            // reports itself as flexible and the parent stretches it into any spare space.
            else if (prefH >= 0) le.flexibleHeight = 0;
            return le;
        }

        /// <summary>Destroy runs at end of frame, so detach first; layouts and Find() then ignore old children.</summary>
        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        // ───────────── widgets ─────────────

        public static Image Box(string name, Transform parent, Color c, float cornerScale = 1.6f)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = Rounded();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cornerScale;   // bigger = smaller corners
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string s, int size, TextAnchor align = TextAnchor.UpperLeft, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var t = Rect("Text", parent).gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color ?? Ink;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            t.raycastTarget = false;
            t.text = s;
            return t;
        }

        /// <summary>Small caps label, used for section headers ("YOUR CHOICES").</summary>
        public static Text Overline(Transform parent, string s, Color? color = null) =>
            Label(parent, Spaced(s), 14, TextAnchor.UpperLeft, color ?? Accent, FontStyle.Bold);

        /// <summary>Header caps. (No extra letter spacing: spaced-out letters are hard for young readers and need tiny text.)</summary>
        public static string Spaced(string s) => s.ToUpper();

        public static Button Btn(Transform parent, string s, int size, Color c, float height, UnityAction onClick, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var img = Box("Button", parent, c);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.45f);
            b.colors = colors;
            var t = Label(img.transform, s, size, align);
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-10, -4));
            img.gameObject.AddComponent<ButtonFit>().label = t;   // never break a word in half
            if (height > 0) LE(img, prefH: height);
            if (onClick != null) b.onClick.AddListener(onClick);
            // Don't keep the button "selected": otherwise Enter/Space would press it again later
            // (e.g. closing the Mars Report with Enter would flip the last system tile clicked).
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null); });
            return b;
        }

        public static Image Star(Transform parent, float size)
        {
            var img = Rect("Star", parent).gameObject.AddComponent<Image>();
            img.sprite = StarSprite();
            img.preserveAspect = true;
            img.raycastTarget = false;
            LE(img, size, size);
            return img;
        }

        public static void SetStars(Image[] stars, int count)
        {
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < count ? Gold : new Color(1, 1, 1, 0.16f);
        }

        /// <summary>Vertical scroll area. Put content in 'content' (it has a VerticalLayoutGroup).</summary>
        public static ScrollRect Scroll(Transform parent, out RectTransform content, float spacing = 12)
        {
            var root = Rect("Scroll", parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);   // receives mouse wheel
            var sr = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", root);
            Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VList(content, spacing, 0);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30;
            return sr;
        }

        /// <summary>Thin progress bar. Returns the fill rect (set anchorMax.x to the value).</summary>
        public static RectTransform Bar(Transform parent, float height, Color back, Color fill, out Image fillImage)
        {
            var b = Box("Bar", parent, back, 3.5f);
            LE(b, prefH: height);
            fillImage = Box("Fill", b.transform, fill, 3.5f);
            var r = fillImage.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }

        /// <summary>
        /// A real NASA image card: picture (kept in proportion) + kid-friendly caption + title, date and credit.
        /// Returns null if the image isn't embedded.
        /// </summary>
        public static RectTransform NasaPhoto(Transform parent, string key, float imageHeight, bool caption = true)
        {
            var info = ContentDb.Image(key);
            var tex = ContentDb.ImageTexture(key);
            if (info == null || tex == null) return null;

            var card = Box("NasaPhoto_" + key, parent, CardBg);
            VList(card, 4, 10);
            var holder = Rect("Image", card.transform);
            LE(holder, prefH: imageHeight);
            var raw = Rect("Raw", holder).gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = tex.width / (float)tex.height;
            if (caption && !string.IsNullOrEmpty(info.caption)) Label(card.transform, info.caption, TextSmall);
            Label(card.transform, $"<b>Real NASA image</b>  ·  {info.title}  ·  {info.date}  ·  {info.credit}", 13, TextAnchor.UpperLeft, Muted);
            return card.rectTransform;
        }

        /// <summary>[[key|word]] -> highlighted word (plain Text has no links; chips handle taps).</summary>
        public static string Gloss(string raw) =>
            string.IsNullOrEmpty(raw) ? "" : GlossaryTag.Replace(raw, m =>
                "<color=#8FD6FF><b>" + (m.Groups[2].Success ? m.Groups[2].Value : (ContentDb.Glossary(m.Groups[1].Value)?.word ?? m.Groups[1].Value)) + "</b></color>");

        // ───────────── generated sprites ─────────────

        static Sprite rounded, star, fadeRight;

        /// <summary>Soft rounded rectangle, 9-sliced.</summary>
        public static Sprite Rounded()
        {
            if (rounded) return rounded;
            const int s = 64;
            const float r = 16;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var c = new Vector2(Mathf.Clamp(p.x, r, s - r), Mathf.Clamp(p.y, r, s - r));
                float a = Mathf.Clamp01(r - Vector2.Distance(p, c) + 0.5f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            rounded = Sprite.Create(Tex(s, s, px), new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return rounded;
        }

        /// <summary>Five-point star, anti-aliased.</summary>
        public static Sprite StarSprite()
        {
            if (star) return star;
            const int s = 96;
            var poly = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2 + i * Mathf.PI / 5;
                float rad = i % 2 == 0 ? 44 : 18;
                poly[i] = new Vector2(48 + Mathf.Cos(ang) * rad, 46 + Mathf.Sin(ang) * rad);
            }
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                    if (Inside(poly, new Vector2(x + (sx + 0.5f) / 4, y + (sy + 0.5f) / 4))) hits++;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
            }
            star = Sprite.Create(Tex(s, s, px), new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100);
            return star;
        }

        /// <summary>Horizontal fade (opaque left, clear right) for the title-screen scrim.</summary>
        public static Sprite FadeRight()
        {
            if (fadeRight) return fadeRight;
            const int w = 256;
            var px = new Color32[w * 4];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < w; x++)
            {
                float t = x / (w - 1f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255 * (1 - Mathf.SmoothStep(0, 1, t))));
            }
            fadeRight = Sprite.Create(Tex(w, 4, px), new Rect(0, 0, w, 4), new Vector2(0.5f, 0.5f), 100);
            return fadeRight;
        }

        static Texture2D Tex(int w, int h, Color32[] px)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    c = !c;
            return c;
        }
    }
}
