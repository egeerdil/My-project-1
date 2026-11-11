using UnityEngine;

namespace LingoRun
{
    /// <summary>
    /// Procedurally generated sound effects, ported from the Web Audio code in
    /// lingorun_v48.jsx: correct = two-note sine chime (C5 -> E5), wrong = descending
    /// square buzz (220 -> 130 Hz), click = short triangle blip (1200 -> 800 Hz).
    /// No audio assets required.
    /// </summary>
    public class ProceduralAudio : MonoBehaviour
    {
        public static bool Muted;

        static ProceduralAudio _instance;
        AudioSource _source;
        AudioClip _correct, _wrong, _click;

        const int SampleRate = 44100;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("LingoRunAudio");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ProceduralAudio>();
        }

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _correct = BuildCorrectClip();
            _wrong = BuildWrongClip();
            _click = BuildClickClip();
        }

        public static void PlayCorrect() { Play(_instance != null ? _instance._correct : null); }
        public static void PlayWrong()   { Play(_instance != null ? _instance._wrong : null); }
        public static void PlayClick()   { Play(_instance != null ? _instance._click : null); }

        static void Play(AudioClip clip)
        {
            if (Muted || clip == null || _instance == null) return;
            _instance._source.PlayOneShot(clip);
        }

        // ---------- clip construction ----------

        enum Wave { Sine, Square, Triangle }

        static AudioClip BuildCorrectClip()
        {
            var data = NewBuffer(0.30f);
            // C5 then E5, slight overlap, 10ms attack + exponential decay
            AddTone(data, Wave.Sine, 523.25f, 523.25f, 0.00f, 0.10f, 0.18f);
            AddTone(data, Wave.Sine, 659.25f, 659.25f, 0.07f, 0.16f, 0.18f);
            return ToClip("sfx_correct", data);
        }

        static AudioClip BuildWrongClip()
        {
            var data = NewBuffer(0.26f);
            AddTone(data, Wave.Square, 220f, 130f, 0f, 0.22f, 0.10f);
            return ToClip("sfx_wrong", data);
        }

        static AudioClip BuildClickClip()
        {
            var data = NewBuffer(0.09f);
            AddTone(data, Wave.Triangle, 1200f, 800f, 0f, 0.06f, 0.07f);
            return ToClip("sfx_click", data);
        }

        static float[] NewBuffer(float seconds)
        {
            return new float[Mathf.CeilToInt(seconds * SampleRate)];
        }

        static void AddTone(float[] data, Wave wave, float freqStart, float freqEnd,
                            float start, float dur, float gain)
        {
            int i0 = Mathf.FloorToInt(start * SampleRate);
            int n = Mathf.FloorToInt(dur * SampleRate);
            float attack = 0.01f * SampleRate; // 10ms linear attack
            float phase = 0f;

            for (int i = 0; i < n && i0 + i < data.Length; i++)
            {
                float t01 = n > 1 ? (float)i / (n - 1) : 0f;
                // exponential-ish frequency ramp (matches exponentialRampToValueAtTime feel)
                float freq = freqStart * Mathf.Pow(freqEnd / Mathf.Max(1f, freqStart), t01);
                phase += 2f * Mathf.PI * freq / SampleRate;

                float sample;
                switch (wave)
                {
                    case Wave.Square:
                        sample = Mathf.Sin(phase) >= 0f ? 1f : -1f;
                        break;
                    case Wave.Triangle:
                        sample = 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phase));
                        break;
                    default:
                        sample = Mathf.Sin(phase);
                        break;
                }

                // envelope: linear attack, exponential decay to ~0.001 at the end
                float env;
                if (i < attack) env = i / attack;
                else
                {
                    float decayT = (i - attack) / Mathf.Max(1f, n - attack);
                    env = Mathf.Pow(0.001f, decayT);
                }

                data[i0 + i] += sample * gain * env;
            }
        }

        static AudioClip ToClip(string name, float[] data)
        {
            // soft clip so overlapping tones never pop
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Clamp(data[i], -0.95f, 0.95f);
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
