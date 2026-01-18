using UnityEngine;
using UnityEngine.SceneManagement;

namespace GalacticFishing.UI
{
    public sealed class SceneLoader : MonoBehaviour
    {
        public void LoadSceneByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Scene name is empty.");
                return;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public void LoadSceneAdditive(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Scene name is empty.");
                return;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
        }
    }
}
