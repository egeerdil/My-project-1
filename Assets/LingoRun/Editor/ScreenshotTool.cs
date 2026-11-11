using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LingoRun.EditorTools
{
    /// <summary>
    /// App Store screenshot helper.
    ///   1) "LingoRun → Screenshots → Add iPad Size (2064x2752)" registers the
    ///      13" iPad portrait resolution in the Game view's size dropdown
    ///      (pick it there once).
    ///   2) In Play mode, navigate to the screen you want and hit
    ///      "LingoRun → Screenshots → Capture Game View  %#s" (Cmd+Shift+S).
    ///      The PNG captures at the Game view's exact resolution (overlay UI
    ///      included) into <project>/Screenshots/.
    /// </summary>
    public static class ScreenshotTool
    {
        const int W = 2064, H = 2752;

        [MenuItem("LingoRun/Screenshots/Add iPad Size (2064x2752)")]
        public static void AddIpadSize()
        {
            TryAddGameViewSize(W, H, "iPad 13\" AppStore");
            Debug.Log("ScreenshotTool: Game view boyut listesine 'iPad 13\" AppStore' (2064x2752) eklendi — Game sekmesindeki çözünürlük menüsünden seç.");
        }

        [MenuItem("LingoRun/Screenshots/Capture Game View %#s")]
        public static void Capture()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir,
                "lingorun_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("ScreenshotTool: kaydedildi → " + file
                + "  (çözünürlük = Game view çözünürlüğü; 2064x2752 preset'inin seçili olduğundan emin ol)");
        }

        [MenuItem("LingoRun/Screenshots/Capture Game View %#s", true)]
        public static bool CaptureValidate() { return Application.isPlaying; }

        /// <summary>Registers a fixed-resolution Game view size via the
        /// (internal) GameViewSizes API — same thing the "+" button does.</summary>
        static void TryAddGameViewSize(int width, int height, string label)
        {
            try
            {
                var sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleType.GetProperty("instance").GetValue(null, null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance,
                    new object[] { (int)GameViewSizeGroupType.Standalone });

                // iOS group when building for iOS, Standalone otherwise.
#if UNITY_IOS
                group = sizesType.GetMethod("GetGroup").Invoke(instance,
                    new object[] { (int)GameViewSizeGroupType.iOS });
#endif
                var sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
                var enumType = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
                var ctor = sizeType.GetConstructor(new[] { enumType, typeof(int), typeof(int), typeof(string) });
                var newSize = ctor.Invoke(new object[]
                    { Enum.Parse(enumType, "FixedResolution"), width, height, label });

                // Skip if an identical size is already registered.
                var getTotal = group.GetType().GetMethod("GetTotalCount");
                var getSize = group.GetType().GetMethod("GetGameViewSize");
                int total = (int)getTotal.Invoke(group, null);
                for (int i = 0; i < total; i++)
                {
                    var s = getSize.Invoke(group, new object[] { i });
                    int w = (int)s.GetType().GetProperty("width").GetValue(s, null);
                    int h = (int)s.GetType().GetProperty("height").GetValue(s, null);
                    if (w == width && h == height) return;
                }
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                sizesType.GetMethod("SaveToHDD").Invoke(instance, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("ScreenshotTool: boyut otomatik eklenemedi (" + e.Message
                    + ") — Game view'daki çözünürlük menüsünden '+' ile 2064x2752 elle ekleyebilirsin.");
            }
        }
    }
}
