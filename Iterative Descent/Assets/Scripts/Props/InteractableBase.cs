using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableBase : MonoBehaviour
{
    [Header("Detection")]
    public float highlightRadius = 3f;
    public Transform propCenter;

    [Header("Prompt UI")]
    public GameObject promptPanel;
    public TMP_Text promptLabel;

    // ���� State set by PlayerInteractor each frame ����������������������������������
    [HideInInspector] public bool playerInRadius;
    [HideInInspector] public bool isSelected;       // highlighted via arrow key selection

    // ���� Cached ������������������������������������������������������������������������������������������������������
    private Outline _outline;
    private IInteractable _handler;

    public Vector3 Center => propCenter != null ? propCenter.position : transform.position;
    public IInteractable Handler => _handler;

    void Awake()
    {
        _outline = GetComponent<Outline>();
        _handler = GetComponent<IInteractable>();

        if (_outline != null) _outline.enabled = false;
        if (promptPanel != null) promptPanel.SetActive(false);

        if (_handler == null)
            Debug.LogWarning($"[InteractableBase] '{gameObject.name}' has no IInteractable component.", this);
    }

    /// <summary>Called every frame by PlayerInteractor.</summary>
    public void UpdateState(bool inRadius, bool selected)
    {
        playerInRadius = inRadius;
        isSelected = selected;

        // Outline: on when in range; brighter/different color when selected
        if (_outline != null)
        {
            _outline.enabled = inRadius;
            if (inRadius)
                _outline.OutlineColor = Color.white;
        }

        // Prompt: only for the currently selected object
        if (promptPanel != null)
        {
            promptPanel.SetActive(inRadius);
            if (inRadius && promptLabel != null)
                promptLabel.text = $"[E]  {_handler?.InteractLabel ?? "Interact"}";
        }
    }

    public void TryInteract(GameObject interactor)
    {
        _handler?.Interact(interactor);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Center, highlightRadius);
    }
}