using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthSystem))]
    [RequireComponent(typeof(Damageable))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float fallMultiplier = 2.5f;
        [SerializeField] private float lowJumpMultiplier = 2f;

        [Header("Double & Wall Jump Settings")]
        [SerializeField] private int maxJumps = 2;
        [SerializeField] private float doubleJumpForce = 14.5f;
        [SerializeField] private float wallJumpForce = 14f;

        private int jumpsRemaining;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
        [SerializeField] private LayerMask groundLayer;

        [Header("Combat Settings")]
        [SerializeField] private Transform attackPoint;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private int meleeDamage = 1;
        [SerializeField] private float comboWindow = 0.8f;
        [SerializeField] private float attackCooldown = 0.3f;
        [SerializeField] private int rangedCost = 5; // Gold cost per arrow/dagger

        [Header("Ladder Settings")]
        [SerializeField] private float climbSpeed = 4f;

        // References
        private Rigidbody2D rb;
        private HealthSystem healthSystem;
        private Damageable damageable;
        private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteAnimator spriteAnimator;
        [SerializeField] private Animator heroAnimator;
        private int heroAttackIndex = 0;
        private float heroAttackTime = 0f;
        private BoxCollider2D sensorCollider;

        // Layered Equipment SpriteRenderers
        private SpriteRenderer eqHelmet;
        private SpriteRenderer eqShield;
        private SpriteRenderer eqArmor;
        private SpriteRenderer eqGloves;
        private SpriteRenderer eqBoots;
        private SpriteRenderer eqLegs;
        private SpriteRenderer eqKnife;

        // State variables
        private float horizontalInput;
        private float verticalInput;
        private bool isGrounded = false;
        private int groundContacts = 0;
        private bool isClimbing;
        private bool canClimb;
        private int ladderOverlapCount = 0;
        private float targetLadderX = 0f;
        private float originalGravity;
        private float attackCooldownTimer;
        private float comboTimer;
        private int comboStep = 0;
        private bool isBlocking;
        private bool facingRight = true;
        private Transform visualWeaponTransform;
        private Coroutine activeSlashCoroutine;
        private bool isAttacking = false;
        private bool controlsLocked = false;
        private float voidTimer = 0f;

        // Timed Shield variables
        private bool isShieldActive;
        private float shieldTimer;
        private bool isShieldCooldown;
        private float shieldCooldownTimer;
        private float shieldAbsorbRatio;
        private GameObject shieldVisualGo;
        private bool wasBlockPressedLastFrame;

        // Synchronized Equipment Stats
        public int equipmentArmor { get; private set; }
        public float equipmentMoveSpeed { get; private set; }
        public float equipmentAttackSpeed { get; private set; }
        public int equipmentMeleeDamage { get; private set; }
        public int equipmentHeavyDamage { get; private set; }
        public int equipmentRangedDamage { get; private set; }
        public int equipmentMaxHPBonus { get; private set; }
        public float equipmentCritChance { get; private set; }

        [Header("Life System")]
        public float currentHP = 100f;
        public float maxHP = 100f;
        public int extraHearts = 1;
        public bool isInWater = false;
        public bool isInLava = false;

        // Mobile overrides
        private float mobileHorizontalInput;
        private float mobileVerticalInput;
        private bool jumpQueued;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            healthSystem = GetComponent<HealthSystem>();
            damageable = GetComponent<Damageable>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalGravity = rb.gravityScale;

            // Enforce SortingGroup on Player to group body & armor layers, rendering them at sortingOrder 10
            var sg = GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (sg == null) sg = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            sg.sortingOrder = 10;

            if (heroAnimator == null)
            {
                heroAnimator = GetComponentInChildren<Animator>();
            }

            Transform visualT = transform.Find("HeroKnightVisual");
            if (visualT != null)
            {
                visualT.localPosition = new Vector3(0f, -0.7f, 0f);
            }

#if UNITY_EDITOR
            if (heroAnimator == null)
            {
                GameObject heroPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hero Knight - Pixel Art/Demo/HeroKnight.prefab");
                if (heroPrefab != null)
                {
                    GameObject heroVisual = Instantiate(heroPrefab, transform);
                    heroVisual.name = "HeroKnightVisual";
                    heroVisual.transform.localPosition = new Vector3(0f, -0.7f, 0f);
                    
                    var hkScript = heroVisual.GetComponent<HeroKnight>();
                    if (hkScript != null) Destroy(hkScript);

                    var hkRb = heroVisual.GetComponent<Rigidbody2D>();
                    if (hkRb != null) Destroy(hkRb);

                    var hkCol = heroVisual.GetComponent<BoxCollider2D>();
                    if (hkCol != null) Destroy(hkCol);

                    Transform groundSensor = heroVisual.transform.Find("GroundSensor");
                    if (groundSensor != null) groundSensor.gameObject.SetActive(false);
                    
                    for (int i = 1; i <= 2; i++)
                    {
                        Transform wsl = heroVisual.transform.Find("WallSensor_L" + i);
                        if (wsl != null) wsl.gameObject.SetActive(false);
                        Transform wsr = heroVisual.transform.Find("WallSensor_R" + i);
                        if (wsr != null) wsr.gameObject.SetActive(false);
                    }

                    heroAnimator = heroVisual.GetComponent<Animator>();
                }
            }
#endif

            if (heroAnimator != null)
            {
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                if (spriteAnimator != null) spriteAnimator.enabled = false;
            }

            Transform sensorT = transform.Find("GroundCheckSensor");
            if (sensorT != null)
            {
                sensorCollider = sensorT.GetComponent<BoxCollider2D>();
            }

            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.size = new Vector2(0.6f, 1.4f);
                col.offset = new Vector2(0f, -0.05f);
                PhysicsMaterial2D mat = new PhysicsMaterial2D("Frictionless");
                mat.friction = 0f;
                mat.bounciness = 0f;
                col.sharedMaterial = mat;
            }
            if (spriteAnimator == null)
            {
                spriteAnimator = GetComponentInChildren<SpriteAnimator>();
            }
            if (groundCheckPoint == null)
            {
                Transform gc = transform.Find("GroundCheck") ?? transform.Find("GroundCheckPoint");
                if (gc == null)
                {
                    GameObject gcGo = new GameObject("GroundCheck");
                    gcGo.transform.SetParent(transform, false);
                    gcGo.transform.localPosition = new Vector3(0f, -0.76f, 0f);
                    gc = gcGo.transform;
                }
                groundCheckPoint = gc;
            }
            else
            {
                groundCheckPoint.localPosition = new Vector3(0f, -0.76f, 0f);
            }
            if (attackPoint == null)
            {
                Transform wp = transform.Find("Visual_Weapon");
                if (wp == null)
                {
                    GameObject wpGo = new GameObject("Visual_Weapon");
                    wpGo.transform.SetParent(transform, false);
                    wpGo.transform.localPosition = new Vector3(0.5f, 0f, 0f);
                    wp = wpGo.transform;
                }
                attackPoint = wp;
            }
            jumpsRemaining = maxJumps;
            visualWeaponTransform = transform.Find("Visual_Weapon");
        }

        private void Start()
        {
            GameManager.Instance.NotifyPlayerSpawned();

            // Apply Shop Upgrades
            int atkUpgrade = PlayerPrefs.GetInt("Pulsevania_ATKUpgrade", 0);
            meleeDamage = 1 + atkUpgrade;

            // Initialize HP and ExtraHearts strictly
            currentHP = 100f;
            maxHP = 100f;
            extraHearts = 1;

            InitializeEquipmentRenderers();

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.LocatePlayerVisuals();
            }

            UpdateHealthUI();
            UpdateHeartsUI();
        }

        private void OnDestroy()
        {
        }

        private void HandleDamage(int damage)
        {
            if (heroAnimator != null)
            {
                heroAnimator.SetTrigger("Hurt");
            }
            else if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Hurt, true);
            }
        }

        public void TakeDamage(float amount)
        {
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";

            if (isShieldActive)
            {
                // Reduce damage based on shield absorb ratio
                amount *= (1f - shieldAbsorbRatio);
                
                if (DamageTextPool.Instance != null)
                {
                    int absorbPercent = Mathf.RoundToInt(shieldAbsorbRatio * 100f);
                    int takenPercent = 100 - absorbPercent;
                    string blockMsg = isTR ? $"Absorbe! -%{absorbPercent} (%{takenPercent} Alındı)" : $"Absorbed! -{absorbPercent}% ({takenPercent}% Taken)";
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.5f, blockMsg, Color.cyan);
                }
            }
            else if (isBlocking)
            {
                bool hasShield = false;
                if (InventoryManager.Instance != null && InventoryManager.Instance.equippedItems.ContainsKey(EquipSlot.Shield))
                {
                    hasShield = (InventoryManager.Instance.equippedItems[EquipSlot.Shield] != null);
                }

                if (hasShield)
                {
                    amount *= 0.2f;
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Engellendi! -80%" : "Blocked! -80%", Color.cyan);
                    }
                }
                else
                {
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Kalkan Yok!" : "No Shield!", Color.red);
                    }
                }
            }

            currentHP -= amount;
            if (currentHP < 0) currentHP = 0;

            UpdateHealthUI();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.DamageTaken);
            }

            if (heroAnimator != null)
            {
                heroAnimator.SetTrigger("Hurt");
            }
            else if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Hurt, true);
            }

            if (currentHP <= 0)
            {
                HandlePlayerDeathCondition();
            }
            else
            {
                if (healthSystem != null)
                {
                    healthSystem.TriggerInvulnerability();
                }
            }
        }

        private void HandlePlayerDeathCondition()
        {
            if (extraHearts > 0)
            {
                // Trigger Save Point / Revive mechanic instantly on the spot
                extraHearts--;
                currentHP = maxHP;
                UpdateHealthUI();
                UpdateHeartsUI(); // Re-render heart grid icons (removes 1 heart sprite)
                if (healthSystem != null)
                {
                    healthSystem.TriggerInvulnerability();
                }
                Debug.Log("[Pulsevania Combat] Player avoided death! 1 Extra Heart consumed. Invulnerability triggered. HP restored to " + maxHP);
            }
            else
            {
                Debug.Log("[Pulsevania Combat] Game Over! No hearts left.");
                // Trigger standard Game Over scene/panel logic
                HandleDeath();
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0 || currentHP <= 0 || currentHP >= maxHP) return;
            currentHP = Mathf.Min(maxHP, currentHP + amount);
            UpdateHealthUI();
        }

        public void UpdateHealthUI()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateHealthUI(currentHP, maxHP);
            }
        }

        public void UpdateHeartsUI()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateExtraHeartsUI(extraHearts);
            }
        }

        private void Update()
        {
            // Update Shield Timers
            if (isShieldActive)
            {
                shieldTimer -= Time.deltaTime;
                if (shieldTimer <= 0)
                {
                    isShieldActive = false;
                    DestroyShieldVisual();
                    
                    isShieldCooldown = true;
                    shieldCooldownTimer = 60f; // 60 seconds cooldown
                    
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.5f, isTR ? "Kalkan Bitti!" : "Shield Expired!", Color.yellow);
                    }
                }
            }
            else if (isShieldCooldown)
            {
                shieldCooldownTimer -= Time.deltaTime;
                if (shieldCooldownTimer <= 0)
                {
                    isShieldCooldown = false;
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.5f, isTR ? "Kalkan Hazır!" : "Shield Ready!", Color.green);
                    }
                }
            }

            if (controlsLocked)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Gameplay)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            // Real-time 5 seconds void death check
            if (currentHP > 0f && MapManager.Instance != null)
            {
                int currentRoomId = MapManager.Instance.GetCurrentRoomId();
                int index = currentRoomId - 1;
                int row = index / 10;
                float floorY = MapManager.Instance.originY + (4 - row) * MapManager.Instance.roomHeight + MapManager.Instance.roomHeight / 2f - 2f;

                if (transform.position.y < floorY - 12f)
                {
                    voidTimer += Time.deltaTime;
                    if (voidTimer >= 5f)
                    {
                        voidTimer = 0f;
                        InstantVoidDeath();
                    }
                }
                else
                {
                    voidTimer = 0f;
                }
            }

            Vector2 checkCenter = new Vector2(transform.position.x, transform.position.y - 0.76f);
            Vector2 checkSize = new Vector2(0.6f, 0.1f);
            Collider2D[] results = Physics2D.OverlapBoxAll(checkCenter, checkSize, 0f);

            bool touchingSolid = false;
            foreach (var col in results)
            {
                if (col != null && !col.isTrigger && col.transform.root != transform.root)
                {
                    touchingSolid = true;
                    break;
                }
            }
            isGrounded = touchingSolid;

            if (isGrounded)
            {
                jumpsRemaining = maxJumps;
            }

            if (currentHP > 0 && currentHP <= maxHP * 0.2f)
            {
                float pingPong = Mathf.PingPong(Time.time * 5f, 1f);
                spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f, 1f), pingPong);
            }
            else
            {
                spriteRenderer.color = Color.white;
            }

            if ((isInWater || isInLava) && (healthSystem == null || !healthSystem.IsInvulnerable))
            {
                int currentRoomId = MapManager.Instance != null ? MapManager.Instance.GetCurrentRoomId() : 1;
                float baseDamagePerSec = 3f;
                float finalHazardDps = baseDamagePerSec + (currentRoomId - 1) * 0.3f;

                currentHP -= finalHazardDps * Time.deltaTime;
                if (currentHP < 0) currentHP = 0;
                UpdateHealthUI();
                if (currentHP <= 0)
                {
                    HandlePlayerDeathCondition();
                }
            }

            if (UIManager.Instance != null && UIManager.Instance.IsShopOpen())
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

            // Keyboard inputs (New Input System support)
            float keyboardH = 0f;
            float keyboardV = 0f;
            bool spacePressed = false;
            bool attackPressed = false;
            bool shootPressed = false;
            bool isBlockPressed = false;

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keyboardH -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keyboardH += 1f;
                
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keyboardV += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keyboardV -= 1f;

                spacePressed = kb.spaceKey.wasPressedThisFrame;
                attackPressed = kb.fKey.wasPressedThisFrame;
                shootPressed = kb.gKey.wasPressedThisFrame;
                isBlockPressed = kb.leftShiftKey.isPressed;
            }

            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse.leftButton.wasPressedThisFrame) attackPressed = true;
                if (mouse.rightButton.wasPressedThisFrame) shootPressed = true;
            }

            // Check if pointer is over UI to block mouse/touch clicks from triggering attacks in background
            bool isPointerOverUI = false;
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    isPointerOverUI = true;
                }
                else if (UnityEngine.Input.touchCount > 0)
                {
                    var touch = UnityEngine.Input.GetTouch(0);
                    if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    {
                        isPointerOverUI = true;
                    }
                }
            }

            if (isPointerOverUI)
            {
                attackPressed = false;
                shootPressed = false;
            }

            if (UIManager.Instance != null && (UIManager.Instance.IsShopOpen() || UIManager.Instance.IsInventoryOpen() || UIManager.Instance.IsWorldMapOpen()))
            {
                attackPressed = false;
                shootPressed = false;
                spacePressed = false;
                keyboardH = 0f;
                keyboardV = 0f;
                mobileHorizontalInput = 0f;
                mobileVerticalInput = 0f;
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }

            // Combine keyboard and mobile inputs
            float targetH = Mathf.Abs(keyboardH) > 0.01f ? keyboardH : mobileHorizontalInput;
            float targetV = Mathf.Abs(keyboardV) > 0.01f ? keyboardV : mobileVerticalInput;

            // Smoothly interpolate inputs to make touch/keyboard movement feel smoother and prevent instant stutters
            horizontalInput = Mathf.MoveTowards(horizontalInput, targetH, Time.deltaTime * 12f);
            verticalInput = Mathf.MoveTowards(verticalInput, targetV, Time.deltaTime * 12f);

            // Combat cooldowns
            if (attackCooldownTimer > 0)
                attackCooldownTimer -= Time.deltaTime;

            if (comboTimer > 0)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0)
                {
                    comboStep = 0;
                }
            }

            // Action triggers
            if (spacePressed)
            {
                TriggerJump();
            }

            if (attackPressed)
            {
                TriggerAttack();
            }

            if (shootPressed)
            {
                TriggerRanged();
            }

            bool isBlockInput = isBlockPressed;
            if (UIManager.Instance != null && UIManager.Instance.IsBlockButtonPressed())
            {
                isBlockInput = true;
            }
            SetBlocking(isBlockInput);

            // Ladder Climb Enter Trigger
            if (canClimb && !isClimbing && Mathf.Abs(verticalInput) > 0.1f)
            {
                StartClimbing();
            }

            // Exit climbing if no longer can climb or jumps
            if (isClimbing && !canClimb)
            {
                StopClimbing();
            }

            // Character Flips
            if (!isBlocking)
            {
                if (horizontalInput > 0.1f && !facingRight)
                {
                    Flip();
                }
                else if (horizontalInput < -0.1f && facingRight)
                {
                    Flip();
                }
            }

            // Update animator state
            if (heroAnimator != null)
            {
                heroAnimator.SetBool("Grounded", isGrounded);
                heroAnimator.SetFloat("AirSpeedY", rb.linearVelocity.y);
                heroAnimator.SetBool("IdleBlock", isBlocking);

                int stateValue = 0; // 0 = Idle, 1 = Run
                if (!isBlocking && !isClimbing)
                {
                    if (Mathf.Abs(horizontalInput) > 0.05f)
                    {
                        stateValue = 1;
                    }
                }
                else if (isClimbing && (Mathf.Abs(verticalInput) > 0.05f || Mathf.Abs(horizontalInput) > 0.05f))
                {
                    stateValue = 1;
                }
                heroAnimator.SetInteger("AnimState", stateValue);
            }
            else if (spriteAnimator != null)
            {
                if (isBlocking)
                {
                    spriteAnimator.PlayState(AnimState.Idle);
                }
                else if (isClimbing)
                {
                    if (Mathf.Abs(verticalInput) > 0.1f || Mathf.Abs(horizontalInput) > 0.1f)
                        spriteAnimator.PlayState(AnimState.Walk);
                    else
                        spriteAnimator.PlayState(AnimState.Idle);
                }
                else
                {
                    if (!isGrounded)
                    {
                        spriteAnimator.PlayState(AnimState.Jump);
                    }
                    else if (Mathf.Abs(horizontalInput) > 0.1f)
                    {
                        spriteAnimator.PlayState(AnimState.Walk);
                    }
                    else
                    {
                        spriteAnimator.PlayState(AnimState.Idle);
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (controlsLocked) return;

            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Gameplay) return;

            if (isClimbing)
            {
                jumpsRemaining = maxJumps;
            }

            if (isInWater || isInLava)
            {
                rb.gravityScale = originalGravity * 0.25f;
                float currentMoveSpeed = moveSpeed * (1f + equipmentMoveSpeed) * 0.5f;
                
                float swimY = -1.2f; // negative buoyancy pull
                if (verticalInput > 0.1f)
                {
                    swimY = 3.5f; // upward swim
                }
                rb.linearVelocity = new Vector2(horizontalInput * currentMoveSpeed, swimY);
                jumpsRemaining = maxJumps;
            }
            else if (isClimbing)
            {
                rb.gravityScale = 0f;
                float currentMoveSpeed = moveSpeed * (1f + equipmentMoveSpeed);
                rb.linearVelocity = new Vector2(horizontalInput * currentMoveSpeed, verticalInput * climbSpeed);
                
                // If there is no horizontal input, keep the player centered on the ladder to prevent wall collision bugs
                if (Mathf.Abs(horizontalInput) < 0.05f)
                {
                    transform.position = new Vector3(Mathf.Lerp(transform.position.x, targetLadderX, Time.fixedDeltaTime * 15f), transform.position.y, transform.position.z);
                }
            }
            else
            {
                rb.gravityScale = originalGravity;
                if (isBlocking)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
                else
                {
                    float currentMoveSpeed = moveSpeed * (1f + equipmentMoveSpeed);
                    rb.linearVelocity = new Vector2(horizontalInput * currentMoveSpeed, rb.linearVelocity.y);
                }
            }
                bool jumpHeld = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed;
                if (rb.linearVelocity.y < 0)
                {
                    rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
                }
                else if (rb.linearVelocity.y > 0 && !jumpHeld)
                {
                    rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
                }
        }

        // --- PUBLIC INPUT INTERFACES FOR MOBILE ---

        public void SetHorizontalInput(float val)
        {
            mobileHorizontalInput = val;
        }

        public void SetVerticalInput(float val)
        {
            mobileVerticalInput = val;
        }

        public void TriggerJump()
        {
            if (isBlocking) return;

            if (isInWater || isInLava)
            {
                if (MapManager.Instance != null)
                {
                    int currentRoomId = MapManager.Instance.GetCurrentRoomId();
                    int index = currentRoomId - 1;
                    int row = index / 10;
                    float floorY = MapManager.Instance.originY + (4 - row) * MapManager.Instance.roomHeight + MapManager.Instance.roomHeight / 2f - 2f;
                    
                    if (transform.position.y >= floorY - 0.5f)
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                        rb.AddForce(Vector2.up * jumpForce * 1.2f, ForceMode2D.Impulse);
                        if (heroAnimator != null)
                        {
                            heroAnimator.SetTrigger("Jump");
                            heroAnimator.SetBool("Grounded", false);
                        }
                        else if (spriteAnimator != null) spriteAnimator.PlayState(AnimState.Jump);
                        if (DamageTextPool.Instance != null)
                        {
                            DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, "ESCAPE JUMP!", Color.green);
                        }
                        return;
                    }
                }
            }

            // Check wall jump first
            bool wallLeft = Physics2D.OverlapBox((Vector2)transform.position + new Vector2(-0.55f, 0f), new Vector2(0.1f, 1.2f), 0f, groundLayer);
            bool wallRight = Physics2D.OverlapBox((Vector2)transform.position + new Vector2(0.55f, 0f), new Vector2(0.1f, 1.2f), 0f, groundLayer);

            if ((wallLeft || wallRight) && !isGrounded && !isClimbing)
            {
                // Wall Jump - jump away from the wall and HIGHER!
                float pushDir = wallLeft ? 1f : -1f;
                rb.linearVelocity = new Vector2(pushDir * moveSpeed * 1.1f, wallJumpForce);

                // Force flip towards jump direction
                if (pushDir > 0 && !facingRight) Flip();
                else if (pushDir < 0 && facingRight) Flip();

                jumpsRemaining = maxJumps - 1;
                if (heroAnimator != null)
                {
                    heroAnimator.SetTrigger("Jump");
                    heroAnimator.SetBool("Grounded", false);
                }
                else if (spriteAnimator != null) spriteAnimator.PlayState(AnimState.Jump);
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, "Wall Jump!", Color.cyan);
                return;
            }

            if (isGrounded && !isClimbing)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpsRemaining = maxJumps - 1;
                isGrounded = false;
                groundContacts = 0;
                if (heroAnimator != null)
                {
                    heroAnimator.SetTrigger("Jump");
                    heroAnimator.SetBool("Grounded", false);
                }
                else if (spriteAnimator != null) spriteAnimator.PlayState(AnimState.Jump);
            }
            else if (jumpsRemaining > 0 && !isClimbing)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
                jumpsRemaining = 0;
                isGrounded = false;
                groundContacts = 0;
                if (heroAnimator != null)
                {
                    heroAnimator.SetTrigger("Jump");
                    heroAnimator.SetBool("Grounded", false);
                }
                else if (spriteAnimator != null) spriteAnimator.PlayState(AnimState.Jump);
                if (DamageTextPool.Instance != null)
                {
                    bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Çift Zıplama!" : "Double Jump!", Color.yellow);
                }
            }
            else if (isClimbing)
            {
                StopClimbing();
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * 0.8f);
                jumpsRemaining = maxJumps - 1;
                if (heroAnimator != null)
                {
                    heroAnimator.SetTrigger("Jump");
                    heroAnimator.SetBool("Grounded", false);
                }
            }
        }

        private IEnumerator AttackVisualFeedback()
        {
            if (spriteRenderer != null)
            {
                Color originalColor = spriteRenderer.color;
                spriteRenderer.color = Color.yellow; // Split-second flash feedback
                yield return new WaitForSeconds(0.12f);
                spriteRenderer.color = originalColor;
            }
        }

        public void TriggerAttack()
        {
            Debug.Log($"[Pulsevania] TriggerAttack called. CooldownTimer: {attackCooldownTimer}, isBlocking: {isBlocking}, isClimbing: {isClimbing}, isAttacking: {isAttacking}");
            if (attackCooldownTimer > 0 || isBlocking || isClimbing || isAttacking) return;

            // Attack combo step logic
            attackCooldownTimer = attackCooldown;

            if (heroAnimator != null)
            {
                // 3-step attack combo logic
                if (Time.time - heroAttackTime > comboWindow)
                {
                    heroAttackIndex = 1;
                }
                else
                {
                    heroAttackIndex++;
                    if (heroAttackIndex > 3)
                    {
                        heroAttackIndex = 1;
                    }
                }
                heroAttackTime = Time.time;
                heroAnimator.SetTrigger("Attack" + heroAttackIndex);
                comboStep = heroAttackIndex;
            }
            else
            {
                comboTimer = comboWindow;
                comboStep = (comboStep == 0) ? 1 : 2;
                if (spriteAnimator != null)
                {
                    Debug.Log("[Pulsevania] Triggering Attack Animation state.");
                    spriteAnimator.PlayState(AnimState.Attack, true);
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.SwordSwing);
            }

            // Animate equipped weapon in a slash arc
            if (!isAttacking)
            {
                activeSlashCoroutine = StartCoroutine(WeaponSlashRoutine());
            }

            StartCoroutine(AttackVisualFeedback());
            PerformMeleeAttack(comboStep);

            if (heroAnimator == null && comboStep == 2)
            {
                comboStep = 0; // reset
            }
        }

        public int knifeAmmo = 0;

        public void TriggerRanged()
        {
            if (isBlocking || isClimbing) return;

            if (InventoryManager.Instance == null) return;

            string lang = PlayerPrefs.GetString("GameLanguage", "Turkish");
            bool isTR = lang == "Turkish";

            ItemData equippedKnife = InventoryManager.Instance.equippedItems.ContainsKey(EquipSlot.ThrowingKnife) ? InventoryManager.Instance.equippedItems[EquipSlot.ThrowingKnife] : null;
            if (equippedKnife == null)
            {
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Bıçak Yok!" : "No Knives Equipped!", Color.red);
                }
                return;
            }

            if (knifeAmmo <= 0)
            {
                InventoryManager.Instance.equippedItems[EquipSlot.ThrowingKnife] = null;
                InventoryManager.Instance.UpdateVisualEquipment();
                if (UIManager.Instance != null) UIManager.Instance.UpdateInventoryUI();
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Bıçak Yok!" : "No Knives!", Color.red);
                }
                return;
            }

            // Decrement ammo
            knifeAmmo--;

            // Spawn projectile
            SpawnRangedProjectile();

            // Display remaining ammo
            if (DamageTextPool.Instance != null)
            {
                string msg = isTR ? $"{knifeAmmo} Bıçak Kaldı!" : $"{knifeAmmo} Knives Left!";
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, msg, Color.cyan);
            }

            // If ammo is depleted, destroy/unequip the knife
            if (knifeAmmo <= 0)
            {
                InventoryManager.Instance.equippedItems[EquipSlot.ThrowingKnife] = null;
                InventoryManager.Instance.UpdateVisualEquipment();
                if (UIManager.Instance != null) UIManager.Instance.UpdateInventoryUI();
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Bıçak Tükendi!" : "Knives Depleted!", Color.red);
                }
            }
        }

        public void SetBlocking(bool block)
        {
            if (block && !wasBlockPressedLastFrame)
            {
                TryActivateShield();
            }
            wasBlockPressedLastFrame = block;
            isBlocking = block;
        }

        private float nextShieldCooldownWarningTime = 0f;

        private void TryActivateShield()
        {
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";

            // 1. Check if shield is already active
            if (isShieldActive) return;

            // 2. Check if on cooldown
            if (isShieldCooldown)
            {
                if (Time.time >= nextShieldCooldownWarningTime)
                {
                    nextShieldCooldownWarningTime = Time.time + 1.5f; // Limit warnings to 1.5s intervals
                    if (DamageTextPool.Instance != null)
                    {
                        string cooldownMsg = isTR ? $"Kalkan Bekleme Süresinde! ({Mathf.CeilToInt(shieldCooldownTimer)}s)" : $"Shield on Cooldown! ({Mathf.CeilToInt(shieldCooldownTimer)}s)";
                        DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.5f, cooldownMsg, Color.yellow);
                    }
                }
                return;
            }

            // 3. Check equipped shield
            if (InventoryManager.Instance == null) return;

            ItemData equippedShield = InventoryManager.Instance.equippedItems.ContainsKey(EquipSlot.Shield) ? InventoryManager.Instance.equippedItems[EquipSlot.Shield] : null;

            if (equippedShield == null)
            {
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.5f, isTR ? "Kalkan Kuşanılmadı!" : "No Shield Equipped!", Color.red);
                }
                return;
            }

            // Determine shield stats
            float duration = 10f;
            float absorb = 0.2f; // %20 absorb, %80 damage taken

            if (equippedShield.itemName.Contains("Bronze Shield") || equippedShield.itemName.Contains("Bronz Kalkan"))
            {
                duration = 10f;
                absorb = 0.2f;
            }
            else if (equippedShield.itemName.Contains("Silver Shield") || equippedShield.itemName.Contains("Gümüş Kalkan"))
            {
                duration = 20f;
                absorb = 0.4f;
            }
            else if (equippedShield.itemName.Contains("Gold Shield") || equippedShield.itemName.Contains("Altın Kalkan"))
            {
                duration = 30f;
                absorb = 0.6f;
            }
            else
            {
                duration = 10f;
                absorb = 0.2f;
            }

            // Activate shield
            isShieldActive = true;
            shieldTimer = duration;
            shieldAbsorbRatio = absorb;

            // Spawn visual
            SpawnShieldVisual();

            if (DamageTextPool.Instance != null)
            {
                string activeMsg = isTR ? $"Kalkan Aktif! ({duration}s)" : $"Shield Active! ({duration}s)";
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.5f, activeMsg, Color.cyan);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(SoundEffect.CoinPickup);
            }
        }

        private void SpawnShieldVisual()
        {
            if (shieldVisualGo != null) Destroy(shieldVisualGo);

            shieldVisualGo = new GameObject("ShieldActiveVisual");
            shieldVisualGo.transform.SetParent(transform, false);
            shieldVisualGo.transform.localPosition = new Vector3(0f, 0.2f, -0.1f); // Centers nicely relative to player

            var sr = shieldVisualGo.AddComponent<SpriteRenderer>();
            sr.sprite = CreateShieldCircleSprite();
            sr.sortingOrder = 100;
        }

        private Sprite CreateShieldCircleSprite()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size * 0.46f;
            float thickness = 1.5f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float distDiff = Mathf.Abs(dist - radius);
                    if (distDiff <= thickness)
                    {
                        float alphaFactor = 1f - (distDiff / thickness);
                        float edgeAlpha = 0.45f * alphaFactor;
                        tex.SetPixel(x, y, new Color(0.2f, 0.8f, 1f, edgeAlpha));
                    }
                    else if (dist < radius - thickness)
                    {
                        tex.SetPixel(x, y, new Color(0.1f, 0.5f, 0.9f, 0.03f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        private void DestroyShieldVisual()
        {
            if (shieldVisualGo != null)
            {
                Destroy(shieldVisualGo);
                shieldVisualGo = null;
            }
        }

        // --- INTERNAL ACTIONS ---

        private void PerformMeleeAttack(int step)
        {
            // Apply reach (Spear weapons add +1 to attackRange)
            float currentRange = attackRange;
            if (InventoryManager.Instance != null)
            {
                ItemData weapon = InventoryManager.Instance.equippedItems.ContainsKey(EquipSlot.Weapon) ? InventoryManager.Instance.equippedItems[EquipSlot.Weapon] : null;
                if (weapon != null && weapon.itemName.Contains("Spear"))
                {
                    currentRange += 1.0f;
                }
            }

            // Perform overlap circle/box for hitbox check
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, currentRange, enemyLayer);
            Debug.Log($"[Pulsevania] PerformMeleeAttack: Overlap detected {hitEnemies.Length} enemies on layer mask.");

            // Calculate weapon specific damage (Axes use heavy damage, others melee damage)
            bool isAxe = false;
            if (InventoryManager.Instance != null)
            {
                ItemData weapon = InventoryManager.Instance.equippedItems.ContainsKey(EquipSlot.Weapon) ? InventoryManager.Instance.equippedItems[EquipSlot.Weapon] : null;
                if (weapon != null && weapon.itemName.Contains("Axe"))
                {
                    isAxe = true;
                }
            }
            int bonus = isAxe ? equipmentHeavyDamage : equipmentMeleeDamage;
            int dmg = (meleeDamage + bonus) * step;

            // Kritik hasar hesabı
            bool isCrit = false;
            float finalDmg = dmg;
            if (Random.value < equipmentCritChance)
            {
                isCrit = true;
                finalDmg *= 2f; // Kritik hasar 2 katı
            }

            foreach (Collider2D enemy in hitEnemies)
            {
                PulsevaniaChest realChest = enemy.GetComponent<PulsevaniaChest>();
                if (realChest != null)
                {
                    realChest.TakeDamage(1);
                    continue;
                }

                KeyChest kChest = enemy.GetComponent<KeyChest>();
                if (kChest != null)
                {
                    kChest.TakeDamage(1);
                    continue;
                }

                Damageable enemyDmg = enemy.GetComponent<Damageable>();
                if (enemyDmg != null)
                {
                    enemyDmg.Damage((int)finalDmg, Team.Player);
                    Color dmgColor = isCrit ? new Color(1f, 0.3f, 0f) : Color.yellow; // Turuncu/Kırmızımsı kritik
                    string textToShow = isCrit ? $"{(int)finalDmg} CRIT!" : ((int)finalDmg).ToString();
                    DamageTextPool.Instance.SpawnText(enemy.transform.position + Vector3.up, textToShow, dmgColor);
                }
            }

            // Also support hitting/opening loot chests with attacks (Forgotten Warrior style UX)
            Collider2D[] hitInteractables = Physics2D.OverlapCircleAll(attackPoint.position, currentRange);
            Debug.Log($"[Pulsevania] PerformMeleeAttack: Overlap detected {hitInteractables.Length} total objects in attack range.");
            foreach (Collider2D coll in hitInteractables)
            {
                PulsevaniaChest chest = coll.GetComponent<PulsevaniaChest>();
                if (chest != null)
                {
                    Debug.Log("[Pulsevania] Melee attack hit a real loot chest. Opening it...");
                    chest.TakeDamage(1);
                    continue;
                }

                KeyChest kChest = coll.GetComponent<KeyChest>();
                if (kChest != null)
                {
                    Debug.Log("[Pulsevania] Melee attack hit a key chest. Damaging it...");
                    kChest.TakeDamage(1);
                    continue;
                }

                Damageable dmgable = coll.GetComponent<Damageable>();
                if (dmgable != null && dmgable.Team == Team.Enemy)
                {
                    dmgable.Damage((int)finalDmg, Team.Player);
                    Color dmgColor = isCrit ? new Color(1f, 0.3f, 0f) : Color.yellow;
                    string textToShow = isCrit ? $"{(int)finalDmg} CRIT!" : ((int)finalDmg).ToString();
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(coll.transform.position + Vector3.up, textToShow, dmgColor);
                    }
                }
            }
        }

        private void SpawnRangedProjectile()
        {
            if (InventoryManager.Instance == null) return;

            ItemData knife = InventoryManager.Instance.equippedItems.ContainsKey(EquipSlot.ThrowingKnife) ? InventoryManager.Instance.equippedItems[EquipSlot.ThrowingKnife] : null;
            if (knife == null)
            {
                bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
                DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Bıçak Kuşanılmadı!" : "No Knife Equipped!", Color.red);
                return;
            }

            Vector2 dir = facingRight ? Vector2.right : Vector2.left;
            int totalDamage = 1 + equipmentRangedDamage; // base ranged damage 1 + knife stats

            // Target lock / aim assist on ground
            if (isGrounded)
            {
                Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, 12f, enemyLayer);
                float closestDist = float.MaxValue;
                Transform closestEnemy = null;

                foreach (var col in hitEnemies)
                {
                    float dist = Vector2.Distance(transform.position, col.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestEnemy = col.transform;
                    }
                }

                if (closestEnemy != null)
                {
                    Vector3 targetPos = closestEnemy.position;
                    var col2d = closestEnemy.GetComponent<Collider2D>();
                    if (col2d != null)
                    {
                        targetPos = col2d.bounds.center;
                    }
                    dir = (targetPos - attackPoint.position).normalized;
                }
            }

            // Bıçak için kritik vuruş hesabı
            bool isCrit = false;
            float finalDamage = totalDamage;

            if (!isGrounded) // Air throw guarantees a 100% Critical Hit
            {
                isCrit = true;
                finalDamage *= 2f;
            }
            else
            {
                // Ground throw uses regular crit chance
                if (Random.value < equipmentCritChance)
                {
                    isCrit = true;
                    finalDamage *= 2f;
                }
            }

            ProjectilePool.Instance.SpawnProjectile(attackPoint.position, dir, Team.Player, (int)finalDamage, isCrit);
        }

        private void StartClimbing()
        {
            isClimbing = true;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            // Snap player's X position to the center of the ladder to prevent wall collision bugs
            transform.position = new Vector3(targetLadderX, transform.position.y, transform.position.z);
        }

        private void StopClimbing()
        {
            isClimbing = false;
            rb.gravityScale = originalGravity;
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
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
            if (heroAnimator != null)
            {
                heroAnimator.SetTrigger("Death");
            }
            else if (spriteAnimator != null)
            {
                spriteAnimator.PlayState(AnimState.Death, true);
            }
            GameManager.Instance.TriggerPlayerDeath();
        }

        private void InstantVoidDeath()
        {
            extraHearts = 0;
            currentHP = 0f;
            UpdateHealthUI();
            UpdateHeartsUI();
            HandleDeath();
            Debug.Log("[Pulsevania] Player fell into the void and died after 5 seconds!");
        }

        // --- LADDER TRIGGER DETECTION ---

        public void SetCanClimb(bool climbState, float ladderX = 0f)
        {
            if (climbState)
            {
                ladderOverlapCount++;
                targetLadderX = ladderX;
            }
            else
            {
                ladderOverlapCount = Mathf.Max(0, ladderOverlapCount - 1);
            }

            canClimb = (ladderOverlapCount > 0);

            if (!canClimb && isClimbing)
            {
                StopClimbing();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheckPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(groundCheckPoint.position, groundCheckSize);
            }

            if (attackPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(attackPoint.position, attackRange);
            }
        }

        private void SpawnSlashParticle(Vector3 position, Color color, float size = 0.2f)
        {
            GameObject p = new GameObject("SlashParticle");
            p.transform.position = position;
            p.transform.localScale = new Vector3(size, size, 1f);

            SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSlashParticleSprite();
            sr.color = color;
            sr.sortingOrder = 20; // Render on top of player (group order 10)

            p.AddComponent<FadeOutDestroy>().Initialize(0.2f);
        }

        private Sprite cachedParticleSprite;
        private Sprite CreateSlashParticleSprite()
        {
            if (cachedParticleSprite != null) return cachedParticleSprite;

            Texture2D tex = new Texture2D(8, 8);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(3.5f, 3.5f));
                    if (dist < 3.5f)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            cachedParticleSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
            return cachedParticleSprite;
        }

        private IEnumerator WeaponSlashRoutine()
        {
            if (visualWeaponTransform == null) yield break;

            isAttacking = true;
            Transform weaponTx = visualWeaponTransform;

            string weaponName = "";
            if (InventoryManager.Instance != null)
            {
                ItemData weapon = InventoryManager.Instance.equippedItems.ContainsKey(EquipSlot.Weapon) ? InventoryManager.Instance.equippedItems[EquipSlot.Weapon] : null;
                if (weapon != null)
                {
                    weaponName = weapon.itemName;
                }
            }

            Color trailColor = new Color(1f, 1f, 1f, 0.6f);
            bool spawnSparks = false;

            if (weaponName.Contains("Gold"))
            {
                trailColor = new Color(1f, 0.85f, 0.1f, 0.9f); // Golden glow
                spawnSparks = true;
            }
            else if (weaponName.Contains("Silver"))
            {
                trailColor = new Color(0.6f, 0.8f, 1f, 0.9f); // Steel Blue specular
            }
            else if (weaponName.Contains("Bronze"))
            {
                trailColor = new Color(0.85f, 0.4f, 0.15f, 0.9f); // Bronze copper
            }

            float elapsed = 0f;
            float duration = 0.12f; // Fast, responsive action game swing
            Quaternion startRot = Quaternion.identity;
            Quaternion targetRot = Quaternion.Euler(0f, 0f, -90f);

            // Smoothly and rapidly rotate weapon from 0 to -90 degrees
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float pct = elapsed / duration;
                weaponTx.localRotation = Quaternion.Slerp(startRot, targetRot, pct);

                // Spawn trails at the tip of the weapon
                Vector3 localTip = new Vector3(0f, 1.0f, 0f); // weapon length is approx 1 unit
                Vector3 tipPos = weaponTx.position + weaponTx.rotation * localTip;

                SpawnSlashParticle(tipPos, trailColor, 0.25f);
                if (spawnSparks && UnityEngine.Random.value < 0.4f)
                {
                    SpawnSlashParticle(tipPos + (Vector3)UnityEngine.Random.insideUnitCircle * 0.15f, Color.white, 0.1f);
                }

                yield return null;
            }

            // Quick reset back to baseline guard position
            elapsed = 0f;
            while (elapsed < 0.05f)
            {
                elapsed += Time.deltaTime;
                weaponTx.localRotation = Quaternion.Slerp(targetRot, startRot, elapsed / 0.05f);
                yield return null;
            }

            weaponTx.localRotation = Quaternion.identity;
            isAttacking = false;
        }

        public void SyncEquipmentStats(int maxHPBonus, float moveSpeed, float attackSpeed, int meleeDmg, int heavyDmg, int rangedDmg, float critChance)
        {
            equipmentArmor = 0; // Artık zırhlar Armor vermiyor!
            equipmentMoveSpeed = moveSpeed;
            equipmentAttackSpeed = attackSpeed;
            equipmentMeleeDamage = meleeDmg;
            equipmentHeavyDamage = heavyDmg;
            equipmentRangedDamage = rangedDmg;
            equipmentMaxHPBonus = maxHPBonus;
            equipmentCritChance = critChance;

            // MaxHP guncellemesi
            float oldMaxHP = maxHP;
            maxHP = 100f + maxHPBonus;

            if (maxHP != oldMaxHP)
            {
                if (currentHP > maxHP)
                {
                    currentHP = maxHP;
                }
                else if (maxHP > oldMaxHP)
                {
                    currentHP += (maxHP - oldMaxHP);
                }
                UpdateHealthUI();
            }

            // Attack speed reduction
            attackCooldown = 0.3f / (1f + equipmentAttackSpeed);
        }

        private void InitializeEquipmentRenderers()
        {
            eqHelmet = FindOrCreateEquipRenderer("Equip_Helmet", "Visual_Head", new Vector3(0f, 0.3f, 0f), 2);
            eqShield = FindOrCreateEquipRenderer("Visual_Shield", "Visual_Shield", new Vector3(-0.22f, -0.15f, 0f), 49);
            eqArmor = FindOrCreateEquipRenderer("Equip_Armor", "Visual_Chest", new Vector3(0f, 0.05f, 0f), 2);
            eqGloves = FindOrCreateEquipRenderer("Equip_Gloves", "Visual_Hands", new Vector3(0f, 0f, 0f), 2);
            eqBoots = FindOrCreateEquipRenderer("Equip_Boots", "Visual_Feet", new Vector3(0f, -0.38f, 0f), 2);
            eqLegs = FindOrCreateEquipRenderer("Visual_Legs", "Visual_Legs", new Vector3(0f, -0.2f, 0f), 2);
            eqKnife = FindOrCreateEquipRenderer("Visual_ThrowingKnife", "Visual_ThrowingKnife", new Vector3(-0.22f, -0.15f, 0f), 2);

            if (visualWeaponTransform != null)
            {
                visualWeaponTransform.localPosition = new Vector3(0.22f, -0.15f, 0f);
            }
        }

        private SpriteRenderer FindOrCreateEquipRenderer(string primaryName, string fallbackName, Vector3 defaultLocalPos, int sortingOrder)
        {
            Transform t = transform.Find(primaryName) ?? transform.Find(fallbackName);
            if (t == null)
            {
                GameObject go = new GameObject(primaryName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = defaultLocalPos;
                t = go.transform;
            }
            else
            {
                t.transform.localPosition = defaultLocalPos; // Force reset to correct local position on scene load
            }
            t.gameObject.name = primaryName;

            SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private void LateUpdate()
        {
            SyncEquipmentBobs();
        }

        private void SyncEquipmentBobs()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null) return;

            string spriteName = spriteRenderer.sprite.name;

            float yBob = 0f;
            float xBob = 0f;

            float helmetY = 0.3f;
            float armorY = 0.05f;
            float glovesY = 0f;
            float legsY = -0.2f;
            float bootsY = -0.38f;

            if (spriteName.Contains("Idle_1") || spriteName.Contains("Walk_1"))
            {
                // Bob down by 1 pixel in secondary walk/idle frames
                yBob = -0.0625f;
                // Feet stay on floor, do not bob down
                bootsY = -0.38f;
            }
            else if (spriteName.Contains("Jump"))
            {
                // Align folding legs/boots higher during jump frame
                yBob = 0.12f;
                legsY = -0.15f;
                bootsY = -0.38f;
            }
            else if (spriteName.Contains("Attack"))
            {
                xBob = facingRight ? 0.05f : -0.05f;
                yBob = -0.03f;
            }
            else if (spriteName.Contains("Hurt"))
            {
                xBob = facingRight ? -0.08f : 0.08f;
                yBob = 0.05f;
            }
            else if (spriteName.Contains("Death"))
            {
                yBob = -0.6f;
                xBob = facingRight ? -0.2f : 0.2f;
                legsY = -0.6f;
                bootsY = -0.6f;
            }

            if (eqHelmet != null) eqHelmet.transform.localPosition = new Vector3(xBob * 0.8f, helmetY + yBob, 0f);
            if (eqArmor != null) eqArmor.transform.localPosition = new Vector3(xBob, armorY + yBob, 0f);
            if (eqGloves != null) eqGloves.transform.localPosition = new Vector3(xBob * 1.1f, glovesY + yBob, 0f);
            if (eqLegs != null) eqLegs.transform.localPosition = new Vector3(xBob * 0.9f, legsY + yBob, 0f);
            if (eqBoots != null) eqBoots.transform.localPosition = new Vector3(xBob * 0.9f, bootsY, 0f);
            if (eqKnife != null) eqKnife.transform.localPosition = new Vector3(-0.22f + xBob * 0.9f, -0.15f + yBob, 0f);

            if (eqShield != null)
            {
                if (isBlocking)
                {
                    eqShield.transform.localPosition = new Vector3(facingRight ? 0.35f : -0.35f, -0.15f + yBob, 0f);
                    eqShield.transform.localRotation = Quaternion.Euler(0f, 0f, facingRight ? -25f : 25f);
                    eqShield.sortingOrder = 53;
                }
                else
                {
                    eqShield.transform.localPosition = new Vector3(facingRight ? -0.22f : 0.22f, -0.15f + yBob, 0f);
                    eqShield.transform.localRotation = Quaternion.identity;
                    eqShield.sortingOrder = 49;
                }
            }
        }

        public void ResetPlayerStatus()
        {
            currentHP = 100f;
            maxHP = 100f;
            extraHearts = 1;
            UpdateHealthUI();
            UpdateHeartsUI();
        }

        public void SetControlsLocked(bool locked)
        {
            controlsLocked = locked;
            if (locked && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        public void SetGroundedState(bool grounded, bool entering)
        {
            // Handled directly via OverlapCollider in Update
        }

        public bool IsUsingHeroAnimator()
        {
            return heroAnimator != null;
        }
    }

    public class GroundSensorComponent : MonoBehaviour
    {
        // Handled directly via OverlapCollider in Update
    }

    public class FadeOutDestroy : MonoBehaviour
    {
        private SpriteRenderer sr;
        private float lifeTime;
        private float elapsed;

        public void Initialize(float duration)
        {
            sr = GetComponent<SpriteRenderer>();
            lifeTime = duration;
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifeTime)
            {
                Destroy(gameObject);
                return;
            }
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(1f, 0f, elapsed / lifeTime);
                sr.color = c;
            }
        }
    }
}
