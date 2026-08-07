using System;
using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    public class HealthSystem : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private float invulnerabilityDuration = 1f;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color damageFlashColor = Color.red;
        [SerializeField] private float flashInterval = 0.1f;

        // Properties
        public int MaxHealth => maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsInvulnerable { get; private set; }

        // Events
        public event Action<int, int> OnHealthChanged; // (current, max)
        public event Action<int> OnDamageTaken;        // (amount)
        public event Action<int> OnHealed;             // (amount)
        public event Action OnDeath;

        private Coroutine flashCoroutine;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public void SetMaxHealth(int newMax)
        {
            maxHealth = newMax;
            CurrentHealth = maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsInvulnerable || CurrentHealth <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnDamageTaken?.Invoke(amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.DamageTaken);
            }

            if (CurrentHealth <= 0)
            {
                Die();
            }
            else
            {
                TriggerInvulnerability();
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || CurrentHealth <= 0 || CurrentHealth >= maxHealth) return;

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            OnHealed?.Invoke(amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        public void TriggerInvulnerability(float customDuration = -1f)
        {
            float duration = customDuration >= 0 ? customDuration : invulnerabilityDuration;
            if (duration > 0f)
            {
                StartCoroutine(InvulnerabilityRoutine(duration));
            }
        }

        private IEnumerator InvulnerabilityRoutine(float duration)
        {
            IsInvulnerable = true;
            yield return new WaitForSeconds(duration);
            IsInvulnerable = false;
        }

        private IEnumerator FlashRoutine(float duration)
        {
            float elapsed = 0f;
            Color originalColor = spriteRenderer.color;

            while (elapsed < duration)
            {
                // Toggle flash color
                spriteRenderer.color = damageFlashColor;
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;

                // Toggle back / semi-transparent
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.3f);
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            spriteRenderer.color = originalColor;
            flashCoroutine = null;
        }

        private void Die()
        {
            IsInvulnerable = true;
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(0.3f, 0.3f, 0.3f, 0.5f); // Greyed out/semi-transparent on death
            }
            OnDeath?.Invoke();
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            IsInvulnerable = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }
    }
}
