using UnityEngine;

namespace Pulsevania.Core
{
    public enum Team
    {
        Player,
        Enemy,
        Environment
    }

    public class Damageable : MonoBehaviour
    {
        [SerializeField] private HealthSystem healthSystem;
        [SerializeField] private Team team;

        public Team Team { get => team; set => team = value; }
        public HealthSystem HealthSystem => healthSystem;

        private void Awake()
        {
            if (healthSystem == null)
            {
                healthSystem = GetComponent<HealthSystem>();
            }
        }

        public void Damage(int amount, Team attackerTeam)
        {
            if (attackerTeam == team) return; // No friendly fire

            int finalAmount = amount;
            PlayerController controller = GetComponentInParent<PlayerController>();
            if (controller != null)
            {
                int armor = controller.equipmentArmor;
                int reduction = armor / 3; // Every 3 armor points absorb 1 damage point
                finalAmount = Mathf.Max(1, amount - reduction);
                controller.TakeDamage(finalAmount);
                return;
            }

            if (healthSystem != null)
            {
                healthSystem.TakeDamage(finalAmount);
            }
        }
    }
}
