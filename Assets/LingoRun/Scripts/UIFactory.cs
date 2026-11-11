using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Small helpers for building uGUI hierarchies from code, so the whole game
    /// runs from a single bootstrap component with no scene/prefab wiring.
    /// Decorative graphics are created with raycastTarget = false; only buttons
    /// receive pointer events (keeps gameplay swipe input clean).
    /// </summary>
    public static class UIFactory
    {
        static Font _font;
        static Font _displayFont;
        public static bool UsingCustomFont { get; private set; }

        /// <summary>Body font: Nunito Bold (bundled in Resources/Fonts, covers
        /// Latin + Latin-ext + Cyrillic), falling back to Unity's built-in font
        /// if the asset is missing. For CJK UI languages the whole UI is served
        /// from the system font instead — Nunito has no glyphs there, and
        /// per-glyph OS fallback looks patchwork (proper rounded CJK fonts like
        /// M PLUS Rounded 1c / Jua can replace this later).</summary>
        public static Font DefaultFont
        {
            get
            {
                if (UseSystemFontForUILang()) return SystemFont;
                if (_font == null)
                {
                    _font = Resources.Load<Font>("Fonts/Nunito-Bold");
                    UsingCustomFont = _font != null;
                    if (_font == null)
                    {
                        try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                        catch { }
                    }
                    if (_font == null)
                    {
                        try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                        catch { }
                    }
                    if (_font == null)
                        _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return _font;
            }
        }

        /// <summary>Display font for titles and big numbers: Baloo 2 ExtraBold
        /// (Latin only — Russian UI uses the Nunito body font for titles too,
        /// since Baloo has no Cyrillic).</summary>
        public static Font DisplayFont
        {
            get
            {
                if (UseSystemFontForUILang()) return SystemFont;
                if (Loc.UILang == "ru") return DefaultFont;
                if (_displayFont == null)
                {
                    _displayFont = Resources.Load<Font>("Fonts/Baloo2-ExtraBold");
                    if (_displayFont == null) _displayFont = DefaultFont;
                }
                return _displayFont;
            }
        }

        static Font _systemFont;

        static bool UseSystemFontForUILang()
        {
            string l = Loc.UILang;
            return l == "ja" || l == "ko" || l == "zh";
        }

        static Font SystemFont
        {
            get
            {
                if (_systemFont == null)
                {
                    try { _systemFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { }
                    if (_systemFont == null)
                        _systemFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return _systemFont;
            }
        }

        // ---------- icons (Casual Icon Pack, Resources/Sprites/Icons) ----------

        static readonly System.Collections.Generic.Dictionary<string, Sprite> _iconCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite IconSprite(string icon)
        {
            Sprite s;
            if (!_iconCache.TryGetValue(icon, out s))
            {
                s = Resources.Load<Sprite>("Sprites/Icons/" + icon);
                _iconCache[icon] = s;
            }
            return s;
        }

        /// <summary>Image using a pack icon ("heart", "coin", "gem", "star",
        /// "lightning", "gift", "trophy", ...). Falls back to a flat colored
        /// circle if the sprite asset is missing, so callers can size/anchor
        /// the returned rect the same way as CreateCircle.</summary>
        public static Image CreateIcon(Transform parent, string name, string icon, Color fallback)
        {
            var s = IconSprite(icon);
            if (s == null) return CreateCircle(parent, name, fallback);
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // ---------- layout helpers ----------

        /// <summary>Full-stretch empty rect under parent.</summary>
        public static RectTransform CreateRoot(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Position with (x, yFromTop) measured like CSS: anchor top-left, pivot centered,
        /// so anchoredPosition (x, -yFromTop) puts the element's CENTER at that point.
        /// Matches the translateX(-50%) positioning of the web version's word chips.
        /// </summary>
        public static void PlaceTop(RectTransform rt, float centerX, float centerYFromTop, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(centerX, -centerYFromTop);
        }

        /// <summary>Horizontal band pinned to top, stretched across width with margins.</summary>
        public static void BandTop(RectTransform rt, float yFromTop, float height, float sideMargin = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(sideMargin, 0f);
            rt.offsetMax = new Vector2(-sideMargin, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
        }

        /// <summary>Horizontal band pinned to bottom.</summary>
        public static void BandBottom(RectTransform rt, float yFromBottom, float height, float sideMargin = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(sideMargin, 0f);
            rt.offsetMax = new Vector2(-sideMargin, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0f, yFromBottom);
        }

        // ---------- element builders ----------

        /// <summary>
        /// Panel with modern rounded corners by default (corner = radius in UI px).
        /// Pass corner = 0 for a sharp full-bleed rectangle (overlays, gradients).
        /// </summary>
        public static Image CreatePanel(Transform parent, string name, Color color, float corner = 18f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (corner > 0f) ApplyRounded(img, corner);
            Stretch(go.GetComponent<RectTransform>());
            return img;
        }

        /// <summary>Give an Image rounded corners using the shared 9-sliced sprite.</summary>
        public static void ApplyRounded(Image img, float corner)
        {
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            // Sprite border is 22px; displayed radius = border / multiplier.
            img.pixelsPerUnitMultiplier = 22f / Mathf.Max(4f, corner + 2f);
        }

        /// <summary>Soft drop shadow (uGUI effect) for cards and buttons.</summary>
        public static void AddSoftShadow(GameObject go, float distance = 3f, float alpha = 0.14f)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.12f, 0.08f, 0.22f, alpha);
            shadow.effectDistance = new Vector2(0f, -distance);
        }

        public static Text CreateText(Transform parent, string name, string content,
                                      int size, Color color,
                                      TextAnchor anchor = TextAnchor.MiddleCenter,
                                      FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.font = DefaultFont;
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = anchor;
            // Baloo 2 SemiBold is already heavy — faux-bolding it looks mushy.
            txt.fontStyle = (UsingCustomFont && style == FontStyle.Bold) ? FontStyle.Normal : style;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(go.GetComponent<RectTransform>());
            return txt;
        }

        public static Button CreateButton(Transform parent, string name, string label,
                                          int fontSize, Color bg, Color fg, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = true;
            ApplyRounded(img, 16f);
            AddSoftShadow(go);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            go.AddComponent<ButtonJuice>(); // press squash on every button
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.97f, 0.97f, 0.97f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var txt = CreateText(go.transform, "Label", label, fontSize, fg);
            txt.raycastTarget = false;
            return btn;
        }

        // ---------- generated sprites ----------

        static Sprite _circle;
        public static Sprite CircleSprite
        {
            get
            {
                if (_circle == null) _circle = MakeCircle(64);
                return _circle;
            }
        }

        static Sprite _rounded;
        /// <summary>64x64 anti-aliased rounded rect (radius 20) with a 22px 9-slice border.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null) _rounded = MakeRounded(64, 20);
                return _rounded;
            }
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var px = new Color[size * size];
            float r = radius;
            float max = size - 1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Signed distance to the rounded-rect: clamp to the inner box,
                    // measure distance to the corner circle centers.
                    float ix = Mathf.Clamp(x, r, max - r);
                    float iy = Mathf.Clamp(y, r, max - r);
                    float dx = x - ix, dy = y - iy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            float b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            float r = size * 0.5f - 1f;
            float cx = size * 0.5f - 0.5f, cy = size * 0.5f - 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f); // 1px anti-aliased edge
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Image CreateCircle(Transform parent, string name, Color color)
        {
            var img = CreatePanel(parent, name, color, 0f);
            img.sprite = CircleSprite;
            img.type = Image.Type.Simple;
            return img;
        }

        /// <summary>Vertical gradient (top -> bottom) as a stretched Image.</summary>
        public static Image CreateVerticalGradient(Transform parent, string name, Color top, Color bottom)
        {
            return CreateVerticalGradient(parent, name, new[] { top, bottom });
        }

        /// <summary>
        /// Vertical multi-stop gradient (top -> bottom), evenly spaced across
        /// however many colors are passed — e.g. a 3-tone sky (purple -> pink
        /// -> peach) instead of a flat 2-color blend. Needs at least 2 stops.
        /// </summary>
        public static Image CreateVerticalGradient(Transform parent, string name, Color[] stops)
        {
            const int H = 128;
            var tex = new Texture2D(1, H, TextureFormat.ARGB32, false);
            int segments = Mathf.Max(1, stops.Length - 1);
            for (int y = 0; y < H; y++)
            {
                // t=0 at the bottom pixel, t=1 at the top pixel (texture rows
                // are bottom-up), matching the top/bottom stop order in `stops`.
                float t = (float)y / (H - 1);
                float scaled = (1f - t) * segments;
                int seg = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, segments - 1);
                float local = scaled - seg;
                tex.SetPixel(0, y, Color.Lerp(stops[seg], stops[seg + 1], local));
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 100f);

            var img = CreatePanel(parent, name, Color.white, 0f);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            return img;
        }

        /// <summary>
        /// Flat-faceted filled polygon (see PolyImage) from boundary points
        /// radiating out from the shape's own local center (0,0). Used for the
        /// world map's low-poly islands/trees/rocks — facetColors, if given,
        /// must be the same length as boundaryPoints (one color per triangle).
        /// </summary>
        public static PolyImage CreatePolygon(Transform parent, string name, Color color,
                                               Vector2[] boundaryPoints, Color[] facetColors = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var poly = go.AddComponent<PolyImage>();
            poly.color = color;
            poly.raycastTarget = false;
            poly.SetPoints(boundaryPoints, facetColors);
            return poly;
        }

        /// <summary>A simple N-point star polygon (see CreatePolygon), centered
        /// on its own RectTransform — used for decorative sparkle accents.</summary>
        public static PolyImage CreateStar(Transform parent, string name, Color color,
                                            float radius, int points = 4, float innerRatio = 0.42f)
        {
            int n = points * 2;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float angle = (i / (float)n) * Mathf.PI * 2f - Mathf.PI * 0.5f;
                float r = (i % 2 == 0) ? radius : radius * innerRatio;
                pts[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            }
            var poly = CreatePolygon(parent, name, color, pts);
            poly.rectTransform.sizeDelta = new Vector2(radius * 2.4f, radius * 2.4f);
            return poly;
        }

        /// <summary>
        /// Soft glass-style highlight ellipse across the upper portion of a
        /// button/circle/card — the "glossy 3D icon" look. Purely anchor-based
        /// (a fraction of the host's own rect), so it works regardless of
        /// whether the host was sized with PlaceTop's explicit box or
        /// BandTop's stretched band.
        /// </summary>
        public static void AddGlossHighlight(Transform host, float widthFrac = 0.62f,
                                              float heightFrac = 0.34f, float alpha = 0.30f)
        {
            var go = new GameObject("Gloss", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(host, false);
            var img = go.GetComponent<Image>();
            img.sprite = CircleSprite;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            float margin = (1f - widthFrac) * 0.5f;
            const float topInset = 0.08f;
            rt.anchorMin = new Vector2(margin, Mathf.Max(0f, 1f - heightFrac - topInset));
            rt.anchorMax = new Vector2(1f - margin, 1f - topInset);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
