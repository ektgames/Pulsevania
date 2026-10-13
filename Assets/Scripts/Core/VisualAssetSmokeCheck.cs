using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pulsevania.Core
{
    // Opt-in player test. Never runs during normal gameplay or on TestFlight.
    public sealed class VisualAssetSmokeCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(), a => a == "-verify-runtime-visuals")) return;
            var go = new GameObject("VisualAssetSmokeCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<VisualAssetSmokeCheck>();
        }

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().name != "SampleScene" && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            try
            {
                Require(SceneManager.GetActiveScene().name == "SampleScene", "Game scene did not load");
                var hero = GameObject.Find("HeroKnightVisual");
                Require(hero != null, "Player did not create HeroKnightVisual");
                Require(hero.GetComponent<SpriteRenderer>().sprite != null, "Hero sprite missing");
                Require(hero.GetComponent<Animator>().runtimeAnimatorController != null, "Hero animator missing");
                Require(hero.GetComponent<HeroKnight>() == null, "Demo controller leaked into player visual");

                var storyLoader = typeof(UIManager).GetMethod("LoadStorySprite", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (string name in new[] { "kingdom_destroyed", "king", "leonardo", "princess" })
                {
                    var sprite = (Sprite)storyLoader.Invoke(UIManager.Instance, new object[] { name + ".png", name != "kingdom_destroyed" });
                    Require(sprite != null && sprite.texture.width > 2, "Story portrait missing: " + name);
                }
                Require(Resources.Load<TextAsset>("RuntimeVisuals/Portraits/hero_portrait") != null, "HUD portrait missing");

                var monsterLoader = typeof(MapManager).GetMethod("LoadCustomMonsterAnimations", BindingFlags.Instance | BindingFlags.NonPublic);
                var testGo = new GameObject("MonsterFramesCheck");
                testGo.AddComponent<SpriteRenderer>();
                var animation = testGo.AddComponent<SpriteAnimator>();
                int checkedClips = 0;
                for (int room = 1; room <= 50; room++)
                foreach (var behavior in new[] { EnemyGuardian.MonsterBehavior.ClubMelee, EnemyGuardian.MonsterBehavior.DaggerThrower, EnemyGuardian.MonsterBehavior.FlameMage, EnemyGuardian.MonsterBehavior.Boss })
                {
                    if (behavior == EnemyGuardian.MonsterBehavior.Boss && (room % 10 != 0 || room == 50)) continue;
                    Require((bool)monsterLoader.Invoke(MapManager.Instance, new object[] { animation, behavior, room }), "Monster visuals missing: " + room + "/" + behavior);
                    foreach (var state in new[] { AnimState.Idle, AnimState.Walk, AnimState.Attack, AnimState.Hurt, AnimState.Death })
                    {
                        Require(animation.TryGetClip(state, out var clip) && clip.frames != null && clip.frames.Length > 0 && Array.TrueForAll(clip.frames, s => s != null), "Missing frames: " + room + "/" + behavior + "/" + state);
                        checkedClips++;
                    }
                }
                Destroy(testGo);
                Debug.Log("[VisualSmoke] PASS: live hero, story portraits, HUD portrait and " + checkedClips + " monster clips loaded in player.");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[VisualSmoke] FAIL: " + exception);
                Application.Quit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
