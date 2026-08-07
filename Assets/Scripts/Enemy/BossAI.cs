using System.Collections;
using UnityEngine;
using Pulsevania.Core;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Damageable))]
    public class BossAI : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float chaseRange = 12f;

        [Header("Attack Settings")]
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private int meleeDamage = 2;
        [SerializeField] private float attackCooldown = 2f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Visual References")]
        [SerializeField] private SpriteAnimator spriteAnimator;

        private Transform player;
        private Rigidbody2D rb;
        private HealthSystem healthSystem;
        private float cooldownTimer;
        private bool facingRight = false;
        private bool isDead = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            healthSystem = GetComponent<HealthSystem>();
            if (spriteAnimator == null)
            {
                spriteAnimator = GetComponentInChildren<SpriteAnimator>();
            }
        }

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                player = pc.transform;
            }

            healthSystem.OnDeath += HandleDeath;
            healthSystem.OnDamageTaken += HandleDamage;
        }

        private void OnDestroy()
        {
            if (healthSystem != null)
            {
                healthSystem.OnDeath -= HandleDeath;
                healthSystem.OnDamageTaken -= HandleDamage;
            }
        }

        private void Update()
        {
            if (isDead || GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Gameplay || player == null)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            float distance = Vector2.Distance(transform.position, player.position);

            if (cooldownTimer > 0)
            {
                cooldownTimer -= Time.deltaTime;
            }

            float dirX = player.position.x - transform.position.x;
            if (dirX > 0.1f && !facingRight) Flip();
            else if (dirX < -0.1f && facingRight) Flip();

            if (distance <= chaseRange)
            {
                if (distance <= meleeRange)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    if (cooldownTimer <= 0)
                    {
                        TriggerBossAttack(true);
                    }
                }
                else
                {
                    float step = Mathf.Sign(dirX) * moveSpeed;
                    rb.linearVelocity = new Vector2(step, rb.linearVelocity.y);
                    
                    if (spriteAnimator != null)
                    {
                        spriteAnimator.PlayState(AnimState.Walk);
                    }

                    if (cooldownTimer <= 0 && Random.value < 0.3f)
                    {
                        TriggerBossAttack(false);
                    }
                }
            }
            else
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                if (spriteAnimator != null)
                {
                    spriteAnimator.PlayState(AnimState.Idle);
                }
            }
        }

        private void TriggerBossAttack(bool isMelee)
        {
            cooldownTimer = attackCooldown;

            if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Attack, true);
            }

            if (isMelee)
            {
                StartCoroutine(PerformMeleeSmash());
            }
            else
            {
                StartCoroutine(PerformShockwaveAttack());
            }
        }

        private IEnumerator PerformMeleeSmash()
        {
            yield return new WaitForSeconds(0.3f);
            if (isDead) yield break;

            Vector2 checkPos = (Vector2)transform.position + (facingRight ? Vector2.right : Vector2.left) * 1.2f;
            Collider2D playerColl = Physics2D.OverlapCircle(checkPos, meleeRange * 0.8f, playerLayer);

            if (playerColl != null)
            {
                Damageable playerDmg = playerColl.GetComponent<Damageable>();
                if (playerDmg != null)
                {
                    playerDmg.Damage(meleeDamage, Team.Enemy);
                    DamageTextPool.Instance.SpawnText(playerColl.transform.position + Vector3.up, "BOSS SMASH!", Color.red);
                }
            }
        }

        private IEnumerator PerformShockwaveAttack()
        {
            yield return new WaitForSeconds(0.4f);
            if (isDead) yield break;

            Vector2 spawnPos = transform.position + (facingRight ? Vector3.right : Vector3.left) * 1f;
            Vector2 dir = facingRight ? Vector2.right : Vector2.left;

            if (ProjectilePool.Instance != null)
            {
                ProjectilePool.Instance.SpawnProjectile(spawnPos, dir, Team.Enemy);
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.8f, "SHOCKWAVE!", Color.magenta);
            }
        }

        private void HandleDamage(int dmg)
        {
            if (spriteAnimator != null && !isDead)
            {
                spriteAnimator.PlayState(AnimState.Hurt, true);
            }
        }

        private void HandleDeath()
        {
            if (isDead) return;
            isDead = true;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;

            if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Death, true);
            }

            Debug.Log("[Pulsevania] Boss Defeated! Clearing active room.");
            StartCoroutine(CompleteLevelDelay());
        }

        private IEnumerator CompleteLevelDelay()
        {
            yield return new WaitForSeconds(1.5f);
            if (MapManager.Instance != null)
            {
                int currentRoomId = MapManager.Instance.GetCurrentRoomId();
                MapManager.Instance.ClearRoom(currentRoomId);
            }
            if (DamageTextPool.Instance != null)
            {
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 2f, "BOSS DEFEATED!", Color.green);
            }
        }

        private void Flip()
        {
            facingRight = !facingRight;
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
    }
}
