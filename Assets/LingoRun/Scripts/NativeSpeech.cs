using System;
using System.Collections;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace LingoRun
{
    /// <summary>
    /// C# side of the native speech plugin (Plugins/iOS/LingoRunSpeech.mm).
    /// One hidden GameObject named "LingoRunSpeech" receives the plugin's
    /// UnitySendMessage callbacks and forwards them to whoever is listening.
    ///
    ///   NativeSpeech.Speak("beber")               — TTS in the active language
    ///   NativeSpeech.RequestAuth(ok => …)         — mic + recognition permission
    ///   NativeSpeech.StartListening(p, f, e)      — live transcription
    ///   NativeSpeech.StopListening()
    ///
    /// In the editor (and non-iOS platforms) everything degrades gracefully:
    /// Speak logs, auth always succeeds, and listening returns
    /// EditorSimulatedResult after a short delay so the pronunciation game
    /// stays fully testable without a device.
    /// </summary>
    public class NativeSpeech : MonoBehaviour
    {
        static NativeSpeech _instance;

        Action<bool> _authCb;
        Action<string> _partialCb;
        Action<string> _finalCb;
        Action<string> _errorCb;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _lingorunSpeak(string text, string locale, float rate);
        [DllImport("__Internal")] static extern void _lingorunStopSpeaking();
        [DllImport("__Internal")] static extern void _lingorunRequestSpeechAuth();
        [DllImport("__Internal")] static extern void _lingorunStartListening(string locale);
        [DllImport("__Internal")] static extern void _lingorunStopListening();
#endif

        /// <summary>What the editor fallback "hears" — the pronunciation game
        /// sets this to the current target word so editor runs always pass.</summary>
        public static string EditorSimulatedResult = "";

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("LingoRunSpeech");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NativeSpeech>();
        }

        /// <summary>BCP-47 voice/recognizer locale for the active learning language.</summary>
        public static string CurrentLocale()
        {
            switch (LanguageManager.Current)
            {
                case "fr": return "fr-FR";
                case "it": return "it-IT";
                case "de": return "de-DE";
                case "pt": return "pt-BR";
                case "tr": return "tr-TR";
                case "ru": return "ru-RU";
                case "ja": return "ja-JP";
                case "ko": return "ko-KR";
                case "zh": return "zh-CN";
                default: return "es-ES";
            }
        }

        // ---------------------------------------------------------------- TTS

        public static void Speak(string text) { Speak(text, 0.42f); }

        public static void Speak(string text, float rate)
        {
            if (string.IsNullOrEmpty(text) || ProceduralAudio.Muted) return;
            Ensure();
#if UNITY_IOS && !UNITY_EDITOR
            _lingorunSpeak(text, CurrentLocale(), rate);
#else
            Debug.Log("[LingoRun] TTS (" + CurrentLocale() + "): " + text);
#endif
        }

        public static void StopSpeaking()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _lingorunStopSpeaking();
#endif
        }

        // ------------------------------------------------------- recognition

        public static void RequestAuth(Action<bool> callback)
        {
            Ensure();
            _instance._authCb = callback;
#if UNITY_IOS && !UNITY_EDITOR
            _lingorunRequestSpeechAuth();
#else
            _instance.StartCoroutine(_instance.EditorDelay(0.2f,
                delegate { _instance.OnSpeechAuth("1"); }));
#endif
        }

        public static void StartListening(Action<string> onPartial, Action<string> onFinal, Action<string> onError)
        {
            Ensure();
            _instance._partialCb = onPartial;
            _instance._finalCb = onFinal;
            _instance._errorCb = onError;
#if UNITY_IOS && !UNITY_EDITOR
            _lingorunStartListening(CurrentLocale());
#else
            _instance.StartCoroutine(_instance.EditorDelay(1.1f,
                delegate { _instance.OnSpeechFinal(EditorSimulatedResult); }));
#endif
        }

        public static void StopListening()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _lingorunStopListening();
#else
            if (_instance != null) _instance.StopAllCoroutines();
#endif
        }

        IEnumerator EditorDelay(float seconds, Action then)
        {
            yield return new WaitForSeconds(seconds);
            then();
        }

        // ---------------- UnitySendMessage receivers (called by the plugin) --

        void OnSpeechAuth(string ok)
        {
            var cb = _authCb; _authCb = null;
            if (cb != null) cb(ok == "1");
        }

        void OnSpeechPartial(string text)
        {
            if (_partialCb != null) _partialCb(text);
        }

        void OnSpeechFinal(string text)
        {
            if (_finalCb != null) _finalCb(text);
        }

        void OnSpeechError(string reason)
        {
            if (_errorCb != null) _errorCb(reason);
        }
    }
}
