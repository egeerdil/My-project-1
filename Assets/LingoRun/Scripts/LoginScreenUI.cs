using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Login + onboarding. Existing players are listed with their learning
    /// language; creating a new player runs a 3-step wizard:
    ///   1. name  →  2. language (fixed per profile, Duolingo-style)  →
    ///   3. topic personalization (pick at least MinTopics — early levels
    ///      teach words from those topics first).
    /// One profile = one language; to learn another language you create a new
    /// profile. The first ProfileManager.FreeProfileSlots profiles are free,
    /// extra slots are reserved for a future premium unlock.
    /// </summary>
    public class LoginScreenUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;
        RectTransform _content;   // rebuilt on every Refresh
        InputField _input;
        Text _error;

        // Wizard state
        int _step;                 // 0 list, 1 name, 2 language, 3 topics
        string _pendingName = "";
        string _pendingLang = "";
        readonly HashSet<string> _pendingTopics = new HashSet<string>();

        public LoginScreenUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "LoginScreen");

            var caption = UIFactory.CreateText(_root.transform, "Caption", Loc.T("login.who"), 14, Theme.Teal);
            UIFactory.BandTop(caption.rectTransform, 96f, 22f);

            var title = UIFactory.CreateText(_root.transform, "Title", "LINGORUN", 40, Theme.Orange);
            UIFactory.BandTop(title.rectTransform, 120f, 52f);

            var sub = UIFactory.CreateText(_root.transform, "Sub",
                Loc.T("login.saved"), 14, Theme.InkSoft);
            UIFactory.BandTop(sub.rectTransform, 176f, 20f);

            _content = UIFactory.CreateRoot(_root.transform, "Content");

            // Lino greets new players from the bottom-right corner — the
            // brand's face belongs on the front door.
            var bunnyHost = new GameObject("LoginBunny", typeof(RectTransform));
            bunnyHost.transform.SetParent(_root.transform, false);
            var bhrt = bunnyHost.GetComponent<RectTransform>();
            bhrt.anchorMin = bhrt.anchorMax = new Vector2(1f, 0f);
            bhrt.pivot = new Vector2(0.5f, 0.5f);
            bhrt.sizeDelta = new Vector2(120f, 160f);
            bhrt.anchoredPosition = new Vector2(-78f, 96f);
            var refs = MascotUI.BuildBunny(bunnyHost.transform, Vector2.zero, 0.85f);
            var idle = bunnyHost.AddComponent<BunnyIdle>();
            idle.refs = refs;

            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            // Reset when leaving so the next visit starts fresh (bootstrap
            // calls Refresh() before SetVisible(true), so resetting on show
            // would desync state from the already-built content).
            if (!visible) ResetWizard();
            UIFX.SetVisible(_root, visible);
        }

        void ResetWizard()
        {
            _step = 0;
            _pendingName = "";
            _pendingLang = "";
            _pendingTopics.Clear();
        }

        void ClearContent()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                Object.Destroy(_content.GetChild(i).gameObject);
            _input = null;
            _error = null;
        }

        public void Refresh()
        {
            ClearContent();
            // No profiles yet → jump straight into onboarding.
            if (_step == 0 && ProfileManager.GetProfiles().Count == 0) _step = 1;

            switch (_step)
            {
                case 0: BuildProfileList(); break;
                case 1: BuildNameStep(); break;
                case 2: BuildLanguageStep(); break;
                case 3: BuildTopicStep(); break;
            }
        }

        void GoTo(int step) { _step = step; Refresh(); }

        // ---------------------------------------------------------------- step 0
        void BuildProfileList()
        {
            var t = _content.transform;
            float y = 224f;
            List<string> profiles = ProfileManager.GetProfiles();

            var pick = UIFactory.CreateText(t, "PickLabel", Loc.T("login.choose"), 14, Theme.InkSoft, TextAnchor.MiddleLeft);
            UIFactory.BandTop(pick.rectTransform, y, 18f, 28f);
            y += 26f;

            foreach (var name in profiles)
            {
                string captured = name;
                bool wasLast = name == ProfileManager.LastProfile;
                var btn = UIFactory.CreateButton(t, "Profile_" + name, name.ToUpperInvariant(), 18,
                    wasLast ? new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.20f) : Theme.CreamPanel,
                    wasLast ? Theme.Teal : Theme.Ink,
                    delegate { ProceduralAudio.PlayClick(); Choose(captured); });
                UIFactory.BandTop(btn.GetComponent<RectTransform>(), y, 50f, 28f);

                // Language badge on the right: each profile is locked to one language.
                string lang = ProfileManager.GetLanguage(name);
                string badge = "ES";
                foreach (var info in LanguageManager.All)
                    if (info.code == lang) { badge = info.badge; break; }
                var badgeText = UIFactory.CreateText(btn.transform, "Lang", badge, 12,
                    wasLast ? Theme.Teal : Theme.InkSoft, TextAnchor.MiddleRight);
                var brt = badgeText.rectTransform;
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.offsetMin = new Vector2(14f, 0f);
                brt.offsetMax = new Vector2(-16f, 0f);

                y += 58f;
            }
            y += 14f;

            if (profiles.Count >= ProfileManager.MaxProfiles)
            {
                var full = UIFactory.CreateText(t, "Full",
                    Loc.F("login.full", ProfileManager.MaxProfiles), 14,
                    Theme.InkSoft, TextAnchor.MiddleLeft);
                UIFactory.BandTop(full.rectTransform, y, 22f, 28f);
                return;
            }

            if (profiles.Count >= ProfileManager.AvailableSlots)
            {
                // Free slots used up — an extra slot (= an extra language)
                // is the premium hook, unlockable with gems.
                var locked = UIFactory.CreateButton(t, "NewLocked",
                    Loc.F("login.unlock", GameConfig.PremiumSlotGems), 17,
                    new Color(Theme.Purple.r, Theme.Purple.g, Theme.Purple.b, 0.16f), Theme.Purple,
                    delegate
                    {
                        ProceduralAudio.PlayClick();
                        if (ProfileManager.TryUnlockPremiumSlot())
                        {
                            ProceduralAudio.PlayCorrect();
                            Refresh();
                        }
                        else if (_error != null)
                        {
                            _error.text = Loc.F("login.gems_short", ProgressManager.Gems, GameConfig.PremiumSlotGems);
                        }
                    });
                UIFactory.BandTop(locked.GetComponent<RectTransform>(), y, 50f, 28f);
                y += 58f;
                _error = UIFactory.CreateText(t, "Error", "", 14, Theme.InkSoft);
                UIFactory.BandTop(_error.rectTransform, y, 20f);
                return;
            }

            var create = UIFactory.CreateButton(t, "New", Loc.T("login.new"), 17,
                Theme.Amber, Theme.WordChipDark,
                delegate { ProceduralAudio.PlayClick(); GoTo(1); });
            UIFactory.BandTop(create.GetComponent<RectTransform>(), y, 52f, 28f);
        }

        // ---------------------------------------------------------------- step 1
        void BuildNameStep()
        {
            var t = _content.transform;
            float y = 224f;

            var label = UIFactory.CreateText(t, "StepLabel", Loc.T("login.step1"), 14,
                Theme.InkSoft, TextAnchor.MiddleLeft);
            UIFactory.BandTop(label.rectTransform, y, 18f, 28f);
            y += 26f;

            var inputGo = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(t, false);
            var inputBg = inputGo.GetComponent<Image>();
            inputBg.color = Color.white;
            inputBg.raycastTarget = true;
            UIFactory.ApplyRounded(inputBg, 14f);
            UIFactory.AddSoftShadow(inputGo);
            var inputRt = inputGo.GetComponent<RectTransform>();
            UIFactory.BandTop(inputRt, y, 50f, 28f);

            var placeholder = UIFactory.CreateText(inputGo.transform, "Placeholder", Loc.T("login.type_name"),
                17, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.35f), TextAnchor.MiddleLeft, FontStyle.Italic);
            placeholder.rectTransform.offsetMin = new Vector2(14f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-14f, -4f);

            var text = UIFactory.CreateText(inputGo.transform, "Text", "", 18, Theme.Ink, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.rectTransform.offsetMin = new Vector2(14f, 4f);
            text.rectTransform.offsetMax = new Vector2(-14f, -4f);

            _input = inputGo.GetComponent<InputField>();
            _input.textComponent = text;
            _input.placeholder = placeholder;
            _input.characterLimit = ProfileManager.MaxNameLength;
            _input.targetGraphic = inputBg;
            _input.text = _pendingName;
            y += 58f;

            var next = UIFactory.CreateButton(t, "Next", Loc.T("common.next"), 19,
                Theme.Amber, Theme.WordChipDark,
                delegate { ProceduralAudio.PlayClick(); TryName(); });
            UIFactory.BandTop(next.GetComponent<RectTransform>(), y, 54f, 28f);
            y += 60f;

            _error = UIFactory.CreateText(t, "Error", "", 14, Theme.BadRed);
            UIFactory.BandTop(_error.rectTransform, y, 20f);
            y += 26f;

            if (ProfileManager.GetProfiles().Count > 0)
            {
                var back = UIFactory.CreateButton(t, "Back", "<  BACK", 15,
                    Theme.CreamPanel, Theme.Ink,
                    delegate { ProceduralAudio.PlayClick(); GoTo(0); });
                UIFactory.BandTop(back.GetComponent<RectTransform>(), y, 40f, 120f);
            }
        }

        void TryName()
        {
            string raw = _input != null ? _input.text : "";
            string name = raw != null ? raw.Trim() : "";
            if (name.Length == 0)
            {
                if (_error != null) _error.text = Loc.T("login.type_first");
                return;
            }
            foreach (var p in ProfileManager.GetProfiles())
                if (p.ToLowerInvariant() == name.ToLowerInvariant())
                {
                    if (_error != null) _error.text = Loc.T("login.name_taken");
                    return;
                }
            _pendingName = name;
            GoTo(2);
        }

        // ---------------------------------------------------------------- step 2
        void BuildLanguageStep()
        {
            var t = _content.transform;
            float y = 224f;

            var label = UIFactory.CreateText(t, "StepLabel", Loc.T("login.step2"), 14,
                Theme.InkSoft, TextAnchor.MiddleLeft);
            UIFactory.BandTop(label.rectTransform, y, 18f, 28f);
            y += 26f;

            // Two-column grid — ten languages won't fit as a single stack.
            for (int i = 0; i < LanguageManager.All.Length; i++)
            {
                var info = LanguageManager.All[i];
                string code = info.code;
                int col = i % 2, row = i / 2;
                var btn = UIFactory.CreateButton(t, "Lang_" + code, Loc.LangName(code), 15,
                    Theme.CreamPanel, Theme.Ink,
                    delegate { ProceduralAudio.PlayClick(); _pendingLang = code; GoTo(3); });
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(col * 0.5f, 1f);
                rt.anchorMax = new Vector2((col + 1) * 0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(col == 0 ? 28f : 5f, 0f);
                rt.offsetMax = new Vector2(col == 1 ? -28f : -5f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 46f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(y + row * 54f));

                var badgeText = UIFactory.CreateText(btn.transform, "Badge", info.badge, 11,
                    Theme.InkSoft, TextAnchor.MiddleRight);
                var brt = badgeText.rectTransform;
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.offsetMin = new Vector2(10f, 0f);
                brt.offsetMax = new Vector2(-12f, 0f);
            }
            y += Mathf.CeilToInt(LanguageManager.All.Length / 2f) * 54f + 6f;

            var note = UIFactory.CreateText(t, "Note",
                Loc.T("login.one_lang"), 13, Theme.InkSoft);
            UIFactory.BandTop(note.rectTransform, y, 32f, 28f);
            y += 40f;

            var back = UIFactory.CreateButton(t, "Back", "<  BACK", 15,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); GoTo(1); });
            UIFactory.BandTop(back.GetComponent<RectTransform>(), y, 40f, 120f);
        }

        // ---------------------------------------------------------------- step 3
        void BuildTopicStep()
        {
            var t = _content.transform;
            float y = 210f;

            var label = UIFactory.CreateText(t, "StepLabel",
                Loc.F("login.step3", ProfileManager.MinTopics), 14,
                Theme.InkSoft, TextAnchor.MiddleLeft);
            UIFactory.BandTop(label.rectTransform, y, 18f, 28f);
            y += 24f;

            var hint = UIFactory.CreateText(t, "Hint",
                Loc.T("login.topics_hint"), 13,
                Theme.InkSoft, TextAnchor.MiddleLeft);
            UIFactory.BandTop(hint.rectTransform, y, 16f, 28f);
            y += 24f;

            // Chip grid: 3 columns.
            string[] topics = VocabDatabase.TopicOrder;
            const int cols = 3;
            const float chipH = 40f;
            const float gap = 8f;
            const float sideMargin = 24f;
            float gridTop = y;
            for (int i = 0; i < topics.Length; i++)
            {
                string topic = topics[i];
                int col = i % cols, row = i / cols;
                bool selected = _pendingTopics.Contains(topic);

                var btn = UIFactory.CreateButton(t, "Topic_" + topic,
                    Loc.Topic(topic), 12,
                    selected ? new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.22f) : Theme.CreamPanel,
                    selected ? Theme.Teal : Theme.Ink, null);
                btn.onClick.AddListener(delegate
                {
                    ProceduralAudio.PlayClick();
                    if (!_pendingTopics.Remove(topic)) _pendingTopics.Add(topic);
                    Refresh();  // rebuild to recolor chips + CTA
                });
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2((float)col / cols, 1f);
                rt.anchorMax = new Vector2((float)(col + 1) / cols, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(col == 0 ? sideMargin : gap * 0.5f, 0f);
                rt.offsetMax = new Vector2(col == cols - 1 ? -sideMargin : -gap * 0.5f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, chipH);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -(gridTop + row * (chipH + gap)));
            }
            int rows = (topics.Length + cols - 1) / cols;
            y = gridTop + rows * (chipH + gap) + 10f;

            bool ready = _pendingTopics.Count >= ProfileManager.MinTopics;
            var cta = UIFactory.CreateButton(t, "Create",
                ready ? Loc.T("login.start") : Loc.F("login.pick_more", ProfileManager.MinTopics - _pendingTopics.Count),
                18,
                ready ? Theme.Amber : Theme.CreamPanel,
                ready ? Theme.WordChipDark : Theme.InkSoft,
                delegate { ProceduralAudio.PlayClick(); TryCreate(); });
            UIFactory.BandTop(cta.GetComponent<RectTransform>(), y, 54f, 28f);
            y += 60f;

            _error = UIFactory.CreateText(t, "Error", "", 14, Theme.BadRed);
            UIFactory.BandTop(_error.rectTransform, y, 18f);
            y += 22f;

            var back = UIFactory.CreateButton(t, "Back", "<  BACK", 15,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); GoTo(2); });
            UIFactory.BandTop(back.GetComponent<RectTransform>(), y, 40f, 120f);
        }

        void TryCreate()
        {
            if (_pendingTopics.Count < ProfileManager.MinTopics)
            {
                if (_error != null)
                    _error.text = Loc.F("login.pick_min", ProfileManager.MinTopics);
                return;
            }
            var topics = new List<string>(_pendingTopics);
            string created = ProfileManager.CreateProfile(_pendingName, _pendingLang, topics);
            if (created == null)
            {
                if (_error != null) _error.text = Loc.T("login.create_fail");
                return;
            }
            Choose(created);
        }

        void Choose(string name)
        {
            ProfileManager.SetActive(name);
            _boot.OnLoggedIn();
        }
    }
}
