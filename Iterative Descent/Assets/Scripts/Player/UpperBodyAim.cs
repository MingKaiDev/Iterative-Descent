using UnityEngine;

/// <summary>
/// Rotates the upper spine bones by the camera pitch so the character's arms
/// and equipped weapon track the camera look direction up/down.
///
/// Run order: this must execute AFTER the Animator updates the skeleton each frame.
/// Set Script Execution Order: UpperBodyAim AFTER Default Time (positive value, e.g. 100).
///
/// Setup:
///   Attach to the Player GameObject alongside PlayerMovement.
///   Assign Spine1 and Spine2 bones in the Inspector (auto-found on Start if left empty).
///   Tune pitchAxis if up/down rotation looks wrong (try Vector3.right or Vector3.forward).
/// </summary>
public class UpperBodyAim : MonoBehaviour
{
    [Header("Bones (auto-found if left empty)")]
    public Transform spine1;   // mixamorig:Spine1
    public Transform spine2;   // mixamorig:Spine2

    [Header("Rotation Settings")]
    [Tooltip("Local axis to rotate each spine bone on. Vector3.right works for most Mixamo rigs.")]
    public Vector3 pitchAxis = Vector3.right;

    [Tooltip("Fraction of total camera pitch applied to Spine1. Remainder goes to Spine2.")]
    [Range(0f, 1f)]
    public float spine1Weight = 0.5f;

    [Tooltip("Hard clamp on the pitch applied to bones. Prevents extreme bending at low/high camera angles.")]
    public float pitchClamp = 60f;

    // --- Private ------------------------------------------------------------

    private PlayerMovement _movement;

    // --- Unity Lifecycle ----------------------------------------------------

    void Start()
    {
        _movement = GetComponent<PlayerMovement>();
        if (_movement == null)
            Debug.LogWarning("[UpperBodyAim] PlayerMovement not found on this GameObject.");

        if (spine1 == null || spine2 == null)
            AutoFindBones();
    }

    // LateUpdate runs after Animator -- we apply our override on top of the animated pose.
    void LateUpdate()
    {
        if (_movement == null) return;

        float pitch = Mathf.Clamp(_movement.CameraPitch, -pitchClamp, pitchClamp);
        float pitch1 = pitch * spine1Weight;
        float pitch2 = pitch * (1f - spine1Weight);

        if (spine1 != null)
            spine1.localRotation *= Quaternion.AngleAxis(pitch1, pitchAxis);

        if (spine2 != null)
            spine2.localRotation *= Quaternion.AngleAxis(pitch2, pitchAxis);
    }

    // --- Helpers ------------------------------------------------------------

    void AutoFindBones()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (spine1 == null && t.name == "mixamorig:Spine1") spine1 = t;
            if (spine2 == null && t.name == "mixamorig:Spine2") spine2 = t;
            if (spine1 != null && spine2 != null) break;
        }

        if (spine1 == null) Debug.LogWarning("[UpperBodyAim] mixamorig:Spine1 not found.");
        if (spine2 == null) Debug.LogWarning("[UpperBodyAim] mixamorig:Spine2 not found.");
    }
}
