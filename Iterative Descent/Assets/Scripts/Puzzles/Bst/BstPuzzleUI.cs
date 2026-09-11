using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Master controller for the "Build the Catalog" puzzle -- a Binary Search
/// Tree insertion puzzle. Accessed via a library returns-cart prop.
///
/// WHAT THE PLAYER DOES
/// --------------------
/// A stack of returned books (call-number cards) arrives one at a time from
/// the Returns Cart strip. For each card, the player walks it down the
/// growing catalog tree from the root: the current card (the "cursor") is
/// highlighted amber in the tree, and the player compares the value in hand
/// against it and presses LOWER SHELF or HIGHER SHELF.
///   - If a card already occupies that branch, the cursor steps down into it
///     and the comparison repeats against the new cursor.
///   - If the branch is empty, the card in hand is shelved there -- a new
///     card appears in the tree, wired to its parent with a connecting line.
/// The whole tree (every card, every branch) is visible at all times -- there
/// is no hidden state and no "hotter/colder" feedback to chase. Every choice
/// is a plain "which number is bigger" comparison the player can verify by
/// reading the two cards on screen; a wrong branch just says so and the
/// cursor doesn't move, so nothing is lost by getting one wrong.
/// Goal: every card in the Returns Cart is correctly shelved.
///
/// DDA SCALING
/// -----------
///   Tier 0 / 1  (Very Easy / Easy)   -> 3 cards
///   Tier 2      (Normal)              -> 4 cards
///   Tier 3      (Hard)                -> 5 cards
///   Tier 4      (Very Hard)           -> 6 cards
/// Tier read from PuzzleDDAController.GetTierForConcept(BayesianKnowledgeTracker.TreesBst).
///
/// LAYOUT OWNERSHIP -- READ THIS BEFORE MOVING ANYTHING IN THE PREFAB
/// ------------------------------------------------------------------
/// Rebuilt to follow PacketFilterPuzzleUI's convention instead of computing
/// panel-wide pixel positions in code: every STATIC piece of chrome --
/// Header, InstructionsText, StatusText, the close button, and both branch
/// buttons -- is positioned, sized and scaled entirely by whatever you set
/// on it in "Bst Puzzle Panel.prefab" (or a room-prefab instance override of
/// it). This script never touches their RectTransforms. Move them in the
/// Editor and they stay moved -- that is the whole point of this pass.
///
/// The only things this script positions are DYNAMIC content it spawns at
/// runtime, because there's nothing to author in the Editor for content that
/// doesn't exist until the puzzle is running:
///   - Tree cards + branch lines, inside NodeContainer. RelayoutTree() reads
///     NodeContainer's own RectTransform size directly (nodeContainer.rect,
///     after a forced canvas rebuild) as the box to lay the tree out in --
///     resize or reposition NodeContainer in the Editor and the tree adapts,
///     no code or Inspector-field changes needed.
///   - Cart cards, inside CartContainer. This script only Instantiates them
///     as children and sets their text/colour, exactly like
///     PacketFilterPuzzleUI's packetCardParent. CartContainer needs a
///     Horizontal Layout Group (+ optionally a Content Size Fitter) added to
///     it in the Editor -- spacing, padding and child alignment are then
///     tunable there, not in this script. See SETUP note on cartContainer.
///
/// Root Slot / Lower Shelf / Higher Shelf buttons are never repositioned or
/// rescaled by code any more either -- only SetActive() and sibling order
/// (see RaisePlacementControls). Put them wherever you want them in the
/// prefab; that's now the only place their position lives.
///
/// Two concrete faults this design replaces (both previously discovered by
/// direct prefab/scene audit, see project-bst-catalog-puzzle.md):
///   1. Lower/Higher Shelf used to get MOVED onto the cursor's target slot
///      each step. Spawned cards are appended to nodeContainer after those
///      buttons, so an occupied slot's card drew on top of the button, and
///      the card's Image has raycastTarget on, so it ate the click too.
///      Fix: the buttons never move now, and RaisePlacementControls() keeps
///      them as nodeContainer's last siblings (drawn/hit-tested above every
///      card and branch line) after every insertion.
///   2. Panel-wide pixel/percentage layout code (ApplyPanelRegions, DELETED
///      in this pass) recomputed every region's RectTransform from scratch
///      on every open, silently overwriting anything authored in the
///      Editor -- which is exactly why nothing could be tuned by hand and
///      why positions could differ between opens (a RectTransform that was
///      inactive doesn't get its cached rect refreshed by anything that
///      changed while it was off, so the first read after SetActive(true)
///      could be stale). The only rect this script still reads at runtime
///      is NodeContainer's, and only to size the tree -- never written.
///
/// NODE CARD SCALE
/// ----------------
/// Previously cards were capped at a hard-coded 0.8 scale even when the
/// tree trivially fit (nodeCardScale * fit, fit itself capped at 1) -- a
/// single root card could never render larger than 80% of its authored
/// size no matter how much room NodeContainer had. That separate multiplier
/// is gone: RelayoutTree's fit factor (capped at 1, i.e. never enlarged past
/// the card prefab's own authored size) is now the ONLY scale applied, so a
/// small tree renders cards at their full natural size and only shrinks
/// when the tree genuinely doesn't fit the box.
///
/// NOTES
/// -----
///   - Time.timeScale = 0 while open (set by the prop); ALL timers use
///     Time.realtimeSinceStartup -- see FlashWrong's WaitForSecondsRealtime.
///   - Unlike LinkedListPuzzleUI there is no single precomputed "_solution"
///     array to check at the end: because any unique set of values inserted
///     in a fixed order has exactly one correct BST shape, correctness is
///     validated locally at every branch click (does this choice match
///     cardValue.CompareTo(cursor.Value)), which is both simpler and the
///     more faithful mirror of how BST insertion actually works.
/// </summary>
public class BstPuzzleUI : MonoBehaviour
{
    // ── Static ────────────────────────────────────────────────────────────────
    public static BstPuzzleUI Instance { get; private set; }

    /// <summary>Fired once every card in the cart has been correctly shelved. Argument = total wrong branch clicks.</summary>
    public static event Action<int> OnBstSolved;

    // ── Inspector: Prefabs ────────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private GameObject branchLinePrefab;
    [SerializeField] private GameObject cartCardPrefab;

    // ── Inspector: References ─────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The tree diagram's box. Position/size this directly in the Editor -- " +
             "RelayoutTree() lays the tree out inside whatever rect you draw here, " +
             "nothing else in this script touches it.")]
    [SerializeField] private RectTransform     nodeContainer;
    [Tooltip("SETUP: give this a Horizontal Layout Group (+ optionally a Content Size " +
             "Fitter set to Preferred Size on Horizontal) in the Editor, same as " +
             "PacketFilterPuzzleUI's packetCardParent. This script only instantiates " +
             "cards as children and sets their text/colour -- the layout group owns " +
             "spacing, padding and alignment, all tunable in the Inspector.")]
    [SerializeField] private RectTransform     cartContainer;
    [SerializeField] private BstFeedbackPopup  feedbackPopup;
    [SerializeField] private TextMeshProUGUI   instructionText;
    [SerializeField] private TextMeshProUGUI   statusText;
    [SerializeField] private Button            closeButton;

    [Header("Placement Controls -- position these directly in the prefab")]
    [Tooltip("Shown only while the tree is empty -- placing the first card becomes the root. " +
             "Position/size/scale this in the Editor; script only toggles it active and raises it above spawned cards.")]
    [SerializeField] private Button rootSlotButton;
    [Tooltip("Fixed button under the tree. Shelves/steps into the cursor's LOWER (smaller-value) branch. " +
             "Position/size/scale this in the Editor; never moved by code.")]
    [SerializeField] private Button lowerBranchButton;
    [Tooltip("Fixed button under the tree. Shelves/steps into the cursor's HIGHER (larger-value) branch. " +
             "Position/size/scale this in the Editor; never moved by code.")]
    [SerializeField] private Button higherBranchButton;

    [Header("Layout - Tree")]
    [Tooltip("BstNodeCard prefab's RectTransform sizeDelta. Column and row pitch are derived from " +
             "this, so overlap can't be reintroduced by editing spacing alone.")]
    [SerializeField] private Vector2 nodeCardSize  = new Vector2(220f, 140f);
    [Tooltip("Clear space between two horizontally adjacent cards, before fit scaling.")]
    [SerializeField] private float   columnGap     = 34f;
    [Tooltip("Clear space between two vertically adjacent depth levels, before fit scaling.")]
    [SerializeField] private float   rowGap        = 38f;
    [Tooltip("Floor on the fit-to-container scale, so a very deep tree in a small NodeContainer " +
             "never shrinks to unreadable card text instead of just running past the box a little.")]
    [SerializeField] private float   minFitScale   = 0.18f;

    [Header("Returns Cart")]
    [Tooltip("How many upcoming cards (including the one currently in hand) the cart strip shows at once.")]
    [SerializeField] private int     cartPreviewCount = 5;

    [Header("Generation")]
    [Tooltip("Reject draw orders that build a tree deeper than this. A near-sorted draw makes a " +
             "one-sided chain, which shrinks the whole tree to fit and teaches nothing about " +
             "branching; 3 keeps every tier's card count legible and still varies shape.")]
    [SerializeField] private int maxTreeDepth = 3;

    // ── Colours ───────────────────────────────────────────────────────────────
    [Header("Colours")]
    [SerializeField] private Color wrongFlashColor = new Color(0.85f, 0.25f, 0.2f, 1f);
    [SerializeField] private Color inHandCartColor = Color.white;
    [SerializeField] private Color queuedCartColor  = new Color(0.75f, 0.75f, 0.75f, 1f);

    // ── Audio ─────────────────────────────────────────────────────────────────
    // Same convention as LinkedListPuzzleUI/StackPuzzleUI/HashTablePuzzleUI:
    // a single sfxAudioSource + PlaySfx() helper that no-ops if either the
    // source or the clip is unassigned. "Bst Puzzle Panel" already has a spare
    // AudioSource component on its root -- drag that into sfxAudioSource.
    [Header("Audio")]
    [Tooltip("AudioSource used for the shelf buttons' one-shot click sting.")]
    [SerializeField] private AudioSource sfxAudioSource;
    [Tooltip("Played on every Lower Shelf / Higher Shelf click, correct or wrong.")]
    [SerializeField] private AudioClip   shelfClickSound;
    [Tooltip("Volume for the shelf click sting.")]
    [Range(0f, 1f)]
    [SerializeField] private float       sfxVolume = 0.4f;

    // ── Tree State ─────────────────────────────────────────────────────────────
    private BstNodeCardView       _root;
    private BstNodeCardView       _cursor;
    private BstNodeCardView       _highlighted;
    private readonly List<BstNodeCardView> _allNodes = new();
    private readonly List<Branch>          _branches = new();
    private readonly List<BstNodeCardView> _inOrder  = new();

    /// <summary>A drawn parent-to-child edge. Kept so RelayoutTree can redraw rather than rebuild.</summary>
    private struct Branch
    {
        public BstNodeCardView Parent;
        public BstNodeCardView Child;
        public BstBranchLine   Line;
    }

    // ── Cart State ─────────────────────────────────────────────────────────────
    private List<int> _cart = new();
    private int        _cartIndex;
    private int         _currentCardValue;

    // ── Puzzle State ───────────────────────────────────────────────────────────
    private int    _wrongAttempts;
    private Action _onClose;

    // ── Unity ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        if (rootSlotButton)      rootSlotButton.onClick.AddListener(OnRootSlotClicked);
        if (lowerBranchButton)   lowerBranchButton.onClick.AddListener(OnLowerClicked);
        if (higherBranchButton)  higherBranchButton.onClick.AddListener(OnHigherClicked);
        if (closeButton)         closeButton.onClick.AddListener(ClosePanel);
    }

    void OnDisable()
    {
        if (rootSlotButton)      rootSlotButton.onClick.RemoveListener(OnRootSlotClicked);
        if (lowerBranchButton)   lowerBranchButton.onClick.RemoveListener(OnLowerClicked);
        if (higherBranchButton)  higherBranchButton.onClick.RemoveListener(OnHigherClicked);
        if (closeButton)         closeButton.onClick.RemoveListener(ClosePanel);
    }

    // ── Public Entry Point ────────────────────────────────────────────────────
    /// <summary>Called by the prop to open the puzzle.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose = onClose;
        _wrongAttempts = 0;

        if (feedbackPopup != null) feedbackPopup.gameObject.SetActive(false);

        PlayerMetricsTracker.Instance?.NotifyBstStarted();

        // BstPuzzleProp activates this panel and calls InitPuzzle() in the same
        // line, same frame. NodeContainer is a child of a panel that was just
        // reactivated, so its cached rect can be stale (whatever it was the
        // last time this panel was active) until the canvas system rebuilds
        // it -- forcing that here means RelayoutTree() below reads its real,
        // current size instead of a leftover value from a different open.
        Canvas.ForceUpdateCanvases();

        SetInstructionText();
        GeneratePuzzle();
    }

    // ── Puzzle Generation ─────────────────────────────────────────────────────
    private void GeneratePuzzle()
    {
        ClearAll();

        int count = CardCountForCurrentTier();
        _cart = GenerateCart(count);
        _cartIndex = 0;

        BeginNextCard();
    }

    private void ClearAll()
    {
        foreach (var n in _allNodes) if (n) Destroy(n.gameObject);
        foreach (var b in _branches) if (b.Line) Destroy(b.Line.gameObject);
        _allNodes.Clear();
        _branches.Clear();
        _inOrder.Clear();

        if (cartContainer != null)
            foreach (Transform t in cartContainer) Destroy(t.gameObject);

        _root = null;
        _cursor = null;
        _highlighted = null;
        ShowRootSlot(false);
        ShowBranchButtons(false);
    }

    // ── Card Sequencing ────────────────────────────────────────────────────────
    private void BeginNextCard()
    {
        if (_cartIndex >= _cart.Count)
        {
            OnAllCardsPlaced();
            return;
        }

        _currentCardValue = _cart[_cartIndex];
        _cartIndex++;
        RefreshCartStrip();

        if (_root == null)
        {
            ShowRootSlot(true);
            ShowBranchButtons(false);
            SetStatus($"> NEXT RETURN: No. {_currentCardValue}  |  NO CATALOG YET -- PLACE AS ROOT CARD");
        }
        else
        {
            _cursor = _root;
            ShowRootSlot(false);
            SetCursorHighlight(_cursor);
            ShowBranchButtons(true);
            SetStatus($"> NEXT RETURN: No. {_currentCardValue}  |  COMPARE AGAINST No. {_cursor.Value}");
        }
    }

    private void OnAllCardsPlaced()
    {
        ShowRootSlot(false);
        ShowBranchButtons(false);
        SetCursorHighlight(null);

        OnBstSolved?.Invoke(_wrongAttempts);

        feedbackPopup?.Show(true,
            "> CATALOG COMPLETE\n" +
            "> EVERY RETURN SHELVED IN ITS CORRECT POSITION\n" +
            "> ACCESS GRANTED",
            onDismiss: ClosePanel);
    }

    // ── Placement Input ────────────────────────────────────────────────────────
    private void OnRootSlotClicked()
    {
        if (_root != null) return; // safety -- shouldn't be visible once root exists

        _root = SpawnNode(_currentCardValue, depth: 0);
        RelayoutTree();
        ShowRootSlot(false);
        SetStatus($"> No. {_currentCardValue} SHELVED AS ROOT");
        BeginNextCard();
    }

    private void OnLowerClicked()  => HandleBranchChoice(goLeft: true);
    private void OnHigherClicked() => HandleBranchChoice(goLeft: false);

    /// <summary>
    /// The player claimed the card in hand belongs LOWER (goLeft = true) or
    /// HIGHER (goLeft = false) than the current cursor card.
    /// </summary>
    private void HandleBranchChoice(bool goLeft)
    {
        if (_cursor == null) return;

        PlaySfx(shelfClickSound);

        bool actuallyLower = _currentCardValue < _cursor.Value;
        if (actuallyLower != goLeft)
        {
            RegisterWrongAttempt(goLeft);
            return;
        }

        BstNodeCardView child = goLeft ? _cursor.Left : _cursor.Right;
        if (child == null)
        {
            // Empty slot -- shelve the card in hand right here.
            BstNodeCardView newNode = SpawnNode(_currentCardValue, _cursor.Depth + 1);
            if (goLeft) _cursor.Left = newNode; else _cursor.Right = newNode;
            DrawBranch(_cursor, newNode);
            RelayoutTree();

            SetStatus($"> No. {_currentCardValue} SHELVED  ({(goLeft ? "LOWER" : "HIGHER")} of No. {_cursor.Value})");
            BeginNextCard();
        }
        else
        {
            // Slot already occupied -- step the cursor down and keep comparing.
            _cursor = child;
            SetCursorHighlight(_cursor);
            SetStatus($"> COMPARE No. {_currentCardValue} AGAINST No. {_cursor.Value}");
        }
    }

    private void RegisterWrongAttempt(bool clickedLower)
    {
        _wrongAttempts++;
        Button btn = clickedLower ? lowerBranchButton : higherBranchButton;
        if (btn != null) StartCoroutine(FlashWrong(btn));

        SetStatus($"> MISFILED -- No. {_currentCardValue} IS {(clickedLower ? "NOT LOWER" : "NOT HIGHER")} " +
                  $"THAN No. {_cursor.Value}. CHECK THE NUMBERS AND TRY AGAIN.");
    }

    private IEnumerator FlashWrong(Button btn)
    {
        var img = btn.GetComponent<Image>();
        if (img == null) yield break;

        Color original = img.color;
        img.color = wrongFlashColor;
        // Realtime -- Time.timeScale is 0 while any puzzle panel is open.
        yield return new WaitForSecondsRealtime(0.25f);
        if (img != null) img.color = original;
    }

    /// <summary>Defensive one-shot helper -- no-ops if either the AudioSource or clip is unassigned.</summary>
    private void PlaySfx(AudioClip clip)
    {
        if (sfxAudioSource != null && clip != null) sfxAudioSource.PlayOneShot(clip, sfxVolume);
    }

    // ── Tree Layout ────────────────────────────────────────────────────────────
    /// <summary>
    /// Lays the ENTIRE tree out from scratch, then uniformly scales it to fit
    /// inside NodeContainer's own authored size. Called after every insertion.
    ///
    /// NodeContainer's RectTransform is read directly (position, size) rather
    /// than computed from panel-wide bands -- resize or reposition
    /// NodeContainer in the Editor and this just adapts, no code changes.
    ///
    ///   * Column index comes from an IN-ORDER traversal, which is exactly the
    ///     sorted order of a BST -- so no two cards ever share a column, and
    ///     the drawing reads left-to-right as ascending call numbers, which is
    ///     the property the puzzle is teaching.
    ///   * Column pitch is the card's own rendered width plus columnGap, so
    ///     the gap between neighbours is a constant the caller sets, not an
    ///     emergent property of depth.
    ///   * The fit pass scales cards AND pitch by the same factor, capped at 1
    ///     so a small tree renders at the card prefab's full authored size
    ///     instead of an artificial fixed fraction of it, and only shrinks
    ///     below that when the tree genuinely doesn't fit the box. Deep trees
    ///     get smaller, never overlapping.
    /// </summary>
    private void RelayoutTree()
    {
        if (_root == null || nodeContainer == null) return;

        _inOrder.Clear();
        CollectInOrder(_root, _inOrder);

        int columns  = _inOrder.Count;
        int maxDepth = 0;
        foreach (var n in _allNodes) if (n) maxDepth = Mathf.Max(maxDepth, n.Depth);

        Vector2 treeAreaSize = nodeContainer.rect.size;

        float cardW = nodeCardSize.x;
        float cardH = nodeCardSize.y;

        float colStep = cardW + columnGap;
        float rowStep = cardH + rowGap;

        // Footprint measured edge-to-edge, so the fit test accounts for the
        // outermost cards' own half-widths rather than just their centres.
        float needW = (columns - 1) * colStep + cardW;
        float needH = maxDepth * rowStep + cardH;

        // Capped at 1 -- cards render at their authored size when the tree
        // trivially fits, and only shrink when it genuinely doesn't.
        float fit = Mathf.Min(1f,
                              treeAreaSize.x / Mathf.Max(needW, 1f),
                              treeAreaSize.y / Mathf.Max(needH, 1f));
        fit = Mathf.Max(fit, minFitScale);

        colStep *= fit;
        rowStep *= fit;

        float topY      = maxDepth * rowStep * 0.5f;          // centres the tree vertically in the area
        float centreCol = (columns - 1) * 0.5f;

        for (int i = 0; i < columns; i++)
        {
            var node = _inOrder[i];
            if (!node) continue;
            node.Rect.localScale       = Vector3.one * fit;
            node.Rect.anchoredPosition = new Vector2((i - centreCol) * colStep,
                                                     topY - node.Depth * rowStep);
        }

        foreach (var b in _branches)
        {
            if (!b.Line || !b.Parent || !b.Child) continue;
            b.Line.SetEndpoints(b.Parent.Rect.anchoredPosition,
                                b.Child.Rect.anchoredPosition,
                                thickness: Mathf.Max(2f, 20f * fit));
        }

        // Cards are appended to nodeContainer as they spawn, so without this the
        // buttons (authored as its first children) end up underneath every card.
        RaisePlacementControls();
    }

    private static void CollectInOrder(BstNodeCardView node, List<BstNodeCardView> into)
    {
        if (node == null) return;
        CollectInOrder(node.Left, into);
        into.Add(node);
        CollectInOrder(node.Right, into);
    }

    private BstNodeCardView SpawnNode(int value, int depth)
    {
        var go = Instantiate(nodePrefab, nodeContainer);
        var node = go.GetComponent<BstNodeCardView>();
        node.Init(value, depth, Vector2.zero);   // real position assigned by RelayoutTree
        _allNodes.Add(node);
        return node;
    }

    private void DrawBranch(BstNodeCardView parent, BstNodeCardView child)
    {
        var go = Instantiate(branchLinePrefab, nodeContainer);
        go.transform.SetAsFirstSibling(); // render behind cards, mirrors LLArrow's arrows-behind-nodes convention
        var line = go.GetComponent<BstBranchLine>();
        _branches.Add(new Branch { Parent = parent, Child = child, Line = line });
    }

    private void SetCursorHighlight(BstNodeCardView node)
    {
        if (_highlighted != null) _highlighted.SetCursor(false);
        _highlighted = node;
        if (_highlighted != null) _highlighted.SetCursor(true);
    }

    // ── Branch Button Placement ───────────────────────────────────────────────
    /// <summary>
    /// The two shelf buttons are wherever you put them in the prefab and are
    /// never moved by this script -- see the LAYOUT OWNERSHIP note on the
    /// class. Only their active state and sibling order are code-driven:
    /// they must always be the last children of nodeContainer so they draw
    /// (and hit-test) above every spawned card and branch line -- see
    /// RaisePlacementControls.
    /// </summary>
    private void ShowRootSlot(bool show)
    {
        if (rootSlotButton == null) return;
        rootSlotButton.gameObject.SetActive(show);
        if (show) RaisePlacementControls();
    }

    private void ShowBranchButtons(bool show)
    {
        if (lowerBranchButton)  lowerBranchButton.gameObject.SetActive(show);
        if (higherBranchButton) higherBranchButton.gameObject.SetActive(show);
        if (show) RaisePlacementControls();
    }

    /// <summary>Keeps the clickable controls above every spawned card and branch line.</summary>
    private void RaisePlacementControls()
    {
        if (rootSlotButton)     rootSlotButton.transform.SetAsLastSibling();
        if (lowerBranchButton)  lowerBranchButton.transform.SetAsLastSibling();
        if (higherBranchButton) higherBranchButton.transform.SetAsLastSibling();
    }

    // ── Returns Cart Strip ────────────────────────────────────────────────────
    /// <summary>
    /// Rebuilds the cart strip's cards. Positioning is NOT done here any more
    /// -- CartContainer needs a Horizontal Layout Group (+ optionally a
    /// Content Size Fitter) added to it in the Editor, exactly like
    /// PacketFilterPuzzleUI's packetCardParent. This mirrors that: instantiate
    /// the row's children, set their content, and let the layout group place
    /// them. Spacing/padding/alignment all live on that component in the
    /// Inspector now, not in this method.
    /// </summary>
    private void RefreshCartStrip()
    {
        if (cartContainer == null || cartCardPrefab == null) return;

        foreach (Transform t in cartContainer) Destroy(t.gameObject);

        // Slot 0 = the card currently in hand (index _cartIndex - 1); the rest
        // are upcoming, still-queued cards.
        int first = Mathf.Max(0, _cartIndex - 1);
        int count = Mathf.Clamp(_cart.Count - first, 0, Mathf.Max(1, cartPreviewCount));

        for (int shown = 0; shown < count; shown++)
        {
            var go = Instantiate(cartCardPrefab, cartContainer);

            var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = "No. " + _cart[first + shown];

            var img = go.GetComponent<Image>();
            if (img) img.color = shown == 0 ? inHandCartColor : queuedCartColor;
        }
    }

    // ── UI Text Helpers ───────────────────────────────────────────────────────
    private void SetInstructionText()
    {
        if (!instructionText) return;
        instructionText.text = "> TASK: SHELVE EVERY RETURNED BOOK  |  LOWER SHELF / HIGHER SHELF  |  COMPARE CALL NUMBERS AT EACH CARD";
    }

    private void SetStatus(string s)
    {
        if (statusText) statusText.text = s;
    }

    // ── Close ─────────────────────────────────────────────────────────────────
    public void ClosePanel()
    {
        _onClose?.Invoke();
    }

    // ── DDA Tier Scaling ───────────────────────────────────────────────────────
    private int CurrentTier()
    {
        return PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.TreesBst)
            : 2;
    }

    private int CardCountForCurrentTier()
    {
        return CurrentTier() switch
        {
            0 => 3,
            1 => 3,
            2 => 4,
            3 => 5,
            4 => 6,
            _ => 4
        };
    }

    // ── Value Generation ───────────────────────────────────────────────────────
    /// <summary>
    /// Draws the cart, rejecting orders whose BST would be deeper than
    /// maxTreeDepth.
    ///
    /// The values are unique and randomly ordered, so a near-sorted draw is
    /// perfectly possible -- and a sorted draw builds a one-sided chain, which
    /// is the worst case for this puzzle twice over: RelayoutTree has to shrink
    /// the whole tree hard to fit a deep stack into NodeContainer (down to
    /// unreadable card text), and a chain is also the least instructive shape,
    /// since every single comparison goes the same way and the player never
    /// has to actually branch. Rejecting them is cheaper and far less fragile
    /// than trying to render them.
    ///
    /// Bounded retries, then the last draw is accepted regardless, so this can
    /// never hang even if maxTreeDepth is set too low for the card count.
    /// </summary>
    private List<int> GenerateCart(int count)
    {
        // count - 2 excludes the chain and near-chain for small carts -- a 4-card
        // chain is depth 3, and across all 870 possible draw orders that is the
        // worst-fitting shape there is. The lower bound keeps the cap achievable
        // so the retry loop isn't hunting for a perfect tree (which would make
        // every easy-tier puzzle the same shape); maxTreeDepth is the ceiling.
        // Resulting caps: 3 and 4 cards -> depth 2, 5 and 6 cards -> depth 3.
        int floorDepth = Mathf.CeilToInt(Mathf.Log(count + 1, 2f)) - 1;  // best case for this many cards
        int cap        = Mathf.Clamp(count - 2,
                                     Mathf.Max(2, floorDepth),
                                     Mathf.Max(2, maxTreeDepth));

        List<int> draw = null;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            draw = GenerateUniqueCallNumbers(count).ToList();
            if (BstDepthOf(draw) <= cap) return draw;
        }
        return draw;
    }

    /// <summary>Depth of the BST built by inserting <paramref name="values"/> in order (root = 0).</summary>
    private static int BstDepthOf(List<int> values)
    {
        var nodes = new List<(int value, int left, int right)>();
        int maxDepth = 0;

        foreach (int v in values)
        {
            if (nodes.Count == 0) { nodes.Add((v, -1, -1)); continue; }

            int cur = 0, depth = 0;
            while (true)
            {
                depth++;
                var n = nodes[cur];
                if (v < n.value)
                {
                    if (n.left < 0) { nodes.Add((v, -1, -1)); nodes[cur] = (n.value, nodes.Count - 1, n.right); break; }
                    cur = n.left;
                }
                else
                {
                    if (n.right < 0) { nodes.Add((v, -1, -1)); nodes[cur] = (n.value, n.left, nodes.Count - 1); break; }
                    cur = n.right;
                }
            }
            maxDepth = Mathf.Max(maxDepth, depth);
        }
        return maxDepth;
    }

    /// <summary>Generates `count` unique call numbers (100-499 range reads like a Dewey-style catalog entry).</summary>
    private static int[] GenerateUniqueCallNumbers(int count)
    {
        var pool = Enumerable.Range(100, 400).ToList();
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            result[i] = pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }
}
