using System;
using System.Collections.Generic;
using UnityEngine;

namespace LingoRun
{
    /// <summary>Results of one finished run, handed to the game-over screen.</summary>
    public class RunResults
    {
        public int score;
        public int wordsLearned;
        public int bestStreak;
        public int totalChallenges;
        public int accuracy;          // percent, 0-100
        public bool reviewMode;
        public bool isBlaster;        // true when this run came from Word Blaster, not the runner
        public bool isBubbles;        // true when this run came from Bubble Spell, not the runner
        public bool isMatch;          // true when this run came from Memory Match
        public bool isSpeech;         // true when this run came from pronunciation practice
        public bool isFreePlay;       // true when launched from the Free Play picker
        public bool newBest;
        public int newlyMastered;     // words that crossed MasteryThreshold this run
        public bool a2JustUnlocked;
        public int missedPoolCount;   // size of review pool after this run
        public List<string> newAchievements = new List<string>(); // titles unlocked this run

        // ----- level structure -----
        public bool levelMode;          // fixed word-set level (Words mode, non-review)
        public bool levelCompleted;     // survived all gates of the level
        public int levelNumber;
        public int levelBonusCoins;
        // True only when levelCompleted AND this run's mode matched what the
        // World Map's cycle calls for at levelNumber, so CurrentLevel
        // actually advanced. Completing a level in the "wrong" mode for this
        // slot still pays levelBonusCoins but leaves this false — replaying
        // still shows the same levelNumber next time.
        public bool levelAdvanced;

        // ----- World Map -----
        public int mapLevel;            // >0 when this run was launched from a specific map node
        public bool mapPassed;          // that node's pass/fail (see RecordRunEnd)

        // ----- coin economy -----
        public int runCoins;            // earned from this run's score
        public int achievementCoins;    // bonus from achievements unlocked this run
        public bool dailyGoalBonus;     // daily goal crossed this run (+DailyGoalCoins)
        public int chestCoins;          // chest roll: coins (0 if the chest held an unlock)
        public int chestSkinIndex = -1;  // chest roll: ball color unlocked
        public int chestShapeIndex = -1; // chest roll: ball shape unlocked
        public int coinsBalance;        // balance after everything was applied

        // ----- hearts -----
        public bool heartLost;          // failed level/map node cost one heart
        public bool heartEarned;        // completed review practice earned one back

        // ----- gems (premium currency) -----
        public int gemsEarned;          // unit completion / jackpot / achievements this run
    }

    /// <summary>
    /// Persistent progression, ported from the endGame() logic of lingorun_v48.jsx:
    /// missed-word review pool, per-word correct counts, mastery (3x correct),
    /// A1->A2 unlock at 300 mastered words, spaced-repetition weights, high score —
    /// plus lifetime stats and achievements. Saved as JSON in PlayerPrefs,
    /// one save blob per local profile.
    /// </summary>
    public static class ProgressManager
    {
        const string BaseKeyPrefix = "LingoRun.Save.";
        const string BaseKeySuffix = ".v1";

        [Serializable]
        class SaveBlob
        {
            public int highScore;
            public bool a2Unlocked;
            public int difficulty; // 0 calm, 1 normal, 2 fast
            public List<string> missedKeys = new List<string>();
            public List<int> missedCounts = new List<int>();
            public List<string> correctKeys = new List<string>();
            public List<int> correctCountsList = new List<int>();
            public List<string> masteredA1 = new List<string>();
            public List<string> diffKeys = new List<string>();
            public List<int> diffScores = new List<int>();
            // lifetime stats
            public int totalRuns;
            public int lifetimeBestStreak;
            public int totalCorrect;
            public int totalMistakes;
            public List<string> achievements = new List<string>();
            // daily streak / goal
            public string lastPlayDate = ""; // "yyyy-MM-dd"
            public int dayStreak;
            public int todayCount;
            // cosmetics + economy
            public int selectedSkin;
            public int selectedShape;
            public int selectedTrail;
            public int selectedBackground;
            public int coins;
            public int currentLevel;
            public List<int> ownedSkins = new List<int>();
            public List<int> ownedShapes = new List<int>();
            public List<int> ownedTrails = new List<int>();
            public List<int> ownedBackgrounds = new List<int>();
        }

        static bool _loaded;
        static string _profile = "";

        static string PrefsKey
        {
            get
            {
                string key = BaseKeyPrefix + LanguageManager.Current + BaseKeySuffix;
                return string.IsNullOrEmpty(_profile) ? key : key + "." + _profile;
            }
        }

        public static string ActiveProfile { get { return _profile; } }

        public static int HighScore { get; private set; }
        public static bool A2Unlocked { get; private set; }
        public static Difficulty SavedDifficulty { get; set; }

        // lifetime stats
        public static int TotalRuns { get; private set; }
        public static int LifetimeBestStreak { get; private set; }
        public static int TotalCorrect { get; private set; }
        public static int TotalMistakes { get; private set; }

        // daily streak / goal
        static DateTime _lastPlayDate = DateTime.MinValue;
        public static int DayStreakRaw { get; private set; }
        public static int TodayCountRaw { get; private set; }
        public static int SelectedSkin { get; set; }
        public static int SelectedShape { get; set; }

        public static int SelectedTrail { get; set; }
        public static int SelectedBackground { get; set; }

        /// <summary>Current level in Words mode; advances only when a level is completed.</summary>
        public static int CurrentLevel { get; private set; } = 1;

        /// <summary>How far the World Map's unlocked frontier reaches — the same
        /// underlying progression as CurrentLevel (Home's sequential level flow),
        /// just exposed under the name MapScreenUI reads for its locked/unlocked
        /// node check. Kept as an alias (rather than a second saved counter) so
        /// the two views of level progress can never drift out of sync.</summary>
        public static int MapLevelReached { get { return CurrentLevel; } }

        // coin economy
        public static int Coins { get; private set; }
        public static HashSet<int> OwnedSkins = new HashSet<int> { 0 };
        public static HashSet<int> OwnedShapes = new HashSet<int> { 0 };
        public static HashSet<int> OwnedTrails = new HashSet<int> { 0 };
        public static HashSet<int> OwnedBackgrounds = new HashSet<int> { 0 };

        public static void AddCoins(int amount) { Coins += Mathf.Max(0, amount); }

        /// <summary>Spend coins if affordable. Returns false (no change) otherwise.</summary>
        public static bool TrySpendCoins(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            Coins -= amount;
            return true;
        }

        // ----- gems: the premium (hard) currency -----
        // Unlike coins (earned freely, per profile+language), gems are
        // DEVICE-global and scarce: a trickle from finishing units, chest
        // jackpots and achievements, with real-money gem packs (IAP) as the
        // main faucet later. Spent on things coins can't buy: instant heart
        // refills, premium profile slots (extra languages), gem→coin packs.
        const string GemsKey = "LingoRun.Gems";

        public static int Gems
        {
            get { return PlayerPrefs.GetInt(GemsKey, 0); }
        }

        public static void AddGems(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(GemsKey, Gems + amount);
            PlayerPrefs.Save();
        }

        /// <summary>Spend gems if affordable. Returns false (no change) otherwise.</summary>
        public static bool TrySpendGems(int amount)
        {
            if (amount < 0 || Gems < amount) return false;
            PlayerPrefs.SetInt(GemsKey, Gems - amount);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Streak shown on Home: 0 if the chain is already broken.</summary>
        public static int DayStreakDisplay
        {
            get
            {
                Load();
                if (_lastPlayDate == DateTime.MinValue) return 0;
                int diff = (DateTime.Today - _lastPlayDate.Date).Days;
                return diff <= 1 ? DayStreakRaw : 0;
            }
        }

        /// <summary>Catches made today (0 if the player hasn't played today).</summary>
        public static int TodayCatches
        {
            get
            {
                Load();
                return _lastPlayDate.Date == DateTime.Today ? TodayCountRaw : 0;
            }
        }

        // en -> times missed (this IS the review pool)
        public static Dictionary<string, int> MissedWords = new Dictionary<string, int>();
        // en -> correct answers across runs
        public static Dictionary<string, int> CorrectCounts = new Dictionary<string, int>();
        // en keys of mastered A1 words
        public static HashSet<string> MasteredA1 = new HashSet<string>();
        // en -> spaced-repetition weight (missed +2, correct -1, floor 0)
        public static Dictionary<string, int> DifficultyScores = new Dictionary<string, int>();
        // achievement ids already unlocked
        public static HashSet<string> UnlockedAchievements = new HashSet<string>();

        public static int MasteredA1Count { get { Load(); return MasteredA1.Count; } }
        public static int MissedCount { get { Load(); return MissedWords.Count; } }

        public static List<string> MissedKeys()
        {
            Load();
            return new List<string>(MissedWords.Keys);
        }

        /// <summary>Review-pool keys for one mode (word keys vs "s|" sentence keys).</summary>
        public static List<string> MissedKeysFor(bool sentences)
        {
            Load();
            var list = new List<string>();
            foreach (var k in MissedWords.Keys)
                if (k.StartsWith(SentenceDatabase.KeyPrefix) == sentences) list.Add(k);
            return list;
        }

        public static int MissedCountFor(bool sentences)
        {
            return MissedKeysFor(sentences).Count;
        }

        /// <summary>Switch to another local profile: save current, load the new one.</summary>
        public static void SetProfile(string name)
        {
            if (_loaded && _profile == name) return;
            if (_loaded) Save();
            _profile = name ?? "";
            _loaded = false;
            ResetState();
            Load();
        }

        /// <summary>Reload the save blob after LanguageManager switches the
        /// active language: saves the outgoing language's progress (same
        /// profile) and loads the new language's blob, which starts fresh
        /// (StartingCoins etc.) the first time a profile plays that language.</summary>
        public static void ReloadForLanguageChange()
        {
            if (_loaded) Save();
            _loaded = false;
            ResetState();
            Load();
        }

        static void ResetState()
        {
            HighScore = 0;
            A2Unlocked = false;
            SavedDifficulty = Difficulty.Calm;
            TotalRuns = 0;
            LifetimeBestStreak = 0;
            TotalCorrect = 0;
            TotalMistakes = 0;
            _lastPlayDate = DateTime.MinValue;
            DayStreakRaw = 0;
            TodayCountRaw = 0;
            SelectedSkin = 0;
            SelectedShape = 0;
            SelectedTrail = 0;
            SelectedBackground = 0;
            CurrentLevel = 1;
            // Brand-new profile: gift starting coins. Load() below will overwrite
            // this with the saved balance if a save for this profile already exists.
            Coins = GameConfig.StartingCoins;
            OwnedSkins = new HashSet<int> { 0 };
            OwnedShapes = new HashSet<int> { 0 };
            OwnedTrails = new HashSet<int> { 0 };
            OwnedBackgrounds = new HashSet<int> { 0 };
            MissedWords = new Dictionary<string, int>();
            CorrectCounts = new Dictionary<string, int>();
            MasteredA1 = new HashSet<string>();
            DifficultyScores = new Dictionary<string, int>();
            UnlockedAchievements = new HashSet<string>();
        }

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            if (!PlayerPrefs.HasKey(PrefsKey)) return;
            try
            {
                var blob = JsonUtility.FromJson<SaveBlob>(PlayerPrefs.GetString(PrefsKey));
                if (blob == null) return;
                HighScore = blob.highScore;
                A2Unlocked = blob.a2Unlocked;
                SavedDifficulty = (Difficulty)Mathf.Clamp(blob.difficulty, 0, 2);
                MissedWords = ZipDict(blob.missedKeys, blob.missedCounts);
                CorrectCounts = ZipDict(blob.correctKeys, blob.correctCountsList);
                MasteredA1 = new HashSet<string>(blob.masteredA1 ?? new List<string>());
                DifficultyScores = ZipDict(blob.diffKeys, blob.diffScores);
                TotalRuns = blob.totalRuns;
                LifetimeBestStreak = blob.lifetimeBestStreak;
                TotalCorrect = blob.totalCorrect;
                TotalMistakes = blob.totalMistakes;
                UnlockedAchievements = new HashSet<string>(blob.achievements ?? new List<string>());
                DayStreakRaw = blob.dayStreak;
                TodayCountRaw = blob.todayCount;
                SelectedSkin = blob.selectedSkin;
                SelectedShape = blob.selectedShape;
                SelectedTrail = blob.selectedTrail;
                SelectedBackground = blob.selectedBackground;
                CurrentLevel = Mathf.Max(1, blob.currentLevel);
                Coins = blob.coins;
                OwnedSkins = new HashSet<int>(blob.ownedSkins ?? new List<int>());
                OwnedSkins.Add(0);
                OwnedShapes = new HashSet<int>(blob.ownedShapes ?? new List<int>());
                OwnedShapes.Add(0);
                OwnedTrails = new HashSet<int>(blob.ownedTrails ?? new List<int>());
                OwnedTrails.Add(0);
                OwnedBackgrounds = new HashSet<int>(blob.ownedBackgrounds ?? new List<int>());
                OwnedBackgrounds.Add(0);
                if (!string.IsNullOrEmpty(blob.lastPlayDate))
                {
                    DateTime d;
                    if (DateTime.TryParse(blob.lastPlayDate, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out d))
                        _lastPlayDate = d;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LingoRun] Failed to load save: " + e.Message);
            }
        }

        public static void Save()
        {
            var blob = new SaveBlob
            {
                highScore = HighScore,
                a2Unlocked = A2Unlocked,
                difficulty = (int)SavedDifficulty,
                totalRuns = TotalRuns,
                lifetimeBestStreak = LifetimeBestStreak,
                totalCorrect = TotalCorrect,
                totalMistakes = TotalMistakes,
                dayStreak = DayStreakRaw,
                todayCount = TodayCountRaw,
                selectedSkin = SelectedSkin,
                selectedShape = SelectedShape,
                selectedTrail = SelectedTrail,
                selectedBackground = SelectedBackground,
                currentLevel = CurrentLevel,
                coins = Coins,
                lastPlayDate = _lastPlayDate == DateTime.MinValue
                    ? "" : _lastPlayDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            };
            UnzipDict(MissedWords, blob.missedKeys, blob.missedCounts);
            UnzipDict(CorrectCounts, blob.correctKeys, blob.correctCountsList);
            blob.masteredA1.AddRange(MasteredA1);
            UnzipDict(DifficultyScores, blob.diffKeys, blob.diffScores);
            blob.achievements.AddRange(UnlockedAchievements);
            blob.ownedSkins.AddRange(OwnedSkins);
            blob.ownedShapes.AddRange(OwnedShapes);
            blob.ownedTrails.AddRange(OwnedTrails);
            blob.ownedBackgrounds.AddRange(OwnedBackgrounds);
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(blob));
            PlayerPrefs.Save();
        }

        static Dictionary<string, int> ZipDict(List<string> keys, List<int> vals)
        {
            var d = new Dictionary<string, int>();
            if (keys == null || vals == null) return d;
            int n = Mathf.Min(keys.Count, vals.Count);
            for (int i = 0; i < n; i++) d[keys[i]] = vals[i];
            return d;
        }

        static void UnzipDict(Dictionary<string, int> d, List<string> keys, List<int> vals)
        {
            foreach (var kv in d) { keys.Add(kv.Key); vals.Add(kv.Value); }
        }

        /// <summary>
        /// Apply one run's outcome. Mirrors endGame() from the web version (Running
        /// mode), then updates lifetime stats and evaluates achievements.
        /// sessionMissed / sessionLearned contain English keys.
        /// </summary>
        public static RunResults RecordRunEnd(
            int score, int bestStreak,
            HashSet<string> sessionMissed, HashSet<string> sessionLearned,
            int totalChallenges, bool reviewMode, bool sentenceMode = false,
            bool levelMode = false, bool levelCompleted = false, int mapLevel = 0)
        {
            Load();
            var results = new RunResults
            {
                score = score,
                bestStreak = bestStreak,
                wordsLearned = sessionLearned.Count,
                totalChallenges = totalChallenges,
                reviewMode = reviewMode,
                levelMode = levelMode,
                levelCompleted = levelCompleted,
                levelNumber = CurrentLevel,
            };

            // Level progression: completing a level always pays its bonus
            // coins, but only ADVANCES the level counter if this run's mode
            // is the one the World Map's cycle actually calls for at the
            // CURRENT level (GameConfig.GetMapLevel interleaves Words /
            // Sentences / Blaster). So finishing a Words level while the map
            // is asking for Sentences at this slot still earns coins and
            // lets you keep practicing, but doesn't skip you past that
            // Sentences checkpoint — only clearing it in the map-designated
            // mode does. Map-launched runs (mapLevel > 0, below) are exempt
            // — StartFromMap already forces the correct mode for that node.
            if (levelMode && levelCompleted)
            {
                results.levelBonusCoins = GameConfig.LevelCompleteCoins;
                AddCoins(results.levelBonusCoins);
                GameMode runMode = sentenceMode ? GameMode.Sentences : GameMode.Words;
                bool matchesMapCycle = GameConfig.GetMapLevel(CurrentLevel).mode == runMode;
                if (matchesMapCycle)
                {
                    CurrentLevel += 1;
                    results.levelAdvanced = true;
                }
            }

            int correct = totalChallenges - sessionMissed.Count;
            results.accuracy = totalChallenges > 0
                ? Mathf.RoundToInt(100f * correct / totalChallenges) : 0;

            // World Map progression: mapLevel > 0 means this run was launched
            // from a specific map node (LingoRunBootstrap.StartFromMap)
            // rather than Home's own sequential PLAY button. "Passed" uses
            // each mode's natural success signal: levelCompleted for
            // Words-mode nodes (already computed above — a fixed-length
            // level, not a percentage), accuracy vs. the node's required
            // passAccuracy for Sentences/Blaster nodes (which have no fixed
            // "complete" concept). Advances the SAME CurrentLevel counter
            // Home's level path uses (see MapLevelReached) so the two views
            // of progress never drift apart — guarded so a Words-mode node
            // doesn't double-advance past the bonus already paid above.
            if (mapLevel > 0)
            {
                var mapDef = GameConfig.GetMapLevel(mapLevel);
                bool mapPassed = levelMode ? levelCompleted : results.accuracy >= mapDef.passAccuracy;
                results.mapLevel = mapLevel;
                results.mapPassed = mapPassed;
                if (mapPassed && mapLevel == CurrentLevel && !(levelMode && levelCompleted))
                {
                    results.levelBonusCoins = GameConfig.LevelCompleteCoins;
                    AddCoins(results.levelBonusCoins);
                    CurrentLevel += 1;
                    results.levelAdvanced = true;
                }
            }

            if (score > HighScore && !reviewMode)
            {
                HighScore = score;
                results.newBest = true;
            }

            // Sentence↔dictionary bridge (VERB_FORMS port): answering "bebo"
            // correctly also advances "beber"'s mastery below, and missing it
            // puts the verb into the WORD review pool too. Done after the
            // accuracy math so bridged keys don't distort the run's score,
            // but before all mastery/review/spaced-repetition bookkeeping.
            if (sentenceMode)
            {
                BridgeSentenceKeys(sessionLearned);
                BridgeSentenceKeys(sessionMissed);
            }

            // Review pool: misses add to the pool; in review mode, correct answers
            // remove the word from the pool ("mastered the review item").
            foreach (var en in sessionMissed)
            {
                int c;
                MissedWords.TryGetValue(en, out c);
                MissedWords[en] = c + 1;
            }
            if (reviewMode)
                foreach (var en in sessionLearned)
                    MissedWords.Remove(en);

            // Mastery: +1 correct count per learned word; crossing the threshold
            // on an A1 word adds it to masteredA1 and may unlock A2.
            bool wasA2 = A2Unlocked;
            foreach (var en in sessionLearned)
            {
                int c;
                CorrectCounts.TryGetValue(en, out c);
                c += 1;
                CorrectCounts[en] = c;
                if (c >= GameConfig.MasteryThreshold && VocabDatabase.IsA1(en) && !MasteredA1.Contains(en))
                {
                    MasteredA1.Add(en);
                    results.newlyMastered++;
                }
            }
            if (!wasA2 && MasteredA1.Count >= GameConfig.A2UnlockThreshold)
            {
                A2Unlocked = true;
                results.a2JustUnlocked = true;
            }

            // Spaced-repetition weights: wrong +2, right -1 (floor 0, drop at 0).
            foreach (var en in sessionMissed)
            {
                int s;
                DifficultyScores.TryGetValue(en, out s);
                DifficultyScores[en] = s + 2;
            }
            foreach (var en in sessionLearned)
            {
                int s;
                if (DifficultyScores.TryGetValue(en, out s))
                {
                    s = Mathf.Max(0, s - 1);
                    if (s == 0) DifficultyScores.Remove(en);
                    else DifficultyScores[en] = s;
                }
            }

            results.missedPoolCount = MissedCountFor(sentenceMode);

            // Lifetime stats.
            TotalRuns += 1;
            LifetimeBestStreak = Mathf.Max(LifetimeBestStreak, bestStreak);
            TotalCorrect += sessionLearned.Count;
            TotalMistakes += sessionMissed.Count;

            // Daily streak / goal (+ coin bonus the moment the goal is crossed).
            int daysSince = _lastPlayDate == DateTime.MinValue
                ? int.MaxValue : (DateTime.Today - _lastPlayDate.Date).Days;
            int todayBefore = daysSince == 0 ? TodayCountRaw : 0;
            if (daysSince == 0)
            {
                TodayCountRaw += sessionLearned.Count;
            }
            else
            {
                DayStreakRaw = daysSince == 1 ? DayStreakRaw + 1 : 1;
                TodayCountRaw = sessionLearned.Count;
            }
            _lastPlayDate = DateTime.Today;
            if (todayBefore < GameConfig.DailyGoal && TodayCountRaw >= GameConfig.DailyGoal)
            {
                results.dailyGoalBonus = true;
                AddCoins(GameConfig.DailyGoalCoins);
            }

            // Coins from this run's score (1 coin per 10 points).
            results.runCoins = score / 10;
            AddCoins(results.runCoins);

            // Achievements.
            var ctx = new AchievementContext
            {
                run = results,
                totalRuns = TotalRuns,
                masteredCount = MasteredA1.Count,
                lifetimeBestStreak = LifetimeBestStreak,
                totalCorrect = TotalCorrect,
                a2Unlocked = A2Unlocked,
                missedPoolCount = MissedCountFor(sentenceMode),
                highScore = HighScore,
            };
            foreach (var def in Achievements.All)
            {
                if (UnlockedAchievements.Contains(def.id)) continue;
                bool met;
                try { met = def.IsMet(ctx); }
                catch { met = false; }
                if (met)
                {
                    UnlockedAchievements.Add(def.id);
                    results.newAchievements.Add(def.title);
                    results.achievementCoins += GameConfig.AchievementCoins;
                    results.gemsEarned += GameConfig.GemsPerAchievement;
                }
            }
            AddCoins(results.achievementCoins);

            // Gems: finishing a unit's LAST level (the chest node) pays the
            // premium currency — CurrentLevel already advanced, so the level
            // just completed is CurrentLevel - 1.
            if (results.levelAdvanced && (CurrentLevel - 1) % GameConfig.LevelsPerUnit == 0)
                results.gemsEarned += GameConfig.GemsPerUnitComplete;

            RollChest(results);
            results.coinsBalance = Coins;
            if (results.gemsEarned > 0) AddGems(results.gemsEarned);

            // Hearts: a failed level (or failed map node) costs one heart;
            // completing a real review session earns one back — practice is
            // always free AND rewarding (see HeartsManager).
            if (reviewMode)
            {
                if (totalChallenges >= HeartsManager.MinReviewChallenges
                    && HeartsManager.AddHearts(1))
                    results.heartEarned = true;
            }
            else
            {
                bool failed = levelMode
                    ? !levelCompleted
                    : (mapLevel > 0 && !results.mapPassed);
                if (failed && HeartsManager.SpendHeart()) results.heartLost = true;
            }

            Save();
            return results;
        }

        /// <summary>For every sentence key ("s|tpl|correct") in the set, add
        /// the vocab key of the blank's underlying word when it resolves to a
        /// real dictionary entry (see VerbForms.ResolveToVocabKey).</summary>
        static void BridgeSentenceKeys(HashSet<string> keys)
        {
            var bridged = new List<string>();
            foreach (var k in keys)
            {
                if (!k.StartsWith(SentenceDatabase.KeyPrefix)) continue;
                int cut = k.LastIndexOf('|');
                if (cut < 0 || cut + 1 >= k.Length) continue;
                string en = VerbForms.ResolveToVocabKey(k.Substring(cut + 1));
                if (en != null) bridged.Add(en);
            }
            foreach (var en in bridged) keys.Add(en);
        }

        /// <summary>
        /// Run-end chest: weighted random reward, applied immediately (the
        /// game-over screen only *reveals* it, so skipping the screen can't
        /// lose a reward). Rare rolls grant an unowned ball color/shape outright.
        /// </summary>
        static void RollChest(RunResults results)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll >= 96)
            {
                // Jackpots always carry a small gem bonus on top.
                results.gemsEarned += GameConfig.GemsJackpot;
                // Jackpot: try to unlock a random unowned color, then shape.
                var lockedSkins = new List<int>();
                for (int i = 0; i < GameConfig.SkinPrices.Length; i++)
                    if (!OwnedSkins.Contains(i)) lockedSkins.Add(i);
                var lockedShapes = new List<int>();
                for (int i = 0; i < GameConfig.ShapePrices.Length; i++)
                    if (!OwnedShapes.Contains(i)) lockedShapes.Add(i);

                bool pickShape = lockedShapes.Count > 0 &&
                    (lockedSkins.Count == 0 || UnityEngine.Random.value < 0.4f);
                if (pickShape)
                {
                    int idx = lockedShapes[UnityEngine.Random.Range(0, lockedShapes.Count)];
                    OwnedShapes.Add(idx);
                    results.chestShapeIndex = idx;
                    return;
                }
                if (lockedSkins.Count > 0)
                {
                    int idx = lockedSkins[UnityEngine.Random.Range(0, lockedSkins.Count)];
                    OwnedSkins.Add(idx);
                    results.chestSkinIndex = idx;
                    return;
                }
                // Everything owned already — jackpot pays out big coins instead.
                results.chestCoins = 150;
            }
            else if (roll >= 85) results.chestCoins = UnityEngine.Random.Range(60, 101);
            else if (roll >= 55) results.chestCoins = UnityEngine.Random.Range(25, 41);
            else results.chestCoins = UnityEngine.Random.Range(8, 21);

            AddCoins(results.chestCoins);
        }
    }
}
