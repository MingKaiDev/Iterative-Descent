using UnityEngine;

/// <summary>
/// Attach to an EXTRA child collider that represents a specific body part (head,
/// limb, etc.) on an enemy that already has an EnemyBase-derived component
/// somewhere up its parent chain. Forwards damage to the enemy's real
/// TakeDamage(), scaled by damageMultiplier -- e.g. 2x on a head collider for a
/// headshot bonus. The enemy's existing main collider (capsule, etc.) is left
/// completely alone and keeps dealing normal (1x) damage exactly as before --
/// only add this component to NEW colliders you place for specific body parts.
///
/// ─── Setup ─────────────────────────────────────────────────────────────────
///   Root (EnemyChaser/EnemyBrute + NavMeshAgent + main body Collider)
///   └── CharacterRig (Animator)
///       └── ... → Head bone
///           └── HeadHitbox  (Collider, NOT a trigger -- same physics setup as
///                            the main body collider, on the same layer, so
///                            ShotgunPellet.OnCollisionEnter still fires
///                            normally -- + THIS script)
///                                damageMultiplier = 2  (or whatever headshot
///                                bonus you want)
///
///   1. Create an empty child GameObject under the head bone, position/scale it
///      to roughly cover the head.
///   2. Add a Collider (SphereCollider or CapsuleCollider both work well for a
///      head) -- leave "Is Trigger" OFF, same as the enemy's main collider.
///   3. Add this EnemyHitbox script, set damageMultiplier.
///   4. Repeat for any other body part you want a multiplier on (e.g. a limb
///      at 0.75x for reduced limb damage) -- one EnemyHitbox per collider.
///
/// ─── Why this needs no changes to weapon scripts ───────────────────────────
/// ShotgunPellet (and anything else that resolves damage via
/// collision.collider.GetComponentInParent&lt;IDamageable&gt;()) starts its
/// search at the collider's OWN GameObject before walking up the parent chain.
/// A pellet hitting HeadHitbox resolves to THIS script first -- since it also
/// implements IDamageable -- instead of walking further up to EnemyBase. No
/// other code needs to know body-part hitboxes exist.
/// </summary>
public class EnemyHitbox : MonoBehaviour, IDamageable
{
    [Tooltip("Multiplies incoming damage before forwarding it to the enemy's real " +
             "health. 2 = double damage (a typical headshot multiplier). Use a value " +
             "below 1 for a reduced-damage body part (e.g. a limb).")]
    [SerializeField] private float damageMultiplier = 2f;

    private EnemyBase _enemy;

    private void Awake()
    {
        // Deliberately typed as EnemyBase, not IDamageable -- searching for IDamageable
        // here would immediately re-match THIS component on the very first parent-chain
        // step (GetComponentInParent includes the starting object itself), causing an
        // infinite self-forward instead of walking up to the real enemy.
        _enemy = GetComponentInParent<EnemyBase>();
        if (_enemy == null)
            Debug.LogWarning($"[EnemyHitbox] No EnemyBase found in parents of '{name}' -- " +
                              "this hitbox will not forward damage anywhere. Check it was " +
                              "added under an EnemyChaser/EnemyBrute hierarchy.");
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_enemy == null) return;
        _enemy.TakeDamage(amount * damageMultiplier, hitPoint);
    }
}
