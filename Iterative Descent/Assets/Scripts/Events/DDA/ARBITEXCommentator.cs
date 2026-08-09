using UnityEngine;

/// <summary>
/// Listens for DDA tier changes from both PuzzleDDAController and CombatDDAController.
/// When a tier change fires, picks a random DialogueSequence from the matching pool
/// and enqueues it through DialogueManager so ARBITEX reacts to the player's performance.
///
/// Pools are assigned in the Inspector as arrays of DialogueSequence ScriptableObjects.
/// Each pool may contain any number of sequences; one is picked at random each time.
/// Leave a pool empty to suppress ARBITEX commentary for that tier.
///
/// Cooldown guard: a minimum number of seconds must pass between any two ARBITEX
/// comments to prevent spam when the tier oscillates rapidly.
///
/// Attach to the persistent GameManager GameObject alongside PuzzleDDAController,
/// CombatDDAController, and DialogueManager.
/// </summary>
public class ARBITEXCommentator : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static ARBITEXCommentator Instance { get; private set; }

    // ── Inspector: Intro ──────────────────────────────────────────────────────
    [Header("Intro (Story 10)")]
    [Tooltip("Played exactly once, the first time ANY enemy dies (EnemyBase.OnAnyEnemyDied). " +
             "No tier-change commentary plays before this has fired -- see OnTierChanged's " +
             "guard below. First entry's speakerName should be '???' with later entries " +
             "switching to 'ARBITEX' once the reveal line lands, so SubtitleUI's speaker-colour " +
             "swap (white -> red) visually reinforces the reveal.")]
    [SerializeField] private DialogueSequence introSequence;

    // ── Inspector: Line Pools ─────────────────────────────────────────────────
    [Header("Tier 0 - Very Easy (bored / condescending)")]
    [Tooltip("Played when DDA drops to Tier 0. ARBITEX is dismissive.")]
    [SerializeField] private DialogueSequence[] tier0Lines;

    [Header("Tier 1 - Easy (condescending)")]
    [Tooltip("Played when DDA reaches Tier 1.")]
    [SerializeField] private DialogueSequence[] tier1Lines;

    [Header("Tier 2 - Normal (neutral / observational)")]
    [Tooltip("Played when DDA reaches Tier 2.")]
    [SerializeField] private DialogueSequence[] tier2Lines;

    [Header("Tier 3 - Hard (unsettled)")]
    [Tooltip("Played when DDA reaches Tier 3. ARBITEX is beginning to notice.")]
    [SerializeField] private DialogueSequence[] tier3Lines;

    [Header("Tier 4 - Very Hard (threatened / urgent)")]
    [Tooltip("Played when DDA reaches Tier 4. ARBITEX is alarmed.")]
    [SerializeField] private DialogueSequence[] tier4Lines;

    // ── Inspector: Cooldown ───────────────────────────────────────────────────
    [Header("Cooldown")]
    [Tooltip("Minimum seconds between any two ARBITEX comments. " +
             "Prevents spam if tier oscillates rapidly.")]
    [SerializeField] private float cooldownSeconds = 20f;

    // ── State ─────────────────────────────────────────────────────────────────
    // Uses realtimeSinceStartup so the cooldown is not affected by Time.timeScale.
    private float _lastCommentTime = -9999f;

    // One-shot guard: true once the intro sequence has fired (first enemy death).
    // Tier-change reactive lines are suppressed entirely until this is true --
    // see OnTierChanged().
    private bool _hasIntroduced = false;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        PuzzleDDAController.OnPuzzleTierChanged += OnTierChanged;
        CombatDDAController.OnCombatTierChanged  += OnTierChanged;
        EnemyBase.OnAnyEnemyDied                 += HandleFirstEnemyDied;
    }

    private void OnDisable()
    {
        PuzzleDDAController.OnPuzzleTierChanged -= OnTierChanged;
        CombatDDAController.OnCombatTierChanged  -= OnTierChanged;
        EnemyBase.OnAnyEnemyDied                 -= HandleFirstEnemyDied;
    }

    // ── Intro (Story 10) ──────────────────────────────────────────────────────
    // Fires once, on the first enemy death of the session (normally the Combat 1
    // breach enemy). Does NOT go through the cooldown/tier-pool machinery below --
    // it is a one-time story beat, not reactive commentary.
    private void HandleFirstEnemyDied(Vector3 deathPosition)
    {
        if (_hasIntroduced) return;
        _hasIntroduced = true;

        if (introSequence == null)
        {
            Debug.LogWarning("[ARBITEXCommentator] introSequence not assigned -- " +
                              "reactive tier commentary will remain suppressed forever. " +
                              "Assign Arb_Intro in the Inspector.");
            return;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[ARBITEXCommentator] DialogueManager.Instance is null. " +
                              "Ensure DialogueManager is on the GameManager.");
            return;
        }

        DialogueManager.Instance.Enqueue(introSequence);
        _lastCommentTime = Time.realtimeSinceStartup; // also respect cooldown for the next reactive line
        Debug.Log("[ARBITEXCommentator] Intro fired on first enemy death -- reactive commentary now active.");
    }

    // ── Event Handler ─────────────────────────────────────────────────────────
    private void OnTierChanged(int newTier)
    {
        // Suppress ALL reactive commentary until ARBITEX has introduced itself
        // (see HandleFirstEnemyDied above). A tier change that happens before
        // the intro (e.g. during the Quiz 1 / Linked List puzzle in Main Hall,
        // before Combat 1 ends) is silently dropped, not queued -- by the time
        // the intro plays it would no longer reflect current performance.
        if (!_hasIntroduced) return;

        // Cooldown guard: too soon since the last comment?
        if (Time.realtimeSinceStartup - _lastCommentTime < cooldownSeconds)
            return;

        DialogueSequence[] pool = GetPool(newTier);
        if (pool == null || pool.Length == 0) return;

        // Pick a random sequence from the pool.
        // Using UnityEngine.Random explicitly to avoid Linq ambiguity.
        DialogueSequence pick = pool[UnityEngine.Random.Range(0, pool.Length)];
        if (pick == null) return;

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[ARBITEXCommentator] DialogueManager.Instance is null. " +
                             "Ensure DialogueManager is on the GameManager.");
            return;
        }

        DialogueManager.Instance.Enqueue(pick);
        _lastCommentTime = Time.realtimeSinceStartup;

        Debug.Log($"[ARBITEXCommentator] Tier {newTier} -> enqueued '{pick.name}'");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private DialogueSequence[] GetPool(int tier)
    {
        return tier switch
        {
            0 => tier0Lines,
            1 => tier1Lines,
            2 => tier2Lines,
            3 => tier3Lines,
            4 => tier4Lines,
            _ => null
        };
    }
}
