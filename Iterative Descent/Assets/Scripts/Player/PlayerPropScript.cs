using UnityEngine;
using System.Linq;

public class PlayerPropScript : MonoBehaviour
{
    [Header("Gun Reference")]
    public GameObject gun; // Assign Glock17 in Inspector, or it will be found automatically

    [Header("Idle / Walk / Run Gun Transform")]
    public Vector3 idleLocalPosition = new Vector3(-0.15f, 0.37f, 0.09f);
    public Vector3 idleLocalRotation = new Vector3(13.9f, -113.3f, 68.32f);

    [Header("Aim / Fire Gun Transform")]
    public Vector3 aimLocalPosition = new Vector3(-0.15f, 0.37f, 0.09f); // Tune in Inspector
    public Vector3 aimLocalRotation = new Vector3(13.9f, -113.3f, 68.32f); // Tune in Inspector

    [Header("Transition")]
    public float gunTransitionSpeed = 10f;

    private PlayerCombat      _combat;
    private ShotgunController _shotgun;

    void Start()
    {
        _combat  = GetComponent<PlayerCombat>();
        _shotgun = GetComponent<ShotgunController>();

        // Find and parent the gun to the right hand bone
        Transform rightHand = GetComponentsInChildren<Transform>()
            .FirstOrDefault(t => t.name == "mixamorig:RightHand");

        if (rightHand != null)
        {
            // Auto-find gun if not assigned in Inspector
            if (gun == null)
                gun = GameObject.Find("Glock17");

            if (gun != null)
            {
                gun.transform.SetParent(rightHand);
                gun.transform.localPosition = idleLocalPosition;
                gun.transform.localRotation = Quaternion.Euler(idleLocalRotation);
            }
            else
            {
                Debug.LogWarning("Glock17 not found in scene.");
            }
        }
        else
        {
            Debug.LogWarning("mixamorig:RightHand bone not found.");
        }
    }

    void Update()
    {
        HandleGunTransform();
    }

    void HandleGunTransform()
    {
        if (gun == null) return;

        bool isAiming = (_combat  != null && _combat.enabled  && _combat.IsAiming)
                     || (_shotgun != null && _shotgun.enabled && _shotgun.IsAiming);

        Vector3 targetPos = isAiming ? aimLocalPosition : idleLocalPosition;
        Vector3 targetRot = isAiming ? aimLocalRotation : idleLocalRotation;

        gun.transform.localPosition = Vector3.Lerp(
            gun.transform.localPosition,
            targetPos,
            gunTransitionSpeed * Time.deltaTime
        );

        gun.transform.localRotation = Quaternion.Lerp(
            gun.transform.localRotation,
            Quaternion.Euler(targetRot),
            gunTransitionSpeed * Time.deltaTime
        );
    }
}