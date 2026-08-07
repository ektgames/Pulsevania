using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class LavaTile : MonoBehaviour
    {
        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController pc = other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.isInLava = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerController pc = other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.isInLava = false;
            }
        }
    }

    [RequireComponent(typeof(BoxCollider2D))]
    public class SpikeTile : MonoBehaviour
    {
        [SerializeField] private float damage = 20f;
        [SerializeField] private float hitCooldown = 1.0f;
        private float nextHitTime = 0f;

        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController pc = other.GetComponentInParent<PlayerController>();
            if (pc != null && Time.time >= nextHitTime)
            {
                nextHitTime = Time.time + hitCooldown;
                int currentRoomId = MapManager.Instance != null ? MapManager.Instance.GetCurrentRoomId() : 1;
                float baseSpikeDamage = 4f;
                float finalSpikeDamage = baseSpikeDamage + (currentRoomId - 1) * 0.5f;

                pc.TakeDamage(finalSpikeDamage);

                if (DamageTextPool.Instance != null)
                {
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    DamageTextPool.Instance.SpawnText(pc.transform.position + Vector3.up, isTR ? "DİKENLENDİ!" : "SPIKED!", Color.red);
                }

                Rigidbody2D rb = pc.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    float direction = pc.transform.position.x > transform.position.x ? 1f : -1f;
                    rb.linearVelocity = Vector2.zero;
                    rb.AddForce(new Vector2(direction * 7f, 4.5f), ForceMode2D.Impulse);
                }
            }
        }
    }

    [RequireComponent(typeof(BoxCollider2D))]
    public class WaterBody : MonoBehaviour
    {
        private void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController pc = other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.isInWater = true;
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(pc.transform.position, "Splashing!", Color.cyan);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerController pc = other.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                pc.isInWater = false;
            }
        }
    }
}
