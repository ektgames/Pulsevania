using UnityEngine;

namespace Pulsevania.Core
{
    // Real asset references make sprites build dependencies without copying textures.
    public sealed class RuntimeSpriteSheet : ScriptableObject
    {
        public Sprite[] frames;

        public static Sprite[] Load(string sourcePath)
        {
            string key = sourcePath.Replace('\\', '/');
            if (key.StartsWith("Assets/")) key = key.Substring(7);
            if (key.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                key = key.Substring(0, key.Length - 4);
            var sheet = Resources.Load<RuntimeSpriteSheet>("RuntimeVisuals/Sheets/" + key);
            return sheet != null ? sheet.frames : System.Array.Empty<Sprite>();
        }
    }
}
