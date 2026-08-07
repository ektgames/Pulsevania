using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pulsevania.Core
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 12f;
        [SerializeField] private int damage = 1;
        [SerializeField] private float maxLifeTime = 2.5f;

        private Vector2 moveDirection;
        private Team ownerTeam;
        private Action<Projectile> returnToPoolAction;
        private float lifeTimer;
        private bool isInitialized;
        private Rigidbody2D rb;
        private bool isCritical = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;

            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }
        }

        public void Launch(Vector2 direction, Team team, Action<Projectile> onDeactivate)
        {
            moveDirection = direction.normalized;
            ownerTeam = team;
            returnToPoolAction = onDeactivate;
            lifeTimer = maxLifeTime;
            isInitialized = true;

            // Rotate projectile to face direction of travel
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public void SetDamage(int dmg)
        {
            damage = dmg;
        }

        public void SetCritical(bool crit)
        {
            isCritical = crit;
        }

        private void Update()
        {
            if (!isInitialized) return;

            // Move
            transform.Translate(Vector2.right * speed * Time.deltaTime, Space.Self);

            // Lifetime check
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0)
            {
                Deactivate();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!isInitialized) return;

            Damageable target = collision.GetComponent<Damageable>();
            if (target != null)
            {
                if (target.Team != ownerTeam)
                {
                    target.Damage(damage, ownerTeam);
                    Color dmgColor = ownerTeam == Team.Player ? (isCritical ? new Color(1f, 0.3f, 0f) : Color.yellow) : Color.red;
                    string textToShow = isCritical ? $"{damage} CRIT!" : damage.ToString();
                    DamageTextPool.Instance.SpawnText(target.transform.position + Vector3.up, textToShow, dmgColor);
                    Deactivate();
                }
            }
            else if (((1 << collision.gameObject.layer) & LayerMask.GetMask("Default", "Ground")) != 0)
            {
                // Hit wall or ground obstacle
                Deactivate();
            }
        }

        private void Deactivate()
        {
            isInitialized = false;
            returnToPoolAction?.Invoke(this);
        }
    }

    public class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        [SerializeField] private GameObject customPrefab;
        [SerializeField] private int initialPoolSize = 15;

        private Queue<Projectile> pool = new Queue<Projectile>();
        private Sprite defaultSprite;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateDefaultSprite();
            InitializePool();
        }

        private void CreateDefaultSprite()
        {
            Texture2D texture = new Texture2D(12, 4);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    // Draw a dagger shape in yellow
                    if (x >= 8)
                        texture.SetPixel(x, y, Color.yellow);
                    else
                        texture.SetPixel(x, y, y == 1 || y == 2 ? Color.yellow : Color.clear);
                }
            }
            texture.Apply();
            defaultSprite = Sprite.Create(texture, new Rect(0f, 0f, 12f, 4f), new Vector2(0.5f, 0.5f));
        }

        private void InitializePool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewInstance();
            }
        }

        private Projectile CreateNewInstance()
        {
            GameObject go;
            if (customPrefab != null)
            {
                go = Instantiate(customPrefab, transform);
            }
            else
            {
                go = new GameObject("Projectile_Instance");
                go.transform.SetParent(transform);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = defaultSprite;

                Rigidbody2D r2d = go.AddComponent<Rigidbody2D>();
                r2d.gravityScale = 0f;
                r2d.bodyType = RigidbodyType2D.Kinematic;

                BoxCollider2D col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1.2f, 0.4f);
            }

            Projectile proj = go.GetComponent<Projectile>() ?? go.AddComponent<Projectile>();
            go.SetActive(false);
            pool.Enqueue(proj);
            return proj;
        }

        public void SpawnProjectile(Vector3 position, Vector2 direction, Team team, int customDamage = 1, bool isCrit = false)
        {
            Projectile instance;
            if (pool.Count > 0)
            {
                instance = pool.Dequeue();
            }
            else
            {
                instance = CreateNewInstance();
                pool.Dequeue();
            }

            instance.transform.position = position;
            instance.SetDamage(customDamage);
            instance.SetCritical(isCrit);
            instance.gameObject.SetActive(true);
            instance.Launch(direction, team, ReturnToPool);
        }

        private void ReturnToPool(Projectile instance)
        {
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
        }
    }
}
