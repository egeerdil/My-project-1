using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LingoRun
{
    /// <summary>
    /// Tiny juice layer: screens fade/scale in instead of snapping, buttons
    /// squash on press. Zero dependencies — a hidden runner MonoBehaviour
    /// hosts the coroutines so plain screen classes can use it too.
    /// </summary>
    public static class UIFX
    {
        class Runner : MonoBehaviour { }
        static Runner _runner;

        static Runner R()
        {
            if (_runner == null)
            {
                var go = new GameObject("UIFX");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            return _runner;
        }

        /// <summary>Drop-in replacement for root.gameObject.SetActive:
        /// showing plays a quick fade + scale pop.</summary>
        public static void SetVisible(RectTransform root, bool visible)
        {
            if (root == null) return;
            if (!visible) { root.gameObject.SetActive(false); return; }
            root.gameObject.SetActive(true);
            var cg = root.GetComponent<CanvasGroup>();
            if (cg == null) cg = root.gameObject.AddComponent<CanvasGroup>();
            R().StartCoroutine(PopIn(root, cg));
        }

        static IEnumerator PopIn(RectTransform rt, CanvasGroup cg)
        {
            const float d = 0.16f;
            float t = 0f;
            while (t < d)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / d);
                cg.alpha = k;
                rt.localScale = Vector3.one * (0.965f + 0.035f * k);
                yield return null;
            }
            if (rt != null)
            {
                cg.alpha = 1f;
                rt.localScale = Vector3.one;
            }
        }
    }

    /// <summary>Press-squash for every button (attached by
    /// UIFactory.CreateButton): scale dips on touch, springs back on
    /// release/cancel. Captures the current scale at press time so buttons
    /// whose scale is animated elsewhere aren't broken.</summary>
    public class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        Vector3 _restScale = Vector3.one;
        bool _down;

        public void OnPointerDown(PointerEventData e)
        {
            _restScale = transform.localScale;
            transform.localScale = _restScale * 0.93f;
            _down = true;
        }

        public void OnPointerUp(PointerEventData e) { Release(); }
        public void OnPointerExit(PointerEventData e) { Release(); }

        void Release()
        {
            if (!_down) return;
            _down = false;
            transform.localScale = _restScale;
        }
    }
}
