using UnityEngine;

namespace LingoRun
{
    /// <summary>
    /// Game Center integration — the no-backend way to get friends +
    /// leaderboards on iOS. Uses Unity's built-in Social API (GameKit under
    /// the hood): sign-in happens automatically at boot (iOS shows its own
    /// banner/sheet), scores are pushed after every run, and the RANKS
    /// button opens Apple's leaderboard UI, which includes the Friends tab
    /// out of the box.
    ///
    /// The three leaderboards must be created in App Store Connect
    /// (My App > Services > Game Center) with EXACTLY these IDs:
    ///   lingorun.highscore  — best run score (Score Format: Integer)
    ///   lingorun.maplevel   — highest map level reached
    ///   lingorun.mastered   — words mastered
    /// Everything no-ops safely in the editor and on non-iOS platforms.
    /// </summary>
    public static class GameCenterManager
    {
        public const string LeaderboardHighScore = "lingorun.highscore";
        public const string LeaderboardMapLevel = "lingorun.maplevel";
        public const string LeaderboardMastered = "lingorun.mastered";

        public static bool Authenticated { get; private set; }
        public static string PlayerAlias { get; private set; }

        static bool _authInProgress;

        /// <summary>Kick off Game Center sign-in (call once at boot; safe to
        /// call again — e.g. before showing leaderboards after a failure).</summary>
        public static void Authenticate()
        {
            if (Authenticated || _authInProgress) return;
#if UNITY_IOS && !UNITY_EDITOR
            _authInProgress = true;
            Social.localUser.Authenticate(delegate (bool success)
            {
                _authInProgress = false;
                Authenticated = success;
                PlayerAlias = success ? Social.localUser.userName : "";
                Debug.Log("[LingoRun] Game Center auth: " + (success ? PlayerAlias : "failed/declined"));
                if (success) ReportAll();
            });
#else
            Debug.Log("[LingoRun] Game Center: editor/non-iOS, auth skipped.");
#endif
        }

        /// <summary>Push the player's current bests to all leaderboards —
        /// called after sign-in and after every run (Game Center keeps the
        /// max itself, so re-reporting the same value is harmless).</summary>
        public static void ReportAll()
        {
            if (!Authenticated) return;
            Report(ProgressManager.HighScore, LeaderboardHighScore);
            Report(ProgressManager.CurrentLevel, LeaderboardMapLevel);
            Report(ProgressManager.MasteredA1Count, LeaderboardMastered);
        }

        static void Report(long value, string leaderboardId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (value <= 0) return;
            Social.ReportScore(value, leaderboardId, delegate (bool ok)
            {
                if (!ok) Debug.Log("[LingoRun] Game Center report failed: " + leaderboardId);
            });
#endif
        }

        /// <summary>Open Apple's leaderboard UI (includes the Friends tab).
        /// Retries sign-in first when the player isn't authenticated yet.</summary>
        public static void ShowLeaderboards()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (!Authenticated) { Authenticate(); return; }
            Social.ShowLeaderboardUI();
#else
            Debug.Log("[LingoRun] Game Center: leaderboard UI only exists on device.");
#endif
        }
    }
}
