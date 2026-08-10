// FistHitboxRelay is no longer used.
// EnemyAttack switched to Physics.OverlapBox for hit detection (2026-08-10),
// so this relay script does nothing and can be safely removed from fist bone
// GameObjects (see FYP/setup-guides/MeleeHitbox_UnitySetup.md step 3).
// Kept as an empty stub, not deleted, to avoid missing-script warnings on
// CyberSoldier.prefab and any scene instances until that cleanup step is done.
public class FistHitboxRelay : UnityEngine.MonoBehaviour { }
