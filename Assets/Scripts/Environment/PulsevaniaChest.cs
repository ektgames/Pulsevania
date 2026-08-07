using UnityEngine;

public class PulsevaniaChest : MonoBehaviour 
{
    public enum ChestType
    {
        Yellow, // Gold only
        Purple, // Key only
        Blue    // Equipment item only
    }

    [SerializeField] public ChestType chestType = ChestType.Yellow;
    [SerializeField] private int maxHealth = 3;
    
    private int currentHealth;
    private bool isBroken = false;
    private SpriteRenderer spriteRenderer;

    private void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        // Set baseline scale to 1f, 1f, 1f (restore the old large size!)
        transform.localScale = new Vector3(1f, 1f, 1f);

        Color themeColor = GetThemeColor();
        spriteRenderer.sprite = CreateChestSprite(themeColor, false);
    }

    private void Update()
    {
        // Smoothly interpolate squash/stretch hit response back to 1f, 1f, 1f
        transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(1f, 1f, 1f), Time.deltaTime * 10f);
    }

    public void TakeDamage(int damage) 
    {
        if (isBroken) return;

        currentHealth -= damage;

        // Visual squash & stretch hit response relative to 1f baseline scale
        transform.localScale = new Vector3(1.3f, 0.6f, 1f);

        // Play damage taken sound for hit feedback
        if (Pulsevania.Core.AudioManager.Instance != null)
        {
            Pulsevania.Core.AudioManager.Instance.PlaySFX(Pulsevania.Core.SoundEffect.DamageTaken);
        }

        if (currentHealth <= 0)
        {
            isBroken = true;
            OpenChestAndDropLoot();
        }
    }

    private Color GetThemeColor()
    {
        switch (chestType)
        {
            case ChestType.Yellow: return Color.yellow;
            case ChestType.Purple: return new Color(0.7f, 0f, 1f); // Rich Purple/Magenta
            case ChestType.Blue: return new Color(0f, 0.5f, 1f);   // Bright Neon Blue
            default: return Color.yellow;
        }
    }

    private Sprite CreateChestSprite(Color themeColor, bool isOpenFrame)
    {
        Texture2D tex = new Texture2D(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                // Border outline
                bool isOutline = (x == 0 || x == 15 || y == 0 || y == 15);
                
                // Lid band accent rows
                bool isBand = (x == 3 || x == 4 || x == 11 || x == 12);
                bool isLock = (x >= 7 && x <= 8 && y >= 6 && y <= 8);
                bool isLidSeparation = (y == 9);

                if (isOutline)
                {
                    tex.SetPixel(x, y, new Color(0.1f, 0.05f, 0.02f, 1f));
                }
                else if (isLock)
                {
                    tex.SetPixel(x, y, isOpenFrame ? Color.clear : Color.gray);
                }
                else if (isBand)
                {
                    tex.SetPixel(x, y, themeColor);
                }
                else if (isLidSeparation)
                {
                    tex.SetPixel(x, y, Color.black);
                }
                else
                {
                    // Shaded Wood Body
                    float shade = 0.35f + (y / 24f);
                    tex.SetPixel(x, y, new Color(0.45f * shade, 0.25f * shade, 0.15f * shade, 1f));
                }
            }
        }
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
    }

    private void OpenChestAndDropLoot()
    {
        // Deactivate collider
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Color themeColor = GetThemeColor();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = CreateChestSprite(themeColor, true);
        }

        // Play open coin sfx
        if (Pulsevania.Core.AudioManager.Instance != null)
        {
            Pulsevania.Core.AudioManager.Instance.PlaySFX(Pulsevania.Core.SoundEffect.CoinPickup);
        }

        // Spawn floating text
        if (Pulsevania.Core.DamageTextPool.Instance != null)
        {
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            string msg = "";
            if (chestType == ChestType.Yellow)
            {
                msg = isTR ? "Altın Sandığı!" : "Gold Chest!";
            }
            else if (chestType == ChestType.Purple)
            {
                msg = isTR ? "Anahtar Sandığı!" : "Key Chest!";
            }
            else
            {
                msg = isTR ? "Ekipman Sandığı!" : "Item Chest!";
            }
            Pulsevania.Core.DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, msg, themeColor);
        }

        // Spawn physics debris wood shards!
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

        // Drop contents
        Vector3 spawnPos = new Vector3(transform.position.x, transform.position.y + 0.5f, 0f);
        int level = 1;
        if (Pulsevania.Core.MapManager.Instance != null)
        {
            level = Pulsevania.Core.MapManager.Instance.GetCurrentRoomId();
        }

        if (chestType == ChestType.Yellow)
        {
            // Yellow drops Gold only (Level dependent)
            int goldVal = 10 * level + Random.Range(5, 15) * level;
            int coinCount = Random.Range(3, 6);
            int valPerCoin = goldVal / coinCount;

            for (int i = 0; i < coinCount; i++)
            {
                Pulsevania.Core.LootPickup.SpawnPhysicalLoot(spawnPos, Pulsevania.Core.LootPickup.LootType.Gold, valPerCoin);
            }
        }
        else if (chestType == ChestType.Purple)
        {
            // Purple drops Collectible Key only
            Pulsevania.Core.LootPickup.SpawnPhysicalLoot(spawnPos, Pulsevania.Core.LootPickup.LootType.Key, 1);
        }
        else if (chestType == ChestType.Blue)
        {
            // Blue drops level-based equipment item only
            if (Pulsevania.Core.InventoryManager.Instance != null && Pulsevania.Core.InventoryManager.Instance.itemDatabase.Count > 0)
            {
                var db = Pulsevania.Core.InventoryManager.Instance.itemDatabase;
                System.Collections.Generic.List<Pulsevania.Core.ItemData> filteredDb;
                if (level <= 10)
                {
                    filteredDb = db.FindAll(x => x.itemName.Contains("Bronze"));
                }
                else if (level <= 30)
                {
                    filteredDb = db.FindAll(x => x.itemName.Contains("Silver") || x.itemName == "Throwing Knife");
                }
                else
                {
                    filteredDb = db.FindAll(x => x.itemName.Contains("Gold") || x.itemName == "Masterwork Throwing Knife");
                }

                if (filteredDb.Count == 0) filteredDb = db;

                Pulsevania.Core.ItemData randomItem = filteredDb[Random.Range(0, filteredDb.Count)];
                SpawnEquipmentDrop(randomItem, spawnPos);
            }
        }

        // Fade out and destroy
        StartCoroutine(FadeOutAndDestroy());
    }

    private System.Collections.IEnumerator FadeOutAndDestroy()
    {
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

    private void SpawnGoldCoin(Vector3 spawnPos, int amount)
    {
        GameObject coin = new GameObject("Gold_Coin");
        coin.transform.position = spawnPos;
        coin.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        
        BoxCollider2D col = coin.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        SpriteRenderer sr = coin.AddComponent<SpriteRenderer>();
        Texture2D tex = new Texture2D(16, 16);
        float r = 8f;
        for (int x = 0; x < 16; x++) 
        {
            for (int y = 0; y < 16; y++) 
            {
                float dx = x - r;
                float dy = y - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= r)
                {
                    if (dist >= r - 1.5f)
                        tex.SetPixel(x, y, Color.black);
                    else if (dist <= 3.5f)
                        tex.SetPixel(x, y, new Color(0.8f, 0.45f, 0f));
                    else
                        tex.SetPixel(x, y, Color.yellow);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        sr.sortingOrder = 100;

        var pickup = coin.AddComponent<Pulsevania.Core.LootPickup>();
        Vector2 launchForce = new Vector2(Random.Range(-2f, 2f), Random.Range(3f, 5f));
        pickup.Initialize(Pulsevania.Core.LootPickup.LootType.Gold, amount, launchForce);
    }

    private void SpawnKeyCollectible(Vector3 spawnPos)
    {
        GameObject key = new GameObject("Key_Collectible");
        key.transform.position = spawnPos;
        key.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

        BoxCollider2D colK = key.AddComponent<BoxCollider2D>();
        colK.isTrigger = true;

        SpriteRenderer srk = key.AddComponent<SpriteRenderer>();
        Texture2D texk = new Texture2D(16, 16);
        for (int x = 0; x < 16; x++) 
        {
            for (int y = 0; y < 16; y++) 
            {
                bool isRing = (x > 4 && x < 11 && y > 9 && y < 15);
                bool isRingHole = (x > 6 && x < 9 && y > 10 && y < 13);
                bool isShaft = (x == 7 && y > 2 && y <= 9);
                bool isTeeth = (x == 8 || x == 9) && (y == 3 || y == 5);

                if ((isRing && !isRingHole) || isShaft || isTeeth)
                {
                    if (isRingHole || (isRing && (x == 5 || x == 10 || y == 10 || y == 14)))
                        texk.SetPixel(x, y, Color.black);
                    else
                        texk.SetPixel(x, y, new Color(0.7f, 0f, 1f)); // Glowing Purple Key Color
                }
                else
                {
                    texk.SetPixel(x, y, Color.clear);
                }
            }
        }
        texk.filterMode = FilterMode.Point;
        texk.Apply();
        srk.sprite = Sprite.Create(texk, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        srk.sortingOrder = 100;

        var pickupk = key.AddComponent<Pulsevania.Core.LootPickup>();
        Vector2 launchForceK = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(3f, 5f));
        pickupk.Initialize(Pulsevania.Core.LootPickup.LootType.Key, 1, launchForceK);
    }

    private void SpawnEquipmentDrop(Pulsevania.Core.ItemData item, Vector3 spawnPos)
    {
        GameObject equipGo = new GameObject(item.itemName + "_Drop");
        equipGo.transform.position = spawnPos;
        equipGo.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

        BoxCollider2D colE = equipGo.AddComponent<BoxCollider2D>();
        colE.isTrigger = true;

        SpriteRenderer sre = equipGo.AddComponent<SpriteRenderer>();
        sre.sprite = item.icon;
        sre.sortingOrder = 100;

        var pickupE = equipGo.AddComponent<Pulsevania.Core.EquipmentItemPickup>();
        Vector2 launchForceE = new Vector2(Random.Range(-2f, 2f), Random.Range(3f, 5f));
        pickupE.Initialize(item, launchForceE);
    }
}
