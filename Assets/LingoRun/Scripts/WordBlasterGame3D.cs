using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Word Blaster: a compact arcade companion to the lane-runner. Three
    /// asteroids drift down a starry Space backdrop, each labeled with a
    /// translation candidate; tap the one that matches the English prompt
    /// before it reaches the bottom. Pure uGUI (no 3D world), one wave of
    /// three asteroids active at a time — reuses the same vocab pool, mastery
    /// tracking and coin economy as RunningGame3D (ProgressManager.RecordRunEnd),
    /// just a different reflex loop.
    /// </summary>
    public class WordBlasterGame3D : MonoBehaviour
    {
        [System.NonSerialized] public LingoRunBootstrap bootstrap;

        const int RoundSize = 12;      // prompts per round
        const float PlayTop = 200f;    // asteroid spawn Y (from screen top)
        const float PlayBottom = 660f; // asteroid "danger line" Y (from screen top)
        const float AsteroidSize = 86f;
        const float ResolveGap = 0.6f; // pause between one wave resolving and the next spawning
        const float ShipX = 220f;      // turret position (screen-center; GameConfig.RefWidth * 0.5f)
        const float ShipY = PlayBottom + 26f;
        const float FireDuration = 0.16f; // projectile travel time, ship -> tapped asteroid
        static readonly Color AsteroidIdle = Theme.Hex("4A3B78");

        class Wave
        {
            public WordEntry[] words;
            public int correctIndex;
            public WordEntry correct;
            public bool resolved;
        }

        // ---------- state ----------
        bool _built;
        bool _running;
        bool _userPaused;
        List<WordEntry> _pool;
        bool _reviewMode;
        Wave _wave;
        float _waveT;
        float _fallDuration;
        float _nextWaveDelay;
        float _fireTimer;          // >0 while the projectile is in flight to a tapped asteroid
        int _fireLane;
        Vector2 _fireStart, _fireTarget;
        int _impactScaleLane = -1; // asteroid getting the post-hit "punch" scale during _nextWaveDelay
        int _score, _lives, _streak, _bestStreak, _challengeCount;
        readonly HashSet<string> _sessionMissed = new HashSet<string>();
        readonly HashSet<string> _sessionLearned = new HashSet<string>();

        // ---------- HUD ----------
        Text _promptText;
        Text _scoreText;
        Text _streakText;
        readonly Image[] _hearts = new Image[3];
        readonly Image[] _asteroidBg = new Image[3];
        readonly Text[] _asteroidLabel = new Text[3];
        readonly RectTransform[] _asteroidRt = new RectTransform[3];
        RectTransform _pauseOverlay;
        Text _muteLabel;
        RectTransform _shipRt;
        Image _shipImg;
        GameObject _shipCockpit;
        RectTransform _projectileRt;
        Image _projectileImg;

        // ==================== lifecycle ====================

        public void BeginRun(bool review)
        {
            EnsureBuilt();
            gameObject.SetActive(true);

            _reviewMode = review;
            _score = 0; _lives = 3; _streak = 0; _bestStreak = 0; _challengeCount = 0;
            _waveT = 0f; _nextWaveDelay = 0f; _fireTimer = 0f; _impactScaleLane = -1;
            _userPaused = false; _running = true;
            _sessionMissed.Clear();
            _sessionLearned.Clear();
            _servedThisRun.Clear();
            _wave = null;

            BuildPool();
            _fallDuration = FallDurationFor(bootstrap.CurrentDifficulty);
            _pauseOverlay.gameObject.SetActive(false);
            _projectileImg.gameObject.SetActive(false);
            _shipRt.localScale = Vector3.one;

            // Ship = the player's equipped ball (same shape + color as the
            // Words runner uses). Vehicles fall back to the shop's silhouette.
            int shipSkin = Mathf.Clamp(ProgressManager.SelectedSkin, 0, Theme.BallSkins.Length - 1);
            int shipShape = Mathf.Clamp(ProgressManager.SelectedShape, 0, BallScreenUI.ShapeNames.Length - 1);
            Color shipColor = Theme.BallSkins[shipSkin];
            BallScreenUI.StyleShapeImage(_shipImg, shipShape, shipColor, 34f);
            UIFactory.PlaceTop(_shipRt, ShipX, ShipY, _shipRt.sizeDelta.x, _shipRt.sizeDelta.y);
            _shipCockpit.SetActive(shipShape == 0); // white cap dot only fits the sphere
            _projectileImg.color = new Color(
                Mathf.Lerp(shipColor.r, 1f, 0.5f),
                Mathf.Lerp(shipColor.g, 1f, 0.5f),
                Mathf.Lerp(shipColor.b, 1f, 0.5f));
            for (int i = 0; i < 3; i++)
            {
                _asteroidBg[i].gameObject.SetActive(false);
                _asteroidRt[i].localScale = Vector3.one;
            }

            UpdateHud();
            if (_pool == null || _pool.Count == 0) { EndRun(); return; }
            SpawnWave();
        }

        static float FallDurationFor(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Fast: return 3.0f;
                case Difficulty.Normal: return 4.0f;
                default: return 5.2f;
            }
        }

        void BuildPool()
        {
            var missed = ProgressManager.MissedKeysFor(false);
            if (_reviewMode && missed.Count > 0)
            {
                var reviewWords = new List<WordEntry>();
                foreach (var en in missed)
                {
                    var w = VocabDatabase.FindByEn(en);
                    if (w != null) reviewWords.Add(w);
                }
                if (reviewWords.Count == 0)
                {
                    _pool = VocabDatabase.BuildPool(ProfileManager.ActiveTopics, ProgressManager.A2Unlocked);
                    return;
                }
                _pool = new List<WordEntry>(reviewWords);
                if (reviewWords.Count < 8)
                {
                    _pool.AddRange(reviewWords);
                    _pool.AddRange(reviewWords);
                }
                return;
            }

            _pool = VocabDatabase.BuildPool(ProfileManager.ActiveTopics, ProgressManager.A2Unlocked);
            foreach (var en in missed)
            {
                var w = VocabDatabase.FindByEn(en);
                if (w == null) continue;
                _pool.Add(w); _pool.Add(w); _pool.Add(w);
            }
        }

        void Update()
        {
            if (!_running || _userPaused) return;

            // 1) Projectile in flight: fly it toward the tapped asteroid, then
            // apply the actual hit/miss outcome once it arrives.
            if (_fireTimer > 0f)
            {
                _fireTimer -= Time.deltaTime;
                float k = 1f - Mathf.Clamp01(_fireTimer / FireDuration);
                _projectileRt.anchoredPosition = Vector2.Lerp(_fireStart, _fireTarget, k);
                _shipRt.localScale = Vector3.one * (1f + 0.25f * (1f - k)); // quick recoil, eases back to 1x
                if (_fireTimer <= 0f)
                {
                    _projectileImg.gameObject.SetActive(false);
                    _shipRt.localScale = Vector3.one;
                    ApplyOutcome(_fireLane);
                }
                return;
            }

            // 2) Gap between one wave resolving and the next spawning: play the
            // impact "punch" on whichever asteroid was just hit/missed.
            if (_nextWaveDelay > 0f)
            {
                _nextWaveDelay -= Time.deltaTime;
                if (_impactScaleLane >= 0)
                {
                    float k = Mathf.Clamp01(1f - _nextWaveDelay / ResolveGap);
                    _asteroidRt[_impactScaleLane].localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(k * Mathf.PI));
                }
                if (_nextWaveDelay <= 0f)
                {
                    _impactScaleLane = -1;
                    if (_challengeCount >= RoundSize || _lives <= 0) { EndRun(); return; }
                    SpawnWave();
                }
                return;
            }

            // 3) Asteroids falling, waiting for a tap.
            if (_wave == null || _wave.resolved) return;

            _waveT += Time.deltaTime / _fallDuration;
            for (int i = 0; i < 3; i++) SetAsteroidY(i, Mathf.Clamp01(_waveT));

            if (_waveT >= 1f) ApplyOutcome(-1); // reached the ship un-tapped — straight miss, no shot fired
        }

        // No-repeat within one round (cleared in BeginRun / when pool runs dry).
        readonly System.Collections.Generic.HashSet<string> _servedThisRun =
            new System.Collections.Generic.HashSet<string>();

        void SpawnWave()
        {
            var candidates = new List<WordEntry>();
            foreach (var w in _pool)
                if (!_servedThisRun.Contains(w.en)) candidates.Add(w);
            if (candidates.Count < 1) { _servedThisRun.Clear(); candidates = _pool; }
            WordEntry correct = VocabDatabase.WeightedPick(candidates, ProgressManager.DifficultyScores);
            if (correct == null) { EndRun(); return; }
            _servedThisRun.Add(correct.en);

            var all = new List<WordEntry> { correct };
            all.AddRange(VocabDatabase.PickDistractors(correct, 2));
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
            }

            _wave = new Wave { words = all.ToArray(), correctIndex = all.IndexOf(correct), correct = correct };
            _waveT = 0f;
            _promptText.text = correct.en;

            for (int i = 0; i < 3; i++)
            {
                _asteroidLabel[i].text = all[i].tgt;
                _asteroidBg[i].color = AsteroidIdle;
                _asteroidBg[i].gameObject.SetActive(true);
                _asteroidRt[i].localScale = Vector3.one;
                SetAsteroidY(i, 0f);
            }
        }

        void SetAsteroidY(int i, float t)
        {
            var rt = _asteroidRt[i];
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -Mathf.Lerp(PlayTop, PlayBottom, t));
        }

        void OnAsteroidTap(int lane)
        {
            if (!_running || _userPaused) return;
            if (_wave == null || _wave.resolved || _nextWaveDelay > 0f || _fireTimer > 0f) return;
            _wave.resolved = true; // lock input + freeze the fall the instant you fire
            FireAt(lane);
        }

        /// <summary>Launch the projectile from the turret toward the tapped
        /// asteroid; the actual hit/miss is applied when it arrives (see Update).</summary>
        void FireAt(int lane)
        {
            ProceduralAudio.PlayClick();
            _fireLane = lane;
            _fireTimer = FireDuration;
            _fireStart = new Vector2(ShipX, -ShipY);
            _fireTarget = _asteroidRt[lane].anchoredPosition;
            _projectileRt.anchoredPosition = _fireStart;
            _projectileImg.gameObject.SetActive(true);
        }

        void ApplyOutcome(int tappedLane)
        {
            _wave.resolved = true;
            bool correct = tappedLane == _wave.correctIndex;
            _challengeCount++;

            if (correct)
            {
                _streak++;
                _bestStreak = Mathf.Max(_bestStreak, _streak);
                _score += 10 + Mathf.Min(_streak, 10) * 2;
                _sessionLearned.Add(_wave.correct.en);
                ProceduralAudio.PlayCorrect();
                _asteroidBg[tappedLane].color = Theme.Good;
                _impactScaleLane = tappedLane;
            }
            else
            {
                _lives = Mathf.Max(0, _lives - 1);
                _streak = 0;
                _sessionMissed.Add(_wave.correct.en);
                ProceduralAudio.PlayWrong();
                if (tappedLane >= 0) _asteroidBg[tappedLane].color = Theme.BadRed;
                _asteroidBg[_wave.correctIndex].color = Theme.Good;
                _impactScaleLane = tappedLane >= 0 ? tappedLane : _wave.correctIndex;
            }

            UpdateHud();
            _nextWaveDelay = ResolveGap;
        }

        void EndRun()
        {
            _running = false;
            var results = ProgressManager.RecordRunEnd(
                _score, _bestStreak, _sessionMissed, _sessionLearned,
                _challengeCount, _reviewMode, false, false, false, bootstrap.ActiveMapLevel);
            results.isBlaster = true;
            bootstrap.ShowGameOver(results);
        }

        void UpdateHud()
        {
            _scoreText.text = _score.ToString();
            _streakText.text = "x" + _streak;
            for (int i = 0; i < 3; i++)
                _hearts[i].color = i < _lives ? Theme.HeartRed : new Color(1f, 1f, 1f, 0.22f);
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

            // Starry Space backdrop (forced — Blaster always looks like this,
            // independent of the equipped ball-run background theme).
            var bgTheme = Theme.Backgrounds[1]; // Space
            UIFactory.CreateVerticalGradient(t, "Sky", bgTheme.skyTint, bgTheme.skyGround);
            var rand = new System.Random(7);
            for (int i = 0; i < 36; i++)
            {
                var star = UIFactory.CreateCircle(t, "Star" + i, new Color(1f, 1f, 1f, 0.55f + 0.4f * (float)rand.NextDouble()));
                var srt = star.rectTransform;
                srt.anchorMin = srt.anchorMax = new Vector2((float)rand.NextDouble(), (float)rand.NextDouble());
                srt.pivot = new Vector2(0.5f, 0.5f);
                float s = 2f + 3f * (float)rand.NextDouble();
                srt.sizeDelta = new Vector2(s, s);
                srt.anchoredPosition = Vector2.zero;
            }

            // ----- top HUD -----
            var pauseBtn = UIFactory.CreateButton(t, "PauseBtn", "II", 16,
                Theme.CreamPanel, Theme.Ink, delegate { ProceduralAudio.PlayClick(); TogglePause(true); });
            UIFactory.PlaceTop(pauseBtn.GetComponent<RectTransform>(), 34f, 30f, 40f, 40f);

            for (int i = 0; i < 3; i++)
            {
                var heart = UIFactory.CreateCircle(t, "Heart" + i, Theme.HeartRed);
                UIFactory.PlaceTop(heart.rectTransform, 74f + i * 24f, 30f, 16f, 16f);
                _hearts[i] = heart;
            }

            var scorePill = UIFactory.CreatePanel(t, "ScorePill", new Color(1f, 1f, 1f, 0.14f), 16f);
            UIFactory.PlaceTop(scorePill.rectTransform, 372f, 40f, 110f, 54f);
            _scoreText = UIFactory.CreateText(scorePill.transform, "Score", "0", 20, Color.white);
            UIFactory.BandTop(_scoreText.rectTransform, 4f, 26f);
            _streakText = UIFactory.CreateText(scorePill.transform, "Streak", "x0", 12, Theme.Amber);
            UIFactory.BandTop(_streakText.rectTransform, 30f, 18f);

            // ----- prompt card -----
            var card = UIFactory.CreatePanel(t, "PromptCard", new Color(1f, 1f, 1f, 0.12f), 20f);
            UIFactory.BandTop(card.rectTransform, 96f, 78f, 40f);
            var caption = UIFactory.CreateText(card.transform, "Caption", "SHOOT THE TRANSLATION", 11,
                new Color(1f, 1f, 1f, 0.7f));
            UIFactory.BandTop(caption.rectTransform, 10f, 18f);
            _promptText = UIFactory.CreateText(card.transform, "Prompt", "-", 28, Color.white);
            _promptText.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_promptText.rectTransform, 32f, 40f);

            // ----- ship / turret (fires the projectile at whatever lane you tap) -----
            // Rendered as the player's equipped ball (same shape + color as the
            // Words runner) — styled in BeginRun via BallScreenUI.StyleShapeImage.
            var ship = UIFactory.CreateCircle(t, "Ship", Theme.Amber);
            UIFactory.PlaceTop(ship.rectTransform, ShipX, ShipY, 30f, 30f);
            _shipRt = ship.rectTransform;
            _shipImg = ship;
            var cockpit = UIFactory.CreateCircle(ship.transform, "Cockpit", Theme.CreamPanel);
            _shipCockpit = cockpit.gameObject;
            var cprt = cockpit.rectTransform;
            cprt.anchorMin = cprt.anchorMax = new Vector2(0.5f, 0.5f);
            cprt.pivot = new Vector2(0.5f, 0.5f);
            cprt.sizeDelta = new Vector2(12f, 12f);
            cprt.anchoredPosition = Vector2.zero;

            // ----- projectile (hidden until fired) -----
            var projGo = new GameObject("Projectile", typeof(RectTransform), typeof(Image));
            projGo.transform.SetParent(t, false);
            _projectileImg = projGo.GetComponent<Image>();
            _projectileImg.sprite = UIFactory.CircleSprite;
            _projectileImg.color = Theme.Hex("FFF4C2");
            _projectileImg.raycastTarget = false;
            _projectileRt = projGo.GetComponent<RectTransform>();
            UIFactory.PlaceTop(_projectileRt, ShipX, ShipY, 14f, 14f);
            UIFactory.AddSoftShadow(projGo, 0f, 0.35f);
            projGo.SetActive(false);

            // ----- asteroids (3 persistent slots, repositioned/relabeled per wave) -----
            for (int i = 0; i < 3; i++)
            {
                float laneX = GameConfig.RefWidth * (i + 0.5f) / 3f;

                var btnGo = new GameObject("Asteroid" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(t, false);
                var img = btnGo.GetComponent<Image>();
                UIFactory.ApplyRounded(img, 40f);
                img.color = AsteroidIdle;
                img.raycastTarget = true;
                UIFactory.AddSoftShadow(btnGo, 3f, 0.25f);
                var rt = btnGo.GetComponent<RectTransform>();
                UIFactory.PlaceTop(rt, laneX, PlayTop, AsteroidSize, AsteroidSize);
                _asteroidRt[i] = rt;
                _asteroidBg[i] = img;

                int idx = i;
                btnGo.GetComponent<Button>().onClick.AddListener(delegate { OnAsteroidTap(idx); });

                var label = UIFactory.CreateText(btnGo.transform, "Label", "", 13, Color.white);
                label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = 15;
                var lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(6f, 6f);
                lrt.offsetMax = new Vector2(-6f, -6f);
                _asteroidLabel[i] = label;

                btnGo.SetActive(false);
            }

            BuildPauseOverlay();
        }

        void BuildPauseOverlay()
        {
            var t = transform;
            var overlay = UIFactory.CreatePanel(t, "PauseOverlay", new Color(0.98f, 0.97f, 1f, 0.97f), 0f);
            overlay.raycastTarget = true;
            _pauseOverlay = overlay.rectTransform;

            var caption = UIFactory.CreateText(overlay.transform, "Caption", "GAME PAUSED", 12, Theme.InkSoft);
            UIFactory.BandTop(caption.rectTransform, 200f, 24f);

            var title = UIFactory.CreateText(overlay.transform, "Title", "PAUSED", 40, Theme.Ink);
            UIFactory.BandTop(title.rectTransform, 230f, 60f);

            var resume = UIFactory.CreateButton(overlay.transform, "Resume", "RESUME", 20,
                Theme.Orange, Theme.Cream, delegate { ProceduralAudio.PlayClick(); TogglePause(false); });
            UIFactory.BandTop(resume.GetComponent<RectTransform>(), 330f, 56f, 80f);

            var mute = UIFactory.CreateButton(overlay.transform, "Mute", "SOUND: ON", 14,
                Theme.CreamPanel, Theme.Ink, null);
            UIFactory.BandTop(mute.GetComponent<RectTransform>(), 400f, 48f, 80f);
            _muteLabel = mute.GetComponentInChildren<Text>();
            mute.onClick.AddListener(delegate
            {
                ProceduralAudio.Muted = !ProceduralAudio.Muted;
                _muteLabel.text = ProceduralAudio.Muted ? "SOUND: OFF" : "SOUND: ON";
                ProceduralAudio.PlayClick();
            });

            var exit = UIFactory.CreateButton(overlay.transform, "Exit", "EXIT TO HOME", 14,
                Theme.CreamPanel, Theme.Ink, delegate { ProceduralAudio.PlayClick(); ExitToHome(); });
            UIFactory.BandTop(exit.GetComponent<RectTransform>(), 460f, 48f, 80f);

            var note = UIFactory.CreateText(overlay.transform, "Note",
                "Exiting ends this run without saving the score.", 11, Theme.InkSoft);
            UIFactory.BandTop(note.rectTransform, 530f, 22f);

            _pauseOverlay.gameObject.SetActive(false);
        }
    }
}
