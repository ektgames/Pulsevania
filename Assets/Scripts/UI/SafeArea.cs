using UnityEngine;

namespace Pulsevania.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2 lastScreenSize = new Vector2(0, 0);
        private ScreenOrientation lastOrientation = ScreenOrientation.Unknown;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            Refresh();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || 
                lastScreenSize.x != Screen.width || 
                lastScreenSize.y != Screen.height || 
                lastOrientation != Screen.orientation)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            Rect safeArea = Screen.safeArea;

            if (safeArea != lastSafeArea || 
                Screen.width != lastScreenSize.x || 
                Screen.height != lastScreenSize.y || 
                Screen.orientation != lastOrientation)
            {
                lastSafeArea = safeArea;
                lastScreenSize = new Vector2(Screen.width, Screen.height);
                lastOrientation = Screen.orientation;

                ApplySafeArea(safeArea);
            }
        }

        private void ApplySafeArea(Rect r)
        {
            if (rectTransform == null) return;

            Vector2 anchorMin = r.position;
            Vector2 anchorMax = r.position + r.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }
    }
}
