using UnityEngine;
using UnityEngine.UI;

namespace Pulsevania.Diagnostic
{
    /// <summary>
    /// Diagnostic bootstrap component to verify clean iOS application startup.
    /// Free of gameplay prefabs, asset references, and complex serialization structures.
    /// </summary>
    public class DiagnosticBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Debug.Log("[DiagnosticBootstrap] Awake executed cleanly. Engine startup verified.");
        }

        private void Start()
        {
            Debug.Log("[DiagnosticBootstrap] Start executed cleanly.");
        }

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.green;

            Rect rect = new Rect(0, 0, Screen.width, Screen.height);
            GUI.Label(rect, "DIAGNOSTIC BOOTSTRAP ACTIVE\nBuild 8 - Clean Engine Startup Verified", style);
        }
    }
}
