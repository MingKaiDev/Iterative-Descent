using UnityEngine;

/// <summary>
/// Listens for MatchingPuzzleUI.OnMatchingSolved and spawns a guaranteed
/// ammo + health reward at the configured spawn point.
///
/// RULES (same as DrainEventHandler / StackEventHandler)
/// -----------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. GameManager).
///   - Do NOT add duplicate instances.
///   - Wire 'rewardSpawnPoint' in the Inspector (place it near the terminal prop).
///   - ItemSpawner must be present in the scene (on GameManager).
/// </summary>
public class MatchingEventHandler : MonoBehaviour
{
    [Header("Reward spawn")]
    [Tooltip("Transform in world space where ammo + health will be dropped on solve.")]
    [SerializeField] private Transform rewardSpawnPoint;

    private void OnEnable()  => MatchingPuzzleUI.OnMatchingSolved += HandleSolved;
    private void OnDisable() => MatchingPuzzleUI.OnMatchingSolved -= HandleSolved;

    private void HandleSolved(int wrongSubmissions)
    {
        Debug.Log($"[MatchingEventHandler] '{gameObject.name}' -- " +
                  $"Matching puzzle solved! Wrong submissions: {wrongSubmissions}");

        if (rewardSpawnPoint == null)
        {
            Debug.LogWarning("[MatchingEventHandler] No rewardSpawnPoint assigned -- reward not spawned.");
            return;
        }

        ItemSpawner.Instance?.GuaranteedSpawn(rewardSpawnPoint.position);
    }
}
