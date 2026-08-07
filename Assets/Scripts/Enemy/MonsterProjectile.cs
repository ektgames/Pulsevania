using UnityEngine;

namespace Pulsevania.Core
{
    public class MonsterProjectile : MonoBehaviour
    {
        public enum ProjectileType 
        { 
            Dagger, 
            Fireball, 
            PoisonSpit, 
            VoidBall, 
            IceShard, 
            EarthStomp, 
            TrackingFireball,
            DeathBolt
        }

        private Vector3 direction;
        private float speed = 8f;
        private int damage = 10;
        private float lifeTime = 3f;
        private ProjectileType type = ProjectileType.Dagger;
        
        private float trackingSpeed = 1.8f;
        private Transform playerTransform;

        public void Initialize(Vector3 dir, int dmg, float projSpeed, ProjectileType projType, float duration = 3f)
        {
            direction = dir.normalized;
            damage = dmg;
            speed = projSpeed;
            type = projType;
            lifeTime = duration;

            Destroy(gameObject, lifeTime);
            SetupVisuals();
        }

        private void Start()
        {
            // Find player for homing mechanics
            PlayerController pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                playerTransform = pc.transform;
            }
        }

        private void SetupVisuals()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = gameObject.AddComponent<SpriteRenderer>();
            }
            sr.sortingOrder = 30;

            int w = 16;
            int h = 16;
            if (type == ProjectileType.Dagger) { w = 8; h = 4; }
            else if (type == ProjectileType.IceShard) { w = 12; h = 6; }
            else if (type == ProjectileType.EarthStomp) { w = 16; h = 8; }
            else if (type == ProjectileType.PoisonSpit || type == ProjectileType.Fireball || type == ProjectileType.VoidBall) { w = 8; h = 8; }
            else if (type == ProjectileType.TrackingFireball || type == ProjectileType.DeathBolt) { w = 12; h = 12; }

            Texture2D tex = new Texture2D(w, h);
            // Clear
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, Color.clear);

            // Draw procedural textures
            if (type == ProjectileType.Dagger)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (x < 2) tex.SetPixel(x, y, new Color(0.4f, 0.2f, 0.1f)); // Brown hilt
                        else if (y == 1 || y == 2) tex.SetPixel(x, y, Color.white); // Silver blade
                        else tex.SetPixel(x, y, Color.gray);
                    }
                }
            }
            else if (type == ProjectileType.Fireball)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float dx = x - 3.5f;
                        float dy = y - 3.5f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist <= 1.5f) tex.SetPixel(x, y, Color.yellow);
                        else if (dist <= 3.2f) tex.SetPixel(x, y, new Color(1f, 0.4f, 0f)); // Orange
                        else if (dist <= 4.2f && (x + y) % 2 == 0) tex.SetPixel(x, y, Color.red);
                    }
                }
            }
            else if (type == ProjectileType.PoisonSpit)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float dx = x - 3.5f;
                        float dy = y - 3.5f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist <= 1.8f) tex.SetPixel(x, y, new Color(0.4f, 1f, 0.2f)); // Lime
                        else if (dist <= 3.2f) tex.SetPixel(x, y, new Color(0.1f, 0.7f, 0.1f)); // Dark Green
                        else if (dist <= 4.0f && Random.value > 0.5f) tex.SetPixel(x, y, new Color(0.1f, 0.4f, 0f, 0.6f));
                    }
                }
            }
            else if (type == ProjectileType.VoidBall)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float dx = x - 3.5f;
                        float dy = y - 3.5f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist <= 1.5f) tex.SetPixel(x, y, new Color(1f, 0.2f, 0.9f)); // Neon pink
                        else if (dist <= 3.5f) tex.SetPixel(x, y, new Color(0.3f, 0.05f, 0.6f)); // Deep purple
                        else if (dist <= 4.2f && Random.value > 0.4f) tex.SetPixel(x, y, new Color(0.1f, 0f, 0.3f, 0.5f));
                    }
                }
            }
            else if (type == ProjectileType.IceShard)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        // Needle shape pointing right
                        float diff = Mathf.Abs(y - 2.5f);
                        if (diff <= (w - x) * 0.3f)
                        {
                            if (diff < 0.8f) tex.SetPixel(x, y, Color.white);
                            else tex.SetPixel(x, y, new Color(0.5f, 0.85f, 1f)); // Ice blue
                        }
                    }
                }
            }
            else if (type == ProjectileType.EarthStomp)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        // Jagged shockwave spikes pointing up
                        bool isPixel = y <= (h - 1 - Mathf.Abs(x - 8) * 0.8f);
                        if (isPixel)
                        {
                            if (y == Mathf.FloorToInt(h - 1 - Mathf.Abs(x - 8) * 0.8f))
                                tex.SetPixel(x, y, new Color(0.8f, 0.7f, 0.6f)); // Light dust outline
                            else
                                tex.SetPixel(x, y, new Color(0.35f, 0.25f, 0.2f)); // Dark earth
                        }
                    }
                }
            }
            else if (type == ProjectileType.TrackingFireball)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float dx = x - 5.5f;
                        float dy = y - 5.5f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist <= 2.2f) tex.SetPixel(x, y, Color.white);
                        else if (dist <= 3.8f) tex.SetPixel(x, y, Color.yellow);
                        else if (dist <= 5.2f) tex.SetPixel(x, y, new Color(1f, 0.2f, 0f)); // Fire red
                        else if (dist <= 6.0f && (x + y) % 3 == 0) tex.SetPixel(x, y, new Color(0.3f, 0f, 0f, 0.4f)); // smoke
                    }
                }
            }
            else if (type == ProjectileType.DeathBolt)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        float dx = x - 5.5f;
                        float dy = y - 5.5f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist <= 2.2f) tex.SetPixel(x, y, Color.black);
                        else if (dist <= 3.8f) tex.SetPixel(x, y, new Color(0.3f, 0f, 0.6f)); // Purple
                        else if (dist <= 5.2f) tex.SetPixel(x, y, new Color(0.7f, 0.1f, 0.9f)); // Violet
                        else if (dist <= 6.0f && (x + y) % 3 == 0) tex.SetPixel(x, y, new Color(0.1f, 0f, 0.2f, 0.4f)); // purple smoke
                    }
                }
            }

            tex.filterMode = FilterMode.Point;
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }

        private void Update()
        {
            // Homing tracking logic
            if ((type == ProjectileType.TrackingFireball || type == ProjectileType.DeathBolt) && playerTransform != null)
            {
                Vector3 targetDir = (playerTransform.position - transform.position).normalized;
                direction = Vector3.RotateTowards(direction, targetDir, trackingSpeed * Time.deltaTime, 0f).normalized;
            }

            transform.position += direction * speed * Time.deltaTime;

            // Handle rotation visuals
            if (type == ProjectileType.Dagger)
            {
                transform.Rotate(0f, 0f, 360f * Time.deltaTime);
            }
            else if (type == ProjectileType.IceShard)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
            else if (type == ProjectileType.EarthStomp || type == ProjectileType.TrackingFireball || type == ProjectileType.DeathBolt)
            {
                // No rotation or face forward
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            PlayerController pc = collision.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                Damageable dmg = pc.GetComponent<Damageable>();
                if (dmg != null)
                {
                    dmg.Damage(damage, Team.Enemy);
                    if (DamageTextPool.Instance != null)
                    {
                        DamageTextPool.Instance.SpawnText(pc.transform.position + Vector3.up, damage.ToString(), Color.red);
                    }

                    // Apply poison DOT if PoisonSpit
                    if (type == ProjectileType.PoisonSpit)
                    {
                        PoisonStatusEffect poison = pc.GetComponent<PoisonStatusEffect>();
                        if (poison == null) poison = pc.gameObject.AddComponent<PoisonStatusEffect>();
                        int pDmg = Mathf.Max(2, damage / 5);
                        poison.ApplyPoison(pDmg, 5);
                    }
                }
                Destroy(gameObject);
            }
            else if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                // EarthStomp moves along ground, don't destroy when touching ground layer triggers
                if (type != ProjectileType.EarthStomp)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
