using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Real-time "conveyor belt" hash-table puzzle.
///
/// Kitchen framing: numbered food orders roll in on a conveyor belt from the
/// left. The player holds W / S to scroll a column of pickup baskets so the
/// correct basket (order # mod table size) is lined up with the fixed chute
/// before each order reaches the end of the belt. Early tiers (Very Easy /
/// Easy) use table sizes 2 and 5 specifically because mod-2 (odd/even) and
/// mod-5 (last digit) are the two moduli most people can do in their head
/// without working through long division -- Hard / Very Hard step up to
/// harder moduli AND switch collision resolution from chaining to linear
/// probing (shift forward, wrapping past the last basket, until you find an
/// empty one).
///
/// The order's food sprite (burger / fries / pasta / ...) is purely a skin:
/// FoodFamily = orderNumber / 10, so orders 0-9 always render as the same
/// food, 10-19 the next, etc. It carries no gameplay meaning by itself --
/// the number underneath is what the player actually needs, so recognising
/// the food is a bonus memory aid, never a requirement.
///
/// Difficulty tier (0 Very Easy .. 4 Very Hard) is pulled once per round
/// from PuzzleDDAController, exactly like every other puzzle in the project
/// (StackPuzzleUI, LinkedListPuzzleUI, etc.) -- there is no player-facing
/// tier picker; the Director chooses.
///
/// Wiring mirrors StackPuzzleProp/StackPuzzleUI:
///   InitPuzzle(onClose) is called by HashTablePuzzleProp when it opens the
///   overlay. OnHashTableSolved(int spilled) fires once the round's spawn
///   queue AND belt are both empty -- "spilled" (a wrong basket at arrival)
///   is this puzzle's equivalent of Stack's "wrong attempts": the round
///   always completes (the door still opens), spills only feed the DDA
///   telemetry, same convention as LinkedList/Stack.
/// </summary>
public class HashTablePuzzleUI : MonoBehaviour
{
    // Singleton set in Awake() -- same cross-scene-safe pattern as every other
    // puzzle UI in this project (see BstPuzzleUI.Instance). Lets
    // HashTablePuzzleProp resolve this shared Canvas panel at runtime instead
    // of relying only on a serialized Inspector reference, which goes stale
    // across a Level 1 -> Level 2 -> Level 1 scene reload.
    public static HashTablePuzzleUI Instance { get; private set; }

    // ── Tier data ────────────────────────────────────────────────────────────
    [Serializable]
    private struct Tier
    {
        public string label;
        public int tableSize;
        public int orderCount;
        public bool probeMode;   // false = chaining, true = linear probing
        public bool forceWrap;   // Very Hard: guarantee a probe sequence that wraps past the last basket
        public float travelSeconds;
        public float spawnSeconds;
        public float shiftCooldown;
    }

    private static readonly Tier[] TIERS =
    {
        new Tier { label = "Very Easy", tableSize = 2, orderCount = 4, probeMode = false, forceWrap = false, travelSeconds = 6.0f, spawnSeconds = 2.6f, shiftCooldown = 0.16f },
        new Tier { label = "Easy",      tableSize = 5, orderCount = 6, probeMode = false, forceWrap = false, travelSeconds = 5.0f, spawnSeconds = 2.2f, shiftCooldown = 0.15f },
        new Tier { label = "Normal",    tableSize = 7, orderCount = 7, probeMode = false, forceWrap = false, travelSeconds = 4.0f, spawnSeconds = 2.0f, shiftCooldown = 0.145f },
        new Tier { label = "Hard",      tableSize = 5, orderCount = 5, probeMode = true,  forceWrap = false, travelSeconds = 3.6f, spawnSeconds = 2.1f, shiftCooldown = 0.125f },
        new Tier { label = "Very Hard", tableSize = 4, orderCount = 4, probeMode = true,  forceWrap = true,  travelSeconds = 3.0f, spawnSeconds = 2.0f, shiftCooldown = 0.11f },
    };

    private const int KEY_MAX = 99; // keys are 0-99 so FoodFamily (key/10) always lands in 0-9
    private const int FOOD_FAMILIES = 10;
    private const int VISIBLE_ROWS = 3; // rows rendered above/below the centre row

    // ── Inspector: layout ────────────────────────────────────────────────────
    [Header("Header / Status")]
    [SerializeField] private TextMeshProUGUI tierBadgeText;
    [SerializeField] private TextMeshProUGUI formulaText;
    [SerializeField] private TextMeshProUGUI ruleHintText;
    [SerializeField] private TextMeshProUGUI sortedText;
    [SerializeField] private TextMeshProUGUI spilledText;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Belt")]
    [SerializeField] private RectTransform beltTrack;      // orders are spawned as children and slide along +X
    [SerializeField] private RectTransform orderTemplate;  // inactive child of beltTrack: Image (food icon) + "NumberText" (TMP)
    [SerializeField] private RectTransform chuteMarker;    // fixed marker at the belt's drop point (visual only)

    [Header("Baskets")]
    [SerializeField] private RectTransform bucketWindow;       // scrolls vertically inside a masked viewport
    [SerializeField] private RectTransform bucketRowTemplate;  // inactive child of bucketWindow: Image (basket) + "IndexText" + "ContentsText"
    [SerializeField] private float rowHeight = 128f;
    [Tooltip("Fixed bracket/cage overlay in the Basket Viewport (sibling of bucketWindow, NOT a child of it) " +
             "marking whichever row is currently centred -- i.e. the basket actually under the chute right now.")]
    [SerializeField] private RectTransform selectorFrame;

    [Header("Food sprites (index = key / 10)")]
    [SerializeField] private Sprite[] foodSprites = new Sprite[FOOD_FAMILIES];

    [Header("Feedback")]
    [SerializeField] private HashTableFeedbackPopup feedbackPopup;

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Audio")]
    [Tooltip("Looping AudioSource dedicated to the conveyor belt's running sound -- " +
             "kept separate from sfxAudioSource so a correct/wrong one-shot never " +
             "cuts the loop off. Starts on InitPuzzle(), stops on close.")]
    [SerializeField] private AudioSource beltAudioSource;
    [Tooltip("Continuous belt-running sound. Looped for as long as the puzzle is open.")]
    [SerializeField] private AudioClip beltLoopClip;
    [Tooltip("Belt loop volume. Applied every time the loop (re)starts, and live while " +
             "the puzzle is open in Play Mode so it can be tuned without stopping.")]
    [Range(0f, 1f)]
    [SerializeField] private float beltVolume = 0.25f;

    [Tooltip("AudioSource used for one-shot correct/wrong feedback stings.")]
    [SerializeField] private AudioSource sfxAudioSource;
    [Tooltip("Played when an order lands in the right basket.")]
    [SerializeField] private AudioClip correctSound;
    [Tooltip("Played when an order spills (wrong basket).")]
    [SerializeField] private AudioClip wrongSound;
    [Tooltip("Volume for both correct/wrong one-shot stings.")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.4f;

    // Minimum fraction of attempted orders that must land in the right basket
    // for the round to be "completed" (door-unlock-worthy). Below this, the
    // round still reports its raw stats via OnHashTableSolved (for DDA/BKT
    // telemetry, same as every attempt), it just isn't graded as a pass.
    private const float PASS_THRESHOLD = 0.5f;

    // ── Static event (mirrors StackPuzzleUI.OnStackSolved) ──────────────────
    public static event Action<int> OnHashTableSolved; // arg = crates spilled this round

    // Fires alongside OnHashTableSolved with the pass/fail verdict for the
    // round (sorted >= PASS_THRESHOLD of attempted). Anything that should only
    // happen on a genuine "completed" run -- e.g. unlocking a door -- should
    // key off THIS event rather than assuming OnHashTableSolved means success.
    public static event Action<bool> OnHashTableAttemptGraded; // arg = passed (>= 50% sorted)

    // Fires when the panel actually closes (feedback popup OK'd, or the panel's
    // own X button used as an escape hatch) -- i.e. once the player is back in
    // gameplay and looking at the room again. OnHashTableSolved fires the moment
    // the round finishes, which can be well BEFORE the player dismisses the
    // "LINE CLEARED" popup (a player-paced click, not a timer) -- anything that
    // should feel like it happens "in the room" (e.g. an ambush reveal) should
    // key off THIS event instead, not OnHashTableSolved, or it can fire while
    // the puzzle overlay still covers the screen.
    public static event Action OnHashTablePanelClosed;

    // ── Runtime state ────────────────────────────────────────────────────────
    private Action _onClose;
    private Tier _tier;
    private int _virtualIndex;
    private float _bucketWindowVisualY;
    private List<int>[] _buckets;
    private Queue<Order> _spawnQueue;
    private List<BeltOrder> _belt;
    private float _nextSpawnTimer;
    private float _shiftTimer;
    private float _elapsed;
    private int _sorted;
    private int _spilled;
    private bool _running;
    private bool _finished;

    private struct Order
    {
        public int key;
        public int home;
    }

    private class BeltOrder
    {
        public Order order;
        public float progress; // 0..1 along the belt
        public RectTransform rect;
        public Image icon;
        public TextMeshProUGUI numberText;
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        Instance = this;
        if (orderTemplate != null) orderTemplate.gameObject.SetActive(false);
        if (bucketRowTemplate != null) bucketRowTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(RequestClose);
    }

    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(RequestClose);
        StopBeltAudio(); // covers the panel being SetActive(false)'d by HashTablePuzzleProp.ClosePuzzle()
    }

    // ── Audio ────────────────────────────────────────────────────────────────
    private void StartBeltAudio()
    {
        if (beltAudioSource == null || beltLoopClip == null) return;
        beltAudioSource.clip = beltLoopClip;
        beltAudioSource.loop = true;
        beltAudioSource.playOnAwake = false;
        beltAudioSource.spatialBlend = 0f; // 2D -- same volume regardless of camera position, matches GameAudioManager's ambient source
        beltAudioSource.volume = beltVolume;
        if (!beltAudioSource.isPlaying) beltAudioSource.Play();
    }

    private void StopBeltAudio()
    {
        if (beltAudioSource != null && beltAudioSource.isPlaying) beltAudioSource.Stop();
    }

    /// <summary>
    /// Lets beltVolume be scrubbed live in the Inspector while the puzzle is
    /// open in Play Mode, instead of only taking effect on the next
    /// InitPuzzle(). sfxVolume doesn't need this -- it's read fresh on every
    /// PlayOneShot() call anyway.
    /// </summary>
    private void OnValidate()
    {
        if (beltAudioSource != null) beltAudioSource.volume = beltVolume;
    }

    private void Update()
    {
        if (!_running) return;

        float dt = Time.unscaledDeltaTime;
        _elapsed += dt;
        if (timerText != null) timerText.text = $"TIME  {_elapsed:F1}s";

        HandleShiftInput(dt);
        SmoothBucketWindow(dt);
        MaybeSpawn(dt);
        UpdateBelt(dt);
        CheckFinished();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Called by HashTablePuzzleProp when the overlay is opened.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose = onClose;

        if (feedbackPopup != null) feedbackPopup.gameObject.SetActive(false);

        PlayerMetricsTracker.Instance?.NotifyHashTableStarted();

        int tierIndex = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.HashTables)
            : 2;
        tierIndex = Mathf.Clamp(tierIndex, 0, TIERS.Length - 1);
        _tier = TIERS[tierIndex];

        if (tierBadgeText != null) tierBadgeText.text = _tier.label.ToUpperInvariant();
        if (formulaText != null) formulaText.text = $"BASKET = ORDER # mod {_tier.tableSize}";
        if (ruleHintText != null)
        {
            ruleHintText.text = _tier.probeMode
                ? "W / S -- shift baskets. Full basket? The order shifts to the next empty one, wrapping past the last."
                : "W / S -- shift baskets so the right one is under the chute before each order arrives.";
        }

        RecomputeLayoutSizes();

        _buckets = new List<int>[_tier.tableSize];
        for (int i = 0; i < _tier.tableSize; i++) _buckets[i] = new List<int>();

        _spawnQueue = new Queue<Order>(GenerateOrders(_tier));
        _belt = new List<BeltOrder>();
        _virtualIndex = _tier.tableSize / 2;
        _bucketWindowVisualY = _virtualIndex * rowHeight;
        _nextSpawnTimer = _tier.spawnSeconds * 0.45f;
        _shiftTimer = 0f;
        _elapsed = 0f;
        _sorted = 0;
        _spilled = 0;
        _finished = false;

        StartBeltAudio();

        RenderBuckets();
        RenderStatus();
        _running = true;
    }

    public void ClosePanel()
    {
        OnHashTablePanelClosed?.Invoke();
        _onClose?.Invoke();
    }

    /// <summary>
    /// Sizes the order-crate template and the basket-row template (and
    /// therefore rowHeight) as fractions of their actual parent viewport's
    /// rect at runtime, instead of relying on fixed design-time pixel
    /// constants. The panel is meant to be dropped into whatever Canvas
    /// scale a scene happens to use, so anything hand-tuned in the Editor
    /// builder for one canvas scale can look wildly wrong (tiny/huge) on
    /// another -- deriving sizes from the real RectTransforms at InitPuzzle
    /// time keeps the belt crates and basket rows proportioned correctly
    /// regardless of that scale.
    /// </summary>
    private void RecomputeLayoutSizes()
    {
        // Force Unity's deferred layout system to rebuild NOW. Without this,
        // a call made in the same frame as SetActive(true) (which is exactly
        // how HashTablePuzzleProp opens this panel) can read stale/zero
        // .rect values on beltViewport/basketViewport below, because Unity
        // doesn't recompute layout until the next actual layout pass.
        Canvas.ForceUpdateCanvases();

        if (beltTrack != null && orderTemplate != null && beltTrack.parent is RectTransform beltViewport)
        {
            float laneH = beltViewport.rect.height;
            if (laneH > 1f)
            {
                float iconH = laneH * 0.86f;
                float iconW = iconH * 0.75f;
                orderTemplate.sizeDelta = new Vector2(iconW, iconH);
            }
        }

        if (bucketWindow != null && bucketWindow.parent is RectTransform basketViewport)
        {
            float viewportH = basketViewport.rect.height;
            if (viewportH > 1f)
            {
                rowHeight = Mathf.Max(viewportH / (2f * VISIBLE_ROWS + 1f), 32f);
                bucketWindow.sizeDelta = new Vector2(bucketWindow.sizeDelta.x, rowHeight);
                if (bucketRowTemplate != null)
                {
                    bucketRowTemplate.sizeDelta = new Vector2(bucketRowTemplate.sizeDelta.x, rowHeight - 10f);

                    // The icon and the two text columns inside the template were
                    // laid out against the Editor-time rowHeight guess -- rescale
                    // them to the real rowHeight too so nothing overlaps/floats.
                    float iconSize = (rowHeight - 10f) * 0.8f;
                    if (bucketRowTemplate.Find("Basket Icon") is RectTransform basketIcon)
                    {
                        basketIcon.sizeDelta = new Vector2(iconSize, iconSize);
                        basketIcon.anchoredPosition = new Vector2(6f, 0f);
                    }
                    float textLeft = iconSize + 14f;
                    if (bucketRowTemplate.Find("Index Text") is RectTransform indexRect)
                        indexRect.offsetMin = new Vector2(textLeft, indexRect.offsetMin.y);
                    if (bucketRowTemplate.Find("Contents Text") is RectTransform contentsRect)
                        contentsRect.offsetMin = new Vector2(textLeft, contentsRect.offsetMin.y);
                }

                // Keep the "equipped basket" cage the same height as an actual
                // row (plus a small outward margin) so it still frames the
                // centred row correctly after rowHeight is recalculated above.
                if (selectorFrame != null)
                    selectorFrame.sizeDelta = new Vector2(selectorFrame.sizeDelta.x, rowHeight + 10f);
            }
        }
    }

    // ── Order generation (ported from the HTML prototype) ───────────────────
    private static List<Order> GenerateOrders(Tier tier)
    {
        var used = new HashSet<int>();
        var raw = new List<int>();
        var rng = new System.Random();

        while (raw.Count < tier.orderCount)
        {
            int candidate = rng.Next(0, KEY_MAX + 1);
            if (used.Add(candidate)) raw.Add(candidate);
        }

        // Guarantee at least one collision (or, for Very Hard, a forced wrap)
        // by re-homing the second key onto the first key's home bucket.
        if (tier.orderCount >= 2)
        {
            int targetHome = tier.forceWrap ? tier.tableSize - 1 : raw[0] % tier.tableSize;
            bool alreadyCollides = false;
            for (int i = 1; i < raw.Count; i++)
                if (raw[i] % tier.tableSize == targetHome) { alreadyCollides = true; break; }

            if (!alreadyCollides)
            {
                int candidate = targetHome;
                while (candidate <= KEY_MAX && (used.Contains(candidate) || candidate == raw[0]))
                    candidate += tier.tableSize;
                if (candidate <= KEY_MAX)
                {
                    used.Remove(raw[1]);
                    used.Add(candidate);
                    raw[1] = candidate;
                }
            }
        }

        var orders = new List<Order>(raw.Count);
        foreach (int k in raw) orders.Add(new Order { key = k, home = k % tier.tableSize });
        return orders;
    }

    // ── Input ────────────────────────────────────────────────────────────────
    private void HandleShiftInput(float dt)
    {
        _shiftTimer -= dt;
        if (_shiftTimer > 0f) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
        {
            _virtualIndex--;
            _shiftTimer = _tier.shiftCooldown;
        }
        else if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
        {
            _virtualIndex++;
            _shiftTimer = _tier.shiftCooldown;
        }
        else
        {
            return;
        }

        RenderBuckets();
    }

    private int CurrentLogicalIndex()
    {
        int m = _tier.tableSize;
        return ((_virtualIndex % m) + m) % m;
    }

    // ── Belt ─────────────────────────────────────────────────────────────────
    private void MaybeSpawn(float dt)
    {
        if (_spawnQueue.Count == 0) return;
        _nextSpawnTimer -= dt;
        if (_nextSpawnTimer > 0f) return;

        _nextSpawnTimer = _tier.spawnSeconds;
        Order order = _spawnQueue.Dequeue();
        SpawnBeltOrder(order);
    }

    private void SpawnBeltOrder(Order order)
    {
        if (orderTemplate == null) return;

        RectTransform rect = Instantiate(orderTemplate, beltTrack);
        rect.gameObject.SetActive(true);
        rect.anchoredPosition = new Vector2(0f, orderTemplate.anchoredPosition.y);

        int family = Mathf.Clamp(order.key / 10, 0, FOOD_FAMILIES - 1);
        Image icon = rect.GetComponent<Image>();
        if (icon != null && foodSprites != null && family < foodSprites.Length && foodSprites[family] != null)
            icon.sprite = foodSprites[family];

        TextMeshProUGUI numberText = rect.GetComponentInChildren<TextMeshProUGUI>();
        if (numberText != null) numberText.text = order.key.ToString();

        _belt.Add(new BeltOrder { order = order, progress = 0f, rect = rect, icon = icon, numberText = numberText });
    }

    private void UpdateBelt(float dt)
    {
        if (beltTrack == null) return;
        float trackWidth = Mathf.Max(beltTrack.rect.width, 1f);
        float startX = 0f;
        float endX = trackWidth - (orderTemplate != null ? orderTemplate.rect.width : 0f);

        for (int i = _belt.Count - 1; i >= 0; i--)
        {
            BeltOrder b = _belt[i];
            b.progress += dt / _tier.travelSeconds;
            if (b.progress >= 1f)
            {
                Evaluate(b);
                Destroy(b.rect.gameObject);
                _belt.RemoveAt(i);
                continue;
            }
            float x = Mathf.Lerp(startX, endX, b.progress);
            b.rect.anchoredPosition = new Vector2(x, b.rect.anchoredPosition.y);
        }
    }

    private void Evaluate(BeltOrder b)
    {
        int idx = CurrentLogicalIndex();
        bool ok = _tier.probeMode
            ? ExpectedProbeSlot(b.order.home, _buckets, _tier.tableSize) == idx
            : b.order.home == idx;

        if (ok)
        {
            _buckets[idx].Add(b.order.key);
            _sorted++;
            if (sfxAudioSource != null && correctSound != null) sfxAudioSource.PlayOneShot(correctSound, sfxVolume);
        }
        else
        {
            _spilled++;
            if (sfxAudioSource != null && wrongSound != null) sfxAudioSource.PlayOneShot(wrongSound, sfxVolume);
        }

        RenderBuckets();
        RenderStatus();
    }

    private static int ExpectedProbeSlot(int home, List<int>[] buckets, int tableSize)
    {
        int i = home;
        for (int step = 0; step < tableSize; step++)
        {
            if (buckets[i].Count == 0) return i;
            i = (i + 1) % tableSize;
        }
        return home; // table completely full (shouldn't happen given orderCount <= tableSize for probe tiers)
    }

    // ── Baskets ──────────────────────────────────────────────────────────────
    private void SmoothBucketWindow(float dt)
    {
        if (bucketWindow == null) return;
        float target = _virtualIndex * rowHeight;
        _bucketWindowVisualY = Mathf.Lerp(_bucketWindowVisualY, target, 1f - Mathf.Exp(-18f * dt));
        bucketWindow.anchoredPosition = new Vector2(bucketWindow.anchoredPosition.x, _bucketWindowVisualY);
    }

    private readonly List<RectTransform> _spawnedRows = new List<RectTransform>();

    private void RenderBuckets()
    {
        if (bucketRowTemplate == null || bucketWindow == null) return;

        foreach (RectTransform r in _spawnedRows) if (r != null) Destroy(r.gameObject);
        _spawnedRows.Clear();

        int m = _tier.tableSize;
        for (int i = _virtualIndex - VISIBLE_ROWS; i <= _virtualIndex + VISIBLE_ROWS; i++)
        {
            int logical = ((i % m) + m) % m;
            RectTransform row = Instantiate(bucketRowTemplate, bucketWindow);
            row.gameObject.SetActive(true);
            row.anchoredPosition = new Vector2(0f, -i * rowHeight);

            TextMeshProUGUI[] texts = row.GetComponentsInChildren<TextMeshProUGUI>();
            TextMeshProUGUI indexText = texts.Length > 0 ? texts[0] : null;
            TextMeshProUGUI contentsText = texts.Length > 1 ? texts[1] : null;

            if (indexText != null) indexText.text = $"BASKET {logical}";
            if (contentsText != null)
            {
                List<int> contents = _buckets[logical];
                contentsText.text = contents.Count == 0 ? "-- empty --" : string.Join(" -> ", contents);
            }

            _spawnedRows.Add(row);
        }
    }

    private void RenderStatus()
    {
        if (sortedText != null) sortedText.text = $"SORTED  {_sorted}";
        if (spilledText != null) spilledText.text = $"SPILLED  {_spilled}";
    }

    // ── Finish ───────────────────────────────────────────────────────────────

    /// <summary>Sorted at least PASS_THRESHOLD of everything attempted so far. total==0 (no orders even reached the chute) never counts as a pass.</summary>
    private bool RoundPassed(int total) => total > 0 && (float)_sorted / total >= PASS_THRESHOLD;

    private void CheckFinished()
    {
        if (_finished || _spawnQueue.Count > 0 || _belt.Count > 0) return;

        _finished = true;
        _running = false;

        int total = _sorted + _spilled;
        bool clean = _spilled == 0;
        bool passed = RoundPassed(total);
        string message = clean
            ? $"LINE CLEARED\n{_sorted}/{total} orders sorted clean in {_elapsed:F1}s."
            : passed
                ? $"LINE CLEARED\n{_sorted}/{total} sorted, {_spilled} spilled, {_elapsed:F1}s."
                : $"LINE FAILED\n{_sorted}/{total} sorted, {_spilled} spilled -- need at least {PASS_THRESHOLD:P0} sorted.";

        OnHashTableSolved?.Invoke(_spilled);
        OnHashTableAttemptGraded?.Invoke(passed);

        if (feedbackPopup != null)
            feedbackPopup.Show(passed, message, ClosePanel);
        else
            ClosePanel();
    }

    private void RequestClose()
    {
        // Close button always available as an escape hatch; treat as ending the
        // round with whatever was sorted/spilled so far so the door still
        // reflects an honest attempt rather than softlocking the player.
        if (_finished) { ClosePanel(); return; }
        if (!_running) return;

        _finished = true;
        _running = false;
        int total = _sorted + _spilled;
        OnHashTableSolved?.Invoke(_spilled);
        OnHashTableAttemptGraded?.Invoke(RoundPassed(total));
        ClosePanel();
    }
}
