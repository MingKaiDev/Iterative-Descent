using UnityEngine;

public class PasswordEventHandler : MonoBehaviour
{
    [SerializeField] private ComputerScreenProp computerScreen;

    void OnEnable()
    {
        PuzzleUI.OnPuzzleFinished += HandlePuzzleFinished;
        PasswordScreenUI.OnPC1LoggedIn += HandlePC1LoggedIn;
    }

    void OnDisable()
    {
        PuzzleUI.OnPuzzleFinished -= HandlePuzzleFinished;
        PasswordScreenUI.OnPC1LoggedIn -= HandlePC1LoggedIn;
    }

    void HandlePuzzleFinished(int correct, int total)
    {
        if (total <= 0) return;

        // 60% pass threshold (was: correct == total, i.e. 100%).
        // Keep this formula in sync with PuzzleUI.ShowCompletion(), which uses
        // the same check to decide whether to reveal the password on the
        // quiz completion screen.
        int required = Mathf.CeilToInt(total * 0.6f);
        if (correct >= required)
            PasswordScreenUI.FolderPuzzleSolved = true;
    }

    void HandlePC1LoggedIn()
    {
        // Swap password panel → linked list puzzle on the same computer
        computerScreen.ShowLinkedListPuzzle();
    }
}