// PaintingInteractable.cs
// IInteractable on the PaintingButton prop below each painting.
//
// SETUP PER PAINTING:
//   1. Place PaintingButton.fbx next to the painting frame.
//   2. Add this script + InteractableBase + InteractableRegistrar + BoxCollider.
//   3. Set correctSlot (1-8) -- the step in the sequence this painting belongs to.
//      1=NEW, 2=READY, 3=RUNNING, 4=BLOCKED, 5=READY-SUSPENDED,
//      6=BLOCKED-SUSPENDED, 7=ZOMBIE, 8=TERMINATED
//   4. Assign spotLight -- the spotlight for this painting.
//   5. Drag into PaintingPuzzleManager.paintings[].

using UnityEngine;

public class PaintingInteractable : MonoBehaviour, IInteractable
{
    // ── Inspector ──────────────────────────────────────────────────────────
    [Tooltip("Position in the correct sequence. 1=NEW ... 8=TERMINATED")]
    [Range(1, 8)]
    public int correctSlot = 1;

    [Tooltip("The spotlight illuminating this painting/button.")]
    public Light spotLight;

    [Tooltip("Sound played when the player presses this button.")]
    public AudioClip pressSound;

    [Tooltip("AudioSource to play pressSound through. If unassigned, one is added automatically.")]
    public AudioSource audioSource;

    // ── IInteractable ──────────────────────────────────────────────────────
    public string InteractLabel => "Examine";

    public void Interact(GameObject interactor)
    {
        if (PaintingPuzzleManager.Instance == null) return;
        if (PaintingPuzzleManager.Instance.IsSolved) return;
        if (pressSound != null) audioSource.PlayOneShot(pressSound);
        PaintingPuzzleManager.Instance.OnPaintingPressed(this);
    }

    // ── Public ─────────────────────────────────────────────────────────────
    public int CorrectSlot => correctSlot;

    // ── Light colours ──────────────────────────────────────────────────────
    private static readonly Color ColourIdle    = new Color(0xCF / 255f, 0xB5 / 255f, 0x95 / 255f);
    private static readonly Color ColourCorrect = new Color(0.15f, 0.90f, 0.25f);

    private float _originalIntensity;

    private void Start()
    {
        if (spotLight != null)
        {
            _originalIntensity = spotLight.intensity;
            spotLight.color    = ColourIdle;
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // ── Light state (called by PaintingPuzzleManager) ──────────────────────

    /// <summary>Correct press -- bright green.</summary>
    public void OnCycled()
    {
        if (spotLight == null) return;
        spotLight.color     = ColourCorrect;
        spotLight.intensity = _originalIntensity * 3f;
    }

    /// <summary>All 8 correct -- bright green.</summary>
    public void OnCorrect()
    {
        if (spotLight == null) return;
        spotLight.color     = ColourCorrect;
        spotLight.intensity = _originalIntensity * 3f;
    }

    /// <summary>Wrong press anywhere -- reset to original.</summary>
    public void OnWrong()
    {
        if (spotLight == null) return;
        spotLight.color     = ColourIdle;
        spotLight.intensity = _originalIntensity;
    }
}
