using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Damageable))]
    public class RangedEnemyAI : MonoBehaviour
    {
        [Header("Combat Settings")]
        [SerializeField] private float attackRange = 8f;
        [SerializeField] private float attackCooldown = 2f;
        [SerializeField] private Transform shootPoint;
        [SerializeField] private int goldReward = 5;

        // References
        private HealthSystem healthSystem;
        private Damageable damageable;
        private Transform playerTransform;

        // State variables
        private float attackCooldownTimer;
        private bool isDead;
        private bool facingRight = false; // Static ranged enemy default faces left

        private void Awake()
        {
            healthSystem = GetComponent<HealthSystem>();
            damageable = GetComponent<Damageable>();
        }

        private void Start()
        {
            healthSystem.OnDeath += HandleDeath;
            FindPlayer();
        }

        private void OnDestroy()
        {
            if (healthSystem != null)
            {
                healthSystem.OnDeath -= HandleDeath;
            }
        }

        private void FindPlayer()
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (isDead || GameManager.Instance.CurrentState != GameState.Gameplay) return;

            if (playerTransform == null)
            {
                FindPlayer();
                return;
            }

            if (attackCooldownTimer > 0)
            {
                attackCooldownTimer -= Time.deltaTime;
            }

            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= attackRange && CanSeePlayer())
            {
                // Face the player
                FacePlayer();

                // Shoot
                if (attackCooldownTimer <= 0)
                {
                    ShootProjectile();
                }
            }
        }

        private bool CanSeePlayer()
        {
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, LayerMask.GetMask("Ground"));
            return hit.collider == null; // Clear sightline
        }

        private void FacePlayer()
        {
            float direction = playerTransform.position.x - transform.position.x;
            if (direction > 0.1f && !facingRight)
            {
                Flip();
            }
            else if (direction < -0.1f && facingRight)
            {
                Flip();
            }
        }

        private void ShootProjectile()
        {
            attackCooldownTimer = attackCooldown;
            Vector2 dir = facingRight ? Vector2.right : Vector2.left;
            
            Vector3 spawnPos = shootPoint != null ? shootPoint.position : transform.position;
            ProjectilePool.Instance.SpawnProjectile(spawnPos, dir, Team.Enemy);
        }

        private void Flip()
        {
            facingRight = !facingRight;
            Vector3 scaler = transform.localScale;
            scaler.x *= -1;
            transform.localScale = scaler;
        }

        private void HandleDeath()
        {
            isDead = true;
            GetComponent<Collider2D>().enabled = false;
            
            // Spawn loot
            LootPickup.SpawnPhysicalLoot(transform.position, LootPickup.LootType.Gold, goldReward);

            Destroy(gameObject, 1f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            if (shootPoint != null)
            {
                Gizmos.DrawSphere(shootPoint.position, 0.15f);
            }
        }
    }
}
