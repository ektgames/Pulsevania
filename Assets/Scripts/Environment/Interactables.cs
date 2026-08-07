using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    // --- LADDER CLIMB INTERACTABLE ---
    [RequireComponent(typeof(BoxCollider2D))]
    public class Ladder : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.SetCanClimb(true, transform.position.x);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.SetCanClimb(false);
            }
        }
    }

    // --- BREAKABLE JAR / BOX ---
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class BreakableJar : MonoBehaviour
    {
        [Header("Loot Drop Configuration")]
        [SerializeField] private int minGold = 5;
        [SerializeField] private int maxGold = 15;
        [SerializeField] [Range(0f, 1f)] private float keyDropChance = 0.1f;
        [SerializeField] [Range(0f, 1f)] private float potionDropChance = 0.2f;

        [Header("Visual Effects")]
        [SerializeField] private GameObject breakParticlePrefab;

        private HealthSystem healthSystem;

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
        }

        private void Start()
        {
            healthSystem.OnDeath += Break;
        }

        private void OnDestroy()
        {
            if (healthSystem != null)
            {
                healthSystem.OnDeath -= Break;
            }
        }

        private void Break()
        {
            // Spawn break particles if assigned
            if (breakParticlePrefab != null)
            {
                Instantiate(breakParticlePrefab, transform.position, Quaternion.identity);
            }

            // Distribute loot
            int goldReward = Random.Range(minGold, maxGold + 1);
            LootPickup.SpawnPhysicalLoot(transform.position, LootPickup.LootType.Gold, goldReward);

            float roll = Random.value;
            if (roll < keyDropChance)
            {
                LootPickup.SpawnPhysicalLoot(transform.position + Vector3.up * 0.5f, LootPickup.LootType.Key, 1);
            }
            else if (roll < keyDropChance + potionDropChance)
            {
                LootPickup.SpawnPhysicalLoot(transform.position + Vector3.up * 0.5f, LootPickup.LootType.Potion, 1);
            }

            // Disable renderer and collider, destroy object after short delay
            GetComponent<Collider2D>().enabled = false;
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.enabled = false;

            Destroy(gameObject, 0.5f);
        }
    }

    // --- LOCKED DOOR ---
    [RequireComponent(typeof(BoxCollider2D))]
    public class LockedDoor : MonoBehaviour
    {
        [Header("Door Settings")]
        [SerializeField] private bool requiresKey = true;
        [SerializeField] private bool completesLevelOnOpen = true;

        private bool isOpened = false;

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (isOpened) return;

            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                TryOpenDoor();
            }
        }

        private void TryOpenDoor()
        {
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            if (requiresKey)
            {
                if (GameManager.Instance.UseKey())
                {
                    OpenDoor();
                }
                else
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Anahtar Gerekiyor!" : "Requires Key!", Color.red);
                }
            }
            else
            {
                OpenDoor();
            }
        }

        private void OpenDoor()
        {
            isOpened = true;
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Kapı Açıldı!" : "Door Unlocked!", Color.green);

            // Deactivate or trigger unlock animation/effect
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                // Semi-transparent opened representation
                sr.color = new Color(1f, 1f, 1f, 0.3f);
            }

            if (completesLevelOnOpen)
            {
                StartCoroutine(CompleteLevelDelay());
            }
        }

        private IEnumerator CompleteLevelDelay()
        {
            yield return new WaitForSeconds(1f);
            GameManager.Instance.CompleteLevel();
        }
    }

    // --- LOOT CHEST ---
    [RequireComponent(typeof(BoxCollider2D))]
    public class LootChest : MonoBehaviour
    {
        [Header("Chest Settings")]
        [SerializeField] private bool requiresKey = false;
        [SerializeField] private int minGoldReward = 20;
        [SerializeField] private int maxGoldReward = 50;
        [SerializeField] private int potionRewardCount = 1;
        [SerializeField] private int keyRewardCount = 1;
        [SerializeField] private Sprite openSprite;

        private bool isOpen = false;

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = false;
        }

        private void Start()
        {
            HealthSystem hs = GetComponent<HealthSystem>();
            if (hs != null)
            {
                Debug.Log($"[Pulsevania] LootChest {name} subscribing to HealthSystem.OnDeath.");
                hs.OnDeath += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            HealthSystem hs = GetComponent<HealthSystem>();
            if (hs != null)
            {
                hs.OnDeath -= HandleDeath;
            }
        }

        private void HandleDeath()
        {
            Debug.Log($"[Pulsevania] LootChest {name} OnDeath triggered.");
            TryOpenChest();
        }

        public void TakeDamage(int damage)
        {
            Debug.Log("[Pulsevania Emergency] Chest TakeDamage triggered successfully!");
            TryOpenChest();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isOpen) return;

            PlayerController player = collision.collider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                TryOpenChest();
            }
        }

        public void TryOpenChest()
        {
            if (isOpen)
            {
                Debug.Log($"[Pulsevania] LootChest {name} is already open, ignoring TryOpen.");
                return;
            }

            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            if (requiresKey)
            {
                if (GameManager.Instance.UseKey())
                {
                    OpenChest();
                }
                else
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Anahtar Gerekiyor!" : "Requires Key!", Color.red);
                }
            }
            else
            {
                OpenChest();
            }
        }

        private void OpenChest()
        {
            isOpen = true;
            Debug.Log($"[Pulsevania] LootChest {name} opening. Spawning hardcoded visual drops...");

            // Play sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
            }

            // Spawn floating texts safely
            if (DamageTextPool.Instance != null)
            {
                bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Sandık Açıldı!" : "Chest Opened!", Color.green);
            }

            Vector3 spawnPos = new Vector3(transform.position.x, transform.position.y + 0.5f, 0f);

            // Calculate coins to spawn (1 coin per 10 Gold)
            int goldAmount = Random.Range(minGoldReward, maxGoldReward + 1);
            int coinCount = Mathf.Clamp(goldAmount / 10, 3, 5);
            int valPerCoin = goldAmount / coinCount;

            for (int i = 0; i < coinCount; i++)
            {
                SpawnHardcodedLoot("GoldCoin_Drop", Color.yellow, LootPickup.LootType.Gold, valPerCoin, spawnPos);
            }

            // Spawn Potions
            for (int i = 0; i < potionRewardCount; i++)
            {
                SpawnHardcodedLoot("Potion_Drop", Color.green, LootPickup.LootType.Potion, 1, spawnPos);
            }

            // Spawn Keys
            for (int i = 0; i < keyRewardCount; i++)
            {
                SpawnHardcodedLoot("Key_Drop", Color.cyan, LootPickup.LootType.Key, 1, spawnPos);
            }

            // Instantly delete chest from the scene
            Destroy(gameObject);
        }

        private void SpawnHardcodedLoot(string lootName, Color color, LootPickup.LootType type, int amount, Vector3 startPos)
        {
            // Guaranteed Primitive-based visual instantiation
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = lootName;
            item.transform.position = startPos;
            item.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            // Clean 3D physics components
            var c3d = item.GetComponent<Collider>();
            if (c3d != null) Destroy(c3d);

            var rb3d = item.GetComponent<Rigidbody>();
            if (rb3d != null) Destroy(rb3d);

            // Add 2D physics
            BoxCollider2D col = item.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            SpriteRenderer sr = item.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 100; // Absolute foreground

            // Generate bright programmatic texture
            Texture2D tex = new Texture2D(16, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (x > 1 && x < 14 && y > 1 && y < 14)
                        tex.SetPixel(x, y, color);
                    else
                        tex.SetPixel(x, y, Color.black); // cute outline
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);

            // Attach pickup behavior and trigger launch internal coroutine (safe from chest destruction)
            LootPickup pickup = item.AddComponent<LootPickup>();
            Vector2 launchForce = new Vector2(Random.Range(-2f, 2f), Random.Range(3f, 5f));
            pickup.Initialize(type, amount, launchForce);

            Debug.Log("[Pulsevania Emergency] SUCCESS: Instantiated hardcoded item: " + lootName + " at " + item.transform.position);
        }
    }

    public class KeyChest : MonoBehaviour
    {
        private int hits = 0;
        private bool isBroken = false;
        private SpriteRenderer spriteRenderer;

        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            // Smoothly restore scale back to baseline 1.0f
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(1f, 1f, 1f), Time.deltaTime * 10f);
        }

        public void TakeDamage(int damage)
        {
            if (isBroken) return;
            hits++;
            
            // Squash and stretch response
            transform.localScale = new Vector3(1.3f, 0.6f, 1f);

            // Play damage feedback sfx
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.DamageTaken);
            }

            if (DamageTextPool.Instance != null)
            {
                bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                string msg = isTR ? $"{5 - hits} vuruş kaldı!" : $"{5 - hits} hits left!";
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, msg, Color.magenta);
            }

            if (hits >= 5)
            {
                isBroken = true;
                SpawnKeyAndDestroy();
            }
        }

        private void SpawnKeyAndDestroy()
        {
            if (MapManager.Instance != null)
            {
                MapManager.Instance.ClearRoom(MapManager.Instance.GetCurrentRoomId());
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
            }

            // Spawn physics debris purple shards!
            Color themeColor = new Color(0.7f, 0f, 1f);
            for (int i = 0; i < 8; i++)
            {
                GameObject shard = new GameObject("ChestShard");
                shard.transform.position = transform.position;
                shard.transform.localScale = new Vector3(0.12f, 0.12f, 1f);
                
                SpriteRenderer ssr = shard.AddComponent<SpriteRenderer>();
                Texture2D stex = new Texture2D(4, 4);
                Color shardColor = Random.value < 0.5f ? themeColor : new Color(0.4f, 0.2f, 0.1f);
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                        stex.SetPixel(sx, sy, shardColor);
                stex.filterMode = FilterMode.Point;
                stex.Apply();
                ssr.sprite = Sprite.Create(stex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                ssr.sortingOrder = 95;

                Rigidbody2D srb = shard.AddComponent<Rigidbody2D>();
                srb.linearVelocity = new Vector2(Random.Range(-5f, 5f), Random.Range(5f, 9f));
                srb.angularVelocity = Random.Range(-500f, 500f);
                
                shard.AddComponent<BoxCollider2D>();
                Destroy(shard, 1.2f);
            }

            GameObject keyGo = new GameObject("Room_Key");
            keyGo.transform.position = transform.position + Vector3.up * 0.5f;
            keyGo.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            
            BoxCollider2D col = keyGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 0.9f);
            
            SpriteRenderer sr = keyGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 100;
            
            Texture2D tex = new Texture2D(16, 16);
            string[] keyLayout = {
                "................",
                "................",
                "......WWWW......",
                "....WWKKKKWW....",
                "...WKKHHHHKKW...",
                "...WKKH..HKKW...",
                "...WKKHHHHKKWWWW",
                "....WWKKKKWWKKKW",
                "......WWWW..W.W.",
                "............W.W.",
                "................",
                "................",
                "................",
                "................",
                "................",
                "................"
            };
            Color border = new Color(0.3f, 0f, 0.4f);
            Color neon = new Color(0.8f, 0.1f, 1f);
            Color highlight = new Color(1f, 0.6f, 1f);

            for (int y = 0; y < 16; y++)
            {
                int row = 15 - y;
                for (int x = 0; x < 16; x++)
                {
                    Color colVal = Color.clear;
                    char c = keyLayout[row][x];
                    if (c == 'W') colVal = border;
                    else if (c == 'K') colVal = neon;
                    else if (c == 'H') colVal = highlight;
                    tex.SetPixel(x, y, colVal);
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            
            keyGo.AddComponent<RoomKeyPickup>();
            keyGo.AddComponent<GlowingKeyEffect>();
            
            // Fade out chest before destruction
            StartCoroutine(FadeOutAndDestroy());
        }

        private System.Collections.IEnumerator FadeOutAndDestroy()
        {
            // Deactivate collider immediately
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            float duration = 0.5f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    spriteRenderer.color = c;
                }
                yield return null;
            }

            Destroy(gameObject);
        }
    }

    public class RoomKeyPickup : MonoBehaviour
    {
        private bool collected = false;

        private void Update()
        {
            if (collected) return;
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist <= 6.0f) // Magnet range: 6 units
                {
                    Vector3 targetPos = player.transform.position + Vector3.up * 0.5f;
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, Time.deltaTime * 12f);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collected) return;
            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                collected = true;
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetKeys(GameManager.Instance.CurrentKeys + 1);
                }
                if (DamageTextPool.Instance != null)
                {
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    DamageTextPool.Instance.SpawnText(transform.position, isTR ? "Bölüm Anahtarı Toplandı!" : "Room Key Picked Up!", Color.green);
                }
                Destroy(gameObject);
            }
        }
    }

    public class RoomExitDoor : MonoBehaviour
    {
        private bool isPlayerNear = false;

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        private GameObject activePromptGo;

        private void ShowPrompt(string message)
        {
            if (activePromptGo != null) return;

            activePromptGo = new GameObject("InteractionPrompt");
            activePromptGo.transform.SetParent(transform);
            activePromptGo.transform.localPosition = new Vector3(0f, 1.4f, -0.2f);

            // 1. Drop shadow text (Black)
            GameObject shadowGo = new GameObject("ShadowText");
            shadowGo.transform.SetParent(activePromptGo.transform);
            shadowGo.transform.localPosition = new Vector3(0.02f, -0.02f, 0.01f);
            var tmShadow = shadowGo.AddComponent<TextMesh>();
            tmShadow.anchor = TextAnchor.MiddleCenter;
            tmShadow.alignment = TextAlignment.Center;
            tmShadow.fontSize = 32;
            tmShadow.characterSize = 0.08f;
            tmShadow.fontStyle = FontStyle.Bold;
            tmShadow.color = Color.black;
            tmShadow.text = message;

            var mrShadow = shadowGo.GetComponent<MeshRenderer>();
            if (mrShadow != null)
            {
                mrShadow.sortingOrder = 150;
            }

            // 2. Main text (Golden Yellow)
            GameObject mainGo = new GameObject("MainText");
            mainGo.transform.SetParent(activePromptGo.transform);
            mainGo.transform.localPosition = Vector3.zero;
            var tmMain = mainGo.AddComponent<TextMesh>();
            tmMain.anchor = TextAnchor.MiddleCenter;
            tmMain.alignment = TextAlignment.Center;
            tmMain.fontSize = 32;
            tmMain.characterSize = 0.08f;
            tmMain.fontStyle = FontStyle.Bold;
            tmMain.color = new Color(1f, 0.9f, 0.1f, 1f);
            tmMain.text = message;

            var mrMain = mainGo.GetComponent<MeshRenderer>();
            if (mrMain != null)
            {
                mrMain.sortingOrder = 150;
            }
        }

        private void HidePrompt()
        {
            if (activePromptGo != null)
            {
                Destroy(activePromptGo);
                activePromptGo = null;
            }
        }

        private void OnDisable()
        {
            HidePrompt();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            PlayerController pc = collision.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                isPlayerNear = true;
                int currentRoomId = MapManager.Instance != null ? MapManager.Instance.GetCurrentRoomId() : 1;
                bool isCleared = MapManager.Instance != null && MapManager.Instance.rooms[currentRoomId - 1].state == RoomState.Cleared;
                
                string lang = PlayerPrefs.GetString("GameLanguage", "Turkish");
                bool isTR = lang == "Turkish";
                string msg = "";
                if (isCleared || currentRoomId == 1)
                {
                    msg = isTR ? "Çıkmak için Kapıya Dokunun" : "Tap Door to Exit";
                }
                else
                {
                    msg = isTR ? "Kilidi Açmak için Kapıya Dokunun" : "Tap Door to Unlock";
                }
                ShowPrompt(msg);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            PlayerController pc = collision.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                isPlayerNear = false;
                HidePrompt();
            }
        }

        private void OnMouseDown()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (UIManager.Instance != null && UIManager.Instance.IsWorldMapOpen())
            {
                return;
            }

            TryTransition();
        }

        private void Update()
        {
            bool ePressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                ePressed = UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
            }

            if (isPlayerNear && ePressed)
            {
                TryTransition();
            }
        }

        private bool isTransitioning = false;

        public void TryTransition()
        {
            if (isTransitioning) return;

            EnemyGuardian activeBoss = FindFirstObjectByType<EnemyGuardian>();
            if (activeBoss != null && activeBoss.behavior == EnemyGuardian.MonsterBehavior.Boss)
            {
                if (DamageTextPool.Instance != null)
                {
                    string lang = PlayerPrefs.GetString("GameLanguage", "Turkish");
                    string msg = lang == "English" ? "Defeat Boss to Unlock!" : "Kilidi açmak için Patronu yenin!";
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, msg, Color.red);
                }
                return;
            }

            int currentRoomId = MapManager.Instance != null ? MapManager.Instance.GetCurrentRoomId() : 1;
            
            bool doorUnlocked = false;
            if (MapManager.Instance != null)
            {
                doorUnlocked = MapManager.Instance.rooms[currentRoomId - 1].exitDoorUnlocked;
            }

            if (doorUnlocked)
            {
                StartCoroutine(OpenAndTransition(currentRoomId));
            }
            else
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentKeys > 0)
                {
                    // Consume exactly 1 key instead of resetting to 0
                    GameManager.Instance.SetKeys(GameManager.Instance.CurrentKeys - 1);

                    // Unlock the door persistently
                    if (MapManager.Instance != null)
                    {
                        MapManager.Instance.rooms[currentRoomId - 1].exitDoorUnlocked = true;
                        MapManager.Instance.ClearRoom(currentRoomId);
                    }

                    StartCoroutine(OpenAndTransition(currentRoomId));
                }
                else
                {
                    if (DamageTextPool.Instance != null)
                    {
                        bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                        DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Bölüm Anahtarı Gerekiyor!" : "Requires Room Key!", Color.red);
                    }
                }
            }
        }

        private IEnumerator OpenAndTransition(int currentRoomId)
        {
            isTransitioning = true;

            GameObject player = GameObject.FindWithTag("Player");
            PlayerController pc = null;
            if (player != null)
            {
                pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.SetControlsLocked(true);
                    var rb = pc.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                        rb.isKinematic = true;
                    }
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup); // Play unlock chime
            }

            float duration = 0.4f;
            float elapsed = 0f;

            // Door animation values (scale X towards zero for 3D door rotation/opening look)
            Vector3 startDoorScale = transform.localScale;
            Vector3 targetDoorScale = new Vector3(0.02f, startDoorScale.y, startDoorScale.z);
            SpriteRenderer doorSr = GetComponent<SpriteRenderer>();
            Color startDoorColor = doorSr != null ? doorSr.color : Color.white;
            Color targetDoorColor = new Color(0.12f, 0.08f, 0.06f, 0.9f); // Dim color inside depth

            // Player walk-in / shrink values
            Vector3 startPlayerPos = player != null ? player.transform.position : Vector3.zero;
            Vector3 targetPlayerPos = player != null ? new Vector3(transform.position.x, startPlayerPos.y, startPlayerPos.z) : Vector3.zero;
            Vector3 startPlayerScale = player != null ? player.transform.localScale : Vector3.one;
            Vector3 targetPlayerScale = player != null ? new Vector3(0.1f, 0.1f, 1f) : Vector3.one;

            SpriteRenderer[] playerSrs = player != null ? player.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
            System.Collections.Generic.Dictionary<SpriteRenderer, Color> playerStartColors = new System.Collections.Generic.Dictionary<SpriteRenderer, Color>();
            foreach (var psr in playerSrs)
            {
                if (psr != null) playerStartColors[psr] = psr.color;
            }

            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 1. Perspective 3D door opening
                transform.localScale = Vector3.Lerp(startDoorScale, targetDoorScale, t);
                if (doorSr != null)
                {
                    doorSr.color = Color.Lerp(startDoorColor, targetDoorColor, t);
                }

                // 2. Player moves inside door and shrinks into depth (Fade out)
                if (player != null)
                {
                    player.transform.position = Vector3.Lerp(startPlayerPos, targetPlayerPos, t);
                    player.transform.localScale = Vector3.Lerp(startPlayerScale, targetPlayerScale, t);

                    foreach (var psr in playerSrs)
                    {
                        if (psr != null && playerStartColors.TryGetValue(psr, out Color startCol))
                        {
                            psr.color = Color.Lerp(startCol, new Color(startCol.r, startCol.g, startCol.b, 0f), t);
                        }
                    }
                }

                yield return null;
            }

            // Restore player references for next scene
            if (player != null)
            {
                player.transform.localScale = startPlayerScale;
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null) rb.isKinematic = false;

                foreach (var psr in playerSrs)
                {
                    if (psr != null && playerStartColors.TryGetValue(psr, out Color startCol))
                    {
                        psr.color = startCol;
                    }
                }
            }

            int nextRoomId = currentRoomId + 1;
            if (nextRoomId > 50) nextRoomId = 1;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.TriggerRoomTransition(nextRoomId);
            }
            
            isTransitioning = false;
        }
    }

    public class RoomEntryDoor : MonoBehaviour
    {
        private bool isPlayerNear = false;

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        private GameObject activePromptGo;

        private void ShowPrompt(string message)
        {
            if (activePromptGo != null) return;

            activePromptGo = new GameObject("InteractionPrompt");
            activePromptGo.transform.SetParent(transform);
            activePromptGo.transform.localPosition = new Vector3(0f, 1.4f, -0.2f);

            // 1. Drop shadow text (Black)
            GameObject shadowGo = new GameObject("ShadowText");
            shadowGo.transform.SetParent(activePromptGo.transform);
            shadowGo.transform.localPosition = new Vector3(0.02f, -0.02f, 0.01f);
            var tmShadow = shadowGo.AddComponent<TextMesh>();
            tmShadow.anchor = TextAnchor.MiddleCenter;
            tmShadow.alignment = TextAlignment.Center;
            tmShadow.fontSize = 32;
            tmShadow.characterSize = 0.08f;
            tmShadow.fontStyle = FontStyle.Bold;
            tmShadow.color = Color.black;
            tmShadow.text = message;

            var mrShadow = shadowGo.GetComponent<MeshRenderer>();
            if (mrShadow != null)
            {
                mrShadow.sortingOrder = 150;
            }

            // 2. Main text (Golden Yellow)
            GameObject mainGo = new GameObject("MainText");
            mainGo.transform.SetParent(activePromptGo.transform);
            mainGo.transform.localPosition = Vector3.zero;
            var tmMain = mainGo.AddComponent<TextMesh>();
            tmMain.anchor = TextAnchor.MiddleCenter;
            tmMain.alignment = TextAlignment.Center;
            tmMain.fontSize = 32;
            tmMain.characterSize = 0.08f;
            tmMain.fontStyle = FontStyle.Bold;
            tmMain.color = new Color(1f, 0.9f, 0.1f, 1f);
            tmMain.text = message;

            var mrMain = mainGo.GetComponent<MeshRenderer>();
            if (mrMain != null)
            {
                mrMain.sortingOrder = 150;
            }
        }

        private void HidePrompt()
        {
            if (activePromptGo != null)
            {
                Destroy(activePromptGo);
                activePromptGo = null;
            }
        }

        private void OnDisable()
        {
            HidePrompt();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            PlayerController pc = collision.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                isPlayerNear = true;
                string lang = PlayerPrefs.GetString("GameLanguage", "Turkish");
                string msg = lang == "English" ? "Tap Door to Return" : "Geri dönmek için Kapıya Dokunun";
                ShowPrompt(msg);
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            PlayerController pc = collision.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                isPlayerNear = false;
                HidePrompt();
            }
        }

        private void OnMouseDown()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (UIManager.Instance != null && UIManager.Instance.IsWorldMapOpen())
            {
                return;
            }

            TryTransitionBack();
        }

        private void Update()
        {
            bool ePressed = false;
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                ePressed = UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
            }

            if (isPlayerNear && ePressed)
            {
                TryTransitionBack();
            }
        }

        private void TryTransitionBack()
        {
            if (MapManager.Instance != null)
            {
                int currentId = MapManager.Instance.GetCurrentRoomId();
                int prevRoomId = currentId - 1;
                if (prevRoomId >= 1)
                {
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.TriggerRoomTransition(prevRoomId);
                    }
                }
            }
        }
    }

    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class BreakableWall : MonoBehaviour
    {
        private HealthSystem healthSystem;

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
            GetComponent<Damageable>().Team = Team.Enemy; // So player attacks can damage it
        }

        private void Start()
        {
            healthSystem.OnDeath += Break;
        }

        private void OnDestroy()
        {
            if (healthSystem != null)
            {
                healthSystem.OnDeath -= Break;
            }
        }

        private void Break()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.DamageTaken);
            }

            if (DamageTextPool.Instance != null)
            {
                DamageTextPool.Instance.SpawnText(transform.position, "CRUMBLED!", Color.yellow);
            }

            GetComponent<Collider2D>().enabled = false;
            
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;

            Destroy(gameObject, 0.2f);
        }
    }

    public class WallShooterTrap : MonoBehaviour
    {
        public Vector2 shootDirection = Vector2.left;
        public float shootCooldown = 2.5f;
        public int damage = 8;
        private float cooldownTimer;

        private void Start()
        {
            cooldownTimer = Random.Range(0.2f, shootCooldown);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay)
                return;

            // Target Layer Mask includes Player and Ground walls
            int layerMask = LayerMask.GetMask("Player", "Ground");
            
            // Perform 15 units line-of-sight raycast detection
            RaycastHit2D hit = Physics2D.Raycast(transform.position, shootDirection, 15f, layerMask);
            bool canSeePlayer = false;

            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                canSeePlayer = true;
            }

            if (!canSeePlayer)
            {
                // Reset cooldown slightly so it reacts fast when player is spotted
                if (cooldownTimer < 0.2f) cooldownTimer = 0.2f;
                return;
            }

            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                cooldownTimer = shootCooldown;
                Shoot();
            }
        }

        private void Shoot()
        {
            if (ProjectilePool.Instance != null)
            {
                ProjectilePool.Instance.SpawnProjectile(transform.position + (Vector3)(shootDirection * 0.6f), shootDirection, Team.Enemy, damage);
                if (AudioManager.Instance != null)
                {
                    // Reuse SwordSwing sound effect as whoosh for trap daggers
                    AudioManager.Instance.PlaySFX(SoundEffect.SwordSwing);
                }
            }
        }
    }

    public class GlowingKeyEffect : MonoBehaviour
    {
        private Vector3 startPos;
        private float randomOffset;

        private void Start()
        {
            startPos = transform.position;
            randomOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist <= 6.0f) // Magnet is active
                {
                    // Allow magnet to control the position by tracking the moved transform
                    startPos = transform.position;
                    
                    // Pulse scale only
                    float s = 0.5f + Mathf.PingPong(Time.time * 0.8f, 0.1f);
                    transform.localScale = new Vector3(s, s, 1f);
                    return;
                }
            }

            // Hover up and down
            float newY = startPos.y + Mathf.Sin(Time.time * 3f + randomOffset) * 0.15f;
            transform.position = new Vector3(startPos.x, newY, startPos.z);

            // Pulse scale
            float scale = 0.5f + Mathf.PingPong(Time.time * 0.8f, 0.1f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
