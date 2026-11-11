using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Game-over screen: final score, new-best flag, words/accuracy/streak stats,
    /// newly-mastered count, A2 unlock banner, and Run Again / Home / Review actions.
    /// </summary>
    public class GameOverUI
    {
        readonly LingoRunBootstrap _boot;
        readonly RectTransform _root;

        Text _banner;      // A2 unlock celebration
        Text _newBest;
        Text _caption;
        Text _score;
        Text _wordsValue, _accuracyValue, _streakValue;
        Text _mastered;
        Text _reviewHint;
        Text _achievementLine;
        Text _coinsLine;
        Text _againLabel;
        RunResults _results;
        bool _chestOpened;

        // chest animation pieces
        RectTransform _chestRoot;
        RectTransform _chestLid;
        RectTransform _chestBodyGroup;
        Button _chestButton;
        Text _chestTapLabel;
        Text _chestReward;
        Button _reviewButton;
        Text _reviewLabel;
        bool _lastWasReview;
        bool _lastWasBlaster;
        bool _lastWasBubbles;
        bool _lastWasMatch;
        bool _lastWasSpeech;
        bool _lastWasFreePlay;
        int _lastMapLevel;
        bool _lastMapPassed;

        public GameOverUI(Transform parent, LingoRunBootstrap boot)
        {
            _boot = boot;
            _root = UIFactory.CreateRoot(parent, "GameOverScreen");
            Build();
            SetVisible(false);
        }

        public void SetVisible(bool visible) { UIFX.SetVisible(_root, visible); }

        void Build()
        {
            var t = _root.transform;

            // Dim panel so the meadow reads as background.
            var dim = UIFactory.CreatePanel(t, "Dim", new Color(0.98f, 0.97f, 1f, 0.75f), 0f);
            dim.raycastTarget = true; // swallow stray taps

            _banner = UIFactory.CreateText(t, "Banner", "* A2 UNLOCKED *", 19, Theme.AmberDeep);
            UIFactory.BandTop(_banner.rectTransform, 64f, 30f);

            _newBest = UIFactory.CreateText(t, "NewBest", "* NEW BEST *", 16, Theme.AmberDeep);
            UIFactory.BandTop(_newBest.rectTransform, 104f, 22f);

            _caption = UIFactory.CreateText(t, "Caption", "RUN COMPLETE", 14, Theme.InkSoft);
            UIFactory.BandTop(_caption.rectTransform, 132f, 20f);

            _score = UIFactory.CreateText(t, "Score", "0", 64, Theme.Orange);
            _score.font = UIFactory.DisplayFont;
            UIFactory.AddSoftShadow(_score.gameObject, 4f, 0.20f);
            UIFactory.BandTop(_score.rectTransform, 160f, 80f);

            var pointsLabel = UIFactory.CreateText(t, "Points", "POINTS", 14, Theme.InkSoft);
            UIFactory.BandTop(pointsLabel.rectTransform, 244f, 18f);

            // ----- stats row -----
            string[] statNames = { "WORDS", "ACCURACY", "STREAK" };
            Text[] values = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var cell = UIFactory.CreatePanel(t, "Stat" + i, Theme.CreamPanel);
                var rt = cell.rectTransform;
                rt.anchorMin = new Vector2(i / 3f, 1f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(i == 0 ? 24f : 6f, 0f);
                rt.offsetMax = new Vector2(i == 2 ? -24f : -6f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 74f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -292f);

                var val = UIFactory.CreateText(cell.transform, "Value", "0", 26, Theme.Teal);
                UIFactory.BandTop(val.rectTransform, 10f, 36f);
                values[i] = val;

                var label = UIFactory.CreateText(cell.transform, "Label", statNames[i], 12, Theme.InkSoft);
                UIFactory.BandTop(label.rectTransform, 50f, 16f);
            }
            _wordsValue = values[0];
            _accuracyValue = values[1];
            _streakValue = values[2];

            _mastered = UIFactory.CreateText(t, "Mastered", "", 15, Theme.GoodDark);
            UIFactory.BandTop(_mastered.rectTransform, 380f, 20f);

            _reviewHint = UIFactory.CreateText(t, "ReviewHint", "", 14, Theme.Teal);
            UIFactory.BandTop(_reviewHint.rectTransform, 402f, 18f);

            _achievementLine = UIFactory.CreateText(t, "Achievement", "", 15, Theme.AmberDeep);
            UIFactory.BandTop(_achievementLine.rectTransform, 418f, 20f);

            // ----- coins summary + chest -----
            _coinsLine = UIFactory.CreateText(t, "Coins", "", 14, Theme.InkSoft);
            UIFactory.BandTop(_coinsLine.rectTransform, 440f, 18f);

            BuildChest(t);

            // ----- buttons -----
            var again = UIFactory.CreateButton(t, "Again", "RUN AGAIN", 22,
                Theme.Amber, Theme.WordChipDark,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (_lastMapLevel > 0)
                    {
                        // World map has no level cap: clearing a node always
                        // advances to the next one, forever.
                        int next = _lastMapPassed ? _lastMapLevel + 1 : _lastMapLevel;
                        _boot.StartFromMap(next);
                        return;
                    }
                    bool review = _lastWasReview && ProgressManager.MissedCount > 0;
                    if (_lastWasFreePlay) _boot.StartFreePlay(_boot.CurrentMode);
                    else if (_lastWasBlaster) _boot.StartBlaster(review);
                    else if (_lastWasBubbles) _boot.StartBubbles(review);
                    else if (_lastWasMatch) _boot.StartMatch(review);
                    else if (_lastWasSpeech) _boot.StartSpeech(review);
                    else _boot.StartRun(review);
                });
            UIFactory.BandTop(again.GetComponent<RectTransform>(), 528f, 60f, 24f);
            _againLabel = again.GetComponentInChildren<Text>();

            var home = UIFactory.CreateButton(t, "Home", "HOME", 17,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); _boot.ShowHome(); });
            var homeRt = home.GetComponent<RectTransform>();
            homeRt.anchorMin = new Vector2(0f, 1f);
            homeRt.anchorMax = new Vector2(0.5f, 1f);
            homeRt.pivot = new Vector2(0.5f, 1f);
            homeRt.offsetMin = new Vector2(24f, 0f);
            homeRt.offsetMax = new Vector2(-6f, 0f);
            homeRt.sizeDelta = new Vector2(homeRt.sizeDelta.x, 52f);
            homeRt.anchoredPosition = new Vector2(homeRt.anchoredPosition.x, -596f);

            _reviewButton = UIFactory.CreateButton(t, "Review", "REVIEW", 17,
                Theme.CreamPanel, Theme.Teal,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (_lastWasBlaster) _boot.StartBlaster(true);
                    else if (_lastWasBubbles) _boot.StartBubbles(true);
                    else if (_lastWasMatch) _boot.StartMatch(true);
                    else if (_lastWasSpeech) _boot.StartSpeech(true);
                    else _boot.StartRun(true);
                });
            var revRt = _reviewButton.GetComponent<RectTransform>();
            revRt.anchorMin = new Vector2(0.5f, 1f);
            revRt.anchorMax = new Vector2(1f, 1f);
            revRt.pivot = new Vector2(0.5f, 1f);
            revRt.offsetMin = new Vector2(6f, 0f);
            revRt.offsetMax = new Vector2(-24f, 0f);
            revRt.sizeDelta = new Vector2(revRt.sizeDelta.x, 52f);
            revRt.anchoredPosition = new Vector2(revRt.anchoredPosition.x, -596f);
            _reviewLabel = _reviewButton.GetComponentInChildren<Text>();
        }

        /// <summary>Little chest drawn from UI pieces: body + dark band + gold
        /// lock, with a lid that can pop open, sitting inside a tappable area.</summary>
        void BuildChest(Transform t)
        {
            var rootGo = new GameObject("ChestRoot", typeof(RectTransform));
            rootGo.transform.SetParent(t, false);
            _chestRoot = rootGo.GetComponent<RectTransform>();
            UIFactory.PlaceTop(_chestRoot, GameConfig.RefWidth * 0.5f, 490f, 240f, 74f);

            // Invisible tap area covering the whole chest zone.
            var btnGo = new GameObject("Tap", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(rootGo.transform, false);
            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(1f, 1f, 1f, 0.01f);
            btnImg.raycastTarget = true;
            UIFactory.Stretch(btnGo.GetComponent<RectTransform>());
            _chestButton = btnGo.GetComponent<Button>();
            _chestButton.onClick.AddListener(delegate { OpenChest(); });

            // Body group (shaken during the animation).
            var bodyGroupGo = new GameObject("BodyGroup", typeof(RectTransform));
            bodyGroupGo.transform.SetParent(rootGo.transform, false);
            _chestBodyGroup = bodyGroupGo.GetComponent<RectTransform>();
            _chestBodyGroup.anchorMin = _chestBodyGroup.anchorMax = new Vector2(0.5f, 0.5f);
            _chestBodyGroup.pivot = new Vector2(0.5f, 0.5f);
            _chestBodyGroup.sizeDelta = new Vector2(90f, 70f);
            _chestBodyGroup.anchoredPosition = Vector2.zero;

            var body = UIFactory.CreatePanel(_chestBodyGroup, "Body", Theme.Hex("8A5A2B"), 10f);
            var bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0.5f, 0f);
            bodyRt.anchorMax = new Vector2(0.5f, 0f);
            bodyRt.pivot = new Vector2(0.5f, 0f);
            bodyRt.sizeDelta = new Vector2(84f, 40f);
            bodyRt.anchoredPosition = new Vector2(0f, 2f);

            var band = UIFactory.CreatePanel(body.transform, "Band", Theme.Hex("6E4520"), 3f);
            var bandRt = band.rectTransform;
            bandRt.anchorMin = new Vector2(0f, 0.5f);
            bandRt.anchorMax = new Vector2(1f, 0.5f);
            bandRt.pivot = new Vector2(0.5f, 0.5f);
            bandRt.offsetMin = new Vector2(0f, -4f);
            bandRt.offsetMax = new Vector2(0f, 4f);

            var lockDot = UIFactory.CreateCircle(body.transform, "Lock", Theme.Amber);
            var lockRt = lockDot.rectTransform;
            lockRt.anchorMin = lockRt.anchorMax = new Vector2(0.5f, 1f);
            lockRt.pivot = new Vector2(0.5f, 0.5f);
            lockRt.sizeDelta = new Vector2(16f, 16f);
            lockRt.anchoredPosition = new Vector2(0f, 0f);

            // Lid: pivots at its back-bottom corner so rotating it "flips" it open.
            var lid = UIFactory.CreatePanel(_chestBodyGroup, "Lid", Theme.Hex("A06A34"), 12f);
            _chestLid = lid.rectTransform;
            _chestLid.anchorMin = new Vector2(0.5f, 0f);
            _chestLid.anchorMax = new Vector2(0.5f, 0f);
            _chestLid.pivot = new Vector2(0.1f, 0f);
            _chestLid.sizeDelta = new Vector2(88f, 24f);
            _chestLid.anchoredPosition = new Vector2(-44f * 0.8f, 42f);

            _chestTapLabel = UIFactory.CreateText(rootGo.transform, "TapLabel", "TAP TO OPEN!", 14, Theme.Ink);
            var tapRt = _chestTapLabel.rectTransform;
            tapRt.anchorMin = new Vector2(0.5f, 0.5f);
            tapRt.anchorMax = new Vector2(0.5f, 0.5f);
            tapRt.pivot = new Vector2(0.5f, 0.5f);
            tapRt.sizeDelta = new Vector2(200f, 20f);
            tapRt.anchoredPosition = new Vector2(0f, -46f);
            _chestTapLabel.raycastTarget = false;

            _chestReward = UIFactory.CreateText(rootGo.transform, "Reward", "", 20, Theme.AmberDeep);
            _chestReward.font = UIFactory.DisplayFont;
            var rewRt = _chestReward.rectTransform;
            rewRt.anchorMin = rewRt.anchorMax = new Vector2(0.5f, 0.5f);
            rewRt.pivot = new Vector2(0.5f, 0.5f);
            rewRt.sizeDelta = new Vector2(340f, 30f);
            rewRt.anchoredPosition = new Vector2(0f, 48f);
            _chestReward.raycastTarget = false;
            _chestReward.gameObject.SetActive(false);
        }

        void OpenChest()
        {
            if (_chestOpened || _results == null) return;
            _chestOpened = true;
            _chestButton.interactable = false;
            _boot.RunRoutine(ChestAnimation());
        }

        System.Collections.IEnumerator ChestAnimation()
        {
            _chestTapLabel.gameObject.SetActive(false);

            // 1) Shake with anticipation.
            ProceduralAudio.PlayClick();
            float t = 0f;
            const float shakeDur = 0.4f;
            while (t < shakeDur)
            {
                t += Time.deltaTime;
                float wobble = Mathf.Sin(t * 55f) * Mathf.Lerp(2f, 8f, t / shakeDur);
                _chestBodyGroup.localRotation = Quaternion.Euler(0f, 0f, wobble);
                yield return null;
            }
            _chestBodyGroup.localRotation = Quaternion.identity;

            // 2) Lid pops open (rotates back with a little overshoot).
            ProceduralAudio.PlayCorrect();
            t = 0f;
            const float lidDur = 0.28f;
            while (t < lidDur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / lidDur);
                float overshoot = 1.7f * k * k * k - 2.4f * k * k + 1.7f * k; // eased with wobble
                _chestLid.localRotation = Quaternion.Euler(0f, 0f, 78f * overshoot);
                yield return null;
            }
            _chestLid.localRotation = Quaternion.Euler(0f, 0f, 62f);

            // 3) Coin burst: little amber circles fly out with gravity and fade.
            int coinCount = _results.chestSkinIndex >= 0 || _results.chestShapeIndex >= 0
                ? 16 : Mathf.Clamp(6 + _results.chestCoins / 6, 8, 18);
            var coins = new RectTransform[coinCount];
            var vels = new Vector2[coinCount];
            var imgs = new Image[coinCount];
            for (int i = 0; i < coinCount; i++)
            {
                var c = UIFactory.CreateIcon(_chestRoot, "Coin" + i, "coin", Theme.Amber);
                var crt = c.rectTransform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                float size = Random.Range(9f, 15f);
                crt.sizeDelta = new Vector2(size, size);
                crt.anchoredPosition = new Vector2(Random.Range(-14f, 14f), 18f);
                coins[i] = crt;
                imgs[i] = c;
                vels[i] = new Vector2(Random.Range(-150f, 150f), Random.Range(220f, 380f));
            }

            // 4) Reward text pops while coins fly.
            string reward;
            if (_results.chestSkinIndex >= 0) reward = "NEW BALL COLOR!";
            else if (_results.chestShapeIndex >= 0) reward = "NEW BALL SHAPE!";
            else reward = "+" + _results.chestCoins + " COINS!";
            _chestReward.text = reward;
            _chestReward.gameObject.SetActive(true);

            t = 0f;
            const float burstDur = 1.0f;
            int lastTick = -1;
            while (t < burstDur)
            {
                float dt = Time.deltaTime;
                t += dt;

                // coin physics
                for (int i = 0; i < coinCount; i++)
                {
                    vels[i].y -= 900f * dt; // gravity
                    coins[i].anchoredPosition += vels[i] * dt;
                    if (t > 0.45f)
                    {
                        var col = imgs[i].color;
                        col.a = Mathf.Clamp01(1f - (t - 0.45f) / 0.5f);
                        imgs[i].color = col;
                    }
                }

                // reward text pop (scale 0 -> 1.2 -> 1)
                float k = Mathf.Clamp01(t / 0.3f);
                float scale = k < 0.7f ? Mathf.Lerp(0.2f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f);
                _chestReward.rectTransform.localScale = Vector3.one * scale;

                // coin tick sounds
                int tick = (int)(t / 0.12f);
                if (tick != lastTick && tick < 4) { ProceduralAudio.PlayClick(); lastTick = tick; }

                yield return null;
            }

            for (int i = 0; i < coinCount; i++)
                Object.Destroy(coins[i].gameObject);
            _chestReward.rectTransform.localScale = Vector3.one;

            _coinsLine.text = BuildCoinsSummary(_results, true);
        }

        /// <summary>Short uppercase label for a World Map mode — used to tell
        /// the player which mode a level slot actually wants when they've
        /// completed it in the "wrong" one (see the levelAdvanced branch
        /// in Populate).</summary>
        static string MapModeLabel(GameMode m)
        {
            switch (m)
            {
                case GameMode.Sentences: return "SENTENCES";
                case GameMode.Blaster: return "BLASTER";
                case GameMode.Match: return "MATCH";
                case GameMode.Bubbles: return "BUBBLES";
                default: return "WORDS";
            }
        }

        static string BuildCoinsSummary(RunResults r, bool chestOpened)
        {
            int shown = r.runCoins + r.achievementCoins + r.levelBonusCoins
                + (r.dailyGoalBonus ? GameConfig.DailyGoalCoins : 0)
                + (chestOpened ? r.chestCoins : 0);
            string s = Loc.F("go.coins_earned", shown);
            if (r.levelBonusCoins > 0) s += "  (level +" + r.levelBonusCoins + ")";
            if (r.dailyGoalBonus) s += "  (daily goal +" + GameConfig.DailyGoalCoins + ")";
            s += "   ·   " + Loc.F("go.balance", r.coinsBalance);
            if (r.gemsEarned > 0) s += "   ·   +" + r.gemsEarned + " GEMS";
            return s;
        }

        public void Populate(RunResults r)
        {
            _results = r;
            _chestOpened = false;
            _chestButton.interactable = true;
            _chestLid.localRotation = Quaternion.identity;
            _chestBodyGroup.localRotation = Quaternion.identity;
            _chestTapLabel.gameObject.SetActive(true);
            _chestReward.gameObject.SetActive(false);
            _coinsLine.text = BuildCoinsSummary(r, false);
            _lastWasReview = r.reviewMode;
            _lastWasBlaster = r.isBlaster;
            _lastWasBubbles = r.isBubbles;
            _lastWasMatch = r.isMatch;
            _lastWasSpeech = r.isSpeech;
            _lastWasFreePlay = r.isFreePlay;
            _lastMapLevel = r.mapLevel;
            _lastMapPassed = r.mapPassed;
            _banner.gameObject.SetActive(r.a2JustUnlocked);
            _newBest.gameObject.SetActive(r.newBest);
            if (r.mapLevel > 0)
            {
                _caption.text = r.mapPassed
                    ? Loc.F("go.map_cleared", r.mapLevel)
                    : Loc.F("go.map_failed", r.mapLevel);
                _caption.color = r.mapPassed ? Theme.Teal : Theme.BadRed;
                _againLabel.text = r.mapPassed ? Loc.T("go.next_level") : Loc.T("go.retry_level");
            }
            else if (r.levelMode)
            {
                // levelCompleted (finished the fixed set) and levelAdvanced
                // (that mode matched what the World Map's cycle calls for
                // at this level, so CurrentLevel actually moved on) can now
                // disagree — e.g. clearing a Words level while the map is
                // asking for Sentences at this slot. Three distinct states:
                if (r.levelCompleted && r.levelAdvanced)
                {
                    _caption.text = Loc.F("go.level_complete", r.levelNumber);
                    _caption.color = Theme.Teal;
                    _againLabel.text = Loc.T("go.next_level");
                }
                else if (r.levelCompleted)
                {
                    string neededMode = MapModeLabel(GameConfig.GetMapLevel(r.levelNumber).mode);
                    _caption.text = "LEVEL " + r.levelNumber + " DONE — PLAY " + neededMode + " TO ADVANCE";
                    _caption.color = Theme.Teal;
                    _againLabel.text = Loc.T("go.play_again");
                }
                else
                {
                    _caption.text = Loc.F("go.level_failed", r.levelNumber);
                    _caption.color = Theme.BadRed;
                    _againLabel.text = Loc.T("go.retry_level");
                }
            }
            else
            {
                string modeCompleteLabel = r.isBlaster ? "BLASTER COMPLETE"
                    : r.isBubbles ? Loc.T("go.bubbles_complete")
                    : r.isMatch ? Loc.T("go.match_complete")
                    : r.isSpeech ? Loc.T("go.speak_complete") : Loc.T("go.run_complete");
                _caption.text = r.reviewMode ? Loc.T("go.review_complete") : modeCompleteLabel;
                _caption.color = Theme.InkSoft;
                _againLabel.text = r.reviewMode || (!r.isBlaster && !r.isBubbles && !r.isMatch && !r.isSpeech)
                    ? Loc.T("go.run_again") : Loc.T("go.play_again");
            }
            // Hearts feedback: failing cost a heart, finishing review earned one.
            if (r.heartLost) _caption.text += "   ·   " + Loc.T("go.heart_lost");
            else if (r.heartEarned) _caption.text += "   ·   " + Loc.T("go.heart_earned");

            _score.text = r.score.ToString();
            _wordsValue.text = r.wordsLearned.ToString();
            _accuracyValue.text = r.accuracy + "%";
            _streakValue.text = r.bestStreak.ToString();

            if (r.newlyMastered > 0)
            {
                _mastered.gameObject.SetActive(true);
                _mastered.text = Loc.F("go.mastered", r.newlyMastered);
            }
            else
            {
                _mastered.gameObject.SetActive(false);
            }

            if (r.newAchievements.Count > 0)
            {
                _achievementLine.gameObject.SetActive(true);
                string names = r.newAchievements[0];
                if (r.newAchievements.Count == 2) names += "  +  " + r.newAchievements[1];
                else if (r.newAchievements.Count > 2) names += "  +" + (r.newAchievements.Count - 1) + " more";
                _achievementLine.text = "ACHIEVEMENT UNLOCKED: " + names.ToUpperInvariant();
            }
            else
            {
                _achievementLine.gameObject.SetActive(false);
            }

            int missed = r.missedPoolCount;
            _reviewHint.text = missed > 0
                ? Loc.F("go.items_review", missed)
                : "";
            _reviewButton.interactable = missed > 0;
            _reviewLabel.text = missed > 0 ? Loc.F("go.review_n", missed) : Loc.T("go.review");
            _reviewLabel.color = missed > 0 ? Theme.Teal : Theme.InkSoft;
        }
    }
}
