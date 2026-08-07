using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Damageable))]
    public class BaseEnemyAI : MonoBehaviour
    {
        [Header("Patrol Settings")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float idleTimeAtTurn = 1f;
        [SerializeField] private Transform edgeCheckPoint;
        [SerializeField] private Transform wallCheckPoint;
        [SerializeField] private float checkDistance = 0.5f;
        [SerializeField] private LayerMask groundLayer;

        [Header("Combat Settings")]
        [SerializeField] private float chaseSpeed = 4.5f;
        [SerializeField] private float aggroRange = 6f;
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackCheckRadius = 0.6f;

        [Header("Loot Drops")]
        [SerializeField] private int goldMin = 2;
        [SerializeField] private int goldMax = 7;
        [SerializeField] [Range(0f, 1f)] private float potionDropChance = 0.25f;
        [SerializeField] private GameObject goldPrefab; // assigned or spawned via Manager
        [SerializeField] private GameObject potionPrefab;

        // References
        private Rigidbody2D rb;
        private HealthSystem healthSystem;
        private Damageable damageable;
        private Transform playerTransform;
        [SerializeField] private SpriteAnimator spriteAnimator;

        // State variables
        private bool isFacingRight = true;
        private bool isIdle;
        private bool isChasing;
        private float attackCooldownTimer;
        private bool isDead;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            healthSystem = GetComponent<HealthSystem>();
            damageable = GetComponent<Damageable>();
            spriteAnimator = GetComponentInChildren<SpriteAnimator>();
        }

        private void Start()
        {
            healthSystem.OnDeath += HandleDeath;
            healthSystem.OnDamageTaken += HandleHit;

            // Simple active player finding
            FindPlayer();
        }

        private void OnDestroy()
        {
            if (healthSystem != null)
            {
                healthSystem.OnDeath -= HandleDeath;
                healthSystem.OnDamageTaken -= HandleHit;
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
            if (isDead || GameManager.Instance.CurrentState != GameState.Gameplay)
            {
                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    rb.linearVelocity = Vector2.zero;
                }
                return;
            }

            if (playerTransform == null)
            {
                FindPlayer();
                return;
            }

            // Manage cooldowns
            if (attackCooldownTimer > 0)
            {
                attackCooldownTimer -= Time.deltaTime;
            }

            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= aggroRange && CanSeePlayer())
            {
                isChasing = true;
            }
            else
            {
                isChasing = false;
            }

            if (isChasing)
            {
                if (distanceToPlayer <= attackRange)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    if (attackCooldownTimer <= 0)
                    {
                        AttackPlayer();
                    }
                }
                else
                {
                    ChasePlayer();
                }
            }
            else
            {
                PatrolBehavior();
            }

            // Prevent suicide: stop movement if moving into water, lava, or cliff
            if (rb.linearVelocity.x > 0.05f && IsNearHazardOrEdge(true))
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            else if (rb.linearVelocity.x < -0.05f && IsNearHazardOrEdge(false))
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }

            // Update animator state
            if (spriteAnimator != null)
            {
                if (isIdle)
                {
                    spriteAnimator.PlayState(AnimState.Idle);
                }
                else if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
                {
                    spriteAnimator.PlayState(AnimState.Walk);
                }
                else
                {
                    spriteAnimator.PlayState(AnimState.Idle);
                }
            }
        }

        private bool CanSeePlayer()
        {
            // Direct line of sight cast to ensure no walls are in between
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, playerTransform.position);

            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, groundLayer);
            return hit.collider == null; // Clear view
        }

        private void PatrolBehavior()
        {
            if (isIdle) return;

            // Move in facing direction
            float currentSpeed = patrolSpeed * (isFacingRight ? 1f : -1f);
            rb.linearVelocity = new Vector2(currentSpeed, rb.linearVelocity.y);

            // Front wall detection
            bool isAtWall = Physics2D.Raycast(wallCheckPoint.position, isFacingRight ? Vector2.right : Vector2.left, checkDistance, groundLayer);
            bool isAtHazard = IsNearHazardOrEdge(isFacingRight);

            if (isAtHazard || isAtWall)
            {
                StartCoroutine(TurnAroundRoutine());
            }
        }

        private IEnumerator TurnAroundRoutine()
        {
            isIdle = true;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            yield return new WaitForSeconds(idleTimeAtTurn);
            if (!isDead && !isChasing)
            {
                Flip();
            }
            isIdle = false;
        }

        private void ChasePlayer()
        {
            float direction = playerTransform.position.x - transform.position.x;
            if (direction > 0.1f && !isFacingRight)
            {
                Flip();
            }
            else if (direction < -0.1f && isFacingRight)
            {
                Flip();
            }

            float currentSpeed = chaseSpeed * (isFacingRight ? 1f : -1f);
            rb.linearVelocity = new Vector2(currentSpeed, rb.linearVelocity.y);

            // Turn around if chasing leads to edge (stop enemy falling down)
            bool isAtEdge = !Physics2D.Raycast(edgeCheckPoint.position, Vector2.down, checkDistance, groundLayer);
            if (isAtEdge)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop chasing further
            }
        }

        private void AttackPlayer()
        {
            attackCooldownTimer = attackCooldown;
            if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Attack, true);
            }

            // Perform attack check
            Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackCheckRadius);
            foreach (Collider2D hit in hits)
            {
                Damageable dmg = hit.GetComponent<Damageable>();
                if (dmg != null && dmg.Team == Team.Player)
                {
                    dmg.Damage(attackDamage, Team.Enemy);
                    DamageTextPool.Instance.SpawnText(hit.transform.position + Vector3.up, attackDamage.ToString(), Color.red);
                }
            }
        }

        private void Flip()
        {
            isFacingRight = !isFacingRight;
            Vector3 scaler = transform.localScale;
            scaler.x *= -1;
            transform.localScale = scaler;
        }

        private void HandleHit(int damage)
        {
            if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Hurt, true);
            }
            // Aggro player upon getting damaged even if outside vision
            if (playerTransform != null)
            {
                isChasing = true;
            }
        }

        private void HandleDeath()
        {
            isDead = true;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Death, true);
            }
            GetComponent<Collider2D>().enabled = false;

            // Drop loot
            SpawnLoot();

            Destroy(gameObject, 1.5f);
        }

        private void SpawnLoot()
        {
            int goldCount = Random.Range(goldMin, goldMax + 1);
            LootPickup.SpawnPhysicalLoot(transform.position, LootPickup.LootType.Gold, goldCount);

            if (Random.value < potionDropChance)
            {
                LootPickup.SpawnPhysicalLoot(transform.position + Vector3.up * 0.5f, LootPickup.LootType.Potion, 1);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (edgeCheckPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(edgeCheckPoint.position, edgeCheckPoint.position + Vector3.down * checkDistance);
            }

            if (wallCheckPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(wallCheckPoint.position, wallCheckPoint.position + (isFacingRight ? Vector3.right : Vector3.left) * checkDistance);
            }

            if (attackPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(attackPoint.position, attackCheckRadius);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }

        private bool IsNearHazardOrEdge(bool checkRight)
        {
            float checkX = transform.position.x + (checkRight ? 0.8f : -0.8f);
            Vector2 checkOrigin = new Vector2(checkX, transform.position.y);
            
            // 1. Ground edge check
            RaycastHit2D groundHit = Physics2D.Raycast(checkOrigin, Vector2.down, 1.5f, groundLayer);
            if (groundHit.collider == null)
            {
                return true; // Empty cliff
            }

            // 2. Water / Lava / Spike check
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(checkOrigin + Vector2.down * 0.5f, 0.5f);
            foreach (var col in overlaps)
            {
                if (col != null && (
                    col.GetComponent<LavaTile>() != null || 
                    col.GetComponent<WaterBody>() != null || 
                    col.GetComponent<SpikeTile>() != null))
                {
                    return true; // Danger hazard
                }
            }

            return false;
        }
    }
}
