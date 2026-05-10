// InteractableProp.cs
using UnityEngine;

[System.Serializable]
public class QuestionData
{
    public string question;
    public string[] answers = new string[4];
    public int correctAnswerIndex;
}

public class InteractableProp : MonoBehaviour
{
    [Header("Interaction")]
    public float interactRadius = 2.5f;
    public Transform player;
    public Transform propCenter;
    public KeyCode interactKey = KeyCode.E;

    [Header("UI References")]
    public GameObject promptUI;
    public GameObject puzzleOverlay;

    [Header("Puzzle Data")]
    public QuestionData[] questions = new QuestionData[5];

    private Outline _outline;
    private bool _playerInRange;
    private bool _puzzleOpen;

    void Awake()
    {
        // Grab the Quick Outline component on this GameObject
        _outline = GetComponent<Outline>();
        if (_outline != null)
            _outline.enabled = false;

        promptUI.SetActive(false);
        puzzleOverlay.SetActive(false);
    }

    void Update()
    {
        if (_puzzleOpen) return;
        Transform origin = propCenter != null ? propCenter : transform;
        float dist = Vector3.Distance(origin.position, player.position);
        print(dist);
        _playerInRange = dist <= interactRadius;

        // Toggle outline and prompt based on proximity
        if (_outline != null)
            _outline.enabled = _playerInRange;

        promptUI.SetActive(_playerInRange);

        if (_playerInRange && Input.GetKeyDown(interactKey))
            OpenPuzzle();
    }

    public void OpenPuzzle()
    {
        _puzzleOpen = true;

        // Hide outline and prompt while puzzle is open
        if (_outline != null)
            _outline.enabled = false;

        promptUI.SetActive(false);
        puzzleOverlay.SetActive(true);

        puzzleOverlay.GetComponent<PuzzleUI>()
            .Setup(questions, ClosePuzzle);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void ClosePuzzle(bool completed)
    {
        _puzzleOpen = false;
        puzzleOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (completed)
            Debug.Log("All questions completed! Trigger your game event here.");
    }

    void OnDrawGizmosSelected()
    {
        Transform origin = propCenter != null ? propCenter : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, interactRadius);
    }
}