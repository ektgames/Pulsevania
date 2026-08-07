using System.Collections.Generic;
using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class StaticHazard : MonoBehaviour
    {
        [Header("Hazard Settings")]
        [SerializeField] private int damageAmount = 1;
        [SerializeField] private float damageInterval = 0.5f;

        private Dictionary<Damageable, float> contactCooldowns = new Dictionary<Damageable, float>();
        private List<Damageable> currentContacts = new List<Damageable>();

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            Damageable target = collision.GetComponent<Damageable>();
            if (target != null && target.Team == Team.Player)
            {
                if (!contactCooldowns.ContainsKey(target))
                {
                    contactCooldowns[target] = 0f;
                }

                if (Time.time >= contactCooldowns[target])
                {
                    // Apply damage
                    target.Damage(damageAmount, Team.Environment);
                    DamageTextPool.Instance.SpawnText(target.transform.position + Vector3.up, damageAmount.ToString(), Color.red);
                    
                    // Set next damage time
                    contactCooldowns[target] = Time.time + damageInterval;
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            Damageable target = collision.GetComponent<Damageable>();
            if (target != null)
            {
                if (contactCooldowns.ContainsKey(target))
                {
                    contactCooldowns.Remove(target);
                }
            }
        }
    }
}
