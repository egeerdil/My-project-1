using System;
using System.Collections.Generic;
using UnityEngine;

namespace LingoRun
{
    [Serializable]
    public class SentenceEntry
    {
        public string tpl;           // template with ___ as the blank
        public string correct;       // the word that fills the blank
        public string[] distractors; // two wrong options
        public string hint;          // English meaning of the full sentence
        public string level;         // "A1" / "A2"
        public string[] topics;
    }

    [Serializable]
    public class SentenceFile
    {
        public string language;
        public SentenceEntry[] sentences;
    }

    /// <summary>
    /// Sentence-completion challenges (Jumping mode data from lingorun_v48.jsx).
    /// Sentence progress keys are namespaced "s|tpl|correct" so they share the
    /// ProgressManager dictionaries with words without colliding.
    /// </summary>
    public static class SentenceDatabase
    {
        public const string KeyPrefix = "s|";

        static SentenceFile _data;
        static Dictionary<string, SentenceEntry> _byKey;
        static string _langCode = "es";

        public static void Load()
        {
            if (_data != null) return;
            string resourceName = "sentences_" + _langCode;
            TextAsset ta = Resources.Load<TextAsset>(resourceName);
            if (ta == null)
            {
                Debug.LogError("[LingoRun] " + resourceName + ".json not found in a Resources folder.");
                _data = new SentenceFile { language = _langCode, sentences = new SentenceEntry[0] };
            }
            else
            {
                _data = JsonUtility.FromJson<SentenceFile>(ta.text);
            }
            _byKey = new Dictionary<string, SentenceEntry>();
            foreach (var s in _data.sentences)
            {
                string k = Key(s);
                if (!_byKey.ContainsKey(k)) _byKey[k] = s;
            }
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

        public static string Key(SentenceEntry s)
        {
            return KeyPrefix + s.tpl + "|" + s.correct;
        }

        public static List<SentenceEntry> All
        {
            get { Load(); return new List<SentenceEntry>(_data.sentences); }
        }

        public static SentenceEntry FindByKey(string key)
        {
            Load();
            SentenceEntry s;
            return _byKey.TryGetValue(key, out s) ? s : null;
        }

        /// <summary>A2 sentences only join the pool once A2 is unlocked.</summary>
        public static List<SentenceEntry> BuildPool(bool a2Unlocked)
        {
            Load();
            var pool = new List<SentenceEntry>();
            foreach (var s in _data.sentences)
                if (a2Unlocked || s.level != "A2") pool.Add(s);
            return pool;
        }

        /// <summary>Level-curriculum ordering (see VocabDatabase.BuildCurriculum):
        /// every unlocked sentence exactly once, topic-matched ones first, so
        /// sequential level slices teach the player's chosen topics early.</summary>
        public static List<SentenceEntry> BuildCurriculum(List<string> topics, bool a2Unlocked)
        {
            var pool = BuildPool(a2Unlocked);
            if (topics == null || topics.Count == 0) return pool;

            var topicSet = new HashSet<string>(topics);
            var matched = new List<SentenceEntry>();
            var rest = new List<SentenceEntry>();
            foreach (var s in pool)
            {
                bool hit = false;
                if (s.topics != null)
                    foreach (var tag in s.topics)
                        if (topicSet.Contains(tag)) { hit = true; break; }
                if (hit) matched.Add(s); else rest.Add(s);
            }
            matched.AddRange(rest);
            return matched;
        }

        /// <summary>Weighted pick: sentences the player keeps missing spawn more.</summary>
        public static SentenceEntry WeightedPick(List<SentenceEntry> pool, Dictionary<string, int> difficultyScores)
        {
            if (pool == null || pool.Count == 0) return null;
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                int score;
                difficultyScores.TryGetValue(Key(pool[i]), out score);
                total += Mathf.Max(0, score) + 1;
            }
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                int score;
                difficultyScores.TryGetValue(Key(pool[i]), out score);
                r -= Mathf.Max(0, score) + 1;
                if (r <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }
    }
}
