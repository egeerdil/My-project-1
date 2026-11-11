using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Memory Match: a 4x4 grid of face-down cards — 8 target-language word
    /// cards and their 8 English meaning cards. Flip two; a word+meaning pair
    /// stays revealed, a mismatch flips back. Familiar casual mechanic that
    /// doubles as vocabulary drill, and integrates with the dictionary the
    /// same way the runner does: matched pairs count as correct answers
    /// (mastery ticks up), pairs missed after being seen go to the review
    /// pool. Replaces Word Blaster on the map/home (map slot 4 of each unit).
    /// Words come from the current level's unit topic; review mode deals the
    /// review pool instead. One "challenge" = one two-card flip attempt, so
    /// the map node's pass accuracy = matches / attempts.
    /// </summary>
    public class MemoryMatchGameUI : MonoBehaviour
    {
        public LingoRunBootstrap bootstrap;

        const int Pairs = 8;
        const int Columns = 4;
        const float FlipBackDelay = 1.4f;    // mismatch stays visible long enough to study
        const float MemorizeSeconds = 4f;    // face-up preview before the round starts

        class Card
        {
            public WordEntry word;
            public bool isWordSide;   // true = shows tgt, false = shows en
            public bool matched;
            public Button button;
            public Image bg;
            public Text label;
            public GameObject back;   // the "?" cover shown while face-down
        }

        RectTransform _root;
        Text _scoreText, _streakText, _pairsText;
        Text _memorizeText;
        readonly List<Card> _cards = new List<Card>();
        Card _first;
        bool _busy;               // waiting for a mismatch to flip back
        bool _running;
        bool _reviewMode;

        int _score, _streak, _bestStreak;
        int _attempts, _matches;
        readonly HashSet<string> _sessionMissed = new HashSet<string>();
        readonly HashSet<string> _sessionLearned = new HashSet<string>();
        // Word cards the player has already seen face-up at least once —
        // only a mismatch on a SEEN pair is a real memory/vocab miss.
        readonly HashSet<string> _seen = new HashSet<string>();

        bool _built;

        void OnEnable()
        {
            if (!_built) { Build(); _built = true; }
        }

        void Build()
        {
            _root = UIFactory.CreateRoot(transform, "MatchUI");
            var t = _root.transform;

            UIFactory.CreateVerticalGradient(t, "Bg", Theme.Hex("6B4FB8"), Theme.Hex("A66BD8"));

            var quit = UIFactory.CreateButton(t, "Quit", "<", 19,
                new Color(1f, 1f, 1f, 0.25f), Color.white,
                delegate { ProceduralAudio.PlayClick(); EndRun(); });
            UIFactory.PlaceTop(quit.GetComponent<RectTransform>(), 40f, 40f, 44f, 44f);

            var title = UIFactory.CreateText(t, "Title", "MEMORY MATCH", 20, Color.white);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 28f, 28f);

            _scoreText = UIFactory.CreateText(t, "Score", "0", 24, Color.white);
            _scoreText.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_scoreText.rectTransform, 58f, 30f);

            _streakText = UIFactory.CreateText(t, "Streak", "x0", 15, Theme.Hex("FFE8A3"));
            UIFactory.PlaceTop(_streakText.rectTransform, 396f, 40f, 70f, 24f);

            _pairsText = UIFactory.CreateText(t, "Pairs", "", 14, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.BandTop(_pairsText.rectTransform, 92f, 20f);

            // "MEMORIZE!" banner shown while all cards are face-up.
            _memorizeText = UIFactory.CreateText(t, "Memorize", Loc.T("match.memorize"), 22, Theme.Hex("FFE8A3"));
            _memorizeText.font = UIFactory.DisplayFont;
            UIFactory.AddSoftShadow(_memorizeText.gameObject, 2f, 0.35f);
            UIFactory.BandTop(_memorizeText.rectTransform, 92f, 26f);
            _memorizeText.gameObject.SetActive(false);

            // 4x4 card grid.
            const float side = 24f;
            const float gap = 10f;
            const float gridTop = 128f;
            float cardW = (GameConfig.RefWidth - side * 2f - gap * (Columns - 1)) / Columns;
            float cardH = 118f;

            for (int i = 0; i < Pairs * 2; i++)
            {
                int col = i % Columns, row = i / Columns;

                var btn = UIFactory.CreateButton(t, "Card" + i, "", 15,
                    Color.white, Theme.Ink, null);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cardW, cardH);
                rt.anchoredPosition = new Vector2(side + col * (cardW + gap), -(gridTop + row * (cardH + gap)));

                var card = new Card
                {
                    button = btn,
                    bg = btn.GetComponent<Image>(),
                    label = btn.GetComponentInChildren<Text>(),
                };
                card.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                // Long words (German compounds!) shrink instead of clipping.
                card.label.resizeTextForBestFit = true;
                card.label.resizeTextMinSize = 9;
                card.label.resizeTextMaxSize = 14;
                card.label.rectTransform.offsetMin = new Vector2(4f, 4f);
                card.label.rectTransform.offsetMax = new Vector2(-4f, -4f);

                // Face-down cover.
                var back = UIFactory.CreatePanel(btn.transform, "Back", Theme.PurpleDeep, 14f);
                back.raycastTarget = false;
                var brt = back.rectTransform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                var q = UIFactory.CreateText(back.transform, "Q", "?", 26, new Color(1f, 1f, 1f, 0.85f));
                q.font = UIFactory.DisplayFont;
                q.raycastTarget = false;
                card.back = back.gameObject;

                int idx = i;
                btn.onClick.AddListener(delegate { OnCardTap(idx); });
                _cards.Add(card);
            }
        }

        public void BeginRun(bool review)
        {
            if (!_built) { Build(); _built = true; }
            _reviewMode = review;
            _running = true;
            _busy = false;
            _first = null;
            _score = 0; _streak = 0; _bestStreak = 0;
            _attempts = 0; _matches = 0;
            _sessionMissed.Clear();
            _sessionLearned.Clear();
            _seen.Clear();

            DealCards(PickWords(review));
            UpdateHud();
            // Fair start: show every card face-up for a few seconds first.
            StartCoroutine(MemorizePhase());
        }

        /// <summary>All cards revealed, input blocked, "MEMORIZE!" banner —
        /// then everything flips face-down and the round begins.</summary>
        IEnumerator MemorizePhase()
        {
            _busy = true;
            _memorizeText.gameObject.SetActive(true);
            _pairsText.gameObject.SetActive(false);
            foreach (var c in _cards)
                if (c.word != null) c.back.SetActive(false);

            yield return new WaitForSeconds(MemorizeSeconds);

            foreach (var c in _cards)
                if (c.word != null && !c.matched) c.back.SetActive(true);
            _memorizeText.gameObject.SetActive(false);
            _pairsText.gameObject.SetActive(true);
            _busy = false;
        }

        List<WordEntry> PickWords(bool review)
        {
            var picked = new List<WordEntry>();
            var used = new HashSet<string>();

            if (review)
            {
                foreach (var en in ProgressManager.MissedKeysFor(false))
                {
                    var w = VocabDatabase.FindByEn(en);
                    if (w != null && used.Add(w.en)) picked.Add(w);
                    if (picked.Count >= Pairs) break;
                }
            }

            // Level deal (and top-up when the review pool is short): the
            // current unit topic's curriculum, at this level's Match slice.
            int level = ProgressManager.CurrentLevel;
            string topic = GameConfig.UnitTopicForLevel(level);
            var pool = VocabDatabase.BuildCurriculum(
                new List<string> { topic }, ProgressManager.A2Unlocked);
            if (pool.Count > 0)
            {
                int start = (GameConfig.PriorSlicesInTopic(level, GameMode.Match) * Pairs) % pool.Count;
                for (int i = 0; picked.Count < Pairs && i < pool.Count; i++)
                {
                    var w = pool[(start + i) % pool.Count];
                    if (used.Add(w.en)) picked.Add(w);
                }
            }
            return picked;
        }

        void DealCards(List<WordEntry> words)
        {
            // Two cards per word (tgt side + en side), shuffled into the grid.
            var deal = new List<KeyValuePair<WordEntry, bool>>();
            foreach (var w in words)
            {
                deal.Add(new KeyValuePair<WordEntry, bool>(w, true));
                deal.Add(new KeyValuePair<WordEntry, bool>(w, false));
            }
            for (int i = deal.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = deal[i]; deal[i] = deal[j]; deal[j] = tmp;
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (i < deal.Count)
                {
                    card.word = deal[i].Key;
                    card.isWordSide = deal[i].Value;
                    card.matched = false;
                    card.label.text = card.isWordSide ? card.word.tgt : card.word.en;
                    card.label.color = card.isWordSide ? Theme.Purple : Theme.Ink;
                    card.bg.color = Color.white;
                    card.back.SetActive(true);
                    card.button.gameObject.SetActive(true);
                    card.button.interactable = true;
                }
                else
                {
                    card.word = null;
                    card.button.gameObject.SetActive(false);
                }
            }
        }

        void OnCardTap(int idx)
        {
            if (!_running || _busy) return;
            var card = _cards[idx];
            if (card.word == null || card.matched || card == _first) return;

            ProceduralAudio.PlayClick();
            Reveal(card);

            if (_first == null) { _first = card; return; }

            // Second card of the attempt.
            var a = _first;
            _first = null;
            _attempts++;

            if (a.word.en == card.word.en)
            {
                _matches++;
                _streak++;
                _bestStreak = Mathf.Max(_bestStreak, _streak);
                _score += 10 + 2 * (_streak - 1);
                a.matched = card.matched = true;
                a.button.interactable = card.button.interactable = false;
                a.bg.color = card.bg.color =
                    new Color(Theme.Teal.r, Theme.Teal.g, Theme.Teal.b, 0.35f);
                _sessionLearned.Add(a.word.en);
                ProceduralAudio.PlayCorrect();
                NativeSpeech.Speak(a.word.tgt); // reinforce the pair you just made
                UpdateHud();
                if (_matches >= CountDealtPairs()) StartCoroutine(FinishSoon());
            }
            else
            {
                _streak = 0;
                // Only a real memory error counts as a miss: at least one of
                // the two words had already been revealed before this attempt.
                if (_seen.Contains(a.word.en)) _sessionMissed.Add(a.word.en);
                if (_seen.Contains(card.word.en)) _sessionMissed.Add(card.word.en);
                ProceduralAudio.PlayWrong();
                UpdateHud();
                StartCoroutine(FlipBack(a, card));
            }

            _seen.Add(a.word.en);
            _seen.Add(card.word.en);
        }

        int CountDealtPairs()
        {
            int n = 0;
            foreach (var c in _cards) if (c.word != null && c.isWordSide) n++;
            return n;
        }

        void Reveal(Card card) { card.back.SetActive(false); }

        IEnumerator FlipBack(Card a, Card b)
        {
            _busy = true;
            yield return new WaitForSeconds(FlipBackDelay);
            if (!a.matched) a.back.SetActive(true);
            if (!b.matched) b.back.SetActive(true);
            _busy = false;
        }

        IEnumerator FinishSoon()
        {
            _busy = true;
            yield return new WaitForSeconds(0.7f);
            _busy = false;
            EndRun();
        }

        void UpdateHud()
        {
            _scoreText.text = _score.ToString();
            _streakText.text = "x" + _streak;
            _pairsText.text = _matches + " / " + CountDealtPairs() + " PAIRS"
                + (_reviewMode ? "   ·   REVIEW" : "");
        }

        void EndRun()
        {
            if (!_running) return;
            _running = false;
            StopAllCoroutines();
            var results = ProgressManager.RecordRunEnd(
                _score, _bestStreak, _sessionMissed, _sessionLearned,
                Mathf.Max(_attempts, 1), _reviewMode, false, false, false,
                bootstrap.ActiveMapLevel);
            results.isMatch = true;
            bootstrap.ShowGameOver(results);
        }
    }
}
