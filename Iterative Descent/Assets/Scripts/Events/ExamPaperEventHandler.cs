using UnityEngine;

/// <summary>
/// Listens for ExamPaperPuzzleUI.OnPuzzleFinished and spawns a guaranteed
/// ammo + health reward at the configured spawn point.
///
/// RULES (same as MatchingEventHandler / DrainEventHandler / StackEventHandler)
/// -----------------------------------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. GameManager).
///   - Do NOT add duplicate instances.
///   - Wire 'rewardSpawnPoint' in the Inspector (place it near the exam prop).
///   - ItemSpawner must be present in the scene (on GameManager).
/// </summary>
public class ExamPaperEventHandler : MonoBehaviour
{
    [Header("Reward spawn")]
    [Tooltip("Transform in world space where ammo + health will be dropped on solve.")]
    [SerializeField] private Transform rewardSpawnPoint;

    private void OnEnable()  => ExamPaperPuzzleUI.OnPuzzleFinished += HandleSolved;
    private void OnDisable() => ExamPaperPuzzleUI.OnPuzzleFinished -= HandleSolved;

    private void HandleSolved(int correctCount, int totalCount)
    {
        Debug.Log($"[ExamPaperEventHandler] '{gameObject.name}' -- " +
                  $"Exam Paper puzzle finished! Score: {correctCount}/{totalCount}");

        if (rewardSpawnPoint == null)
        {
            Debug.LogWarning("[ExamPaperEventHandler] No rewardSpawnPoint assigned -- reward not spawned.");
            return;
        }

        ItemSpawner.Instance?.GuaranteedSpawn(rewardSpawnPoint.position);
    }
}
