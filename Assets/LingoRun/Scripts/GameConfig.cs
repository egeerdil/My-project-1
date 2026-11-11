using System.Collections.Generic;
using UnityEngine;

namespace LingoRun
{
    public enum Difficulty { Calm, Normal, Fast }

    /// <summary>Gameplay modes: catch the translation, fill the sentence blank,
    /// spell it in Bubbles, or pair word and meaning in Memory Match.
    /// (Blaster kept for save/enum stability; no longer reachable from UI.)</summary>
    public enum GameMode { Words, Sentences, Blaster, Bubbles, Match }

    /// <summary>Per-difficulty pacing, mirrors DIFFICULTY_CONFIG from lingorun_v48.jsx.</summary>
    public class DifficultyConfig
    {
        public float speedStart;   // px/sec at run start (reference-canvas units)
        public float speedMax;     // px/sec ceiling
        public float spawnGap;     // horizontal distance between consecutive word groups
        public float rampMs;       // time to ramp from speedStart to speedMax
        public string label;
        public string description;
    }

    public static class GameConfig
    {
        // Reference canvas (matches the 440x~782 9:16 layout of the web version)
        public const float RefWidth = 440f;
        public const float RefHeight = 782f;

        // Gameplay constants (ported 1:1)
        public const float CollisionZoneX = 80f;
        public const float PlayerX = 60f;
        public const float PlayAreaTop = 130f;
        public const float PlayAreaBottomMargin = 60f;
        public const float WrongPauseSeconds = 1.6f;
        public const int MasteryThreshold = 3;   // correct answers across runs to master a word
        // Only 296 A1 words exist in the Spanish dataset, so the unlock must sit
        // below that; 250 = ~85% of A1 mastered before A2 opens up.
        public const int A2UnlockThreshold = 250;

        // Daily goal: correct catches per day (drives the streak card on Home).
        public const int DailyGoal = 30;

        // ----- level structure (Words mode) -----
        public const int WordsPerLevel = 8;      // unique words introduced per level
        public const int RepeatsPerWord = 2;     // each word appears this many times -> 16 gates
        public const int LevelCompleteCoins = 25;
        public const float PreviewSecondsBase = 2.5f;    // preview time before the run
        public const float PreviewSecondsPerWord = 0.7f; // extra per word

        // ----- world map: themed units (one node per level) -----
        public const int MapLevelCount = 30;
        // Each UNIT is LevelsPerUnit consecutive levels sharing one vocab
        // topic ("Food", "Travel", …). The mode cycle spans exactly one unit,
        // so every unit contains all four games; difficulty (pace + required
        // accuracy) ramps up WITHIN the unit and resets when a new theme
        // starts — theme = unit, challenge = position inside it.
        public const int LevelsPerUnit = 5;
        static readonly GameMode[] MapModeCycle =
            { GameMode.Words, GameMode.Sentences, GameMode.Words, GameMode.Match, GameMode.Bubbles };

        /// <summary>One map node's fixed configuration: which mode it plays, at
        /// what pace, the accuracy needed to clear it, and its unit's theme.</summary>
        public struct MapLevelDef
        {
            public int number;
            public GameMode mode;
            public Difficulty pace;
            public int passAccuracy; // percent, 0-100
            public string topic;     // the unit's vocab theme ("food", "travel", …)
        }

        /// <summary>Unit order: the profile's onboarding topics first (in
        /// canonical order), then every remaining topic — so personalization
        /// literally IS the map: your favorite themes are the first units.
        /// Always a permutation of all topics, so units wrap deterministically
        /// after TopicOrder.Length units regardless of what was picked.</summary>
        public static List<string> OrderedUnitTopics()
        {
            var chosen = ProfileManager.ActiveTopics;
            var result = new List<string>();
            foreach (var t in VocabDatabase.TopicOrder)
                if (chosen.Contains(t)) result.Add(t);
            foreach (var t in VocabDatabase.TopicOrder)
                if (!result.Contains(t)) result.Add(t);
            return result;
        }

        /// <summary>Theme of the unit containing this level.</summary>
        public static string UnitTopicForLevel(int number)
        {
            var order = OrderedUnitTopics();
            int unit = (Mathf.Max(1, number) - 1) / LevelsPerUnit;
            return order[unit % order.Count];
        }

        /// <summary>How many map levels of this mode came BEFORE this level
        /// within the same topic (units repeat their theme every
        /// TopicOrder.Length units) — the sequential slice index used by
        /// RunningGame3D to serve fresh words/sentences from the unit's topic
        /// pool each time that mode comes up.</summary>
        public static int PriorSlicesInTopic(int number, GameMode mode)
        {
            number = Mathf.Max(1, number);
            int unit = (number - 1) / LevelsPerUnit;
            int li = (number - 1) % LevelsPerUnit;
            int perUnit = 0;
            foreach (var m in MapModeCycle) if (m == mode) perUnit++;
            int before = 0;
            for (int i = 0; i < li; i++) if (MapModeCycle[i] == mode) before++;
            return (unit / VocabDatabase.TopicOrder.Length) * perUnit + before;
        }

        /// <summary>Letter-by-letter spelling (Bubble Spell) only makes sense
        /// for alphabet scripts — Japanese/Korean/Chinese get Memory Match in
        /// its place on the map and in Free Play.</summary>
        public static bool SupportsSpelling(string langCode)
        {
            return langCode != "ja" && langCode != "ko" && langCode != "zh";
        }

        /// <summary>Deterministic node lookup — same map every time, no save data
        /// needed beyond ProgressManager.MapLevelReached (how far you've unlocked).
        /// Difficulty ramps INSIDE each unit (start calm on a fresh theme, finish
        /// fast) with a slow global creep on required accuracy; the map has no
        /// upper level cap, it keeps generating nodes forever.</summary>
        public static MapLevelDef GetMapLevel(int number)
        {
            number = Mathf.Max(1, number);
            int cycleIdx = (number - 1) % MapModeCycle.Length;
            int li = (number - 1) % LevelsPerUnit;   // position inside the unit
            int unit = (number - 1) / LevelsPerUnit;
            Difficulty pace = li < 2 ? Difficulty.Calm : li < 4 ? Difficulty.Normal : Difficulty.Fast;
            int passAccuracy = Mathf.Min(80, 55 + li * 5 + unit);
            GameMode mode = MapModeCycle[cycleIdx];
            if (mode == GameMode.Bubbles && !SupportsSpelling(LanguageManager.Current))
                mode = GameMode.Match;
            return new MapLevelDef
            {
                number = number,
                mode = mode,
                pace = pace,
                passAccuracy = passAccuracy,
                topic = UnitTopicForLevel(number),
            };
        }

        // ----- coin economy -----
        public const int StartingCoins = 10000;    // gifted to every brand-new profile
        public const int DailyGoalCoins = 30;      // bonus when the daily goal is hit
        public const int AchievementCoins = 50;    // bonus per achievement unlocked

        // ----- gem economy (premium currency, device-global) -----
        // Earned as a slow trickle; real-money gem packs (IAP) come later.
        public const int GemsPerUnitComplete = 5;  // finishing a themed unit's last level
        public const int GemsPerAchievement = 1;   // per achievement unlocked
        public const int GemsJackpot = 2;          // chest jackpot bonus
        public const int HeartRefillGems = 3;      // instant full heart refill
        public const int PremiumSlotGems = 60;     // extra profile slot (= extra language)
        public const int GemCoinExchangeRate = 100; // coins granted per gem exchanged
        // Shop prices (index 0 = starter item, free). Lengths must match
        // Theme.BallSkins, RunningGame3D's shape count and the trail styles.
        public static readonly int[] SkinPrices  = { 0, 100, 150, 200, 250, 300, 350, 400, 450, 500, 600, 750 };
        // Shape indices 0-7 are the original procedural primitives; 8-18 are
        // real vehicle models (RunningGame3D.BuildVehicleShapes / BallScreenUI
        // shop) — priced higher as premium items since they're full
        // purchased-pack models, not simple shapes.
        public const int PrimitiveShapeCount = 8;
        public static readonly int[] ShapePrices =
        {
            0, 200, 300, 500, 650, 800, 1000, 1200,
            1500, 1650, 1650, 1400, 1800, 1900, 2000, 1700, 1900, 2200, 3000,
        };
        public static readonly int[] TrailPrices = { 0, 300, 400, 900 };
        // Background themes swap the whole 3D run environment (sky/fog/ground/
        // scenery colors). Index 0 = starter theme, free. See Theme.Backgrounds.
        // Indices 3-12 are the real baked-art themes (Palmov's asset pack).
        public static readonly int[] BackgroundPrices =
            { 0, 450, 550, 600, 650, 700, 750, 800, 850, 900, 950, 1000, 1050 };

        static readonly DifficultyConfig Calm = new DifficultyConfig
        {
            speedStart = 75f, speedMax = 110f, spawnGap = 360f, rampMs = 90000f,
            label = "CALM", description = "Gentle. Easy start."
        };
        static readonly DifficultyConfig Normal = new DifficultyConfig
        {
            speedStart = 130f, speedMax = 220f, spawnGap = 420f, rampMs = 60000f,
            label = "NORMAL", description = "Standard. Stays fast."
        };
        static readonly DifficultyConfig Fast = new DifficultyConfig
        {
            speedStart = 180f, speedMax = 290f, spawnGap = 380f, rampMs = 45000f,
            label = "FAST", description = "Expert. No mercy."
        };

        public static DifficultyConfig Get(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Normal: return Normal;
                case Difficulty.Fast: return Fast;
                default: return Calm;
            }
        }
    }

    /// <summary>Modern pastel-candy palette: white cards, vibrant accents,
    /// deep plum ink, soft purple->pink->peach sky gradient background —
    /// styled after a glossy mobile-game home screen mockup.</summary>
    public static class Theme
    {
        public static readonly Color Ink        = Hex("2E2A3B"); // deep plum ink
        public static readonly Color InkSoft    = Hex("2E2A3B", 0.55f);
        public static readonly Color Cream      = Hex("FFFDF7");
        public static readonly Color CreamPanel = Hex("FFFFFF", 0.94f);
        public static readonly Color Orange     = Hex("F97316"); // vibrant coral-orange
        public static readonly Color Teal       = Hex("0D9488");
        public static readonly Color Amber      = Hex("FFC94D");
        public static readonly Color AmberDeep  = Hex("F59E0B");
        public static readonly Color Good       = Hex("2DD4BF");
        public static readonly Color GoodDark   = Hex("0F766E");
        public static readonly Color BadRed     = Hex("F87171");
        public static readonly Color BadRedSoft = Hex("F87171", 0.25f);
        public static readonly Color HeartRed   = Hex("FB4D6D");
        public static readonly Color HeartOff   = Hex("2E2A3B", 0.18f);
        // Backdrop sky gradient (top -> mid -> bottom) — a candy purple-pink-peach
        // wash behind every menu screen (see LingoRunBootstrap.BuildMeadowBackground).
        public static readonly Color SkyTop     = Hex("9385E3");
        public static readonly Color SkyMid     = Hex("EDA6CC");
        public static readonly Color SkyBottom  = Hex("FFDFBE");
        public static readonly Color GrassTop   = Hex("7CC26F"); // legacy, unused by modern bg
        public static readonly Color GrassBot   = Hex("8FC470");
        public static readonly Color HillNear   = Hex("4A8154");
        public static readonly Color Border     = Hex("2E2A3B", 0.10f);
        public static readonly Color WordChip   = Hex("FFFFFF", 0.95f);
        public static readonly Color WordChipDark = Hex("221C33");
        // Purple accent (LEVEL banner card) + grass green accent (MAP button).
        public static readonly Color Purple     = Hex("7C5CFA");
        public static readonly Color PurpleDeep = Hex("5B3FC9");
        public static readonly Color Green      = Hex("4CAF50");
        public static readonly Color GreenDeep  = Hex("2E8B3D");

        /// <summary>Ball skin colors — bought with coins (see GameConfig.SkinPrices).</summary>
        public static readonly Color[] BallSkins =
        {
            Hex("35D0F0"), // cyan (default)
            Hex("FF6B6B"), // coral
            Hex("7CE577"), // lime
            Hex("B08CFF"), // violet
            Hex("FFC94D"), // gold
            Hex("3B3650"), // midnight
            Hex("FF8FC7"), // pink
            Hex("4D79FF"), // royal blue
            Hex("00C2A8"), // teal
            Hex("FF9838"), // orange
            Hex("F2F1EC"), // pearl
            Hex("7B4DFF"), // indigo
        };

        public static Color Hex(string hex, float alpha = 1f)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString("#" + hex, out c))
            {
                c.a = alpha;
                return c;
            }
            return Color.magenta;
        }

        /// <summary>One full run-environment look: sky, fog, ground, track and
        /// scenery colors. Bought/equipped from the Shop (see GameConfig.BackgroundPrices).</summary>
        public class BackgroundTheme
        {
            public string name;
            public Color skyTint;       // procedural skybox sky color
            public Color skyGround;     // procedural skybox "ground" color (horizon)
            public Color camBackground; // fallback flat color if the skybox shader is missing
            public Color fogColor;
            public Color groundColor;   // wide ground plane either side of the track
            public Color trackColor;
            public Color railColor;
            public Color[] hillColors;   // 3, far-off scenery
            public Color[] canopyColors; // 3, near scenery "tops"
            public Color trunkColor;     // near scenery "stems"
            public Color cloudColor;     // sky decoration (clouds or stars)
            public bool starsInSky;      // small bright dots instead of puffy clouds

            // Optional: real pack-model near-track scenery (Resources/Prefabs/<name>,
            // see Editor/IslandThumbnailBaker.cs' RuntimeTargets + runtime
            // RunningGame3D.BuildThemedPropScenery) instead of the generic recolored
            // tree — e.g. real buildings for Downtown, real pyramids/cacti for Desert
            // Pyramids, a real space shuttle for Rocket Launch. One prefab is picked
            // at random per scenery slot when more than one name is listed. Null/empty
            // for themes that should keep the generic tree scenery (Meadow, Space,
            // Playground). Also falls back to generic trees automatically if the
            // listed prefab(s) haven't been copied into Resources yet.
            public string[] sceneryPropPrefabs;

            // Optional: a real baked scene (Resources/Sprites/Backgrounds/<name>.png,
            // see Editor/IslandThumbnailBaker.cs) shown as a big backdrop far behind
            // the track, on top of the procedural colors above. Null for the original
            // 3 procedural-only themes.
            public string backdropSprite;
        }

        public static readonly BackgroundTheme[] Backgrounds =
        {
            // 0: Meadow — the original grassy/sky look, free starter theme.
            new BackgroundTheme
            {
                name = "MEADOW",
                skyTint = Hex("7FB8E8"), skyGround = Hex("E8F2E4"), camBackground = Hex("6EC9F5"),
                fogColor = Hex("DCEBF7"), groundColor = Hex("8FD97F"),
                trackColor = Hex("F0EEF9"), railColor = Hex("A855F7"),
                hillColors = new[] { Hex("7CCB6E"), Hex("5FB864"), Hex("A8E6A0") },
                canopyColors = new[] { Hex("4FA85D"), Hex("6BC46F"), Hex("3E9350") },
                trunkColor = Hex("8A6647"), cloudColor = new Color(1f, 1f, 1f, 0.92f), starsInSky = false,
            },
            // 1: Space — deep-night sky, dark ground, neon rails, glowing "planet" scenery.
            new BackgroundTheme
            {
                name = "SPACE",
                skyTint = Hex("1B1042"), skyGround = Hex("0A0620"), camBackground = Hex("120B2E"),
                fogColor = Hex("241947"), groundColor = Hex("241947"),
                trackColor = Hex("362A63"), railColor = Hex("35D0F0"),
                hillColors = new[] { Hex("352A66"), Hex("2B2153"), Hex("463887") },
                canopyColors = new[] { Hex("FF6EC7"), Hex("35D0F0"), Hex("FFC94D") },
                trunkColor = Hex("120B2E"), cloudColor = Hex("FFFFFF", 0.95f), starsInSky = true,
            },
            // 2: Playground — bright candy colors, sandy ground, balloon scenery.
            new BackgroundTheme
            {
                name = "PLAYGROUND",
                skyTint = Hex("8FD9F7"), skyGround = Hex("FFF3D0"), camBackground = Hex("7FD8F5"),
                fogColor = Hex("FFF0DA"), groundColor = Hex("FFD873"),
                trackColor = Hex("FFFFFF"), railColor = Hex("FF6B9D"),
                hillColors = new[] { Hex("FF9AA2"), Hex("FFD3A5"), Hex("A0E7E5") },
                canopyColors = new[] { Hex("FF6B6B"), Hex("FFC94D"), Hex("6FBBE8") },
                trunkColor = Hex("F2F1EC"), cloudColor = new Color(1f, 1f, 1f, 0.92f), starsInSky = false,
            },

            // ----- 3-12: real baked scenes from Palmov's "Low Poly Atmospheric
            // Locations Pack" (Assets/Palmov Island/...) — a big scenic backdrop
            // image behind the same procedural track/hills, tinted to match.
            // Run Editor menu "LingoRun/Bake Shop Backgrounds" to generate the
            // backdropSprite images before these show up textured in-game. -----
            new BackgroundTheme
            {
                name = "AUTUMN FOREST",
                skyTint = Hex("F3B77D"), skyGround = Hex("FFDCA8"), camBackground = Hex("F0A868"),
                fogColor = Hex("F6C98C"), groundColor = Hex("C97B3D"),
                trackColor = Hex("F5E6C8"), railColor = Hex("D65C36"),
                hillColors = new[] { Hex("B5651D"), Hex("8B4513"), Hex("D2691E") },
                canopyColors = new[] { Hex("D2691E"), Hex("CD5C1D"), Hex("E8A33D") },
                trunkColor = Hex("5C4030"), cloudColor = new Color(1f, 1f, 1f, 0.85f), starsInSky = false,
                backdropSprite = "bg_autumn_forest",
                sceneryPropPrefabs = new[] { "autumn_tree", "autumn_tree_2", "autumn_tree_3" },
            },
            new BackgroundTheme
            {
                name = "CAMPING FOREST",
                skyTint = Hex("6FA8C9"), skyGround = Hex("DCE9D5"), camBackground = Hex("5C9BC0"),
                fogColor = Hex("CBE0DD"), groundColor = Hex("5B8A4A"),
                trackColor = Hex("EDE3C8"), railColor = Hex("C0392B"),
                hillColors = new[] { Hex("3F6B3A"), Hex("4F7C42"), Hex("2E5A2A") },
                canopyColors = new[] { Hex("2E7D46"), Hex("3E9350"), Hex("245C34") },
                trunkColor = Hex("6E4520"), cloudColor = new Color(1f, 1f, 1f, 0.9f), starsInSky = false,
                backdropSprite = "bg_camping_forest",
                sceneryPropPrefabs = new[] { "camping_tent_blue", "camping_tent_red", "camping_tent_yellow", "fir_tree_medium" },
            },
            new BackgroundTheme
            {
                name = "CHRISTMAS VILLAGE",
                skyTint = Hex("A9CBE8"), skyGround = Hex("F2F7FB"), camBackground = Hex("9BC1E0"),
                fogColor = Hex("E8F1F8"), groundColor = Hex("F5F8FC"),
                trackColor = Hex("FFFFFF"), railColor = Hex("C0392B"),
                hillColors = new[] { Hex("EAF3FA"), Hex("D7E8F4"), Hex("F5FAFE") },
                canopyColors = new[] { Hex("2E7D46"), Hex("1F5C34"), Hex("3E9350") },
                trunkColor = Hex("6E4520"), cloudColor = new Color(1f, 1f, 1f, 0.95f), starsInSky = false,
                backdropSprite = "bg_christmas_village",
                sceneryPropPrefabs = new[] { "christmas_tree", "wooden_winter_house", "wooden_winter_house_2" },
            },
            new BackgroundTheme
            {
                name = "DESERT PYRAMIDS",
                skyTint = Hex("F7CE7E"), skyGround = Hex("FCE7B8"), camBackground = Hex("F5C36B"),
                fogColor = Hex("F9DFA6"), groundColor = Hex("E3B76A"),
                trackColor = Hex("FFF3D6"), railColor = Hex("B8860B"),
                hillColors = new[] { Hex("D9A55A"), Hex("C4924A"), Hex("E8C07E") },
                canopyColors = new[] { Hex("6B8E4E"), Hex("87A662"), Hex("5A7A3E") },
                trunkColor = Hex("8A6A3E"), cloudColor = new Color(1f, 1f, 1f, 0.7f), starsInSky = false,
                backdropSprite = "bg_desert_pyramids",
                sceneryPropPrefabs = new[] { "pyramid", "pyramid_2", "cactus", "cactus_2" },
            },
            new BackgroundTheme
            {
                name = "DOWNTOWN",
                skyTint = Hex("8FA8C9"), skyGround = Hex("D9E2EC"), camBackground = Hex("7C97BC"),
                fogColor = Hex("C7D4E2"), groundColor = Hex("8B92A0"),
                trackColor = Hex("D8DCE3"), railColor = Hex("F2C94C"),
                hillColors = new[] { Hex("6B7280"), Hex("545B68"), Hex("828C99") },
                canopyColors = new[] { Hex("3EA0D8"), Hex("5FB0E0"), Hex("2E7DAE") },
                trunkColor = Hex("444B54"), cloudColor = new Color(1f, 1f, 1f, 0.8f), starsInSky = false,
                backdropSprite = "bg_downtown",
                sceneryPropPrefabs = new[] { "building", "building_2", "building_3", "building_4" },
            },
            new BackgroundTheme
            {
                name = "JAPANESE ISLAND",
                skyTint = Hex("F6C9D8"), skyGround = Hex("FDEFF3"), camBackground = Hex("F0B8CC"),
                fogColor = Hex("FBE3EC"), groundColor = Hex("8FBF8A"),
                trackColor = Hex("F5EFE3"), railColor = Hex("C0392B"),
                hillColors = new[] { Hex("6FA86E"), Hex("5C9457"), Hex("83BC7C") },
                canopyColors = new[] { Hex("F49AC2"), Hex("F7B5D1"), Hex("EE7FAE") },
                trunkColor = Hex("5C4A3D"), cloudColor = new Color(1f, 1f, 1f, 0.9f), starsInSky = false,
                backdropSprite = "bg_japanese_island",
                sceneryPropPrefabs = new[] { "sakura_tree", "japanese_temple", "torii_gate" },
            },
            new BackgroundTheme
            {
                name = "SOCCER FIELD",
                skyTint = Hex("7FB8E8"), skyGround = Hex("D6ECF7"), camBackground = Hex("6EC9F5"),
                fogColor = Hex("DCEBF7"), groundColor = Hex("4C9A45"),
                trackColor = Hex("FFFFFF"), railColor = Hex("2E7D46"),
                hillColors = new[] { Hex("3E8E41"), Hex("4C9A45"), Hex("579C4F") },
                canopyColors = new[] { Hex("F2C94C"), Hex("FFFFFF"), Hex("2E7D46") },
                trunkColor = Hex("6E4520"), cloudColor = new Color(1f, 1f, 1f, 0.92f), starsInSky = false,
                backdropSprite = "bg_soccer_field",
                sceneryPropPrefabs = new[] { "soccer_goal", "soccer_grandstand", "mesh_fence" },
            },
            new BackgroundTheme
            {
                name = "ROCKET LAUNCH",
                skyTint = Hex("2A2550"), skyGround = Hex("140F30"), camBackground = Hex("1C1640"),
                fogColor = Hex("241D4A"), groundColor = Hex("35304F"),
                trackColor = Hex("463F6E"), railColor = Hex("FF7A3D"),
                hillColors = new[] { Hex("352A66"), Hex("2B2153"), Hex("463887") },
                canopyColors = new[] { Hex("FF7A3D"), Hex("FFC94D"), Hex("FF4D4D") },
                trunkColor = Hex("120B2E"), cloudColor = Hex("FFFFFF", 0.95f), starsInSky = true,
                backdropSprite = "bg_space_shuttle",
                sceneryPropPrefabs = new[] { "space_shuttle" },
            },
            new BackgroundTheme
            {
                name = "TREASURE COVE",
                skyTint = Hex("7FD8E0"), skyGround = Hex("E8F9F4"), camBackground = Hex("6ECBD8"),
                fogColor = Hex("DFF6F2"), groundColor = Hex("E8D19E"),
                trackColor = Hex("FFF6DF"), railColor = Hex("C79A5C"),
                hillColors = new[] { Hex("2FA6A0"), Hex("3BB6AC"), Hex("258C87") },
                canopyColors = new[] { Hex("2E7D46"), Hex("3E9350"), Hex("4FA85D") },
                trunkColor = Hex("8A6647"), cloudColor = new Color(1f, 1f, 1f, 0.9f), starsInSky = false,
                backdropSprite = "bg_treasure_island",
                sceneryPropPrefabs = new[] { "palm_tree_large", "palm_tree_bent", "wooden_pier", "chest" },
            },
            new BackgroundTheme
            {
                name = "WILD WEST",
                skyTint = Hex("E8B074"), skyGround = Hex("F7DDB0"), camBackground = Hex("E0A468"),
                fogColor = Hex("F0CB93"), groundColor = Hex("C79A5C"),
                trackColor = Hex("E8D3AC"), railColor = Hex("8B3A2F"),
                hillColors = new[] { Hex("B8834F"), Hex("A8763F"), Hex("C79358") },
                canopyColors = new[] { Hex("8B6B3D"), Hex("6E4520"), Hex("A8763F") },
                trunkColor = Hex("6E4520"), cloudColor = new Color(1f, 1f, 1f, 0.8f), starsInSky = false,
                backdropSprite = "bg_wild_west_station",
                sceneryPropPrefabs = new[] { "railway_station", "hitching_post", "wooden_post" },
            },
        };
    }
}
