using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Shop: buy colors (12), shapes (8), trail styles (4) and run backgrounds
    /// (Theme.Backgrounds.Length — 3 procedural + 10 real baked-art scenes)
    /// with coins. Live preview pinned at the top; the catalogue scrolls beneath it.
    /// </summary>
    public class BallScreenUI
    {
        // Indices 0-7: procedural primitives. 8-18: real vehicle models
        // (RunningGame3D.VehicleShapes) — same order as GameConfig.ShapePrices'
        // vehicle tail, so ShapeNames[i]/ShapePrices[i] always line up.
        public static readonly string[] ShapeNames =
        {
            "BALL", "CUBE", "PILL", "DIAMOND", "EGG", "PUCK", "GEM", "UFO",
            "SEDAN", "SUV", "PICKUP", "CART", "BOAT", "SAMPAN", "SHIPWRECK",
            "FREIGHT WAGON", "PASSENGER WAGON", "TRAIN", "SHUTTLE",
        };
        public static readonly string[] TrailNames = { "CLASSIC", "LONG", "WIDE", "RAINBOW" };
        static readonly string[] TrailDescs =
        {
            "The standard streak",
            "A streak twice as long",
            "A thick, bold streak",
            "Full spectrum glow",
        };
        static readonly string[] BackgroundDescs =
        {
            "The classic grassy run",
            "Race among the stars",
            "Bright and bouncy",
            "Crunch through fallen leaves",
            "Campfire glow in the pines",
            "A snowy village aglow with lights",
            "Race past ancient pyramids",
            "Neon-lit city skyline",
            "Cherry blossoms over the isle",
            "Sprint down the pitch",
            "Countdown to liftoff",
            "Chase treasure on the tide",
            "Dust and rails out west",
        };

        public static bool ColorOwned(int idx) { return ProgressManager.OwnedSkins.Contains(idx); }
        public static bool ShapeOwned(int idx) { return ProgressManager.OwnedShapes.Contains(idx); }
        public static bool TrailOwned(int idx) { return ProgressManager.OwnedTrails.Contains(idx); }
        public static bool BackgroundOwned(int idx) { return ProgressManager.OwnedBackgrounds.Contains(idx); }

        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;

        Image _previewShape;
        Text _balanceText;
        Text _gemText;
        float _gemConfirmUntil; // double-tap confirmation window for gem exchange

        // Try-before-buy: tapping any item only PREVIEWS it here; buying/
        // equipping happens through the contextual action button on the
        // preview card. -1 = "follow whatever is equipped".
        int _prevColor = -1, _prevShape = -1, _prevTrail = -1, _prevBg = -1;
        Image _previewCardImg;
        RawImage _preview3D;
        GameObject _preview3DMask;

        // Big preview popup — opens on item tap, shows the 3D stage large
        // enough that background themes actually read as backgrounds.
        RectTransform _previewPopup;
        RawImage _popup3D;
        Text _popupTitle;
        Text _popupStatus;
        Image _popActionBg;
        Text _popActionLabel;
        readonly Image[] _popupTrailDots = new Image[3];
        readonly Image[] _previewTrailDots = new Image[3];
        Button _actionButton;
        Image _actionBg;
        Text _actionLabel;
        readonly Image[] _colorDots = new Image[12];
        readonly Image[] _colorRings = new Image[12];
        readonly Text[] _colorPriceLabels = new Text[12];
        readonly Image[] _shapeCards = new Image[ShapeNames.Length];
        readonly Image[] _shapeIcons = new Image[ShapeNames.Length];
        readonly Text[] _shapeNames = new Text[ShapeNames.Length];
        readonly Text[] _shapeDescs = new Text[ShapeNames.Length];
        readonly Image[] _trailCards = new Image[4];
        readonly Text[] _trailNamesT = new Text[4];
        readonly Text[] _trailDescsT = new Text[4];
        readonly Image[] _bgCards = new Image[Theme.Backgrounds.Length];
        readonly Image[] _bgSwatchSky = new Image[Theme.Backgrounds.Length];
        readonly Image[] _bgSwatchGround = new Image[Theme.Backgrounds.Length];
        readonly Text[] _bgNames = new Text[Theme.Backgrounds.Length];
        readonly Text[] _bgDescs = new Text[Theme.Backgrounds.Length];
        readonly bool[] _bgHasThumb = new bool[Theme.Backgrounds.Length]; // true once a baked photo swatch is showing

        // Corner price badges (coin icon + price) and equipped checkmarks.
        readonly GameObject[] _shapeBadges = new GameObject[ShapeNames.Length];
        readonly Text[] _shapeBadgePrices = new Text[ShapeNames.Length];
        readonly GameObject[] _shapeChecks = new GameObject[ShapeNames.Length];
        readonly GameObject[] _trailBadges = new GameObject[4];
        readonly Text[] _trailBadgePrices = new Text[4];
        readonly GameObject[] _trailChecks = new GameObject[4];
        readonly GameObject[] _bgBadges = new GameObject[Theme.Backgrounds.Length];
        readonly Text[] _bgBadgePrices = new Text[Theme.Backgrounds.Length];
        readonly GameObject[] _bgChecks = new GameObject[Theme.Backgrounds.Length];
        readonly GameObject[] _colorChecks = new GameObject[12];

        // Category tabs (COLOR / SHAPE / TRAIL / BG) — one category visible at a
        // time so the catalogue reads as a short list instead of one long scroll.
        static readonly string[] CategoryLabels = { "COLOR", "SHAPE", "TRAIL", "BG" };
        static string CatLabel(int i)
        {
            return i == 0 ? Loc.T("cat.color") : i == 1 ? Loc.T("cat.shape")
                : i == 2 ? Loc.T("cat.trail") : Loc.T("cat.bg");
        }
        readonly Image[] _catTabBg = new Image[4];
        readonly Text[] _catTabLabel = new Text[4];
        readonly RectTransform[] _catRoots = new RectTransform[4];
        readonly float[] _catHeights = new float[4];
        int _selectedCategory;
        ScrollRect _scroll;
        RectTransform _content;

        public BallScreenUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "BallScreen");
            Build();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            // Leaving the shop drops any un-bought preview back to equipped
            // and stops the 3D preview rig's camera.
            if (!visible) { _prevColor = _prevShape = _prevTrail = _prevBg = -1; }
            if (!visible && _previewPopup != null) _previewPopup.gameObject.SetActive(false);
            ShapePreview3D.SetActive(visible);
            UIFX.SetVisible(_root, visible);
        }

        void Build()
        {
            var t = _root.transform;

            var dim = UIFactory.CreatePanel(t, "Dim", new Color(0.98f, 0.97f, 1f, 0.65f), 0f);
            dim.raycastTarget = false;

            var back = UIFactory.CreateButton(t, "Back", Loc.T("common.back"), 15,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowHome(); });
            UIFactory.PlaceTop(back.GetComponent<RectTransform>(), 70f, 34f, 100f, 36f);

            var title = UIFactory.CreateText(t, "Title", Loc.T("shop.title"), 26, Theme.Orange);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 60f, 36f);

            // Coin balance pill (top right).
            var balancePill = UIFactory.CreatePanel(t, "BalancePill", Theme.CreamPanel, 16f);
            UIFactory.AddSoftShadow(balancePill.gameObject);
            UIFactory.PlaceTop(balancePill.rectTransform, 372f, 34f, 110f, 34f);
            var coin = UIFactory.CreateIcon(balancePill.transform, "Coin", "coin", Theme.Amber);
            var crt = coin.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(18f, 18f);
            crt.anchoredPosition = new Vector2(18f, 0f);
            _balanceText = UIFactory.CreateText(balancePill.transform, "Text", "0", 15, Theme.Ink);
            _balanceText.rectTransform.offsetMin = new Vector2(30f, 0f);

            // Gem pill (premium currency): exchanging 1 gem for coins needs
            // a CONFIRMING second tap within a few seconds — premium
            // currency must never be spendable by an accidental touch.
            var gemPill = UIFactory.CreateButton(t, "GemPill", "", 15,
                Theme.CreamPanel, Theme.Ink,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (ProgressManager.Gems <= 0) return;
                    if (Time.unscaledTime < _gemConfirmUntil)
                    {
                        _gemConfirmUntil = 0f;
                        if (ProgressManager.TrySpendGems(1))
                        {
                            ProgressManager.AddCoins(GameConfig.GemCoinExchangeRate);
                            ProgressManager.Save();
                            ProceduralAudio.PlayCorrect();
                        }
                        Refresh();
                    }
                    else
                    {
                        _gemConfirmUntil = Time.unscaledTime + 3f;
                        if (_gemText != null)
                            _gemText.text = Loc.F("shop.gem_confirm", GameConfig.GemCoinExchangeRate);
                    }
                });
            var gemImg = gemPill.GetComponent<Image>();
            UIFactory.ApplyRounded(gemImg, 16f);
            UIFactory.AddSoftShadow(gemPill.gameObject);
            UIFactory.PlaceTop(gemPill.GetComponent<RectTransform>(), 248f, 34f, 118f, 34f);
            var gemDot = UIFactory.CreateIcon(gemPill.transform, "Gem", "gem", Theme.Purple);
            var gdrt = gemDot.rectTransform;
            gdrt.anchorMin = gdrt.anchorMax = new Vector2(0f, 0.5f);
            gdrt.pivot = new Vector2(0.5f, 0.5f);
            gdrt.sizeDelta = new Vector2(18f, 18f);
            gdrt.anchoredPosition = new Vector2(17f, 0f);
            gemDot.raycastTarget = false;
            _gemText = UIFactory.CreateText(gemPill.transform, "Text", "0", 14, Theme.Ink, TextAnchor.MiddleLeft);
            _gemText.rectTransform.offsetMin = new Vector2(29f, 0f);
            _gemText.raycastTarget = false;

            // ----- preview card (pinned podium: ball + trail + bg tint) -----
            var previewCard = UIFactory.CreatePanel(t, "PreviewCard", Theme.CreamPanel, 22f);
            UIFactory.AddSoftShadow(previewCard.gameObject, 4f, 0.16f);
            UIFactory.BandTop(previewCard.rectTransform, 98f, 112f, 60f);
            _previewCardImg = previewCard;

            // Trail preview: three fading dots behind the ball.
            for (int i = 0; i < 3; i++)
            {
                var dot = UIFactory.CreateCircle(previewCard.transform, "TrailDot" + i, Color.white);
                var drt = dot.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(14f, 14f);
                drt.anchoredPosition = new Vector2(24f + i * 22f, 0f);
                dot.raycastTarget = false;
                _previewTrailDots[i] = dot;
            }

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            previewGo.transform.SetParent(previewCard.transform, false);
            _previewShape = previewGo.GetComponent<Image>();
            _previewShape.raycastTarget = false;
            var prt = previewGo.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(76f, 76f);
            prt.anchoredPosition = new Vector2(128f, 0f);

            // Live 3D render of the real model (see ShapePreview3D), clipped
            // to rounded corners by a mask so the mini-scene (theme sky +
            // ground behind the model) sits like a window inside the card.
            var maskGo = new GameObject("Preview3DMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(previewCard.transform, false);
            var maskImg = maskGo.GetComponent<Image>();
            maskImg.raycastTarget = false;
            UIFactory.ApplyRounded(maskImg, 16f);
            maskGo.GetComponent<Mask>().showMaskGraphic = false;
            var p3mrt = maskGo.GetComponent<RectTransform>();
            p3mrt.anchorMin = p3mrt.anchorMax = new Vector2(0f, 0.5f);
            p3mrt.pivot = new Vector2(0.5f, 0.5f);
            p3mrt.sizeDelta = new Vector2(166f, 96f);
            p3mrt.anchoredPosition = new Vector2(112f, 0f);

            var p3dGo = new GameObject("Preview3D", typeof(RectTransform), typeof(RawImage));
            p3dGo.transform.SetParent(maskGo.transform, false);
            _preview3D = p3dGo.GetComponent<RawImage>();
            _preview3D.raycastTarget = false;
            var p3rt = p3dGo.GetComponent<RectTransform>();
            p3rt.anchorMin = Vector2.zero;
            p3rt.anchorMax = Vector2.one;
            p3rt.offsetMin = Vector2.zero;
            p3rt.offsetMax = Vector2.zero;
            // The RT is square (sized for the big popup window) — the small
            // wide podium window shows its center band instead of stretching.
            _preview3D.uvRect = new Rect(0f, 0.22f, 1f, 0.56f);
            _preview3DMask = maskGo;

            // Trail dots draw ON TOP of the 3D window (they were created
            // earlier, so re-order them after it) — they read as the trail
            // streaking behind the model inside the little scene.
            for (int i = 0; i < 3; i++)
                if (_previewTrailDots[i] != null) _previewTrailDots[i].transform.SetAsLastSibling();

            // Contextual action button: EQUIPPED / EQUIP / BUY · price.
            _actionButton = UIFactory.CreateButton(previewCard.transform, "Action", "EQUIPPED", 14,
                Theme.Amber, Theme.WordChipDark,
                delegate { OnActionTap(); });
            var art = _actionButton.GetComponent<RectTransform>();
            art.anchorMin = art.anchorMax = new Vector2(1f, 0.5f);
            art.pivot = new Vector2(1f, 0.5f);
            art.sizeDelta = new Vector2(118f, 46f);
            art.anchoredPosition = new Vector2(-12f, 0f);
            _actionBg = _actionButton.GetComponent<Image>();
            _actionLabel = _actionButton.GetComponentInChildren<Text>();

            // ----- category tab bar (COLOR / SHAPE / TRAIL / BG) -----
            // Only one category's items are shown at a time, so the shop reads
            // as four short lists instead of one long scroll.
            var tabTrack = UIFactory.CreatePanel(t, "CatTrack", Theme.Hex("F1EFE8"), 19f);
            UIFactory.AddSoftShadow(tabTrack.gameObject, 2f, 0.10f);
            UIFactory.BandTop(tabTrack.rectTransform, 216f, 44f, 16f);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var tabBtn = UIFactory.CreateButton(tabTrack.transform, "Cat" + CategoryLabels[i],
                    CatLabel(i), 14, Theme.Hex("F1EFE8"), Theme.Ink,
                    delegate { ProceduralAudio.PlayClick(); SelectCategory(idx); });
                var tbrt = tabBtn.GetComponent<RectTransform>();
                tbrt.anchorMin = new Vector2(i / 4f, 0f);
                tbrt.anchorMax = new Vector2((i + 1) / 4f, 1f);
                tbrt.offsetMin = new Vector2(4f, 4f);
                tbrt.offsetMax = new Vector2(-4f, -4f);
                _catTabBg[i] = tabBtn.GetComponent<Image>();
                _catTabLabel[i] = tabBtn.GetComponentInChildren<Text>();
                _catTabLabel[i].font = UIFactory.DisplayFont;
            }

            // ----- scrollable catalogue (one category visible at a time) -----
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(t, false);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(1f, 1f, 1f, 0.01f);
            scrollImg.raycastTarget = true;
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(0f, 12f);
            scrollRt.offsetMax = new Vector2(0f, -266f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            _content = content;

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.content = content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 22f;

            // ===== COLOR category: 2 rows of 6 =====
            {
                var catRoot = UIFactory.CreateRoot(content.transform, "CatColor");
                _catRoots[0] = catRoot;
                var ct = catRoot.transform;
                float y = 10f;

                var caption = UIFactory.CreateText(ct, "Caption",
                    Loc.T("shop.tap_preview"), 13, Theme.InkSoft, TextAnchor.MiddleLeft);
                UIFactory.BandTop(caption.rectTransform, y, 16f, 28f);
                y += 28f;

                for (int i = 0; i < 12; i++)
                {
                    int idx = i;
                    int col = i % 6;
                    int row = i / 6;
                    float x = 52f + col * 67f;
                    float cy = y + 26f + row * 78f;

                    var ring = UIFactory.CreateCircle(ct, "ColorRing" + i, new Color(0f, 0f, 0f, 0f));
                    UIFactory.PlaceTop(ring.rectTransform, x, cy, 52f, 52f);
                    _colorRings[i] = ring;

                    var dotBtn = new GameObject("Color" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    dotBtn.transform.SetParent(ct, false);
                    var dotImg = dotBtn.GetComponent<Image>();
                    dotImg.sprite = UIFactory.CircleSprite;
                    dotImg.color = Theme.BallSkins[i];
                    dotImg.raycastTarget = true;
                    UIFactory.PlaceTop(dotBtn.GetComponent<RectTransform>(), x, cy, 42f, 42f);
                    dotBtn.GetComponent<Button>().onClick.AddListener(delegate { OnColorTap(idx); });
                    _colorDots[i] = dotImg;

                    var price = UIFactory.CreateText(ct, "ColorPrice" + i, "", 12, Theme.InkSoft);
                    UIFactory.PlaceTop(price.rectTransform, x, cy + 33f, 62f, 14f);
                    _colorPriceLabels[i] = price;

                    _colorChecks[i] = MakeCheck(ct);
                    UIFactory.PlaceTop(_colorChecks[i].GetComponent<RectTransform>(),
                        x + 16f, cy + 16f, 20f, 20f);
                }
                y += 26f + 78f + 52f;
                _catHeights[0] = y;
            }

            // ===== SHAPE category: 4 rows of 2 =====
            {
                var catRoot = UIFactory.CreateRoot(content.transform, "CatShape");
                _catRoots[1] = catRoot;
                var ct = catRoot.transform;
                float y = 10f;

                var caption = UIFactory.CreateText(ct, "Caption",
                    Loc.T("shop.tap_preview"), 13, Theme.InkSoft, TextAnchor.MiddleLeft);
                UIFactory.BandTop(caption.rectTransform, y, 16f, 28f);
                y += 24f;

                for (int i = 0; i < ShapeNames.Length; i++)
                {
                    int idx = i;
                    int col = i % 2;
                    int row = i / 2;

                    var cardGo = new GameObject("Shape" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    cardGo.transform.SetParent(ct, false);
                    var card = cardGo.GetComponent<Image>();
                    UIFactory.ApplyRounded(card, 16f);
                    card.raycastTarget = true;
                    UIFactory.AddSoftShadow(cardGo);
                    var rt = cardGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(col / 2f, 1f);
                    rt.anchorMax = new Vector2((col + 1) / 2f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(col == 0 ? 24f : 6f, 0f);
                    rt.offsetMax = new Vector2(col == 1 ? -24f : -6f, 0f);
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, 96f);
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(y + row * 106f));
                    cardGo.GetComponent<Button>().onClick.AddListener(delegate { OnShapeTap(idx); });
                    _shapeCards[i] = card;

                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(cardGo.transform, false);
                    var icon = iconGo.GetComponent<Image>();
                    icon.raycastTarget = false;
                    var irt = iconGo.GetComponent<RectTransform>();
                    irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
                    irt.pivot = new Vector2(0.5f, 0.5f);
                    irt.sizeDelta = new Vector2(34f, 34f);
                    irt.anchoredPosition = new Vector2(0f, -28f);
                    _shapeIcons[i] = icon;

                    var nameText = UIFactory.CreateText(cardGo.transform, "Name", ShapeNames[i], 15, Theme.Ink);
                    UIFactory.BandTop(nameText.rectTransform, 48f, 20f);
                    _shapeNames[i] = nameText;

                    var descText = UIFactory.CreateText(cardGo.transform, "Desc", "", 13,
                        Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Normal);
                    UIFactory.BandTop(descText.rectTransform, 68f, 18f);
                    _shapeDescs[i] = descText;

                    _shapeBadges[i] = MakeCardBadge(cardGo.transform, out _shapeBadgePrices[i]);
                    _shapeChecks[i] = MakeCheck(cardGo.transform);
                    PinCorner(_shapeChecks[i]);
                }
                y += Mathf.CeilToInt(ShapeNames.Length / 2f) * 106f + 14f;
                _catHeights[1] = y;
            }

            // ===== TRAIL category: 2 rows of 2 =====
            {
                var catRoot = UIFactory.CreateRoot(content.transform, "CatTrail");
                _catRoots[2] = catRoot;
                var ct = catRoot.transform;
                float y = 10f;

                var caption = UIFactory.CreateText(ct, "Caption",
                    Loc.T("shop.tap_preview"), 13, Theme.InkSoft, TextAnchor.MiddleLeft);
                UIFactory.BandTop(caption.rectTransform, y, 16f, 28f);
                y += 24f;

                for (int i = 0; i < 4; i++)
                {
                    int idx = i;
                    int col = i % 2;
                    int row = i / 2;

                    var cardGo = new GameObject("Trail" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    cardGo.transform.SetParent(ct, false);
                    var card = cardGo.GetComponent<Image>();
                    UIFactory.ApplyRounded(card, 16f);
                    card.raycastTarget = true;
                    UIFactory.AddSoftShadow(cardGo);
                    var rt = cardGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(col / 2f, 1f);
                    rt.anchorMax = new Vector2((col + 1) / 2f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(col == 0 ? 24f : 6f, 0f);
                    rt.offsetMax = new Vector2(col == 1 ? -24f : -6f, 0f);
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, 88f);
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(y + row * 98f));
                    cardGo.GetComponent<Button>().onClick.AddListener(delegate { OnTrailTap(idx); });
                    _trailCards[i] = card;

                    // Simple bar icon; rainbow gets three mini bars.
                    if (i == 3)
                    {
                        Color[] rc = { Theme.Hex("FF5E5E"), Theme.Hex("FFC94D"), Theme.Hex("4DC3FF") };
                        for (int b = 0; b < 3; b++)
                        {
                            var bar = UIFactory.CreatePanel(cardGo.transform, "Bar" + b, rc[b], 4f);
                            var brt = bar.rectTransform;
                            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
                            brt.pivot = new Vector2(0.5f, 0.5f);
                            brt.sizeDelta = new Vector2(52f, 6f);
                            brt.anchoredPosition = new Vector2(0f, -16f - b * 8f);
                        }
                    }
                    else
                    {
                        var bar = UIFactory.CreatePanel(cardGo.transform, "Bar", Theme.Hex("8FE3F7"), 5f);
                        var brt = bar.rectTransform;
                        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
                        brt.pivot = new Vector2(0.5f, 0.5f);
                        brt.sizeDelta = new Vector2(i == 1 ? 76f : 52f, i == 2 ? 14f : 7f);
                        brt.anchoredPosition = new Vector2(0f, -24f);
                    }

                    var nameText = UIFactory.CreateText(cardGo.transform, "Name", TrailNames[i], 15, Theme.Ink);
                    UIFactory.BandTop(nameText.rectTransform, 40f, 20f);
                    _trailNamesT[i] = nameText;

                    var descText = UIFactory.CreateText(cardGo.transform, "Desc", "", 13,
                        Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Normal);
                    UIFactory.BandTop(descText.rectTransform, 60f, 18f);
                    _trailDescsT[i] = descText;

                    _trailBadges[i] = MakeCardBadge(cardGo.transform, out _trailBadgePrices[i]);
                    _trailChecks[i] = MakeCheck(cardGo.transform);
                    PinCorner(_trailChecks[i]);
                }
                y += 2 * 98f + 20f;
                _catHeights[2] = y;
            }

            // ===== BACKGROUND category: 2 rows of 2 (3 themes) =====
            {
                var catRoot = UIFactory.CreateRoot(content.transform, "CatBackground");
                _catRoots[3] = catRoot;
                var ct = catRoot.transform;
                float y = 10f;

                var caption = UIFactory.CreateText(ct, "Caption",
                    Loc.T("shop.tap_preview"), 13, Theme.InkSoft, TextAnchor.MiddleLeft);
                UIFactory.BandTop(caption.rectTransform, y, 16f, 28f);
                y += 24f;

                for (int i = 0; i < Theme.Backgrounds.Length; i++)
                {
                    int idx = i;
                    int col = i % 2;
                    int row = i / 2;
                    var bg = Theme.Backgrounds[i];

                    var cardGo = new GameObject("Background" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    cardGo.transform.SetParent(ct, false);
                    var card = cardGo.GetComponent<Image>();
                    UIFactory.ApplyRounded(card, 16f);
                    card.raycastTarget = true;
                    UIFactory.AddSoftShadow(cardGo);
                    var rt = cardGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(col / 2f, 1f);
                    rt.anchorMax = new Vector2((col + 1) / 2f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(col == 0 ? 24f : 6f, 0f);
                    rt.offsetMax = new Vector2(col == 1 ? -24f : -6f, 0f);
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, 104f);
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(y + row * 114f));
                    cardGo.GetComponent<Button>().onClick.AddListener(delegate { OnBackgroundTap(idx); });
                    _bgCards[i] = card;

                    // Two-tone swatch: sky on top, ground below, rounded like a tiny screenshot.
                    var swatchGo = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
                    swatchGo.transform.SetParent(cardGo.transform, false);
                    var swatchImg = swatchGo.GetComponent<Image>();
                    swatchImg.raycastTarget = false;
                    UIFactory.ApplyRounded(swatchImg, 10f);
                    var srt = swatchGo.GetComponent<RectTransform>();
                    srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 1f);
                    srt.pivot = new Vector2(0.5f, 0.5f);
                    srt.sizeDelta = new Vector2(64f, 40f);
                    srt.anchoredPosition = new Vector2(0f, -26f);
                    _bgSwatchSky[i] = swatchImg;

                    var groundGo = new GameObject("Ground", typeof(RectTransform), typeof(Image));
                    groundGo.transform.SetParent(swatchGo.transform, false);
                    var groundImg = groundGo.GetComponent<Image>();
                    groundImg.raycastTarget = false;
                    var grt = groundGo.GetComponent<RectTransform>();
                    grt.anchorMin = new Vector2(0f, 0f);
                    grt.anchorMax = new Vector2(1f, 0.4f);
                    grt.offsetMin = Vector2.zero;
                    grt.offsetMax = Vector2.zero;
                    _bgSwatchGround[i] = groundImg;

                    // Real baked-art themes show the actual scene as the swatch
                    // instead of a flat two-tone color guess (falls back to the
                    // color swatch above if the bake hasn't been run yet).
                    if (!string.IsNullOrEmpty(bg.backdropSprite))
                    {
                        var bakedThumb = Resources.Load<Sprite>("Sprites/Backgrounds/" + bg.backdropSprite);
                        if (bakedThumb != null)
                        {
                            swatchImg.sprite = bakedThumb;
                            swatchImg.type = Image.Type.Simple;
                            swatchImg.preserveAspect = false;
                            groundGo.SetActive(false);
                            _bgHasThumb[i] = true;
                        }
                    }

                    var nameText = UIFactory.CreateText(cardGo.transform, "Name", bg.name, 15, Theme.Ink);
                    UIFactory.BandTop(nameText.rectTransform, 52f, 20f);
                    _bgNames[i] = nameText;

                    var descText = UIFactory.CreateText(cardGo.transform, "Desc", "", 13,
                        Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Normal);
                    UIFactory.BandTop(descText.rectTransform, 74f, 18f);
                    _bgDescs[i] = descText;

                    _bgBadges[i] = MakeCardBadge(cardGo.transform, out _bgBadgePrices[i]);
                    _bgChecks[i] = MakeCheck(cardGo.transform);
                    PinCorner(_bgChecks[i]);
                }
                int bgRows = Mathf.CeilToInt(Theme.Backgrounds.Length / 2f);
                y += bgRows * 114f + 20f;
                _catHeights[3] = y;
            }

            SelectCategory(0);
            BuildPreviewPopup(t);
        }

        /// <summary>Full-screen try-before-buy popup: big rounded 3D window
        /// (theme sky + ground + spinning model + trail), item name, and the
        /// contextual buy/equip button.</summary>
        void BuildPreviewPopup(Transform t)
        {
            var overlay = UIFactory.CreatePanel(t, "PreviewPopup", new Color(0.10f, 0.07f, 0.16f, 0.62f), 0f);
            overlay.raycastTarget = true;
            _previewPopup = overlay.rectTransform;

            var card = UIFactory.CreatePanel(overlay.transform, "Card", Theme.CreamPanel, 26f);
            UIFactory.AddSoftShadow(card.gameObject, 6f, 0.30f);
            UIFactory.BandTop(card.rectTransform, 84f, 620f, 24f);
            var ct = card.transform;

            // Big rounded 3D window.
            var maskGo = new GameObject("Window", typeof(RectTransform), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(ct, false);
            var maskImg = maskGo.GetComponent<Image>();
            maskImg.raycastTarget = false;
            UIFactory.ApplyRounded(maskImg, 20f);
            maskGo.GetComponent<Mask>().showMaskGraphic = false;
            UIFactory.BandTop(maskGo.GetComponent<RectTransform>(), 14f, 404f, 14f);

            var rawGo = new GameObject("Live", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(maskGo.transform, false);
            _popup3D = rawGo.GetComponent<RawImage>();
            _popup3D.raycastTarget = false;
            var rrt = rawGo.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            // Trail dots over the window (streak behind the model).
            for (int i = 0; i < 3; i++)
            {
                var dot = UIFactory.CreateCircle(ct, "PopTrail" + i, Color.white);
                var drt = dot.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(20f, 20f);
                drt.anchoredPosition = new Vector2(-108f + i * 34f, -232f);
                dot.raycastTarget = false;
                _popupTrailDots[i] = dot;
            }

            _popupTitle = UIFactory.CreateText(ct, "Title", "", 24, Theme.Ink);
            _popupTitle.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_popupTitle.rectTransform, 428f, 32f);

            _popupStatus = UIFactory.CreateText(ct, "Status", "", 14, Theme.InkSoft);
            UIFactory.BandTop(_popupStatus.rectTransform, 462f, 22f);

            var action = UIFactory.CreateButton(ct, "Action", "", 18,
                Theme.Amber, Theme.WordChipDark,
                delegate { OnActionTap(); UpdatePopup(); });
            UIFactory.BandTop(action.GetComponent<RectTransform>(), 492f, 56f, 60f);
            _popActionBg = action.GetComponent<Image>();
            _popActionLabel = action.GetComponentInChildren<Text>();

            var close = UIFactory.CreateButton(ct, "Close", "CLOSE", 15,
                Theme.CreamPanel, Theme.InkSoft,
                delegate { ProceduralAudio.PlayClick(); _previewPopup.gameObject.SetActive(false); });
            UIFactory.BandTop(close.GetComponent<RectTransform>(), 558f, 44f, 110f);

            _previewPopup.gameObject.SetActive(false);
        }

        void ShowPreviewPopup()
        {
            _previewPopup.gameObject.SetActive(true);
            UpdatePopup();
        }

        void UpdatePopup()
        {
            if (_previewPopup == null || !_previewPopup.gameObject.activeSelf) return;
            int cat = _selectedCategory;
            int idx = cat == 0 ? PrevColor : cat == 1 ? PrevShape : cat == 2 ? PrevTrail : PrevBg;

            _popup3D.texture = ShapePreview3D.LiveTexture;
            _popupTitle.text = cat == 0 ? "BALL COLOR " + (idx + 1)
                : cat == 1 ? ShapeNames[Mathf.Clamp(idx, 0, ShapeNames.Length - 1)]
                : cat == 2 ? TrailNames[Mathf.Clamp(idx, 0, TrailNames.Length - 1)] + " TRAIL"
                : Theme.Backgrounds[Mathf.Clamp(idx, 0, Theme.Backgrounds.Length - 1)].name.ToUpperInvariant();

            bool owned = cat == 0 ? ColorOwned(idx) : cat == 1 ? ShapeOwned(idx)
                : cat == 2 ? TrailOwned(idx) : BackgroundOwned(idx);
            int equipped = cat == 0 ? ProgressManager.SelectedSkin : cat == 1 ? ProgressManager.SelectedShape
                : cat == 2 ? ProgressManager.SelectedTrail : ProgressManager.SelectedBackground;
            int price = cat == 0 ? GameConfig.SkinPrices[idx] : cat == 1 ? GameConfig.ShapePrices[idx]
                : cat == 2 ? GameConfig.TrailPrices[idx] : GameConfig.BackgroundPrices[idx];
            _popupStatus.text = owned && idx == equipped ? Loc.T("shop.equipped_now")
                : owned ? Loc.T("shop.owned_equip")
                : Loc.F("shop.price_you", price, ProgressManager.Coins);

            ApplyActionState(_popActionBg, _popActionLabel);

            // Trail dots mirror the previewed trail style.
            Color skin = Theme.BallSkins[PrevColor];
            int pTrail = PrevTrail;
            for (int i = 0; i < 3; i++)
            {
                var dot = _popupTrailDots[i];
                if (dot == null) continue;
                float fade = 0.65f - i * 0.22f;
                Color c = pTrail == 3
                    ? (i == 0 ? Theme.Hex("FF8FC7") : i == 1 ? Theme.Hex("FFC94D") : Theme.Hex("35D0F0"))
                    : new Color(skin.r, skin.g, skin.b, fade);
                if (pTrail == 3) c.a = fade + 0.2f;
                dot.color = c;
                float size = pTrail == 2 ? 28f : 20f;
                float spacing = pTrail == 1 ? 44f : 34f;
                dot.rectTransform.sizeDelta = new Vector2(size, size);
                dot.rectTransform.anchoredPosition = new Vector2(-40f - (2 - i) * spacing, -232f);
            }
        }

        /// <summary>Show only the tapped category's items, resize the scroll
        /// content to that category's height, and jump the scroll back to top.</summary>
        void SelectCategory(int idx)
        {
            _selectedCategory = idx;
            for (int i = 0; i < 4; i++)
            {
                bool active = i == idx;
                _catRoots[i].gameObject.SetActive(active);
                _catTabBg[i].color = active ? Theme.Teal : Theme.Hex("F1EFE8");
                _catTabLabel[i].color = active ? Color.white : Theme.Ink;
            }
            _content.sizeDelta = new Vector2(_content.sizeDelta.x, _catHeights[idx]);
            _scroll.verticalNormalizedPosition = 1f;
            // The action button is contextual per category — retarget it.
            if (_actionLabel != null) UpdateActionButton();
        }

        // ==================== interactions ====================
        // Tapping an item only PREVIEWS it on the podium (try before buy);
        // the action button on the preview card confirms buy/equip.

        // Owned item: tap = equip instantly (fast browsing, no popup churn).
        // Unowned item: tap = preview + the try-before-buy popup.
        void OnColorTap(int idx)
        {
            ProceduralAudio.PlayClick();
            _prevColor = idx;
            if (ColorOwned(idx)) { ProgressManager.SelectedSkin = idx; ProgressManager.Save(); Refresh(); }
            else { Refresh(); ShowPreviewPopup(); }
        }

        void OnShapeTap(int idx)
        {
            ProceduralAudio.PlayClick();
            _prevShape = idx;
            if (ShapeOwned(idx)) { ProgressManager.SelectedShape = idx; ProgressManager.Save(); Refresh(); }
            else { Refresh(); ShowPreviewPopup(); }
        }

        void OnTrailTap(int idx)
        {
            ProceduralAudio.PlayClick();
            _prevTrail = idx;
            if (TrailOwned(idx)) { ProgressManager.SelectedTrail = idx; ProgressManager.Save(); Refresh(); }
            else { Refresh(); ShowPreviewPopup(); }
        }

        void OnBackgroundTap(int idx)
        {
            ProceduralAudio.PlayClick();
            _prevBg = idx;
            if (BackgroundOwned(idx)) { ProgressManager.SelectedBackground = idx; ProgressManager.Save(); Refresh(); }
            else { Refresh(); ShowPreviewPopup(); }
        }

        // Effective preview values (fall back to whatever is equipped).
        int PrevColor { get { return _prevColor >= 0 ? _prevColor : Mathf.Clamp(ProgressManager.SelectedSkin, 0, Theme.BallSkins.Length - 1); } }
        int PrevShape { get { return _prevShape >= 0 ? _prevShape : Mathf.Clamp(ProgressManager.SelectedShape, 0, ShapeNames.Length - 1); } }
        int PrevTrail { get { return _prevTrail >= 0 ? _prevTrail : Mathf.Clamp(ProgressManager.SelectedTrail, 0, TrailNames.Length - 1); } }
        int PrevBg { get { return _prevBg >= 0 ? _prevBg : Mathf.Clamp(ProgressManager.SelectedBackground, 0, Theme.Backgrounds.Length - 1); } }

        /// <summary>Buy (if needed) then equip the item currently previewed
        /// in the ACTIVE category.</summary>
        void OnActionTap()
        {
            int cat = _selectedCategory;
            int idx = cat == 0 ? PrevColor : cat == 1 ? PrevShape : cat == 2 ? PrevTrail : PrevBg;
            bool owned = cat == 0 ? ColorOwned(idx) : cat == 1 ? ShapeOwned(idx)
                : cat == 2 ? TrailOwned(idx) : BackgroundOwned(idx);
            int price = cat == 0 ? GameConfig.SkinPrices[idx] : cat == 1 ? GameConfig.ShapePrices[idx]
                : cat == 2 ? GameConfig.TrailPrices[idx] : GameConfig.BackgroundPrices[idx];

            if (!owned)
            {
                if (!ProgressManager.TrySpendCoins(price)) { ProceduralAudio.PlayWrong(); return; }
                if (cat == 0) ProgressManager.OwnedSkins.Add(idx);
                else if (cat == 1) ProgressManager.OwnedShapes.Add(idx);
                else if (cat == 2) ProgressManager.OwnedTrails.Add(idx);
                else ProgressManager.OwnedBackgrounds.Add(idx);
                ProceduralAudio.PlayCorrect();
            }
            else ProceduralAudio.PlayClick();

            if (cat == 0) ProgressManager.SelectedSkin = idx;
            else if (cat == 1) ProgressManager.SelectedShape = idx;
            else if (cat == 2) ProgressManager.SelectedTrail = idx;
            else ProgressManager.SelectedBackground = idx;
            ProgressManager.Save();
            Refresh();
        }

        /// <summary>Podium visuals: card tinted with the previewed background
        /// theme, trail dots styled per the previewed trail.</summary>
        void UpdatePreviewCard(int pColor, int pTrail, int pBg)
        {
            var bg = Theme.Backgrounds[Mathf.Clamp(pBg, 0, Theme.Backgrounds.Length - 1)];
            _previewCardImg.color = Color.Lerp(bg.skyTint, Color.white, 0.35f);

            Color skin = Theme.BallSkins[pColor];
            for (int i = 0; i < 3; i++)
            {
                var dot = _previewTrailDots[i];
                if (dot == null) continue;
                float fade = 0.65f - i * 0.22f;
                Color c = pTrail == 3
                    ? (i == 0 ? Theme.Hex("FF8FC7") : i == 1 ? Theme.Hex("FFC94D") : Theme.Hex("35D0F0"))
                    : new Color(skin.r, skin.g, skin.b, fade);
                if (pTrail == 3) c.a = fade + 0.2f;
                dot.color = c;
                float size = pTrail == 2 ? 20f : 14f;                 // wide
                float spacing = pTrail == 1 ? 30f : 22f;              // long
                dot.rectTransform.sizeDelta = new Vector2(size, size);
                dot.rectTransform.anchoredPosition = new Vector2(24f + (2 - i) * spacing, 0f);
            }
        }

        /// <summary>Contextual buy/equip button for the active category's
        /// previewed item.</summary>
        void UpdateActionButton()
        {
            int cat = _selectedCategory;
            int idx = cat == 0 ? PrevColor : cat == 1 ? PrevShape : cat == 2 ? PrevTrail : PrevBg;
            bool owned = cat == 0 ? ColorOwned(idx) : cat == 1 ? ShapeOwned(idx)
                : cat == 2 ? TrailOwned(idx) : BackgroundOwned(idx);
            int equipped = cat == 0 ? ProgressManager.SelectedSkin : cat == 1 ? ProgressManager.SelectedShape
                : cat == 2 ? ProgressManager.SelectedTrail : ProgressManager.SelectedBackground;
            int price = cat == 0 ? GameConfig.SkinPrices[idx] : cat == 1 ? GameConfig.ShapePrices[idx]
                : cat == 2 ? GameConfig.TrailPrices[idx] : GameConfig.BackgroundPrices[idx];

            ApplyActionState(_actionBg, _actionLabel, owned, idx == equipped, price);
        }

        /// <summary>Shared contextual button styling — used by both the
        /// podium's small button and the popup's big one.</summary>
        void ApplyActionState(Image bg, Text label)
        {
            int cat = _selectedCategory;
            int idx = cat == 0 ? PrevColor : cat == 1 ? PrevShape : cat == 2 ? PrevTrail : PrevBg;
            bool owned = cat == 0 ? ColorOwned(idx) : cat == 1 ? ShapeOwned(idx)
                : cat == 2 ? TrailOwned(idx) : BackgroundOwned(idx);
            int equipped = cat == 0 ? ProgressManager.SelectedSkin : cat == 1 ? ProgressManager.SelectedShape
                : cat == 2 ? ProgressManager.SelectedTrail : ProgressManager.SelectedBackground;
            int price = cat == 0 ? GameConfig.SkinPrices[idx] : cat == 1 ? GameConfig.ShapePrices[idx]
                : cat == 2 ? GameConfig.TrailPrices[idx] : GameConfig.BackgroundPrices[idx];
            ApplyActionState(bg, label, owned, idx == equipped, price);
        }

        void ApplyActionState(Image bg, Text label, bool owned, bool isEquipped, int price)
        {
            if (owned && isEquipped)
            {
                label.text = Loc.T("shop.equipped");
                bg.color = Theme.CreamPanel;
                label.color = Theme.InkSoft;
            }
            else if (owned)
            {
                label.text = Loc.T("shop.equip");
                bg.color = Theme.Teal;
                label.color = Color.white;
            }
            else if (ProgressManager.Coins >= price)
            {
                label.text = Loc.F("shop.buy", price);
                bg.color = Theme.Amber;
                label.color = Theme.WordChipDark;
            }
            else
            {
                label.text = Loc.F("shop.need", price);
                bg.color = Theme.CreamPanel;
                label.color = Theme.InkSoft;
            }
        }

        // ==================== visuals ====================

        /// <summary>Style a UI Image to suggest one of the 3D shapes.</summary>
        /// <summary>
        /// Style a UI Image to suggest one of the ball shapes. Public so other
        /// screens (e.g. WordBlasterGame3D's ship) can render the player's
        /// equipped shape+color the same way the shop does.
        /// </summary>
        public static void StyleShapeImage(Image img, int shapeIdx, Color color, float baseSize)
        {
            img.color = color;
            var rt = img.rectTransform;
            rt.localRotation = Quaternion.identity;
            img.sprite = UIFactory.RoundedSprite;
            img.type = Image.Type.Sliced;

            if (shapeIdx >= GameConfig.PrimitiveShapeCount)
            {
                // Vehicles (car/boat/train/...): one abstracted "vehicle"
                // silhouette (wide rounded rect) for all of them — these
                // are real 3D pack models in-game (RunningGame3D.
                // VehicleShapes), not simple shapes, so a bespoke 2D icon
                // per model isn't attempted here.
                img.pixelsPerUnitMultiplier = 22f / 10f;
                rt.sizeDelta = new Vector2(baseSize * 1.3f, baseSize * 0.6f);
                return;
            }

            switch (shapeIdx)
            {
                case 1: // cube
                    img.pixelsPerUnitMultiplier = 22f / 10f;
                    rt.sizeDelta = new Vector2(baseSize * 0.9f, baseSize * 0.9f);
                    break;
                case 2: // pill
                    img.pixelsPerUnitMultiplier = 22f / (baseSize * 0.45f);
                    rt.sizeDelta = new Vector2(baseSize * 1.15f, baseSize * 0.62f);
                    break;
                case 3: // diamond
                    img.pixelsPerUnitMultiplier = 22f / 8f;
                    rt.sizeDelta = new Vector2(baseSize * 0.78f, baseSize * 0.78f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case 4: // egg
                    img.sprite = UIFactory.CircleSprite;
                    img.type = Image.Type.Simple;
                    rt.sizeDelta = new Vector2(baseSize * 0.72f, baseSize);
                    break;
                case 5: // puck
                    img.sprite = UIFactory.CircleSprite;
                    img.type = Image.Type.Simple;
                    rt.sizeDelta = new Vector2(baseSize, baseSize * 0.38f);
                    break;
                case 6: // gem
                    img.pixelsPerUnitMultiplier = 22f / 8f;
                    rt.sizeDelta = new Vector2(baseSize * 0.55f, baseSize);
                    rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case 7: // ufo
                    img.sprite = UIFactory.CircleSprite;
                    img.type = Image.Type.Simple;
                    rt.sizeDelta = new Vector2(baseSize * 1.15f, baseSize * 0.45f);
                    break;
                default: // sphere
                    img.sprite = UIFactory.CircleSprite;
                    img.type = Image.Type.Simple;
                    rt.sizeDelta = new Vector2(baseSize, baseSize);
                    break;
            }
        }

        /// <summary>Corner price badge: amber pill with a coin icon + price,
        /// pinned to the card's top-right. Hidden for owned items.</summary>
        static GameObject MakeCardBadge(Transform card, out Text priceText)
        {
            var pill = UIFactory.CreatePanel(card, "PriceBadge",
                new Color(Theme.Amber.r, Theme.Amber.g, Theme.Amber.b, 0.30f), 11f);
            var rt = pill.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(66f, 22f);
            rt.anchoredPosition = new Vector2(-8f, -8f);
            pill.raycastTarget = false;

            var ic = UIFactory.CreateIcon(pill.transform, "Coin", "coin", Theme.AmberDeep);
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(14f, 14f);
            irt.anchoredPosition = new Vector2(12f, 0f);

            priceText = UIFactory.CreateText(pill.transform, "Price", "", 11,
                Theme.Hex("854F0B"), TextAnchor.MiddleLeft);
            priceText.raycastTarget = false;
            var prt = priceText.rectTransform;
            prt.offsetMin = new Vector2(21f, 0f);
            prt.offsetMax = new Vector2(-4f, 0f);
            return pill.gameObject;
        }

        /// <summary>Small teal circle with a white two-bar checkmark —
        /// marks the equipped item.</summary>
        static GameObject MakeCheck(Transform parent)
        {
            var circle = UIFactory.CreateCircle(parent, "Check", Theme.Teal);
            circle.raycastTarget = false;
            var rt = circle.rectTransform;
            rt.sizeDelta = new Vector2(20f, 20f);
            var shortBar = UIFactory.CreatePanel(circle.transform, "B1", Color.white, 1.5f);
            var b1 = shortBar.rectTransform;
            b1.anchorMin = b1.anchorMax = new Vector2(0.5f, 0.5f);
            b1.pivot = new Vector2(0.5f, 0.5f);
            b1.sizeDelta = new Vector2(6f, 2.6f);
            b1.anchoredPosition = new Vector2(-3.4f, -1.6f);
            b1.localRotation = Quaternion.Euler(0f, 0f, 45f);
            shortBar.raycastTarget = false;
            var longBar = UIFactory.CreatePanel(circle.transform, "B2", Color.white, 1.5f);
            var b2 = longBar.rectTransform;
            b2.anchorMin = b2.anchorMax = new Vector2(0.5f, 0.5f);
            b2.pivot = new Vector2(0.5f, 0.5f);
            b2.sizeDelta = new Vector2(10f, 2.6f);
            b2.anchoredPosition = new Vector2(1.6f, -0.4f);
            b2.localRotation = Quaternion.Euler(0f, 0f, -45f);
            longBar.raycastTarget = false;
            return circle.gameObject;
        }

        static void PinCorner(GameObject check)
        {
            var rt = check.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(20f, 20f);
            rt.anchoredPosition = new Vector2(-16f, -16f);
        }

        public void Refresh()
        {
            int selColor = Mathf.Clamp(ProgressManager.SelectedSkin, 0, Theme.BallSkins.Length - 1);
            int selShape = Mathf.Clamp(ProgressManager.SelectedShape, 0, ShapeNames.Length - 1);
            int selTrail = Mathf.Clamp(ProgressManager.SelectedTrail, 0, TrailNames.Length - 1);
            // Previewed (not necessarily owned) picks drive all the visuals.
            int pColor = PrevColor, pShape = PrevShape, pTrail = PrevTrail, pBg = PrevBg;

            _balanceText.text = ProgressManager.Coins.ToString();
            _gemConfirmUntil = 0f; // any refresh clears a pending confirmation
            if (_gemText != null)
                _gemText.text = ProgressManager.Gems + " » +" + GameConfig.GemCoinExchangeRate;
            // Podium: real 3D model staged in the previewed background theme,
            // 2D emblem as fallback.
            var stageBg = Theme.Backgrounds[Mathf.Clamp(pBg, 0, Theme.Backgrounds.Length - 1)];
            bool live3d = ShapePreview3D.ShowLive(pShape, Theme.BallSkins[pColor], stageBg);
            _preview3DMask.SetActive(live3d);
            if (live3d) _preview3D.texture = ShapePreview3D.LiveTexture;
            _previewShape.gameObject.SetActive(!live3d);
            if (!live3d) StyleShapeImage(_previewShape, pShape, Theme.BallSkins[pColor], 76f);
            UpdatePreviewCard(pColor, pTrail, pBg);
            UpdateActionButton();

            for (int i = 0; i < 12; i++)
            {
                bool owned = ColorOwned(i);
                bool affordable = ProgressManager.Coins >= GameConfig.SkinPrices[i];
                var c = Theme.BallSkins[i];
                _colorDots[i].color = owned ? c : new Color(c.r, c.g, c.b, affordable ? 0.45f : 0.20f);
                _colorRings[i].color = i == pColor
                    ? new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.85f)
                    : new Color(0f, 0f, 0f, 0f);
                _colorPriceLabels[i].text = owned ? "" : GameConfig.SkinPrices[i].ToString();
                _colorPriceLabels[i].color = affordable ? Theme.Teal : Theme.InkSoft;
                if (_colorChecks[i] != null)
                    _colorChecks[i].SetActive(owned && i == Mathf.Clamp(ProgressManager.SelectedSkin, 0, 11));
            }

            for (int i = 0; i < ShapeNames.Length; i++)
            {
                bool owned = ShapeOwned(i);
                bool affordable = ProgressManager.Coins >= GameConfig.ShapePrices[i];
                bool selected = i == pShape;
                _shapeCards[i].color = selected
                    ? new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.20f)
                    : (owned ? Theme.CreamPanel : new Color(1f, 1f, 1f, 0.45f));
                // Vehicle cards get a baked 3D thumbnail of the real model;
                // primitives keep the accurate-enough 2D emblem.
                Sprite thumb = i >= GameConfig.PrimitiveShapeCount ? ShapePreview3D.VehicleThumbnail(i) : null;
                if (thumb != null)
                {
                    _shapeIcons[i].sprite = thumb;
                    _shapeIcons[i].type = Image.Type.Simple;
                    _shapeIcons[i].preserveAspect = true;
                    _shapeIcons[i].color = owned ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    _shapeIcons[i].rectTransform.sizeDelta = new Vector2(56f, 34f);
                }
                else
                {
                    StyleShapeImage(_shapeIcons[i], i,
                        owned ? Theme.BallSkins[selColor] : new Color(0.5f, 0.5f, 0.55f, 0.5f), 34f);
                }
                _shapeNames[i].color = selected ? Theme.Teal : (owned ? Theme.Ink : Theme.InkSoft);
                if (owned)
                    _shapeDescs[i].text = i == selShape ? Loc.T("shop.equipped_now") : Loc.T("shop.tap_equip");
                else
                    _shapeDescs[i].text = "";
                if (_shapeBadges[i] != null)
                {
                    _shapeBadges[i].SetActive(!owned);
                    _shapeBadgePrices[i].text = GameConfig.ShapePrices[i].ToString();
                    _shapeBadgePrices[i].color = affordable ? Theme.Hex("854F0B") : Theme.InkSoft;
                }
                if (_shapeChecks[i] != null) _shapeChecks[i].SetActive(owned && i == selShape);
            }

            for (int i = 0; i < TrailNames.Length; i++)
            {
                bool owned = TrailOwned(i);
                bool affordable = ProgressManager.Coins >= GameConfig.TrailPrices[i];
                bool selected = i == pTrail;
                _trailCards[i].color = selected
                    ? new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.20f)
                    : (owned ? Theme.CreamPanel : new Color(1f, 1f, 1f, 0.45f));
                _trailNamesT[i].color = selected ? Theme.Teal : (owned ? Theme.Ink : Theme.InkSoft);
                if (owned)
                    _trailDescsT[i].text = i == selTrail ? Loc.T("shop.equipped_now") : TrailDescs[i];
                else
                    _trailDescsT[i].text = "";
                if (_trailBadges[i] != null)
                {
                    _trailBadges[i].SetActive(!owned);
                    _trailBadgePrices[i].text = GameConfig.TrailPrices[i].ToString();
                    _trailBadgePrices[i].color = affordable ? Theme.Hex("854F0B") : Theme.InkSoft;
                }
                if (_trailChecks[i] != null) _trailChecks[i].SetActive(owned && i == selTrail);
            }

            int selBackground = Mathf.Clamp(ProgressManager.SelectedBackground, 0, Theme.Backgrounds.Length - 1);
            for (int i = 0; i < Theme.Backgrounds.Length; i++)
            {
                bool owned = BackgroundOwned(i);
                bool affordable = ProgressManager.Coins >= GameConfig.BackgroundPrices[i];
                bool selected = i == pBg;
                var bg = Theme.Backgrounds[i];
                _bgCards[i].color = selected
                    ? new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.20f)
                    : (owned ? Theme.CreamPanel : new Color(1f, 1f, 1f, 0.45f));
                float dim = owned ? 1f : 0.55f;
                // Baked photo swatches get dimmed toward black (not tinted by the
                // theme's sky color, which would color-cast the real photo).
                _bgSwatchSky[i].color = _bgHasThumb[i]
                    ? new Color(dim, dim, dim, 1f)
                    : new Color(bg.skyTint.r * dim, bg.skyTint.g * dim, bg.skyTint.b * dim, 1f);
                _bgSwatchGround[i].color = new Color(bg.groundColor.r * dim, bg.groundColor.g * dim, bg.groundColor.b * dim, 1f);
                _bgNames[i].color = selected ? Theme.Teal : (owned ? Theme.Ink : Theme.InkSoft);
                if (owned)
                    _bgDescs[i].text = i == selBackground ? Loc.T("shop.equipped_now") : BackgroundDescs[i];
                else
                    _bgDescs[i].text = "";
                if (_bgBadges[i] != null)
                {
                    _bgBadges[i].SetActive(!owned);
                    _bgBadgePrices[i].text = GameConfig.BackgroundPrices[i].ToString();
                    _bgBadgePrices[i].color = affordable ? Theme.Hex("854F0B") : Theme.InkSoft;
                }
                if (_bgChecks[i] != null) _bgChecks[i].SetActive(owned && i == selBackground);
            }
        }
    }
}
