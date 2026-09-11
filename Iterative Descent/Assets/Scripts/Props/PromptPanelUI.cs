// PromptPanelUI.cs
// Shared-singleton fallback for InteractableBase.promptPanel/promptLabel.
//
// Every interactable prop shows its "[E]  <label>" prompt through a single shared
// panel living once under the persisted Canvas (per PersistentUIRoot.cs's own doc
// comment: "any new puzzle panels Level 2 introduces get added as new children under
// this same persisted Canvas instead of a separate one" -- the Prompt Panel is that
// kind of shared object too). Every Level 1 prop got that panel/label pair dragged in
// by hand while authoring Level 1.unity, with the Canvas open in the same scene.
//
// Any prop placed somewhere that ISN'T open alongside that Canvas -- a prop inside
// the Library room prefab (edited in Prefab Mode, its own isolated context), or any
// future Level 2/3 prop -- has nothing to drag that reference from, so promptPanel/
// promptLabel are left unassigned (confirmed via direct scene/prefab-file audit:
// BstPuzzleProp's InteractableBase has promptPanel/promptLabel both fileID 0). Since
// InteractableBase.UpdateState() only ever touches promptPanel through a null check,
// this fails completely silently -- the "[E]  Sort the Returns Cart" text never
// shows, even though pressing E and actually interacting works fine (that path
// doesn't touch promptPanel at all). Reported by Ming Kai as: the "[E] interact"
// prompt doesn't work, despite E itself working.
//
// Fix, same shape as BstPuzzleUI.Instance / SelectionPromptUI.Instance: give
// InteractableBase a shared fallback it can resolve at runtime instead of requiring
// a per-instance Inspector drag. FindFirstObjectByType(..., FindObjectsInactive.Include)
// is used because the Prompt Panel itself starts inactive, same as every other HUD
// overlay in this project.
//
// Setup (one-time): add this component to the EXISTING "Prompt Panel" GameObject
// under the persisted Canvas (the same object every Level 1 prop's promptPanel field
// already points at) -- do not create a new GameObject. Leave 'panel' empty (it
// defaults to this component's own GameObject); assign 'label' to the same TMP_Text
// child any already-wired Level 1 prop's promptLabel field points at.
using TMPro;
using UnityEngine;

public class PromptPanelUI : MonoBehaviour
{
    [Tooltip("Leave empty to default to this component's own GameObject.")]
    [SerializeField] private GameObject panel;

    [Tooltip("The TMP label inside the prompt panel -- same object every Level 1 prop's " +
             "promptLabel field already points at.")]
    [SerializeField] private TMP_Text label;

    public GameObject Panel => panel != null ? panel : gameObject;
    public TMP_Text   Label => label;

    private static PromptPanelUI _instance;
    public static PromptPanelUI Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<PromptPanelUI>(FindObjectsInactive.Include);
            return _instance;
        }
        private set => _instance = value;
    }

    void Awake() => Instance = this;
}
