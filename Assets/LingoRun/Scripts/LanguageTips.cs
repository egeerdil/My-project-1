using System.Collections.Generic;
using UnityEngine;

namespace LingoRun
{
    /// <summary>
    /// Bite-size facts about the LEARNING language, surfaced on Home (the
    /// TIP strip next to REVIEW). One tip per day by default (deterministic
    /// from the date), and "next" cycles further within the session. Tips
    /// are written in English — same convention as the vocab hints.
    /// </summary>
    public static class LanguageTips
    {
        static readonly Dictionary<string, string[]> Tips = new Dictionary<string, string[]>
        {
            { "es", new[] {
                "Nouns ending in -o are usually masculine (el gato), -a feminine (la casa).",
                "SER is for what things ARE, ESTAR for how they are right now.",
                "Questions open with an upside-down mark: ¿Cómo estás?",
                "Adjectives usually come AFTER the noun: el coche rojo.",
                "TÚ is for friends, USTED shows respect — the verb changes too.",
                "Double negatives are correct: No veo nada = I see nothing.",
                "An accent can change the meaning: sí (yes) vs si (if).",
                "Plural: add -s after a vowel, -es after a consonant." } },
            { "fr", new[] {
                "Every noun has a gender: le livre (m), la table (f) — learn them together.",
                "Liaison links words: 'les amis' sounds like le-za-mi.",
                "Negation wraps the verb: je NE sais PAS.",
                "TU is informal, VOUS is polite or plural.",
                "Adjectives agree in gender and number: petite maison.",
                "The final consonant is often silent: petit sounds like 'peti'.",
                "é, è and ê all sound different — accents matter.",
                "There's no 'it' — everything is il or elle." } },
            { "it", new[] {
                "Double consonants change words: pala (shovel) vs palla (ball).",
                "CH sounds like K: chiesa = 'kyeza'.",
                "Nouns in -o are usually masculine, -a feminine; plurals: -i and -e.",
                "You can drop the pronoun: 'Parlo italiano' — the verb says who.",
                "CIAO works for both hello and goodbye — but only informally.",
                "The article changes with the sound: il libro, lo studente, l'amico.",
                "Stress usually falls on the second-to-last syllable.",
                "Question = same sentence, rising voice: Parli inglese?" } },
            { "de", new[] {
                "ALL nouns are capitalized: das Haus, der Hund.",
                "Three genders — der, die, das — must be memorized per word.",
                "The verb is glued to SECOND position in a statement.",
                "Long words are just glued nouns: Hand + Schuh = Handschuh (glove).",
                "SIE (formal) vs DU (informal) — Germans notice the difference.",
                "Umlauts change meaning: schon (already) vs schön (beautiful).",
                "Separable verbs split: aufstehen → Ich stehe auf.",
                "ß sounds like a sharp double-s: Straße." } },
            { "pt", new[] {
                "The nasal -ão is everywhere: pão, não, coração.",
                "In Brazil, VOCÊ is the everyday 'you'.",
                "Ç sounds like S: coração.",
                "-inho makes things small and cute: café → cafezinho.",
                "SER for permanent things, ESTAR for temporary states.",
                "R at the start sounds like English H in Brazil: Rio = 'Hio'.",
                "Nouns in -o are usually masculine, -a feminine.",
                "Days of the week use numbers: segunda-feira = Monday." } },
            { "tr", new[] {
                "Vowel harmony: suffix vowels match the word — evler, but kitaplar.",
                "Turkish stacks suffixes: ev → evimde = 'in my house'.",
                "No gender at all — o means he, she AND it.",
                "The verb goes to the END of the sentence.",
                "Dotless ı and dotted i are different letters and sounds.",
                "Questions use the particle mi: Geliyor musun?",
                "There is no word for 'the'.",
                "C sounds like English J: cam = 'jam' (glass)." } },
            { "ru", new[] {
                "Cyrillic has 33 letters — several look Latin but sound different.",
                "There are no articles: no 'a', no 'the'.",
                "Six cases change noun endings — learn them gradually.",
                "Present tense has no 'to be': Я студент = I (am a) student.",
                "Stress is unpredictable and can change meaning: замо́к vs за́мок.",
                "Ы is a sound English simply doesn't have — practice it!",
                "Verbs come in pairs: one for process, one for result.",
                "The soft sign Ь changes the consonant before it." } },
            { "ja", new[] {
                "Japanese mixes three scripts: hiragana, katakana and kanji.",
                "The verb always comes at the END of the sentence.",
                "Particles mark roles: は (topic), を (object), に (direction).",
                "There are no plurals — 猫 is cat or cats.",
                "Politeness is grammar: -ます forms are the safe default.",
                "Long vowels change words: おばさん (aunt) vs おばあさん (grandma).",
                "Counting uses counters: 人 for people, 匹 for animals.",
                "The Japanese R is between English R and L." } },
            { "ko", new[] {
                "Hangul groups letters into syllable blocks: ㅎ+ㅏ+ㄴ = 한.",
                "The verb always comes at the END of the sentence.",
                "Particles mark roles: 은/는 (topic), 이/가 (subject).",
                "No gender, no articles, usually no plurals.",
                "Honorifics are built into verbs — politeness is grammar.",
                "Final consonants are 'held' — 밥 ends closed, not 'babuh'.",
                "Two number systems: native Korean and Sino-Korean.",
                "Double consonants (ㄲ ㄸ ㅃ) are tense, not just long." } },
            { "zh", new[] {
                "Four tones change meaning: mā (mother) vs mǎ (horse).",
                "Verbs never conjugate: 我吃, 你吃, 他吃 — always 吃.",
                "Counting needs measure words: 一本书 (one [volume] book).",
                "Word order is like English: Subject-Verb-Object.",
                "There are no plurals and no articles.",
                "了 marks a completed action — not exactly past tense.",
                "Pinyin is a learning aid — characters are the real writing.",
                "是 links nouns only — never adjectives: 她高 (she tall)." } },
        };

        static int _sessionOffset;

        /// <summary>Every tip for the language (the Tips page shows them all).</summary>
        public static string[] AllFor(string langCode)
        {
            string[] pool;
            return Tips.TryGetValue(langCode, out pool) ? pool : new string[0];
        }

        /// <summary>Today's tip for the given learning language —
        /// deterministic per day, advanced by NextTip() within a session.</summary>
        public static string TipFor(string langCode)
        {
            string[] pool;
            if (!Tips.TryGetValue(langCode, out pool) || pool.Length == 0)
                return "";
            int day = (int)(System.DateTime.UtcNow - new System.DateTime(2026, 1, 1)).TotalDays;
            return pool[Mathf.Abs(day + _sessionOffset) % pool.Length];
        }

        public static void NextTip() { _sessionOffset++; }
    }
}
