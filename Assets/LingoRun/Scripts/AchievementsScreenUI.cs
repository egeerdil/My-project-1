using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Achievements screen: lifetime stats up top ("how far you've gotten"),
    /// then a scrollable list of achievements with locked/unlocked states.
    /// </summary>
    public class AchievementsScreenUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;
        Text _playerLabel;
        Text _countLabel;
        readonly Text[] _statValues = new Text[6];
        RectTransform _listContent;
        ScrollRect _scroll;

        static string[] StatNames()
        {
            return new[] { Loc.T("aw.runs"), Loc.T("aw.best"), Loc.T("dict.mastered"),
                Loc.T("aw.streak"), Loc.T("aw.catches"), Loc.T("aw.review") };
        }

        public AchievementsScreenUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "AchievementsScreen");
            Build();
            SetVisible(false);
        }

        public void SetVisible(bool visible) { UIFX.SetVisible(_root, visible); }

        void Build()
        {
            var t = _root.transform;

            // Slight dim so the meadow stays in the background.
            var dim = UIFactory.CreatePanel(t, "Dim", new Color(0.98f, 0.97f, 1f, 0.65f), 0f);
            dim.raycastTarget = false;

            var back = UIFactory.CreateButton(t, "Back", Loc.T("common.back"), 13,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowHome(); });
            UIFactory.PlaceTop(back.GetComponent<RectTransform>(), 70f, 34f, 100f, 36f);

            // Game Center: Apple's leaderboard sheet (friends tab included).
            var ranks = UIFactory.CreateButton(t, "Ranks", Loc.T("aw.ranks"), 13,
                Theme.Teal, Color.white,
                delegate { ProceduralAudio.PlayClick(); GameCenterManager.ShowLeaderboards(); });
            UIFactory.PlaceTop(ranks.GetComponent<RectTransform>(), 372f, 34f, 96f, 36f);

            var title = UIFactory.CreateText(t, "Title", Loc.T("aw.title"), 28, Theme.Orange);
            UIFactory.BandTop(title.rectTransform, 62f, 40f);

            _playerLabel = UIFactory.CreateText(t, "Player", "", 14, Theme.Teal);
            UIFactory.BandTop(_playerLabel.rectTransform, 104f, 20f);

            // ----- stats grid: 2 rows x 3 columns -----
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3;
                int row = i / 3;
                var cell = UIFactory.CreatePanel(t, "StatCell" + i, Theme.CreamPanel);
                var rt = cell.rectTransform;
                rt.anchorMin = new Vector2(col / 3f, 1f);
                rt.anchorMax = new Vector2((col + 1) / 3f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(col == 0 ? 16f : 5f, 0f);
                rt.offsetMax = new Vector2(col == 2 ? -16f : -5f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 62f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(134f + row * 70f));

                var val = UIFactory.CreateText(cell.transform, "Value", "0", 24, Theme.Teal);
                UIFactory.BandTop(val.rectTransform, 6f, 32f);
                _statValues[i] = val;

                var label = UIFactory.CreateText(cell.transform, "Label", StatNames()[i], 11, Theme.InkSoft);
                UIFactory.BandTop(label.rectTransform, 40f, 16f);
            }

            _countLabel = UIFactory.CreateText(t, "Count", "", 13, Theme.InkSoft, TextAnchor.MiddleLeft);
            UIFactory.BandTop(_countLabel.rectTransform, 286f, 18f, 20f);

            // ----- scrollable achievements list -----
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            scrollGo.transform.SetParent(t, false);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(1f, 1f, 1f, 0.01f); // invisible but draggable
            scrollImg.raycastTarget = true;
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(16f, 18f);     // bottom margin
            scrollRt.offsetMax = new Vector2(-16f, -310f);  // below the stats grid

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
            string name = ProgressManager.ActiveProfile;
            _playerLabel.text = string.IsNullOrEmpty(name) ? "" : Loc.F("settings.player", name.ToUpperInvariant());

            _statValues[0].text = ProgressManager.TotalRuns.ToString();
            _statValues[1].text = ProgressManager.HighScore.ToString();
            _statValues[2].text = ProgressManager.MasteredA1Count.ToString();
            _statValues[3].text = ProgressManager.LifetimeBestStreak.ToString();
            _statValues[4].text = ProgressManager.TotalCorrect.ToString();
            _statValues[5].text = ProgressManager.MissedCount.ToString();

            int unlocked = 0;
            foreach (var def in Achievements.All)
                if (ProgressManager.UnlockedAchievements.Contains(def.id)) unlocked++;
            _countLabel.text = Loc.F("aw.unlocked", unlocked, Achievements.All.Count);

            // Rebuild rows.
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);

            const float rowH = 64f;
            const float gap = 8f;
            float y = 0f;
            foreach (var def in Achievements.All)
            {
                bool got = ProgressManager.UnlockedAchievements.Contains(def.id);

                var row = UIFactory.CreatePanel(_listContent.transform, "Row_" + def.id,
                    got ? new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.16f)
                        : new Color(1f, 1f, 1f, 0.55f));
                var rt = row.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(0f, 0f);
                rt.offsetMax = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowH);
                rt.anchoredPosition = new Vector2(0f, -y);

                var titleText = UIFactory.CreateText(row.transform, "Title", def.title, 17,
                    got ? Theme.Teal : new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.55f),
                    TextAnchor.MiddleLeft);
                UIFactory.BandTop(titleText.rectTransform, 8f, 24f, 14f);

                var descText = UIFactory.CreateText(row.transform, "Desc", def.desc, 13,
                    new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, got ? 0.7f : 0.45f),
                    TextAnchor.MiddleLeft, FontStyle.Normal);
                UIFactory.BandTop(descText.rectTransform, 34f, 22f, 14f);

var badge = UIFactory.CreateText(row.transform, "Badge", got ? Loc.T("aw.done") : Loc.T("aw.locked"), 12,
                    got ? Theme.Teal : new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.35f),
                    TextAnchor.MiddleRight);
                UIFactory.BandTop(badge.rectTransform, 8f, 40f, 14f);

                y += rowH + gap;
            }
            _listContent.sizeDelta = new Vector2(_listContent.sizeDelta.x, Mathf.Max(1f, y));
            _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
