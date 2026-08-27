using UnityEngine;

/// <summary>
/// Scripted training opponent: closes distance and stays in the boss's face, firing
/// constantly. One of BossTrainingEnv's 3 opponent archetypes -- teaches BossAgent to
/// handle an in-your-face attacker (favors fast, close-range attacks like Slash/
/// LeftPunch/GroundSlam over slow wind-up moves like CoreOverload, which an aggressive
/// opponent can close through before it fires).
/// </summary>
public class PlayerAgentAggressive : PlayerAgentBase
{
    [Header("Aggressive")]
    [Tooltip("Once within this range of the boss, stop closing in and jitter-circle instead of colliding with it.")]
    public float chargeRange = 3f;
    [Tooltip("Radius of the small circling jitter applied once inside chargeRange.")]
    public float jitterCircle = 1f;
    [Tooltip("Angular speed (radians/sec) of the chargeRange jitter circle.")]
    public float jitterOrbitSpeed = 0.4f;

    private float _jitterAngle;

    protected override void DecideMovement()
    {
        float dist = Vector3.Distance(transform.position, bossTransform.position);

        if (dist <= chargeRange)
        {
            // Already in the boss's face -- jitter around it instead of standing still / colliding.
            _jitterAngle += jitterOrbitSpeed * decisionInterval;
            Vector3 offset = new Vector3(Mathf.Cos(_jitterAngle), 0f, Mathf.Sin(_jitterAngle)) * jitterCircle;
            Agent.SetDestination(bossTransform.position + offset);
        }
        else
        {
            Agent.SetDestination(bossTransform.position);
        }
    }
}
