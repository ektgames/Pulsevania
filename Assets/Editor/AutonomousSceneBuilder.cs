using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Pulsevania.Core;

namespace Pulsevania.Editor
{
    public class AutonomousSceneBuilder : EditorWindow
    {
        private static string SpritePath = "Assets/Sprites";

        // Level Map Grid (10 rows x 40 columns)
        private static string[] levelMap = {
            "########################################", // 0
            "#......................................#", // 1
            "#............................D.........#", // 2
            "#..........................#######.....#", // 3
            "#..................M..........L....R...#", // 4
            "#..........#####..............L........#", // 5
            "#.............................L........#", // 6
            "#...C......P..................L........#", // 7
            "#######...S...S...............L........#", // 8
            "########################################"  // 9
        };

        [MenuItem("Pulsevania / Build Core Scene")]
        public static void BuildCoreScene()
        {
            Debug.Log("[Pulsevania] BuildCoreScene execution started.");
            // Clear current active scene except Main Camera
            ClearActiveScene();

            // Setup AudioManager
            if (GameObject.FindFirstObjectByType<AudioManager>() == null)
            {
                GameObject audioManagerGo = new GameObject("AudioManager");
                audioManagerGo.AddComponent<AudioManager>();
                Debug.Log("[Pulsevania] Instantiated AudioManager in Editor.");
            }

            // Set up layers programmatically
            int playerLayer = AddLayer("Player");
            int groundLayer = AddLayer("Ground");
            int enemyLayer = AddLayer("Enemy");

            // Grant 1000 starting gold for testing upgrades if total gold is 0
            if (PlayerPrefs.GetInt("Pulsevania_TotalGold", 0) == 0)
            {
                PlayerPrefs.SetInt("Pulsevania_TotalGold", 1000);
                PlayerPrefs.Save();
                Debug.Log("[Pulsevania] Granted 1000 starting gold in PlayerPrefs for easy upgrade testing!");
            }

            // Setup Procedural Visual Assets
            SetupProceduralVisuals();

            string[] leftRoom = {
                "############",
                "#...........",
                "#...........",
                "#...........",
                "#...........",
                "#...........",
                "#...........",
                "#...C..P....",
                "############",
                "############"
            };

            string[] rightRoom = {
                "############",
                "...........#",
                ".........D.#",
                ".......#####",
                "...........#",
                "...........#",
                "......B....#",
                "...........#",
                "############",
                "############"
            };

            string[][] middleModules = new string[][] {
                new string[] { // Module A: Ladder & Wizard
                    "################",
                    "................",
                    "................",
                    "................",
                    ".....L....R.....",
                    ".....L..........",
                    ".....L..........",
                    ".....L..........",
                    ".....L..........",
                    "################"
                },
                new string[] { // Module B: Spiky Patrol Skeleton
                    "................",
                    "................",
                    "................",
                    "................",
                    "................",
                    "....#####.......",
                    "................",
                    "........M.......",
                    "....S.......S...",
                    "################"
                },
                new string[] { // Module C: Double ladder & hazard
                    "................",
                    "................",
                    "................",
                    "....####........",
                    ".......L........",
                    ".......L........",
                    ".......L...S....",
                    ".......L........",
                    "....S..L........",
                    "################"
                }
            };

            int randIndex = Random.Range(0, middleModules.Length);
            string[] selectedMid = middleModules[randIndex];
            string[] dynamicLevelMap = new string[10];
            for (int r = 0; r < 10; r++)
            {
                dynamicLevelMap[r] = leftRoom[r] + selectedMid[r] + rightRoom[r];
            }
            Debug.Log("[Pulsevania] Stitched middle room Module: " + randIndex);

            // 1. Setup Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                mainCam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            mainCam.transform.position = new Vector3(0f, 1f, -10f);
            mainCam.transform.rotation = Quaternion.identity;
            mainCam.backgroundColor = new Color(0.08f, 0.08f, 0.12f); // dark dungeon bg
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            
            CameraFollow camFollow = mainCam.GetComponent<CameraFollow>() ?? mainCam.gameObject.AddComponent<CameraFollow>();

            // 2. Setup GameManager, ProjectilePool, DamageTextPool, InventoryManager
            GameObject gameManagerGo = new GameObject("GameManager");
            GameManager gm = gameManagerGo.AddComponent<GameManager>();
            ProjectilePool projPool = gameManagerGo.AddComponent<ProjectilePool>();
            DamageTextPool dmgTextPool = gameManagerGo.AddComponent<DamageTextPool>();
            UIManager uiManager = gameManagerGo.AddComponent<UIManager>();
            InventoryManager invManager = gameManagerGo.AddComponent<InventoryManager>();

            // 3. Build Level Layout Grid
            GameObject playerGo = null;
            Transform playerTransform = null;

            GameObject gridHolder = new GameObject("LevelGrid");

            GameObject groundGrid = new GameObject("GroundGrid");
            groundGrid.transform.SetParent(gridHolder.transform);
            groundGrid.layer = groundLayer;
            Rigidbody2D compositeRb = groundGrid.AddComponent<Rigidbody2D>();
            compositeRb.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D compositeCol = groundGrid.AddComponent<CompositeCollider2D>();
            compositeCol.geometryType = CompositeCollider2D.GeometryType.Outlines;

            // Textures/Sprites references
            Sprite stoneSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "StoneBlock.png"));
            Sprite ladderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "LadderStep.png"));
            Sprite spikesSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Spikes.png"));
            Sprite chestSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Chest_Closed.png"));
            Sprite doorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Door_Closed.png"));

            int rows = dynamicLevelMap.Length;
            int cols = dynamicLevelMap[0].Length;

            for (int r = 0; r < rows; r++)
            {
                string rowStr = dynamicLevelMap[r];
                for (int c = 0; c < cols; c++)
                {
                    char cell = rowStr[c];
                    float posX = c - (cols / 2f);
                    float posY = (rows / 2f) - r;
                    Vector3 position = new Vector3(posX, posY, 0f);

                    switch (cell)
                    {
                        case '#': // Stone wall/ground
                            GameObject stone = new GameObject("StoneBlock_" + r + "_" + c);
                            stone.transform.position = position;
                            stone.transform.SetParent(groundGrid.transform);
                            stone.layer = groundLayer;

                            SpriteRenderer stoneSR = stone.AddComponent<SpriteRenderer>();
                            stoneSR.sprite = stoneSprite;

                            BoxCollider2D stoneCol = stone.AddComponent<BoxCollider2D>();
                            stoneCol.size = Vector2.one;
                            stoneCol.usedByComposite = true;
                            break;

                        case 'L': // Ladder
                            GameObject ladder = new GameObject("Ladder_" + r + "_" + c);
                            ladder.transform.position = position;
                            ladder.transform.SetParent(gridHolder.transform);

                            SpriteRenderer ladderSR = ladder.AddComponent<SpriteRenderer>();
                            ladderSR.sprite = ladderSprite;

                            BoxCollider2D ladderCol = ladder.AddComponent<BoxCollider2D>();
                            ladderCol.isTrigger = true;
                            ladder.AddComponent<Ladder>();
                            break;

                        case 'S': // Spikes
                            GameObject spikes = new GameObject("Spikes_" + r + "_" + c);
                            spikes.transform.position = position + new Vector3(0f, -0.2f, 0f); // Rest slightly lower
                            spikes.transform.SetParent(gridHolder.transform);

                            SpriteRenderer spikesSR = spikes.AddComponent<SpriteRenderer>();
                            spikesSR.sprite = spikesSprite;

                            BoxCollider2D spikesCol = spikes.AddComponent<BoxCollider2D>();
                            spikesCol.isTrigger = true;
                            spikesCol.size = new Vector2(1f, 0.6f);
                            spikes.AddComponent<StaticHazard>();
                            break;

                        case 'C': // Chest
                            GameObject chest = new GameObject("LootChest");
                            chest.transform.position = position;
                            chest.transform.SetParent(gridHolder.transform);
                            chest.layer = enemyLayer;

                            SpriteRenderer chestSR = chest.AddComponent<SpriteRenderer>();
                            chestSR.sprite = chestSprite;

                            BoxCollider2D chestCol = chest.AddComponent<BoxCollider2D>();
                            chestCol.isTrigger = false;
                            chestCol.size = new Vector2(1f, 0.8f);

                            chest.AddComponent<PulsevaniaChest>();
                            break;

                        case 'D': // Door
                            GameObject door = new GameObject("LockedDoor");
                            door.transform.position = position;
                            door.transform.SetParent(gridHolder.transform);

                            SpriteRenderer doorSR = door.AddComponent<SpriteRenderer>();
                            doorSR.sprite = doorSprite;

                            BoxCollider2D doorCol = door.AddComponent<BoxCollider2D>();
                            doorCol.isTrigger = true;
                            doorCol.size = new Vector2(1f, 1.5f);
                            door.AddComponent<LockedDoor>();
                            break;

                        case 'P': // Player Starting Point
                            playerGo = new GameObject("Player");
                            playerGo.tag = "Player";
                            playerGo.layer = playerLayer;
                            playerGo.transform.position = position;
                            playerTransform = playerGo.transform;

                            // Spawn Merchant NPC nearby
                            GameObject merchantGo = new GameObject("Merchant_NPC");
                            merchantGo.transform.position = position + new Vector3(3.5f, -0.5f, -0.5f);
                            merchantGo.AddComponent<SpriteRenderer>();
                            BoxCollider2D merchantCol = merchantGo.AddComponent<BoxCollider2D>();
                            merchantCol.size = new Vector2(1.5f, 1.2f);
                            merchantCol.isTrigger = true;
                            merchantGo.AddComponent<MerchantNPC>();

                            // Instantiate HeroKnight prefab if available
                            GameObject heroVisual = null;
                            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hero Knight - Pixel Art/Demo/HeroKnight.prefab");
                            if (heroPrefab != null)
                            {
                                heroVisual = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab, playerGo.transform);
                                heroVisual.name = "HeroKnightVisual";
                                heroVisual.transform.localPosition = new Vector3(0f, -0.7f, 0f); // align visual with collider base to touch ground
                                
                                // Clean conflict components on visual prefab child
                                var hkScript = heroVisual.GetComponent<HeroKnight>();
                                if (hkScript != null) DestroyImmediate(hkScript);

                                var hkRb = heroVisual.GetComponent<Rigidbody2D>();
                                if (hkRb != null) DestroyImmediate(hkRb);

                                var hkCol = heroVisual.GetComponent<BoxCollider2D>();
                                if (hkCol != null) DestroyImmediate(hkCol);
                                
                                // Disable wall/ground sensors since parent handles it
                                Transform groundSensor = heroVisual.transform.Find("GroundSensor");
                                if (groundSensor != null) groundSensor.gameObject.SetActive(false);
                                
                                for (int i = 1; i <= 2; i++)
                                {
                                    Transform wsl = heroVisual.transform.Find("WallSensor_L" + i);
                                    if (wsl != null) wsl.gameObject.SetActive(false);
                                    Transform wsr = heroVisual.transform.Find("WallSensor_R" + i);
                                    if (wsr != null) wsr.gameObject.SetActive(false);
                                }
                            }

                            SpriteRenderer playerSR = playerGo.AddComponent<SpriteRenderer>();
                            if (heroVisual != null)
                            {
                                playerSR.enabled = false;
                            }

                            Rigidbody2D playerRb = playerGo.AddComponent<Rigidbody2D>();
                            playerRb.constraints = RigidbodyConstraints2D.FreezeRotation;
                            playerRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                            BoxCollider2D playerCol = playerGo.AddComponent<BoxCollider2D>();
                            playerCol.size = new Vector2(0.6f, 1.4f);
                            playerCol.offset = new Vector2(0f, -0.05f);

                            // Assign frictionless material to prevent player sticking to walls
                            PhysicsMaterial2D playerFrictionless = new PhysicsMaterial2D("PlayerFrictionless");
                            playerFrictionless.friction = 0f;
                            playerFrictionless.bounciness = 0f;
                            playerCol.sharedMaterial = playerFrictionless;

                            HealthSystem playerHealth = playerGo.AddComponent<HealthSystem>();
                            Damageable playerDmg = playerGo.AddComponent<Damageable>();
                            SerializedObject playerDmgSO = new SerializedObject(playerDmg);
                            playerDmgSO.FindProperty("team").enumValueIndex = (int)Team.Player;
                            playerDmgSO.FindProperty("healthSystem").objectReferenceValue = playerHealth;
                            playerDmgSO.ApplyModifiedProperties();

                            // Player Visual Equipment Layers
                            string[] layers = { "Equip_Helmet", "Equip_Armor", "Equip_Gloves", "Visual_Legs", "Equip_Boots", "Visual_Weapon", "Visual_Shield", "Visual_ThrowingKnife" };
                            float[] xOffsets = { 0f, 0f, 0f, 0f, 0f, 0.22f, -0.22f, -0.22f };
                            float[] yOffsets = { 0.3f, 0.05f, 0f, -0.2f, -0.38f, -0.15f, -0.15f, -0.15f };
                            for (int i = 0; i < layers.Length; i++)
                            {
                                GameObject layerGo = new GameObject(layers[i]);
                                layerGo.transform.SetParent(playerGo.transform);
                                layerGo.transform.localPosition = new Vector3(xOffsets[i], yOffsets[i], 0f);
                                SpriteRenderer layerSR = layerGo.AddComponent<SpriteRenderer>();
                                layerSR.sortingOrder = (layers[i] == "Visual_Weapon" || layers[i] == "Visual_Shield" || layers[i] == "Visual_ThrowingKnife") ? 3 : 2; // Equipped weapon/shield on top
                            }

                            // Player Checkpoints
                            GameObject playerGroundCheck = new GameObject("GroundCheckPoint");
                            playerGroundCheck.transform.SetParent(playerGo.transform);
                            playerGroundCheck.transform.localPosition = new Vector3(0f, -0.76f, 0f);

                            GameObject playerAttackPoint = new GameObject("AttackPoint");
                            playerAttackPoint.transform.SetParent(playerGo.transform);
                            playerAttackPoint.transform.localPosition = new Vector3(0.6f, 0f, 0f);

                            // Setup Animated clips
                            SpriteAnimator playerAnimator = playerGo.AddComponent<SpriteAnimator>();
                            SetupPlayerAnimations(playerAnimator);

                            PlayerController playerCtrl = playerGo.AddComponent<PlayerController>();
                            SerializedObject playerCtrlSO = new SerializedObject(playerCtrl);
                            playerCtrlSO.FindProperty("groundCheckPoint").objectReferenceValue = playerGroundCheck.transform;
                            playerCtrlSO.FindProperty("attackPoint").objectReferenceValue = playerAttackPoint.transform;
                            playerCtrlSO.FindProperty("groundLayer").intValue = 1 << groundLayer;
                            playerCtrlSO.FindProperty("enemyLayer").intValue = 1 << enemyLayer;
                            playerCtrlSO.FindProperty("attackRange").floatValue = 1.5f;
                            playerCtrlSO.FindProperty("spriteAnimator").objectReferenceValue = playerAnimator;
                            if (heroVisual != null)
                            {
                                playerCtrlSO.FindProperty("heroAnimator").objectReferenceValue = heroVisual.GetComponent<Animator>();
                            }
                            playerCtrlSO.ApplyModifiedProperties();
                            break;

                        case 'M': // Melee Patrol Enemy
                            GameObject enemyGo = new GameObject("Enemy_Skeleton");
                            enemyGo.layer = enemyLayer;
                            enemyGo.transform.position = position;

                            SpriteRenderer enemySR = enemyGo.AddComponent<SpriteRenderer>();

                            Rigidbody2D enemyRb = enemyGo.AddComponent<Rigidbody2D>();
                            enemyRb.constraints = RigidbodyConstraints2D.FreezeRotation;

                            BoxCollider2D enemyCol = enemyGo.AddComponent<BoxCollider2D>();
                            enemyCol.size = new Vector2(0.8f, 1.4f);

                            HealthSystem enemyHealth = enemyGo.AddComponent<HealthSystem>();
                            Damageable enemyDmg = enemyGo.AddComponent<Damageable>();
                            SerializedObject enemyDmgSO = new SerializedObject(enemyDmg);
                            enemyDmgSO.FindProperty("team").enumValueIndex = (int)Team.Enemy;
                            enemyDmgSO.FindProperty("healthSystem").objectReferenceValue = enemyHealth;
                            enemyDmgSO.ApplyModifiedProperties();

                            // AI checks
                            GameObject edgeCheck = new GameObject("EdgeCheck");
                            edgeCheck.transform.SetParent(enemyGo.transform);
                            edgeCheck.transform.localPosition = new Vector3(0.5f, -0.8f, 0f);

                            GameObject wallCheck = new GameObject("WallCheck");
                            wallCheck.transform.SetParent(enemyGo.transform);
                            wallCheck.transform.localPosition = new Vector3(0.5f, 0f, 0f);

                            GameObject enemyAttackPoint = new GameObject("EnemyAttackPoint");
                            enemyAttackPoint.transform.SetParent(enemyGo.transform);
                            enemyAttackPoint.transform.localPosition = new Vector3(0.5f, 0f, 0f);

                            // Setup Enemy animator
                            SpriteAnimator enemyAnimator = enemyGo.AddComponent<SpriteAnimator>();
                            SetupEnemyAnimations(enemyAnimator);

                            BaseEnemyAI enemyAI = enemyGo.AddComponent<BaseEnemyAI>();
                            SerializedObject enemyAISO = new SerializedObject(enemyAI);
                            enemyAISO.FindProperty("edgeCheckPoint").objectReferenceValue = edgeCheck.transform;
                            enemyAISO.FindProperty("wallCheckPoint").objectReferenceValue = wallCheck.transform;
                            enemyAISO.FindProperty("attackPoint").objectReferenceValue = enemyAttackPoint.transform;
                            enemyAISO.FindProperty("groundLayer").intValue = 1 << groundLayer;
                            enemyAISO.FindProperty("spriteAnimator").objectReferenceValue = enemyAnimator;
                            enemyAISO.ApplyModifiedProperties();
                            break;

                        case 'R': // Ranged Static Wizard Enemy
                            GameObject wizardGo = new GameObject("Enemy_Wizard");
                            wizardGo.layer = enemyLayer;
                            wizardGo.transform.position = position;

                            SpriteRenderer wizardSR = wizardGo.AddComponent<SpriteRenderer>();
                            wizardSR.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "RangedEnemy_Idle.png"));

                            BoxCollider2D wizardCol = wizardGo.AddComponent<BoxCollider2D>();
                            wizardCol.size = new Vector2(0.8f, 1.4f);

                            HealthSystem wizardHealth = wizardGo.AddComponent<HealthSystem>();
                            Damageable wizardDmg = wizardGo.AddComponent<Damageable>();
                            SerializedObject wizardDmgSO = new SerializedObject(wizardDmg);
                            wizardDmgSO.FindProperty("team").enumValueIndex = (int)Team.Enemy;
                            wizardDmgSO.FindProperty("healthSystem").objectReferenceValue = wizardHealth;
                            wizardDmgSO.ApplyModifiedProperties();

                            GameObject wizardShootPoint = new GameObject("ShootPoint");
                            wizardShootPoint.transform.SetParent(wizardGo.transform);
                            wizardShootPoint.transform.localPosition = new Vector3(-0.6f, 0f, 0f);

                            RangedEnemyAI wizardAI = wizardGo.AddComponent<RangedEnemyAI>();
                            SerializedObject wizardAISO = new SerializedObject(wizardAI);
                            wizardAISO.FindProperty("shootPoint").objectReferenceValue = wizardShootPoint.transform;
                            wizardAISO.ApplyModifiedProperties();
                            break;

                        case 'B': // Boss Enemy
                            GameObject bossGo = new GameObject("Enemy_Boss");
                            bossGo.layer = enemyLayer;
                            bossGo.transform.position = position;
                            bossGo.transform.localScale = new Vector3(1.8f, 1.8f, 1f);

                            SpriteRenderer bossSR = bossGo.AddComponent<SpriteRenderer>();
                            bossSR.color = new Color(1f, 0.4f, 0.4f, 1f);

                            Rigidbody2D bossRb = bossGo.AddComponent<Rigidbody2D>();
                            bossRb.constraints = RigidbodyConstraints2D.FreezeRotation;

                            BoxCollider2D bossCol = bossGo.AddComponent<BoxCollider2D>();
                            bossCol.size = new Vector2(0.8f, 1.4f);

                            HealthSystem bossHealth = bossGo.AddComponent<HealthSystem>();
                            bossHealth.SetMaxHealth(10);
                            
                            Damageable bossDmg = bossGo.AddComponent<Damageable>();
                            SerializedObject bossDmgSO = new SerializedObject(bossDmg);
                            bossDmgSO.FindProperty("team").enumValueIndex = (int)Team.Enemy;
                            bossDmgSO.FindProperty("healthSystem").objectReferenceValue = bossHealth;
                            bossDmgSO.ApplyModifiedProperties();

                            SpriteAnimator bossAnimator = bossGo.AddComponent<SpriteAnimator>();
                            SetupEnemyAnimations(bossAnimator);

                            BossAI bossAI = bossGo.AddComponent<BossAI>();
                            SerializedObject bossAISO = new SerializedObject(bossAI);
                            bossAISO.FindProperty("playerLayer").intValue = 1 << playerLayer;
                            bossAISO.FindProperty("spriteAnimator").objectReferenceValue = bossAnimator;
                            bossAISO.ApplyModifiedProperties();
                            break;
                    }
                }
            }

            // 4. Link Camera Target
            if (playerTransform != null)
            {
                camFollow.SetTarget(playerTransform);
            }

            // 5. Setup UI Canvas
            SetupCanvas(uiManager);

            // Save scene state changes
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("[Pulsevania] Connected zindan level generated successfully! Ready to play.");
        }

        private static void ClearActiveScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            foreach (GameObject obj in rootObjects)
            {
                if (obj.tag != "MainCamera")
                {
                    DestroyImmediate(obj);
                }
            }
        }

        private static void SetupPlayerAnimations(SpriteAnimator animator)
        {
            List<SpriteAnimator.AnimationClip> playerClips = new List<SpriteAnimator.AnimationClip>();

            playerClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Idle,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Idle_0.png")),
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Idle_1.png"))
                },
                frameRate = 3f,
                loop = true
            });

            playerClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Walk,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Walk_0.png")),
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Walk_1.png"))
                },
                frameRate = 6f,
                loop = true
            });

            playerClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Jump,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Jump.png"))
                },
                frameRate = 1f,
                loop = false
            });

            playerClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Attack,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Attack.png")),
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Idle_0.png"))
                },
                frameRate = 8f,
                loop = false
            });

            playerClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Hurt,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Hurt.png")),
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Idle_0.png"))
                },
                frameRate = 6f,
                loop = false
            });

            playerClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Death,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Player_Death.png"))
                },
                frameRate = 1f,
                loop = false
            });

            animator.SetClips(playerClips);
        }

        private static void SetupEnemyAnimations(SpriteAnimator animator)
        {
            List<SpriteAnimator.AnimationClip> enemyClips = new List<SpriteAnimator.AnimationClip>();

            enemyClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Idle,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Idle_0.png")),
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Idle_1.png"))
                },
                frameRate = 3f,
                loop = true
            });

            enemyClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Walk,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Walk_0.png")),
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Walk_1.png"))
                },
                frameRate = 5f,
                loop = true
            });

            enemyClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Attack,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Idle_1.png")),
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Idle_0.png"))
                },
                frameRate = 6f,
                loop = false
            });

            enemyClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Hurt,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Idle_0.png"))
                },
                frameRate = 6f,
                loop = false
            });

            enemyClips.Add(new SpriteAnimator.AnimationClip {
                state = AnimState.Death,
                frames = new Sprite[] {
                    AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(SpritePath, "Enemy_Idle_1.png"))
                },
                frameRate = 1f,
                loop = false
            });

            animator.SetClips(enemyClips);
        }

        private static void SetupCanvas(UIManager uiManager)
        {
            // Canvas parent
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Event System
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            Color panelBgColor = new Color(0.08f, 0.08f, 0.1f, 0.92f);
            Color buttonColor = new Color(0.25f, 0.2f, 0.15f, 1f); // wooden brown button

            // Create Panels
            GameObject mainMenu = CreatePanel("MainMenuPanel", canvasGo, panelBgColor);
            CreateButton("PlayButton", mainMenu, "PLAY GAME", buttonColor).transform.localPosition = new Vector3(0f, 80f, 0f);
            CreateButton("ShopButton", mainMenu, "SHOP", buttonColor).transform.localPosition = new Vector3(0f, 0f, 0f);
            CreateButton("QuitButton", mainMenu, "QUIT", buttonColor).transform.localPosition = new Vector3(0f, -80f, 0f);

            GameObject shopMenu = CreatePanel("ShopPanel", canvasGo, panelBgColor);
            CreateText("ShopTitle", shopMenu, "UPGRADES SHOP", TextAnchor.MiddleCenter, new Vector2(500f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), 48, Color.yellow);
            GameObject shopGold = CreateText("ShopGoldText", shopMenu, "Total Gold: 0", TextAnchor.MiddleCenter, new Vector2(400f, 50f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), 32, Color.white);
            
            // HP Upgrade Section
            GameObject hpLabel = CreateText("HPUpgradeText", shopMenu, "Max HP Hearts: 3\nUpgrade Cost: 50 G", TextAnchor.MiddleCenter, new Vector2(500f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250f, 50f), 24, Color.white);
            GameObject hpBtn = CreateButton("UpgradeHPButton", shopMenu, "BUY HP", buttonColor);
            hpBtn.transform.localPosition = new Vector3(-250f, -50f, 0f);
            
            // ATK Upgrade Section
            GameObject atkLabel = CreateText("ATKUpgradeText", shopMenu, "Melee Damage: 1\nUpgrade Cost: 75 G", TextAnchor.MiddleCenter, new Vector2(500f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250f, 50f), 24, Color.white);
            GameObject atkBtn = CreateButton("UpgradeATKButton", shopMenu, "BUY ATK", buttonColor);
            atkBtn.transform.localPosition = new Vector3(250f, -50f, 0f);
            
            // Close Button
            GameObject closeBtn = CreateButton("CloseShopButton", shopMenu, "BACK", buttonColor);
            closeBtn.transform.localPosition = new Vector3(0f, -220f, 0f);
            shopMenu.SetActive(false);

            GameObject pauseMenu = CreatePanel("PausePanel", canvasGo, panelBgColor);
            CreateButton("ResumeButton", pauseMenu, "RESUME", buttonColor).transform.localPosition = new Vector3(0f, 80f, 0f);
            CreateButton("RestartButton", pauseMenu, "RESTART", buttonColor).transform.localPosition = new Vector3(0f, 0f, 0f);
            CreateButton("MainMenuButton", pauseMenu, "MAIN MENU", buttonColor).transform.localPosition = new Vector3(0f, -80f, 0f);
            pauseMenu.SetActive(false);

            GameObject gameOver = CreatePanel("GameOverPanel", canvasGo, panelBgColor);
            CreateText("GameOverTitle", gameOver, "GAME OVER", TextAnchor.MiddleCenter, new Vector2(400f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), 48, Color.red);
            CreateButton("SavepointButton", gameOver, "NEAREST CHECKPOINT", buttonColor).transform.localPosition = new Vector3(0f, 60f, 0f);
            CreateButton("RestartButton", gameOver, "RESTART RUN", buttonColor).transform.localPosition = new Vector3(0f, -20f, 0f);
            CreateButton("QuitButton", gameOver, "QUIT GAME", buttonColor).transform.localPosition = new Vector3(0f, -100f, 0f);
            gameOver.SetActive(false);

            GameObject levelComplete = CreatePanel("LevelCompletePanel", canvasGo, panelBgColor);
            CreateText("LevelCompleteTitle", levelComplete, "LEVEL COMPLETE", TextAnchor.MiddleCenter, new Vector2(500f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), 48, Color.green);
            CreateButton("NextLevelButton", levelComplete, "NEXT LEVEL", buttonColor).transform.localPosition = new Vector3(0f, 0f, 0f);
            CreateButton("MainMenuButton", levelComplete, "MAIN MENU", buttonColor).transform.localPosition = new Vector3(0f, -80f, 0f);
            levelComplete.SetActive(false);

            GameObject hud = CreateUIElement("GameplayHUD", canvasGo);
            RectTransform hudRect = hud.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.one;

            // 1. Create HealthBar Slider
            GameObject healthBarGo = CreateUIElement("HealthBar", hud);
            RectTransform sliderRect = healthBarGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 1f);
            sliderRect.anchorMax = new Vector2(0f, 1f);
            sliderRect.pivot = new Vector2(0f, 1f);
            sliderRect.anchoredPosition = new Vector2(25f, -25f);
            sliderRect.sizeDelta = new Vector2(220f, 25f);

            Slider slider = healthBarGo.AddComponent<Slider>();

            // Background
            GameObject bgGo = CreateUIElement("Background", healthBarGo);
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.one;
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark Grey

            // Fill Area
            GameObject fillAreaGo = CreateUIElement("Fill Area", healthBarGo);
            RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.one;

            // Fill
            GameObject fillGo = CreateUIElement("Fill", fillAreaGo);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.one;
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.9f, 0.1f, 0.1f, 1f); // Vibrant Red

            // Configure Slider
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 100f;
            slider.interactable = false;

            // Percent Text Label
            GameObject percentTextGo = CreateText("PercentText", healthBarGo, "100%", TextAnchor.MiddleLeft, new Vector2(60f, 25f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(10f, 0f), 18, Color.white);
            percentTextGo.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // 2. Create ExtraHeartsContainer Layout Group
            GameObject extraHeartsContainerGo = CreateUIElement("ExtraHeartsContainer", hud);
            RectTransform containerRect = extraHeartsContainerGo.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 1f);
            containerRect.anchorMax = new Vector2(0f, 1f);
            containerRect.pivot = new Vector2(0f, 1f);
            containerRect.anchoredPosition = new Vector2(25f, -55f);
            containerRect.sizeDelta = new Vector2(300f, 30f);

            HorizontalLayoutGroup layout = extraHeartsContainerGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            GameObject goldGo = CreateText("GoldText", hud, "Gold: 0", TextAnchor.MiddleLeft, new Vector2(250f, 50f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(400f, -60f), 28, Color.yellow);
            GameObject keysGo = CreateText("KeysText", hud, "Keys: 0", TextAnchor.MiddleLeft, new Vector2(200f, 50f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(600f, -60f), 28, Color.cyan);
            GameObject potionsGo = CreateText("PotionsText", hud, "Potions: 0", TextAnchor.MiddleLeft, new Vector2(200f, 50f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(750f, -60f), 28, Color.green);

            // Minimap UI
            GameObject minimapHolder = CreateUIElement("Minimap", hud);
            RectTransform mmRect = minimapHolder.GetComponent<RectTransform>();
            mmRect.anchorMin = new Vector2(0.5f, 1f);
            mmRect.anchorMax = new Vector2(0.5f, 1f);
            mmRect.anchoredPosition = new Vector2(0f, -60f);
            mmRect.sizeDelta = new Vector2(160f, 40f);

            CreateMinimapCell("CellLeft", minimapHolder, new Vector2(-50f, 0f));
            CreateMinimapCell("CellMid", minimapHolder, new Vector2(0f, 0f));
            CreateMinimapCell("CellRight", minimapHolder, new Vector2(50f, 0f));

            // HUD Pause Button
            CreateButton("PauseButton", hud, "II", buttonColor).GetComponent<RectTransform>().anchoredPosition = new Vector2(-100f, -60f);
            hud.transform.Find("PauseButton").GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
            hud.transform.Find("PauseButton").GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            hud.transform.Find("PauseButton").GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);

            // Virtual mobile controls
            GameObject btnL = CreateHoldButton("BtnLeft", hud, "<-", buttonColor, new Vector2(65f, 160f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            GameObject btnR = CreateHoldButton("BtnRight", hud, "->", buttonColor, new Vector2(255f, 160f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            GameObject btnU = CreateHoldButton("BtnUp", hud, "^", buttonColor, new Vector2(160f, 255f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            GameObject btnD = CreateHoldButton("BtnDown", hud, "v", buttonColor, new Vector2(160f, 65f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            GameObject btnJumpGo = CreateButton("BtnJump", hud, "JUMP", buttonColor);
            SetAnchorAndPos(btnJumpGo, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 100f));
            
            GameObject btnAttackGo = CreateButton("BtnAttack", hud, "ATTACK", buttonColor);
            SetAnchorAndPos(btnAttackGo, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 100f));

            GameObject btnBlockGo = CreateButton("BtnBlock", hud, "BLOCK", buttonColor);
            SetAnchorAndPos(btnBlockGo, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-220f, 200f));
            
            GameObject btnShootGo = CreateButton("BtnShoot", hud, "SHOOT", buttonColor);
            SetAnchorAndPos(btnShootGo, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-380f, 200f));

            GameObject btnPotionGo = CreateButton("BtnPotion", hud, "USE POTION", buttonColor);
            SetAnchorAndPos(btnPotionGo, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-150f, 280f));

            // Link UIManager fields
            SerializedObject uiSO = new SerializedObject(uiManager);
            uiSO.FindProperty("mainMenuPanel").objectReferenceValue = mainMenu;
            uiSO.FindProperty("gameplayHUD").objectReferenceValue = hud;
            uiSO.FindProperty("pausePanel").objectReferenceValue = pauseMenu;
            uiSO.FindProperty("gameOverPanel").objectReferenceValue = gameOver;
            uiSO.FindProperty("levelCompletePanel").objectReferenceValue = levelComplete;
            uiSO.FindProperty("shopPanel").objectReferenceValue = shopMenu;

            uiSO.FindProperty("healthSlider").objectReferenceValue = slider;
            uiSO.FindProperty("healthPercentText").objectReferenceValue = percentTextGo.GetComponent<Text>();
            uiSO.FindProperty("extraHeartsContainer").objectReferenceValue = layout;
            uiSO.FindProperty("goldText").objectReferenceValue = goldGo.GetComponent<Text>();
            uiSO.FindProperty("keysText").objectReferenceValue = keysGo.GetComponent<Text>();
            uiSO.FindProperty("potionsText").objectReferenceValue = potionsGo.GetComponent<Text>();

            uiSO.FindProperty("btnLeft").objectReferenceValue = btnL.GetComponent<MobileHoldButton>();
            uiSO.FindProperty("btnRight").objectReferenceValue = btnR.GetComponent<MobileHoldButton>();
            uiSO.FindProperty("btnUp").objectReferenceValue = btnU.GetComponent<MobileHoldButton>();
            uiSO.FindProperty("btnDown").objectReferenceValue = btnD.GetComponent<MobileHoldButton>();
            uiSO.FindProperty("btnJump").objectReferenceValue = btnJumpGo.GetComponent<Button>();
            uiSO.FindProperty("btnAttack").objectReferenceValue = btnAttackGo.GetComponent<Button>();
            uiSO.FindProperty("btnBlock").objectReferenceValue = btnBlockGo.GetComponent<Button>();
            uiSO.FindProperty("btnShoot").objectReferenceValue = btnShootGo.GetComponent<Button>();
            uiSO.FindProperty("btnUsePotion").objectReferenceValue = btnPotionGo.GetComponent<Button>();

            uiSO.FindProperty("btnOpenShop").objectReferenceValue = mainMenu.transform.Find("ShopButton").GetComponent<Button>();
            uiSO.FindProperty("btnCloseShop").objectReferenceValue = closeBtn.GetComponent<Button>();
            uiSO.FindProperty("btnUpgradeHP").objectReferenceValue = hpBtn.GetComponent<Button>();
            uiSO.FindProperty("btnUpgradeATK").objectReferenceValue = atkBtn.GetComponent<Button>();
            uiSO.FindProperty("shopGoldText").objectReferenceValue = shopGold.GetComponent<Text>();
            uiSO.FindProperty("hpUpgradeText").objectReferenceValue = hpLabel.GetComponent<Text>();
            uiSO.FindProperty("atkUpgradeText").objectReferenceValue = atkLabel.GetComponent<Text>();

            uiSO.ApplyModifiedProperties();
        }

        private static void SetAnchorAndPos(GameObject go, Vector2 min, Vector2 max, Vector2 pos)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = pos;
        }

        private static GameObject CreateUIElement(string name, GameObject parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject CreatePanel(string name, GameObject parent, Color color)
        {
            GameObject go = CreateUIElement(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.one;

            Image img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static GameObject CreateButton(string name, GameObject parent, string labelText, Color btnColor)
        {
            GameObject go = CreateUIElement(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 60f);

            Image img = go.AddComponent<Image>();
            img.color = btnColor;

            go.AddComponent<Button>();

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            Text t = textGo.AddComponent<Text>();
            t.text = labelText;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 20;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            RectTransform tRect = textGo.GetComponent<RectTransform>() ?? textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.one;

            return go;
        }

        private static GameObject CreateHoldButton(string name, GameObject parent, string labelText, Color btnColor, Vector2 anchoredPos, Vector2 min, Vector2 max)
        {
            GameObject go = CreateUIElement(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(90f, 90f);

            Image img = go.AddComponent<Image>();
            img.color = btnColor;

            go.AddComponent<MobileHoldButton>();

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            Text t = textGo.AddComponent<Text>();
            t.text = labelText;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            RectTransform tRect = textGo.GetComponent<RectTransform>() ?? textGo.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.one;

            return go;
        }

        private static GameObject CreateText(string name, GameObject parent, string initialText, TextAnchor alignment, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, int fontSize, Color color)
        {
            GameObject go = CreateUIElement(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            Text t = go.AddComponent<Text>();
            t.text = initialText;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;

            return go;
        }

        private static GameObject CreateMinimapCell(string name, GameObject parent, Vector2 pos)
        {
            GameObject cell = CreateUIElement(name, parent);
            RectTransform r = cell.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(40f, 30f);
            r.anchoredPosition = pos;
            
            Image img = cell.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Unvisited
            
            GameObject border = CreateUIElement("Border", cell);
            RectTransform bRect = border.GetComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.offsetMin = new Vector2(-2f, -2f);
            bRect.offsetMax = new Vector2(2f, 2f);
            Image bImg = border.AddComponent<Image>();
            bImg.color = Color.black;
            border.transform.SetAsFirstSibling();

            return cell;
        }

        private static int AddLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (layerProp != null && layerProp.stringValue == layerName)
                {
                    return i;
                }
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (layerProp != null && string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }
            return -1;
        }

        // Procedural Sprite sheet builder
        private static void SetupProceduralVisuals()
        {
            // Color Maps
            var playerColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'o', new Color(0.12f, 0.08f, 0.1f) },  // Dark outline
                { 'H', new Color(0.98f, 0.95f, 0.55f) }, // Highlight blonde
                { 'h', new Color(0.9f, 0.8f, 0.25f) },  // Mid-tone yellow
                { 'D', new Color(0.6f, 0.45f, 0.1f) },  // Shadow brown-yellow
                { 'P', new Color(0.98f, 0.85f, 0.75f) }, // Light skin
                { 'p', new Color(0.88f, 0.68f, 0.55f) }, // Shadow skin
                { 'C', new Color(0.9f, 0.68f, 0.25f) },  // Gold highlight
                { 'c', new Color(0.72f, 0.48f, 0.15f) }, // Mid bronze
                { 'a', new Color(0.48f, 0.28f, 0.08f) }, // Shadow bronze
                { 'R', new Color(0.9f, 0.2f, 0.2f) },    // Bright red
                { 'r', new Color(0.7f, 0.1f, 0.1f) },    // Mid red
                { 'd', new Color(0.45f, 0.05f, 0.05f) }, // Dark red
                { 'B', new Color(0.45f, 0.25f, 0.15f) }, // Mid brown
                { 'b', new Color(0.28f, 0.15f, 0.08f) }, // Dark brown
                { 'S', new Color(0.92f, 0.92f, 0.95f) }, // Shiny silver
                { 's', new Color(0.65f, 0.65f, 0.72f) }, // Mid steel
                { 'g', new Color(0.45f, 0.45f, 0.52f) }, // Shadow steel
                { 'Y', new Color(0.98f, 0.95f, 0.55f) }  // Golden accents
            };

            var enemyColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'W', Color.white },
                { 'R', Color.red },
                { 'D', new Color(0.35f, 0.35f, 0.38f) },
                { 'S', new Color(0.8f, 0.1f, 0.1f) }
            };

            var wizardColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'G', new Color(0.1f, 0.5f, 0.25f) },
                { 'P', new Color(0.45f, 0.15f, 0.5f) },
                { 'S', Color.yellow }
            };

            var stoneColors = new Dictionary<char, Color> {
                { 'W', new Color(0.6f, 0.6f, 0.65f) },
                { 'G', new Color(0.35f, 0.35f, 0.38f) },
                { 'D', new Color(0.2f, 0.2f, 0.22f) }
            };

            var ladderColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'B', new Color(0.4f, 0.22f, 0.1f) },
                { 'D', new Color(0.2f, 0.1f, 0.05f) }
            };

            var spikesColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'G', new Color(0.5f, 0.5f, 0.5f) },
                { 'D', new Color(0.25f, 0.25f, 0.25f) }
            };

            var keyColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'Y', Color.yellow },
                { 'O', new Color(0.85f, 0.5f, 0f) }
            };

            var chestColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'B', new Color(0.4f, 0.2f, 0.08f) },
                { 'D', new Color(0.2f, 0.1f, 0.04f) },
                { 'Y', Color.yellow }
            };

            var doorColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'B', new Color(0.3f, 0.18f, 0.05f) },
                { 'D', new Color(0.18f, 0.09f, 0.02f) },
                { 'G', new Color(0.45f, 0.45f, 0.45f) },
                { 'K', Color.black }
            };

            var potionColors = new Dictionary<char, Color> {
                { '.', Color.clear },
                { 'W', Color.white },
                { 'G', Color.green }
            };

            // Player Designs
            string[] playerIdle0 = {
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo...",
                "..oohhHppHhho...",
                "..oohhPohpHhho..",
                "...ohhppphho....",
                "....occcccoo....",
                "...ocCCCCCCco...",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "...odrrrrrrdo...",
                "....orrrrrro....",
                "....orobodo.....",
                "....or.o.r......",
                "....obbbhbo.....",
                "....obbbhbo.....",
                "....obb..bo.....",
                "....obboobo.....",
                "....ooooooo.....",
                "................"
            };
            string[] playerIdle1 = {
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo...",
                "..oohhHppHhho...",
                "..oohhPohpHhho..",
                "...ohhppphho....",
                "....occcccoo....",
                "...ocCCCCCCco...",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "...odrrrrrrdo...",
                "....orrrrrro....",
                "....orobodo.....",
                "....or.o.r......",
                "....obbbhbo.....",
                "....obbbhbo.....",
                "....obb..bo.....",
                "....obb..bo.....",
                "....ooooooo.....",
                "................"
            };
            string[] playerWalk0 = {
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo...",
                "..oohhHppHhho...",
                "..oohhPohpHhho..",
                "...ohhppphho....",
                "....occcccoo....",
                "...ocCCCCCCco...",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "...odrrrrrrdo...",
                "....orrrrrro....",
                "....or...ro.....",
                "....or...ro.....",
                "....obbb.obbo...",
                "....obbb.obbo...",
                "....obb..oooo...",
                "....obbo........",
                "....oooo........",
                "................"
            };
            string[] playerWalk1 = {
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo...",
                "..oohhHppHhho...",
                "..oohhPohpHhho..",
                "...ohhppphho....",
                "....occcccoo....",
                "...ocCCCCCCco...",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "...odrrrrrrdo...",
                "....orrrrrro....",
                "....or...ro.....",
                "....or...ro.....",
                "....obbo.obbb...",
                "....obbo.obbb...",
                "....oooo.obbo...",
                ".........obbo...",
                ".........oooo...",
                "................"
            };
            string[] playerJump = {
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo...",
                "..oohhHppHhho...",
                "..oohhPohpHhho..",
                "...ohhppphho....",
                "....occcccoo....",
                "...ocCCCCCCco...",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "...odrrrrrrdo...",
                "....orrrrrro....",
                "....or....ro....",
                "....or....ro....",
                "....ob....bo....",
                "....ob....bo....",
                "...obb....bbo...",
                "..obbo....oobo..",
                "..oooo....oooo..",
                "................"
            };
            string[] playerAttack = {
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo.ss",
                "..oohhHppHhho.SS",
                "..oohhPohpHhhoSS",
                "...ohhppphhoSSSS",
                "....occcccoo.SS.",
                "...ocCCCCCCco.s.",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "...odrrrrrrdo...",
                "....orrrrrro....",
                "....orobodo.....",
                "....or.o.r......",
                "....obbbhbo.....",
                "....obbbhbo.....",
                "....obb..bo.....",
                "....obboobo.....",
                "....ooooooo.....",
                "................"
            };
            string[] playerHurt = {
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo...",
                "..oohhHppHhho...",
                "..oohhPohpHhho..",
                "...ohhppphho....",
                "....occcccoo....",
                "...ocCCCCCCco...",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "...odrrrrrrdo...",
                "....orrrrrro....",
                "....orobodo.....",
                "....or.o.r......",
                "....obbbhbo.....",
                "....obbbhbo.....",
                "....obb..bo.....",
                "....obboobo.....",
                "....ooooooo.....",
                "................"
            };
            string[] playerDeath = {
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................",
                "......ooo.......",
                "....oohhHho.....",
                "...oohhhHhhoo...",
                "..oohhHppHhho...",
                "..oohhPohpHhho..",
                "...occcccoo.....",
                "..oddccccccddo..",
                "..oddCCCCCCddo..",
                "..oddrrrrrrddo..",
                "...odrrrrrrdo...",
                "....obbbhbo.....",
                "....ooooooo....."
            };

            // Enemy Skeleton Designs
            string[] enemyIdle0 = {
                ".....WWWWW......",
                "....WWWWWWW.....",
                "...WWWRWRWWW....",
                "....WWWWWWW.....",
                ".....WWWWW......",
                "....DWWWWWDD....",
                "...DDWWWWWWDD...",
                "....WWWWWW......",
                ".....WWWW.......",
                "....WW.WW.......",
                "....WW.WW.......",
                "....WW.WW......."
            };
            string[] enemyIdle1 = {
                ".....WWWWW......",
                "....WWWWWWW.....",
                "...WWWRWRWWW....",
                "....WWWWWWW.....",
                ".....WWWWW......",
                "....DWWWWWDD....",
                "...DDWWWWWWDD...",
                "....WWWWWW......",
                ".....WWWW.......",
                "....W...W.......",
                "....W...W.......",
                "....W...W......."
            };
            string[] enemyWalk0 = {
                ".....WWWWW......",
                "....WWWWWWW.....",
                "...WWWRWRWWW....",
                "....WWWWWWW.....",
                ".....WWWWW......",
                "....DWWWWWDD....",
                "...DDWWWWWWDD...",
                "....WWWWWW......",
                ".....WWWW.......",
                "....W...W.......",
                "...W.....W......",
                "..W.......W....."
            };
            string[] enemyWalk1 = {
                ".....WWWWW......",
                "....WWWWWWW.....",
                "...WWWRWRWWW....",
                "....WWWWWWW.....",
                ".....WWWWW......",
                "....DWWWWWDD....",
                "...DDWWWWWWDD...",
                "....WWWWWW......",
                ".....WWWW.......",
                "....WW.WW.......",
                "....WW.WW.......",
                "....WW.WW......."
            };

            // Ranged Wizard Design
            string[] wizardIdle = {
                ".....GGGGG......",
                "....GGGGGGG.....",
                "...GGPPPPPPC....",
                "....GGGGGGG.....",
                ".....GGGGG......",
                "....GGGGGGGSS...",
                "...GGGGGGGGSS...",
                "....GGGGGG......",
                ".....GGGG.......",
                "....GG.GG.......",
                "....GG.GG.......",
                "....GG.GG......."
            };

            // Environment Designs
            string[] spikesDesign = {
                "................",
                "................",
                "....G.....G.....",
                "...GDG...GDG....",
                "..GDDDG.GDDDG...",
                ".GDDDDDGDDDDDG..",
                "GGDDDDDGDDDDDGG.",
                "GGDDDDDGDDDDDGG."
            };

            string[] stoneDesign = {
                "WWWWWWWWWWWWWWWW",
                "WGGGGGGGGGGGGGDW",
                "WGDDDDDDDDDDDDDW",
                "WGDGGGGGGGGGDDDW",
                "WGDGDDDDDDDGDDDW",
                "WGDGDGGGGGGDGDDW",
                "WGDGDGDDDDGDGDDW",
                "WGDGDGDDDDGDGDDW",
                "WGDGDGGGGGGDGDDW",
                "WGDGDDDDDDDGDDDW",
                "WGDGGGGGGGGGDDDW",
                "WGDDDDDDDDDDDDDW",
                "WGGGGGGGGGGGGGDW",
                "WDDDDDDDDDDDDDDW",
                "WWWWWWWWWWWWWWWW"
            };

            string[] ladderDesign = {
                "D.B........B.D..",
                "D.B........B.D..",
                "D.BBBBBBBBBB.D..",
                "D.B........B.D..",
                "D.B........B.D..",
                "D.BBBBBBBBBB.D..",
                "D.B........B.D..",
                "D.B........B.D..",
                "D.BBBBBBBBBB.D..",
                "D.B........B.D..",
                "D.B........B.D..",
                "D.BBBBBBBBBB.D.."
            };

            string[] keyDesign = {
                "................",
                "......YYYY......",
                ".....Y....Y.....",
                ".....Y.OO.Y.....",
                ".....Y.OO.Y.....",
                ".....Y....Y.....",
                "......YYYY......",
                ".......YY.......",
                ".......YY.......",
                ".......YY.Y.....",
                ".......YY.Y.....",
                ".......YYYY.....",
                ".......YY......."
            };

            string[] chestClosed = {
                "....YYYYYYYY....",
                "...YBBBBBBBBY...",
                "..YBBBBBBBBBBY..",
                "..YBDDDDDDDDBY..",
                "..YBDDYYYYDDBY..",
                "..YBDDY..YDDBY..",
                "...YYYYYYYYY....",
                "...YBBBBBBBBY...",
                "..YBBBBBBBBBBY..",
                "..YBDDDDDDDDBY..",
                "..YBDDDDDDDDBY..",
                "...YYYYYYYYY...."
            };

            string[] chestOpen = {
                "...YYYYYYYYY....",
                "..YBBBBBBBBBBY..",
                ".YBBBBBBBBBBBBY.",
                ".YBDDDDDDDDDDBY.",
                "................",
                "....YYYYYYYY....",
                "...YBBBBBBBBY...",
                "..YBBBBBBBBBBY..",
                "..YBDDDDDDDDBY..",
                "..YBDDDDDDDDBY..",
                "...YYYYYYYYY...."
            };

            string[] doorClosed = {
                "GGGGGGGGGGGGGGGG",
                "GBBBBBBBBBBBBBBG",
                "GBDDDDDDDDDDDDDG",
                "GBDGGGGGGGGGDDDG",
                "GBDGBBBBBBBGDDDG",
                "GBDGBDKKKDBGDDDG",
                "GBDGBDKKKDBGDDDG",
                "GBDGBBBBBBBGDDDG",
                "GBDGGGGGGGGGDDDG",
                "GBDDDDDDDDDDDDDG",
                "GBBBBBBBBBBBBBBG",
                "GGGGGGGGGGGGGGGG"
            };

            string[] coinDesign = {
                "....YYYY....",
                "..YYYYYYYY..",
                ".YYYYYYYYYY.",
                ".YYYOOYYYYY.",
                "YYYYOOYYYYYY",
                "YYYYOOYYYYYY",
                "YYYYOOYYYYYY",
                "YYYYOOYYYYYY",
                ".YYYYYYYYYY.",
                "..YYYYYYYY..",
                "....YYYY...."
            };

            string[] potionDesign = {
                ".....WW.....",
                ".....WW.....",
                "....GGGG....",
                "...GGGGGG...",
                "..GGGGGGGG..",
                "..GGGGGGGG..",
                "...GGGGGG...",
                "....GGGG...."
            };

            // Write all sprites programmatically
            CreateProceduralSprite("Player_Idle_0.png", playerIdle0, playerColors);
            CreateProceduralSprite("Player_Idle_1.png", playerIdle1, playerColors);
            CreateProceduralSprite("Player_Walk_0.png", playerWalk0, playerColors);
            CreateProceduralSprite("Player_Walk_1.png", playerWalk1, playerColors);
            CreateProceduralSprite("Player_Jump.png", playerJump, playerColors);
            CreateProceduralSprite("Player_Attack.png", playerAttack, playerColors);
            CreateProceduralSprite("Player_Hurt.png", playerHurt, playerColors);
            CreateProceduralSprite("Player_Death.png", playerDeath, playerColors);

            CreateProceduralSprite("Enemy_Idle_0.png", enemyIdle0, enemyColors);
            CreateProceduralSprite("Enemy_Idle_1.png", enemyIdle1, enemyColors);
            CreateProceduralSprite("Enemy_Walk_0.png", enemyWalk0, enemyColors);
            CreateProceduralSprite("Enemy_Walk_1.png", enemyWalk1, enemyColors);

            CreateProceduralSprite("RangedEnemy_Idle.png", wizardIdle, wizardColors);

            CreateProceduralSprite("StoneBlock.png", stoneDesign, stoneColors);
            CreateProceduralSprite("LadderStep.png", ladderDesign, ladderColors);
            CreateProceduralSprite("Spikes.png", spikesDesign, spikesColors);
            CreateProceduralSprite("Coin.png", coinDesign, keyColors);
            CreateProceduralSprite("Potion.png", potionDesign, potionColors);
            CreateProceduralSprite("Chest_Closed.png", chestClosed, chestColors);
            CreateProceduralSprite("Chest_Open.png", chestOpen, chestColors);
            CreateProceduralSprite("Door_Closed.png", doorClosed, doorColors);
            CreateProceduralSprite("Key.png", keyDesign, keyColors);
        }

        private static Sprite CreateProceduralSprite(string filename, string[] design, Dictionary<char, Color> colorMap)
        {
            string path = Path.Combine(SpritePath, filename);
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            int height = design.Length;
            int width = design[0].Length;

            Texture2D tex = new Texture2D(width, height);
            tex.filterMode = FilterMode.Point;

            for (int y = 0; y < height; y++)
            {
                string row = design[height - 1 - y];
                for (int x = 0; x < width; x++)
                {
                    char c = '.';
                    if (x < row.Length)
                    {
                        c = row[x];
                    }
                    Color col = colorMap.ContainsKey(c) ? colorMap[c] : Color.clear;
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
