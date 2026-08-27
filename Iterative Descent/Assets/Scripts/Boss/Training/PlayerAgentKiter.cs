using UnityEngine;

/// <summary>
/// Scripted training opponent: keeps its distance, retreating when the boss closes in
/// and repositioning with lateral sidesteps rather than standing still at range. One of
/// BossTrainingEnv's 3 opponent archetypes -- teaches BossAgent to handle a backpedaling
/// target (favors mobility attacks like SprintCharge/Pounce that close distance, over
/// pure melee that can't reach a kiter holding safeDistance).
/// </summary>
public class PlayerAgentKiter : PlayerAgentBase
{
    [Header("Kiter")]
    [Tooltip("Beyond this range, hold position (with periodic sidesteps) rather than retreating further.")]
    public float safeDistance = 16f;
    [Tooltip("Inside this range, retreat directly away from the boss.")]
    public float dangerDistance = 8f;
    public float retreatStepSize = 5f;
    [Tooltip("Degrees off the boss-facing line for the lateral sidestep while holding at safe range.")]
    public float sidestepAngle = 70f;
    [Tooltip("Average seconds between reposition sidesteps while holding at safe range.")]
    public float repositionInterval = 2.5f;
    public float repositionJitter = 2f;

    private float _repositionTimer;

    protected override void Awake()
    {
        base.Awake();
        _repositionTimer = Random.Range(0f, repositionInterval);
    }

    protected override void DecideMovement()
    {
        float dist = Vector3.Distance(transform.position, bossTransform.position);
        Vector3 awayFromBoss = (transform.position - bossTransform.position).normalized;

        if (dist < dangerDistance)
        {
            // Too close -- retreat directly away.
            Vector3 retreatPoint = transform.position + awayFromBoss * retreatStepSize;
            Agent.SetDestination(retreatPoint);
            return;
        }

        if (dist >= safeDistance)
        {
            // Comfortable range -- only reposition periodically, don't just stand still.
            _repositionTimer -= decisionInterval;
            if (_repositionTimer > 0f) return;
            _repositionTimer = Mathf.Max(0.5f, repositionInterval + Random.Range(-repositionJitter, repositionJitter));

            Vector3 sidestepDir = Quaternion.Euler(0f, Random.value < 0.5f ? sidestepAngle : -sidestepAngle, 0f) * awayFromBoss;
            Agent.SetDestination(transform.position + sidestepDir * (retreatStepSize * 0.5f));
            return;
        }

        // Between dangerDistance and safeDistance -- hold, no movement command needed this tick.
    }
}
