# LingoRun — Game Plan

## Content snapshot (Spanish)

| Data | Count |
|---|---|
| A1 words | 296 |
| A2 words | 711 |
| Sentences (A1 + A2) | 216 |

Progression math: mastery = 3 correct catches per word → full A1 ≈ 890 catches
≈ **30–50 runs**. A2 unlocks at 250 mastered A1 words (~85% of A1).

## Shipped (phases 1–3)

- **Core loop**: 3D gate-runner, Words mode + Sentences mode, three paces,
  distance-fair spawning, wrong-answer teach-back pause.
- **Learning system**: per-word mastery (3x correct), review pool with per-mode
  separation, spaced-repetition weighting (missed words spawn more), A2 unlock.
- **Retention**: daily streak + daily goal (30 catches/day) on the home screen.
- **Progress visibility**: My Words screen (per-word state: in review / n of 3 /
  mastered), Achievements screen with lifetime stats, 15 achievements.
- **Accounts**: local profiles (up to 6 per device), login screen, per-profile saves.
- **Cosmetics**: 6 ball skins, unlocked at 0/3/6/9/12/15 achievements.
- **Look**: modern rounded UI with shadows, pastel palette, fog, confetti,
  lane-tinted gates + always-readable HUD word chips.

## Next up (in priority order)

1. **More languages** — French / Italian / German. The JSON format already
   supports it; it's a data-conversion pass (vocab_fr.json, sentences_fr.json, …)
   plus a language picker and per-language save keys.
2. **Proper font** — import a bold rounded .ttf (e.g., Baloo, Nunito, Fredoka)
   and swap it into UIFactory.DefaultFont. Biggest remaining visual upgrade.
3. **Listening mode** — needs TTS. On iOS/Android use a native TTS plugin
   (AVSpeechSynthesizer / android.speech.tts) behind a small C# interface;
   every 4th gate becomes "listen and catch".
4. **App-store readiness** — app icon, splash, portrait-lock in Player Settings,
   Always Included Shaders (URP Unlit + URP Particles Unlit), privacy label
   ("no data collected").
5. **More juice** — camera shake on wrong, gate-break animation, milestone
   celebrations ("100 words mastered!"), background themes per topic.
6. **Monetization (decide later)** — options: cosmetic skins (extend the skin
   system), remove-ads IAP with interstitial-free design, or fully premium.
   Recommendation: keep free with cosmetics until retention data exists.

## Known limitations

- Editor scene looks empty when not playing (everything is runtime-built —
  cosmetic only).
- Default Unity font until a .ttf is imported.
- One language (Spanish) for now.
