using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Loads scene with build index 1
    public void PlayGame()
    {
        SceneManager.LoadScene(1);
    }

    // Quits the game (works in build, not in editor)
    public void QuitGame()
    {
        Debug.Log("Quit pressed");
        Application.Quit();
    }
}