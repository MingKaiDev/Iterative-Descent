using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Tooltip("Delay in seconds before loading the game scene. Gives the click sound time to play.")]
    [SerializeField] float loadDelay = 0.15f;

    // Called by Play Button onClick
    public void PlayGame()
    {
        StartCoroutine(LoadGameRoutine());
    }

    IEnumerator LoadGameRoutine()
    {
        yield return new WaitForSecondsRealtime(loadDelay);
        SceneManager.LoadScene(1);
    }

    // Stub - no functionality yet
    public void LoadGame()
    {
        Debug.Log("Load pressed - not implemented");
    }

    public void OpenOptions()
    {
        SettingsPanelUI.Instance?.Show();
    }

    // Quits the game (works in build, not in editor)
    public void QuitGame()
    {
        Debug.Log("Quit pressed");
        Application.Quit();
    }
}