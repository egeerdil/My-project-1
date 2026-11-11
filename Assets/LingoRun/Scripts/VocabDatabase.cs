using System;
using System.Collections.Generic;
using UnityEngine;

namespace LingoRun
{
    [Serializable]
    public class WordEntry
    {
        public string en;    // English key (unique per language)
        public string tgt;   // target-language word (with article)
        public string[] t;   // topic tags
        public string s;     // sub-category tag
    }

    [Serializable]
    public class VocabFile
    {
        public string language;
        public string name;
        public WordEntry[] words;
        public string[] a1Keys;
    }

    /// <summary>
    /// Loads vocab_es.json from Resources and exposes the word pool helpers
    /// (buildWordPool / weightedPick / pickN ports from lingorun_v48.jsx).
    /// </summary>
    public static class VocabDatabase
    {
        /// <summary>Canonical topic tags used across the vocab data (all
        /// languages share the same tag set) — this is what the onboarding
        /// topic picker and the Dictionary section ordering are built from.</summary>
        public static readonly string[] TopicOrder =
        {
            "essentials", "food", "home", "love", "nature", "work", "tech",
            "fashion", "travel", "sports", "school", "books", "music",
            "movies", "art", "coffee",
        };

        static VocabFile _data;
        static HashSet<string> _a1;
        static Dictionary<string, WordEntry> _byEn;
        static Dictionary<string, WordEntry> _byTgtSurface;
        static string _langCode = "es";

        // Articles stripped when matching a bare sentence-blank word ("café")
        // against vocab targets ("el café") — covers es/fr/it/de.
        static readonly HashSet<string> Articles = new HashSet<string>
        {
            "el", "la", "los", "las", "un", "una", "unos", "unas",
            "le", "les", "l", "il", "lo", "i", "gli", "uno",
            "der", "die", "das", "den", "dem", "ein", "eine",
        };

        public static string LanguageName { get { Load(); return _data.name; } }
        public static List<WordEntry> AllWords { get { Load(); return new List<WordEntry>(_data.words); } }

        public static void Load()
        {
            if (_data != null) return;
            string resourceName = "vocab_" + _langCode;
            TextAsset ta = Resources.Load<TextAsset>(resourceName);
            if (ta == null)
            {
                Debug.LogError("[LingoRun] " + resourceName + ".json not found in a Resources folder.");
                _data = new VocabFile { language = _langCode, name = _langCode, words = new WordEntry[0], a1Keys = new string[0] };
            }
            else
            {
                _data = JsonUtility.FromJson<VocabFile>(ta.text);
            }
            _a1 = new HashSet<string>(_data.a1Keys);
            _byEn = new Dictionary<string, WordEntry>();
            _byTgtSurface = new Dictionary<string, WordEntry>();
            foreach (var w in _data.words)
            {
                if (!_byEn.ContainsKey(w.en)) _byEn[w.en] = w;
                string surf = StripArticle(w.tgt);
                if (surf.Length > 0 && !_byTgtSurface.ContainsKey(surf)) _byTgtSurface[surf] = w;
            }
        }

        static string StripArticle(string tgt)
        {
            if (string.IsNullOrEmpty(tgt)) return "";
            string s = tgt.Trim().ToLowerInvariant();
            if (s.StartsWith("l'")) return s.Substring(2).Trim();
            int space = s.IndexOf(' ');
            if (space > 0 && Articles.Contains(s.Substring(0, space)))
                return s.Substring(space + 1).Trim();
            return s;
        }

        /// <summary>Find a vocab entry by its bare target-language surface
        /// form (articles stripped, case-insensitive) — used by the
        /// sentence→dictionary bridge for non-verb blanks. Null if none.</summary>
        public static WordEntry FindByTargetSurface(string surface)
        {
            Load();
            if (string.IsNullOrEmpty(surface)) return null;
            WordEntry w;
            return _byTgtSurface.TryGetValue(StripArticle(surface), out w) ? w : null;
        }

        /// <summary>Switch the active language and force a reload (see
        /// LanguageManager.SetLanguage). No-op if already on this language
        /// and already loaded.</summary>
        public static void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) code = "es";
            if (_langCode == code && _data != null) return;
            _langCode = code;
            _data = null;
            Load();
        }

        public static bool IsA1(string enKey) { Load(); return _a1.Contains(enKey); }
        public static string WordLevel(string enKey) { return IsA1(enKey) ? "A1" : "A2"; }

        public static WordEntry FindByEn(string enKey)
        {
            Load();
            WordEntry w;
            return _byEn.TryGetValue(enKey, out w) ? w : null;
        }

        /// <summary>
        /// Port of buildWordPool: filter by unlocked level, then (optionally) topics.
        /// Topic-matched words are doubled so they spawn more often; universal words stay in.
        /// </summary>
        public static List<WordEntry> BuildPool(List<string> topics, bool a2Unlocked)
        {
            Load();
            var levelFiltered = new List<WordEntry>();
            foreach (var w in _data.words)
                if (a2Unlocked || IsA1(w.en)) levelFiltered.Add(w);

            if (topics == null || topics.Count == 0) return levelFiltered;

            var topicSet = new HashSet<string>(topics);
            var matched = new List<WordEntry>();
            var universal = new List<WordEntry>();
            foreach (var w in levelFiltered)
            {
                bool hit = false;
                if (w.t != null)
                    foreach (var tag in w.t)
                        if (topicSet.Contains(tag)) { hit = true; break; }
                if (hit) matched.Add(w);
                else if (w.t == null || w.t.Length == 0) universal.Add(w);
            }
            if (matched.Count < 8) return levelFiltered;

            var pool = new List<WordEntry>(matched.Count * 2 + universal.Count);
            pool.AddRange(matched);
            pool.AddRange(matched);
            pool.AddRange(universal);
            return pool;
        }

        /// <summary>
        /// Level-curriculum ordering: every unlocked word exactly ONCE, in a
        /// stable order with the player's chosen topics first (data order
        /// inside each half). Unlike BuildPool this never duplicates entries,
        /// so RunningGame3D can slice it sequentially — "level 1 = first 8
        /// words" — and personalization simply means your favorite topics
        /// are what the early levels teach.
        /// </summary>
        public static List<WordEntry> BuildCurriculum(List<string> topics, bool a2Unlocked)
        {
            Load();
            var levelFiltered = new List<WordEntry>();
            foreach (var w in _data.words)
                if (a2Unlocked || IsA1(w.en)) levelFiltered.Add(w);

            if (topics == null || topics.Count == 0) return levelFiltered;

            var topicSet = new HashSet<string>(topics);
            var matched = new List<WordEntry>();
            var rest = new List<WordEntry>();
            foreach (var w in levelFiltered)
            {
                bool hit = false;
                if (w.t != null)
                    foreach (var tag in w.t)
                        if (topicSet.Contains(tag)) { hit = true; break; }
                if (hit) matched.Add(w); else rest.Add(w);
            }
            matched.AddRange(rest);
            return matched;
        }

        /// <summary>
        /// Port of weightedPick: weight = difficultyScore + 1 so every word keeps a
        /// baseline chance, while frequently-missed words spawn proportionally more.
        /// </summary>
        public static WordEntry WeightedPick(List<WordEntry> pool, Dictionary<string, int> difficultyScores)
        {
            if (pool == null || pool.Count == 0) return null;
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                int score;
                difficultyScores.TryGetValue(pool[i].en, out score);
                total += Mathf.Max(0, score) + 1;
            }
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                int score;
                difficultyScores.TryGetValue(pool[i].en, out score);
                r -= Mathf.Max(0, score) + 1;
                if (r <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        /// <summary>Port of pickN: n random distinct words whose tgt differs from the excluded word.</summary>
        public static List<WordEntry> PickDistractors(WordEntry correct, int n)
        {
            Load();
            var pool = new List<WordEntry>();
            foreach (var w in _data.words)
                if (w.tgt != correct.tgt && w.en != correct.en) pool.Add(w);

            var outList = new List<WordEntry>(n);
            while (outList.Count < n && pool.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, pool.Count);
                outList.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return outList;
        }
    }
}
