using System;
using UnityEngine;

namespace LingoRun
{
    /// <summary>
    /// Global hearts (lives): 5 max, shared across all game modes and
    /// languages but tracked per local profile. A failed level (or failed
    /// map node) costs one heart; hearts regenerate over time (one per
    /// RefillMinutes), can be refilled instantly with coins, and — the
    /// pedagogical escape hatch — Review practice is ALWAYS free to play
    /// and EARNS a heart back when completed, so running out of hearts
    /// pushes players toward studying, never away from it.
    /// Stored directly in PlayerPrefs (not the per-language save blob)
    /// because hearts are global per profile.
    /// </summary>
    public static class HeartsManager
    {
        public const int MaxHearts = 5;
        public const int RefillMinutes = 20;       // one heart per 20 minutes
        public const int RefillAllCost = 250;      // coins for an instant full refill
        public const int MinReviewChallenges = 5;  // review answers needed to earn a heart

        const string CountKeyPrefix = "LingoRun.Hearts.";
        const string StampKeyPrefix = "LingoRun.HeartsStamp.";

        static string ProfileKey
        {
            get
            {
                string p = ProfileManager.LastProfile;
                return string.IsNullOrEmpty(p) ? "guest" : p.ToLowerInvariant();
            }
        }

        static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

        static long ReadStamp()
        {
            long v;
            return long.TryParse(PlayerPrefs.GetString(StampKeyPrefix + ProfileKey, "0"), out v) ? v : 0L;
        }

        static void Write(int hearts, long stamp)
        {
            PlayerPrefs.SetInt(CountKeyPrefix + ProfileKey, hearts);
            PlayerPrefs.SetString(StampKeyPrefix + ProfileKey, stamp.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>Current heart count, applying any time-based regeneration
        /// that has accrued since the last check.</summary>
        public static int Hearts { get { return ApplyRegen(); } }

        static int ApplyRegen()
        {
            int hearts = PlayerPrefs.GetInt(CountKeyPrefix + ProfileKey, MaxHearts);
            if (hearts >= MaxHearts) return MaxHearts;

            long now = Now();
            long stamp = ReadStamp();
            if (stamp <= 0 || stamp > now)
            {
                Write(hearts, now);
                return hearts;
            }
            long per = RefillMinutes * 60L;
            int gained = (int)((now - stamp) / per);
            if (gained > 0)
            {
                hearts = Mathf.Min(MaxHearts, hearts + gained);
                Write(hearts, hearts >= MaxHearts ? now : stamp + gained * per);
            }
            return hearts;
        }

        /// <summary>Seconds until the next heart regenerates (0 when full).</summary>
        public static int SecondsToNextHeart
        {
            get
            {
                if (ApplyRegen() >= MaxHearts) return 0;
                long per = RefillMinutes * 60L;
                long elapsed = Now() - ReadStamp();
                return (int)Mathf.Max(1, per - elapsed % per);
            }
        }

        /// <summary>Take one heart (failed level). Returns false when already empty.
        /// Dropping below full starts the regen countdown from now.</summary>
        public static bool SpendHeart()
        {
            int hearts = ApplyRegen();
            if (hearts <= 0) return false;
            Write(hearts - 1, hearts >= MaxHearts ? Now() : ReadStamp());
            return true;
        }

        /// <summary>Grant hearts (review practice reward). Returns true if at
        /// least one was actually added (false when already full).</summary>
        public static bool AddHearts(int n)
        {
            int hearts = ApplyRegen();
            if (hearts >= MaxHearts || n <= 0) return false;
            int after = Mathf.Min(MaxHearts, hearts + n);
            Write(after, after >= MaxHearts ? Now() : ReadStamp());
            return true;
        }

        /// <summary>Instant full refill for coins. Returns false when already
        /// full or the player can't afford it.</summary>
        public static bool TryCoinRefill()
        {
            if (ApplyRegen() >= MaxHearts) return false;
            if (!ProgressManager.TrySpendCoins(RefillAllCost)) return false;
            Write(MaxHearts, Now());
            return true;
        }

        /// <summary>Instant full refill for gems (the premium option).</summary>
        public static bool TryGemRefill()
        {
            if (ApplyRegen() >= MaxHearts) return false;
            if (!ProgressManager.TrySpendGems(GameConfig.HeartRefillGems)) return false;
            Write(MaxHearts, Now());
            return true;
        }
    }
}
