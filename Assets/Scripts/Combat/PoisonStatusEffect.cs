using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    public class PoisonStatusEffect : MonoBehaviour
    {
        private float tickInterval = 1.0f;
        private int totalTicks = 5;
        private int damagePerTick = 3;
        
        private PlayerController player;
        private SpriteRenderer spriteRenderer;
        private Coroutine poisonCoroutine;

        public void ApplyPoison(int tickDamage, int ticks)
        {
            damagePerTick = tickDamage;
            totalTicks = ticks;
            
            if (player == null)
            {
                player = GetComponent<PlayerController>();
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (poisonCoroutine != null)
            {
                StopCoroutine(poisonCoroutine);
            }
            poisonCoroutine = StartCoroutine(PoisonRoutine());
        }

        private IEnumerator PoisonRoutine()
        {
            Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            
            for (int i = 0; i < totalTicks; i++)
            {
                yield return new WaitForSeconds(tickInterval);
                
                if (player == null || player.currentHP <= 0) break;

                // Apply poison tick damage directly (bypassing invulnerability & hurt animation)
                player.currentHP -= damagePerTick;
                if (player.currentHP < 0) player.currentHP = 0;
                player.UpdateHealthUI();

                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.2f, damagePerTick.ToString(), new Color(0.1f, 0.8f, 0.2f)); // Green poison text
                }

                // Green flash visual effect
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = new Color(0.2f, 0.8f, 0.2f, 1f);
                    yield return new WaitForSeconds(0.15f);
                    spriteRenderer.color = originalColor;
                }

                if (player.currentHP <= 0)
                {
                    // Trigger death using standard TakeDamage method
                    player.TakeDamage(1f);
                    break;
                }
            }

            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white; // Safety reset
            }
        }
    }
}
