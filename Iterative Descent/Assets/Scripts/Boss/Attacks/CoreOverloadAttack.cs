using System.Collections;
using UnityEngine;

/// <summary>
/// DROID-7 Core Overload attack -- growing laser beam from the head.
///
/// Phase 1 -- Telegraph (PerformAttack -> OnHitboxOpen):
///   A dim tracking line follows the player during the charge-up animation.
///
/// Phase 2 -- Growing Beam (OnHitboxOpen):
///   Telegraph hides. A bright beam extends from LaserOrigin toward the player
///   at beamExtendSpeed units/second. Deals damage when the tip reaches the player.
///   Spawns a hit burst at the impact point.
///
/// Animation Event setup:
///   - "OnHitboxOpen" -- frame at the end of the charge-up (fires the beam)
///   - Do NOT add OnAttackAnimEnd -- the beam coroutine controls when the attack ends.
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject.
///   - Assign laserOrigin (child GO on head BONE with LineRenderer).
///   - Assign hitBurstPrefab (particle burst prefab).
///   - Assign playerLayer.
/// </summary>
public class CoreOverloadAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Beam Settings")]
    [Tooltip("Damage dealt when the beam tip reaches the player.")]
    public float damage = 35f;

    [Tooltip("How fast the beam extends in units per second.")]
    public float beamExtendSpeed = 15f;

    [Tooltip("Maximum range the beam travels if it misses the player.")]
    public float maxBeamRange = 20f;

    [Tooltip("How long the beam stays visible after fully extending.")]
    public float beamHoldDuration = 0.3f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("Telegraph")]
    [Tooltip("Child GO on the head BONE containing the LineRenderer.")]
    public Transform laserOrigin;

    [Tooltip("Dim colour shown during the charge-up telegraph phase.")]
    public Color telegraphColor = new Color(1f, 0.2f, 0.2f, 0.25f);

    [Tooltip("Bright colour of the fired beam.")]
    public Color beamColor = new Color(1f, 0.1f, 0.1f, 1f);

    [Header("VFX")]
    [Tooltip("Particle burst prefab spawned at the hit point.")]
    public GameObject hitBurstPrefab;

    [Header("Laser Loop Sound")]
    [Tooltip("Looping hum played on LaserOrigin's AudioSource for the whole time the beam is out " +
             "(from the moment it starts extending until it hides after beamHoldDuration). Stops " +
             "explicitly when the beam ends -- unlike hitboxOpenClip (a one-shot fire cue via the " +
             "shared boss AudioSource), this needs its own AudioSource on LaserOrigin because it " +
             "must be stoppable independently of every other boss sound.")]
    public AudioClip laserLoopClip;

    [Range(0f, 1f)]
    public float laserLoopVolume = 0.8f;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerCoreOverload = Animator.StringToHash("triggerCoreOverload");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator     _animator;
    private LineRenderer _lineRenderer;
    private Transform    _player;
    private AudioSource  _laserAudioSource;

    // ─── Private ────────────────────────────────────────────────────────────────
    private bool _isTelegraphing;
    private int  _wallLayer;

    void Start()
    {
        _animator  = GetComponent<Animator>();
        _wallLayer = LayerMask.GetMask("Level 1 Obstacle", "Layer 1 Floor");

        if (laserOrigin != null)
        {
            _lineRenderer     = laserOrigin.GetComponent<LineRenderer>();
            _laserAudioSource = laserOrigin.GetComponent<AudioSource>();
        }

        if (_lineRenderer == null)
            Debug.LogError("[CoreOverloadAttack] No LineRenderer on LaserOrigin.");

        if (_laserAudioSource == null)
            Debug.LogWarning("[CoreOverloadAttack] No AudioSource on LaserOrigin -- laserLoopClip " +
                              "will not play. Add an AudioSource there if you want the beam to hum.");
        else
        {
            _laserAudioSource.playOnAwake  = false;
            _laserAudioSource.loop         = true;
            _laserAudioSource.spatialBlend = 1f;
        }

        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) _player = ph.transform;

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerCoreOverload);
        _isTelegraphing = true;
        Debug.Log("[CoreOverloadAttack] Telegraph started.");
    }

    protected override void Update()
    {
        base.Update();

        if (!_isTelegraphing || _lineRenderer == null || laserOrigin == null) return;

        Vector3 origin = laserOrigin.position;
        Vector3 target = _player != null
            ? _player.position + Vector3.up
            : origin + laserOrigin.forward * maxBeamRange;

        _lineRenderer.startColor = telegraphColor;
        _lineRenderer.endColor   = telegraphColor;
        _lineRenderer.SetPosition(0, origin);
        _lineRenderer.SetPosition(1, target);
        _lineRenderer.enabled = true;
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        PlaySound(hitboxOpenClip);
        StartLaserLoop();

        _isTelegraphing = false;

        if (laserOrigin == null || _lineRenderer == null) return;

        Vector3 origin = laserOrigin.position;
        Vector3 target = _player != null
            ? _player.position + Vector3.up
            : origin + laserOrigin.forward * maxBeamRange;
        Vector3 direction = (target - origin).normalized;

        StartCoroutine(ExtendBeamRoutine(origin, direction));
    }

    /// <summary>Suppressed -- the beam coroutine controls when this attack ends.</summary>
    public override void OnAttackAnimEnd() { }

    // ─── RL-only target override ──────────────────────────────────────────────────

    /// <summary>Overrides the auto-found FindFirstObjectByType&lt;PlayerHealth&gt;() target.
    /// Added 2026-08-27: Training.unity's opponents use TrainingDummyHealth instead of
    /// PlayerHealth, so the automatic lookup in Start() always found nothing there -- the
    /// telegraph/beam fell back to laserOrigin.forward instead of tracking the opponent. Called
    /// by BossTrainingEnv each episode, mirroring BossStateMachine.SetTarget()/
    /// BossAttackRegistry.SetTarget(). Never called in the live game.</summary>
    public void SetTarget(Transform target) => _player = target;

    // ─── RL-only cancellation override ────────────────────────────────────────────

    /// <summary>See BossAttackBase.CancelAttack() for the full story. Unlike the other three
    /// RL-affected attacks this one doesn't touch the NavMeshAgent, but it has its own lingering
    /// state that StopAllCoroutines() alone won't clean up: _isTelegraphing stays true forever
    /// (Update() would keep redrawing a dim tracking line at the opponent indefinitely, cancelled
    /// attack or not, since that check doesn't look at IsActive), the beam/telegraph LineRenderer
    /// can be left enabled showing a frozen orphaned line, and the looping laser hum (if it had
    /// already started via OnHitboxOpen -> StartLaserLoop) would never stop. All three are cleaned
    /// up here.</summary>
    public override void CancelAttack()
    {
        bool wasActive = IsActive;
        base.CancelAttack();
        if (!wasActive) return;

        _isTelegraphing = false;
        if (_lineRenderer != null)
            _lineRenderer.enabled = false;
        StopLaserLoop();
    }

    // ─── Beam Coroutine ──────────────────────────────────────────────────────────

    IEnumerator ExtendBeamRoutine(Vector3 origin, Vector3 direction)
    {
        float currentLength = 0f;
        bool  hitSomething  = false;

        _lineRenderer.startColor = beamColor;
        _lineRenderer.endColor   = beamColor;
        _lineRenderer.SetPosition(0, origin);
        _lineRenderer.SetPosition(1, origin);
        _lineRenderer.enabled = true;

        while (currentLength < maxBeamRange && !hitSomething)
        {
            currentLength += beamExtendSpeed * Time.deltaTime;
            currentLength  = Mathf.Min(currentLength, maxBeamRange);

            Vector3 tip = origin + direction * currentLength;
            _lineRenderer.SetPosition(1, tip);

            // Wall check -- stop beam if it hits geometry.
            if (Physics.Raycast(origin, direction, out RaycastHit wallHit,
                                 currentLength, _wallLayer, QueryTriggerInteraction.Ignore))
            {
                _lineRenderer.SetPosition(1, wallHit.point);
                SpawnBurst(wallHit.point);
                hitSomething = true;
                break;
            }

            // Player check -- damage when the tip physically reaches the player.
            var hits = Physics.OverlapSphere(tip, 0.3f, playerLayer,
                                             QueryTriggerInteraction.Ignore);
            if (hits.Length > 0)
            {
                var damageable = hits[0].GetComponent<IDamageable>();
                damageable?.TakeDamage(damage, tip);
                SpawnBurst(tip);
                Debug.Log($"[CoreOverloadAttack] Beam hit player for {damage}.");
                hitSomething = true;
                break;
            }

            yield return null;
        }

        if (!hitSomething)
            SpawnBurst(origin + direction * maxBeamRange);

        // Hold beam briefly then hide.
        yield return new WaitForSeconds(beamHoldDuration);
        _lineRenderer.enabled = false;

        StopLaserLoop();
        EndAttack();
    }

    void SpawnBurst(Vector3 position)
    {
        if (hitBurstPrefab != null)
            Instantiate(hitBurstPrefab, position, Quaternion.identity);
    }

    // ─── Laser Loop Helpers ─────────────────────────────────────────────────────

    /// <summary>Starts the sustained beam hum. Called once, when the beam begins extending.</summary>
    void StartLaserLoop()
    {
        if (_laserAudioSource == null || laserLoopClip == null) return;
        _laserAudioSource.clip   = laserLoopClip;
        _laserAudioSource.volume = laserLoopVolume;
        _laserAudioSource.Play();
    }

    /// <summary>
    /// Stops the beam hum the instant the attack is over -- called from the end of
    /// ExtendBeamRoutine, right after the line renderer hides and before EndAttack().
    /// Safe to call even if StartLaserLoop() was never called (e.g. no clip assigned).
    /// </summary>
    void StopLaserLoop()
    {
        if (_laserAudioSource == null) return;
        _laserAudioSource.Stop();
    }
}
