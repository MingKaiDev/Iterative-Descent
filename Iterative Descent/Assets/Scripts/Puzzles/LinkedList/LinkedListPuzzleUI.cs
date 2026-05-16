using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Master controller for the Linked List drag-and-drop puzzle.
/// Attach to the Linked List Puzzle Panel GameObject in the Canvas.
/// 
/// Inspector Setup:
///   - nodeCardPrefab     : Prefab with LinkedListNodeCard + CanvasGroup + Image + TMP child
///   - nodeContainer      : HorizontalLayoutGroup parent for shuffled node cards (the "pool")
///   - slotContainer      : HorizontalLayoutGroup parent for NodeSlot answer slots
///   - feedbackText       : TMP text for "Correct!" / "Wrong!" messages
///   - closeButton        : Button that calls ClosePanel()
///   - arrowPrefab        : (optional) simple arrow Image prefab placed between slots
/// </summary>
public class LinkedListPuzzleUI : MonoBehaviour
{
    public static LinkedListPuzzleUI Instance { get; private set; }
    public static event Action<int> OnLinkedListSolved;

    [Header("Prefabs")]
    [SerializeField] private GameObject nodeCardPrefab;
    [SerializeField] private GameObject slotPrefab;

    [Header("Containers")]
    [SerializeField] private Transform nodeContainer;   // Pool area (shuffled cards live here)
    [SerializeField] private Transform slotContainer;   // Answer chain slots live here

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button submitButton;

    private Action _onClose;
    private int[] _solution;          // Correct sorted order
    private List<NodeSlot> _slots = new();
    private List<GameObject> _spawnedCards = new();
    private int attempts;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (submitButton != null) submitButton.onClick.AddListener(CheckSolution);
    }

    void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
        if (submitButton != null) submitButton.onClick.RemoveListener(CheckSolution);
    }

    /// <summary>Called by ComputerScreenProp when swapping to this panel.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose = onClose;
        if (feedbackText != null) feedbackText.text = "";
        GeneratePuzzle();
    }

    private void GeneratePuzzle()
    {
        // Clear previous state
        foreach (GameObject card in _spawnedCards)
            if (card != null) Destroy(card);
        _spawnedCards.Clear();

        foreach (Transform t in nodeContainer) Destroy(t.gameObject);
        foreach (Transform t in slotContainer) Destroy(t.gameObject);
        _slots.Clear();

        // Generate a random set of 4 unique values, solution = ascending sort
        int[] values = GenerateValues(4);
        _solution = values.OrderBy(v => v).ToArray();

        // Shuffle for display in the pool
        int[] shuffled = values.OrderBy(_ => UnityEngine.Random.value).ToArray();

        // Spawn draggable node cards in the pool
        foreach (int val in shuffled)
        {
            GameObject card = Instantiate(nodeCardPrefab, nodeContainer);
            card.GetComponent<LinkedListNodeCard>().Init(val);
            _spawnedCards.Add(card);

        }

        // Spawn answer slots
        for (int i = 0; i < _solution.Length; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotContainer);
            var slotComp = slot.GetComponent<NodeSlot>();
            slotComp.SlotIndex = i;
            _slots.Add(slotComp);
        }
    }

    private int[] GenerateValues(int count)
    {
        // Pick `count` unique values between 1–20
        List<int> pool = Enumerable.Range(1, 20).ToList();
        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            result[i] = pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }

    /// <summary>Called by NodeSlot on every drop, or by Submit button.</summary>
    public void CheckSolution()
    {
        // Only validate if all slots are filled
        bool allFilled = _slots.All(s => s.OccupiedBy != null);
        if (!allFilled)
        {
            if (feedbackText != null) feedbackText.text = "Fill all slots first!";
            return;
        }

        bool correct = true;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].OccupiedBy.NodeValue != _solution[i])
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            if (feedbackText != null) feedbackText.text = "Correct! Linked list sorted.";
            OnLinkedListSolved?.Invoke(attempts);
            Invoke(nameof(ClosePanel), 1.5f); // Brief pause before closing
        }
        else
        {
            if (feedbackText != null) feedbackText.text = "Wrong order. Try again.";
            attempts++;
            StartCoroutine(ClearWrongAfterDelay());
        }
    }

    private System.Collections.IEnumerator ClearWrongAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        if (feedbackText != null) feedbackText.text = "";
        ReturnAllCardsToPool();
    }

    private void ReturnAllCardsToPool()
    {
        foreach (var slot in _slots)
        {
            if (slot.OccupiedBy != null)
            {
                slot.OccupiedBy.ReturnToPool(nodeContainer);
                slot.OccupiedBy = null;
            }
        }
    }

    public void ClosePanel()
    {
        _onClose?.Invoke();
    }
}