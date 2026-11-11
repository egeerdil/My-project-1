using System.Collections.Generic;
using UnityEngine;

namespace LingoRun
{
    /// <summary>
    /// Local player profiles: a list of names stored in PlayerPrefs. Each profile
    /// gets its own ProgressManager save blob (progress, achievements, high score),
    /// and — since onboarding — its own fixed learning language plus a set of
    /// personalization topics chosen at creation. One profile = one language
    /// (Duolingo-style): to learn another language you create another profile.
    /// The first FreeProfileSlots profiles are free; the remaining slots up to
    /// MaxProfiles are reserved for a future premium unlock.
    /// </summary>
    public static class ProfileManager
    {
        const string ListKey = "LingoRun.Profiles";
        const string LastKey = "LingoRun.LastProfile";
        const string LangKeyPrefix = "LingoRun.ProfileLang.";
        const string TopicsKeyPrefix = "LingoRun.ProfileTopics.";
        const char Sep = '\n';
        const char TopicSep = ',';
        public const int MaxProfiles = 6;
        public const int FreeProfileSlots = 3;
        public const int MaxNameLength = 12;
        public const int MinTopics = 3;

        // Premium slots bought with gems (device-global, like gems themselves).
        const string PremiumSlotsKey = "LingoRun.PremiumSlots";

        public static int PremiumSlots
        {
            get { return PlayerPrefs.GetInt(PremiumSlotsKey, 0); }
        }

        /// <summary>How many profiles this device can hold right now.</summary>
        public static int AvailableSlots
        {
            get { return Mathf.Min(MaxProfiles, FreeProfileSlots + PremiumSlots); }
        }

        /// <summary>Buy one extra profile slot (= one extra language) with
        /// gems. Returns false when maxed out or gems are insufficient.</summary>
        public static bool TryUnlockPremiumSlot()
        {
            if (AvailableSlots >= MaxProfiles) return false;
            if (!ProgressManager.TrySpendGems(GameConfig.PremiumSlotGems)) return false;
            PlayerPrefs.SetInt(PremiumSlotsKey, PremiumSlots + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static List<string> GetProfiles()
        {
            var raw = PlayerPrefs.GetString(ListKey, "");
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var name in raw.Split(Sep))
                if (!string.IsNullOrEmpty(name)) list.Add(name);
            return list;
        }

        public static string LastProfile
        {
            get { return PlayerPrefs.GetString(LastKey, ""); }
        }

        /// <summary>The learning language this profile was created with
        /// ("es"/"fr"/"it"/"de"). Profiles from before onboarding existed
        /// default to Spanish.</summary>
        public static string GetLanguage(string name)
        {
            if (string.IsNullOrEmpty(name)) return "es";
            return PlayerPrefs.GetString(LangKeyPrefix + name.ToLowerInvariant(), "es");
        }

        /// <summary>Personalization topics picked at creation (may be empty
        /// for pre-onboarding profiles — empty means "no preference").</summary>
        public static List<string> GetTopics(string name)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(name)) return list;
            var raw = PlayerPrefs.GetString(TopicsKeyPrefix + name.ToLowerInvariant(), "");
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var topic in raw.Split(TopicSep))
                if (!string.IsNullOrEmpty(topic)) list.Add(topic);
            return list;
        }

        /// <summary>Topics of the currently active (last chosen) profile —
        /// what the games use to personalize their word pools.</summary>
        public static List<string> ActiveTopics
        {
            get { return GetTopics(LastProfile); }
        }

        /// <summary>Sanitize + add a profile with its onboarding choices.
        /// Returns the final name, or null if invalid/duplicate/full.</summary>
        public static string CreateProfile(string rawName, string langCode, List<string> topics)
        {
            if (rawName == null) return null;
            string name = rawName.Trim();
            if (name.Length == 0) return null;
            if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
            name = name.Replace(Sep, ' ').Replace(TopicSep, ' ');

            var profiles = GetProfiles();
            if (profiles.Count >= AvailableSlots) return null;
            foreach (var p in profiles)
                if (p.ToLowerInvariant() == name.ToLowerInvariant()) return null;

            profiles.Add(name);
            PlayerPrefs.SetString(ListKey, string.Join(Sep.ToString(), profiles.ToArray()));

            string keyName = name.ToLowerInvariant();
            PlayerPrefs.SetString(LangKeyPrefix + keyName,
                string.IsNullOrEmpty(langCode) ? "es" : langCode);
            PlayerPrefs.SetString(TopicsKeyPrefix + keyName,
                topics == null ? "" : string.Join(TopicSep.ToString(), topics.ToArray()));
            PlayerPrefs.Save();
            return name;
        }

        /// <summary>Legacy single-arg creation (defaults to Spanish, no topics).</summary>
        public static string CreateProfile(string rawName)
        {
            return CreateProfile(rawName, "es", null);
        }

        /// <summary>Log in: remember the choice, switch to the profile's fixed
        /// language, and point ProgressManager at this profile's save.</summary>
        public static void SetActive(string name)
        {
            PlayerPrefs.SetString(LastKey, name);
            PlayerPrefs.Save();
            LanguageManager.SetLanguage(GetLanguage(name));
            ProgressManager.SetProfile(name);
        }
    }
}
