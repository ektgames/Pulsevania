using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PotionCollectible : MonoBehaviour
    {
        private bool collected = false;

        private void Awake()
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.linearVelocity = Vector2.zero;
            }
            var col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        public void Initialize(Vector2 launchForce)
        {
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

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collected) return;

            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                collected = true;

                if (InventoryManager.Instance != null)
                {
                    ItemData potData = InventoryManager.Instance.itemDatabase.Find(x => x.itemName == "Health Potion (Can Potu)");
                    if (potData != null)
                    {
                        ItemData potCopy = new ItemData(potData.itemName, potData.equipSlot, potData.icon, potData.equippedSprite, potData.goldPrice, potData.statType, potData.statValue);
                        potCopy.count = 1;
                        bool success = InventoryManager.Instance.AddItem(potCopy);
                        if (!success)
                        {
                            collected = false;
                            return;
                        }
                    }
                }

                if (DamageTextPool.Instance != null)
                {
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    DamageTextPool.Instance.SpawnText(transform.position, isTR ? "+1 Can Potu" : "+1 Potion", Color.green);
                }

                Destroy(gameObject);
            }
        }
    }
}
