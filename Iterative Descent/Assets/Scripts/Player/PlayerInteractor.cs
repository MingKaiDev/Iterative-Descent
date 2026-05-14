// PlayerInteractor.cs
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Settings")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode prevKey = KeyCode.LeftArrow;
    public KeyCode nextKey = KeyCode.RightArrow;

    // ── Pause flag — set by PuzzleProp when UI is open ───────────
    private static bool _paused = false;
    public static void Pause() => _paused = true;
    public static void Resume() => _paused = false;

    // ── Static registry ──────────────────────────────────────────
    private static readonly List<InteractableBase> _all = new();
    public static void Register(InteractableBase obj) => _all.Add(obj);
    public static void Unregister(InteractableBase obj) => _all.Remove(obj);

    // ── Runtime ──────────────────────────────────────────────────
    private readonly List<InteractableBase> _inRange = new();
    private int _selectedIndex = 0;

    void Update()
    {
        if (_paused) return; // ← skip everything while puzzle is open

        RefreshInRange();
        HandleSelection();
        UpdateAllStates();
        HandleInteract();
    }

    void RefreshInRange()
    {
        _inRange.Clear();
        foreach (var obj in _all)
        {
            if (obj == null) continue;
            if (Vector3.Distance(transform.position, obj.Center) <= obj.highlightRadius)
                _inRange.Add(obj);
        }

        _inRange.Sort((a, b) =>
            Vector3.Distance(transform.position, a.Center)
            .CompareTo(Vector3.Distance(transform.position, b.Center)));

        if (_inRange.Count == 0) _selectedIndex = 0;
        else _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _inRange.Count - 1);
    }

    void HandleSelection()
    {
        if (_inRange.Count <= 1) return;

        if (Input.GetKeyDown(nextKey))
            _selectedIndex = (_selectedIndex + 1) % _inRange.Count;

        if (Input.GetKeyDown(prevKey))
            _selectedIndex = (_selectedIndex - 1 + _inRange.Count) % _inRange.Count;
    }

    void UpdateAllStates()
    {
        foreach (var obj in _all)
        {
            if (obj == null) continue;
            if (!_inRange.Contains(obj))
                obj.UpdateState(false, false);
        }

        for (int i = 0; i < _inRange.Count; i++)
            _inRange[i].UpdateState(true, i == _selectedIndex);

        if (SelectionPromptUI.Instance != null)
            SelectionPromptUI.Instance.UpdateHint(_inRange, _selectedIndex);
    }

    void HandleInteract()
    {
        if (_inRange.Count == 0) return;
        if (!Input.GetKeyDown(interactKey)) return;
        _inRange[_selectedIndex].TryInteract(gameObject);
    }
}