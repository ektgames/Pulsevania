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

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
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
