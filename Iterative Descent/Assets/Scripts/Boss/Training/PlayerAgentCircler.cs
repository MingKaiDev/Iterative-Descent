using UnityEngine;

/// <summary>
/// Scripted training opponent: holds mid-range and orbits the boss instead of closing
/// in or backing off. One of BossTrainingEnv's 3 opponent archetypes -- teaches BossAgent
/// to handle lateral movement (favors attacks with tracking/area coverage like SprintCharge/
/// Pounce/CoreOverload over attacks that whiff against a target that never stops moving).
/// </summary>
public class PlayerAgentCircler : PlayerAgentBase
{
    [Header("Circler")]
    public float orbitRadius = 9f;
    [Tooltip("Angular speed (radians/sec) around the boss.")]
    public float orbitSpeed = 1.2f;
    [Tooltip("How far outside orbitRadius the agent is allowed to drift before correcting.")]
    public float radiusTolerance = 2.5f;
    public bool  randomFlipDirection = true;
    [Tooltip("Average number of full orbits between direction flips, when randomFlipDirection is true.")]
    public float flipFrequencyOrbits = 2f;

    private float _orbitAngle;
    private float _direction = 1f;
    private float _orbitsSinceFlip;

    protected override void Awake()
    {
        base.Awake();
        // Random start angle so multiple Circler instances (if ever active together) don't sync.
        _orbitAngle = Random.Range(0f, Mathf.PI * 2f);
    }

    protected override void DecideMovement()
    {
        float deltaAngle = orbitSpeed * _direction * decisionInterval;
        _orbitAngle += deltaAngle;
        _orbitsSinceFlip += Mathf.Abs(deltaAngle) / (Mathf.PI * 2f);

        if (randomFlipDirection && flipFrequencyOrbits > 0f && _orbitsSinceFlip >= flipFrequencyOrbits)
        {
            if (Random.value < 0.5f)
                _direction *= -1f;
            _orbitsSinceFlip = 0f;
        }

        float dist = Vector3.Distance(transform.position, bossTransform.position);
        float targetRadius = orbitRadius;
        if (Mathf.Abs(dist - orbitRadius) > radiusTolerance)
            targetRadius = Mathf.Lerp(dist, orbitRadius, 0.5f); // correct gradually back toward the ring

        Vector3 offset = new Vector3(Mathf.Cos(_orbitAngle), 0f, Mathf.Sin(_orbitAngle)) * targetRadius;
        Agent.SetDestination(bossTransform.position + offset);
    }
}
