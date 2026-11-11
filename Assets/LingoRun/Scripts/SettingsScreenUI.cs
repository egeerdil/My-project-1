using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Settings page, v2. Layout:
    ///   - purple profile hero card: avatar initial, name, learning language,
    ///     stat chips (level / coins / gems / streak) and a SWITCH pill;
    ///   - LANGUAGE card: chat icon, learning language + CEFR badge + note;
    ///   - GAMEPLAY card: pace segmented control, an iOS-style sound switch,
    ///     and the app-language row (opens the full 11-language picker).
    /// Section labels sit above the cards, iOS-settings style.
    /// </summary>
    public class SettingsScreenUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;

        Text _avatarInitial;
        Text _nameLabel;
        Text _heroSub;
        Text _chipLevel, _chipCoins, _chipGems, _chipStreak;
        Text _langLabel;
        Text _langBadge;
        readonly Image[] _paceSegs = new Image[3];
        readonly Text[] _paceSegLabels = new Text[3];
        Image _soundTrack;
        RectTransform _soundKnob;
        Text _soundLabel;
        Text _appLangLabel;
        RectTransform _langOverlay;

        public SettingsScreenUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "SettingsScreen");
            Build();
            SetVisible(false);
        }

        public void SetVisible(bool visible) { UIFX.SetVisible(_root, visible); }

        void Build()
        {
            var t = _root.transform;

            var dim = UIFactory.CreatePanel(t, "Dim", new Color(0.98f, 0.97f, 1f, 0.65f), 0f);
            dim.raycastTarget = false;

            var back = UIFactory.CreateButton(t, "Back", Loc.T("common.back"), 15,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowHome(); });
            UIFactory.PlaceTop(back.GetComponent<RectTransform>(), 70f, 34f, 100f, 36f);

            var title = UIFactory.CreateText(t, "Title", Loc.T("settings.title"), 28, Theme.Ink);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 62f, 40f);

            BuildHeroCard(t);
            SectionLabel(t, Loc.T("settings.language"), 288f);
            BuildLanguageCard(t);
            SectionLabel(t, Loc.T("settings.gameplay"), 408f);
            BuildGameplayCard(t);
            BuildAppLangOverlay(t); // last → draws over everything

            var footer = UIFactory.CreateText(t, "Footer", "LingoRun", 12, Theme.InkSoft);
            UIFactory.BandTop(footer.rectTransform, 652f, 16f);
        }

        void SectionLabel(Transform t, string text, float y)
        {
            var label = UIFactory.CreateText(t, "Section", text, 13, Theme.InkSoft,
                TextAnchor.MiddleLeft);
            label.font = UIFactory.DisplayFont;
            UIFactory.BandTop(label.rectTransform, y, 18f, 30f);
        }

        // ---------- hero ----------

        void BuildHeroCard(Transform t)
        {
            var card = UIFactory.CreatePanel(t, "HeroCard", Theme.Purple, 24f);
            UIFactory.AddSoftShadow(card.gameObject, 5f, 0.22f);
            UIFactory.BandTop(card.rectTransform, 108f, 162f, 20f);
            var ct = card.transform;

            // Avatar circle with the player's initial.
            var avatar = UIFactory.CreateCircle(ct, "Avatar", Color.white);
            var art = avatar.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(58f, 58f);
            art.anchoredPosition = new Vector2(48f, -46f);
            _avatarInitial = UIFactory.CreateText(avatar.transform, "Initial", "?", 26, Theme.PurpleDeep);
            _avatarInitial.font = UIFactory.DisplayFont;
            _avatarInitial.raycastTarget = false;

            _nameLabel = UIFactory.CreateText(ct, "Name", "", 20, Color.white, TextAnchor.MiddleLeft);
            _nameLabel.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_nameLabel.rectTransform, 24f, 26f, 88f);

            _heroSub = UIFactory.CreateText(ct, "Sub", "", 13,
                new Color(1f, 1f, 1f, 0.78f), TextAnchor.MiddleLeft);
            UIFactory.BandTop(_heroSub.rectTransform, 52f, 18f, 88f);

            // SWITCH profile pill, top right.
            var sw = UIFactory.CreateButton(ct, "Switch", Loc.T("settings.switch"), 12,
                Color.white, Theme.PurpleDeep,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowLogin(); });
            var srt = sw.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.sizeDelta = new Vector2(96f, 30f);
            srt.anchoredPosition = new Vector2(-14f, -14f);

            // Stat chips: level / coins / gems / streak.
            _chipLevel = MakeChip(ct, 0, "trophy");
            _chipCoins = MakeChip(ct, 1, "coin");
            _chipGems = MakeChip(ct, 2, "gem");
            _chipStreak = MakeChip(ct, 3, "lightning");
        }

        Text MakeChip(Transform card, int i, string icon)
        {
            var pill = UIFactory.CreatePanel(card, "Chip" + icon,
                new Color(1f, 1f, 1f, 0.16f), 14f);
            var rt = pill.rectTransform;
            rt.anchorMin = new Vector2(i / 4f, 0f);
            rt.anchorMax = new Vector2((i + 1) / 4f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(i == 0 ? 14f : 4f, 0f);
            rt.offsetMax = new Vector2(i == 3 ? -14f : -4f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 34f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 14f);

            var ic = UIFactory.CreateIcon(pill.transform, "Icon", icon, Color.white);
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(18f, 18f);
            irt.anchoredPosition = new Vector2(16f, 0f);

            var txt = UIFactory.CreateText(pill.transform, "Val", "", 13, Color.white,
                TextAnchor.MiddleLeft);
            var trt = txt.rectTransform;
            trt.offsetMin = new Vector2(28f, 0f);
            trt.offsetMax = new Vector2(-4f, 0f);
            return txt;
        }

        // ---------- language ----------

        void BuildLanguageCard(Transform t)
        {
            var card = UIFactory.CreatePanel(t, "LanguageCard", Theme.CreamPanel, 20f);
            UIFactory.AddSoftShadow(card.gameObject, 3f, 0.14f);
            UIFactory.BandTop(card.rectTransform, 310f, 84f, 20f);
            var ct = card.transform;

            var ic = UIFactory.CreateIcon(ct, "Icon", "chat", Theme.Purple);
            var irt = ic.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(32f, 32f);
            irt.anchoredPosition = new Vector2(34f, 0f);

            _langLabel = UIFactory.CreateText(ct, "Lang", "", 17, Theme.Purple, TextAnchor.MiddleLeft);
            _langLabel.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_langLabel.rectTransform, 16f, 24f, 62f);

            var note = UIFactory.CreateText(ct, "Note", Loc.T("settings.lang_note"),
                10, Theme.InkSoft, TextAnchor.UpperLeft);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            var nrt = note.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.offsetMin = new Vector2(62f, 0f);
            nrt.offsetMax = new Vector2(-84f, 0f);
            nrt.sizeDelta = new Vector2(nrt.sizeDelta.x, 30f);
            nrt.anchoredPosition = new Vector2(nrt.anchoredPosition.x, -44f);

            // CEFR badge (A1/A2) pill on the right.
            var badge = UIFactory.CreatePanel(ct, "Badge",
                new Color(Theme.AmberDeep.r, Theme.AmberDeep.g, Theme.AmberDeep.b, 0.22f), 14f);
            var brt = badge.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(56f, 32f);
            brt.anchoredPosition = new Vector2(-16f, 0f);
            _langBadge = UIFactory.CreateText(badge.transform, "Txt", "", 14, Theme.AmberDeep);
            _langBadge.font = UIFactory.DisplayFont;
            _langBadge.raycastTarget = false;
        }

        // ---------- gameplay ----------

        void BuildGameplayCard(Transform t)
        {
            var card = UIFactory.CreatePanel(t, "GameplayCard", Theme.CreamPanel, 20f);
            UIFactory.AddSoftShadow(card.gameObject, 3f, 0.14f);
            UIFactory.BandTop(card.rectTransform, 430f, 200f, 20f);
            var ct = card.transform;

            // Pace segmented control in a soft track.
            var track = UIFactory.CreatePanel(ct, "PaceTrack", Theme.Hex("F1EFE8"), 16f);
            UIFactory.BandTop(track.rectTransform, 14f, 46f, 12f);
            string[] paces = { Loc.T("pace.calm"), Loc.T("pace.normal"), Loc.T("pace.fast") };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = UIFactory.CreateButton(track.transform, "Pace" + i, paces[i], 14,
                    Theme.Hex("F1EFE8"), Theme.Ink,
                    delegate { ProceduralAudio.PlayClick(); _boot.SetDifficulty((Difficulty)idx); Refresh(); });
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.offsetMin = new Vector2(4f, 4f);
                rt.offsetMax = new Vector2(-4f, -4f);
                _paceSegs[i] = btn.GetComponent<Image>();
                _paceSegLabels[i] = btn.GetComponentInChildren<Text>();
            }

            Hairline(ct, 72f);

            // Sound row: label left, iOS-style switch right, whole row taps.
            var soundRow = UIFactory.CreateButton(ct, "SoundRow", "", 15,
                new Color(1f, 1f, 1f, 0f), Theme.Ink, null);
            UIFactory.BandTop(soundRow.GetComponent<RectTransform>(), 76f, 50f, 4f);
            _soundLabel = UIFactory.CreateText(soundRow.transform, "Label", "", 16,
                Theme.Ink, TextAnchor.MiddleLeft);
            _soundLabel.raycastTarget = false;
            var slrt = _soundLabel.rectTransform;
            slrt.offsetMin = new Vector2(16f, 0f);
            slrt.offsetMax = new Vector2(-90f, 0f);

            var swTrack = UIFactory.CreatePanel(soundRow.transform, "Track", Theme.Hex("D3D1C7"), 16f);
            _soundTrack = swTrack;
            var strt = swTrack.rectTransform;
            strt.anchorMin = strt.anchorMax = new Vector2(1f, 0.5f);
            strt.pivot = new Vector2(1f, 0.5f);
            strt.sizeDelta = new Vector2(54f, 32f);
            strt.anchoredPosition = new Vector2(-16f, 0f);
            swTrack.raycastTarget = false;
            var knob = UIFactory.CreateCircle(swTrack.transform, "Knob", Color.white);
            _soundKnob = knob.rectTransform;
            _soundKnob.anchorMin = _soundKnob.anchorMax = new Vector2(0.5f, 0.5f);
            _soundKnob.pivot = new Vector2(0.5f, 0.5f);
            _soundKnob.sizeDelta = new Vector2(26f, 26f);
            soundRow.onClick.AddListener(delegate
            {
                ProceduralAudio.Muted = !ProceduralAudio.Muted;
                RefreshSoundRow();
                ProceduralAudio.PlayClick();
            });

            Hairline(ct, 130f);

            // App-language row → full picker overlay.
            var appLang = UIFactory.CreateButton(ct, "AppLang", "", 15,
                new Color(1f, 1f, 1f, 0f), Theme.Purple,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    _langOverlay.gameObject.SetActive(true);
                });
            UIFactory.BandTop(appLang.GetComponent<RectTransform>(), 134f, 50f, 4f);

            var gic = UIFactory.CreateIcon(appLang.transform, "Icon", "map", Theme.Purple);
            var girt = gic.rectTransform;
            girt.anchorMin = girt.anchorMax = new Vector2(0f, 0.5f);
            girt.pivot = new Vector2(0.5f, 0.5f);
            girt.sizeDelta = new Vector2(24f, 24f);
            girt.anchoredPosition = new Vector2(28f, 0f);

            _appLangLabel = UIFactory.CreateText(appLang.transform, "Label", "", 15,
                Theme.Purple, TextAnchor.MiddleLeft);
            _appLangLabel.raycastTarget = false;
            var alrt = _appLangLabel.rectTransform;
            alrt.offsetMin = new Vector2(48f, 0f);
            alrt.offsetMax = new Vector2(-34f, 0f);

            var chev = UIFactory.CreateText(appLang.transform, "Chev", ">", 18,
                Theme.InkSoft, TextAnchor.MiddleRight);
            chev.raycastTarget = false;
            var crt = chev.rectTransform;
            crt.offsetMin = new Vector2(0f, 0f);
            crt.offsetMax = new Vector2(-16f, 0f);
        }

        void Hairline(Transform ct, float y)
        {
            var line = UIFactory.CreatePanel(ct, "Line", Theme.Border, 0f);
            var rt = line.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(16f, 0f);
            rt.offsetMax = new Vector2(-16f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            line.raycastTarget = false;
        }

        /// <summary>Full app-language picker: every UI language visible at
        /// once (native names, 2-column grid), current one highlighted.
        /// Picking a different one rebuilds the whole UI in that language.</summary>
        void BuildAppLangOverlay(Transform t)
        {
            var overlay = UIFactory.CreatePanel(t, "AppLangOverlay", new Color(0.10f, 0.07f, 0.16f, 0.60f), 0f);
            overlay.raycastTarget = true;
            _langOverlay = overlay.rectTransform;

            var card = UIFactory.CreatePanel(overlay.transform, "Card", Theme.CreamPanel, 24f);
            UIFactory.AddSoftShadow(card.gameObject, 5f, 0.28f);
            UIFactory.BandTop(card.rectTransform, 118f, 486f, 32f);
            var ct = card.transform;

            var title = UIFactory.CreateText(ct, "Title",
                Loc.F("settings.app_lang", "").TrimEnd(' ', ':', '：'), 18, Theme.Ink);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 16f, 28f);

            for (int i = 0; i < Loc.Supported.Length; i++)
            {
                string code = Loc.Supported[i];
                bool active = code == Loc.UILang;
                int col = i % 2, row = i / 2;
                var btn = UIFactory.CreateButton(ct, "Lang_" + code, Loc.NativeName(code), 15,
                    active ? new Color(Theme.Purple.r, Theme.Purple.g, Theme.Purple.b, 0.20f) : Color.white,
                    active ? Theme.Purple : Theme.Ink,
                    delegate
                    {
                        ProceduralAudio.PlayClick();
                        if (code == Loc.UILang) { _langOverlay.gameObject.SetActive(false); return; }
                        Loc.SetUILanguage(code);
                        _boot.RebuildUI();
                    });
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(col * 0.5f, 1f);
                rt.anchorMax = new Vector2((col + 1) * 0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(col == 0 ? 16f : 5f, 0f);
                rt.offsetMax = new Vector2(col == 1 ? -16f : -5f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 48f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(54f + row * 56f));
            }

            var close = UIFactory.CreateButton(ct, "Close", Loc.T("common.close"), 15,
                Theme.CreamPanel, Theme.InkSoft,
                delegate { ProceduralAudio.PlayClick(); _langOverlay.gameObject.SetActive(false); });
            UIFactory.BandTop(close.GetComponent<RectTransform>(), 428f, 42f, 90f);

            _langOverlay.gameObject.SetActive(false);
        }

        // ---------- refresh ----------

        public void Refresh()
        {
            string player = ProgressManager.ActiveProfile;
            string shown = string.IsNullOrEmpty(player) ? Loc.T("home.guest") : player;
            _nameLabel.text = shown.ToUpperInvariant();
            _avatarInitial.text = shown.Substring(0, 1).ToUpperInvariant();

            var info = LanguageManager.CurrentInfo;
            _heroSub.text = Loc.F("settings.learning", Loc.LangName(info.code));

            _chipLevel.text = ProgressManager.CurrentLevel.ToString();
            _chipCoins.text = ProgressManager.Coins.ToString();
            _chipGems.text = ProgressManager.Gems.ToString();
            _chipStreak.text = ProgressManager.DayStreakDisplay.ToString();

            _langLabel.text = Loc.LangName(info.code);
            _langBadge.text = info.badge;
            if (_appLangLabel != null)
                _appLangLabel.text = Loc.F("settings.app_lang", Loc.NativeName(Loc.UILang));

            for (int i = 0; i < 3; i++)
            {
                bool active = (int)_boot.CurrentDifficulty == i;
                _paceSegs[i].color = active ? Theme.AmberDeep : Theme.Hex("F1EFE8");
                _paceSegLabels[i].color = active ? Color.white : Theme.Ink;
            }

            RefreshSoundRow();
        }

        void RefreshSoundRow()
        {
            bool on = !ProceduralAudio.Muted;
            if (_soundLabel != null)
                _soundLabel.text = Loc.T(on ? "settings.sound_on" : "settings.sound_off");
            if (_soundTrack != null)
                _soundTrack.color = on ? Theme.Teal : Theme.Hex("D3D1C7");
            if (_soundKnob != null)
                _soundKnob.anchoredPosition = new Vector2(on ? 11f : -11f, 0f);
        }
    }
}
