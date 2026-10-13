using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Pulsevania.Core;

namespace Pulsevania.EditorTools
{
    public static class RuntimeVisualsBuilder
    {
        private const string Root = "Assets/Resources/RuntimeVisuals";

        [MenuItem("Pulsevania/Build/Prepare Runtime Visuals")]
        public static void Prepare()
        {
            string[] folders = {
                "Assets/Monsters Creatures Fantasy/Sprites", "Assets/Monsters Creatures Fantasy 2/Sprites",
                "Assets/Bringer Of Death/Sprite Sheet", "Assets/Dragon Warrior Files/Dragon Warrior PNG",
                "Assets/EVil Wizard/Sprites", "Assets/Slime"
            };
            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder)) throw new BuildFailedException("Missing visuals folder: " + folder);
                foreach (string file in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
                {
                    string path = file.Replace('\\', '/');
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
                    if (sprites.Length > 0) SaveSheet(path.Substring(7, path.Length - 11), sprites);
                }
            }
            foreach (string name in new[] { "Idle", "Walk", "spin", "Sleep", "Jump" })
            {
                string folder = Directory.GetDirectories("Assets/Slime").Single(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase));
                var sprites = Directory.GetFiles(folder, "*.png").Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')))
                    .Where(s => s != null).OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase).ToArray();
                if (sprites.Length == 0) throw new BuildFailedException("Missing Slime frames: " + name);
                SaveSheet("Slime/" + name, sprites);
            }

            Directory.CreateDirectory(Root + "/Portraits");
            foreach (string name in new[] { "kingdom_destroyed", "king", "leonardo", "princess", "hero_portrait" })
            {
                string source = "Assets/Sprites/" + name + ".png";
                if (!File.Exists(source)) throw new BuildFailedException("Missing portrait: " + source);
                string dest = Root + "/Portraits/" + name + ".bytes";
                byte[] bytes = File.ReadAllBytes(source);
                if (!File.Exists(dest) || !File.ReadAllBytes(dest).SequenceEqual(bytes)) File.WriteAllBytes(dest, bytes);
            }
            AssetDatabase.Refresh();

            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hero Knight - Pixel Art/Demo/HeroKnight.prefab");
            if (original == null) throw new BuildFailedException("Hero Knight source prefab is missing.");
            var sourceRenderer = original.GetComponent<SpriteRenderer>();
            var sourceAnimator = original.GetComponent<Animator>();
            if (sourceRenderer == null || sourceRenderer.sprite == null || sourceAnimator == null || sourceAnimator.runtimeAnimatorController == null)
                throw new BuildFailedException("Hero Knight sprite or animation controller is missing.");
            var visual = new GameObject("HeroKnightVisual");
            try
            {
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sourceRenderer.sprite;
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
                PrefabUtility.SaveAsPrefabAsset(visual, Root + "/HeroVisual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(visual); }
            AssetDatabase.SaveAssets();
            Debug.Log("[RuntimeVisuals] Hero, monster frames and five portraits prepared for player builds.");
        }

        private static void SaveSheet(string key, Sprite[] sprites)
        {
            string path = Root + "/Sheets/" + key + ".asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            var sheet = AssetDatabase.LoadAssetAtPath<RuntimeSpriteSheet>(path);
            if (sheet == null)
            {
                sheet = ScriptableObject.CreateInstance<RuntimeSpriteSheet>();
                AssetDatabase.CreateAsset(sheet, path);
            }
            sheet.frames = sprites;
            EditorUtility.SetDirty(sheet);
        }
    }
}
