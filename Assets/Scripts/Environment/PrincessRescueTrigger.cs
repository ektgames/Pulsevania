using UnityEngine;

public class PrincessRescueTrigger : MonoBehaviour
{
    private bool isRescued = false;
    private bool playerInRange = false;

    private void Update()
    {
        if (isRescued) return;

        bool ePressed = false;
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            ePressed = true;
        }

        if (playerInRange && ePressed)
        {
            // Verify if final Boss is dead in Room 50
            GameObject boss = GameObject.Find("BossEnemy");
            if (boss != null)
            {
                if (Pulsevania.Core.DamageTextPool.Instance != null)
                {
                    Pulsevania.Core.DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 2f, "Kızıl Ejderha Yenilmeli!", Color.red);
                }
                return;
            }

            isRescued = true;
            
            if (Pulsevania.Core.UIManager.Instance != null)
            {
                Pulsevania.Core.UIManager.Instance.TriggerRescueDialogue();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isRescued) return;
        
        Pulsevania.Core.PlayerController pc = other.GetComponentInParent<Pulsevania.Core.PlayerController>();
        if (pc != null)
        {
            playerInRange = true;
            if (Pulsevania.Core.DamageTextPool.Instance != null)
            {
                GameObject boss = GameObject.Find("BossEnemy");
                if (boss != null)
                {
                    Pulsevania.Core.DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 2f, "Kızıl Ejderha Yenilmeli!", Color.red);
                }
                else
                {
                    Pulsevania.Core.DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 2f, "Konuşmak için [E] tuşuna basın", Color.yellow);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Pulsevania.Core.PlayerController pc = other.GetComponentInParent<Pulsevania.Core.PlayerController>();
        if (pc != null)
        {
            playerInRange = false;
        }
    }
}
