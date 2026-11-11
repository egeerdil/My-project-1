using System.IO;
using UnityEditor;
using UnityEngine;

namespace LingoRun.EditorTools
{
    /// <summary>
    /// Copies prefabs from imported environment packs (e.g. "Low Poly
    /// Environment - Nature Free", "Lowpoly Medieval Fantasy") into
    /// Resources/Prefabs/MapEnv so the runtime-built 3D world map
    /// (WorldMapUI) can load them with Resources.LoadAll.
    /// </summary>
    public static class MapEnvSetup
    {
        const string Target = "Assets/LingoRun/Resources/Prefabs/MapEnv";

        [MenuItem("LingoRun/Copy Map Env Prefabs")]
        public static void CopyMapEnvPrefabs()
        {
            string[] keywords = { "medieval", "fantasy", "nature", "environment" };
            if (!AssetDatabase.IsValidFolder(Target))
            {
                Directory.CreateDirectory(Target);
                AssetDatabase.Refresh();
            }

            int copied = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/LingoRun")) continue;
                string lower = path.ToLowerInvariant();
                bool match = false;
                foreach (var k in keywords)
                    if (lower.Contains(k)) { match = true; break; }
                if (!match) continue;
                // Skip huge composite scenes/locations; single props only.
                if (lower.Contains("location") || lower.Contains("scene")) continue;

                string name = Path.GetFileNameWithoutExtension(path)
                    .ToLowerInvariant().Replace(' ', '_');
                string dst = Target + "/" + name + ".prefab";
                if (File.Exists(dst)) continue;
                if (AssetDatabase.CopyAsset(path, dst)) copied++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MapEnvSetup: copied " + copied + " prefabs into " + Target
                + " — the 3D world map will pick them up on next open.");
        }
    }
}
