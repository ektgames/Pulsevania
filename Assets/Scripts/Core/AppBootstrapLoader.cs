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
                yield return new WaitForSeconds(delayBeforeLoad);
            }

            string sceneName = string.IsNullOrEmpty(targetSceneName) ? "SampleScene" : targetSceneName;
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad == null)
            {
                int fallbackIndex = SceneManager.sceneCountInBuildSettings > 1 ? 1 : 0;
                asyncLoad = SceneManager.LoadSceneAsync(fallbackIndex);
            }
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = true;
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }
        }
    }
}
