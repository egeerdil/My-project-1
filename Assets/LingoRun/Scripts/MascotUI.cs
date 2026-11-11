using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Lino, the LingoRun bunny — the app's mascot. Built entirely from UI
    /// primitives (circles/panels/polygons, no art assets): round white head,
    /// long pink-lined ears, blinking eyes, cheeks and a tiny nose. He slides
    /// up from the bottom-left of the Home screen with a speech bubble,
    /// says something CONTEXTUAL (out of hearts → points at free Review,
    /// review pool full → asks you to rescue words, goal done → celebrates,
    /// otherwise a rotating tip), idles with a gentle bob + ear wiggles +
    /// blinks, and hops when tapped (tap = next line). Auto-hides after a
    /// few seconds so he never blocks play.
    /// </summary>
    public class MascotUI
    {
        readonly RectTransform _container;
        readonly MascotAnimator _anim;
        readonly Text _bubbleText;

        static string[] Tips()
        {
            return new[]
            {
                Loc.T("mascot.tip1"), Loc.T("mascot.tip2"),
                Loc.T("mascot.tip3"), Loc.T("mascot.tip4"),
            };
        }

        public MascotUI(Transform parent)
        {
            // Container pinned bottom-left, above the tab bar; the animator
            // slides it in/out from below the screen edge.
            var go = new GameObject("Mascot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _container = go.GetComponent<RectTransform>();
            _container.anchorMin = _container.anchorMax = new Vector2(0f, 0f);
            _container.pivot = new Vector2(0f, 0f);
            _container.sizeDelta = new Vector2(392f, 170f);
            _container.anchoredPosition = new Vector2(16f, -200f);

            // Tap target covers ONLY the bunny itself (tap = hop + next
            // line). A full-container hit area used to swallow taps meant
            // for the REVIEW/TIP strips underneath — never block the UI.
            var tapGo = new GameObject("Tap", typeof(RectTransform), typeof(Image), typeof(Button));
            tapGo.transform.SetParent(_container, false);
            var tapImg = tapGo.GetComponent<Image>();
            tapImg.color = new Color(1f, 1f, 1f, 0f);
            tapImg.raycastTarget = true;
            var tapRt = tapGo.GetComponent<RectTransform>();
            tapRt.anchorMin = tapRt.anchorMax = new Vector2(0f, 0f);
            tapRt.pivot = new Vector2(0.5f, 0.5f);
            tapRt.sizeDelta = new Vector2(116f, 150f);
            tapRt.anchoredPosition = new Vector2(62f, 64f);

            // ---------- speech bubble ----------
            var bubble = UIFactory.CreatePanel(_container, "Bubble", Color.white, 18f);
            UIFactory.AddSoftShadow(bubble.gameObject, 4f, 0.18f);
            bubble.raycastTarget = false;
            var brt = bubble.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f);
            brt.pivot = new Vector2(0f, 0f);
            brt.sizeDelta = new Vector2(258f, 96f);
            brt.anchoredPosition = new Vector2(126f, 56f);

            var tail = UIFactory.CreatePolygon(_container, "BubbleTail", Color.white, new[] {
                new Vector2(10f, 6f), new Vector2(-10f, 2f), new Vector2(8f, -7f) });
            tail.raycastTarget = false;
            var trt = tail.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0f);
            trt.sizeDelta = new Vector2(24f, 18f);
            trt.anchoredPosition = new Vector2(122f, 86f);

            _bubbleText = UIFactory.CreateText(bubble.transform, "Text", "", 14, Theme.Ink,
                TextAnchor.MiddleLeft);
            _bubbleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bubbleText.raycastTarget = false;
            var btxt = _bubbleText.rectTransform;
            btxt.offsetMin = new Vector2(14f, 8f);
            btxt.offsetMax = new Vector2(-12f, -8f);

            // ---------- the bunny ----------
            var refs = BuildBunny(_container, new Vector2(62f, 64f), 1f);
            refs.root.anchorMin = refs.root.anchorMax = new Vector2(0f, 0f);
            refs.root.anchoredPosition = new Vector2(62f, 64f);

            // ---------- animation driver ----------
            _anim = go.AddComponent<MascotAnimator>();
            _anim.container = _container;
            _anim.bunny = refs.root;
            _anim.earLeft = refs.earLeft;
            _anim.earRight = refs.earRight;
            _anim.eyeLeft = refs.eyeLeft;
            _anim.eyeRight = refs.eyeRight;
            _anim.bubbleText = _bubbleText;

            tapGo.GetComponent<Button>().onClick.AddListener(delegate
            {
                ProceduralAudio.PlayClick();
                _anim.Hop();
                var tips = Tips();
                Say(tips[Random.Range(0, tips.Length)]);
            });

            // Close button on the bubble's corner — Lino excuses himself.
            var close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            close.transform.SetParent(_container, false);
            var closeImg = close.GetComponent<Image>();
            closeImg.sprite = UIFactory.CircleSprite;
            closeImg.type = Image.Type.Simple;
            closeImg.color = Theme.Hex("2E2A3B", 0.75f);
            closeImg.raycastTarget = true;
            var closeRt = close.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0.5f);
            closeRt.sizeDelta = new Vector2(26f, 26f);
            closeRt.anchoredPosition = new Vector2(384f, 152f);
            var closeText = UIFactory.CreateText(close.transform, "X", "X", 12, Color.white);
            closeText.font = UIFactory.DisplayFont;
            closeText.raycastTarget = false;
            close.GetComponent<Button>().onClick.AddListener(delegate
            {
                ProceduralAudio.PlayClick();
                _anim.Dismiss();
            });

            _container.gameObject.SetActive(false);
        }

        /// <summary>Handles to a built bunny's animatable parts.</summary>
        public class BunnyRefs
        {
            public RectTransform root, earLeft, earRight, eyeLeft, eyeRight;
        }

        /// <summary>Build Lino's full body from UI primitives under any
        /// parent — used by the mascot overlay AND as static decoration
        /// (e.g. the login screen). Pair with BunnyIdle for life.</summary>
        public static BunnyRefs BuildBunny(Transform parent, Vector2 pos, float scale)
        {
            var bunnyGo = new GameObject("Bunny", typeof(RectTransform));
            bunnyGo.transform.SetParent(parent, false);
            var bunnyRt = bunnyGo.GetComponent<RectTransform>();
            bunnyRt.anchorMin = bunnyRt.anchorMax = new Vector2(0.5f, 0.5f);
            bunnyRt.pivot = new Vector2(0.5f, 0.5f);
            bunnyRt.sizeDelta = new Vector2(110f, 150f);
            bunnyRt.anchoredPosition = pos;
            bunnyRt.localScale = Vector3.one * scale;
            var bt = bunnyGo.transform;

            Color fur = Color.white;
            Color pink = Theme.Hex("F4B7CC");
            Color ink = Theme.Hex("3A3350");

            var earL = EarPiece(bt, "EarL", fur, new Vector2(-17f, 58f), 13f);
            var earR = EarPiece(bt, "EarR", fur, new Vector2(17f, 58f), -13f);
            EarInner(earL, pink);
            EarInner(earR, pink);

            var body = UIFactory.CreateCircle(bt, "Body", fur);
            SetPart(body.rectTransform, new Vector2(0f, -34f), new Vector2(72f, 62f));

            var head = UIFactory.CreateCircle(bt, "Head", fur);
            SetPart(head.rectTransform, new Vector2(0f, 12f), new Vector2(78f, 74f));

            var cheekL = UIFactory.CreateCircle(bt, "CheekL", new Color(pink.r, pink.g, pink.b, 0.65f));
            SetPart(cheekL.rectTransform, new Vector2(-24f, 2f), new Vector2(14f, 11f));
            var cheekR = UIFactory.CreateCircle(bt, "CheekR", new Color(pink.r, pink.g, pink.b, 0.65f));
            SetPart(cheekR.rectTransform, new Vector2(24f, 2f), new Vector2(14f, 11f));

            var eyeL = UIFactory.CreateCircle(bt, "EyeL", ink);
            SetPart(eyeL.rectTransform, new Vector2(-13f, 16f), new Vector2(10f, 12f));
            var eyeR = UIFactory.CreateCircle(bt, "EyeR", ink);
            SetPart(eyeR.rectTransform, new Vector2(13f, 16f), new Vector2(10f, 12f));
            var glintL = UIFactory.CreateCircle(eyeL.transform, "Glint", Color.white);
            SetPart(glintL.rectTransform, new Vector2(1.6f, 2.4f), new Vector2(3.6f, 3.6f));
            var glintR = UIFactory.CreateCircle(eyeR.transform, "Glint", Color.white);
            SetPart(glintR.rectTransform, new Vector2(1.6f, 2.4f), new Vector2(3.6f, 3.6f));

            var nose = UIFactory.CreatePolygon(bt, "Nose", Theme.Hex("E88AA9"), new[] {
                new Vector2(-3.6f, 2f), new Vector2(3.6f, 2f), new Vector2(0f, -2.6f) });
            nose.raycastTarget = false;
            SetPart(nose.rectTransform, new Vector2(0f, 7f), new Vector2(10f, 8f));
            var smileL = UIFactory.CreatePanel(bt, "SmileL", ink, 1.4f);
            SetPart(smileL.rectTransform, new Vector2(-3.4f, 0.5f), new Vector2(7.5f, 2.6f));
            smileL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            var smileR = UIFactory.CreatePanel(bt, "SmileR", ink, 1.4f);
            SetPart(smileR.rectTransform, new Vector2(3.4f, 0.5f), new Vector2(7.5f, 2.6f));
            smileR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);

            var tag = UIFactory.CreatePanel(bt, "Tag", Theme.Hex("2E2A3B", 0.85f), 8f);
            SetPart(tag.rectTransform, new Vector2(0f, -62f), new Vector2(52f, 20f));
            var tagText = UIFactory.CreateText(tag.transform, "Name", "LINO", 11, Color.white);
            tagText.font = UIFactory.DisplayFont;
            tagText.raycastTarget = false;

            return new BunnyRefs
            {
                root = bunnyRt,
                earLeft = earL,
                earRight = earR,
                eyeLeft = eyeL.rectTransform,
                eyeRight = eyeR.rectTransform,
            };
        }

        static RectTransform EarPiece(Transform bt, string name, Color fur, Vector2 pos, float tilt)
        {
            var ear = UIFactory.CreatePanel(bt, name, fur, 11f);
            ear.raycastTarget = false;
            var rt = ear.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f); // pivot at the base so wiggles hinge naturally
            rt.sizeDelta = new Vector2(24f, 62f);
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, tilt);
            return rt;
        }

        static void EarInner(RectTransform ear, Color pink)
        {
            var inner = UIFactory.CreatePanel(ear, "Inner", pink, 7f);
            inner.raycastTarget = false;
            var rt = inner.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(11f, 44f);
            rt.anchoredPosition = new Vector2(0f, 8f);
        }

        static void SetPart(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var g = rt.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = false;
        }

        /// <summary>Per-screen chatter (screens other than Home get a short
        /// flavor pool). Built on demand so lines follow the app language.</summary>
        static string[] ScreenLines(string screen)
        {
            switch (screen)
            {
                case "map": return new[] { Loc.T("mascot.map1"), Loc.T("mascot.map2") };
                case "shop": return new[] { Loc.T("mascot.shop1"), Loc.T("mascot.shop2") };
                case "dictionary": return new[] { Loc.T("mascot.dict1"), Loc.T("mascot.dict2") };
                case "awards": return new[] { Loc.T("mascot.awards1"), Loc.T("mascot.awards2") };
                case "games": return new[] { Loc.T("mascot.games1"), Loc.T("mascot.games2") };
                case "gameover": return new[] { Loc.T("mascot.go1"), Loc.T("mascot.go2") };
                default: return null;
            }
        }

        // Frequency gate: Lino is a treat, not a doorman. He always shows on
        // the session's first Home visit and for genuinely important moments
        // (no hearts / a big review backlog); otherwise only occasionally —
        // a dice roll, and never twice within the cooldown window.
        static bool _welcomedThisSession;
        static float _lastShownAt = -9999f;
        const float CooldownSeconds = 120f;
        const float FlavorChance = 0.3f;

        static bool ImportantMoment(string screen)
        {
            if (screen != "home") return false;
            return HeartsManager.Hearts <= 0 || ProgressManager.MissedCount >= 8;
        }

        /// <summary>Maybe slide Lino in with a line fitting the given screen
        /// ("home" uses full progress-aware logic; others use flavor pools).
        /// Skips silently when it's "not his moment" — see the gate above.</summary>
        public void Appear(string screen)
        {
            bool firstHome = screen == "home" && !_welcomedThisSession;
            if (!firstHome && !ImportantMoment(screen))
            {
                if (Time.unscaledTime - _lastShownAt < CooldownSeconds) return;
                if (Random.value > FlavorChance) return;
            }
            if (screen == "home") _welcomedThisSession = true;
            _lastShownAt = Time.unscaledTime;

            _container.gameObject.SetActive(true);
            var pool = screen == "home" ? null : ScreenLines(screen);
            Say(pool != null ? pool[Random.Range(0, pool.Length)] : PickContextualLine());
            _anim.SlideIn();
        }

        public void HideInstant() { _container.gameObject.SetActive(false); }

        /// <summary>Forced celebratory appearance (bypasses the frequency
        /// gate) — used for the once-a-day "daily goal done!" moment.</summary>
        public void CelebrateGoal()
        {
            _lastShownAt = Time.unscaledTime;
            _container.gameObject.SetActive(true);
            Say(Loc.T("mascot.goal"));
            _anim.SlideIn();
            _anim.Hop();
        }

        void Say(string line) { _anim.StartTyping(line); }

        static string PickContextualLine()
        {
            if (HeartsManager.Hearts <= 0)
                return Loc.T("mascot.hearts");
            int missed = ProgressManager.MissedCount;
            if (missed >= 5)
                return Loc.F("mascot.review", missed);
            if (ProgressManager.TodayCatches >= GameConfig.DailyGoal)
                return Loc.T("mascot.goal");
            int level = ProgressManager.CurrentLevel;
            int li = (level - 1) % GameConfig.LevelsPerUnit;
            if (li == GameConfig.LevelsPerUnit - 1)
                return Loc.T("mascot.chest");
            if (li == 0)
                return Loc.F("mascot.new_unit", Loc.Topic(GameConfig.UnitTopicForLevel(level)));
            int streak = ProgressManager.DayStreakDisplay;
            if (streak >= 3)
                return Loc.F("mascot.streak", streak);
            var tips = Tips();
            return tips[Random.Range(0, tips.Length)];
        }
    }

    /// <summary>Lightweight idle life for a STATIC bunny (login screen etc.):
    /// gentle bob, ear wiggles and blinks — no sliding, no bubble.</summary>
    public class BunnyIdle : MonoBehaviour
    {
        public MascotUI.BunnyRefs refs;
        Vector2 _basePos;
        float _t;
        float _blinkTimer = 2f;
        float _blinkPhase;

        void Start() { if (refs != null) _basePos = refs.root.anchoredPosition; }

        void Update()
        {
            if (refs == null) return;
            float dt = Time.deltaTime;
            _t += dt;
            refs.root.anchoredPosition = _basePos + new Vector2(0f, Mathf.Sin(_t * 2.1f) * 2.4f);

            float wig = Mathf.Sin(_t * 1.7f) * 4f;
            refs.earLeft.localRotation = Quaternion.Euler(0f, 0f, 13f + wig);
            refs.earRight.localRotation = Quaternion.Euler(0f, 0f, -13f - wig);

            _blinkTimer -= dt;
            if (_blinkTimer <= 0f) { _blinkPhase = 0.18f; _blinkTimer = Random.Range(2.4f, 4.6f); }
            float s = 1f;
            if (_blinkPhase > 0f)
            {
                _blinkPhase -= dt;
                s = Mathf.Max(0.12f, Mathf.Abs(Mathf.Sin(_blinkPhase / 0.18f * Mathf.PI)));
            }
            refs.eyeLeft.localScale = new Vector3(1f, s, 1f);
            refs.eyeRight.localScale = new Vector3(1f, s, 1f);
        }
    }

    /// <summary>Drives Lino's life: slide in/out, idle bob, ear wiggle,
    /// blinking, tap-hop, typewriter text, and the auto-hide timer.</summary>
    public class MascotAnimator : MonoBehaviour
    {
        public RectTransform container;
        public RectTransform bunny;
        public RectTransform earLeft, earRight;
        public RectTransform eyeLeft, eyeRight;
        public Text bubbleText;

        const float ShownY = 96f;   // clears the floating tab bar
        const float HiddenY = -200f;
        const float AutoHideSeconds = 8f;

        float _y;
        bool _showing;
        float _hideTimer;
        float _idleT;
        float _blinkTimer = 2f;
        float _blinkPhase;
        float _hop;
        string _fullLine = "";
        float _typeT;

        public void SlideIn()
        {
            _showing = true;
            _hideTimer = AutoHideSeconds;
            _hop = 0.6f; // arrive with a little bounce
        }

        public void Hop()
        {
            _hop = 1f;
            _hideTimer = AutoHideSeconds; // interacting keeps him around
        }

        /// <summary>Close button: slide out right away.</summary>
        public void Dismiss() { _showing = false; }

        public void StartTyping(string line)
        {
            _fullLine = line ?? "";
            _typeT = 0f;
        }

        void OnEnable() { _y = HiddenY; Apply(); }

        void Update()
        {
            float dt = Time.deltaTime;

            // Slide toward shown/hidden position.
            float target = _showing ? ShownY : HiddenY;
            _y = Mathf.Lerp(_y, target, 1f - Mathf.Exp(-8f * dt));
            if (!_showing && _y < HiddenY + 4f && container.gameObject.activeSelf)
            {
                container.gameObject.SetActive(false);
                return;
            }

            if (_showing)
            {
                _hideTimer -= dt;
                if (_hideTimer <= 0f) _showing = false;
            }

            // Typewriter bubble text.
            if (bubbleText != null && _fullLine.Length > 0)
            {
                _typeT += dt * 46f; // chars per second
                int chars = Mathf.Clamp(Mathf.FloorToInt(_typeT), 0, _fullLine.Length);
                bubbleText.text = _fullLine.Substring(0, chars);
            }

            // Idle bob + hop.
            _idleT += dt;
            _hop = Mathf.Max(0f, _hop - dt * 2.6f);
            float bob = Mathf.Sin(_idleT * 2.1f) * 2.4f;
            float hopY = Mathf.Sin(Mathf.Clamp01(_hop) * Mathf.PI) * 16f;
            if (bunny != null)
                bunny.anchoredPosition = new Vector2(62f, 64f + bob + hopY);

            // Ear wiggle (hinged at the base).
            float wig = Mathf.Sin(_idleT * 1.7f) * 4f;
            if (earLeft != null) earLeft.localRotation = Quaternion.Euler(0f, 0f, 13f + wig);
            if (earRight != null) earRight.localRotation = Quaternion.Euler(0f, 0f, -13f - wig);

            // Blink: quick squash every few seconds.
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f) { _blinkPhase = 0.18f; _blinkTimer = Random.Range(2.4f, 4.6f); }
            if (_blinkPhase > 0f)
            {
                _blinkPhase -= dt;
                float s = Mathf.Abs(Mathf.Sin(_blinkPhase / 0.18f * Mathf.PI));
                SetEyeScale(Mathf.Max(0.12f, s));
            }
            else SetEyeScale(1f);

            Apply();
        }

        void SetEyeScale(float y)
        {
            var s = new Vector3(1f, y, 1f);
            if (eyeLeft != null) eyeLeft.localScale = s;
            if (eyeRight != null) eyeRight.localScale = s;
        }

        void Apply()
        {
            if (container != null)
                container.anchoredPosition = new Vector2(16f, _y);
        }
    }
}
