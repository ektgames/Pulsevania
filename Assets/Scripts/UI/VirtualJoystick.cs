using UnityEngine;
using UnityEngine.EventSystems;

namespace Pulsevania.UI
{
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform background;
        private RectTransform handle;
        private Vector2 inputVector = Vector2.zero;

        public float Horizontal => inputVector.x;
        public float Vertical => inputVector.y;

        private void Awake()
        {
            background = GetComponent<RectTransform>();
            if (transform.childCount > 0)
            {
                handle = transform.GetChild(0).GetComponent<RectTransform>();
            }
        }

        private void Start()
        {
            if (background != null)
            {
                // Enlarge by 1.4x dynamically for a smoother touch range and better accessibility
                background.sizeDelta *= 1.4f;
                if (handle != null)
                {
                    handle.sizeDelta *= 1.4f;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 position;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out position))
            {
                float radius = background.sizeDelta.x * 0.5f;
                
                // Map the touch offset to a normalized vector (-1 to 1 range)
                inputVector = position / radius;

                // Clamp magnitude to 1
                if (inputVector.magnitude > 1f)
                {
                    inputVector = inputVector.normalized;
                }

                // Move the knob (handle) within the bounds of the background
                if (handle != null)
                {
                    handle.anchoredPosition = inputVector * radius * 0.75f;
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Reset to center on release
            inputVector = Vector2.zero;
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
