using UnityEditor;
using UnityEngine;

namespace Pulsevania.Editor
{
    public class SpriteImporterWatcher : AssetPostprocessor
    {
        private static readonly string[] TargetSprites = {
            "Assets/Sprites/Player_Idle_0.png",
            "Assets/Sprites/Player_Idle_1.png",
            "Assets/Sprites/Player_Walk_0.png",
            "Assets/Sprites/Player_Walk_1.png",
            "Assets/Sprites/Player_Jump.png",
            "Assets/Sprites/Player_Attack.png",
            "Assets/Sprites/Player_Hurt.png",
            "Assets/Sprites/Player_Death.png"
        };

        void OnPreprocessTexture()
        {
            if (System.Array.Exists(TargetSprites, path => string.Equals(path, assetPath, System.StringComparison.OrdinalIgnoreCase)))
            {
                TextureImporter importer = (TextureImporter)assetImporter;
                bool needsUpdate = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    needsUpdate = true;
                }
                if (importer.spritePixelsPerUnit != 16)
                {
                    importer.spritePixelsPerUnit = 16;
                    needsUpdate = true;
                }
                if (importer.filterMode != FilterMode.Point)
                {
                    importer.filterMode = FilterMode.Point;
                    needsUpdate = true;
                }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    Debug.Log("[Pulsevania] SpriteImporterWatcher auto-configured settings for: " + assetPath);
                }
            }
        }
    }
}
