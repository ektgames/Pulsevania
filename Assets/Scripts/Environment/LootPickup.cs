using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    public class LootPickup : MonoBehaviour
    {
        public enum LootType { Gold, Potion, Key }
        public LootType type = LootType.Gold;
        public int amount = 10;
        private bool collected = false;

        public void Initialize(LootType lootType, int val, Vector2 launchForce)
        {
            type = lootType;
            amount = val;
            StartCoroutine(BounceRoutine(launchForce));
        }

        private IEnumerator BounceRoutine(Vector2 launchForce)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0f;
            float duration = 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float height = 1.5f;
                float xOffset = launchForce.x * t;
                float yOffset = Mathf.Sin(t * Mathf.PI) * height;

                transform.position = startPos + new Vector3(xOffset, yOffset, 0f);
                yield return null;
            }
        }

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

        public static void SpawnPhysicalLoot(Vector3 spawnPos, LootType type, int amount)
        {
            GameObject loot = new GameObject(type == LootType.Gold ? "GoldCoin_Drop" : (type == LootType.Potion ? "Potion_Drop" : "Key_Drop"));
            loot.transform.position = spawnPos;
            
            if (type == LootType.Gold)
            {
                loot.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                BoxCollider2D col = loot.AddComponent<BoxCollider2D>();
                col.isTrigger = true;

                SpriteRenderer sr = loot.AddComponent<SpriteRenderer>();
                Texture2D tex = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++) 
                {
                    for (int y = 0; y < 16; y++) 
                    {
                        float dx = x - 7.5f;
                        float dy = y - 7.5f;
                        bool isOuter = (dx * dx + dy * dy <= 49f) && (dx * dx + dy * dy > 36f);
                        bool isInner = (dx * dx + dy * dy <= 36f);
                        if (isOuter) tex.SetPixel(x, y, new Color(0.2f, 0.15f, 0f));
                        else if (isInner) tex.SetPixel(x, y, new Color(1f, 0.85f, 0f));
                        else tex.SetPixel(x, y, Color.clear);
                    }
                }
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
                sr.sortingOrder = 100;
            }
            else if (type == LootType.Potion)
            {
                loot.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                BoxCollider2D col = loot.AddComponent<BoxCollider2D>();
                col.isTrigger = true;

                SpriteRenderer sr = loot.AddComponent<SpriteRenderer>();
                Texture2D tex = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++) 
                {
                    for (int y = 0; y < 16; y++) 
                    {
                        bool isBottle = (x >= 5 && x <= 10 && y >= 2 && y <= 9) || (x >= 7 && x <= 8 && y >= 9 && y <= 13);
                        bool isLiquid = (x >= 6 && x <= 9 && y >= 3 && y <= 7);
                        if (isLiquid) tex.SetPixel(x, y, Color.green);
                        else if (isBottle) tex.SetPixel(x, y, Color.white);
                        else tex.SetPixel(x, y, Color.clear);
                    }
                }
                tex.filterMode = FilterMode.Point;
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
                sr.sortingOrder = 100;
            }
            else if (type == LootType.Key)
            {
                loot.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
                BoxCollider2D col = loot.AddComponent<BoxCollider2D>();
                col.isTrigger = true;

                SpriteRenderer sr = loot.AddComponent<SpriteRenderer>();
                Texture2D tex = new Texture2D(16, 16);
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
                                tex.SetPixel(x, y, Color.black);
                            else
                                tex.SetPixel(x, y, new Color(0.7f, 0f, 1f));
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
            }

            var pickup = loot.AddComponent<LootPickup>();
            Vector2 launchForce = new Vector2(Random.Range(-2f, 2f), Random.Range(3f, 5f));
            pickup.Initialize(type, amount, launchForce);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collected) return;

            bool isPlayer = collision.CompareTag("Player") || 
                            collision.name == "Player" ||
                            collision.GetComponent<PlayerController>() != null ||
                            collision.GetComponentInParent<PlayerController>() != null;

            if (isPlayer)
            {
                collected = true;
                Debug.Log($"[Pulsevania Pickup] Success: Player collected loot type {type} of amount {amount}!");

                if (GameManager.Instance != null)
                {
                    if (type == LootType.Gold) GameManager.Instance.AddGold(amount);
                    else if (type == LootType.Potion)
                    {
                        if (InventoryManager.Instance != null)
                        {
                            ItemData potData = InventoryManager.Instance.itemDatabase.Find(x => x.itemName == "Health Potion (Can Potu)");
                            if (potData != null)
                            {
                                bool anySuccess = false;
                                for (int i = 0; i < amount; i++)
                                {
                                    ItemData potCopy = new ItemData(potData.itemName, potData.equipSlot, potData.icon, potData.equippedSprite, potData.goldPrice, potData.statType, potData.statValue);
                                    potCopy.count = 1;
                                    bool success = InventoryManager.Instance.AddItem(potCopy);
                                    if (success) anySuccess = true;
                                }
                                if (!anySuccess)
                                {
                                    collected = false;
                                    return;
                                }
                            }
                        }
                    }
                    else if (type == LootType.Key) GameManager.Instance.AddKey(amount);
                }

                if (DamageTextPool.Instance != null)
                {
                    string label = type == LootType.Gold ? $"+{amount} G" : (type == LootType.Potion ? $"+{amount} Potion" : $"+{amount} Key");
                    Color col = type == LootType.Gold ? Color.yellow : (type == LootType.Potion ? Color.green : Color.cyan);
                    DamageTextPool.Instance.SpawnText(transform.position, label, col);
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
                }

                Destroy(gameObject);
            }
        }
    }
}
