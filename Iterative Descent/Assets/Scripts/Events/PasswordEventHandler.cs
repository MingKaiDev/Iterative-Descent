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
        if (correct == total)
            PasswordScreenUI.FolderPuzzleSolved = true;
    }

    void HandlePC1LoggedIn()
    {
        // Swap password panel → linked list puzzle on the same computer
        computerScreen.ShowLinkedListPuzzle();
    }
}