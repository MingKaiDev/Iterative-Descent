using UnityEngine;

/// <summary>
/// Drives the GroundSlam VFX prefab.
/// Expands the ShockwaveRing disc outward then destroys the whole prefab.
/// DebrisBurst particle system plays automatically via Play On Awake.
///
/// Unity Setup:
///   - Attach to the GroundSlamVFX root prefab.
///   - Assign shockwaveRing (the flat cylinder child).
///   - DebrisBurst particle system plays on its own -- no reference needed.
/// </summary>
public class GroundSlamVFX : MonoBehaviour
{
    [Tooltip("The flat cylinder child used as the expanding ring.")]
    public Transform shockwaveRing;

    [Tooltip("Maximum diameter the ring expands to (should match slamRadius * 2).")]
    public float maxScale = 10f;

    [Tooltip("How long the ring takes to fully expand.")]
    public float expandDuration = 0.4f;

    [Tooltip("How long the ring stays at full size before the prefab is destroyed.")]
    public float holdDuration = 0.2f;

    private float _elapsed;
    private bool  _holding;
    private float _holdTimer;

    void Update()
    {
        if (!_holding)
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / expandDuration);
            float currentScale = Mathf.Lerp(0f, maxScale, t);

            if (shockwaveRing != null)
                shockwaveRing.localScale = new Vector3(
                    currentScale,
                    shockwaveRing.localScale.y,
                    currentScale);

            if (_elapsed >= expandDuration)
                _holding = true;
        }
        else
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= holdDuration)
                Destroy(gameObject);
        }
    }
}
