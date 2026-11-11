using UnityEngine;

namespace LingoRun
{
    /// <summary>One selectable learning language: which vocab_xx.json /
    /// sentences_xx.json pair to load, plus display metadata for the picker.</summary>
    public struct LanguageInfo
    {
        public string code;   // "es" / "fr" / "it" / "de" - matches vocab_<code>.json
        public string name;   // "Spanish" / "French" / ...
        public string badge;  // short 2-letter label for the picker UI (no emoji font risk)
    }

    /// <summary>
    /// Single entry point for switching the active learning language.
    /// Progress (level, mastery, coins, shop, everything in ProgressManager's
    /// save blob) is tracked per language, same as the existing per-profile
    /// save system — switching languages is like switching to a fresh
    /// "track" for the same local profile, so vocabulary progress in one
    /// language never gets treated as progress in another. The active
    /// language itself is a small standalone PlayerPrefs value (not part of
    /// any one profile's save blob) so it's readable before that blob loads.
    /// </summary>
    public static class LanguageManager
    {
        const string PrefsKey = "LingoRun.ActiveLanguage";

        public static readonly LanguageInfo[] All =
        {
            new LanguageInfo { code = "es", name = "Spanish",    badge = "ES" },
            new LanguageInfo { code = "fr", name = "French",     badge = "FR" },
            new LanguageInfo { code = "it", name = "Italian",    badge = "IT" },
            new LanguageInfo { code = "de", name = "German",     badge = "DE" },
            new LanguageInfo { code = "pt", name = "Portuguese", badge = "PT" },
            new LanguageInfo { code = "tr", name = "Turkish",    badge = "TR" },
            new LanguageInfo { code = "ru", name = "Russian",    badge = "RU" },
            new LanguageInfo { code = "ja", name = "Japanese",   badge = "JA" },
            new LanguageInfo { code = "ko", name = "Korean",     badge = "KO" },
            new LanguageInfo { code = "zh", name = "Chinese",    badge = "ZH" },
        };

        static string _current;

        /// <summary>Currently active language code, e.g. "es". Lazily reads the
        /// last-saved choice from PlayerPrefs on first access.</summary>
        public static string Current
        {
            get
            {
                if (_current == null) _current = PlayerPrefs.GetString(PrefsKey, "es");
                return _current;
            }
        }

        public static LanguageInfo CurrentInfo
        {
            get
            {
                string c = Current;
                for (int i = 0; i < All.Length; i++)
                    if (All[i].code == c) return All[i];
                return All[0];
            }
        }

        /// <summary>Switch to a new active language: reloads the vocab and
        /// sentence databases and swaps ProgressManager over to that
        /// language's save blob (same local profile). No-op if already active.</summary>
        public static void SetLanguage(string code)
        {
            if (string.IsNullOrEmpty(code) || code == Current) return;
            _current = code;
            PlayerPrefs.SetString(PrefsKey, _current);
            PlayerPrefs.Save();
            VocabDatabase.SetLanguage(_current);
            SentenceDatabase.SetLanguage(_current);
            ProgressManager.ReloadForLanguageChange();
        }

        /// <summary>Called once at boot, before the first VocabDatabase/
        /// SentenceDatabase/ProgressManager access, so everything loads the
        /// previously-saved language from the very first frame.</summary>
        public static void EnsureLoaded()
        {
            VocabDatabase.SetLanguage(Current);
            SentenceDatabase.SetLanguage(Current);
        }
    }
}
