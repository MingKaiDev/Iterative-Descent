using UnityEngine;

/// <summary>
/// Reduces camera FOV when either weapon enters aim mode, giving the
/// classic RE-style zoom effect. Lerps smoothly in and out.
///
/// Setup: attach to the Player GameObject. No extra Inspector wiring needed
/// beyond what PlayerCombat and ShotgunController already have.
/// </summary>
public class AimCameraController : MonoBehaviour
{
    [Header("FOV Zoom")]
    [Tooltip("FOV while aiming. Default camera FOV is captured at Start as the base.")]
    public float aimFOV   = 45f;
    [Tooltip("Speed of the FOV lerp in/out.")]
    public float zoomSpeed = 10f;

    // --- Private ------------------------------------------------------------
    private Camera            _camera;
    private float             _baseFOV;
    private PlayerCombat      _pistol;
    private ShotgunController _shotgun;

    void Start()
    {
        _pistol  = GetComponent<PlayerCombat>();
        _shotgun = GetComponent<ShotgunController>();

        _camera = Camera.main;
        if (_camera == null)
        {
            Debug.LogWarning("[AimCameraController] No main camera found.");
            return;
        }

        _baseFOV = _camera.fieldOfView;
    }

    void Update()
    {
        if (_camera == null) return;

        bool isAiming = (_pistol  != null && _pistol.enabled  && _pistol.IsAiming)
                     || (_shotgun != null && _shotgun.enabled && _shotgun.IsAiming);

        float targetFOV = isAiming ? aimFOV : _baseFOV;
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, zoomSpeed * Time.deltaTime);
    }
}
