using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Pronunciation practice ("SPEAK"): the current level's words, one big
    /// card at a time — hear the word (TTS), tap the mic, say it, and the
    /// native speech recognizer checks you (see NativeSpeech /
    /// LingoRunSpeech.mm). Two tries per word; a pass counts as a correct
    /// answer for mastery, a fail speaks the word again and sends it to the
    /// review pool. Free to play (no hearts — speaking practice should never
    /// be gated) and in review mode it drills the review pool instead.
    /// Accuracy check is deliberately forgiving: accent-insensitive,
    /// article-optional, and accepts the target anywhere in the transcript.
    /// </summary>
    public class PronunciationGameUI : MonoBehaviour
    {
        public LingoRunBootstrap bootstrap;

        const int WordsPerSession = 8;
        const int TriesPerWord = 2;
        const float ListenTimeout = 5f;

        RectTransform _root;
        Text _progressText, _scoreText;
        Text _wordText, _meaningText;
        Text _heardText, _feedbackText;
        Button _micButton;
        Image _micBg;
        Text _micLabel;
        RectTransform _authOverlay;

        List<WordEntry> _words;
        int _index;
        int _tries;
        bool _listening;
        bool _advancing;
        bool _running;
        bool _reviewMode;
        string _lastHeard;

        int _score, _streak, _bestStreak;
        readonly HashSet<string> _sessionMissed = new HashSet<string>();
        readonly HashSet<string> _sessionLearned = new HashSet<string>();

        bool _built;

        void OnEnable() { if (!_built) { Build(); _built = true; } }

        void Build()
        {
            _root = UIFactory.CreateRoot(transform, "SpeakUI");
            var t = _root.transform;

            UIFactory.CreateVerticalGradient(t, "Bg", Theme.Hex("1F8A83"), Theme.Hex("5FC4B8"));

            var quit = UIFactory.CreateButton(t, "Quit", "<", 18,
                new Color(1f, 1f, 1f, 0.25f), Color.white,
                delegate { ProceduralAudio.PlayClick(); EndRun(); });
            UIFactory.PlaceTop(quit.GetComponent<RectTransform>(), 40f, 40f, 44f, 44f);

            var title = UIFactory.CreateText(t, "Title", "SPEAK IT", 20, Color.white);
            title.font = UIFactory.DisplayFont;
            UIFactory.BandTop(title.rectTransform, 28f, 28f);

            _scoreText = UIFactory.CreateText(t, "Score", "0", 22, Color.white);
            _scoreText.font = UIFactory.DisplayFont;
            UIFactory.PlaceTop(_scoreText.rectTransform, 396f, 40f, 70f, 30f);

            _progressText = UIFactory.CreateText(t, "Progress", "", 12, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.BandTop(_progressText.rectTransform, 60f, 20f);

            // The word card.
            var card = UIFactory.CreatePanel(t, "Card", new Color(1f, 1f, 1f, 0.97f), 24f);
            UIFactory.AddSoftShadow(card.gameObject, 5f, 0.25f);
            UIFactory.BandTop(card.rectTransform, 100f, 240f, 28f);
            var ct = card.transform;

            _wordText = UIFactory.CreateText(ct, "Word", "", 34, Theme.Purple);
            _wordText.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_wordText.rectTransform, 30f, 46f);

            _meaningText = UIFactory.CreateText(ct, "Meaning", "", 15, Theme.InkSoft);
            UIFactory.BandTop(_meaningText.rectTransform, 84f, 24f);

            var hear = UIFactory.CreateButton(ct, "Hear", "HEAR IT", 14,
                Theme.CreamPanel, Theme.Ink,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (Current != null) NativeSpeech.Speak(Current.tgt);
                });
            UIFactory.BandTop(hear.GetComponent<RectTransform>(), 128f, 44f, 70f);

            _heardText = UIFactory.CreateText(ct, "Heard", "", 13, Theme.Ink);
            _heardText.fontStyle = FontStyle.Italic;
            UIFactory.BandTop(_heardText.rectTransform, 182f, 22f, 16f);

            _feedbackText = UIFactory.CreateText(ct, "Feedback", "", 14, Theme.Teal);
            UIFactory.BandTop(_feedbackText.rectTransform, 206f, 24f, 16f);

            // Big mic button.
            _micButton = UIFactory.CreateButton(t, "Mic", "TAP & SAY IT", 18,
                Theme.Amber, Theme.WordChipDark,
                delegate { OnMicTap(); });
            UIFactory.BandTop(_micButton.GetComponent<RectTransform>(), 372f, 74f, 40f);
            _micBg = _micButton.GetComponent<Image>();
            _micLabel = _micButton.GetComponentInChildren<Text>();

            var skip = UIFactory.CreateButton(t, "Skip", "SKIP WORD", 13,
                new Color(1f, 1f, 1f, 0.22f), Color.white,
                delegate
                {
                    ProceduralAudio.PlayClick();
                    if (_running && !_advancing) FailWord("Skipped — it was \"" + Current.tgt + "\"");
                });
            UIFactory.BandTop(skip.GetComponent<RectTransform>(), 464f, 40f, 120f);

            BuildAuthOverlay(t);
        }

        void BuildAuthOverlay(Transform t)
        {
            var overlay = UIFactory.CreatePanel(t, "AuthOverlay", new Color(0.10f, 0.07f, 0.16f, 0.72f), 0f);
            overlay.raycastTarget = true;
            _authOverlay = overlay.rectTransform;

            var card = UIFactory.CreatePanel(overlay.transform, "Card", Theme.CreamPanel, 22f);
            UIFactory.AddSoftShadow(card.gameObject, 5f, 0.3f);
            UIFactory.BandTop(card.rectTransform, 260f, 190f, 36f);

            var msg = UIFactory.CreateText(card.transform, "Msg",
                "Microphone & speech recognition\npermission is needed for\npronunciation practice.\n\nEnable it in iOS Settings > LingoRun.",
                13, Theme.Ink);
            UIFactory.BandTop(msg.rectTransform, 16f, 110f, 14f);

            var close = UIFactory.CreateButton(card.transform, "Close", "BACK TO HOME", 14,
                Theme.Amber, Theme.WordChipDark,
                delegate { ProceduralAudio.PlayClick(); _authOverlay.gameObject.SetActive(false); bootstrap.ShowHome(); });
            UIFactory.BandTop(close.GetComponent<RectTransform>(), 134f, 44f, 30f);

            _authOverlay.gameObject.SetActive(false);
        }

        WordEntry Current { get { return (_words != null && _index < _words.Count) ? _words[_index] : null; } }

        public void BeginRun(bool review)
        {
            if (!_built) { Build(); _built = true; }
            _reviewMode = review;
            _running = true;
            _listening = false;
            _advancing = false;
            _index = 0; _tries = 0;
            _score = 0; _streak = 0; _bestStreak = 0;
            _sessionMissed.Clear();
            _sessionLearned.Clear();
            _authOverlay.gameObject.SetActive(false);

            _words = PickWords(review);
            NativeSpeech.RequestAuth(delegate (bool ok)
            {
                if (!ok && _running) _authOverlay.gameObject.SetActive(true);
            });
            ShowWord(true);
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
                    if (picked.Count >= WordsPerSession) break;
                }
            }
            // This level's own words (same slice the runner teaches).
            int level = ProgressManager.CurrentLevel;
            string topic = GameConfig.UnitTopicForLevel(level);
            var pool = VocabDatabase.BuildCurriculum(
                new List<string> { topic }, ProgressManager.A2Unlocked);
            if (pool.Count > 0)
            {
                int start = (GameConfig.PriorSlicesInTopic(level, GameMode.Words)
                    * GameConfig.WordsPerLevel) % pool.Count;
                for (int i = 0; picked.Count < WordsPerSession && i < pool.Count; i++)
                {
                    var w = pool[(start + i) % pool.Count];
                    if (used.Add(w.en)) picked.Add(w);
                }
            }
            return picked;
        }

        void ShowWord(bool speak)
        {
            var w = Current;
            if (w == null) { EndRun(); return; }
            _tries = 0;
            _lastHeard = "";
            _wordText.text = w.tgt;
            _meaningText.text = w.en;
            _heardText.text = "";
            _feedbackText.text = "";
            _progressText.text = "WORD " + (_index + 1) + " / " + _words.Count
                + (_reviewMode ? "   ·   REVIEW" : "");
            SetMicIdle();
            if (speak) NativeSpeech.Speak(w.tgt);
        }

        void SetMicIdle()
        {
            _listening = false;
            _micBg.color = Theme.Amber;
            _micLabel.text = "TAP & SAY IT";
            _micButton.interactable = true;
        }

        void OnMicTap()
        {
            if (!_running || _advancing) return;
            ProceduralAudio.PlayClick();
            if (_listening) { StopListen(); return; }

            _listening = true;
            _lastHeard = "";
            _micBg.color = Theme.BadRed;
            _micLabel.text = "LISTENING…  (TAP TO STOP)";
            _heardText.text = "";
            NativeSpeech.StopSpeaking();
            NativeSpeech.EditorSimulatedResult = Current != null ? Current.tgt : "";
            NativeSpeech.StartListening(
                delegate (string partial) { _lastHeard = partial; if (_heardText != null) _heardText.text = "“" + partial + "”"; },
                delegate (string final) { _lastHeard = final; if (_listening) { StopListen(); } },
                delegate (string err)
                {
                    if (!_listening) return;
                    _listening = false;
                    SetMicIdle();
                    _feedbackText.text = "Couldn't hear you — try again.";
                    _feedbackText.color = Theme.BadRed;
                });
            StartCoroutine(ListenTimeoutRoutine());
        }

        IEnumerator ListenTimeoutRoutine()
        {
            float t = 0f;
            while (_listening && t < ListenTimeout) { t += Time.deltaTime; yield return null; }
            if (_listening) StopListen();
        }

        void StopListen()
        {
            if (!_listening) return;
            _listening = false;
            NativeSpeech.StopListening();
            Evaluate(_lastHeard);
        }

        void Evaluate(string heard)
        {
            var w = Current;
            if (w == null) return;
            _heardText.text = string.IsNullOrEmpty(heard) ? "(heard nothing)" : "“" + heard + "”";

            if (Matches(heard, w.tgt))
            {
                ProceduralAudio.PlayCorrect();
                _streak++;
                _bestStreak = Mathf.Max(_bestStreak, _streak);
                _score += 15 + 3 * (_streak - 1);
                _sessionLearned.Add(w.en);
                _feedbackText.text = "PERFECT!";
                _feedbackText.color = Theme.Teal;
                UpdateHud();
                StartCoroutine(NextAfter(0.9f));
            }
            else
            {
                _tries++;
                _streak = 0;
                if (_tries >= TriesPerWord)
                {
                    FailWord("It was “" + w.tgt + "” — listen and move on.");
                }
                else
                {
                    ProceduralAudio.PlayWrong();
                    _feedbackText.text = "Almost — hear it again and retry.";
                    _feedbackText.color = Theme.BadRed;
                    NativeSpeech.Speak(w.tgt);
                    SetMicIdle();
                    UpdateHud();
                }
            }
        }

        void FailWord(string message)
        {
            var w = Current;
            ProceduralAudio.PlayWrong();
            _sessionMissed.Add(w.en);
            _feedbackText.text = message;
            _feedbackText.color = Theme.BadRed;
            NativeSpeech.Speak(w.tgt);
            UpdateHud();
            StartCoroutine(NextAfter(1.3f));
        }

        IEnumerator NextAfter(float seconds)
        {
            _advancing = true;
            _micButton.interactable = false;
            yield return new WaitForSeconds(seconds);
            _advancing = false;
            _index++;
            if (_index >= _words.Count) EndRun();
            else ShowWord(true);
        }

        /// <summary>Forgiving pronunciation check: accent/case-insensitive,
        /// articles optional, target accepted anywhere in the transcript
        /// (recognizers often wrap the word in filler). Public — the
        /// speech-runner mode (RunningGame3D) uses the same check.</summary>
        public static bool Matches(string heard, string target)
        {
            if (string.IsNullOrEmpty(heard) || string.IsNullOrEmpty(target)) return false;
            string h = Normalize(heard);
            string tgtFull = Normalize(target);
            string tgtBare = Normalize(StripFirstWordArticle(target));
            if (h == tgtFull || h == tgtBare) return true;
            return (" " + h + " ").Contains(" " + tgtBare + " ")
                || (" " + h + " ").Contains(" " + tgtFull + " ");
        }

        static string StripFirstWordArticle(string s)
        {
            int space = s.IndexOf(' ');
            if (space <= 0) return s;
            string first = s.Substring(0, space).ToLowerInvariant();
            switch (first)
            {
                case "el": case "la": case "los": case "las": case "un": case "una":
                case "le": case "les": case "il": case "lo": case "i": case "gli":
                case "der": case "die": case "das": case "ein": case "eine":
                    return s.Substring(space + 1);
                default: return s;
            }
        }

        static string Normalize(string s)
        {
            string formD = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (char c in formD)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsPunctuation(c)) continue;
                sb.Append(c);
            }
            // Collapse whitespace runs.
            var parts = sb.ToString().Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        void UpdateHud() { _scoreText.text = _score.ToString(); }

        void EndRun()
        {
            if (!_running) return;
            _running = false;
            _listening = false;
            NativeSpeech.StopListening();
            NativeSpeech.StopSpeaking();
            StopAllCoroutines();
            var results = ProgressManager.RecordRunEnd(
                _score, _bestStreak, _sessionMissed, _sessionLearned,
                Mathf.Max(_words != null ? _words.Count : 1, 1), _reviewMode,
                false, false, false, 0);
            results.isSpeech = true;
            bootstrap.ShowGameOver(results);
        }
    }
}
