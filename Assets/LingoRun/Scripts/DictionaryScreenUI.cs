using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Words screen: every word the player has encountered, with its mastery
    /// state (IN REVIEW / n of 3 / MASTERED). Shows word-by-word "how far
    /// you've gotten", complementing the totals on the achievements screen.
    /// </summary>
    public class DictionaryScreenUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;
        Text _chipWords, _chipMastered, _chipReview;
        RectTransform _listContent;
        ScrollRect _scroll;

        // Canonical topic display order — single source of truth lives in
        // VocabDatabase.TopicOrder (also used by the onboarding topic picker).
        // Any tag not in the list falls back to alphabetical at the end.
        static readonly string[] TopicOrder = VocabDatabase.TopicOrder;

        public DictionaryScreenUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "DictionaryScreen");
            Build();
            SetVisible(false);
        }

        public void SetVisible(bool visible) { UIFX.SetVisible(_root, visible); }

        void Build()
        {
            var t = _root.transform;

            var dim = UIFactory.CreatePanel(t, "Dim", new Color(0.98f, 0.97f, 1f, 0.65f), 0f);
            dim.raycastTarget = false;

            var back = UIFactory.CreateButton(t, "Back", Loc.T("common.back"), 13,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowHome(); });
            UIFactory.PlaceTop(back.GetComponent<RectTransform>(), 70f, 34f, 100f, 36f);

            var title = UIFactory.CreateText(t, "Title", Loc.T("dict.title"), 28, Theme.Orange);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 62f, 40f);

            // Teal summary hero card (same design language as Settings):
            // words / mastered / in-review chips + the tiny mastery legend.
            var hero = UIFactory.CreatePanel(t, "HeroCard", Theme.Teal, 24f);
            UIFactory.AddSoftShadow(hero.gameObject, 5f, 0.22f);
            UIFactory.BandTop(hero.rectTransform, 104f, 104f, 20f);

            _chipWords = MakeChip(hero.transform, 0, "chat");
            _chipMastered = MakeChip(hero.transform, 1, "star");
            _chipReview = MakeChip(hero.transform, 2, "heart");

            var legend = UIFactory.CreateText(hero.transform, "Legend",
                Loc.F("dict.legend", GameConfig.MasteryThreshold), 10,
                new Color(1f, 1f, 1f, 0.78f));
            UIFactory.BandTop(legend.rectTransform, 64f, 32f, 14f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(t, false);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(1f, 1f, 1f, 0.01f);
            scrollImg.raycastTarget = true;
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(16f, 18f);
            scrollRt.offsetMax = new Vector2(-16f, -224f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _listContent = contentGo.GetComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.offsetMin = Vector2.zero;
            _listContent.offsetMax = Vector2.zero;

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.content = _listContent;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 22f;
        }

        static Text MakeChip(Transform card, int i, string icon)
        {
            var pill = UIFactory.CreatePanel(card, "Chip" + icon,
                new Color(1f, 1f, 1f, 0.16f), 14f);
            var rt = pill.rectTransform;
            rt.anchorMin = new Vector2(i / 3f, 1f);
            rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(i == 0 ? 14f : 4f, 0f);
            rt.offsetMax = new Vector2(i == 2 ? -14f : -4f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 36f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -16f);

            var ic = UIFactory.CreateIcon(pill.transform, "Icon", icon, Color.white);
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(20f, 20f);
            irt.anchoredPosition = new Vector2(20f, 0f);

            var txt = UIFactory.CreateText(pill.transform, "Val", "", 14, Color.white,
                TextAnchor.MiddleLeft);
            var trt = txt.rectTransform;
            trt.offsetMin = new Vector2(34f, 0f);
            trt.offsetMax = new Vector2(-4f, 0f);
            return txt;
        }

        class Row
        {
            public WordEntry word;
            public int count;
            public bool inReview;
            public bool mastered;
        }

        public void Refresh()
        {
            // Collect encountered words: anything with correct counts or in review
            // (sentence keys "s|..." are excluded — this screen is vocabulary).
            var keys = new HashSet<string>();
            foreach (var k in ProgressManager.CorrectCounts.Keys) keys.Add(k);
            foreach (var k in ProgressManager.MissedWords.Keys) keys.Add(k);

            var rows = new List<Row>();
            int mastered = 0, inReview = 0;
            foreach (var en in keys)
            {
                if (en.StartsWith(SentenceDatabase.KeyPrefix)) continue;
                var w = VocabDatabase.FindByEn(en);
                if (w == null) continue;
                int c;
                ProgressManager.CorrectCounts.TryGetValue(en, out c);
                var row = new Row
                {
                    word = w,
                    count = c,
                    inReview = ProgressManager.MissedWords.ContainsKey(en),
                    mastered = c >= GameConfig.MasteryThreshold,
                };
                if (row.mastered) mastered++;
                if (row.inReview) inReview++;
                rows.Add(row);
            }

            _chipWords.text = rows.Count.ToString();
            _chipMastered.text = mastered.ToString();
            _chipReview.text = inReview.ToString();

            // Sort: in-review first, then learning (by progress), then mastered; alphabetical inside.
            rows.Sort(delegate (Row a, Row b)
            {
                int ra = a.inReview ? 0 : (a.mastered ? 2 : 1);
                int rb = b.inReview ? 0 : (b.mastered ? 2 : 1);
                if (ra != rb) return ra.CompareTo(rb);
                return string.Compare(a.word.tgt, b.word.tgt, System.StringComparison.OrdinalIgnoreCase);
            });

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);

            const float rowH = 58f;
            const float gap = 6f;
            const float headerH = 30f;
            const float headerGapAbove = 14f;
            const float headerGapBelow = 4f;
            float y = 0f;

            if (rows.Count == 0)
            {
                var empty = UIFactory.CreateText(_listContent.transform, "Empty",
                    Loc.T("dict.empty"), 15, Theme.InkSoft);
                UIFactory.BandTop(empty.rectTransform, 30f, 30f);
                _listContent.sizeDelta = new Vector2(_listContent.sizeDelta.x, 90f);
                return;
            }

            // Group by the word's primary topic tag (t[0]) so the list reads
            // as "FOOD", "HOME", "WORK", etc. sections instead of one long
            // flat alphabetical dump — matches how the vocab data itself is
            // organized. Words with no tag land in a trailing "OTHER" group.
            var groups = new Dictionary<string, List<Row>>();
            foreach (var r in rows)
            {
                string topic = (r.word.t != null && r.word.t.Length > 0) ? r.word.t[0] : "other";
                List<Row> g;
                if (!groups.TryGetValue(topic, out g)) { g = new List<Row>(); groups[topic] = g; }
                g.Add(r);
            }

            var orderedTopics = new List<string>();
            foreach (var topic in TopicOrder)
                if (groups.ContainsKey(topic)) orderedTopics.Add(topic);
            var leftover = new List<string>();
            foreach (var topic in groups.Keys)
                if (!orderedTopics.Contains(topic)) leftover.Add(topic);
            leftover.Sort(System.StringComparer.OrdinalIgnoreCase);
            orderedTopics.AddRange(leftover);

            bool firstSection = true;
            foreach (var topic in orderedTopics)
            {
                var group = groups[topic];
                group.Sort(delegate (Row a, Row b)
                {
                    int ra = a.inReview ? 0 : (a.mastered ? 2 : 1);
                    int rb = b.inReview ? 0 : (b.mastered ? 2 : 1);
                    if (ra != rb) return ra.CompareTo(rb);
                    return string.Compare(a.word.tgt, b.word.tgt, System.StringComparison.OrdinalIgnoreCase);
                });

                if (!firstSection) y += headerGapAbove;
                firstSection = false;

                var headerGo = new GameObject("Header_" + topic, typeof(RectTransform));
                headerGo.transform.SetParent(_listContent.transform, false);
                var hrt = headerGo.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.offsetMin = Vector2.zero;
                hrt.offsetMax = Vector2.zero;
                hrt.sizeDelta = new Vector2(0f, headerH);
                hrt.anchoredPosition = new Vector2(0f, -y);

                var hIcon = UIFactory.CreateIcon(headerGo.transform, "Icon", "mission", Theme.Orange);
                var hirt = hIcon.rectTransform;
                hirt.anchorMin = hirt.anchorMax = new Vector2(0f, 0.5f);
                hirt.pivot = new Vector2(0.5f, 0.5f);
                hirt.sizeDelta = new Vector2(20f, 20f);
                hirt.anchoredPosition = new Vector2(14f, 0f);

                var header = UIFactory.CreateText(headerGo.transform, "Txt",
                    Loc.Topic(topic), 15, Theme.Orange, TextAnchor.MiddleLeft);
                header.font = UIFactory.DisplayFont;
                var htrt = header.rectTransform;
                htrt.offsetMin = new Vector2(30f, 0f);
                htrt.offsetMax = new Vector2(-60f, 0f);

                // Word-count badge on the right of the section header.
                var countBadge = UIFactory.CreatePanel(headerGo.transform, "Count",
                    new Color(Theme.Orange.r, Theme.Orange.g, Theme.Orange.b, 0.16f), 11f);
                var cbrt = countBadge.rectTransform;
                cbrt.anchorMin = cbrt.anchorMax = new Vector2(1f, 0.5f);
                cbrt.pivot = new Vector2(1f, 0.5f);
                cbrt.sizeDelta = new Vector2(44f, 22f);
                cbrt.anchoredPosition = new Vector2(-2f, 0f);
                var countTxt = UIFactory.CreateText(countBadge.transform, "Txt",
                    group.Count.ToString(), 12, Theme.Orange);
                countTxt.raycastTarget = false;

                y += headerH + headerGapBelow;

                foreach (var r in group)
                {
                    var rowPanel = UIFactory.CreatePanel(_listContent.transform, "Row",
                        r.mastered ? new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.10f)
                                   : new Color(1f, 1f, 1f, 0.8f), 14f);
                    var rt = rowPanel.rectTransform;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowH);
                    rt.anchoredPosition = new Vector2(0f, -y);

                    var tgt = UIFactory.CreateText(rowPanel.transform, "Tgt", r.word.tgt, 17, Theme.Ink,
                        TextAnchor.MiddleLeft);
                    UIFactory.BandTop(tgt.rectTransform, 6f, 24f, 14f);

                    var enText = UIFactory.CreateText(rowPanel.transform, "En", r.word.en, 13,
                        Theme.InkSoft, TextAnchor.MiddleLeft, FontStyle.Normal);
                    UIFactory.BandTop(enText.rectTransform, 31f, 20f, 14f);

                    if (r.inReview || r.mastered)
                    {
                        // Status icon: gold star (mastered) or heart (in review).
                        var sic = UIFactory.CreateIcon(rowPanel.transform, "Status",
                            r.mastered ? "star" : "heart",
                            r.mastered ? Theme.Teal : Theme.BadRed);
                        var sirt = sic.rectTransform;
                        sirt.anchorMin = sirt.anchorMax = new Vector2(1f, 0.5f);
                        sirt.pivot = new Vector2(0.5f, 0.5f);
                        sirt.sizeDelta = new Vector2(24f, 24f);
                        sirt.anchoredPosition = new Vector2(-28f, 0f);
                    }
                    else
                    {
                        // Learning: "n / 3" + a mini progress bar underneath.
                        var statusText = UIFactory.CreateText(rowPanel.transform, "Status",
                            r.count + " / " + GameConfig.MasteryThreshold, 12,
                            Theme.AmberDeep, TextAnchor.MiddleRight);
                        UIFactory.BandTop(statusText.rectTransform, 10f, 20f, 14f);

                        var barBg = UIFactory.CreatePanel(rowPanel.transform, "BarBg",
                            Theme.Hex("F1EFE8"), 3f);
                        var bbrt = barBg.rectTransform;
                        bbrt.anchorMin = bbrt.anchorMax = new Vector2(1f, 0f);
                        bbrt.pivot = new Vector2(1f, 0f);
                        bbrt.sizeDelta = new Vector2(46f, 5f);
                        bbrt.anchoredPosition = new Vector2(-14f, 14f);
                        var fill = UIFactory.CreatePanel(barBg.transform, "Fill",
                            Theme.AmberDeep, 3f);
                        var frt = fill.rectTransform;
                        frt.anchorMin = new Vector2(0f, 0f);
                        frt.anchorMax = new Vector2(
                            Mathf.Clamp01(r.count / (float)GameConfig.MasteryThreshold), 1f);
                        frt.offsetMin = Vector2.zero;
                        frt.offsetMax = Vector2.zero;
                    }

                    y += rowH + gap;
                }
            }
            _listContent.sizeDelta = new Vector2(_listContent.sizeDelta.x, Mathf.Max(1f, y));
            _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
