using UnityEditor;

namespace LingoRun.EditorTools
{
    /// <summary>
    /// Forces everything under LingoRun/Resources/Sprites/Icons to import as a
    /// UI sprite (the project is 3D, so PNGs default to plain textures and
    /// Resources.Load&lt;Sprite&gt; would return null).
    /// </summary>
    public class IconImportSetup : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("LingoRun/Resources/Sprites/Icons/"))
                return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
        }
    }
}
