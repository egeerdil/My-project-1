using System;
using System.Collections.Generic;
using UnityEngine;

namespace LingoRun
{
    /// <summary>
    /// UI localization ("app language", independent of the LEARNING language).
    /// Strings live in Resources/localization.json (164 keys x 11 languages:
    /// en + the ten learning languages). Usage:
    ///   Loc.T("home.review")            → "REVIEW" / "TEKRAR" / "復習" ...
    ///   Loc.F("home.level", 12)         → "LEVEL 12" (string.Format)
    /// The app language is device-global (PlayerPrefs), defaults to the OS
    /// language when supported, and falls back to English for any missing
    /// key/translation. Changing it requires a UI rebuild — see
    /// LingoRunBootstrap.RebuildUI().
    /// </summary>
    public static class Loc
    {
        const string PrefsKey = "LingoRun.UILang";

        [Serializable]
        class LocEntry
        {
            public string k, en, es, fr, it, de, pt, tr, ru, ja, ko, zh;
        }

        [Serializable]
        class LocFile { public List<LocEntry> entries; }

        public static readonly string[] Supported =
            { "en", "es", "fr", "it", "de", "pt", "tr", "ru", "ja", "ko", "zh" };

        static Dictionary<string, LocEntry> _map;
        static string _lang;

        public static string UILang
        {
            get
            {
                if (_lang == null)
                    _lang = PlayerPrefs.GetString(PrefsKey, SystemDefault());
                return _lang;
            }
        }

        public static void SetUILanguage(string code)
        {
            _lang = code;
            PlayerPrefs.SetString(PrefsKey, code);
            PlayerPrefs.Save();
        }

        /// <summary>Display name of a UI language in that language's own
        /// script (for the settings cycle button).</summary>
        public static string NativeName(string code)
        {
            switch (code)
            {
                case "es": return "ESPAÑOL";
                case "fr": return "FRANÇAIS";
                case "it": return "ITALIANO";
                case "de": return "DEUTSCH";
                case "pt": return "PORTUGUÊS";
                case "tr": return "TÜRKÇE";
                case "ru": return "РУССКИЙ";
                case "ja": return "日本語";
                case "ko": return "한국어";
                case "zh": return "中文";
                default: return "ENGLISH";
            }
        }

        static string SystemDefault()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return "zh";
                default: return "en";
            }
        }

        static void Load()
        {
            if (_map != null) return;
            _map = new Dictionary<string, LocEntry>();
            var ta = Resources.Load<TextAsset>("localization");
            if (ta == null)
            {
                Debug.LogError("[LingoRun] localization.json not found in Resources.");
                return;
            }
            var file = JsonUtility.FromJson<LocFile>(ta.text);
            if (file == null || file.entries == null) return;
            foreach (var e in file.entries)
                if (!_map.ContainsKey(e.k)) _map[e.k] = e;
        }

        /// <summary>Translated string for the active app language; falls back
        /// to English, then to the given fallback, then to the key itself.</summary>
        public static string T(string key, string fallback = null)
        {
            Load();
            LocEntry e;
            if (!_map.TryGetValue(key, out e))
                return fallback ?? key;
            string s = Pick(e, UILang);
            if (string.IsNullOrEmpty(s)) s = e.en;
            return string.IsNullOrEmpty(s) ? (fallback ?? key) : s;
        }

        /// <summary>T + string.Format.</summary>
        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { return T(key); }
        }

        /// <summary>Localized pace name (CALM / NORMAL / FAST).</summary>
        public static string Pace(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Fast: return T("pace.fast");
                case Difficulty.Normal: return T("pace.normal");
                default: return T("pace.calm");
            }
        }

        /// <summary>Localized display name for a vocab topic tag.</summary>
        public static string Topic(string tag)
        {
            return T("topic." + tag, tag != null ? tag.ToUpperInvariant() : "");
        }

        /// <summary>Localized name of a LEARNING language (login list etc.).</summary>
        public static string LangName(string code)
        {
            return T("lang." + code, code != null ? code.ToUpperInvariant() : "");
        }

        static string Pick(LocEntry e, string lang)
        {
            switch (lang)
            {
                case "es": return e.es;
                case "fr": return e.fr;
                case "it": return e.it;
                case "de": return e.de;
                case "pt": return e.pt;
                case "tr": return e.tr;
                case "ru": return e.ru;
                case "ja": return e.ja;
                case "ko": return e.ko;
                case "zh": return e.zh;
                default: return e.en;
            }
        }
    }
}
