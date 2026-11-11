using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Single entry point for LingoRun. Drop this component on an empty GameObject
    /// in an empty scene and press Play — it builds the EventSystem, Canvas,
    /// meadow background, and all screens from code. No prefabs, no scene wiring.
    ///
    /// Target: mobile portrait 9:16 (reference canvas 440x782, scales with width).
    /// Input: Unity Input System (touch, mouse, keyboard W/S + arrows).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LingoRunBootstrap : MonoBehaviour
    {
        /// <summary>
        /// Zero-setup boot: when Play starts in ANY scene, spawn the game
        /// automatically if no bootstrap exists yet. You can still place the
        /// component manually — this then does nothing.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (Object.FindFirstObjectByType<LingoRunBootstrap>() != null) return;
            var go = new GameObject("LingoRun (auto)");
            go.AddComponent<LingoRunBootstrap>();
        }

        public Difficulty CurrentDifficulty = Difficulty.Calm;
        public GameMode CurrentMode = GameMode.Words;

        Canvas _canvas;
        HomeScreenUI _home;
        GameOverUI _gameOver;
        RunningGame3D _game;
        WordBlasterGame3D _blaster;
        BubbleGameUI _bubbles;
        MemoryMatchGameUI _match;
        LoginScreenUI _login;
        AchievementsScreenUI _achievements;
        DictionaryScreenUI _dictionary;
        BallScreenUI _ball;
        MapScreenUI _map;
        WorldMapUI _worldMap;
        SettingsScreenUI _settings;
        GamePickerUI _picker;
        TipsScreenUI _tips;
        HeartsPopupUI _heartsPopup;
        MascotUI _mascot;
        public MascotUI Mascot { get { return _mascot; } }
        GameObject _meadowGo;

        int _activeMapLevel;
        /// <summary>0 when the run in progress wasn't launched from a specific
        /// World Map node; otherwise the map level number StartFromMap
        /// launched — read by RunningGame3D/WordBlasterGame3D when recording
        /// the run's result (see ProgressManager.RecordRunEnd's mapLevel arg).</summary>
        public int ActiveMapLevel { get { return _activeMapLevel; } }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;

            LanguageManager.EnsureLoaded();
            NativeSpeech.Ensure();
            ProgressManager.Load();
            GameCenterManager.Authenticate();
            CurrentDifficulty = ProgressManager.SavedDifficulty;
            ProceduralAudio.Ensure();

            CreateEventSystem();
            CreateCanvas();
            // Background stays full-bleed (extends under the notch)…
            BuildMeadowBackground(_canvas.transform);

            // …while all interactive content lives inside the device safe area
            // (below the notch / Dynamic Island, above the home indicator).
            var safeRoot = UIFactory.CreateRoot(_canvas.transform, "SafeArea");
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            Transform safe = safeRoot;

            // Creation order defines z-order: background < home < game < game over.
            _home = new HomeScreenUI(safe, this);

            var gameRoot = UIFactory.CreateRoot(safe, "GameRoot");
            _game = gameRoot.gameObject.AddComponent<RunningGame3D>();
            _game.bootstrap = this;

            var blasterRoot = UIFactory.CreateRoot(safe, "BlasterRoot");
            _blaster = blasterRoot.gameObject.AddComponent<WordBlasterGame3D>();
            _blaster.bootstrap = this;
            blasterRoot.gameObject.SetActive(false);

            var bubblesRoot = UIFactory.CreateRoot(safe, "BubblesRoot");
            _bubbles = bubblesRoot.gameObject.AddComponent<BubbleGameUI>();
            _bubbles.bootstrap = this;
            bubblesRoot.gameObject.SetActive(false);

            var matchRoot = UIFactory.CreateRoot(safe, "MatchRoot");
            _match = matchRoot.gameObject.AddComponent<MemoryMatchGameUI>();
            _match.bootstrap = this;
            matchRoot.gameObject.SetActive(false);

            _gameOver = new GameOverUI(safe, this);
            _achievements = new AchievementsScreenUI(safe, this);
            _dictionary = new DictionaryScreenUI(safe, this);
            _ball = new BallScreenUI(safe, this);
            _map = new MapScreenUI(safe, this);
            var worldMapRoot = UIFactory.CreateRoot(safe, "WorldMapRoot");
            _worldMap = worldMapRoot.gameObject.AddComponent<WorldMapUI>();
            _worldMap.bootstrap = this;
            worldMapRoot.gameObject.SetActive(false);
            _settings = new SettingsScreenUI(safe, this);
            _picker = new GamePickerUI(safe, this);
            _tips = new TipsScreenUI(safe, this);
            _login = new LoginScreenUI(safe, this);
            _heartsPopup = new HeartsPopupUI(safe, this);
            _mascot = new MascotUI(safe); // very last → Lino hops in front of everything

            ShowLogin();
        }

        // ---------- screen flow ----------

        void HideAll()
        {
            _game.gameObject.SetActive(false);
            _blaster.gameObject.SetActive(false);
            _bubbles.gameObject.SetActive(false);
            _match.gameObject.SetActive(false);
            _home.SetVisible(false);
            _gameOver.SetVisible(false);
            _achievements.SetVisible(false);
            _dictionary.SetVisible(false);
            _ball.SetVisible(false);
            _map.SetVisible(false);
            _worldMap.gameObject.SetActive(false);
            _settings.SetVisible(false);
            _picker.SetVisible(false);
            _tips.SetVisible(false);
            _login.SetVisible(false);
            _mascot.HideInstant(); // each screen decides whether Lino returns
            _meadowGo.SetActive(true);
        }

        public void ShowDictionary()
        {
            HideAll();
            _dictionary.Refresh();
            _dictionary.SetVisible(true);
            _mascot.Appear("dictionary");
        }

        public void ShowBall()
        {
            HideAll();
            _ball.Refresh();
            _ball.SetVisible(true);
            _mascot.Appear("shop");
        }

        public void ShowLogin()
        {
            HideAll();
            _login.Refresh();
            _login.SetVisible(true);
        }

        /// <summary>Called by the login screen after a profile was chosen/created.</summary>
        public void OnLoggedIn()
        {
            CurrentDifficulty = ProgressManager.SavedDifficulty;
            ShowHome();
        }

        public void ShowHome()
        {
            HideAll();
            _home.Refresh();
            _home.SetVisible(true);
            _mascot.Appear("home");
        }

        public void ShowAchievements()
        {
            HideAll();
            _achievements.Refresh();
            _achievements.SetVisible(true);
            _mascot.Appear("awards");
        }

        public void ShowMap()
        {
            HideAll();
            _map.Refresh();
            _map.SetVisible(true);
            _mascot.Appear("map");
        }

        /// <summary>Overcooked-style 3D overworld — the alternate map view.
        /// The meadow canvas would cover the 3D scene, so it's dropped while
        /// the world map is open (same trick the game modes use).</summary>
        public void ShowWorldMap()
        {
            HideAll();
            _meadowGo.SetActive(false);
            _worldMap.gameObject.SetActive(true);
        }

        public void ShowSettings()
        {
            HideAll();
            _settings.Refresh();
            _settings.SetVisible(true);
        }

        public void ShowTips()
        {
            HideAll();
            _tips.Refresh();
            _tips.SetVisible(true);
        }

        public void ShowGamePicker()
        {
            HideAll();
            _picker.Refresh();
            _picker.SetVisible(true);
            _mascot.Appear("games");
        }

        /// <summary>Free Play launch: the chosen game, casual rules — no
        /// hearts gate, no heart loss, no map/level advancement (see the
        /// isFreePlay flag / RunningGame3D._freePlay). Coins + mastery still
        /// count. Speak has its own entry (StartSpeech) since it's always
        /// free anyway.</summary>
        public void StartFreePlay(GameMode mode)
        {
            _activeMapLevel = 0;
            CurrentMode = mode;
            HideAll();
            _meadowGo.SetActive(false);
            if (mode == GameMode.Match)
            {
                _match.gameObject.SetActive(true);
                _match.BeginRun(false);
            }
            else if (mode == GameMode.Bubbles)
            {
                _bubbles.gameObject.SetActive(true);
                _bubbles.BeginRun(false);
            }
            else
            {
                _game.gameObject.SetActive(true);
                _game.BeginFreePlay();
            }
        }

        /// <summary>
        /// Launches a specific World Map node: picks up that node's fixed
        /// mode + pace (GameConfig.GetMapLevel) and starts a run exactly the
        /// way Home's own PLAY button does. Note: the actual word/sentence
        /// content served during a Words-mode run still comes from
        /// ProgressManager.CurrentLevel's sequential curriculum (see
        /// RunningGame3D.BuildLevelSet) — tapping an already-cleared earlier
        /// map node replays the CURRENT level's words, not that specific
        /// node's original set, since the vocab curriculum isn't (yet) keyed
        /// per map node.
        /// </summary>
        public void StartFromMap(int level)
        {
            if (HeartsManager.Hearts <= 0) { _heartsPopup.Show(); return; }
            var def = GameConfig.GetMapLevel(level);
            CurrentMode = def.mode;
            SetDifficulty(def.pace);
            if (CurrentMode == GameMode.Blaster) StartBlaster(false);
            else if (CurrentMode == GameMode.Bubbles) StartBubbles(false);
            else if (CurrentMode == GameMode.Match) StartMatch(false);
            else StartRun(false);
            // Set AFTER Start*, which reset it to 0 — see ActiveMapLevel.
            _activeMapLevel = level;
        }

        public void StartRun(bool review)
        {
            // Review is ALWAYS free (and earns a heart) — only real levels
            // require hearts. See HeartsManager.
            if (!review && HeartsManager.Hearts <= 0) { _heartsPopup.Show(); return; }
            _activeMapLevel = 0;
            HideAll();
            // The overlay canvas would hide the 3D world — drop the meadow while playing.
            _meadowGo.SetActive(false);
            _game.gameObject.SetActive(true);
            _game.BeginRun(review);
        }

        public void StartBlaster(bool review)
        {
            if (!review && HeartsManager.Hearts <= 0) { _heartsPopup.Show(); return; }
            _activeMapLevel = 0;
            HideAll();
            _meadowGo.SetActive(false);
            _blaster.gameObject.SetActive(true);
            _blaster.BeginRun(review);
        }

        public void StartBubbles(bool review)
        {
            if (!review && HeartsManager.Hearts <= 0) { _heartsPopup.Show(); return; }
            _activeMapLevel = 0;
            HideAll();
            _meadowGo.SetActive(false);
            _bubbles.gameObject.SetActive(true);
            _bubbles.BeginRun(review);
        }

        public void StartMatch(bool review)
        {
            if (!review && HeartsManager.Hearts <= 0) { _heartsPopup.Show(); return; }
            _activeMapLevel = 0;
            HideAll();
            // Memory Match brings its own full-screen gradient — hide the meadow.
            _meadowGo.SetActive(false);
            _match.gameObject.SetActive(true);
            _match.BeginRun(review);
        }

        /// <summary>Pronunciation practice — now the speech-runner: gates
        /// open when you say the word in time (RunningGame3D speech mode).
        /// Never gated by hearts (speaking practice is always available,
        /// same policy as Review).</summary>
        public void StartSpeech(bool review)
        {
            _activeMapLevel = 0;
            HideAll();
            _meadowGo.SetActive(false);
            _game.gameObject.SetActive(true);
            _game.BeginSpeechRun(review);
        }

        public void ShowGameOver(RunResults results)
        {
            HideAll();
            _gameOver.Populate(results);
            _gameOver.SetVisible(true);
            _mascot.Appear("gameover");
            // Push fresh bests to the Game Center leaderboards (no-op when
            // signed out / not on device).
            GameCenterManager.ReportAll();
        }

        /// <summary>Coroutine host for the plain (non-MonoBehaviour) screen classes.</summary>
        public Coroutine RunRoutine(System.Collections.IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        /// <summary>Tear the whole runtime-built UI down and boot it again —
        /// used when the APP LANGUAGE changes, since every label is baked at
        /// construction time. A tiny helper object waits one frame so the old
        /// hierarchy (EventSystem included) is really gone before rebuilding.</summary>
        public void RebuildUI()
        {
            Destroy(gameObject);
            new GameObject("LingoRunReboot").AddComponent<UIRebooter>();
        }

        class UIRebooter : MonoBehaviour
        {
            System.Collections.IEnumerator Start()
            {
                yield return null; // let the old bootstrap finish dying
                var go = new GameObject("LingoRun (auto)");
                go.AddComponent<LingoRunBootstrap>();
                Destroy(gameObject);
            }
        }

        public void SetDifficulty(Difficulty d)
        {
            CurrentDifficulty = d;
            ProgressManager.SavedDifficulty = d;
            ProgressManager.Save();
        }

        // ---------- construction ----------

        void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(transform, false);
        }

        void CreateCanvas()
        {
            var go = new GameObject("LingoRunCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameConfig.RefWidth, GameConfig.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // lock width at 440, let height breathe (portrait)
        }

        /// <summary>
        /// Candy-pastel menu backdrop: a 3-tone purple->pink->peach sky
        /// gradient with large translucent color blobs (faux-blur look),
        /// a few floating dots, and small sparkle stars for depth — styled
        /// after a glossy mobile-game home screen mockup.
        /// </summary>
        void BuildMeadowBackground(Transform parent)
        {
            var root = UIFactory.CreateRoot(parent, "Backdrop");
            _meadowGo = root.gameObject;
            var t = root.transform;

            UIFactory.CreateVerticalGradient(t, "Sky", new[] { Theme.SkyTop, Theme.SkyMid, Theme.SkyBottom });

            // A handful of large soft blobs (layered circles read as
            // blurred color washes) — trimmed down from the original set so
            // the background reads as a quiet wash of color instead of
            // competing with the foreground cards for attention. Kept only
            // in the corners, away from where the main content sits.
            Blob(t, 0.92f, 0.06f, 260f, Theme.Hex("FFC94D", 0.14f));
            Blob(t, 0.04f, 0.24f, 220f, Theme.Hex("FF8FC7", 0.10f));
            Blob(t, 0.10f, 0.92f, 260f, Theme.Hex("9B6BF2", 0.12f));

            // A few floating dots.
            float[,] dots = { { 0.20f, 0.14f, 7f }, { 0.80f, 0.50f, 6f }, { 0.14f, 0.68f, 6f } };
            for (int i = 0; i < dots.GetLength(0); i++)
            {
                var dot = UIFactory.CreateCircle(t, "Dot" + i, new Color(1f, 1f, 1f, 0.4f));
                var rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(dots[i, 0], 1f - dots[i, 1]);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(dots[i, 2], dots[i, 2]);
                rt.anchoredPosition = Vector2.zero;
            }

            // A few sparkle stars — enough to keep the twinkly accent from
            // the reference mockup without turning into visual noise.
            float[,] stars = { { 0.14f, 0.05f, 11f }, { 0.90f, 0.38f, 9f }, { 0.86f, 0.88f, 9f } };
            Color[] starColors = { Theme.Hex("FFE8A3", 0.8f), Theme.Hex("FFFFFF", 0.7f) };
            for (int i = 0; i < stars.GetLength(0); i++)
            {
                var star = UIFactory.CreateStar(t, "Star" + i, starColors[i % 2], stars[i, 2]);
                var rt = star.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(stars[i, 0], 1f - stars[i, 1]);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        void Blob(Transform parent, float nx, float nyFromTop, float size, Color color)
        {
            var blob = UIFactory.CreateCircle(parent, "Blob", color);
            var rt = blob.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(nx, 1f - nyFromTop);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }

        static void PlaceFromTopRight(RectTransform rt, float fromRight, float fromTop, float w, float h)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(-fromRight, -fromTop);
        }
    }
}
