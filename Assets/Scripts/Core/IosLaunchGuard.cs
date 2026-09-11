using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulsevania.Core
{
    public static class IosLaunchGuard
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void DisableRuntimeDebugUi()
        {
            try
            {
                if (DebugManager.instance != null)
                {
                    DebugManager.instance.enableRuntimeUI = false;
                }
            }
            catch (Exception)
            {
                // Rendering debugger is optional; never block boot.
            }
        }
    }
}
