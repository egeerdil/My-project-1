using UnityEngine;

namespace LingoRun
{
    /// <summary>
    /// Shrinks this RectTransform to the device's safe area (notch / Dynamic
    /// Island at the top, home indicator at the bottom). All screen content is
    /// parented under this; backgrounds stay outside it so they still bleed
    /// edge-to-edge.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect _applied = new Rect(-1f, -1f, -1f, -1f);

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            // Cheap check every frame — safe area can change (rotation, iPad
            // multitasking) and is 0-sized for one frame on some devices at boot.
            if (Screen.safeArea != _applied) Apply();
        }

        void Apply()
        {
            Rect sa = Screen.safeArea;
            if (sa.width <= 0f || sa.height <= 0f || Screen.width <= 0 || Screen.height <= 0) return;
            _applied = sa;

            Vector2 min = sa.position;
            Vector2 max = sa.position + sa.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
