using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Tips page: ALL the crucial facts about the profile's learning
    /// language on one scrollable screen (the Home TIP strip opens this).
    /// Each tip is a numbered card with the amber bulb accent; today's tip
    /// (the one the strip rotates through) is highlighted.
    /// </summary>
    public class TipsScreenUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;
        Text _title;
        RectTransform _listContent;
        ScrollRect _scroll;

        public TipsScreenUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "TipsScreen");
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

            _title = UIFactory.CreateText(t, "Title", "", 26, Theme.AmberDeep);
            _title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_title.rectTransform, 60f, 38f);

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
            scrollRt.offsetMax = new Vector2(-16f, -110f);

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

        public void Refresh()
        {
            string code = LanguageManager.Current;
            _title.text = Loc.T("home.tip") + "  ·  " + Loc.LangName(code);

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);

            string[] tips = LanguageTips.AllFor(code);
            string today = LanguageTips.TipFor(code);
            const float rowH = 92f;
            const float gap = 10f;
            float y = 0f;

            for (int i = 0; i < tips.Length; i++)
            {
                bool isToday = tips[i] == today;
                var card = UIFactory.CreatePanel(_listContent.transform, "Tip" + i,
                    isToday ? new Color(Theme.Amber.r, Theme.Amber.g, Theme.Amber.b, 0.28f)
                            : new Color(1f, 1f, 1f, 0.85f), 16f);
                var rt = card.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowH);
                rt.anchoredPosition = new Vector2(0f, -y);

                // Number badge with the bulb color.
                var badge = UIFactory.CreateCircle(card.transform, "Num", Theme.Amber);
                var brt = badge.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(30f, 30f);
                brt.anchoredPosition = new Vector2(28f, 0f);
                var num = UIFactory.CreateText(badge.transform, "N", (i + 1).ToString(), 14,
                    Theme.Hex("633806"));
                num.font = UIFactory.DisplayFont;
                num.raycastTarget = false;

                var text = UIFactory.CreateText(card.transform, "Text", tips[i], 15,
                    Theme.Ink, TextAnchor.MiddleLeft);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                var trt = text.rectTransform;
                trt.offsetMin = new Vector2(52f, 8f);
                trt.offsetMax = new Vector2(-14f, -8f);

                y += rowH + gap;
            }

            _listContent.sizeDelta = new Vector2(_listContent.sizeDelta.x, Mathf.Max(1f, y));
            _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
