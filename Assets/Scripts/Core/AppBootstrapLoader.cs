using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pulsevania.Core
{
    public class AppBootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string targetSceneName = "SampleScene";
        [SerializeField] private float delayBeforeLoad = 0.1f;

        private IEnumerator Start()
        {
            if (delayBeforeLoad > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeLoad);
            }

            string sceneName = string.IsNullOrEmpty(targetSceneName) ? "SampleScene" : targetSceneName;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[Launch] Required game scene is missing from the build: " + sceneName);
                yield break;
            }
            Debug.Log("[Launch] Loading " + sceneName + " with Unity " + Application.unityVersion);
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = true;
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
                Debug.Log("[Launch] Game scene loaded.");
            }
        }
    }
}
