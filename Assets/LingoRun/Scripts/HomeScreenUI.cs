using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Home screen v2 — one primary action, zero duplicate navigation:
    ///   - top bar: avatar + name (left), hearts + coins pills (right)
    ///   - "today" row: day streak + daily-goal progress bar + reward star
    ///   - THE unit hero card, tinted by the current unit's topic theme:
    ///     unit/topic label, MAP chip, level number, next mode + pace,
    ///     equipped-ball preview, the unit's 5 mode slots with the chest at
    ///     the end, and the single big PLAY button (launches the map's
    ///     designated mode for the current level via StartFromMap)
    ///   - two practice cards, always available: REVIEW (badge with pool
    ///     count, earns a heart) and SPEAK (pronunciation)
    ///   - bottom tab bar (Shop / Dictionary / HOME / Awards / Player) —
    ///     the ONLY navigation; the old side badges, MAP pill, level path
    ///     and mode/pace segments are gone (map picks mode+pace now, free
    ///     pace choice lives in Settings, reachable from the PLAYER tab).
    /// </summary>
    public class HomeScreenUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;

        Text _avatarLetter;
        Text _nameText;
        Text _heartsPillText;
        Text _coinsPillText;

        Text _streakText;
        RectTransform _goalFill;
        Text _goalCountText;
        Image _goalStar;

        Image _unitCard;
        Text _unitLabel;
        Text _levelNumText;
        Text _levelSubText;
        readonly Image[] _slotBgs = new Image[GameConfig.LevelsPerUnit];
        readonly Text[] _slotLabels = new Text[GameConfig.LevelsPerUnit];
        readonly GameObject[] _slotChecks = new GameObject[GameConfig.LevelsPerUnit];

        Image _reviewCard;
        Image _reviewBubbleBg;
        Text _reviewBubbleText;

        Image _tabBallIcon;

        // Theme color per unit topic — the hero card (and with it the whole
        // screen's mood) changes as the player moves through units.
        static readonly Dictionary<string, Color> TopicColors = new Dictionary<string, Color>
        {
            { "essentials", Theme.Hex("534AB7") },
            { "food",       Theme.Hex("993C1D") },
            { "home",       Theme.Hex("3B6D11") },
            { "love",       Theme.Hex("993556") },
            { "nature",     Theme.Hex("0F6E56") },
            { "work",       Theme.Hex("44506B") },
            { "tech",       Theme.Hex("0C447C") },
            { "fashion",    Theme.Hex("72243E") },
            { "travel",     Theme.Hex("185FA5") },
            { "sports",     Theme.Hex("A32D2D") },
            { "school",     Theme.Hex("854F0B") },
            { "books",      Theme.Hex("712B13") },
            { "music",      Theme.Hex("3C3489") },
            { "movies",     Theme.Hex("26215C") },
            { "art",        Theme.Hex("8E2F63") },
            { "coffee",     Theme.Hex("4A2C17") },
        };

        public HomeScreenUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "HomeScreen");
            Build();
        }

        public void SetVisible(bool visible) { UIFX.SetVisible(_root, visible); }

        // ==================== construction ====================

        void Build()
        {
            var t = _root.transform;
            BuildTopBar(t);
            BuildTodayRow(t);
            BuildUnitCard(t);      // "career" mode — the themed unit adventure
            BuildFreePlayCard(t);  // "arcade" mode — equal size, equal billing
            BuildReviewStrip(t);
            BuildTabBar(t);
        }

        void BuildTopBar(Transform t)
        {
            var avatarBtn = UIFactory.CreateButton(t, "Avatar", "", 20,
                Theme.Purple, Color.white,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowLogin(); });
            var avatarImg = avatarBtn.GetComponent<Image>();
            avatarImg.sprite = UIFactory.CircleSprite;
            avatarImg.type = Image.Type.Simple;
            UIFactory.PlaceTop(avatarBtn.GetComponent<RectTransform>(), 48f, 46f, 52f, 52f);
            UIFactory.AddGlossHighlight(avatarBtn.transform, 0.66f, 0.30f, 0.32f);
            _avatarLetter = avatarBtn.GetComponentInChildren<Text>();
            _avatarLetter.fontSize = 24;
            _avatarLetter.font = UIFactory.DisplayFont;

            _nameText = UIFactory.CreateText(t, "Name", "", 18, Theme.Ink, TextAnchor.MiddleLeft);
            _nameText.font = UIFactory.DisplayFont;
            UIFactory.PlaceTop(_nameText.rectTransform, 138f, 46f, 122f, 28f);

            // Hearts pill (shows a regen countdown when not full).
            var heartsPill = UIFactory.CreatePanel(t, "HeartsPill", Color.white, 17f);
            UIFactory.AddSoftShadow(heartsPill.gameObject, 3f, 0.12f);
            UIFactory.PlaceTop(heartsPill.rectTransform, 270f, 46f, 122f, 42f);
            var heartIcon = UIFactory.CreateIcon(heartsPill.transform, "Icon", "heart", Theme.HeartRed);
            var hirt = heartIcon.rectTransform;
            hirt.anchorMin = hirt.anchorMax = new Vector2(0f, 0.5f);
            hirt.pivot = new Vector2(0.5f, 0.5f);
            hirt.sizeDelta = new Vector2(22f, 22f);
            hirt.anchoredPosition = new Vector2(20f, 0f);
            _heartsPillText = UIFactory.CreateText(heartsPill.transform, "Text", "5", 16, Theme.Ink, TextAnchor.MiddleLeft);
            var hprt = _heartsPillText.rectTransform;
            hprt.offsetMin = new Vector2(34f, 0f);
            hprt.offsetMax = new Vector2(-6f, 0f);

            // Coins pill.
            var coinsPill = UIFactory.CreatePanel(t, "CoinsPill", Color.white, 17f);
            UIFactory.AddSoftShadow(coinsPill.gameObject, 3f, 0.12f);
            UIFactory.PlaceTop(coinsPill.rectTransform, 385f, 46f, 102f, 42f);
            var coinIcon = UIFactory.CreateIcon(coinsPill.transform, "Icon", "coin", Theme.Amber);
            var cirt = coinIcon.rectTransform;
            cirt.anchorMin = cirt.anchorMax = new Vector2(0f, 0.5f);
            cirt.pivot = new Vector2(0.5f, 0.5f);
            cirt.sizeDelta = new Vector2(22f, 22f);
            cirt.anchoredPosition = new Vector2(20f, 0f);
            _coinsPillText = UIFactory.CreateText(coinsPill.transform, "Text", "0", 16, Theme.Ink, TextAnchor.MiddleLeft);
            var cprt = _coinsPillText.rectTransform;
            cprt.offsetMin = new Vector2(34f, 0f);
            cprt.offsetMax = new Vector2(-6f, 0f);
        }

        void BuildTodayRow(Transform t)
        {
            var row = UIFactory.CreatePanel(t, "TodayRow", Color.white, 14f);
            UIFactory.AddSoftShadow(row.gameObject, 3f, 0.12f);
            UIFactory.BandTop(row.rectTransform, 84f, 50f, 24f);
            var rt2 = row.transform;

            var flame = UIFactory.CreateIcon(rt2, "Flame", "lightning", Theme.Orange);
            var frt = flame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(22f, 22f);
            frt.anchoredPosition = new Vector2(22f, 0f);

            _streakText = UIFactory.CreateText(rt2, "Streak", "DAY 0", 15, Theme.Hex("993C1D"), TextAnchor.MiddleLeft);
            var srt = _streakText.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(74f, 0f);
            srt.anchoredPosition = new Vector2(38f, 0f);

            // Progress bar (bg + fill).
            var barBg = UIFactory.CreatePanel(rt2, "BarBg", Theme.Hex("F1EFE8"), 4f);
            var brt = barBg.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.offsetMin = new Vector2(118f, -5f);
            brt.offsetMax = new Vector2(-104f, 5f);

            var fill = UIFactory.CreatePanel(barBg.transform, "Fill", Theme.AmberDeep, 4f);
            _goalFill = fill.rectTransform;
            _goalFill.anchorMin = new Vector2(0f, 0f);
            _goalFill.anchorMax = new Vector2(0.02f, 1f);
            _goalFill.offsetMin = Vector2.zero;
            _goalFill.offsetMax = Vector2.zero;

            _goalCountText = UIFactory.CreateText(rt2, "Count", "0/" + GameConfig.DailyGoal, 14,
                Theme.InkSoft, TextAnchor.MiddleRight);
            var grt = _goalCountText.rectTransform;
            grt.anchorMin = new Vector2(1f, 0f);
            grt.anchorMax = new Vector2(1f, 1f);
            grt.pivot = new Vector2(1f, 0.5f);
            grt.sizeDelta = new Vector2(70f, 0f);
            grt.anchoredPosition = new Vector2(-32f, 0f);

            // Reward star at the end of the bar — turns teal when the goal
            // pays out (+DailyGoalCoins).
            _goalStar = UIFactory.CreateIcon(rt2, "Reward", "star", Theme.Amber);
            var starRt = _goalStar.rectTransform;
            starRt.anchorMin = starRt.anchorMax = new Vector2(1f, 0.5f);
            starRt.pivot = new Vector2(0.5f, 0.5f);
            starRt.sizeDelta = new Vector2(24f, 24f);
            starRt.anchoredPosition = new Vector2(-17f, 0f);
        }

        void BuildUnitCard(Transform t)
        {
            var card = UIFactory.CreatePanel(t, "UnitCard", Theme.Hex("185FA5"), 26f);
            UIFactory.AddSoftShadow(card.gameObject, 6f, 0.25f);
            UIFactory.BandTop(card.rectTransform, 146f, 236f, 24f);
            _unitCard = card;
            var ct = card.transform;

            _unitLabel = UIFactory.CreateText(ct, "UnitLabel", "UNIT 1  ·  ESSENTIALS", 16,
                new Color(1f, 1f, 1f, 0.78f), TextAnchor.MiddleLeft);
            UIFactory.BandTop(_unitLabel.rectTransform, 16f, 24f, 20f);

            // MAP chip, top-right.
            var mapChip = UIFactory.CreateButton(ct, "MapChip", Loc.T("home.map"), 14,
                new Color(1f, 1f, 1f, 0.18f), Color.white,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowMap(); });
            var mcrt = mapChip.GetComponent<RectTransform>();
            mcrt.anchorMin = mcrt.anchorMax = new Vector2(1f, 1f);
            mcrt.pivot = new Vector2(1f, 0.5f);
            mcrt.sizeDelta = new Vector2(92f, 36f);
            mcrt.anchoredPosition = new Vector2(-16f, -28f);

            _levelNumText = UIFactory.CreateText(ct, "LevelNum", "LEVEL 1", 30, Color.white, TextAnchor.MiddleLeft);
            _levelNumText.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_levelNumText.rectTransform, 44f, 40f, 20f);

            _levelSubText = UIFactory.CreateText(ct, "LevelSub", "", 14,
                new Color(1f, 1f, 1f, 0.78f), TextAnchor.MiddleLeft);
            UIFactory.BandTop(_levelSubText.rectTransform, 84f, 22f, 20f);


            // The unit's 5 mode slots + the chest at the end of the road.
            const float slotY = -126f;
            for (int i = 0; i < GameConfig.LevelsPerUnit; i++)
            {
                var slot = UIFactory.CreateCircle(ct, "Slot" + i, new Color(1f, 1f, 1f, 0.22f));
                var srt = slot.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 1f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.sizeDelta = new Vector2(32f, 32f);
                srt.anchoredPosition = new Vector2(36f + i * 42f, slotY);
                _slotBgs[i] = slot;

                var label = UIFactory.CreateText(slot.transform, "L", "?", 15, Color.white);
                label.font = UIFactory.DisplayFont;
                label.raycastTarget = false;
                _slotLabels[i] = label;

                // Little check for DONE slots (color-blind friendly): two
                // white bars forming a ✓, hidden until the slot is cleared.
                var checkGo = new GameObject("Check", typeof(RectTransform));
                checkGo.transform.SetParent(slot.transform, false);
                var crt2 = checkGo.GetComponent<RectTransform>();
                crt2.anchorMin = crt2.anchorMax = new Vector2(0.5f, 0.5f);
                crt2.pivot = new Vector2(0.5f, 0.5f);
                crt2.sizeDelta = new Vector2(20f, 20f);
                var barA = UIFactory.CreatePanel(checkGo.transform, "A", Color.white, 1.5f);
                var bart = barA.rectTransform;
                bart.anchorMin = bart.anchorMax = new Vector2(0.5f, 0.5f);
                bart.pivot = new Vector2(0.5f, 0.5f);
                bart.sizeDelta = new Vector2(6f, 3.2f);
                bart.anchoredPosition = new Vector2(-4.4f, -2.6f);
                bart.localRotation = Quaternion.Euler(0f, 0f, 45f);
                barA.raycastTarget = false;
                var barB = UIFactory.CreatePanel(checkGo.transform, "B", Color.white, 1.5f);
                var bbrt2 = barB.rectTransform;
                bbrt2.anchorMin = bbrt2.anchorMax = new Vector2(0.5f, 0.5f);
                bbrt2.pivot = new Vector2(0.5f, 0.5f);
                bbrt2.sizeDelta = new Vector2(12f, 3.2f);
                bbrt2.anchoredPosition = new Vector2(1.6f, -0.6f);
                bbrt2.localRotation = Quaternion.Euler(0f, 0f, -48f);
                barB.raycastTarget = false;
                checkGo.SetActive(false);
                _slotChecks[i] = checkGo;
            }

            // Chest: gift-box icon (Casual Icon Pack).
            var chest = UIFactory.CreateIcon(ct, "Chest", "gift", Theme.AmberDeep);
            var chrt = chest.rectTransform;
            chrt.anchorMin = chrt.anchorMax = new Vector2(1f, 1f);
            chrt.pivot = new Vector2(0.5f, 0.5f);
            chrt.sizeDelta = new Vector2(38f, 38f);
            chrt.anchoredPosition = new Vector2(-38f, slotY);


            // The one big PLAY — launches the map-designated mode for the
            // current level (mode + pace come from the unit, not a selector).
            var play = UIFactory.CreateButton(ct, "Play", Loc.T("home.continue"), 22,
                Theme.Amber, Theme.WordChipDark,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    _boot.StartFromMap(ProgressManager.CurrentLevel);
                });
            var playImg = play.GetComponent<Image>();
            UIFactory.ApplyRounded(playImg, 24f);
            UIFactory.BandTop(play.GetComponent<RectTransform>(), 166f, 54f, 16f);
            UIFactory.AddSoftShadow(play.gameObject, 4f, 0.25f);
            UIFactory.AddGlossHighlight(play.transform, 0.92f, 0.30f, 0.16f);
            var playLabel = play.GetComponentInChildren<Text>();
            playLabel.font = UIFactory.DisplayFont;

        }

        /// <summary>The "arcade" mode card — same size and visual weight
        /// as the unit adventure card above it, so the two ways to play read
        /// as equals. Purple identity, four mini game tiles, its own big
        /// button opening the game picker.</summary>
        void BuildFreePlayCard(Transform t)
        {
            var card = UIFactory.CreatePanel(t, "FreePlayCard", Theme.Hex("3C3489"), 26f);
            UIFactory.AddSoftShadow(card.gameObject, 6f, 0.25f);
            UIFactory.BandTop(card.rectTransform, 394f, 236f, 24f);
            var ct = card.transform;

            var title = UIFactory.CreateText(ct, "Title", Loc.T("picker.title"), 24,
                Color.white, TextAnchor.MiddleLeft);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 16f, 32f, 20f);

            var sub = UIFactory.CreateText(ct, "Sub", Loc.T("picker.sub"), 13,
                new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleLeft);
            UIFactory.BandTop(sub.rectTransform, 52f, 22f, 20f);

            // Four mini game tiles (runner / sentences / match / bubbles).
            Color[] tileCols = { Theme.Hex("378ADD"), Theme.Hex("5DCAA5"), Theme.Hex("AFA9EC"), Theme.Hex("ED93B1") };
            for (int i = 0; i < 4; i++)
            {
                var tile = UIFactory.CreatePanel(ct, "Tile" + i, new Color(1f, 1f, 1f, 0.16f), 12f);
                var trt = tile.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = new Vector2(46f, 46f);
                trt.anchoredPosition = new Vector2(-84f + i * 56f, -124f);
                tile.raycastTarget = false;
                var dot = UIFactory.CreateCircle(tile.transform, "Dot", tileCols[i]);
                var drt = dot.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(i == 1 ? 18f : 22f, i == 1 ? 18f : 22f);
                drt.anchoredPosition = Vector2.zero;
                dot.raycastTarget = false;
            }

            var play = UIFactory.CreateButton(ct, "Choose", Loc.T("home.choose_game"), 20,
                Theme.Amber, Theme.WordChipDark,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowGamePicker(); });
            var playImg = play.GetComponent<Image>();
            UIFactory.ApplyRounded(playImg, 24f);
            UIFactory.BandTop(play.GetComponent<RectTransform>(), 166f, 54f, 16f);
            UIFactory.AddSoftShadow(play.gameObject, 4f, 0.25f);
            UIFactory.AddGlossHighlight(play.transform, 0.92f, 0.30f, 0.16f);
            var playLabel = play.GetComponentInChildren<Text>();
            playLabel.font = UIFactory.DisplayFont;
        }

        /// <summary>Slim always-available REVIEW strip under the two mode
        /// cards — badge shows the pool size, dims when there's nothing.</summary>
        void BuildReviewStrip(Transform t)
        {
            var strip = UIFactory.CreateButton(t, "ReviewStrip", "", 14,
                Color.white, Theme.Ink,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (ProgressManager.MissedCount > 0) _boot.StartRun(true);
                });
            _reviewCard = strip.GetComponent<Image>();
            UIFactory.ApplyRounded(_reviewCard, 16f);
            UIFactory.AddSoftShadow(strip.gameObject, 3f, 0.14f);
            // Left half — the TIP strip takes the right half.
            var srt0 = strip.GetComponent<RectTransform>();
            srt0.anchorMin = new Vector2(0f, 1f);
            srt0.anchorMax = new Vector2(0.5f, 1f);
            srt0.pivot = new Vector2(0.5f, 1f);
            srt0.offsetMin = new Vector2(24f, 0f);
            srt0.offsetMax = new Vector2(-5f, 0f);
            srt0.sizeDelta = new Vector2(srt0.sizeDelta.x, 52f);
            srt0.anchoredPosition = new Vector2(srt0.anchoredPosition.x, -642f);

            var bolt = UIFactory.CreatePolygon(strip.transform, "Bolt", Theme.Hex("D85A30"), new[] {
                new Vector2(1.8f, 8.8f), new Vector2(-5.3f, 0f), new Vector2(-0.9f, 0f),
                new Vector2(-1.8f, -8.8f), new Vector2(5.3f, 0.9f), new Vector2(0.9f, 0.9f) });
            var boltRt = bolt.rectTransform;
            boltRt.anchorMin = boltRt.anchorMax = new Vector2(0f, 0.5f);
            boltRt.pivot = new Vector2(0.5f, 0.5f);
            boltRt.sizeDelta = new Vector2(20f, 22f);
            boltRt.anchoredPosition = new Vector2(28f, 0f);
            bolt.raycastTarget = false;

            var label = UIFactory.CreateText(strip.transform, "Label", Loc.T("home.review"), 16,
                Theme.Hex("4A1B0C"), TextAnchor.MiddleLeft);
            label.font = UIFactory.DisplayFont;
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.offsetMin = new Vector2(50f, 0f);
            lrt.offsetMax = new Vector2(-40f, 0f);

            var bubble = UIFactory.CreateCircle(strip.transform, "ReviewBubble", Theme.HeartRed);
            _reviewBubbleBg = bubble;
            var burt = bubble.rectTransform;
            burt.anchorMin = burt.anchorMax = new Vector2(1f, 0.5f);
            burt.pivot = new Vector2(0.5f, 0.5f);
            burt.sizeDelta = new Vector2(28f, 28f);
            burt.anchoredPosition = new Vector2(-26f, 0f);
            bubble.raycastTarget = false;
            _reviewBubbleText = UIFactory.CreateText(bubble.transform, "Count", "0", 13, Color.white);
            _reviewBubbleText.raycastTarget = false;

            BuildTipStrip(t);
        }

        /// <summary>TIP strip (right half, next to REVIEW): a daily fact
        /// about the LEARNING language. Tap → popup with the full tip and a
        /// "next tip" button.</summary>
        void BuildTipStrip(Transform t)
        {
            var strip = UIFactory.CreateButton(t, "TipStrip", "", 14,
                Color.white, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowTips(); });
            var img = strip.GetComponent<Image>();
            UIFactory.ApplyRounded(img, 16f);
            UIFactory.AddSoftShadow(strip.gameObject, 3f, 0.14f);
            var rt = strip.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(5f, 0f);
            rt.offsetMax = new Vector2(-24f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 52f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -642f);

            // Lightbulb-ish glyph: amber circle + tiny base.
            var bulb = UIFactory.CreateCircle(strip.transform, "Bulb", Theme.Amber);
            var brt = bulb.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(18f, 18f);
            brt.anchoredPosition = new Vector2(28f, 3f);
            bulb.raycastTarget = false;
            var bulbBase = UIFactory.CreatePanel(strip.transform, "BulbBase", Theme.AmberDeep, 1.5f);
            var bbrt = bulbBase.rectTransform;
            bbrt.anchorMin = bbrt.anchorMax = new Vector2(0f, 0.5f);
            bbrt.pivot = new Vector2(0.5f, 0.5f);
            bbrt.sizeDelta = new Vector2(10f, 4f);
            bbrt.anchoredPosition = new Vector2(28f, -9f);
            bulbBase.raycastTarget = false;

            var label = UIFactory.CreateText(strip.transform, "Label", Loc.T("home.tip"), 16,
                Theme.Hex("633806"), TextAnchor.MiddleLeft);
            label.font = UIFactory.DisplayFont;
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.offsetMin = new Vector2(50f, 0f);
            lrt.offsetMax = new Vector2(-10f, 0f);

        }


        /// <summary>iPhone-style floating tab bar (matches the v2 mockup):
        /// one white rounded pill with margins, five icon-only tabs evenly
        /// spaced — flat gray glyphs, HOME as a filled purple circle in the
        /// middle. No text labels, no elevated pop-out button. The SHOP tab
        /// carries a small dot tinted with the equipped ball color.</summary>
        void BuildTabBar(Transform t)
        {
            var bar = UIFactory.CreatePanel(t, "TabBar", Color.white, 22f);
            UIFactory.BandBottom(bar.rectTransform, 12f, 68f, 16f);
            UIFactory.AddSoftShadow(bar.gameObject, 4f, 0.14f);

            string[] tabs = { "SHOP", "DICTIONARY", "HOME", "AWARDS", "PLAYER" };
            Color glyphGray = Theme.Hex("888780");

            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                bool isHome = i == 2;

                var tabBtnGo = new GameObject("Tab" + tabs[i], typeof(RectTransform), typeof(Image), typeof(Button));
                tabBtnGo.transform.SetParent(bar.transform, false);
                var tabImg = tabBtnGo.GetComponent<Image>();
                tabImg.color = new Color(1f, 1f, 1f, 0f); // invisible hit area
                tabImg.raycastTarget = true;
                var trt = tabBtnGo.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(i / 5f, 0f);
                trt.anchorMax = new Vector2((i + 1) / 5f, 1f);
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                tabBtnGo.GetComponent<Button>().onClick.AddListener(delegate
                {
                    ProceduralAudio.PlayClick();
                    if (idx == 0) _boot.ShowBall();
                    else if (idx == 1) _boot.ShowDictionary();
                    else if (idx == 3) _boot.ShowAchievements();
                    else if (idx == 4) _boot.ShowSettings(); // account + pace + sound live here now
                });

                if (isHome)
                {
                    // Active tab: filled purple circle with a white house glyph.
                    var homeCircle = UIFactory.CreateCircle(tabBtnGo.transform, "HomeCircle", Theme.Purple);
                    var hcrt = homeCircle.rectTransform;
                    hcrt.anchorMin = hcrt.anchorMax = new Vector2(0.5f, 0.5f);
                    hcrt.pivot = new Vector2(0.5f, 0.5f);
                    hcrt.sizeDelta = new Vector2(48f, 48f);
                    hcrt.anchoredPosition = Vector2.zero;
                    homeCircle.raycastTarget = false;
                    DrawIcon(homeCircle.transform, "home", Color.white, 0.95f);
                    continue;
                }

                // Casual Icon Pack icon, vertically centered (falls back to
                // the old flat gray glyph if the sprite isn't imported).
                string icon = i == 0 ? "shop" : i == 1 ? "chat" : i == 3 ? "trophy" : "cog";
                RectTransform ghrt;
                if (UIFactory.IconSprite(icon) != null)
                {
                    ghrt = UIFactory.CreateIcon(tabBtnGo.transform, "Icon", icon, glyphGray)
                        .rectTransform;
                }
                else
                {
                    var glyphHost = new GameObject("Glyph", typeof(RectTransform));
                    glyphHost.transform.SetParent(tabBtnGo.transform, false);
                    ghrt = glyphHost.GetComponent<RectTransform>();
                    string glyph = i == 0 ? "cart" : i == 1 ? "book" : i == 3 ? "trophy" : "person";
                    DrawIcon(glyphHost.transform, glyph, glyphGray, 1.05f);
                }
                ghrt.anchorMin = ghrt.anchorMax = new Vector2(0.5f, 0.5f);
                ghrt.pivot = new Vector2(0.5f, 0.5f);
                ghrt.sizeDelta = new Vector2(34f, 34f);
                ghrt.anchoredPosition = Vector2.zero;

                if (i == 0)
                {
                    // Equipped-ball color dot on the SHOP tab.
                    var dot = UIFactory.CreateCircle(tabBtnGo.transform, "BallDot", Theme.BallSkins[0]);
                    var drt = dot.rectTransform;
                    drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
                    drt.pivot = new Vector2(0.5f, 0.5f);
                    drt.sizeDelta = new Vector2(13f, 13f);
                    drt.anchoredPosition = new Vector2(15f, 15f);
                    dot.raycastTarget = false;
                    _tabBallIcon = dot;
                }
            }
        }

        /// <summary>Small flat pictogram built from polygons/circles/panels.
        /// Every circle/panel piece re-centers its anchors before sizing
        /// (those helpers default to full-parent stretch).</summary>
        static void DrawIcon(Transform host, string kind, Color fg, float scale)
        {
            switch (kind)
            {
                case "book":
                {
                    var l = UIFactory.CreatePolygon(host, "IconBookL", fg, new[] {
                        new Vector2(-1f, 8f) * scale, new Vector2(0f, 6f) * scale,
                        new Vector2(0f, -8f) * scale, new Vector2(-9f, -6f) * scale,
                        new Vector2(-9f, 6f) * scale });
                    l.rectTransform.sizeDelta = new Vector2(20f, 20f) * scale;
                    l.raycastTarget = false;
                    var r = UIFactory.CreatePolygon(host, "IconBookR", fg, new[] {
                        new Vector2(1f, 8f) * scale, new Vector2(0f, 6f) * scale,
                        new Vector2(0f, -8f) * scale, new Vector2(9f, -6f) * scale,
                        new Vector2(9f, 6f) * scale });
                    r.rectTransform.sizeDelta = new Vector2(20f, 20f) * scale;
                    r.raycastTarget = false;
                    break;
                }
                case "trophy":
                {
                    var cup = UIFactory.CreatePolygon(host, "IconCup", fg, new[] {
                        new Vector2(-7f, 8f) * scale, new Vector2(7f, 8f) * scale,
                        new Vector2(4f, -2f) * scale, new Vector2(-4f, -2f) * scale });
                    cup.rectTransform.anchorMin = cup.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    cup.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    cup.rectTransform.sizeDelta = new Vector2(18f, 18f) * scale;
                    cup.rectTransform.anchoredPosition = new Vector2(0f, 3f) * scale;
                    cup.raycastTarget = false;
                    var stem = UIFactory.CreatePanel(host, "IconStem", fg, 0f);
                    stem.rectTransform.anchorMin = stem.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    stem.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    stem.rectTransform.sizeDelta = new Vector2(3f, 6f) * scale;
                    stem.rectTransform.anchoredPosition = new Vector2(0f, -4f) * scale;
                    stem.raycastTarget = false;
                    var baseBar = UIFactory.CreatePanel(host, "IconBase", fg, 1f);
                    baseBar.rectTransform.anchorMin = baseBar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    baseBar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    baseBar.rectTransform.sizeDelta = new Vector2(12f, 3f) * scale;
                    baseBar.rectTransform.anchoredPosition = new Vector2(0f, -8f) * scale;
                    baseBar.raycastTarget = false;
                    break;
                }
                case "home":
                {
                    var roof = UIFactory.CreatePolygon(host, "IconRoof", fg, new[] {
                        new Vector2(0f, 9f) * scale, new Vector2(10f, 0f) * scale,
                        new Vector2(-10f, 0f) * scale });
                    roof.rectTransform.sizeDelta = new Vector2(22f, 20f) * scale;
                    roof.raycastTarget = false;
                    var body = UIFactory.CreatePanel(host, "IconBody", fg, 2f);
                    body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    body.rectTransform.sizeDelta = new Vector2(13f, 9f) * scale;
                    body.rectTransform.anchoredPosition = new Vector2(0f, -4.5f) * scale;
                    body.raycastTarget = false;
                    break;
                }
                case "cart":
                {
                    var basket = UIFactory.CreatePolygon(host, "IconBasket", fg, new[] {
                        new Vector2(-8f, 5f) * scale, new Vector2(8f, 5f) * scale,
                        new Vector2(6f, -5f) * scale, new Vector2(-6f, -5f) * scale });
                    basket.rectTransform.anchorMin = basket.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    basket.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    basket.rectTransform.sizeDelta = new Vector2(18f, 14f) * scale;
                    basket.rectTransform.anchoredPosition = new Vector2(0f, 2f) * scale;
                    basket.raycastTarget = false;
                    var wheelL = UIFactory.CreateCircle(host, "WheelL", fg);
                    wheelL.rectTransform.anchorMin = wheelL.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    wheelL.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    wheelL.rectTransform.sizeDelta = new Vector2(4f, 4f) * scale;
                    wheelL.rectTransform.anchoredPosition = new Vector2(-4f, -8f) * scale;
                    wheelL.raycastTarget = false;
                    var wheelR = UIFactory.CreateCircle(host, "WheelR", fg);
                    wheelR.rectTransform.anchorMin = wheelR.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    wheelR.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    wheelR.rectTransform.sizeDelta = new Vector2(4f, 4f) * scale;
                    wheelR.rectTransform.anchoredPosition = new Vector2(4f, -8f) * scale;
                    wheelR.raycastTarget = false;
                    break;
                }
                case "person":
                {
                    var head = UIFactory.CreateCircle(host, "IconHead", fg);
                    head.rectTransform.anchorMin = head.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    head.rectTransform.sizeDelta = new Vector2(11f, 11f) * scale;
                    head.rectTransform.anchoredPosition = new Vector2(0f, 6f) * scale;
                    head.raycastTarget = false;
                    var body = UIFactory.CreatePolygon(host, "IconBody", fg, new[] {
                        new Vector2(-9f, -3f) * scale, new Vector2(9f, -3f) * scale,
                        new Vector2(6f, -10f) * scale, new Vector2(-6f, -10f) * scale });
                    body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    body.rectTransform.sizeDelta = new Vector2(20f, 14f) * scale;
                    body.rectTransform.anchoredPosition = new Vector2(0f, -6f) * scale;
                    body.raycastTarget = false;
                    break;
                }
            }
        }

        // ==================== refresh ====================

        static string ModeShort(GameMode m)
        {
            switch (m)
            {
                case GameMode.Sentences: return "S";
                case GameMode.Match: return "M";
                case GameMode.Bubbles: return "B";
                case GameMode.Blaster: return "X";
                default: return "W";
            }
        }

        static string ModeName(GameMode m)
        {
            switch (m)
            {
                case GameMode.Sentences: return Loc.T("mode.sentences");
                case GameMode.Match: return Loc.T("mode.match");
                case GameMode.Bubbles: return Loc.T("mode.bubbles");
                case GameMode.Blaster: return "BLASTER";
                default: return Loc.T("mode.words");
            }
        }

        public void Refresh()
        {
            string player = ProgressManager.ActiveProfile;
            _avatarLetter.text = string.IsNullOrEmpty(player) ? "?" : player.Substring(0, 1).ToUpperInvariant();
            _nameText.text = string.IsNullOrEmpty(player) ? Loc.T("home.guest") : player.ToUpperInvariant();

            // Hearts pill: count, plus a regen countdown when not full.
            int hearts = HeartsManager.Hearts;
            if (hearts >= HeartsManager.MaxHearts)
            {
                _heartsPillText.text = hearts.ToString();
            }
            else
            {
                int secs = HeartsManager.SecondsToNextHeart;
                _heartsPillText.text = hearts + " · " + (secs / 60) + ":" + (secs % 60).ToString("00");
            }
            _coinsPillText.text = ProgressManager.Coins.ToString();

            // Today row.
            _streakText.text = Loc.F("home.day", ProgressManager.DayStreakDisplay);
            int today = ProgressManager.TodayCatches;
            bool goalDone = today >= GameConfig.DailyGoal;
            float goalFrac = Mathf.Clamp01((float)today / GameConfig.DailyGoal);
            _goalFill.anchorMax = new Vector2(Mathf.Max(0.02f, goalFrac), 1f);
            _goalFill.offsetMin = Vector2.zero;
            _goalFill.offsetMax = Vector2.zero;
            _goalCountText.text = goalDone ? Loc.F("home.goal_done", GameConfig.DailyGoalCoins) : today + "/" + GameConfig.DailyGoal;
            _goalCountText.color = goalDone ? Theme.Teal : Theme.InkSoft;
            _goalStar.color = goalDone ? Color.white : new Color(1f, 1f, 1f, 0.45f);

            // Once per day: the goal completing deserves a celebration —
            // Lino bursts in (bypassing his frequency gate) to cheer.
            if (goalDone)
            {
                string todayStamp = System.DateTime.Today.ToString("yyyyMMdd");
                if (PlayerPrefs.GetString("LingoRun.GoalCelebrated", "") != todayStamp)
                {
                    PlayerPrefs.SetString("LingoRun.GoalCelebrated", todayStamp);
                    PlayerPrefs.Save();
                    if (_boot.Mascot != null) _boot.Mascot.CelebrateGoal();
                }
            }

            // Unit hero card.
            int level = ProgressManager.CurrentLevel;
            var def = GameConfig.GetMapLevel(level);
            int unitNumber = (level - 1) / GameConfig.LevelsPerUnit + 1;
            int li = (level - 1) % GameConfig.LevelsPerUnit;
            int firstLevel = (unitNumber - 1) * GameConfig.LevelsPerUnit + 1;

            Color cardColor;
            if (!TopicColors.TryGetValue(def.topic, out cardColor)) cardColor = Theme.Purple;
            _unitCard.color = cardColor;

            _unitLabel.text = Loc.F("home.unit", unitNumber, Loc.Topic(def.topic));
            _levelNumText.text = Loc.F("home.level", level);
            _levelSubText.text = ModeName(def.mode) + "  ·  " + Loc.F("pace.pace", Loc.Pace(def.pace));

            // Slot states: done / current / upcoming, letters = the unit's modes.
            for (int i = 0; i < GameConfig.LevelsPerUnit; i++)
            {
                var slotDef = GameConfig.GetMapLevel(firstLevel + i);
                bool done = i < li;
                bool current = i == li;
                _slotLabels[i].text = ModeShort(slotDef.mode);
                _slotLabels[i].gameObject.SetActive(!done);
                if (_slotChecks[i] != null) _slotChecks[i].SetActive(done);
                _slotBgs[i].color = done ? Theme.Teal
                    : current ? Theme.Amber
                    : new Color(1f, 1f, 1f, 0.22f);
                _slotLabels[i].color = current ? Theme.WordChipDark
                    : new Color(1f, 1f, 1f, 0.85f);
                _slotBgs[i].rectTransform.localScale = Vector3.one * (current ? 1.2f : 1f);
            }
            // Practice cards: review dims when the pool is empty, badge counts.
            int missed = ProgressManager.MissedCount;
            bool hasReview = missed > 0;
            _reviewCard.color = hasReview ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            _reviewBubbleBg.gameObject.SetActive(hasReview);
            _reviewBubbleText.text = missed > 99 ? "99" : missed.ToString();

            // Equipped ball color on the SHOP tab swatch.
            int selColor = Mathf.Clamp(ProgressManager.SelectedSkin, 0, Theme.BallSkins.Length - 1);
            if (_tabBallIcon != null) _tabBallIcon.color = Theme.BallSkins[selColor];
        }
    }
}
