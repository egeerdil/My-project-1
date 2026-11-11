# LingoRun — Unity port (core: 3D Running mode)

Ported from `lingorun_v48.jsx` (React) to Unity C#, with Running mode rebuilt as a
**3D hypercasual gate-runner** (straight track to the horizon, purple rails, cyan
ball with a trail, translucent word gates — steer through the one matching the
English prompt). Everything is built from code at runtime — no prefabs, no scene
wiring, no external assets.

## Setup (2 minutes)

1. Open the project in Unity (made for **Unity 6 / 6000.x**, new Input System, uGUI).
2. Create a new **empty scene** (File > New Scene, "Basic" is fine — you can delete
   the default camera or leave it; the UI is Screen Space Overlay either way).
3. Create an empty GameObject (right-click Hierarchy > Create Empty).
4. Add the **LingoRunBootstrap** component to it (Add Component > LingoRun > Lingo Run Bootstrap,
   or search "LingoRunBootstrap").
5. Set the Game view aspect to **9:16 Portrait** (dropdown at the top of the Game view).
6. Press **Play**.

## What's included

| Piece | File | Notes |
|---|---|---|
| Entry point / screen flow | `Scripts/LingoRunBootstrap.cs` | Builds EventSystem, Canvas (440x782 ref), camera, meadow menu background, all screens |
| 3D Running mode | `Scripts/RunningGame3D.cs` | Perspective track, continuous left/right steering (hold + slide anywhere, or A/D / arrows for lanes), translucent word gates, scoring, streaks, 3 lives, wrong-answer pause with correct-answer reveal |
| Home screen | `Scripts/HomeScreenUI.cs` | Pace selector (Calm/Normal/Fast), Start Run, Review (count), A1 progress bar |
| Game over screen | `Scripts/GameOverUI.cs` | Score, accuracy, streak, newly mastered, A2 unlock banner, Run Again / Home / Review |
| Progression & saving | `Scripts/ProgressManager.cs` | PlayerPrefs JSON: high score, review pool, 3x-correct mastery, A2 unlock at 300 A1 words, spaced-repetition weights |
| Vocab data | `Resources/vocab_es.json` + `Scripts/VocabDatabase.cs` | 1007 Spanish words, 300 A1 keys, weighted pick + distractors |
| Sound effects | `Scripts/ProceduralAudio.cs` | Correct chime / wrong buzz / UI click generated in code (no audio files) |
| Config & theme | `Scripts/GameConfig.cs` | Difficulty pacing + meadow color palette |
| UI helpers | `Scripts/UIFactory.cs` | Code-built uGUI, generated circle/gradient sprites |

## Deliberately not in this pass (per scope decision)

- Jumping and Bubble modes
- Listening challenges (Unity has no built-in TTS)
- FR / IT / DE vocab (the JSON format supports them — add `vocab_fr.json` etc. and
  extend `VocabDatabase.Load`)
- Topic picker, dictionary, tutorial screens
- Bunny sprites (player is a placeholder circle — swap in a sprite on the
  `Player` Image in `RunningGame.EnsureBuilt` if you export the PNGs from the jsx)

## Gameplay parity notes

- Scoring matches the web version: +10 per correct gate, +25 every 5-streak,
  3 lives, 1.6s wrong pause with correct-answer reveal, mastery = 3 correct
  answers across runs, A2 unlocks at 300 mastered A1 words. Speed/gap pacing
  reuses the v48 difficulty tables (px values scaled to world units).
- Spawning is distance-based (constant gap regardless of speed), like v48.
- 3D world: materials use the URP Unlit shader (flat hypercasual look). For a
  device build, add "Universal Render Pipeline/Unlit",
  "Universal Render Pipeline/Particles/Unlit" and "Skybox/Procedural" to
  Project Settings > Graphics > Always Included Shaders so they aren't stripped.
- Controls in-game: hold and slide anywhere to steer the ball continuously;
  A/D or left/right arrows snap between the three lanes.
- Review mode pulls only from your missed-word pool; getting a word right in
  review removes it from the pool. Missed words also spawn 3x more in normal runs.
- Save data lives in PlayerPrefs under `LingoRun.Save.es.v1`.
