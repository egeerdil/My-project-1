using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Free Play — the player picks WHICH game to play, at WHICH pace,
    /// outside the map's structured curriculum. Five cards (Word runner,
    /// Sentence runner, Memory match, Bubble spell, Speak gates), each with
    /// a color-coded icon and a one-line hook. Free play never costs hearts
    /// and never advances the map; coins, mastery and the review pool still
    /// count, so it's the zero-pressure sandbox next to the map's
    /// hearts-and-chests progression.
    /// </summary>
    public class GamePickerUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;
        readonly Image[] _paceSegs = new Image[3];
        readonly Text[] _paceSegLabels = new Text[3];
        GameObject _bubbleCard;

        public GamePickerUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "GamePickerScreen");
            Build();
            SetVisible(false);
        }

        public void SetVisible(bool visible) { UIFX.SetVisible(_root, visible); }

        void Build()
        {
            var t = _root.transform;

            var dim = UIFactory.CreatePanel(t, "Dim", new Color(0.98f, 0.97f, 1f, 0.65f), 0f);
            dim.raycastTarget = false;

            var back = UIFactory.CreateButton(t, "Back", Loc.T("common.back"), 14,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowHome(); });
            UIFactory.PlaceTop(back.GetComponent<RectTransform>(), 70f, 34f, 100f, 36f);

            var title = UIFactory.CreateText(t, "Title", Loc.T("picker.title"), 28, Theme.Orange);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 60f, 40f);

            var sub = UIFactory.CreateText(t, "Sub", Loc.T("picker.sub"), 14, Theme.InkSoft);
            UIFactory.BandTop(sub.rectTransform, 100f, 22f);

            // Pace selector (this is where the free pace choice lives now).
            string[] paces = { Loc.T("pace.calm"), Loc.T("pace.normal"), Loc.T("pace.fast") };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = UIFactory.CreateButton(t, "Pace" + paces[i], paces[i], 14,
                    Theme.CreamPanel, Theme.Ink,
                    delegate { ProceduralAudio.PlayClick(); _boot.SetDifficulty((Difficulty)idx); Refresh(); });
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(i / 3f, 1f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(i == 0 ? 24f : 4f, 0f);
                rt.offsetMax = new Vector2(i == 2 ? -24f : -4f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 40f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -132f);
                _paceSegs[i] = btn.GetComponent<Image>();
                _paceSegLabels[i] = btn.GetComponentInChildren<Text>();
            }

            // Five game cards (Bubble Spell hides for non-alphabet scripts —
            // see Refresh / GameConfig.SupportsSpelling).
            float y = 190f;
            y = GameCard(t, y, Loc.T("picker.word_runner"), Loc.T("picker.word_desc"),
                Theme.Hex("185FA5"), Theme.Hex("E6F1FB"), "ball",
                delegate { _boot.StartFreePlay(GameMode.Words); });
            y = GameCard(t, y, Loc.T("picker.sentence_runner"), Loc.T("picker.sentence_desc"),
                Theme.Hex("0F6E56"), Theme.Hex("E1F5EE"), "lines",
                delegate { _boot.StartFreePlay(GameMode.Sentences); });
            y = GameCard(t, y, Loc.T("mode.match"), Loc.T("picker.match_desc"),
                Theme.Hex("534AB7"), Theme.Hex("EEEDFE"), "cards",
                delegate { _boot.StartFreePlay(GameMode.Match); });
            y = GameCard(t, y, Loc.T("picker.bubble"), Loc.T("picker.bubble_desc"),
                Theme.Hex("993556"), Theme.Hex("FBEAF0"), "bubbles",
                delegate { _boot.StartFreePlay(GameMode.Bubbles); });
            y = GameCard(t, y, Loc.T("mode.speak"), Loc.T("picker.speak_desc"),
                Theme.Hex("3B6D11"), Theme.Hex("EAF3DE"), "mic",
                delegate { _boot.StartSpeech(false); });

            var note = UIFactory.CreateText(t, "Note",
                Loc.T("picker.note"),
                12, Theme.InkSoft);
            UIFactory.BandTop(note.rectTransform, y + 6f, 40f, 24f);
        }

        float GameCard(Transform t, float y, string name, string desc,
            Color accent, Color iconBg, string icon, UnityEngine.Events.UnityAction onClick)
        {
            const float cardH = 82f;
            var card = UIFactory.CreateButton(t, "Card_" + name, "", 14,
                Color.white, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); onClick(); });
            if (name == Loc.T("picker.bubble")) _bubbleCard = card.gameObject;
            var img = card.GetComponent<Image>();
            UIFactory.ApplyRounded(img, 18f);
            UIFactory.AddSoftShadow(card.gameObject, 3f, 0.14f);
            UIFactory.BandTop(card.GetComponent<RectTransform>(), y, cardH, 24f);
            var ct = card.transform;

            // Icon tile.
            var tile = UIFactory.CreatePanel(ct, "Tile", iconBg, 14f);
            var trt = tile.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(50f, 50f);
            trt.anchoredPosition = new Vector2(42f, 0f);
            tile.raycastTarget = false;
            DrawGameIcon(tile.transform, icon, accent);

            var nameText = UIFactory.CreateText(ct, "Name", name, 16, accent, TextAnchor.MiddleLeft);
            nameText.font = UIFactory.DisplayFont;
            nameText.raycastTarget = false;
            UIFactory.BandTop(nameText.rectTransform, 16f, 24f, 80f);

            var descText = UIFactory.CreateText(ct, "Desc", desc, 12, Theme.InkSoft, TextAnchor.MiddleLeft);
            descText.raycastTarget = false;
            UIFactory.BandTop(descText.rectTransform, 44f, 20f, 80f);

            // Play chevron.
            var playDot = UIFactory.CreateCircle(ct, "PlayDot", accent);
            var prt = playDot.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(34f, 34f);
            prt.anchoredPosition = new Vector2(-34f, 0f);
            playDot.raycastTarget = false;
            var arrow = UIFactory.CreatePolygon(playDot.transform, "Arrow", Color.white, new[] {
                new Vector2(-4f, 7f), new Vector2(7f, 0f), new Vector2(-4f, -7f) });
            arrow.raycastTarget = false;

            return y + cardH + 12f;
        }

        /// <summary>Tiny per-game pictograms from primitives.</summary>
        static void DrawGameIcon(Transform host, string kind, Color fg)
        {
            switch (kind)
            {
                case "ball":
                {
                    var b = UIFactory.CreateCircle(host, "B", fg);
                    Center(b.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 24f));
                    var cap = UIFactory.CreateCircle(b.transform, "Cap", Color.white);
                    Center(cap.rectTransform, new Vector2(4f, 5f), new Vector2(8f, 8f));
                    break;
                }
                case "lines":
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var line = UIFactory.CreatePanel(host, "L" + i, fg, 2f);
                        Center(line.rectTransform, new Vector2(i == 1 ? 3f : 0f, 8f - i * 8f),
                            new Vector2(i == 1 ? 18f : 26f, 4.5f));
                    }
                    break;
                }
                case "cards":
                {
                    var back = UIFactory.CreatePanel(host, "CardB", new Color(fg.r, fg.g, fg.b, 0.45f), 4f);
                    Center(back.rectTransform, new Vector2(-5f, 3f), new Vector2(16f, 21f));
                    back.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 10f);
                    var front = UIFactory.CreatePanel(host, "CardF", fg, 4f);
                    Center(front.rectTransform, new Vector2(5f, -2f), new Vector2(16f, 21f));
                    front.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                    break;
                }
                case "bubbles":
                {
                    var b1 = UIFactory.CreateCircle(host, "B1", fg);
                    Center(b1.rectTransform, new Vector2(-6f, -4f), new Vector2(18f, 18f));
                    var b2 = UIFactory.CreateCircle(host, "B2", new Color(fg.r, fg.g, fg.b, 0.6f));
                    Center(b2.rectTransform, new Vector2(8f, 6f), new Vector2(12f, 12f));
                    var b3 = UIFactory.CreateCircle(host, "B3", new Color(fg.r, fg.g, fg.b, 0.35f));
                    Center(b3.rectTransform, new Vector2(2f, 12f), new Vector2(7f, 7f));
                    break;
                }
                case "mic":
                {
                    var body = UIFactory.CreatePanel(host, "Mic", fg, 6f);
                    Center(body.rectTransform, new Vector2(0f, 4f), new Vector2(12f, 18f));
                    var baseBar = UIFactory.CreatePanel(host, "Base", fg, 2f);
                    Center(baseBar.rectTransform, new Vector2(0f, -9f), new Vector2(17f, 3.5f));
                    var stem = UIFactory.CreatePanel(host, "Stem", fg, 1f);
                    Center(stem.rectTransform, new Vector2(0f, -6f), new Vector2(3f, 5f));
                    break;
                }
            }
        }

        static void Center(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var g = rt.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = false;
        }

        public void Refresh()
        {
            // Letter-spelling doesn't exist for ja/ko/zh — hide Bubble Spell.
            if (_bubbleCard != null)
                _bubbleCard.SetActive(GameConfig.SupportsSpelling(LanguageManager.Current));

            for (int i = 0; i < 3; i++)
            {
                bool active = (int)_boot.CurrentDifficulty == i;
                _paceSegs[i].color = active
                    ? new Color(Theme.AmberDeep.r, Theme.AmberDeep.g, Theme.AmberDeep.b, 0.25f)
                    : Theme.CreamPanel;
                _paceSegLabels[i].color = active ? Theme.AmberDeep : Theme.Ink;
            }
        }
    }
}
