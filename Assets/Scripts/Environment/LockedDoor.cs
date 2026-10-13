using System.Collections;
using UnityEngine;

namespace Pulsevania.Core
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class LockedDoor : MonoBehaviour
    {
        [Header("Door Settings")]
        [SerializeField] private bool requiresKey = true;
        [SerializeField] private bool completesLevelOnOpen = true;

        private bool isOpened = false;

        private void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (isOpened) return;

            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                TryOpenDoor();
            }
        }

        private void TryOpenDoor()
        {
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            if (requiresKey)
            {
                if (GameManager.Instance.UseKey())
                {
                    OpenDoor();
                }
                else
                {
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Anahtar Gerekiyor!" : "Requires Key!", Color.red);
                }
            }
            else
            {
                OpenDoor();
            }
        }

        private void OpenDoor()
        {
            isOpened = true;
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            DamageTextPool.Instance.SpawnText(transform.position + Vector3.up, isTR ? "Kapı Açıldı!" : "Door Unlocked!", Color.green);

            // Deactivate or trigger unlock animation/effect
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                // Semi-transparent opened representation
                sr.color = new Color(1f, 1f, 1f, 0.3f);
            }

            if (completesLevelOnOpen)
            {
                StartCoroutine(CompleteLevelDelay());
            }
        }

        private IEnumerator CompleteLevelDelay()
        {
            yield return new WaitForSeconds(1f);
            GameManager.Instance.CompleteLevel();
        }
    }
}
