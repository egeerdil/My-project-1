using System;
using System.Collections.Generic;

namespace LingoRun
{
    /// <summary>Snapshot handed to achievement conditions after each run.</summary>
    public class AchievementContext
    {
        public RunResults run;
        public int totalRuns;
        public int masteredCount;
        public int lifetimeBestStreak;
        public int totalCorrect;
        public bool a2Unlocked;
        public int missedPoolCount;
        public int highScore;
    }

    public class AchievementDef
    {
        public string id;
        public string title;
        public string desc;
        public Func<AchievementContext, bool> IsMet;
    }

    /// <summary>
    /// Static achievement catalogue. Progress-style achievements ("how far have
    /// I gotten") mixed with skill achievements. Evaluated by ProgressManager
    /// after every run; unlocked ids persist in the per-profile save.
    /// </summary>
    public static class Achievements
    {
        public static readonly List<AchievementDef> All = new List<AchievementDef>
        {
            new AchievementDef { id = "first_run", title = "First Steps",
                desc = "Finish your first run.",
                IsMet = c => c.totalRuns >= 1 },

            new AchievementDef { id = "runs_10", title = "Regular Runner",
                desc = "Finish 10 runs.",
                IsMet = c => c.totalRuns >= 10 },

            new AchievementDef { id = "runs_50", title = "Marathoner",
                desc = "Finish 50 runs.",
                IsMet = c => c.totalRuns >= 50 },

            new AchievementDef { id = "catch_100", title = "Word Collector",
                desc = "Catch 100 correct words in total.",
                IsMet = c => c.totalCorrect >= 100 },

            new AchievementDef { id = "catch_500", title = "Word Hoarder",
                desc = "Catch 500 correct words in total.",
                IsMet = c => c.totalCorrect >= 500 },

            new AchievementDef { id = "master_10", title = "Getting Somewhere",
                desc = "Master 10 words (3x correct each).",
                IsMet = c => c.masteredCount >= 10 },

            new AchievementDef { id = "master_50", title = "Vocabulary Builder",
                desc = "Master 50 words.",
                IsMet = c => c.masteredCount >= 50 },

            new AchievementDef { id = "master_150", title = "Halfway to Fluent-ish",
                desc = "Master 150 words.",
                IsMet = c => c.masteredCount >= 150 },

            new AchievementDef { id = "a2_unlock", title = "Level Up",
                desc = "Master 300 A1 words and unlock A2.",
                IsMet = c => c.a2Unlocked },

            new AchievementDef { id = "streak_10", title = "On Fire",
                desc = "Hit a 10-streak in a single run.",
                IsMet = c => c.run.bestStreak >= 10 },

            new AchievementDef { id = "streak_25", title = "Unstoppable",
                desc = "Hit a 25-streak in a single run.",
                IsMet = c => c.run.bestStreak >= 25 },

            new AchievementDef { id = "score_300", title = "Point Machine",
                desc = "Score 300 in a single run.",
                IsMet = c => c.run.score >= 300 },

            new AchievementDef { id = "score_1000", title = "High Roller",
                desc = "Score 1000 in a single run.",
                IsMet = c => c.run.score >= 1000 },

            new AchievementDef { id = "perfect_run", title = "Flawless",
                desc = "100% accuracy in a run of 10+ gates.",
                IsMet = c => c.run.totalChallenges >= 10 && c.run.accuracy >= 100 },

            new AchievementDef { id = "review_clear", title = "Clean Slate",
                desc = "Empty your review pool with a review run.",
                IsMet = c => c.run.reviewMode && c.missedPoolCount == 0 && c.run.wordsLearned > 0 },
        };

        public static AchievementDef Find(string id)
        {
            foreach (var a in All) if (a.id == id) return a;
            return null;
        }
    }
}
