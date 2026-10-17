using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ThunderWire.Attributes;

namespace UHFPS.Runtime
{
    [InspectorHeader("Main Menu Manager")]
    public class MainMenuManager : MonoBehaviour
    {
        public BackgroundFader BackgroundFader;
        public string NewGameSceneName;
        public bool NewGameRemoveSaves;

        public void NewGame()
        {
            string sceneName = NewGameSceneName;
            if (string.IsNullOrEmpty(sceneName) || sceneName == "Playground")
                sceneName = GraveyardLevels.SceneForLevel(1);

            if (string.IsNullOrEmpty(sceneName))
                throw new System.NullReferenceException("The new game scene name field is empty!");

            SaveGameManager.ClearLoadType();
            SaveGameManager.LoadSceneName = sceneName;
            SaveGameManager.LoadFolderName = string.Empty;
            StartCoroutine(LoadNewGame(sceneName));
        }

        IEnumerator LoadNewGame(string sceneName)
        {
            if (BackgroundFader != null)
                yield return BackgroundFader.StartBackgroundFade(false);

            if (NewGameRemoveSaves)
                yield return new WaitToTaskComplete(SaveGameManager.RemoveAllSaves());

            SaveGameManager.ClearLoadType();
            SaveGameManager.LoadSceneName = sceneName;
            SaveGameManager.LoadFolderName = string.Empty;
            SceneManager.LoadScene(sceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}