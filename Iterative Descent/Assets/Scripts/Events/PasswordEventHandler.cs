using UnityEngine;

/// <summary>
/// Always-awake listener. Sets PasswordScreenUI.FolderPuzzleSolved = true
/// when the folder puzzle is completed, regardless of computer panel state.
/// Also handles the PC1 login event to trigger the next puzzle.
/// </summary>
public class PasswordEventHandler : MonoBehaviour
{
    [Header("Next Puzzle (assign when built)")]
    public PuzzleProp nextPuzzleProp;

    void OnEnable()
    {
        PuzzleUI.OnPuzzleFinished += HandleFolderPuzzleFinished;
        PasswordScreenUI.OnPC1LoggedIn += HandlePC1LoggedIn;
    }

    void OnDisable()
    {
        PuzzleUI.OnPuzzleFinished -= HandleFolderPuzzleFinished;
        PasswordScreenUI.OnPC1LoggedIn -= HandlePC1LoggedIn;
    }

    private void HandleFolderPuzzleFinished(int correct, int total)
    {
        if (correct < total) return;

        // Set the static flag — PasswordScreenUI reads this in Setup()
        PasswordScreenUI.FolderPuzzleSolved = true;
        Debug.Log("PasswordEventHandler: Folder puzzle solved. PC1 password unlocked.");
    }

    private void HandlePC1LoggedIn()
    {
        Debug.Log("PasswordEventHandler: PC1 logged in.");

        if (nextPuzzleProp != null)
            nextPuzzleProp.Interact(null);
        else
            Debug.Log("PasswordEventHandler: nextPuzzleProp not assigned yet.");
    }
}