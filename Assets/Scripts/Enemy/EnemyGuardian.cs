using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Damageable))]
    public class EnemyGuardian : MonoBehaviour
    {
        public enum MonsterBehavior { ClubMelee, DaggerThrower, FlameMage, Boss }

        [Header("Combat Configuration")]
        public MonsterBehavior behavior = MonsterBehavior.ClubMelee;

        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float aggroRange = 6f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private int attackDamage = 12;
        [SerializeField] private float attackCooldown = 1.5f;

        private Rigidbody2D rb;
        private HealthSystem healthSystem;
        private Damageable damageable;
        private Transform playerTransform;
        private SpriteAnimator spriteAnimator;
        private Animator unityAnimator;

        private bool isFacingRight = true;
        private bool isAggroed = false;
        private float attackCooldownTimer;
        private bool isDead = false;

        private Vector3 startPosition;
        private float patrolRange = 4.0f;
        private bool patrollingRight = true;
        
        public bool isKeyGuardian = false;
        public float guardXCenter;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            healthSystem = GetComponent<HealthSystem>();
            damageable = GetComponent<Damageable>();
            spriteAnimator = GetComponentInChildren<SpriteAnimator>();
            unityAnimator = GetComponentInChildren<Animator>();
        }

        private void PlayAnimation(string stateName, bool force = false)
        {
            if (spriteAnimator != null)
            {
                if (stateName == "Idle") spriteAnimator.PlayState(AnimState.Idle);
                else if (stateName == "Move") spriteAnimator.PlayState(AnimState.Walk);
                else if (stateName == "Attack") spriteAnimator.PlayState(AnimState.Attack, force);
                else if (stateName == "Stuned") spriteAnimator.PlayState(AnimState.Hurt, force);
                else if (stateName == "Death") spriteAnimator.PlayState(AnimState.Death, force);
                else if (stateName == "Spell")
                {
                    SpriteAnimator.AnimationClip dummy;
                    if (spriteAnimator.TryGetClip(AnimState.Spell, out dummy))
                        spriteAnimator.PlayState(AnimState.Spell, force);
                    else if (spriteAnimator.TryGetClip(AnimState.Cast, out dummy))
                        spriteAnimator.PlayState(AnimState.Cast, force);
                    else
                        spriteAnimator.PlayState(AnimState.Attack, force);
                }
            }
            if (unityAnimator != null)
            {
                unityAnimator.Play(stateName);
            }
        }

        private void Start()
        {
            healthSystem.OnDeath += HandleDeath;
            healthSystem.OnDamageTaken += HandleHit;
            FindPlayer();

            if (IsFlyingBehavior())
            {
                rb.gravityScale = 0f;
            }

            if (isKeyGuardian)
            {
                startPosition = new Vector3(guardXCenter, transform.position.y, transform.position.z);
                patrolRange = 6.0f;
            }
            else
            {
                startPosition = transform.position;
                patrolRange = 12.0f;
            }
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

        private bool IsFlyingBehavior()
        {
            return behavior == MonsterBehavior.FlameMage || 
                  (behavior == MonsterBehavior.Boss && (levelId == 20 || levelId == 40 || levelId == 50));
        }

        public int levelId = 1;

        public void InitializeStats(MonsterBehavior behaviorType, int maxHP, int dmg, float speed, float scale, int roomLevel)
        {
            behavior = behaviorType;
            chaseSpeed = speed;
            attackDamage = dmg;
            levelId = roomLevel;
            transform.localScale = new Vector3(scale, scale, 1f);

            if (healthSystem == null) healthSystem = GetComponent<HealthSystem>();
            healthSystem.SetMaxHealth(maxHP);

            if (behavior == MonsterBehavior.Boss)
            {
                aggroRange = 25f; // Large aggro range so they wake up when player enters the arena
            }

            if (behavior == MonsterBehavior.DaggerThrower)
            {
                attackRange = 6f;
                attackCooldown = 1.8f;
            }
            else if (behavior == MonsterBehavior.FlameMage)
            {
                attackRange = 8f;
                attackCooldown = 2.0f;
                if (rb != null) rb.gravityScale = 0f;
            }
            else if (behavior == MonsterBehavior.Boss)
            {
                // Boss custom setup depending on map levels (10, 20, 30, 40, 50)
                if (levelId == 10)
                {
                    attackRange = 5.0f; // Increased from 2.5f to match its giant size and vertically reach the player
                    attackCooldown = 2.0f;
                    if (rb != null) rb.gravityScale = 2.5f;
                }
                else if (levelId == 20)
                {
                    attackRange = 8f;
                    attackCooldown = 2.2f;
                    if (rb != null) rb.gravityScale = 0f;
                }
                else if (levelId == 30)
                {
                    attackRange = 3.0f;
                    attackCooldown = 1.8f;
                    if (rb != null) rb.gravityScale = 2.5f;
                }
                else if (levelId == 40)
                {
                    attackRange = 9f;
                    attackCooldown = 1.8f;
                    if (rb != null) rb.gravityScale = 0f;
                }
                else if (levelId == 50)
                {
                    attackRange = 11f;
                    attackCooldown = 2.0f;
                    if (rb != null) rb.gravityScale = 0f;
                }
                else
                {
                    attackRange = 2.0f;
                    attackCooldown = 1.5f;
                    if (rb != null) rb.gravityScale = 2.5f;
                }

                // Add glowing boss color
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 0.5f, 0.5f); // Redish glowing tint
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

            if (attackCooldownTimer > 0)
            {
                attackCooldownTimer -= Time.deltaTime;
            }

            float dist = Vector2.Distance(transform.position, playerTransform.position);

            if (!isAggroed && dist <= aggroRange)
            {
                isAggroed = true;
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, "INTRUDER!", Color.red);
                }
            }

            if (isAggroed)
            {
                float direction = playerTransform.position.x - transform.position.x;
                if (direction > 0.1f && !isFacingRight) Flip();
                else if (direction < -0.1f && isFacingRight) Flip();

                if (IsFlyingBehavior())
                {
                    // Chasing movement in the air towards the player
                    float targetY = playerTransform.position.y + 2.0f + Mathf.Sin(Time.time * 3.5f) * 0.4f;
                    float velY = (targetY - transform.position.y) * 2.5f;

                    float velX = 0f;
                    if (dist > attackRange * 0.8f)
                    {
                        velX = chaseSpeed * (isFacingRight ? 1f : -1f);
                    }
                    else if (dist < attackRange * 0.4f)
                    {
                        velX = -chaseSpeed * (isFacingRight ? 1f : -1f); // fly back a bit
                    }

                    rb.linearVelocity = new Vector2(velX, velY);

                    if (attackCooldownTimer <= 0 && dist <= attackRange)
                    {
                        ExecuteAttack();
                    }
                }
                else if (behavior == MonsterBehavior.DaggerThrower)
                {
                    // Ranged spitter path finding: keep distance
                    if (dist < 4.5f)
                    {
                        float speed = chaseSpeed * (isFacingRight ? -1f : 1f);
                        rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
                    }
                    else if (dist > 5.5f)
                    {
                        float speed = chaseSpeed * (isFacingRight ? 1f : -1f);
                        rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
                    }
                    else
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    }

                    if (attackCooldownTimer <= 0 && dist <= attackRange)
                    {
                        RangedAttack();
                    }
                }
                else
                {
                    // Melee or Boss Ground movement
                    if (dist <= attackRange)
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                        if (attackCooldownTimer <= 0)
                        {
                            ExecuteAttack();
                        }
                    }
                    else
                    {
                        float speed = chaseSpeed * (isFacingRight ? 1f : -1f);
                        rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
                    }
                }

                // Jump check (ground monsters only): if near obstacle/wall OR player is above
                if (!IsFlyingBehavior())
                {
                    Vector2 checkDirection = isFacingRight ? Vector2.right : Vector2.left;
                    Vector2 rayStart = new Vector2(transform.position.x + (isFacingRight ? 0.5f : -0.5f), transform.position.y);
                    RaycastHit2D[] wallHits = Physics2D.RaycastAll(rayStart, checkDirection, 0.6f);
                    bool isNearObstacle = false;
                    foreach (var hit in wallHits)
                    {
                        if (hit.collider != null && !hit.collider.isTrigger && hit.collider.transform.root != transform.root)
                        {
                            isNearObstacle = true;
                            break;
                        }
                    }
                    bool playerIsAbove = (playerTransform.position.y - transform.position.y) > 1.5f;

                    if ((isNearObstacle || playerIsAbove) && Mathf.Abs(rb.linearVelocity.y) < 0.05f)
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 8.5f);
                    }
                }
            }
            else
            {
                // Patrol behavior
                if (IsFlyingBehavior())
                {
                    float bob = Mathf.Sin(Time.time * 2.0f) * 0.4f;
                    float targetY = startPosition.y + bob;
                    float velY = (targetY - transform.position.y) * 2f;

                    float velX = 0f;
                    if (behavior != MonsterBehavior.Boss)
                    {
                        if (patrollingRight)
                        {
                            if (!isFacingRight) Flip();
                            if (transform.position.x - startPosition.x > patrolRange)
                            {
                                patrollingRight = false;
                                Flip();
                            }
                        }
                        else
                        {
                            if (isFacingRight) Flip();
                            if (transform.position.x - startPosition.x < -patrolRange)
                            {
                                patrollingRight = true;
                                Flip();
                            }
                        }
                        velX = (chaseSpeed * 0.5f) * (isFacingRight ? 1f : -1f);
                    }
                    rb.linearVelocity = new Vector2(velX, velY);
                }
                else
                {
                    // Ground monster patrol
                    if (behavior == MonsterBehavior.Boss)
                    {
                        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                    }
                    else
                    {
                        if (patrollingRight)
                        {
                            if (!isFacingRight) Flip();
                            if (transform.position.x - startPosition.x > patrolRange)
                            {
                                patrollingRight = false;
                                Flip();
                            }
                        }
                        else
                        {
                            if (isFacingRight) Flip();
                            if (transform.position.x - startPosition.x < -patrolRange)
                            {
                                patrollingRight = true;
                                Flip();
                            }
                        }

                        // Turn around if we hit a wall OR hazard/edge while patrolling
                        Vector2 checkDir = isFacingRight ? Vector2.right : Vector2.left;
                        Vector2 startPos = new Vector2(transform.position.x + (isFacingRight ? 0.5f : -0.5f), transform.position.y);
                        RaycastHit2D[] wallPatrolHits = Physics2D.RaycastAll(startPos, checkDir, 0.4f);
                        bool isWallPatrolBlocked = false;
                        foreach (var hit in wallPatrolHits)
                        {
                            if (hit.collider != null && !hit.collider.isTrigger && hit.collider.transform.root != transform.root)
                            {
                                isWallPatrolBlocked = true;
                                break;
                            }
                        }
                        bool isNearHazard = patrollingRight ? IsNearHazardOrEdge(true) : IsNearHazardOrEdge(false);

                        if (isWallPatrolBlocked || isNearHazard)
                        {
                            patrollingRight = !patrollingRight;
                            Flip();
                        }

                        float speed = (chaseSpeed * 0.5f) * (isFacingRight ? 1f : -1f);
                        rb.linearVelocity = new Vector2(speed, rb.linearVelocity.y);
                    }
                }
            }

            // Prevent suicide (ground only)
            if (!IsFlyingBehavior())
            {
                if (rb.linearVelocity.x > 0.05f && IsNearHazardOrEdge(true))
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
                else if (rb.linearVelocity.x < -0.05f && IsNearHazardOrEdge(false))
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }

            // Animations
            if (isDead)
            {
                PlayAnimation("Death");
            }
            else if (Mathf.Abs(rb.linearVelocity.x) > 0.1f || (IsFlyingBehavior() && isAggroed))
            {
                PlayAnimation("Move");
            }
            else
            {
                PlayAnimation("Idle");
            }
        }

        private void ExecuteAttack()
        {
            if (behavior == MonsterBehavior.Boss)
            {
                if (levelId == 10) MossGolemAttack();
                else if (levelId == 20) SphinxAttack();
                else if (levelId == 30) BringerOfDeathAttack();
                else if (levelId == 40) VoidDevourerAttack();
                else if (levelId == 50) MagmaDragonAttack();
                else MeleeAttack();
            }
            else
            {
                MeleeAttack();
            }
        }

        private void MeleeAttack()
        {
            attackCooldownTimer = attackCooldown;
            PlayAnimation("Attack", true);

            Damageable playerDmg = playerTransform.GetComponent<Damageable>();
            if (playerDmg != null)
            {
                playerDmg.Damage(attackDamage, Team.Enemy);
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(playerTransform.position + Vector3.up, attackDamage.ToString(), Color.red);
                }
            }
        }

        private void RangedAttack()
        {
            attackCooldownTimer = attackCooldown;
            PlayAnimation("Attack", true);

            GameObject poisonGo = new GameObject("MonsterAcidSpit");
            poisonGo.transform.position = transform.position + Vector3.up * 0.4f;
            var col = poisonGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.5f, 0.5f);

            MonsterProjectile proj = poisonGo.AddComponent<MonsterProjectile>();
            Vector3 dir = (playerTransform.position - transform.position);
            proj.Initialize(dir, attackDamage, 7.5f, MonsterProjectile.ProjectileType.PoisonSpit, 3.0f);
        }

        private void FlyingAttack()
        {
            attackCooldownTimer = attackCooldown;
            PlayAnimation("Attack", true);

            GameObject fireballGo = new GameObject("MonsterFireball");
            fireballGo.transform.position = transform.position + Vector3.up * 0.3f;
            var col = fireballGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.5f, 0.5f);

            MonsterProjectile proj = fireballGo.AddComponent<MonsterProjectile>();
            Vector3 dir = (playerTransform.position - transform.position);
            proj.Initialize(dir, attackDamage, 6.0f, MonsterProjectile.ProjectileType.Fireball, 3.0f);
        }

        // BOSS LEVEL 10: Moss Golem Stomp
        private void MossGolemAttack()
        {
            attackCooldownTimer = attackCooldown;
            PlayAnimation("Attack", true);

            // Close range slam
            Damageable playerDmg = playerTransform.GetComponent<Damageable>();
            if (playerDmg != null && Vector2.Distance(transform.position, playerTransform.position) <= attackRange)
            {
                playerDmg.Damage(attackDamage, Team.Enemy);
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(playerTransform.position + Vector3.up, attackDamage.ToString(), Color.red);
                }
            }

            // Spawn EarthStomp shockwaves left and right
            SpawnStompProjectile(Vector3.left);
            SpawnStompProjectile(Vector3.right);
        }

        private void SpawnStompProjectile(Vector3 dir)
        {
            GameObject stompGo = new GameObject("GolemStomp");
            // Golem is scaled up, so its feet are lower. Calculate based on boss scale
            float yOffset = -0.5f;
            if (behavior == MonsterBehavior.Boss && levelId == 10)
            {
                yOffset = -1.8f; // Golem feet offset
            }
            stompGo.transform.position = transform.position + new Vector3(dir.x * 0.6f, yOffset, 0f);
            var col = stompGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 0.5f);

            MonsterProjectile proj = stompGo.AddComponent<MonsterProjectile>();
            proj.Initialize(dir, Mathf.RoundToInt(attackDamage * 0.7f), 5.5f, MonsterProjectile.ProjectileType.EarthStomp, 1.8f);
        }

        // BOSS LEVEL 20: Sphinx
        private void SphinxAttack()
        {
            attackCooldownTimer = attackCooldown;
            PlayAnimation("Attack", true);

            // Double fireballs
            SpawnSphinxProjectile(new Vector3(1f, 0.2f, 0f));
            SpawnSphinxProjectile(new Vector3(1f, -0.2f, 0f));
        }

        private void SpawnSphinxProjectile(Vector3 offsetDir)
        {
            GameObject projGo = new GameObject("SphinxBolt");
            projGo.transform.position = transform.position + Vector3.up * 0.2f;
            var col = projGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.5f, 0.5f);

            MonsterProjectile proj = projGo.AddComponent<MonsterProjectile>();
            Vector3 baseDir = (playerTransform.position - transform.position).normalized;
            Vector3 finalDir = (baseDir + offsetDir * 0.25f).normalized;
            proj.Initialize(finalDir, attackDamage, 7.5f, MonsterProjectile.ProjectileType.Fireball, 3f);
        }

        // BOSS LEVEL 30: Bringer of Death Scythe Slash and Death Bolt Casts
        private void BringerOfDeathAttack()
        {
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            
            // Randomly select between a scythe attack (melee) and a dark spell cast (ranged)
            // But if player is too close, prioritize Scythe Melee
            bool useMelee = (dist <= 3.5f) || (Random.value > 0.5f);

            if (useMelee)
            {
                // Scythe Attack (Melee)
                attackCooldownTimer = attackCooldown;
                PlayAnimation("Attack", true);

                Damageable playerDmg = playerTransform.GetComponent<Damageable>();
                if (playerDmg != null && dist <= attackRange + 1.0f)
                {
                    playerDmg.Damage(attackDamage, Team.Enemy);
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(playerTransform.position + Vector3.up, attackDamage.ToString(), Color.red);
                    }

                    // Scythe slash knockback
                    Rigidbody2D playerRb = playerTransform.GetComponent<Rigidbody2D>();
                    if (playerRb != null)
                    {
                        float knockDir = playerTransform.position.x > transform.position.x ? 1f : -1f;
                        playerRb.linearVelocity = Vector2.zero;
                        playerRb.AddForce(new Vector2(knockDir * 10f, 4.5f), ForceMode2D.Impulse);
                    }
                }
            }
            else
            {
                // Spell Cast (Ranged Spell)
                attackCooldownTimer = attackCooldown + 0.5f; // Extra cooldown for strong spell
                PlayAnimation("Spell", true);

                SpawnDeathBolt();
            }
        }

        private void SpawnDeathBolt()
        {
            GameObject projGo = new GameObject("DeathBolt");
            projGo.transform.position = transform.position + Vector3.up * 0.5f;
            var col = projGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.5f, 0.5f);

            MonsterProjectile proj = projGo.AddComponent<MonsterProjectile>();
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            proj.Initialize(dir, Mathf.RoundToInt(attackDamage * 0.8f), 5.0f, MonsterProjectile.ProjectileType.DeathBolt, 5.0f);
        }

        // BOSS LEVEL 40: Void Devourer
        private void VoidDevourerAttack()
        {
            attackCooldownTimer = attackCooldown;
            PlayAnimation("Attack", true);

            // 3-spread Void Balls
            SpawnVoidBall(0f);
            SpawnVoidBall(20f);
            SpawnVoidBall(-20f);
        }

        private void SpawnVoidBall(float angleDegrees)
        {
            GameObject projGo = new GameObject("VoidBall");
            projGo.transform.position = transform.position + Vector3.up * 0.1f;
            var col = projGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.5f, 0.5f);

            MonsterProjectile proj = projGo.AddComponent<MonsterProjectile>();
            Vector3 baseDir = (playerTransform.position - transform.position).normalized;
            float rad = angleDegrees * Mathf.Deg2Rad;
            Vector3 rotatedDir = new Vector3(
                baseDir.x * Mathf.Cos(rad) - baseDir.y * Mathf.Sin(rad),
                baseDir.x * Mathf.Sin(rad) + baseDir.y * Mathf.Cos(rad),
                0f
            );
            proj.Initialize(rotatedDir, attackDamage, 6.5f, MonsterProjectile.ProjectileType.VoidBall, 3.5f);
        }

        // BOSS LEVEL 50: Magma Dragon
        private void MagmaDragonAttack()
        {
            attackCooldownTimer = attackCooldown;
            PlayAnimation("Attack", true);

            GameObject projGo = new GameObject("TrackingMagmaBall");
            projGo.transform.position = transform.position + Vector3.up * 0.5f;
            var col = projGo.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.8f, 0.8f);

            MonsterProjectile proj = projGo.AddComponent<MonsterProjectile>();
            Vector3 dir = (playerTransform.position - transform.position).normalized;
            proj.Initialize(dir, attackDamage, 4.0f, MonsterProjectile.ProjectileType.TrackingFireball, 5.5f);
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
            isAggroed = true;
            PlayAnimation("Stuned", true);
            StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                Color originalColor = sr.color;
                sr.color = Color.red;
                yield return new WaitForSeconds(0.12f);
                sr.color = originalColor;
            }
        }

        private void HandleDeath()
        {
            isDead = true;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            PlayAnimation("Death", true);
            GetComponent<Collider2D>().enabled = false;

            if (MapManager.Instance != null)
            {
                int index = levelId - 1;
                if (index >= 0 && index < 50)
                {
                    MapManager.Instance.rooms[index].enemiesSpawned = false;
                }
            }

            int goldAmount = behavior == MonsterBehavior.Boss ? 100 : 15;
            LootPickup.SpawnPhysicalLoot(transform.position, LootPickup.LootType.Gold, goldAmount);

            Destroy(gameObject, 1f);
        }

        private void OnGUI()
        {
            if (isDead || !isAggroed || behavior != MonsterBehavior.Boss || GameManager.Instance.CurrentState != GameState.Gameplay) return;
            
            float heightOffset = 2.0f;
            if (levelId == 30)
            {
                heightOffset = 1.9f; // Fits the 3.6x scale Bringer of Death perfectly
            }
            else if (levelId == 40)
            {
                heightOffset = 1.0f; // Fits the player-sized Evil Wizard perfectly
            }
            else
            {
                heightOffset = 2.8f; // Fits the other giant bosses perfectly
            }

            Vector3 worldPos = transform.position + Vector3.up * heightOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            
            if (screenPos.z > 0)
            {
                float currentHP = healthSystem != null ? healthSystem.CurrentHealth : 100f;
                float maxHP = healthSystem != null ? healthSystem.MaxHealth : 100f;
                float pct = Mathf.Clamp01(currentHP / maxHP);
                
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(screenPos.x - 40, Screen.height - screenPos.y - 10, 80, 8), Texture2D.whiteTexture);
                
                GUI.color = Color.red;
                GUI.DrawTexture(new Rect(screenPos.x - 39, Screen.height - screenPos.y - 9, 78 * pct, 6), Texture2D.whiteTexture);
                
                GUI.color = Color.white;
            }
        }

        private bool IsNearHazardOrEdge(bool checkRight)
        {
            if (behavior == MonsterBehavior.Boss)
            {
                return false;
            }

            float checkX = transform.position.x + (checkRight ? 0.8f : -0.8f);
            Vector2 checkOrigin = new Vector2(checkX, transform.position.y);

            // 1. Ground edge check
            RaycastHit2D[] groundHits = Physics2D.RaycastAll(checkOrigin, Vector2.down, 2.2f);
            bool hitGround = false;
            foreach (var hit in groundHits)
            {
                if (hit.collider != null && !hit.collider.isTrigger && hit.collider.transform.root != transform.root)
                {
                    hitGround = true;
                    break;
                }
            }
            if (!hitGround)
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
