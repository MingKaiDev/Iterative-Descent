// InteractableProp.cs
using UnityEngine;

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
    public string question = "What is the capital of France?";
    public string[] answers = { "Paris", "London", "Berlin", "Madrid" };
    public int correctAnswerIndex = 0;

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
            .Setup(question, answers, correctAnswerIndex, ClosePuzzle);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void ClosePuzzle(bool answeredCorrectly)
    {
        _puzzleOpen = false;
        puzzleOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (answeredCorrectly)
        {
            Debug.Log("Correct! Trigger your game event here.");
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform origin = propCenter != null ? propCenter : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, interactRadius);
    }
}