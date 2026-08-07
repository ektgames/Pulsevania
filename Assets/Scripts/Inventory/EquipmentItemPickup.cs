using UnityEngine;

namespace Pulsevania.Core
{
    public class EquipmentItemPickup : MonoBehaviour
    {
        private ItemData item;
        private bool collected = false;

        public void Initialize(ItemData itemData, Vector2 launchForce)
        {
            item = itemData;
            StartCoroutine(BounceRoutine(launchForce));
        }

        private void Update()
        {
            if (collected || item == null) return;
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

        private System.Collections.IEnumerator BounceRoutine(Vector2 launchForce)
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

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collected || item == null) return;

            bool isPlayer = collision.CompareTag("Player") || 
                            collision.name == "Player" ||
                            collision.GetComponent<PlayerController>() != null ||
                            collision.GetComponentInParent<PlayerController>() != null;

            if (isPlayer)
            {
                if (InventoryManager.Instance != null)
                {
                    if (InventoryManager.Instance.AddItem(item))
                    {
                        collected = true;

                        if (DamageTextPool.Instance != null)
                        {
                            DamageTextPool.Instance.SpawnText(transform.position, $"+{item.itemName}", Color.yellow);
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
    }
}
