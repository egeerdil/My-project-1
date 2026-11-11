using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Bubble Spell: a word-spelling arcade mode ported from the web version's
    /// BubbleGame. The English prompt is shown; bubbles carrying single
    /// letters (the target word's letters plus a handful of decoy letters)
    /// float in the play area and must be tapped in order to spell the
    /// target-language word. Wrong taps shake the bubble and cost a life but
    /// don't skip the word — the same bubble set stays up. Pure uGUI (like
    /// WordBlasterGame3D), one persistent pool of bubble buttons reused
    /// across words, driven entirely from Update() (no coroutines) so pause
    /// behaves exactly like the rest of the game.
    /// </summary>
    public class BubbleGameUI : MonoBehaviour
    {
        [System.NonSerialized] public LingoRunBootstrap bootstrap;

        const int MaxBubbles = 16;
        const int MaxWordLen = 9;
        const int Lives = 3;
        const int RoundSize = 10;      // words per round
        const float BubbleAlpha = 0.55f;
        const float SideMargin = 20f;
        const float BubbleAreaTop = 250f;
        const float BubbleAreaBottom = GameConfig.RefHeight - 160f;

        // Per-language article prefixes to strip before spelling (mirrors the
        // web version's ARTICLE_PREFIXES) and a plain-Latin decoy alphabet per
        // language (kept accent-free — decoy letters don't need to be exhaustive,
        // just plausible, and this avoids relying on obscure glyphs rendering).
        static readonly Dictionary<string, string[]> ArticlePrefixesByLang = new Dictionary<string, string[]>
        {
            { "es", new[] { "el ", "la ", "los ", "las ", "un ", "una ", "unos ", "unas " } },
            { "fr", new[] { "le ", "la ", "les ", "l'", "un ", "une ", "des " } },
            { "it", new[] { "il ", "la ", "lo ", "gli ", "le ", "i ", "l'", "un ", "una ", "un'", "uno " } },
            { "de", new[] { "der ", "die ", "das ", "den ", "dem ", "des ",
                             "ein ", "eine ", "einen ", "einer ", "einem ", "eines " } },
            { "pt", new[] { "o ", "a ", "os ", "as ", "um ", "uma " } },
            { "tr", new string[0] },
            { "ru", new string[0] },
        };
        static readonly Dictionary<string, char[]> AlphabetByLang = new Dictionary<string, char[]>
        {
            { "es", "ABCDEFGHIJKLMNÑOPQRSTUVWXYZ".ToCharArray() },
            { "fr", "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray() },
            { "it", "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray() },
            { "de", "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray() },
            { "pt", "ABCDEFGHIJLMNOPQRSTUVXZÇÃÕÉ".ToCharArray() },
            { "tr", "ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ".ToCharArray() },
            { "ru", "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЫЬЭЮЯ".ToCharArray() },
            // ja / ko / zh intentionally absent: letter-spelling doesn't
            // apply to those scripts — see GameConfig.SupportsSpelling,
            // which swaps their Bubbles map slots to Memory Match.
        };

        static string[] CurrentArticlePrefixes()
        {
            string[] list;
            if (ArticlePrefixesByLang.TryGetValue(LanguageManager.Current, out list)) return list;
            return ArticlePrefixesByLang["es"];
        }

        static char[] CurrentAlphabet()
        {
            char[] list;
            if (AlphabetByLang.TryGetValue(LanguageManager.Current, out list)) return list;
            return AlphabetByLang["es"];
        }
        static readonly Color[] BubblePalette =
        {
            Theme.Hex("B08CFF", BubbleAlpha),
            Theme.Hex("FF8FC7", BubbleAlpha),
            Theme.Hex("35D0F0", BubbleAlpha),
            Theme.Hex("7CE577", BubbleAlpha),
            Theme.Hex("FFC94D", BubbleAlpha),
        };
        static readonly Color LetterFilled = Theme.Hex("2DD4BF", 0.6f);
        static readonly Color LetterEmpty = new Color(1f, 1f, 1f, 0.5f);

        class BubbleSlot
        {
            public GameObject go;
            public RectTransform rt;
            public Image bg;
            public Text label;
            public Button button;
            public char letter;
            public bool active;
            public bool popped;
            public float wobbleSeed;
            public Vector2 basePos;
            public Color baseColor;
            public float wrongT;
        }

        // ---------- state ----------
        bool _built;
        bool _running;
        bool _userPaused;
        List<WordEntry> _pool;
        int _poolIndex;
        bool _reviewMode;
        WordEntry _current;
        string _targetLetters;   // stripped-of-article, uppercased letters to spell
        int _typedIndex;
        bool _hintUsed;
        float _wordTimeBudget;
        float _wordTimeLeft;
        int _score, _lives, _streak, _bestStreak, _challengeCount, _wordsInRound;
        readonly HashSet<string> _sessionMissed = new HashSet<string>();
        readonly HashSet<string> _sessionLearned = new HashSet<string>();

        float _pendingAdvanceT; // >0 while showing the word-complete flash before the next word
        float _pendingEndT;     // >0 while showing the final reveal before ending the run

        readonly BubbleSlot[] _bubbles = new BubbleSlot[MaxBubbles];

        // ---------- HUD ----------
        Text _promptText;
        Text _scoreText;
        Text _streakText;
        readonly Image[] _hearts = new Image[Lives];
        readonly Image[] _slotBg = new Image[MaxWordLen];
        readonly Text[] _slotChar = new Text[MaxWordLen];
        readonly RectTransform[] _slotRt = new RectTransform[MaxWordLen];
        Transform _bubbleLayer;
        RectTransform _timerFillRt;
        Image _timerFillImg;
        Button _hintButton;
        Text _hintLabel;
        RectTransform _pauseOverlay;
        Text _muteLabel;
        RectTransform _completeOverlay;
        Text _completeText;
        RectTransform _revealOverlay;
        Text _revealTitle;
        Text _revealTgt;
        Text _revealEn;

        // ==================== lifecycle ====================

        public void BeginRun(bool review)
        {
            EnsureBuilt();
            gameObject.SetActive(true);

            _reviewMode = review;
            _score = 0; _lives = Lives; _streak = 0; _bestStreak = 0;
            _challengeCount = 0; _wordsInRound = 0;
            _pendingAdvanceT = 0f; _pendingEndT = 0f;
            _userPaused = false; _running = true;
            _sessionMissed.Clear();
            _sessionLearned.Clear();
            _current = null;

            BuildPool();
            _pauseOverlay.gameObject.SetActive(false);
            _completeOverlay.gameObject.SetActive(false);
            _revealOverlay.gameObject.SetActive(false);
            HideAllBubbles();

            UpdateHud();
            if (_pool == null || _pool.Count == 0) { EndRun(); return; }
            StartWord();
        }

        static void BubbleParams(Difficulty d, out float buffer, out float perLetter, out int distractors)
        {
            switch (d)
            {
                case Difficulty.Fast: buffer = 2.5f; perLetter = 1.1f; distractors = 6; break;
                case Difficulty.Normal: buffer = 4f; perLetter = 1.6f; distractors = 4; break;
                default: buffer = 6f; perLetter = 2.2f; distractors = 3; break;
            }
        }

        static string StripArticle(string tgt)
        {
            if (string.IsNullOrEmpty(tgt)) return tgt;
            string lower = tgt.ToLowerInvariant();
            foreach (var prefix in CurrentArticlePrefixes())
            {
                if (lower.StartsWith(prefix)) return tgt.Substring(prefix.Length);
            }
            return tgt;
        }

        static bool IsSpellable(WordEntry w)
        {
            if (w == null || string.IsNullOrEmpty(w.tgt)) return false;
            string s = StripArticle(w.tgt).Trim();
            if (s.Length < 2 || s.Length > MaxWordLen) return false;
            if (s.IndexOf(' ') >= 0) return false;
            if (s.IndexOf('\'') >= 0) return false;
            return true;
        }

        void BuildPool()
        {
            var missed = ProgressManager.MissedKeysFor(false);
            List<WordEntry> raw;
            if (_reviewMode && missed.Count > 0)
            {
                var reviewWords = new List<WordEntry>();
                foreach (var en in missed)
                {
                    var w = VocabDatabase.FindByEn(en);
                    if (w != null) reviewWords.Add(w);
                }
                raw = reviewWords.Count > 0 ? reviewWords : VocabDatabase.BuildPool(ProfileManager.ActiveTopics, ProgressManager.A2Unlocked);
            }
            else
            {
                // Themed-unit slice (same scheme as Memory Match): this
                // level's words come from the unit's topic curriculum at the
                // Bubbles slot's slice — walking forward past unspellable
                // words (too long / accented) until a full round is collected,
                // so the round stays on-theme with spellable padding.
                int level = ProgressManager.CurrentLevel;
                string topic = GameConfig.UnitTopicForLevel(level);
                var curriculum = VocabDatabase.BuildCurriculum(
                    new List<string> { topic }, ProgressManager.A2Unlocked);
                raw = new List<WordEntry>();
                if (curriculum.Count > 0)
                {
                    int start = (GameConfig.PriorSlicesInTopic(level, GameMode.Bubbles) * RoundSize)
                        % curriculum.Count;
                    for (int i = 0; i < curriculum.Count && raw.Count < RoundSize; i++)
                    {
                        var w = curriculum[(start + i) % curriculum.Count];
                        if (IsSpellable(w)) raw.Add(w);
                    }
                }
            }

            _pool = new List<WordEntry>();
            foreach (var w in raw)
            {
                if (IsSpellable(w)) _pool.Add(w);
            }
            if (_pool.Count == 0)
            {
                foreach (var w in VocabDatabase.BuildPool(null, true))
                {
                    if (IsSpellable(w)) _pool.Add(w);
                }
            }
            for (int i = _pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = _pool[i]; _pool[i] = _pool[j]; _pool[j] = tmp;
            }
            _poolIndex = 0;
        }

        void StartWord()
        {
            if (_pool == null || _pool.Count == 0) { EndRun(); return; }
            if (_poolIndex >= _pool.Count)
            {
                _poolIndex = 0;
                for (int i = _pool.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    var tmp = _pool[i]; _pool[i] = _pool[j]; _pool[j] = tmp;
                }
            }
            _current = _pool[_poolIndex];
            _poolIndex++;

            // Culture-aware uppercasing: Turkish i→İ and ı→I are DIFFERENT
            // letters — invariant casing would corrupt the spelling target.
            _targetLetters = LanguageManager.Current == "tr"
                ? StripArticle(_current.tgt).ToUpper(new System.Globalization.CultureInfo("tr-TR"))
                : StripArticle(_current.tgt).ToUpperInvariant();
            _typedIndex = 0;
            _hintUsed = false;
            _hintButton.interactable = true;
            _hintLabel.text = "HINT";

            float buffer, perLetter; int distractors;
            BubbleParams(bootstrap.CurrentDifficulty, out buffer, out perLetter, out distractors);
            _wordTimeBudget = buffer + perLetter * _targetLetters.Length;
            _wordTimeLeft = _wordTimeBudget;

            _promptText.text = _current.en;
            RefreshLetterSlots();
            SpawnBubbles(distractors);
            UpdateHud();
            UpdateTimerBar();
        }

        void RefreshLetterSlots()
        {
            int n = _targetLetters.Length;
            const float boxSize = 28f;
            const float gap = 6f;
            float totalW = n * boxSize + Mathf.Max(0, n - 1) * gap;
            float startX = GameConfig.RefWidth * 0.5f - totalW * 0.5f;
            for (int i = 0; i < MaxWordLen; i++)
            {
                bool vis = i < n;
                _slotBg[i].gameObject.SetActive(vis);
                if (!vis) continue;
                float cx = startX + i * (boxSize + gap) + boxSize * 0.5f;
                UIFactory.PlaceTop(_slotRt[i], cx, 182f, boxSize, boxSize);
                bool filled = i < _typedIndex;
                _slotChar[i].text = filled ? _targetLetters[i].ToString() : "";
                _slotBg[i].color = filled ? LetterFilled : LetterEmpty;
            }
        }

        static char RandomDistractorLetter(string exclude)
        {
            char[] alphabet = CurrentAlphabet();
            for (int attempt = 0; attempt < 12; attempt++)
            {
                char c = alphabet[Random.Range(0, alphabet.Length)];
                if (exclude.IndexOf(c) < 0) return c;
            }
            return alphabet[Random.Range(0, alphabet.Length)];
        }

        void SpawnBubbles(int distractorCount)
        {
            HideAllBubbles();
            string letters = _targetLetters;
            int n = letters.Length;
            var chars = new List<char>(n + distractorCount);
            for (int i = 0; i < n; i++) chars.Add(letters[i]);
            for (int i = 0; i < distractorCount; i++) chars.Add(RandomDistractorLetter(letters));
            int count = chars.Count;

            float areaW = GameConfig.RefWidth - SideMargin * 2f;
            float areaH = BubbleAreaBottom - BubbleAreaTop;
            float aspect = areaW / areaH;
            int cols = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(count * aspect)), 3, 6);
            int rows = Mathf.CeilToInt((float)count / cols);
            float cellW = areaW / cols;
            float cellH = areaH / rows;
            float bubbleSize = Mathf.Clamp(Mathf.Min(cellW, cellH) * 0.78f, 40f, 62f);

            int cellCount = cols * rows;
            var cellIndices = new List<int>(cellCount);
            for (int i = 0; i < cellCount; i++) cellIndices.Add(i);
            for (int i = cellIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = cellIndices[i]; cellIndices[i] = cellIndices[j]; cellIndices[j] = tmp;
            }

            for (int i = 0; i < count; i++)
            {
                var slot = _bubbles[i];
                int cell = cellIndices[i];
                int cx = cell % cols;
                int cy = cell / cols;
                float jitterX = (Random.value - 0.5f) * cellW * 0.3f;
                float jitterY = (Random.value - 0.5f) * cellH * 0.3f;
                float px = SideMargin + cellW * (cx + 0.5f) + jitterX;
                float py = BubbleAreaTop + cellH * (cy + 0.5f) + jitterY;

                slot.letter = chars[i];
                slot.active = true;
                slot.popped = false;
                slot.wrongT = 0f;
                slot.wobbleSeed = Random.value * 100f;

                UIFactory.PlaceTop(slot.rt, px, py, bubbleSize, bubbleSize);
                slot.basePos = slot.rt.anchoredPosition;
                Color col = BubblePalette[Random.Range(0, BubblePalette.Length)];
                slot.baseColor = col;
                slot.bg.color = col;
                slot.label.text = slot.letter.ToString();
                slot.go.SetActive(true);
            }
        }

        void HideAllBubbles()
        {
            for (int i = 0; i < MaxBubbles; i++)
            {
                var slot = _bubbles[i];
                if (slot == null) continue;
                slot.active = false;
                slot.popped = false;
                slot.wrongT = 0f;
                slot.go.SetActive(false);
            }
        }

        void Update()
        {
            if (!_running || _userPaused) return;

            if (_pendingEndT > 0f)
            {
                _pendingEndT -= Time.deltaTime;
                if (_pendingEndT <= 0f) EndRun();
                return;
            }

            if (_pendingAdvanceT > 0f)
            {
                _pendingAdvanceT -= Time.deltaTime;
                if (_pendingAdvanceT <= 0f)
                {
                    _completeOverlay.gameObject.SetActive(false);
                    _revealOverlay.gameObject.SetActive(false);
                    if (_wordsInRound >= RoundSize) EndRun();
                    else StartWord();
                }
                return;
            }

            _wordTimeLeft -= Time.deltaTime;
            UpdateTimerBar();
            if (_wordTimeLeft <= 0f)
            {
                HandleTimeout();
                return;
            }

            for (int i = 0; i < MaxBubbles; i++)
            {
                var slot = _bubbles[i];
                if (!slot.active) continue;
                float tt = Time.time + slot.wobbleSeed;
                Vector2 offset = new Vector2(Mathf.Sin(tt * 1.1f) * 5f, Mathf.Cos(tt * 0.9f) * 5f);
                slot.rt.anchoredPosition = slot.basePos + offset;
                if (slot.wrongT > 0f)
                {
                    slot.wrongT -= Time.deltaTime;
                    slot.bg.color = Color.Lerp(slot.baseColor, Theme.BadRed, Mathf.Clamp01(slot.wrongT / 0.3f));
                }
            }
        }

        void OnBubbleTap(int i)
        {
            if (!_running || _userPaused) return;
            if (_pendingAdvanceT > 0f || _pendingEndT > 0f) return;
            var slot = _bubbles[i];
            if (!slot.active || slot.popped) return;

            char needed = _targetLetters[_typedIndex];
            if (slot.letter == needed)
            {
                slot.popped = true;
                slot.active = false;
                slot.go.SetActive(false);
                ProceduralAudio.PlayCorrect();
                _score += 5;
                _typedIndex++;
                RefreshLetterSlots();
                if (_typedIndex >= _targetLetters.Length) CompleteWord();
            }
            else
            {
                ProceduralAudio.PlayWrong();
                slot.wrongT = 0.3f;
                _streak = 0;
                _lives = Mathf.Max(0, _lives - 1);
                _sessionMissed.Add(_current.en);
                UpdateHud();
                if (_lives <= 0)
                {
                    HideAllBubbles();
                    ShowReveal("OUT OF LIVES");
                    _pendingEndT = 1.6f;
                }
            }
        }

        void UseHint()
        {
            if (!_running || _userPaused || _hintUsed) return;
            if (_pendingAdvanceT > 0f || _pendingEndT > 0f) return;
            if (_typedIndex >= _targetLetters.Length) return;
            _hintUsed = true;
            _hintButton.interactable = false;
            _hintLabel.text = "HINT: " + _targetLetters[_typedIndex];
        }

        void SkipWord()
        {
            if (!_running || _userPaused) return;
            if (_pendingAdvanceT > 0f || _pendingEndT > 0f) return;
            ProceduralAudio.PlayWrong();
            _lives = Mathf.Max(0, _lives - 1);
            _streak = 0;
            _sessionMissed.Add(_current.en);
            UpdateHud();
            HideAllBubbles();
            if (_lives <= 0)
            {
                ShowReveal("OUT OF LIVES");
                _pendingEndT = 1.6f;
            }
            else
            {
                ShowReveal("SKIPPED");
                _pendingAdvanceT = 1.6f;
            }
        }

        void HandleTimeout()
        {
            _lives = Mathf.Max(0, _lives - 1);
            _streak = 0;
            _sessionMissed.Add(_current.en);
            UpdateHud();
            HideAllBubbles();
            if (_lives <= 0)
            {
                ShowReveal("TIME'S UP");
                _pendingEndT = 1.6f;
            }
            else
            {
                ShowReveal("TIME'S UP");
                _pendingAdvanceT = 1.6f;
            }
        }

        void CompleteWord()
        {
            int letters = _targetLetters.Length;
            int bonus = 25 + letters * 3;
            _score += bonus;
            _streak++;
            _bestStreak = Mathf.Max(_bestStreak, _streak);
            _challengeCount++;
            _sessionLearned.Add(_current.en);
            _wordsInRound++;
            UpdateHud();
            HideAllBubbles();
            _completeOverlay.gameObject.SetActive(true);
            _completeText.text = "+" + bonus;
            _pendingAdvanceT = 1.1f;
        }

        void ShowReveal(string title)
        {
            _revealOverlay.gameObject.SetActive(true);
            _revealTitle.text = title;
            _revealTgt.text = _current.tgt;
            _revealEn.text = _current.en;
        }

        void EndRun()
        {
            _running = false;
            var results = ProgressManager.RecordRunEnd(
                _score, _bestStreak, _sessionMissed, _sessionLearned,
                _challengeCount, _reviewMode, false, false, false, bootstrap.ActiveMapLevel);
            results.isBubbles = true;
            bootstrap.ShowGameOver(results);
        }

        void UpdateHud()
        {
            _scoreText.text = _score.ToString();
            _streakText.text = "x" + _streak;
            for (int i = 0; i < Lives; i++)
                _hearts[i].color = i < _lives ? Color.white : new Color(1f, 1f, 1f, 0.22f);
        }

        void UpdateTimerBar()
        {
            float frac = _wordTimeBudget > 0f ? Mathf.Clamp01(_wordTimeLeft / _wordTimeBudget) : 0f;
            _timerFillRt.anchorMax = new Vector2(frac, 1f);
            _timerFillImg.color = frac < 0.25f ? Theme.BadRed : Theme.Good;
        }

        // ==================== pause ====================

        void TogglePause(bool paused)
        {
            _userPaused = paused;
            _pauseOverlay.gameObject.SetActive(paused);
        }

        void ExitToHome()
        {
            _running = false;
            TogglePause(false);
            bootstrap.ShowHome();
        }

        // ==================== construction ====================

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            Build();
        }

        void Build()
        {
            var t = transform;

            UIFactory.CreateVerticalGradient(t, "Sky", Theme.SkyTop, Theme.SkyBottom);

            var pauseBtn = UIFactory.CreateButton(t, "PauseBtn", "II", 17,
                Theme.CreamPanel, Theme.Ink, delegate { ProceduralAudio.PlayClick(); TogglePause(true); });
            UIFactory.PlaceTop(pauseBtn.GetComponent<RectTransform>(), 34f, 30f, 40f, 40f);

            for (int i = 0; i < Lives; i++)
            {
                var heart = UIFactory.CreateIcon(t, "Heart" + i, "heart", Theme.HeartRed);
                UIFactory.PlaceTop(heart.rectTransform, 74f + i * 24f, 30f, 16f, 16f);
                _hearts[i] = heart;
            }

            var scorePill = UIFactory.CreatePanel(t, "ScorePill", new Color(1f, 1f, 1f, 0.5f), 16f);
            UIFactory.PlaceTop(scorePill.rectTransform, 372f, 40f, 110f, 54f);
            _scoreText = UIFactory.CreateText(scorePill.transform, "Score", "0", 20, Theme.Ink);
            UIFactory.BandTop(_scoreText.rectTransform, 4f, 26f);
            _streakText = UIFactory.CreateText(scorePill.transform, "Streak", "x0", 14, Theme.Orange);
            UIFactory.BandTop(_streakText.rectTransform, 30f, 18f);

            var card = UIFactory.CreatePanel(t, "PromptCard", new Color(1f, 1f, 1f, 0.55f), 20f);
            UIFactory.BandTop(card.rectTransform, 96f, 70f, 40f);
            var caption = UIFactory.CreateText(card.transform, "Caption", "SPELL THE WORD", 14, Theme.InkSoft);
            UIFactory.BandTop(caption.rectTransform, 10f, 18f);
            _promptText = UIFactory.CreateText(card.transform, "Prompt", "-", 24, Theme.Ink);
            _promptText.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_promptText.rectTransform, 32f, 32f);

            for (int i = 0; i < MaxWordLen; i++)
            {
                var slot = UIFactory.CreatePanel(t, "LetterSlot" + i, LetterEmpty, 6f);
                _slotBg[i] = slot;
                _slotRt[i] = slot.rectTransform;
                var txt = UIFactory.CreateText(slot.transform, "Char", "", 17, Theme.Ink);
                _slotChar[i] = txt;
                slot.gameObject.SetActive(false);
            }

            var timerTrack = UIFactory.CreatePanel(t, "TimerTrack", new Color(1f, 1f, 1f, 0.35f), 6f);
            UIFactory.BandTop(timerTrack.rectTransform, 222f, 10f, 50f);
            var fillGo = new GameObject("TimerFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(timerTrack.transform, false);
            _timerFillImg = fillGo.GetComponent<Image>();
            UIFactory.ApplyRounded(_timerFillImg, 6f);
            _timerFillImg.color = Theme.Good;
            _timerFillImg.raycastTarget = false;
            _timerFillRt = fillGo.GetComponent<RectTransform>();
            _timerFillRt.anchorMin = new Vector2(0f, 0f);
            _timerFillRt.anchorMax = new Vector2(1f, 1f);
            _timerFillRt.offsetMin = Vector2.zero;
            _timerFillRt.offsetMax = Vector2.zero;

            // ----- bubble layer: a fixed pool of MaxBubbles buttons, reused/repositioned per word -----
            var bubbleLayerRt = UIFactory.CreateRoot(t, "BubbleLayer");
            _bubbleLayer = bubbleLayerRt;
            for (int i = 0; i < MaxBubbles; i++)
            {
                var go = new GameObject("Bubble" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_bubbleLayer, false);
                var img = go.GetComponent<Image>();
                img.sprite = UIFactory.CircleSprite;
                img.raycastTarget = true;
                UIFactory.AddSoftShadow(go, 2f, 0.2f);
                var rt = go.GetComponent<RectTransform>();
                var label = UIFactory.CreateText(go.transform, "Label", "", 20, Color.white);
                label.raycastTarget = false;
                var btn = go.GetComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(delegate { OnBubbleTap(idx); });
                go.SetActive(false);

                _bubbles[i] = new BubbleSlot { go = go, rt = rt, bg = img, label = label, button = btn };
            }

            // ----- hint / skip -----
            var hint = UIFactory.CreateButton(t, "HintBtn", "HINT", 17,
                Theme.CreamPanel, Theme.Ink, delegate { ProceduralAudio.PlayClick(); UseHint(); });
            UIFactory.PlaceTop(hint.GetComponent<RectTransform>(), 115f, GameConfig.RefHeight - 95f, 170f, 52f);
            _hintButton = hint;
            _hintLabel = hint.GetComponentInChildren<Text>();

            var skip = UIFactory.CreateButton(t, "SkipBtn", "SKIP", 17,
                Theme.Orange, Theme.Cream, delegate { ProceduralAudio.PlayClick(); SkipWord(); });
            UIFactory.PlaceTop(skip.GetComponent<RectTransform>(), 325f, GameConfig.RefHeight - 95f, 170f, 52f);

            // ----- word-complete flash -----
            var co = UIFactory.CreatePanel(t, "CompleteOverlay", new Color(0f, 0f, 0f, 0f), 0f);
            co.raycastTarget = false;
            _completeOverlay = co.rectTransform;
            _completeText = UIFactory.CreateText(co.transform, "CompleteText", "", 26, Theme.GoodDark);
            UIFactory.BandTop(_completeText.rectTransform, 340f, 40f);
            _completeOverlay.gameObject.SetActive(false);

            // ----- reveal card (timeout / skip / out of lives) -----
            var ro = UIFactory.CreatePanel(t, "RevealOverlay", new Color(0.98f, 0.97f, 1f, 0.95f), 20f);
            UIFactory.BandTop(ro.rectTransform, 260f, 200f, 50f);
            ro.raycastTarget = true;
            _revealOverlay = ro.rectTransform;
            _revealTitle = UIFactory.CreateText(ro.transform, "Title", "", 17, Theme.BadRed);
            UIFactory.BandTop(_revealTitle.rectTransform, 16f, 22f);
            _revealTgt = UIFactory.CreateText(ro.transform, "Tgt", "", 30, Theme.Ink);
            _revealTgt.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_revealTgt.rectTransform, 60f, 40f);
            _revealEn = UIFactory.CreateText(ro.transform, "En", "", 17, Theme.InkSoft);
            UIFactory.BandTop(_revealEn.rectTransform, 108f, 24f);
            _revealOverlay.gameObject.SetActive(false);

            BuildPauseOverlay();
        }

        void BuildPauseOverlay()
        {
            var t = transform;
            var overlay = UIFactory.CreatePanel(t, "PauseOverlay", new Color(0.98f, 0.97f, 1f, 0.97f), 0f);
            overlay.raycastTarget = true;
            _pauseOverlay = overlay.rectTransform;

            var caption = UIFactory.CreateText(overlay.transform, "Caption", "GAME PAUSED", 14, Theme.InkSoft);
            UIFactory.BandTop(caption.rectTransform, 200f, 24f);

            var title = UIFactory.CreateText(overlay.transform, "Title", "PAUSED", 40, Theme.Ink);
            UIFactory.BandTop(title.rectTransform, 230f, 60f);

            var resume = UIFactory.CreateButton(overlay.transform, "Resume", "RESUME", 20,
                Theme.Orange, Theme.Cream, delegate { ProceduralAudio.PlayClick(); TogglePause(false); });
            UIFactory.BandTop(resume.GetComponent<RectTransform>(), 330f, 56f, 80f);

            var mute = UIFactory.CreateButton(overlay.transform, "Mute", "SOUND: ON", 16,
                Theme.CreamPanel, Theme.Ink, null);
            UIFactory.BandTop(mute.GetComponent<RectTransform>(), 400f, 48f, 80f);
            _muteLabel = mute.GetComponentInChildren<Text>();
            mute.onClick.AddListener(delegate
            {
                ProceduralAudio.Muted = !ProceduralAudio.Muted;
                _muteLabel.text = ProceduralAudio.Muted ? "SOUND: OFF" : "SOUND: ON";
                ProceduralAudio.PlayClick();
            });

            var exit = UIFactory.CreateButton(overlay.transform, "Exit", "EXIT TO HOME", 16,
                Theme.CreamPanel, Theme.Ink, delegate { ProceduralAudio.PlayClick(); ExitToHome(); });
            UIFactory.BandTop(exit.GetComponent<RectTransform>(), 460f, 48f, 80f);

            var note = UIFactory.CreateText(overlay.transform, "Note",
                "Exiting ends this run without saving the score.", 14, Theme.InkSoft);
            UIFactory.BandTop(note.rectTransform, 530f, 22f);

            _pauseOverlay.gameObject.SetActive(false);
        }
    }
}
