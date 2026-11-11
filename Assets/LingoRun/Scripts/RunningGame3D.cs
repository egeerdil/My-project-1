using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// 3D lane-runner version of Running mode, styled after hypercasual gate
    /// runners: a straight track receding to the horizon, purple side rails,
    /// a cyan ball the player slides left/right, and translucent gates that
    /// sweep toward the camera. Each gate group shows three Spanish words —
    /// steer through the one matching the English prompt in the HUD.
    ///
    /// The world is built from Unity primitives with code-generated URP-unlit
    /// materials (flat look, no lighting or assets needed). The HUD is uGUI on
    /// the shared canvas. Scoring / lives / review / mastery logic is identical
    /// to the 2D port (and to lingorun_v48.jsx).
    /// </summary>
    public class RunningGame3D : MonoBehaviour
    {
        [System.NonSerialized] public LingoRunBootstrap bootstrap;

        // ---------- 3D tuning ----------
        const float TrackHalfWidth = 3.0f;   // playable half-width
        const float LaneWidth = 2.0f;        // lane centers at -2, 0, +2
        const float PlayerZ = 0f;
        const float SpawnZ = 78f;
        const float CullZ = -14f;
        const float GateHeight = 2.3f;
        // px -> world-unit conversion for the shared DifficultyConfig values
        const float PxToUnits = 0.095f;

        // ---------- world ----------
        GameObject _world;
        Transform _gateLayer;
        Transform _player;
        TrailRenderer _trail;
        readonly List<Transform> _stripes = new List<Transform>();
        const int StripeCount = 26;
        const float StripeGap = 8f;

        // Scrolling side scenery (hills, trees, lane dashes) — each entry loops
        // back by its own length when it passes the camera, giving parallax depth.
        struct SceneryItem { public Transform t; public float loop; }
        readonly List<SceneryItem> _scenery = new List<SceneryItem>();
        const float SceneryLoop = 130f;

        // Every scenery "family" is built once, up front (see BuildSideScenery)
        // and just toggled active/inactive per theme — rebuilding geometry isn't
        // cheap enough to do on every background switch, so instead we build the
        // generic tree scenery PLUS one real-pack-model prop scenery set per
        // background theme that wants one (GameConfig.Theme.BackgroundTheme.
        // sceneryPropPrefabs — real buildings for Downtown, a real space shuttle
        // for Rocket Launch, etc.) and hide whichever sets the current theme
        // doesn't want. Keyed by theme name; see BuildThemedPropScenery.
        readonly List<GameObject> _treeSceneryObjs = new List<GameObject>();
        readonly Dictionary<string, List<GameObject>> _themedSceneryObjs = new Dictionary<string, List<GameObject>>();

        Material _matGoodGate, _matBadGate, _matFadedGate;
        readonly Material[] _matLaneGate = new Material[3];
        ParticleSystem _confetti;

        // Equipped run background (sky/fog/ground/scenery colors), chosen in the Shop.
        Theme.BackgroundTheme _bgTheme;

        // Optional real scenic backdrop (baked from the "Low Poly Atmospheric
        // Locations Pack" — see Editor/IslandThumbnailBaker.cs) shown far
        // behind the track for background themes that have one. Null theme
        // backdropSprite (the original 3 procedural themes) just hides this.
        Renderer _backdropRenderer;
        const float DefaultFogEnd = 118f;
        const float BackdropFogEnd = 260f; // pushed back so the image clears the fog before it's visible

        // ---------- gameplay state ----------
        class Group
        {
            public float z;
            public WordEntry[] words;
            public int correctLane;
            public WordEntry correct;
            public bool resolved;
            public int resolvedPlayerLane = -1;
            public GameObject rootGo;
            public Renderer[] panels;
            public Text[] labels;
            public Transform[] labelRoots;   // per-lane world canvases (distance-scaled)
            public string promptTpl;         // sentence mode: template with blank
            public string promptHint;        // sentence mode: English meaning
            public bool speech;              // speech mode: one full-width gate
            public bool opened;              // speech mode: pronounced in time
        }

        // Fixed pastel tint per lane — links the HUD chips to the 3D gates
        // without hinting at which answer is correct.
        static readonly Color[] LaneTints =
        {
            Theme.Hex("FF9EB8"), // left: pink
            Theme.Hex("8FDDF9"), // middle: sky
            Theme.Hex("FFD97A"), // right: sunshine
        };

        readonly List<Group> _groups = new List<Group>();
        readonly HashSet<string> _sessionMissed = new HashSet<string>();
        readonly HashSet<string> _sessionLearned = new HashSet<string>();

        float _playerX;
        float _targetX;
        int _score, _lives, _streak, _bestStreak;
        float _elapsed;
        int _challengeCount;
        bool _reviewMode;
        bool _wrongPause;
        float _wrongPauseTimer;
        bool _userPaused;
        bool _ending;
        DifficultyConfig _cfg;
        List<WordEntry> _pool;
        List<SentenceEntry> _sentencePool;
        bool _sentenceMode;
        bool _steering;

        // Speech mode ("SPEAK" on Home): every gate is one full-width CLOSED
        // wall showing a word — say it out loud before you reach it and the
        // gate lifts open; stay silent (or get it wrong) and you smack into
        // it. Uses the native recognizer (NativeSpeech); free like Review
        // (recorded as a practice run: no level advance, no heart loss).
        bool _speechMode;
        bool _speechModeRequest;

        // Free play (game picker): endless casual run — no fixed level set,
        // no map/level advancement, never costs hearts. Coins/mastery/review
        // pool still count, so it always contributes to learning.
        bool _freePlay;
        bool _freePlayRequest;
        Group _listenGroup;      // the group we're currently listening for
        string _lastHeard = "";
        bool _micDenied;

        // Level mode (any non-review run): a fixed set shown up front, each
        // item appearing RepeatsPerWord times; finish all gates to clear the
        // level. Words and Sentences each get their own fixed set/queue (the
        // content differs) but share the same ProgressManager.CurrentLevel
        // counter, so clearing a level in EITHER mode advances the same
        // curriculum/map frontier.
        bool _levelMode;
        List<WordEntry> _levelWords;
        List<WordEntry> _gateQueue;
        List<SentenceEntry> _levelSentences;
        List<SentenceEntry> _sentenceGateQueue;
        bool _previewActive;
        float _previewTimer;

        // No-repeat within one run: keys already served by the random (review)
        // pickers. Cleared each BeginRun, and again if the pool runs dry.
        readonly HashSet<string> _servedThisRun = new HashSet<string>();

        // ---------- HUD ----------
        bool _built;
        RectTransform _root;
        Text _scoreText, _streakText, _promptText, _promptCaption, _promptHint;
        RectTransform _promptCardRt;
        // Shape slots 0-7 are the original procedural primitives (sphere,
        // cube, ...); 8+ are real vehicle models (BuildVehicleShapes) —
        // same "ball" system, just with purchased-pack meshes instead of
        // primitives for the later slots. See VehicleShapes below.
        // (GameConfig.PrimitiveShapeCount is the shared source of truth —
        // BallScreenUI's shop catalogue reads it too.)
        const int PrimitiveShapeCount = GameConfig.PrimitiveShapeCount;
        // How big (world units, longer of width/length) each vehicle shape
        // is scaled to — see the comment at the FitToBall call below.
        const float VehicleTargetFootprint = 1.7f;
        static readonly VehicleShapeDef[] VehicleShapes =
        {
            new VehicleShapeDef { resourceName = "vehicle_sedan" },
            new VehicleShapeDef { resourceName = "vehicle_suv_yellow" },
            new VehicleShapeDef { resourceName = "vehicle_suv_red" },
            new VehicleShapeDef { resourceName = "vehicle_cart" },
            new VehicleShapeDef { resourceName = "vehicle_boat" },
            new VehicleShapeDef { resourceName = "vehicle_japanese_boat" },
            new VehicleShapeDef { resourceName = "vehicle_destroyed_ship" },
            new VehicleShapeDef { resourceName = "vehicle_freight_wagon" },
            new VehicleShapeDef { resourceName = "vehicle_passenger_wagon" },
            new VehicleShapeDef { resourceName = "vehicle_train" },
            new VehicleShapeDef { resourceName = "space_shuttle" },
        };
        /// <summary>Vehicle shape lookups for other screens (the Shop's 3D
        /// preview renders the same prefabs — see ShapePreview3D).</summary>
        public static int VehicleShapeCount { get { return VehicleShapes.Length; } }
        public static string VehicleShapeResource(int v) { return VehicleShapes[v].resourceName; }
        public static float VehicleShapeYaw(int v) { return VehicleShapes[v].yawDegrees; }

        struct VehicleShapeDef
        {
            public string resourceName;
            // Most of the pack's vehicles are modeled nose-forward along
            // +Z (matches this track's "into the screen" direction) so 0
            // works as-is. If a specific model turns out backwards/
            // mirrored on the track, adjust its yaw here — that's a visual
            // call that needs to be checked in the Editor, not guessed here.
            public float yawDegrees;
        }
        readonly GameObject[] _shapeObjs = new GameObject[PrimitiveShapeCount + 11];
        readonly Renderer[] _shapeRenderers = new Renderer[PrimitiveShapeCount + 11];
        // True while a vehicle (not a primitive) shape is equipped — Update()
        // uses this to skip the rolling-ball spin (see BeginRun/Update).
        bool _activeShapeIsVehicle;
        Image[] _hearts = new Image[3];

        RectTransform _goodFeedback;
        Text _goodText;
        float _goodTimer;

        RectTransform _badFeedback;
        Text _badWord, _badMeaning;
        float _badTimer;

        RectTransform _pauseOverlay;

        // Level preview overlay
        RectTransform _previewOverlay;
        Text _previewTitle;
        Text _previewCountdown;
        readonly Text[] _previewRows = new Text[GameConfig.WordsPerLevel];

        // Bottom HUD chips: always-crisp copies of the active gate words, one per
        // lane, tinted to match the 3D gates. The player's current lane is highlighted.
        readonly Image[] _chips = new Image[3];
        readonly Text[] _chipTexts = new Text[3];
        float _chipFlashTimer;
        Group _chipGroup;

        // ==================== lifecycle ====================

        /// <summary>Speech mode entry (SPEAK on Home / speak again): the same
        /// runner, but gates open by voice. See _speechMode.</summary>
        public void BeginSpeechRun(bool review)
        {
            _speechModeRequest = true;
            BeginRun(review);
        }

        /// <summary>Free-play entry (game picker): endless run in the current
        /// mode (Words/Sentences), practice rules — see _freePlay.</summary>
        public void BeginFreePlay()
        {
            _freePlayRequest = true;
            BeginRun(false);
        }

        public void BeginRun(bool review)
        {
            EnsureBuilt();
            _world.SetActive(true);

            _speechMode = _speechModeRequest;
            _speechModeRequest = false;
            _freePlay = _freePlayRequest;
            _freePlayRequest = false;
            _reviewMode = review;
            _sentenceMode = !_speechMode && bootstrap.CurrentMode == GameMode.Sentences;
            // Level mode now covers both Words and Sentences (not just
            // Words) — any non-review run plays a fixed level set and can
            // complete it to advance ProgressManager.CurrentLevel. Free play
            // opts out: endless weighted pool, nothing at stake.
            _levelMode = !_reviewMode && !_freePlay;
            _score = 0; _lives = 3; _streak = 0; _bestStreak = 0;
            _elapsed = 0f; _challengeCount = 0;
            _playerX = 0f; _targetX = 0f;
            _wrongPause = false; _userPaused = false; _ending = false; _steering = false;
            _sessionMissed.Clear();
            _sessionLearned.Clear();
            _servedThisRun.Clear();
            ClearGroups();

            _cfg = GameConfig.Get(bootstrap.CurrentDifficulty);
            BuildPool();

            // Re-apply the equipped background theme every run — it may have
            // changed in the Shop since the world was first built.
            ApplyBackgroundTheme();

            // Apply the selected ball color + shape + trail (Shop picks).
            int skin = Mathf.Clamp(ProgressManager.SelectedSkin, 0, Theme.BallSkins.Length - 1);
            int shape = Mathf.Clamp(ProgressManager.SelectedShape, 0, _shapeObjs.Length - 1);
            Color skinColor = Theme.BallSkins[skin];
            for (int i = 0; i < _shapeObjs.Length; i++)
                _shapeObjs[i].SetActive(i == shape);
            _activeShapeIsVehicle = shape >= PrimitiveShapeCount;
            // Vehicles keep their own real paint job (car body texture/
            // color) instead of the Shop's flat "ball skin" tint — picking
            // a vehicle shape is itself the color choice.
            if (!_activeShapeIsVehicle && _shapeRenderers[shape] != null)
                _shapeRenderers[shape].sharedMaterial = MatCache("ballSkin" + skin, skinColor, false);
            _player.rotation = Quaternion.identity;
            ApplyTrailStyle(Mathf.Clamp(ProgressManager.SelectedTrail, 0, 3), skinColor);

            // Speech mode: mic + recognition permission, fresh listen state.
            _listenGroup = null;
            _lastHeard = "";
            _micDenied = false;
            if (_speechMode)
            {
                NativeSpeech.RequestAuth(delegate (bool ok) { _micDenied = !ok; });
            }
            else
            {
                NativeSpeech.StopListening();
            }

            // Prompt card layout differs per mode (sentences need more room).
            _promptCaption.text = _speechMode ? Loc.T("run.say_gate")
                : _sentenceMode ? Loc.T("run.fill_blank") : Loc.T("run.go_match");
            _promptText.fontSize = _sentenceMode ? 17 : 26;
            _promptText.horizontalOverflow = _sentenceMode ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            _promptHint.gameObject.SetActive(_sentenceMode || _speechMode);
            _promptCardRt.sizeDelta = new Vector2(_promptCardRt.sizeDelta.x,
                (_sentenceMode || _speechMode) ? 86f : 66f);

            _player.position = new Vector3(0f, 0.5f, PlayerZ);
            if (_trail != null) _trail.Clear();

            _goodFeedback.gameObject.SetActive(false);
            _badFeedback.gameObject.SetActive(false);
            _pauseOverlay.gameObject.SetActive(false);
            _promptText.text = "-";
            _chipGroup = null;
            _chipFlashTimer = 0f;
            for (int i = 0; i < 3; i++)
                if (_chipTexts[i] != null) _chipTexts[i].text = "-";
            UpdateHearts();
            UpdateScoreText();

            // Level mode: Words shows a "memorize these" preview before the run;
            // Sentences skips it (a list of English hints wasn't useful) but
            // still needs its fixed level set built.
            if (_levelMode && !_sentenceMode && !_speechMode)
            {
                ShowPreview();
            }
            else
            {
                if (_levelMode && _sentenceMode) BuildSentenceLevelSet();
                else if (_levelMode && _speechMode)
                {
                    // Speech level: this level's words, each spoken ONCE
                    // (8 gates — saying each word twice would drag).
                    BuildLevelSet();
                    _gateQueue.Clear();
                    _gateQueue.AddRange(_levelWords);
                }
                _previewActive = false;
                _previewOverlay.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Build the fixed word set for the current level: sequential slices of
        /// the (stable-ordered) unlocked vocabulary, so levels form a curriculum
        /// — level 1 teaches the first 8 words, level 2 the next 8, and so on
        /// (wrapping around once the pool is exhausted).
        /// </summary>
        void BuildLevelSet()
        {
            // Themed units: this level's words come from its unit's topic
            // ("food", "travel", …) — the curriculum puts that topic's words
            // first, and PriorSlicesInTopic tells us how far into them this
            // level's slice starts (spilling into general vocab only once
            // the theme's own words run out).
            int w = GameConfig.WordsPerLevel;
            int level = ProgressManager.CurrentLevel;
            string topic = GameConfig.UnitTopicForLevel(level);
            var pool = VocabDatabase.BuildCurriculum(
                new List<string> { topic }, ProgressManager.A2Unlocked);
            _levelWords = new List<WordEntry>(w);
            if (pool.Count > 0)
            {
                int start = (GameConfig.PriorSlicesInTopic(level, GameMode.Words) * w) % pool.Count;
                for (int i = 0; i < w; i++)
                    _levelWords.Add(pool[(start + i) % pool.Count]);
            }

            // Gate queue: each word RepeatsPerWord times, shuffled.
            _gateQueue = new List<WordEntry>(w * GameConfig.RepeatsPerWord);
            for (int r = 0; r < GameConfig.RepeatsPerWord; r++)
                _gateQueue.AddRange(_levelWords);
            for (int i = _gateQueue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = _gateQueue[i]; _gateQueue[i] = _gateQueue[j]; _gateQueue[j] = tmp;
            }
        }

        /// <summary>Sentence-mode counterpart of BuildLevelSet: sequential
        /// slices of the (stable-ordered) unlocked sentence pool, keyed by
        /// the SAME ProgressManager.CurrentLevel as Words — so "level 4"
        /// means the 4th chunk of whichever content this run's mode teaches.</summary>
        void BuildSentenceLevelSet()
        {
            // Same themed-unit slicing as BuildLevelSet, over the sentence pool.
            int w = GameConfig.WordsPerLevel;
            int level = ProgressManager.CurrentLevel;
            string topic = GameConfig.UnitTopicForLevel(level);
            var pool = SentenceDatabase.BuildCurriculum(
                new List<string> { topic }, ProgressManager.A2Unlocked);
            _levelSentences = new List<SentenceEntry>(w);
            if (pool.Count > 0)
            {
                int start = (GameConfig.PriorSlicesInTopic(level, GameMode.Sentences) * w) % pool.Count;
                for (int i = 0; i < w; i++)
                    _levelSentences.Add(pool[(start + i) % pool.Count]);
            }

            _sentenceGateQueue = new List<SentenceEntry>(w * GameConfig.RepeatsPerWord);
            for (int r = 0; r < GameConfig.RepeatsPerWord; r++)
                _sentenceGateQueue.AddRange(_levelSentences);
            for (int i = _sentenceGateQueue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = _sentenceGateQueue[i]; _sentenceGateQueue[i] = _sentenceGateQueue[j]; _sentenceGateQueue[j] = tmp;
            }
        }

        /// <summary>Words mode only — Sentences goes straight into the run.</summary>
        void ShowPreview()
        {
            BuildLevelSet();
            _previewTitle.text = Loc.F("run.level_preview", ProgressManager.CurrentLevel,
                Loc.Topic(GameConfig.UnitTopicForLevel(ProgressManager.CurrentLevel)));
            for (int i = 0; i < _previewRows.Length; i++)
            {
                if (i < _levelWords.Count)
                {
                    _previewRows[i].gameObject.SetActive(true);
                    _previewRows[i].text = _levelWords[i].tgt + "   =   " + _levelWords[i].en;
                }
                else _previewRows[i].gameObject.SetActive(false);
            }
            _previewTimer = GameConfig.PreviewSecondsBase
                + GameConfig.PreviewSecondsPerWord * _levelWords.Count;
            _previewActive = true;
            _previewOverlay.gameObject.SetActive(true);
        }

        void ClosePreview()
        {
            _previewActive = false;
            _previewOverlay.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (_world != null) _world.SetActive(false);
            if (_speechMode) { NativeSpeech.StopListening(); _listenGroup = null; }
        }

        void BuildPool()
        {
            if (_sentenceMode)
            {
                BuildSentencePool();
                return;
            }
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

        void BuildSentencePool()
        {
            var missed = ProgressManager.MissedKeysFor(true);
            if (_reviewMode && missed.Count > 0)
            {
                var reviewSents = new List<SentenceEntry>();
                foreach (var key in missed)
                {
                    var s = SentenceDatabase.FindByKey(key);
                    if (s != null) reviewSents.Add(s);
                }
                if (reviewSents.Count == 0)
                {
                    _sentencePool = SentenceDatabase.BuildPool(ProgressManager.A2Unlocked);
                    return;
                }
                _sentencePool = new List<SentenceEntry>(reviewSents);
                if (reviewSents.Count < 8)
                {
                    _sentencePool.AddRange(reviewSents);
                    _sentencePool.AddRange(reviewSents);
                }
                return;
            }

            _sentencePool = SentenceDatabase.BuildPool(ProgressManager.A2Unlocked);
            foreach (var key in missed)
            {
                var s = SentenceDatabase.FindByKey(key);
                if (s == null) continue;
                _sentencePool.Add(s); _sentencePool.Add(s); _sentencePool.Add(s);
            }
        }

        void Update()
        {
            if (!_built || _ending) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            // Level preview: freeze everything, count down, then start.
            if (_previewActive)
            {
                _previewTimer -= dt;
                _previewCountdown.text = Loc.F("run.starts_in", Mathf.CeilToInt(Mathf.Max(0f, _previewTimer)));
                if (_previewTimer <= 0f) ClosePreview();
                return;
            }

            HandleInput();
            if (_userPaused) return;

            TickFeedback(dt);
            MovePlayer(dt);
            UpdateChips(dt);

            if (_wrongPause)
            {
                _wrongPauseTimer -= dt;
                if (_wrongPauseTimer <= 0f) _wrongPause = false;
                return;
            }

            _elapsed += dt;
            float ramp = Mathf.Min(1f, _elapsed * 1000f / _cfg.rampMs);
            float speed = (_cfg.speedStart + (_cfg.speedMax - _cfg.speedStart) * ramp) * PxToUnits;
            float gap = _cfg.spawnGap * PxToUnits;
            if (_speechMode)
            {
                // Speaking takes longer than steering: slower gates, wider
                // spacing, no speed ramp.
                speed = _cfg.speedStart * PxToUnits * 0.8f;
                gap *= 2.2f;
            }

            // Distance-based spawning (constant gap between gate groups).
            // In level mode, stop once the level's gate queue is exhausted
            // (Words and Sentences each track their own queue).
            bool canSpawn = !_levelMode || (_sentenceMode
                ? (_sentenceGateQueue != null && _sentenceGateQueue.Count > 0)
                : (_gateQueue != null && _gateQueue.Count > 0));
            Group last = _groups.Count > 0 ? _groups[_groups.Count - 1] : null;
            if (canSpawn && (last == null || (SpawnZ - last.z) >= gap))
                _groups.Add(MakeGroup());

            // Level complete: no gates left to spawn and every group resolved.
            if (_levelMode && !canSpawn && !_ending)
            {
                bool allResolved = true;
                for (int i = 0; i < _groups.Count; i++)
                    if (!_groups[i].resolved) { allResolved = false; break; }
                if (allResolved)
                {
                    StartCoroutine(EndRun(true));
                    return;
                }
            }

            float dz = speed * dt;
            Group collision = null;
            for (int gi = 0; gi < _groups.Count; gi++)
            {
                var g = _groups[gi];
                g.z -= dz;
                g.rootGo.transform.position = new Vector3(0f, 0f, g.z);
                if (!g.resolved && g.z < PlayerZ + 0.4f)
                {
                    g.resolved = true;
                    g.resolvedPlayerLane = CurrentLane();
                    collision = g;
                }
            }

            // Scroll road slabs for a sense of speed.
            for (int i = 0; i < _stripes.Count; i++)
            {
                var s = _stripes[i];
                var p = s.position;
                p.z -= dz;
                if (p.z < -12f) p.z += StripeCount * StripeGap;
                s.position = p;
            }

            // Scroll side scenery (hills, trees, lane dashes) with per-item loops.
            for (int i = 0; i < _scenery.Count; i++)
            {
                var item = _scenery[i];
                var p = item.t.position;
                p.z -= dz;
                if (p.z < -14f) p.z += item.loop;
                item.t.position = p;
            }

            // Cull passed groups.
            for (int gi = _groups.Count - 1; gi >= 0; gi--)
            {
                if (_groups[gi].z < CullZ)
                {
                    Destroy(_groups[gi].rootGo);
                    _groups.RemoveAt(gi);
                }
            }

            // Rolling-ball spin (pure juice — the world moves, the ball "rolls").
            // Vehicles drive instead of tumbling: skip the spin so a car/
            // boat/train stays upright and just slides between lanes, the
            // way an actual vehicle moves, instead of rotating end-over-end.
            if (!_activeShapeIsVehicle)
                _player.Rotate(dz / 0.5f * Mathf.Rad2Deg, 0f, 0f, Space.Self);

            if (_speechMode)
            {
                ManageListening();
                AnimateOpenGates(dt);
            }

            UpdateLabelScales();
            if (collision != null) Resolve(collision);
            UpdatePrompt();
        }

        // ==================== speech mode ====================

        /// <summary>Keep the recognizer pointed at the nearest unresolved
        /// gate: whenever that gate changes, restart listening with a fresh
        /// transcript so earlier speech can't open later gates.</summary>
        void ManageListening()
        {
            Group nearest = null;
            for (int i = 0; i < _groups.Count; i++)
                if (!_groups[i].resolved) { nearest = _groups[i]; break; }

            if (nearest == _listenGroup) return;
            _listenGroup = nearest;
            _lastHeard = "";
            NativeSpeech.StopListening();
            if (nearest == null) return;

            var target = nearest.correct;
            var forGroup = nearest;
            NativeSpeech.EditorSimulatedResult = target.tgt;
            NativeSpeech.StartListening(
                delegate (string partial) { OnHeard(forGroup, partial); },
                delegate (string final) { OnHeard(forGroup, final); },
                delegate (string err) { /* keep running; gate just won't open */ });
        }

        void OnHeard(Group g, string text)
        {
            if (!_speechMode || _ending || g == null || g.resolved || g.opened) return;
            _lastHeard = text ?? "";
            if (PronunciationGameUI.Matches(text, g.correct.tgt)) OpenGate(g);
        }

        /// <summary>Pronounced in time — the wall turns green and lifts.</summary>
        void OpenGate(Group g)
        {
            g.opened = true;
            ProceduralAudio.PlayCorrect();
            if (g.panels != null && g.panels[0] != null)
                g.panels[0].sharedMaterial = _matGoodGate;
            if (_confetti != null)
            {
                _confetti.transform.position = new Vector3(0f, 1.4f, Mathf.Max(1.6f, g.z * 0.4f));
                _confetti.Play();
            }
        }

        void AnimateOpenGates(float dt)
        {
            for (int i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i];
                if (!g.speech || !g.opened || g.panels == null || g.panels[0] == null) continue;
                var pt = g.panels[0].transform;
                var p = pt.localPosition;
                float targetY = GateHeight * 1.9f;
                if (p.y < targetY)
                {
                    p.y = Mathf.Min(targetY, p.y + dt * 7f);
                    pt.localPosition = p;
                }
            }
        }

        /// <summary>
        /// Trail styles (Ball Shop): 0 classic, 1 long, 2 wide, 3 rainbow.
        /// Uses a vertex-color shader so the TrailRenderer's gradient shows.
        /// </summary>
        void ApplyTrailStyle(int style, Color skinColor)
        {
            if (_trail.material == null || _trail.material.shader == null ||
                !_trail.material.shader.name.Contains("Sprites"))
            {
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) _trail.material = new Material(sh);
            }

            _trail.time = style == 1 ? 1.2f : (style == 3 ? 0.9f : 0.55f);
            _trail.startWidth = style == 2 ? 1.05f : 0.55f;
            _trail.endWidth = 0.05f;

            var g = new Gradient();
            if (style == 3)
            {
                g.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Theme.Hex("FF5E5E"), 0f),
                        new GradientColorKey(Theme.Hex("FFC94D"), 0.25f),
                        new GradientColorKey(Theme.Hex("6BE585"), 0.5f),
                        new GradientColorKey(Theme.Hex("4DC3FF"), 0.75f),
                        new GradientColorKey(Theme.Hex("B06BFF"), 1f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.85f, 0f),
                        new GradientAlphaKey(0f, 1f),
                    });
            }
            else
            {
                Color light = new Color(
                    Mathf.Lerp(skinColor.r, 1f, 0.45f),
                    Mathf.Lerp(skinColor.g, 1f, 0.45f),
                    Mathf.Lerp(skinColor.b, 1f, 0.45f));
                g.SetKeys(
                    new[] { new GradientColorKey(light, 0f), new GradientColorKey(light, 1f) },
                    new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
            }
            _trail.colorGradient = g;
        }

        int CurrentLane()
        {
            if (_playerX < -LaneWidth * 0.5f) return 0;
            if (_playerX > LaneWidth * 0.5f) return 2;
            return 1;
        }

        static float LaneX(int lane) { return (lane - 1) * LaneWidth; }

        // ==================== challenge lifecycle ====================

        Group MakeGroup()
        {
            WordEntry correct;
            List<WordEntry> all;
            string promptTpl = null, promptHint = null;

            if (_speechMode)
            {
                if (_levelMode && _gateQueue != null && _gateQueue.Count > 0)
                {
                    correct = _gateQueue[0];
                    _gateQueue.RemoveAt(0);
                }
                else
                {
                    var candidates = new List<WordEntry>();
                    foreach (var w in _pool)
                        if (!_servedThisRun.Contains(w.en)) candidates.Add(w);
                    if (candidates.Count < 1) { _servedThisRun.Clear(); candidates = _pool; }
                    correct = VocabDatabase.WeightedPick(candidates, ProgressManager.DifficultyScores);
                    _servedThisRun.Add(correct.en);
                }
                _challengeCount++;
                return MakeSpeechGroup(correct);
            }

            if (_sentenceMode)
            {
                SentenceEntry sent;
                if (_levelMode && _sentenceGateQueue != null && _sentenceGateQueue.Count > 0)
                {
                    // Level mode: next sentence from the fixed queue.
                    sent = _sentenceGateQueue[0];
                    _sentenceGateQueue.RemoveAt(0);
                }
                else
                {
                    // Review picks: avoid re-serving a sentence already shown
                    // this run while other options remain.
                    var candidates = new List<SentenceEntry>();
                    foreach (var s in _sentencePool)
                        if (!_servedThisRun.Contains(SentenceDatabase.Key(s))) candidates.Add(s);
                    if (candidates.Count < 1) { _servedThisRun.Clear(); candidates = _sentencePool; }
                    sent = SentenceDatabase.WeightedPick(candidates, ProgressManager.DifficultyScores);
                    _servedThisRun.Add(SentenceDatabase.Key(sent));
                }
                string key = SentenceDatabase.Key(sent);
                correct = new WordEntry { en = key, tgt = sent.correct };
                all = new List<WordEntry> { correct };
                foreach (var d in sent.distractors)
                    all.Add(new WordEntry { en = key + "#d", tgt = d });
                promptTpl = sent.tpl.Replace("___", "____");
                promptHint = "\"" + sent.hint + "\"";
            }
            else if (_levelMode && _gateQueue != null && _gateQueue.Count > 0)
            {
                // Level mode: next word from the fixed queue.
                correct = _gateQueue[0];
                _gateQueue.RemoveAt(0);
                all = new List<WordEntry> { correct };
                all.AddRange(VocabDatabase.PickDistractors(correct, 2));
            }
            else
            {
                // Review picks: avoid re-serving a word already shown this run
                // while other options remain.
                var candidates = new List<WordEntry>();
                foreach (var w in _pool)
                    if (!_servedThisRun.Contains(w.en)) candidates.Add(w);
                if (candidates.Count < 1) { _servedThisRun.Clear(); candidates = _pool; }
                correct = VocabDatabase.WeightedPick(candidates, ProgressManager.DifficultyScores);
                _servedThisRun.Add(correct.en);
                all = new List<WordEntry> { correct };
                all.AddRange(VocabDatabase.PickDistractors(correct, 2));
            }

            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var tmp = all[i]; all[i] = all[j]; all[j] = tmp;
            }
            int correctLane = all.IndexOf(correct);
            _challengeCount++;

            var g = new Group
            {
                z = SpawnZ,
                words = all.ToArray(),
                correctLane = correctLane,
                correct = correct,
                promptTpl = promptTpl,
                promptHint = promptHint,
                panels = new Renderer[3],
                labels = new Text[3],
            };

            g.rootGo = new GameObject("GateGroup");
            g.rootGo.transform.SetParent(_gateLayer, false);
            g.rootGo.transform.position = new Vector3(0f, 0f, g.z);
            g.labelRoots = new Transform[3];

            for (int i = 0; i < 3; i++)
            {
                // Gate panel (translucent, lane-tinted) + capsule posts.
                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Gate" + i;
                Destroy(panel.GetComponent<Collider>());
                panel.transform.SetParent(g.rootGo.transform, false);
                panel.transform.localPosition = new Vector3(LaneX(i), GateHeight * 0.5f, 0f);
                panel.transform.localScale = new Vector3(LaneWidth - 0.15f, GateHeight, 0.12f);
                var rend = panel.GetComponent<Renderer>();
                rend.sharedMaterial = _matLaneGate[i];
                rend.shadowCastingMode = ShadowCastingMode.Off;
                g.panels[i] = rend;

                for (int side = -1; side <= 1; side += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    post.name = "Post";
                    Destroy(post.GetComponent<Collider>());
                    post.transform.SetParent(g.rootGo.transform, false);
                    post.transform.localPosition = new Vector3(LaneX(i) + side * (LaneWidth - 0.15f) * 0.5f,
                        (GateHeight + 0.30f) * 0.5f, 0f);
                    post.transform.localScale = new Vector3(0.13f, (GateHeight + 0.30f) * 0.5f, 0.13f);
                    post.GetComponent<Renderer>().sharedMaterial = MatCache("post", Theme.Hex("352C4E"), false);
                }

                // Per-lane floating label: dark rounded pill + big white text on its
                // own world canvas so it can be distance-scaled independently.
                var canvasGo = new GameObject("Label" + i, typeof(RectTransform), typeof(Canvas));
                canvasGo.transform.SetParent(g.rootGo.transform, false);
                var canvasRt = canvasGo.GetComponent<RectTransform>();
                canvasRt.sizeDelta = new Vector2(210f, 74f);
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                canvasGo.transform.localScale = Vector3.one * LabelBaseScale;
                // Middle lane sits higher so the three labels never collide
                // horizontally, even when distance-scaled up.
                canvasGo.transform.localPosition = new Vector3(LaneX(i),
                    GateHeight + (i == 1 ? 1.35f : 0.55f), -0.1f);
                g.labelRoots[i] = canvasGo.transform;

                UIFactory.CreatePanel(canvasGo.transform, "Pill",
                    new Color(0.16f, 0.12f, 0.24f, 0.88f), 30f);

                var label = UIFactory.CreateText(canvasGo.transform, "Word", g.words[i].tgt,
                    30, Color.white);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 30;
                var lrt = label.rectTransform;
                lrt.offsetMin = new Vector2(8f, 4f);
                lrt.offsetMax = new Vector2(-8f, -4f);

                // Small lane-colored tick under the pill (matches the HUD chip).
                var tick = UIFactory.CreatePanel(canvasGo.transform, "Tick", LaneTints[i], 4f);
                var trt = tick.rectTransform;
                trt.anchorMin = new Vector2(0.5f, 0f);
                trt.anchorMax = new Vector2(0.5f, 0f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.sizeDelta = new Vector2(46f, 8f);
                trt.anchoredPosition = new Vector2(0f, 0f);

                g.labels[i] = label;
            }
            return g;
        }

        /// <summary>Speech mode gate: ONE closed wall across the whole track
        /// with the word on it. It spawns "locked" (red-tinted); saying the
        /// word switches it green and lifts it (OpenGate/AnimateOpenGates).</summary>
        Group MakeSpeechGroup(WordEntry correct)
        {
            var g = new Group
            {
                z = SpawnZ,
                words = new[] { correct },
                correctLane = 1,
                correct = correct,
                speech = true,
                panels = new Renderer[1],
                labels = new Text[1],
                labelRoots = new Transform[3], // [1] used; UpdateLabelScales null-checks the rest
            };

            g.rootGo = new GameObject("SpeechGate");
            g.rootGo.transform.SetParent(_gateLayer, false);
            g.rootGo.transform.position = new Vector3(0f, 0f, g.z);

            // Full-width closed wall.
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Wall";
            Destroy(panel.GetComponent<Collider>());
            panel.transform.SetParent(g.rootGo.transform, false);
            panel.transform.localPosition = new Vector3(0f, GateHeight * 0.5f, 0f);
            panel.transform.localScale = new Vector3(TrackHalfWidth * 2f - 0.15f, GateHeight, 0.14f);
            var rend = panel.GetComponent<Renderer>();
            rend.sharedMaterial = _matBadGate;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            g.panels[0] = rend;

            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                post.name = "Post";
                Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(g.rootGo.transform, false);
                post.transform.localPosition = new Vector3(side * (TrackHalfWidth - 0.1f),
                    (GateHeight + 0.30f) * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.15f, (GateHeight + 0.30f) * 0.5f, 0.15f);
                post.GetComponent<Renderer>().sharedMaterial = MatCache("post", Theme.Hex("352C4E"), false);
            }

            // Single big centered label with the target word.
            var canvasGo = new GameObject("Label", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(g.rootGo.transform, false);
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(300f, 84f);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasGo.transform.localScale = Vector3.one * LabelBaseScale;
            canvasGo.transform.localPosition = new Vector3(0f, GateHeight + 0.9f, -0.1f);
            g.labelRoots[1] = canvasGo.transform;

            UIFactory.CreatePanel(canvasGo.transform, "Pill",
                new Color(0.16f, 0.12f, 0.24f, 0.88f), 34f);

            var label = UIFactory.CreateText(canvasGo.transform, "Word", correct.tgt, 36, Color.white);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = 36;
            var lrt = label.rectTransform;
            lrt.offsetMin = new Vector2(10f, 4f);
            lrt.offsetMax = new Vector2(-10f, -4f);
            g.labels[0] = label;

            return g;
        }

        const float LabelBaseScale = 0.0105f;

        /// <summary>
        /// Keep gate labels readable at range: scale each label canvas up with
        /// distance to the camera (clamped so neighbours don't overlap).
        /// </summary>
        void UpdateLabelScales()
        {
            // Only the two nearest unresolved groups show their labels — far
            // groups' labels stacked up behind each other were the main source
            // of the "overlapping words" mess. Their gates stay visible; the
            // words appear as the group becomes relevant.
            int unresolvedSeen = 0;
            for (int gi = 0; gi < _groups.Count; gi++)
            {
                var g = _groups[gi];
                if (g.labelRoots == null) continue;

                bool showLabels = true;
                if (!g.resolved)
                {
                    unresolvedSeen++;
                    showLabels = unresolvedSeen <= 2;
                }

                float dist = g.z + 7.2f; // camera sits at z = -7.2
                float comp = Mathf.Clamp(dist / 16f, 1f, 1.35f);
                float s = LabelBaseScale * comp;
                for (int i = 0; i < 3; i++)
                {
                    if (g.labelRoots[i] == null) continue;
                    if (g.labelRoots[i].gameObject.activeSelf != showLabels)
                        g.labelRoots[i].gameObject.SetActive(showLabels);
                    g.labelRoots[i].localScale = new Vector3(s, s, s);
                }

                // The middle label starts raised (so the three labels don't
                // collide at distance-scale) and glides down to the side
                // labels' height as its gate approaches the player.
                if (g.labelRoots[1] != null)
                {
                    float raise = Mathf.Lerp(0.55f, 1.35f, Mathf.InverseLerp(8f, 26f, g.z));
                    var lp = g.labelRoots[1].localPosition;
                    lp.y = GateHeight + raise;
                    g.labelRoots[1].localPosition = lp;
                }
            }
        }

        void Resolve(Group g)
        {
            if (g.speech) { ResolveSpeech(g); return; }
            bool correct = g.resolvedPlayerLane == g.correctLane;
            if (correct)
            {
                ProceduralAudio.PlayCorrect();
                _score += 10;
                _streak += 1;
                _bestStreak = Mathf.Max(_bestStreak, _streak);
                if (_streak > 0 && _streak % 5 == 0) _score += 25;
                _sessionLearned.Add(g.correct.en);
                ShowGood(_sentenceMode
                    ? "+10  " + g.correct.tgt
                    : "+10  " + g.correct.tgt + " = " + g.correct.en);
            }
            else
            {
                ProceduralAudio.PlayWrong();
                // Say the word you missed — hearing it right after the
                // mistake is when pronunciation sticks best.
                NativeSpeech.Speak(g.correct.tgt);
                _lives -= 1;
                _streak = 0;
                _sessionMissed.Add(g.correct.en);
                ShowBad(g.correct.tgt, _sentenceMode ? g.promptHint : "= " + g.correct.en);
                UpdateHearts();
                if (_lives > 0)
                {
                    _wrongPause = true;
                    _wrongPauseTimer = GameConfig.WrongPauseSeconds;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                if (i == g.correctLane) g.panels[i].sharedMaterial = _matGoodGate;
                else if (!correct && i == g.resolvedPlayerLane) g.panels[i].sharedMaterial = _matBadGate;
                else g.panels[i].sharedMaterial = _matFadedGate;
            }

            // Mirror the verdict on the HUD chips for a moment.
            _chipFlashTimer = 0.7f;
            for (int i = 0; i < 3; i++)
            {
                if (_chips[i] == null) continue;
                if (i == g.correctLane)
                    _chips[i].color = Theme.Hex("4ADE80", 0.95f);
                else if (!correct && i == g.resolvedPlayerLane)
                    _chips[i].color = Theme.Hex("F87171", 0.95f);
                else
                    _chips[i].color = new Color(1f, 1f, 1f, 0.45f);
            }

            if (correct && _confetti != null)
            {
                _confetti.transform.position = new Vector3(LaneX(g.correctLane), 1.4f, 1.6f);
                _confetti.Play();
            }

            UpdateScoreText();
            if (_lives <= 0) StartCoroutine(EndRun(false));
        }

        /// <summary>Speech gate verdict: opened in time = pass, still closed
        /// = smack. The word is spoken back either way (reinforce on pass,
        /// teach on fail).</summary>
        void ResolveSpeech(Group g)
        {
            if (_listenGroup == g) { _listenGroup = null; NativeSpeech.StopListening(); }

            if (g.opened)
            {
                ProceduralAudio.PlayCorrect();
                _score += 15;
                _streak += 1;
                _bestStreak = Mathf.Max(_bestStreak, _streak);
                if (_streak > 0 && _streak % 5 == 0) _score += 25;
                _sessionLearned.Add(g.correct.en);
                ShowGood("+15  " + g.correct.tgt + " = " + g.correct.en);
            }
            else
            {
                ProceduralAudio.PlayWrong();
                NativeSpeech.Speak(g.correct.tgt);
                _lives -= 1;
                _streak = 0;
                _sessionMissed.Add(g.correct.en);
                ShowBad(g.correct.tgt, "= " + g.correct.en);
                UpdateHearts();
                if (_lives > 0)
                {
                    _wrongPause = true;
                    _wrongPauseTimer = GameConfig.WrongPauseSeconds;
                }
            }

            _lastHeard = "";
            UpdateScoreText();
            if (_lives <= 0) StartCoroutine(EndRun(false));
        }

        void UpdateChips(float dt)
        {
            if (_chips[0] == null) return;

            // While a verdict flash is showing, leave the chips alone.
            if (_chipFlashTimer > 0f)
            {
                _chipFlashTimer -= dt;
                if (_chipFlashTimer > 0f) return;
            }

            // Show the words of the nearest unresolved group.
            Group active = null;
            for (int i = 0; i < _groups.Count; i++)
                if (!_groups[i].resolved) { active = _groups[i]; break; }

            if (active != _chipGroup)
            {
                _chipGroup = active;
                for (int i = 0; i < 3; i++)
                    _chipTexts[i].text = active == null ? "-"
                        : active.speech ? (i == 1 ? active.correct.tgt : "-")
                        : active.words[i].tgt;
            }

            // Highlight whichever lane the ball is in right now.
            int lane = CurrentLane();
            for (int i = 0; i < 3; i++)
            {
                bool sel = i == lane;
                _chips[i].color = sel ? new Color(1f, 1f, 1f, 0.98f) : new Color(1f, 1f, 1f, 0.68f);
                _chips[i].rectTransform.localScale = Vector3.one * (sel ? 1.07f : 1f);
                _chipTexts[i].color = sel ? Theme.Ink : Theme.InkSoft;
            }
        }

        IEnumerator EndRun(bool levelCompleted)
        {
            _ending = true;
            if (_speechMode) { NativeSpeech.StopListening(); _listenGroup = null; }
            yield return new WaitForSeconds(0.6f);
            // Speech runs are practice (like Review): they never advance the
            // level/map and never cost hearts — recorded as a plain run.
            var results = ProgressManager.RecordRunEnd(
                _score, _bestStreak, _sessionMissed, _sessionLearned,
                _challengeCount, _reviewMode, _sentenceMode,
                _levelMode && !_speechMode, levelCompleted && !_speechMode,
                (_speechMode || _freePlay) ? 0 : bootstrap.ActiveMapLevel);
            if (_speechMode) results.isSpeech = true;
            if (_freePlay) results.isFreePlay = true;
            bootstrap.ShowGameOver(results);
        }

        void UpdatePrompt()
        {
            Group active = null;
            for (int i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i];
                if (!g.resolved) { active = g; break; }
            }
            if (active == null && _groups.Count > 0) active = _groups[0];
            if (active == null)
            {
                _promptText.text = "-";
                return;
            }
            if (_speechMode)
            {
                _promptText.text = active.correct.tgt;
                _promptHint.text = _micDenied
                    ? Loc.T("run.mic_needed")
                    : active.opened ? Loc.T("run.gate_open")
                    : string.IsNullOrEmpty(_lastHeard) ? "..." : "“" + _lastHeard + "”";
            }
            else if (_sentenceMode)
            {
                _promptText.text = active.promptTpl;
                _promptHint.text = active.promptHint;
            }
            else
            {
                _promptText.text = active.correct.en;
            }
        }

        // ==================== input ====================

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb != null && !_userPaused)
            {
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                    _targetX = LaneX(Mathf.Clamp(NearestLane(_targetX) - 1, 0, 2));
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                    _targetX = LaneX(Mathf.Clamp(NearestLane(_targetX) + 1, 0, 2));
            }

            var ptr = Pointer.current;
            if (ptr == null || _userPaused) return;

            if (ptr.press.wasPressedThisFrame)
            {
                if (PointerOverButton()) { _steering = false; return; }
                _steering = true;
            }
            if (ptr.press.wasReleasedThisFrame) _steering = false;

            if (_steering && ptr.press.isPressed)
            {
                // Map the horizontal screen position across the track width
                // (continuous slide, like the reference video).
                float nx = Mathf.Clamp01(ptr.position.ReadValue().x / Mathf.Max(1f, Screen.width));
                float usable = Mathf.InverseLerp(0.08f, 0.92f, nx); // dead margins at edges
                _targetX = Mathf.Lerp(-(TrackHalfWidth - 0.6f), TrackHalfWidth - 0.6f, usable);
            }
        }

        static int NearestLane(float x)
        {
            return Mathf.Clamp(Mathf.RoundToInt(x / LaneWidth) + 1, 0, 2);
        }

        bool PointerOverButton()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void MovePlayer(float dt)
        {
            _playerX = Mathf.Lerp(_playerX, _targetX, 1f - Mathf.Exp(-12f * dt));
            var p = _player.position;
            p.x = _playerX;
            _player.position = p;
        }

        // ==================== feedback (HUD) ====================

        void ShowGood(string msg)
        {
            _goodText.text = msg;
            UIFactory.PlaceTop(_goodFeedback, _root.rect.width * 0.5f, _root.rect.height * 0.62f,
                _root.rect.width - 80f, 44f);
            _goodFeedback.gameObject.SetActive(true);
            _goodTimer = 1.1f;
        }

        void ShowBad(string tgt, string meaning)
        {
            _badWord.text = tgt;
            _badMeaning.text = meaning;
            UIFactory.PlaceTop(_badFeedback, _root.rect.width * 0.5f, _root.rect.height * 0.42f,
                _root.rect.width - 60f, 140f);
            _badFeedback.gameObject.SetActive(true);
            _badTimer = 1.7f;
        }

        void TickFeedback(float dt)
        {
            if (_goodTimer > 0f)
            {
                _goodTimer -= dt;
                if (_goodTimer <= 0f) _goodFeedback.gameObject.SetActive(false);
            }
            if (_badTimer > 0f)
            {
                _badTimer -= dt;
                if (_badTimer <= 0f) _badFeedback.gameObject.SetActive(false);
            }
        }

        void UpdateHearts()
        {
            for (int i = 0; i < 3; i++)
                _hearts[i].color = i < _lives ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        void UpdateScoreText()
        {
            _scoreText.text = _score.ToString();
            _streakText.text = "x" + _streak;
        }

        // ==================== pause ====================

        void TogglePause(bool paused)
        {
            _userPaused = paused;
            _pauseOverlay.gameObject.SetActive(paused);
            _steering = false;
        }

        void ExitToHome()
        {
            _ending = true;
            TogglePause(false);
            bootstrap.ShowHome();
        }

        // ==================== construction ====================

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;
            _root = GetComponent<RectTransform>();
            BuildMaterials();
            BuildWorld();
            BuildHud();
            BuildFeedback();
            BuildPauseOverlay();
            BuildPreviewOverlay();
        }

        void BuildPreviewOverlay()
        {
            var overlay = UIFactory.CreatePanel(transform, "PreviewOverlay",
                new Color(0.98f, 0.97f, 1f, 0.97f), 0f);
            overlay.raycastTarget = true;
            _previewOverlay = overlay.rectTransform;

            var caption = UIFactory.CreateText(overlay.transform, "Caption",
                "MEMORIZE THESE WORDS", 12, Theme.Teal);
            UIFactory.BandTop(caption.rectTransform, 88f, 22f);

            _previewTitle = UIFactory.CreateText(overlay.transform, "Title", "LEVEL 1", 34, Theme.Orange);
            _previewTitle.font = UIFactory.DisplayFont;
            UIFactory.BandTop(_previewTitle.rectTransform, 112f, 44f);

            // Word list card.
            var card = UIFactory.CreatePanel(overlay.transform, "Card", Theme.CreamPanel, 22f);
            UIFactory.AddSoftShadow(card.gameObject, 4f, 0.18f);
            float rowH = 38f;
            float cardH = GameConfig.WordsPerLevel * rowH + 24f;
            UIFactory.BandTop(card.rectTransform, 170f, cardH, 40f);

            for (int i = 0; i < GameConfig.WordsPerLevel; i++)
            {
                var row = UIFactory.CreateText(card.transform, "Row" + i, "", 16, Theme.Ink);
                UIFactory.BandTop(row.rectTransform, 12f + i * rowH, rowH);
                _previewRows[i] = row;
            }

            _previewCountdown = UIFactory.CreateText(overlay.transform, "Countdown",
                "STARTS IN 8", 14, Theme.InkSoft);
            UIFactory.BandTop(_previewCountdown.rectTransform, 180f + cardH, 24f);

            var startBtn = UIFactory.CreateButton(overlay.transform, "StartNow", "START NOW", 20,
                Theme.Amber, Theme.WordChipDark,
                delegate { ProceduralAudio.PlayClick(); ClosePreview(); });
            UIFactory.BandTop(startBtn.GetComponent<RectTransform>(), 214f + cardH, 58f, 80f);

            _previewOverlay.gameObject.SetActive(false);
        }

        void BuildMaterials()
        {
            for (int i = 0; i < 3; i++)
            {
                var c = LaneTints[i];
                _matLaneGate[i] = MakeMat(new Color(c.r, c.g, c.b, 0.38f), true);
            }
            _matGoodGate = MakeMat(Theme.Hex("4ADE80", 0.60f), true);
            _matBadGate = MakeMat(Theme.Hex("F87171", 0.60f), true);
            _matFadedGate = MakeMat(Theme.Hex("FFFFFF", 0.10f), true);
        }

        readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();
        Material MatCache(string key, Color c, bool transparent)
        {
            Material m;
            if (_matCache.TryGetValue(key, out m)) return m;
            m = MakeMat(c, transparent);
            _matCache[key] = m;
            return m;
        }

        static Material MakeMat(Color c, bool transparent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader);
            m.color = c;
            if (transparent && shader.name.Contains("Universal"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        /// <summary>
        /// Re-tints the already-built world to the currently equipped background
        /// theme, without rebuilding any geometry. Cheap enough to call at the
        /// start of every run, so a Shop purchase/equip takes effect immediately
        /// even though the 3D world itself is only built once (see EnsureBuilt).
        /// </summary>
        void ApplyBackgroundTheme()
        {
            int bgIdx = Mathf.Clamp(ProgressManager.SelectedBackground, 0, Theme.Backgrounds.Length - 1);
            _bgTheme = Theme.Backgrounds[bgIdx];
            if (_world == null) return; // world not built yet — BuildWorld() will pick it up

            if (RenderSettings.skybox != null)
            {
                RenderSettings.skybox.SetColor("_SkyTint", _bgTheme.skyTint);
                RenderSettings.skybox.SetColor("_GroundColor", _bgTheme.skyGround);
            }
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = _bgTheme.camBackground;
            RenderSettings.fogColor = _bgTheme.fogColor;

            RecolorCached("ground", _bgTheme.groundColor);
            RecolorCached("track", _bgTheme.trackColor);
            RecolorCached("rail", _bgTheme.railColor);
            RecolorCached("trunk", _bgTheme.trunkColor);
            RecolorCached("cloud", _bgTheme.cloudColor);
            for (int i = 0; i < 3; i++)
            {
                RecolorCached("hill" + i, _bgTheme.hillColors[i]);
                RecolorCached("canopy" + i, _bgTheme.canopyColors[i]);
            }

            ApplyBackdropSprite();
            ApplySceneryFamily();
        }

        /// <summary>
        /// Every scenery family (generic trees + one real-pack-model prop set
        /// per theme) is built once (BuildSideScenery) and just toggled
        /// active/inactive here based on the equipped theme, so a background
        /// switch between runs doesn't need to rebuild any geometry. Falls
        /// back to the generic trees if the current theme's prop prefabs
        /// haven't been copied into Resources yet (see BuildThemedPropScenery)
        /// so the world is never left looking empty.
        /// </summary>
        void ApplySceneryFamily()
        {
            List<GameObject> themed;
            bool hasThemed = _themedSceneryObjs.TryGetValue(_bgTheme.name, out themed) && themed.Count > 0;

            for (int i = 0; i < _treeSceneryObjs.Count; i++)
                if (_treeSceneryObjs[i] != null) _treeSceneryObjs[i].SetActive(!hasThemed);

            foreach (var kv in _themedSceneryObjs)
            {
                bool active = hasThemed && kv.Key == _bgTheme.name;
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) list[i].SetActive(active);
            }
        }

        /// <summary>
        /// Shows/hides and re-textures the big backdrop quad for whichever
        /// background theme is equipped. Themes without a backdropSprite (the
        /// original 3 procedural themes) just hide it and keep the normal
        /// near-track fog distance; themes with one push fog back so the
        /// image itself doesn't fade into flat fog color before it's visible.
        /// </summary>
        void ApplyBackdropSprite()
        {
            if (_backdropRenderer == null) return;

            Sprite sprite = string.IsNullOrEmpty(_bgTheme.backdropSprite)
                ? null
                : Resources.Load<Sprite>("Sprites/Backgrounds/" + _bgTheme.backdropSprite);

            _backdropRenderer.gameObject.SetActive(sprite != null);
            if (sprite != null)
            {
                var mat = _backdropRenderer.sharedMaterial;
                if (mat != null) mat.SetTexture("_BaseMap", sprite.texture);
            }

            RenderSettings.fogEndDistance = sprite != null ? BackdropFogEnd : DefaultFogEnd;
        }

        void RecolorCached(string key, Color c)
        {
            Material m;
            if (_matCache.TryGetValue(key, out m) && m != null) m.color = c;
        }

        void BuildWorld()
        {
            _world = new GameObject("LingoRunWorld");
            var wt = _world.transform;

            // Equipped background theme (Meadow / Space / Playground / …), chosen in the Shop.
            int bgIdx = Mathf.Clamp(ProgressManager.SelectedBackground, 0, Theme.Backgrounds.Length - 1);
            _bgTheme = Theme.Backgrounds[bgIdx];

            // Camera: behind and above the player, looking down the track.
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }
            cam.transform.position = new Vector3(0f, 5.6f, -7.2f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cam.fieldOfView = 58f;

            // Procedural gradient skybox with a real sun disc (uses the scene's
            // directional light for sun position). Far nicer than a flat color.
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetFloat("_SunSize", 0.06f);
                sky.SetFloat("_SunSizeConvergence", 4f);
                sky.SetFloat("_AtmosphereThickness", 0.85f);
                sky.SetFloat("_Exposure", 1.35f);
                sky.SetColor("_SkyTint", _bgTheme.skyTint);
                sky.SetColor("_GroundColor", _bgTheme.skyGround);
                RenderSettings.skybox = sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = _bgTheme.camBackground;
            }

            // Distance fog: world fades toward a soft haze at the horizon.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _bgTheme.fogColor;
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 118f;

            // Optional real scenic backdrop, far behind everything else — see
            // ApplyBackdropSprite for how it's shown/hidden/re-textured and
            // why fog distance changes when one's in use.
            BuildBackdrop(wt);

            // Wide ground either side of the road.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(wt, false);
            ground.transform.position = new Vector3(0f, -0.42f, 110f);
            ground.transform.localScale = new Vector3(280f, 0.4f, 320f);
            ground.GetComponent<Renderer>().sharedMaterial = MatCache("ground", _bgTheme.groundColor, false);

            // Track bed + rounded rails.
            var track = GameObject.CreatePrimitive(PrimitiveType.Cube);
            track.name = "Track";
            Destroy(track.GetComponent<Collider>());
            track.transform.SetParent(wt, false);
            track.transform.position = new Vector3(0f, -0.25f, 110f);
            track.transform.localScale = new Vector3(TrackHalfWidth * 2f + 1.0f, 0.5f, 300f);
            track.GetComponent<Renderer>().sharedMaterial = MatCache("track", _bgTheme.trackColor, false);

            for (int side = -1; side <= 1; side += 2)
            {
                // Capsule stretched along z = rounded rail (softer than a box edge).
                var rail = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                rail.name = "Rail";
                Destroy(rail.GetComponent<Collider>());
                rail.transform.SetParent(wt, false);
                rail.transform.position = new Vector3(side * (TrackHalfWidth + 0.72f), 0.05f, 110f);
                rail.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                rail.transform.localScale = new Vector3(0.55f, 150f, 0.55f);
                rail.GetComponent<Renderer>().sharedMaterial = MatCache("rail", _bgTheme.railColor, false);
            }

            // Scrolling two-tone road slabs (like the reference video's banding).
            var slabMat = MatCache("slab", Theme.Hex("E2DEF1"), false);
            for (int i = 0; i < StripeCount; i++)
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Slab";
                Destroy(slab.GetComponent<Collider>());
                slab.transform.SetParent(wt, false);
                slab.transform.position = new Vector3(0f, 0.012f, -10f + i * StripeGap);
                slab.transform.localScale = new Vector3(TrackHalfWidth * 2f + 1.0f, 0.02f, 4f);
                var r = slab.GetComponent<Renderer>();
                r.sharedMaterial = slabMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                _stripes.Add(slab.transform);
            }

            // Dashed lane separators at x = -1 / +1 (helps reading which lane is which).
            var dashMat = MatCache("dash", Theme.Hex("FFFFFF", 0.55f), true);
            for (int lane = -1; lane <= 1; lane += 2)
            {
                for (int i = 0; i < 34; i++)
                {
                    var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dash.name = "Dash";
                    Destroy(dash.GetComponent<Collider>());
                    dash.transform.SetParent(wt, false);
                    dash.transform.position = new Vector3(lane * 1f, 0.02f, -10f + i * 4f);
                    dash.transform.localScale = new Vector3(0.09f, 0.02f, 1.3f);
                    var r = dash.GetComponent<Renderer>();
                    r.sharedMaterial = dashMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    _scenery.Add(new SceneryItem { t = dash.transform, loop = 34f * 4f });
                }
            }

            BuildSideScenery(wt);

            // Player: a root that carries one of four selectable shapes
            // (sphere / cube / pill / diamond) plus the trail.
            var playerRoot = new GameObject("Player");
            playerRoot.transform.SetParent(wt, false);
            playerRoot.transform.position = new Vector3(0f, 0.5f, PlayerZ);
            _player = playerRoot.transform;

            // 0: sphere (with a white cap dot, like the reference video)
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            sphere.name = "ShapeSphere";
            sphere.transform.SetParent(playerRoot.transform, false);
            _shapeObjs[0] = sphere;
            _shapeRenderers[0] = sphere.GetComponent<Renderer>();
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Cap";
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(sphere.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.18f, -0.28f);
            cap.transform.localScale = Vector3.one * 0.42f;
            cap.GetComponent<Renderer>().sharedMaterial = MatCache("cap", Color.white, false);

            // 1: cube
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cube.GetComponent<Collider>());
            cube.name = "ShapeCube";
            cube.transform.SetParent(playerRoot.transform, false);
            cube.transform.localScale = Vector3.one * 0.85f;
            _shapeObjs[1] = cube;
            _shapeRenderers[1] = cube.GetComponent<Renderer>();

            // 2: pill (capsule lying across the track)
            var pill = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(pill.GetComponent<Collider>());
            pill.name = "ShapePill";
            pill.transform.SetParent(playerRoot.transform, false);
            pill.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            pill.transform.localScale = new Vector3(0.44f, 0.52f, 0.44f);
            _shapeObjs[2] = pill;
            _shapeRenderers[2] = pill.GetComponent<Renderer>();

            // 3: diamond (tilted cube gem)
            var diamond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(diamond.GetComponent<Collider>());
            diamond.name = "ShapeDiamond";
            diamond.transform.SetParent(playerRoot.transform, false);
            diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            diamond.transform.localScale = Vector3.one * 0.68f;
            _shapeObjs[3] = diamond;
            _shapeRenderers[3] = diamond.GetComponent<Renderer>();

            // 4: egg (tall sphere)
            var egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(egg.GetComponent<Collider>());
            egg.name = "ShapeEgg";
            egg.transform.SetParent(playerRoot.transform, false);
            egg.transform.localScale = new Vector3(0.8f, 1.15f, 0.8f);
            _shapeObjs[4] = egg;
            _shapeRenderers[4] = egg.GetComponent<Renderer>();

            // 5: puck (flat coin, flips as it "rolls")
            var puck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(puck.GetComponent<Collider>());
            puck.name = "ShapePuck";
            puck.transform.SetParent(playerRoot.transform, false);
            puck.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            puck.transform.localScale = new Vector3(0.9f, 0.14f, 0.9f);
            _shapeObjs[5] = puck;
            _shapeRenderers[5] = puck.GetComponent<Renderer>();

            // 6: gem (tall rotated crystal)
            var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(gem.GetComponent<Collider>());
            gem.name = "ShapeGem";
            gem.transform.SetParent(playerRoot.transform, false);
            gem.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            gem.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
            _shapeObjs[6] = gem;
            _shapeRenderers[6] = gem.GetComponent<Renderer>();

            // 7: UFO (flattened saucer + small white dome)
            var ufoRoot = new GameObject("ShapeUfo");
            ufoRoot.transform.SetParent(playerRoot.transform, false);
            var saucer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(saucer.GetComponent<Collider>());
            saucer.transform.SetParent(ufoRoot.transform, false);
            saucer.transform.localScale = new Vector3(1.15f, 0.35f, 1.15f);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dome.GetComponent<Collider>());
            dome.transform.SetParent(ufoRoot.transform, false);
            dome.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            dome.transform.localScale = Vector3.one * 0.45f;
            dome.GetComponent<Renderer>().sharedMaterial = MatCache("cap", Color.white, false);
            dome.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            _shapeObjs[7] = ufoRoot;
            _shapeRenderers[7] = saucer.GetComponent<Renderer>();

            for (int i = 0; i < PrimitiveShapeCount; i++)
            {
                _shapeRenderers[i].sharedMaterial = MatCache("ball", Theme.Hex("35D0F0"), false);
                _shapeRenderers[i].shadowCastingMode = ShadowCastingMode.Off;
            }

            // 8+: real vehicle models (see VehicleShapes) reusing the same
            // purchased-pack prefabs as background scenery.
            BuildVehicleShapes(playerRoot.transform);

            for (int i = 0; i < _shapeObjs.Length; i++)
                _shapeObjs[i].SetActive(i == 0);

            _trail = playerRoot.AddComponent<TrailRenderer>();
            _trail.time = 0.55f;
            _trail.startWidth = 0.55f;
            _trail.endWidth = 0.05f;
            _trail.material = MatCache("trailMat", Theme.Hex("8FE3F7", 0.6f), true);
            _trail.shadowCastingMode = ShadowCastingMode.Off;

            var gates = new GameObject("Gates");
            gates.transform.SetParent(wt, false);
            _gateLayer = gates.transform;

            // Confetti burst played at the gate on every correct answer.
            var confGo = new GameObject("Confetti");
            confGo.transform.SetParent(wt, false);
            _confetti = confGo.AddComponent<ParticleSystem>();
            var main = _confetti.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.24f);
            main.gravityModifier = 1.3f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Theme.Hex("FFD97A"), Theme.Hex("4ADE80"));
            var emission = _confetti.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 42) });
            var shape = _confetti.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.45f;
            var confRend = confGo.GetComponent<ParticleSystemRenderer>();
            var confShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (confShader == null) confShader = Shader.Find("Particles/Standard Unlit");
            if (confShader == null) confShader = Shader.Find("Unlit/Color");
            confRend.material = new Material(confShader);
            confRend.shadowCastingMode = ShadowCastingMode.Off;

            _world.SetActive(false);
        }

        /// <summary>
        /// A big flat quad far behind the track that shows a real baked scene
        /// (see ApplyBackdropSprite) for background themes that have one.
        /// The camera never rotates or moves off its fixed setup position, so
        /// unlike the scrolling side scenery, this can just sit in world space
        /// at a fixed spot and stay correctly framed for the whole run. Built
        /// once per world; ApplyBackdropSprite swaps its texture/visibility
        /// whenever the equipped theme changes without rebuilding it.
        /// </summary>
        void BuildBackdrop(Transform wt)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backdrop";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(wt, false);
            quad.transform.position = new Vector3(0f, 26f, 150f);
            quad.transform.localScale = new Vector3(220f, 170f, 1f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _backdropRenderer = quad.GetComponent<Renderer>();
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetFloat("_Smoothness", 0f);
                mat.SetFloat("_Metallic", 0f);
                // Render both sides — sidesteps ever having to nail down which
                // way PrimitiveType.Quad's default normal happens to face.
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
                _backdropRenderer.sharedMaterial = mat;
            }
            _backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _backdropRenderer.receiveShadows = false;

            ApplyBackdropSprite();
        }

        /// <summary>
        /// Low-poly side scenery: rolling hills, simple trees and soft clouds.
        /// Hills/trees scroll with the road and loop, giving a sense of place
        /// and speed; clouds sit high up and stay put (they read as "far away").
        /// </summary>
        void BuildSideScenery(Transform wt)
        {
            var hillColors = _bgTheme.hillColors;
            var canopyColors = _bgTheme.canopyColors;
            var trunkMat = MatCache("trunk", _bgTheme.trunkColor, false);

            Random.State prev = Random.state;
            Random.InitState(1234); // deterministic layout

            // Hills: squashed spheres well off the road, both sides.
            for (int i = 0; i < 14; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float x = side * Random.Range(11f, 26f);
                float z = -10f + i * (SceneryLoop / 14f) + Random.Range(-3f, 3f);
                float w = Random.Range(7f, 16f);
                float h = Random.Range(2.2f, 5f);

                var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = "Hill";
                Destroy(hill.GetComponent<Collider>());
                hill.transform.SetParent(wt, false);
                hill.transform.position = new Vector3(x, -0.2f, z);
                hill.transform.localScale = new Vector3(w, h, w * 0.8f);
                var r = hill.GetComponent<Renderer>();
                r.sharedMaterial = MatCache("hill" + (i % 3), hillColors[i % 3], false);
                r.shadowCastingMode = ShadowCastingMode.Off;
                _scenery.Add(new SceneryItem { t = hill.transform, loop = SceneryLoop });
            }

            // Trees: cylinder trunk + sphere canopy, closer to the road.
            for (int i = 0; i < 16; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                float x = side * Random.Range(5.5f, 12f);
                float z = -8f + i * (SceneryLoop / 16f) + Random.Range(-2.5f, 2.5f);
                float s = Random.Range(0.8f, 1.5f);

                var treeRoot = new GameObject("Tree");
                treeRoot.transform.SetParent(wt, false);
                treeRoot.transform.position = new Vector3(x, 0f, z);

                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(trunk.GetComponent<Collider>());
                trunk.transform.SetParent(treeRoot.transform, false);
                trunk.transform.localPosition = new Vector3(0f, 0.55f * s, 0f);
                trunk.transform.localScale = new Vector3(0.22f * s, 0.55f * s, 0.22f * s);
                var tr = trunk.GetComponent<Renderer>();
                tr.sharedMaterial = trunkMat;
                tr.shadowCastingMode = ShadowCastingMode.Off;

                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(canopy.GetComponent<Collider>());
                canopy.transform.SetParent(treeRoot.transform, false);
                canopy.transform.localPosition = new Vector3(0f, 1.55f * s, 0f);
                canopy.transform.localScale = new Vector3(1.5f * s, 1.7f * s, 1.5f * s);
                var cr = canopy.GetComponent<Renderer>();
                cr.sharedMaterial = MatCache("canopy" + (i % 3), canopyColors[i % 3], false);
                cr.shadowCastingMode = ShadowCastingMode.Off;

                _scenery.Add(new SceneryItem { t = treeRoot.transform, loop = SceneryLoop });
                _treeSceneryObjs.Add(treeRoot);
            }

            // Real pack-model scenery for every background theme that has one
            // (see GameConfig.Theme.BackgroundTheme.sceneryPropPrefabs) — real
            // buildings for Downtown, real pyramids/cacti for Desert Pyramids,
            // a real space shuttle for Rocket Launch, etc. Built once for
            // EVERY such theme here (not just the currently-equipped one) so
            // switching themes between runs is just a visibility toggle (see
            // ApplySceneryFamily), same "build once" pattern as the trees.
            for (int i = 0; i < Theme.Backgrounds.Length; i++)
            {
                var theme = Theme.Backgrounds[i];
                if (theme.sceneryPropPrefabs != null && theme.sceneryPropPrefabs.Length > 0)
                    BuildThemedPropScenery(wt, theme);
            }

            ApplySceneryFamily();

            // Sky decoration: puffy clouds for grounded themes, a scattered
            // starfield of small glowing dots for Space.
            var cloudMat = MatCache("cloud", _bgTheme.cloudColor, false);
            if (_bgTheme.starsInSky)
            {
                for (int i = 0; i < 60; i++)
                {
                    float x = Random.Range(-40f, 40f);
                    float y = Random.Range(6f, 30f);
                    float z = Random.Range(20f, 110f);
                    var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    star.name = "Star";
                    Destroy(star.GetComponent<Collider>());
                    star.transform.SetParent(wt, false);
                    star.transform.position = new Vector3(x, y, z);
                    float s = Random.Range(0.08f, 0.22f);
                    star.transform.localScale = new Vector3(s, s, s);
                    var sr = star.GetComponent<Renderer>();
                    sr.sharedMaterial = cloudMat;
                    sr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = Random.Range(-20f, 20f);
                    float y = Random.Range(11f, 18f);
                    float z = Random.Range(45f, 100f);
                    var cloudRoot = new GameObject("Cloud");
                    cloudRoot.transform.SetParent(wt, false);
                    cloudRoot.transform.position = new Vector3(x, y, z);
                    float cs = Random.Range(1.6f, 3.2f);
                    float[] ox = { -1.1f, 0f, 1.1f };
                    float[] os = { 0.75f, 1f, 0.7f };
                    for (int p = 0; p < 3; p++)
                    {
                        var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Destroy(puff.GetComponent<Collider>());
                        puff.transform.SetParent(cloudRoot.transform, false);
                        puff.transform.localPosition = new Vector3(ox[p] * cs, p == 1 ? 0.25f * cs : 0f, 0f);
                        puff.transform.localScale = new Vector3(1.6f * cs * os[p], 0.9f * cs * os[p], 1.2f * cs * os[p]);
                        var pr = puff.GetComponent<Renderer>();
                        pr.sharedMaterial = cloudMat;
                        pr.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }

            Random.state = prev;
        }

        /// <summary>
        /// Player "ball" vehicle shapes (see VehicleShapes) — the same real
        /// purchased-pack prefabs used for background scenery, reused here
        /// as selectable player shapes. Must be copied into Resources first
        /// via the Editor menu "LingoRun/Copy Runtime Scenery Prefabs" (see
        /// IslandThumbnailBaker.RuntimeTargets); if a given model hasn't
        /// been copied yet, Resources.Load returns null and that slot gets
        /// a tiny empty placeholder instead (keeps the fixed-size
        /// _shapeObjs/_shapeRenderers arrays valid; the slot just renders
        /// as nothing until the copy step is run).
        /// </summary>
        void BuildVehicleShapes(Transform playerParent)
        {
            for (int v = 0; v < VehicleShapes.Length; v++)
            {
                int i = PrimitiveShapeCount + v;
                var def = VehicleShapes[v];
                var prefab = Resources.Load<GameObject>("Prefabs/" + def.resourceName);
                if (prefab == null)
                {
                    var placeholder = new GameObject("ShapeVehicleMissing_" + def.resourceName);
                    placeholder.transform.SetParent(playerParent, false);
                    _shapeObjs[i] = placeholder;
                    _shapeRenderers[i] = null;
                    continue;
                }

                var instance = Instantiate(prefab, playerParent, false);
                instance.name = "ShapeVehicle_" + def.resourceName;

                // The source prefabs carry BoxColliders on every part (body,
                // wheels, ...) for their original scenery use — the player
                // never uses physics collision (see the Z-position checks in
                // Update/Resolve), so strip them rather than let stray
                // colliders sit around doing nothing.
                var cols = instance.GetComponentsInChildren<Collider>();
                for (int c = 0; c < cols.Length; c++) Destroy(cols[c]);

                var renderers = instance.GetComponentsInChildren<Renderer>();
                for (int r = 0; r < renderers.Length; r++)
                    renderers[r].shadowCastingMode = ShadowCastingMode.Off;

                // Target footprint is noticeably bigger than the ball's own
                // ~0.9 unit size on purpose: FitToBall scales off the
                // LONGER of width/length, and a car's cross-section (width)
                // is much narrower than its length, so matching the ball's
                // footprint left vehicles reading as thin/small from the
                // player's usual view. This was tuned up after visual
                // feedback that the first pass looked too small.
                FitToBall(instance, renderers, VehicleTargetFootprint, def.yawDegrees);

                _shapeObjs[i] = instance;
                _shapeRenderers[i] = renderers.Length > 0 ? renderers[0] : null;
            }
        }

        /// <summary>
        /// Auto-scales and repositions an instantiated model so its actual
        /// combined mesh bounds — whatever size the source model happens to
        /// be, from a small cart to a full space shuttle — fit the same
        /// footprint the procedural ball shapes occupy, with its base
        /// resting on the track (world y = 0) and centered on the player's
        /// lane. Computed from real bounds rather than a hand-tuned
        /// scale/offset per model, since eleven very differently-sized
        /// models would each need separate tuning to get right by eye.
        /// </summary>
        public static void FitToBall(GameObject instance, Renderer[] renderers, float targetFootprint, float yawDegrees)
        {
            if (renderers.Length == 0) return;
            instance.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            float footprint = Mathf.Max(b.size.x, b.size.z);
            float scale = footprint > 0.0001f ? targetFootprint / footprint : 1f;
            instance.transform.localScale = Vector3.one * scale;

            // Bounds shift once scaled — recompute, then shift the instance
            // so its footprint is centered on the lane (x/z) with its base
            // exactly on the track (world y = 0), regardless of where the
            // source model's own pivot happened to be authored.
            Bounds b2 = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b2.Encapsulate(renderers[i].bounds);
            Vector3 delta = new Vector3(-b2.center.x, -b2.min.y, -b2.center.z);
            instance.transform.position += delta;
        }

        /// <summary>
        /// Real pack-model near-track scenery for one background theme (real
        /// buildings for Downtown, real pyramids/cacti for Desert Pyramids, a
        /// real space shuttle for Rocket Launch, etc.) instead of a hand-built
        /// or recolored primitive stand-in. Loads every name in
        /// theme.sceneryPropPrefabs via Resources.Load, picks one at random
        /// per scenery slot, and places a handful of them along the track —
        /// same fewer/wider-spaced/bigger placement used for the original
        /// Rocket Launch shuttles, since a real full-size model reads much
        /// better as a "wow" prop than a repeated small icon. If none of the
        /// theme's prefabs have been copied into Resources yet (run the
        /// Editor menu "LingoRun/Copy Runtime Scenery Prefabs" once), this
        /// simply builds nothing for that theme and ApplySceneryFamily falls
        /// back to the generic trees instead.
        /// </summary>
        void BuildThemedPropScenery(Transform wt, Theme.BackgroundTheme theme)
        {
            var names = theme.sceneryPropPrefabs;
            var prefabs = new List<GameObject>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var p = Resources.Load<GameObject>("Prefabs/" + names[i]);
                if (p != null) prefabs.Add(p);
            }
            if (prefabs.Count == 0) return; // not copied yet — ApplySceneryFamily falls back to trees

            const int Count = 6;
            var list = new List<GameObject>(Count);
            for (int i = 0; i < Count; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                float x = side * Random.Range(12f, 20f);
                float z = -8f + i * (SceneryLoop / Count) + Random.Range(-4f, 4f);
                float s = Random.Range(0.6f, 0.95f);
                float yaw = Random.Range(-20f, 20f);

                var prefab = prefabs[Random.Range(0, prefabs.Count)];
                var instance = Instantiate(prefab, wt);
                instance.name = "Prop";
                var col = instance.GetComponent<Collider>();
                if (col != null) Destroy(col);
                instance.transform.position = new Vector3(x, 0f, z);
                instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                instance.transform.localScale = Vector3.one * s;
                var r = instance.GetComponent<Renderer>();
                if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;

                _scenery.Add(new SceneryItem { t = instance.transform, loop = SceneryLoop });
                list.Add(instance);
            }
            _themedSceneryObjs[theme.name] = list;
        }

        void BuildHud()
        {
            var pauseBtn = UIFactory.CreateButton(transform, "PauseBtn", "II", 16,
                Theme.CreamPanel, Theme.Ink, delegate { ProceduralAudio.PlayClick(); TogglePause(true); });
            UIFactory.PlaceTop(pauseBtn.GetComponent<RectTransform>(), 34f, 30f, 40f, 40f);

            for (int i = 0; i < 3; i++)
            {
                var heart = UIFactory.CreateIcon(transform, "Heart" + i, "heart", Theme.HeartRed);
                UIFactory.PlaceTop(heart.rectTransform, 74f + i * 24f, 30f, 16f, 16f);
                _hearts[i] = heart;
            }

            _scoreText = UIFactory.CreateText(transform, "Score", "0", 30, Color.white);
            UIFactory.BandTop(_scoreText.rectTransform, 12f, 40f);
            var scoreOutline = _scoreText.gameObject.AddComponent<Outline>();
            scoreOutline.effectColor = new Color(0.2f, 0.1f, 0.35f, 0.8f);
            scoreOutline.effectDistance = new Vector2(1.5f, -1.5f);

            _streakText = UIFactory.CreateText(transform, "Streak", "x0", 16, Color.white, TextAnchor.MiddleRight);
            UIFactory.BandTop(_streakText.rectTransform, 20f, 26f, 20f);

            var card = UIFactory.CreatePanel(transform, "PromptCard", new Color(1f, 1f, 1f, 0.95f), 22f);
            UIFactory.AddSoftShadow(card.gameObject, 4f, 0.18f);
            _promptCardRt = card.rectTransform;
            UIFactory.BandTop(_promptCardRt, 56f, 66f, 70f);

            _promptCaption = UIFactory.CreateText(card.transform, "Caption", "GO THROUGH THE MATCH", 10, Theme.InkSoft);
            UIFactory.BandTop(_promptCaption.rectTransform, 6f, 14f);

            _promptText = UIFactory.CreateText(card.transform, "Word", "-", 26, Theme.Ink);
            UIFactory.BandTop(_promptText.rectTransform, 22f, 40f);

            _promptHint = UIFactory.CreateText(card.transform, "Hint", "", 11, Theme.InkSoft,
                TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.BandTop(_promptHint.rectTransform, 62f, 18f);
            _promptHint.gameObject.SetActive(false);

            // ----- bottom lane chips (always-readable answer words) -----
            for (int i = 0; i < 3; i++)
            {
                var chip = UIFactory.CreatePanel(transform, "Chip" + i, new Color(1f, 1f, 1f, 0.68f), 16f);
                UIFactory.AddSoftShadow(chip.gameObject, 3f, 0.18f);
                var rt = chip.rectTransform;
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(i == 0 ? 12f : 4f, 0f);
                rt.offsetMax = new Vector2(i == 2 ? -12f : -4f, 0f);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 58f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 92f);
                _chips[i] = chip;

                var txt = UIFactory.CreateText(chip.transform, "Word", "-", 18, Theme.Ink);
                txt.resizeTextForBestFit = true;
                txt.resizeTextMinSize = 10;
                txt.resizeTextMaxSize = 19;
                txt.horizontalOverflow = HorizontalWrapMode.Wrap;
                var txtRt = txt.rectTransform;
                txtRt.offsetMin = new Vector2(6f, 10f);
                txtRt.offsetMax = new Vector2(-6f, -6f);
                _chipTexts[i] = txt;

                // Lane-colored bar along the bottom, matching the gate tint.
                var bar = UIFactory.CreatePanel(chip.transform, "Bar", LaneTints[i], 4f);
                var brt = bar.rectTransform;
                brt.anchorMin = new Vector2(0.5f, 0f);
                brt.anchorMax = new Vector2(0.5f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.sizeDelta = new Vector2(52f, 6f);
                brt.anchoredPosition = new Vector2(0f, 5f);
            }
        }

        void BuildFeedback()
        {
            var good = UIFactory.CreatePanel(transform, "GoodFeedback", new Color(Theme.Good.r, Theme.Good.g, Theme.Good.b, 0.40f), 22f);
            _goodFeedback = good.rectTransform;
            _goodText = UIFactory.CreateText(good.transform, "Text", "", 16, Theme.WordChipDark);
            _goodFeedback.gameObject.SetActive(false);

            var bad = UIFactory.CreatePanel(transform, "BadFeedback", new Color(0.16f, 0.12f, 0.24f, 0.92f), 24f);
            UIFactory.AddSoftShadow(bad.gameObject, 5f, 0.25f);
            _badFeedback = bad.rectTransform;
            var caption = UIFactory.CreateText(bad.transform, "Caption", "CORRECT ANSWER", 11, Theme.BadRed);
            UIFactory.BandTop(caption.rectTransform, 12f, 18f);
            _badWord = UIFactory.CreateText(bad.transform, "Word", "", 30, Color.white);
            UIFactory.BandTop(_badWord.rectTransform, 38f, 46f);
            _badMeaning = UIFactory.CreateText(bad.transform, "Meaning", "", 16, new Color(1f, 1f, 1f, 0.75f));
            UIFactory.BandTop(_badMeaning.rectTransform, 92f, 26f);
            _badFeedback.gameObject.SetActive(false);
        }

        void BuildPauseOverlay()
        {
            var overlay = UIFactory.CreatePanel(transform, "PauseOverlay", new Color(0.98f, 0.97f, 1f, 0.97f), 0f);
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
            var muteLabel = mute.GetComponentInChildren<Text>();
            mute.onClick.AddListener(delegate
            {
                ProceduralAudio.Muted = !ProceduralAudio.Muted;
                muteLabel.text = ProceduralAudio.Muted ? "SOUND: OFF" : "SOUND: ON";
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

        void ClearGroups()
        {
            foreach (var g in _groups)
                if (g.rootGo != null) Destroy(g.rootGo);
            _groups.Clear();
        }
    }
}
