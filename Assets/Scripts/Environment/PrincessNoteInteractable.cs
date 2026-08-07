using UnityEngine;

namespace Pulsevania.Core
{
    public class PrincessNoteInteractable : MonoBehaviour
    {
        private bool isPlayerNear = false;
        private static bool isNoteOpen = false;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            PlayerController pc = collision.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                isPlayerNear = true;
                if (DamageTextPool.Instance != null)
                {
                    string lang = PlayerPrefs.GetString("GameLanguage", "Turkish");
                    string msg = lang == "English" ? "Tap to Read" : "Okumak için Dokunun";
                    DamageTextPool.Instance.SpawnText(transform.position + Vector3.up * 1.5f, msg, Color.yellow);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            PlayerController pc = collision.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                isPlayerNear = false;
            }
        }

        private void OnMouseDown()
        {
            if (UIManager.Instance != null && UIManager.Instance.IsWorldMapOpen())
            {
                return;
            }

            if (isPlayerNear)
            {
                TryOpenNote();
            }
        }

        private void Update()
        {
            if (isPlayerNear && !isNoteOpen)
            {
                bool ePressed = false;
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    ePressed = UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
                }
                if (ePressed)
                {
                    TryOpenNote();
                }
            }
        }

        private void TryOpenNote()
        {
            if (isNoteOpen) return;
            if (UIManager.Instance != null)
            {
                isNoteOpen = true;
                UIManager.Instance.ShowPrincessNotePopup(() => {
                    isNoteOpen = false;
                });
            }
        }
    }
}
