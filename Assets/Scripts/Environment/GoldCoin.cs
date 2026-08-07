using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class GoldCoin : MonoBehaviour
    {
        private int goldValue = 10;
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

        public void Initialize(int value, Vector2 launchForce)
        {
            goldValue = value;
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
                if (dist <= 4.0f) // Magnet range: 4 units
                {
                    Vector3 targetPos = player.transform.position + Vector3.up * 0.5f;
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, Time.deltaTime * 10f);
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
                    GameManager.Instance.AddGold(goldValue);
                }

                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(transform.position, $"+{goldValue} G", Color.yellow);
                }

                Destroy(gameObject);
            }
        }
    }
}
