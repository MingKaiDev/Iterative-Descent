using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click builder for the Hash Table Conveyor puzzle's UI prefab.
///
/// Why an Editor script instead of a hand-authored .prefab file: this panel
/// has several dynamically-templated children (the belt's order template,
/// the basket row template) and a handful of sprite/font references that
/// are far safer to wire up in code -- run inside the actual Editor, which
/// validates every reference as it's assigned -- than to hand-type as raw
/// prefab YAML with no way to catch a bad GUID or a missing component.
///
/// Usage: Unity menu -> ARBITEX -> Build Hash Table Puzzle Prefab.
/// Re-running it is safe -- it overwrites the existing prefab asset in
/// place (SaveAsPrefabAsset replaces the file at PrefabPath).
///
/// After building: drag Assets/Prefab/UI/HashTablePuzzlePanel.prefab into
/// the scene's existing Canvas (same place Stack Puzzle Panel / PC Password
/// Panel live -- it deliberately has no Canvas of its own, matching every
/// other puzzle panel in the project), leave it inactive, then give a
/// HashTablePuzzleProp its GameObject reference.
/// </summary>
public static class HashTablePuzzlePanelBuilder
{
    private const string ArtDir = "Assets/Art/HashTablePuzzle";
    private const string PrefabDir = "Assets/Prefab/UI";
    private const string PrefabPath = PrefabDir + "/HashTablePuzzlePanel.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly string[] FoodFiles =
    {
        "Food_Burger.png",   // 0-9
        "Food_Pasta.png",    // 10-19
        "Food_Fries.png",    // 20-29
        "Food_Ramen.png",    // 30-39
        "Food_Porridge.png", // 40-49
        "Food_Pizza.png",    // 50-59
        "Food_Sushi.png",    // 60-69
        "Food_Taco.png",     // 70-79
        "Food_Pancakes.png", // 80-89
        "Food_Stew.png",     // 90-99
    };

    // Palette -- kept close to the game's existing amber/industrial HUD tones.
    private static readonly Color PanelBg   = new Color(0.043f, 0.043f, 0.047f, 0.95f);
    private static readonly Color SteelBg   = new Color(0.14f, 0.145f, 0.155f, 1f);
    private static readonly Color SteelLine = new Color(0.30f, 0.28f, 0.24f, 1f);
    private static readonly Color Amber     = new Color(0.91f, 0.64f, 0.24f, 1f);
    private static readonly Color Ink       = new Color(0.94f, 0.90f, 0.82f, 1f);
    private static readonly Color InkDim    = new Color(0.72f, 0.66f, 0.54f, 1f);
    private static readonly Color Good      = new Color(0.35f, 0.80f, 0.40f, 1f);
    private static readonly Color Bad       = new Color(0.88f, 0.32f, 0.30f, 1f);

    // NOTE ON SCALE: this project's Canvas uses CanvasScaler "Scale With
    // Screen Size" with a reference resolution of 800x600 (see Level 1.unity).
    // That means every unit below is a CANVAS unit, and Unity re-multiplies
    // it by (actualScreenWidth / 800) at runtime -- on a 1920-wide display
    // that's a ~2.4x blow-up. All absolute pixel/size/font constants in this
    // builder are sized for an 800x600 canvas accordingly (roughly the old
    // 1920-era values / 2.4) -- do not casually bump these back up without
    // remembering the scaler will multiply them again on the real screen.
    private const float ROW_HEIGHT = 53f;

    [MenuItem("ARBITEX/Build Hash Table Puzzle Prefab")]
    public static void Build()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            Debug.LogWarning($"[HashTablePuzzlePanelBuilder] Default TMP font not found at {FontPath} -- text will fall back to TMP's built-in default.");

        Sprite[] foodSprites = new Sprite[FoodFiles.Length];
        for (int i = 0; i < FoodFiles.Length; i++)
        {
            string p = $"{ArtDir}/{FoodFiles[i]}";
            foodSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (foodSprites[i] == null)
                Debug.LogWarning($"[HashTablePuzzlePanelBuilder] Missing sprite: {p} -- did you import the generated art into {ArtDir} first?");
        }
        Sprite beltSprite   = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Belt_Tread.png");
        Sprite basketSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Basket_Steel.png");

        // ── Root ─────────────────────────────────────────────────────────
        RectTransform root = CreateRect(null, "HashTablePuzzlePanel");
        StretchFull(root);
        AddImage(root, PanelBg);

        // ── Header ───────────────────────────────────────────────────────
        // Sized for an 800x600 reference canvas (see NOTE ON SCALE above).
        // Clear vertical bands per line so the hint text -- the longest
        // line -- has room to wrap without spilling into the content area.
        RectTransform header = CreateRect(root, "Header Panel");
        AnchorTop(header, 125, 16);

        RectTransform title = CreateRect(header, "Title Text");
        AnchorWithin(title, 0f, 0.64f, 0.70f, 1f);
        var titleTmp = AddTMP(title, "ORDER LINE SORTER", 16, TextAlignmentOptions.BottomLeft, Ink, font);
        titleTmp.fontStyle = FontStyles.Bold;

        RectTransform formula = CreateRect(header, "Formula Text");
        AnchorWithin(formula, 0f, 0.38f, 0.70f, 0.62f);
        AddTMP(formula, "BASKET = ORDER # mod 5", 10, TextAlignmentOptions.BottomLeft, Amber, font);

        RectTransform hint = CreateRect(header, "Rule Hint Text");
        AnchorWithin(hint, 0f, 0.02f, 0.70f, 0.36f);
        var hintTmp = AddTMP(hint, "W / S -- shift baskets so the right one is under the chute before each order arrives.", 9, TextAlignmentOptions.TopLeft, InkDim, font);

        RectTransform closeBtnRect = CreateRect(header, "Close Button");
        AnchorTopRightFixed(closeBtnRect, 24, 24, 4f, 4f);
        Image closeBtnImg = AddImage(closeBtnRect, SteelBg);
        Button closeButton = closeBtnRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeBtnImg;
        RectTransform closeXRect = CreateRect(closeBtnRect, "X");
        StretchFull(closeXRect);
        AddTMP(closeXRect, "X", 12, TextAlignmentOptions.Center, Ink, font);

        // Tier badge sits left of the close button with a generous gap --
        // anchored to a fixed distance from the header's right edge (via the
        // close button's own width + padding) rather than a tight fractional
        // split, so the two never crowd each other regardless of header width.
        // Kept small and tucked into the top of the right column so there's
        // room for the W/S move hint underneath it.
        RectTransform tierBadge = CreateRect(header, "Tier Badge");
        tierBadge.anchorMin = new Vector2(0.72f, 0.74f);
        tierBadge.anchorMax = new Vector2(1f, 1f);
        tierBadge.offsetMin = Vector2.zero;
        tierBadge.offsetMax = new Vector2(-(24f + 13f), 0f); // clear the 24-wide close button + padding
        AddImage(tierBadge, SteelBg);
        RectTransform tierBadgeTextRect = CreateRect(tierBadge, "Tier Badge Text");
        StretchFull(tierBadgeTextRect);
        var tierBadgeTmp = AddTMP(tierBadgeTextRect, "NORMAL", 10, TextAlignmentOptions.Center, Amber, font);
        tierBadgeTmp.fontStyle = FontStyles.Bold;

        // Quick move-key reminder, right under the tier badge -- the full
        // rule explanation lives in the hint text on the left, but this sits
        // right where the player's eye already goes to check the difficulty.
        RectTransform wsHint = CreateRect(header, "WS Hint Text");
        AnchorWithin(wsHint, 0.62f, 0.02f, 1f, 0.68f);
        AddTMP(wsHint, "W / S -- MOVE BASKET", 9, TextAlignmentOptions.TopRight, InkDim, font);

        closeBtnRect.SetAsLastSibling();

        // ── Status row ───────────────────────────────────────────────────
        RectTransform statusRow = CreateRect(root, "Status Row");
        AnchorTop(statusRow, 21, 146);
        RectTransform sortedRect = CreateRect(statusRow, "Sorted Text");
        AnchorWithin(sortedRect, 0f, 0f, 0.25f, 1f);
        var sortedTmp = AddTMP(sortedRect, "SORTED  0", 10, TextAlignmentOptions.MidlineLeft, Good, font);
        RectTransform spilledRect = CreateRect(statusRow, "Spilled Text");
        AnchorWithin(spilledRect, 0.25f, 0f, 0.5f, 1f);
        var spilledTmp = AddTMP(spilledRect, "SPILLED  0", 10, TextAlignmentOptions.MidlineLeft, Bad, font);
        RectTransform timerRect = CreateRect(statusRow, "Timer Text");
        AnchorWithin(timerRect, 0.5f, 0f, 0.75f, 1f);
        var timerTmp = AddTMP(timerRect, "TIME  0.0s", 10, TextAlignmentOptions.MidlineLeft, InkDim, font);

        // ── Content area (belt + baskets) ───────────────────────────────
        RectTransform content = CreateRect(root, "Content Area");
        SetMargins(content, 25, 170, 25, 45);

        // Belt panel (left ~70%)
        RectTransform beltPanel = CreateRect(content, "Belt Panel");
        AnchorWithin(beltPanel, 0f, 0f, 0.70f, 1f);
        RectTransform beltLabel = CreateRect(beltPanel, "Belt Label");
        AnchorWithin(beltLabel, 0f, 0.86f, 1f, 1f);
        AddTMP(beltLabel, "INCOMING ORDERS", 9, TextAlignmentOptions.BottomLeft, InkDim, font);

        RectTransform beltViewport = CreateRect(beltPanel, "Belt Viewport");
        AnchorWithin(beltViewport, 0f, 0.30f, 1f, 0.82f);
        AddImage(beltViewport, SteelBg);
        beltViewport.gameObject.AddComponent<RectMask2D>();

        RectTransform beltVisual = CreateRect(beltViewport, "Belt Visual");
        SetMargins(beltVisual, 0, 0, 0, 0);
        Image beltImg = AddImage(beltVisual, Color.white, beltSprite);
        beltImg.type = Image.Type.Simple;
        beltImg.preserveAspect = false;

        RectTransform beltTrack = CreateRect(beltViewport, "Belt Track");
        StretchFull(beltTrack);

        // IMPORTANT: orderTemplate itself must keep a plain point-anchor
        // (anchorMin == anchorMax) with a well-defined sizeDelta/anchoredPosition
        // -- HashTablePuzzleUI reads orderTemplate.anchoredPosition.y and
        // orderTemplate.rect.width directly to drive the belt-travel math, and
        // RecomputeLayoutSizes() writes orderTemplate.sizeDelta at runtime.
        // The food-icon Image also has to live on this SAME GameObject (not a
        // child) because SpawnBeltOrder() does rect.GetComponent<Image>() on
        // the instantiated clone's root. So the icon can't be independently
        // resized/repositioned within the card the way a child could -- instead
        // give the number label its own small dark backing strip so it stays
        // legible sitting near the bottom of the (otherwise full-card) icon.
        RectTransform orderTemplate = CreateRect(beltTrack, "Order Template");
        orderTemplate.anchorMin = new Vector2(0f, 0.5f);
        orderTemplate.anchorMax = new Vector2(0f, 0.5f);
        orderTemplate.pivot = new Vector2(0f, 0.5f);
        orderTemplate.sizeDelta = new Vector2(50f, 63f);
        orderTemplate.anchoredPosition = Vector2.zero;
        AddImage(orderTemplate, Color.white, foodSprites.Length > 0 ? foodSprites[0] : null);

        RectTransform numberBgRect = CreateRect(orderTemplate, "NumberBg");
        numberBgRect.anchorMin = new Vector2(0f, 0f);
        numberBgRect.anchorMax = new Vector2(1f, 0.30f);
        numberBgRect.offsetMin = Vector2.zero; numberBgRect.offsetMax = Vector2.zero;
        AddImage(numberBgRect, new Color(0f, 0f, 0f, 0.55f));

        RectTransform numberTextRect = CreateRect(orderTemplate, "NumberText");
        numberTextRect.anchorMin = new Vector2(0f, 0f);
        numberTextRect.anchorMax = new Vector2(1f, 0.30f);
        numberTextRect.offsetMin = Vector2.zero; numberTextRect.offsetMax = Vector2.zero;
        var numberTmp = AddTMP(numberTextRect, "42", 13, TextAlignmentOptions.Center, Ink, font);
        numberTmp.fontStyle = FontStyles.Bold;
        orderTemplate.gameObject.SetActive(false);

        RectTransform chuteMarker = CreateRect(beltViewport, "Chute Marker");
        chuteMarker.anchorMin = new Vector2(1f, 0f);
        chuteMarker.anchorMax = new Vector2(1f, 1f);
        chuteMarker.pivot = new Vector2(1f, 0.5f);
        chuteMarker.sizeDelta = new Vector2(7f, 0f);
        chuteMarker.anchoredPosition = new Vector2(-54f, 0f);
        AddImage(chuteMarker, Amber);

        // Basket panel (right ~28%)
        RectTransform basketPanel = CreateRect(content, "Basket Panel");
        AnchorWithin(basketPanel, 0.72f, 0f, 1f, 1f);
        RectTransform basketLabel = CreateRect(basketPanel, "Basket Label");
        AnchorWithin(basketLabel, 0f, 0.94f, 1f, 1f);
        AddTMP(basketLabel, "PICKUP BASKETS", 9, TextAlignmentOptions.BottomLeft, InkDim, font);

        RectTransform basketViewport = CreateRect(basketPanel, "Basket Viewport");
        AnchorWithin(basketViewport, 0f, 0f, 1f, 0.90f);
        AddImage(basketViewport, SteelBg);
        basketViewport.gameObject.AddComponent<RectMask2D>();

        RectTransform bucketWindow = CreateRect(basketViewport, "Bucket Window");
        bucketWindow.anchorMin = new Vector2(0f, 0.5f);
        bucketWindow.anchorMax = new Vector2(1f, 0.5f);
        bucketWindow.pivot = new Vector2(0.5f, 0.5f);
        bucketWindow.sizeDelta = new Vector2(0f, ROW_HEIGHT);
        bucketWindow.anchoredPosition = Vector2.zero;

        // Selector Frame -- a FIXED bracket/cage sitting at the vertical
        // centre of Basket Viewport (a sibling of Bucket Window, drawn on
        // top of it, not a child, so it never scrolls). Whichever basket row
        // scrolls to sit under it IS the basket currently loaded at the
        // chute -- this exists purely to make that visually obvious.
        RectTransform selectorFrame = CreateRect(basketViewport, "Selector Frame");
        selectorFrame.anchorMin = new Vector2(0f, 0.5f);
        selectorFrame.anchorMax = new Vector2(1f, 0.5f);
        selectorFrame.pivot = new Vector2(0.5f, 0.5f);
        selectorFrame.sizeDelta = new Vector2(-3f, ROW_HEIGHT + 10f);
        selectorFrame.anchoredPosition = Vector2.zero;

        void AddCageBar(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, string barName)
        {
            RectTransform bar = CreateRect(selectorFrame, barName);
            bar.anchorMin = anchorMin;
            bar.anchorMax = anchorMax;
            bar.pivot = pivot;
            bar.sizeDelta = sizeDelta;
            bar.anchoredPosition = Vector2.zero;
            Image img = AddImage(bar, Amber);
            img.raycastTarget = false;
        }
        AddCageBar(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 3f), "Bar Top");
        AddCageBar(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), "Bar Bottom");
        AddCageBar(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), "Bar Left");
        AddCageBar(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(3f, 0f), "Bar Right");
        selectorFrame.SetAsLastSibling(); // draw on top of the scrolling rows

        RectTransform bucketRowTemplate = CreateRect(bucketWindow, "Bucket Row Template");
        bucketRowTemplate.anchorMin = new Vector2(0f, 0.5f);
        bucketRowTemplate.anchorMax = new Vector2(1f, 0.5f);
        bucketRowTemplate.pivot = new Vector2(0.5f, 0.5f);
        bucketRowTemplate.sizeDelta = new Vector2(-7f, ROW_HEIGHT - 5f);
        bucketRowTemplate.anchoredPosition = Vector2.zero;
        AddImage(bucketRowTemplate, new Color(0, 0, 0, 0.18f));

        RectTransform basketIconRect = CreateRect(bucketRowTemplate, "Basket Icon");
        basketIconRect.anchorMin = new Vector2(0f, 0.5f);
        basketIconRect.anchorMax = new Vector2(0f, 0.5f);
        basketIconRect.pivot = new Vector2(0f, 0.5f);
        basketIconRect.sizeDelta = new Vector2(ROW_HEIGHT - 10f, ROW_HEIGHT - 10f);
        basketIconRect.anchoredPosition = new Vector2(3f, 0f);
        AddImage(basketIconRect, Color.white, basketSprite);

        RectTransform indexTextRect = CreateRect(bucketRowTemplate, "Index Text");
        indexTextRect.anchorMin = new Vector2(0f, 0.52f);
        indexTextRect.anchorMax = new Vector2(1f, 1f);
        indexTextRect.offsetMin = new Vector2(ROW_HEIGHT + 2f, 0f);
        indexTextRect.offsetMax = new Vector2(-3f, 0f);
        AddTMP(indexTextRect, "BASKET 0", 9, TextAlignmentOptions.BottomLeft, Amber, font);

        RectTransform contentsTextRect = CreateRect(bucketRowTemplate, "Contents Text");
        contentsTextRect.anchorMin = new Vector2(0f, 0f);
        contentsTextRect.anchorMax = new Vector2(1f, 0.52f);
        contentsTextRect.offsetMin = new Vector2(ROW_HEIGHT + 2f, 0f);
        contentsTextRect.offsetMax = new Vector2(-3f, 0f);
        AddTMP(contentsTextRect, "-- empty --", 8, TextAlignmentOptions.TopLeft, InkDim, font);
        bucketRowTemplate.gameObject.SetActive(false);

        // ── Feedback popup (last sibling so it renders on top) ──────────
        RectTransform popupRoot = CreateRect(root, "FeedbackPopup");
        StretchFull(popupRoot);

        RectTransform blocker = CreateRect(popupRoot, "Blocker");
        StretchFull(blocker);
        Image blockerImg = AddImage(blocker, new Color(0f, 0f, 0f, 0.65f));
        blockerImg.raycastTarget = true;

        RectTransform card = CreateRect(popupRoot, "PopupCard");
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(284f, 142f);
        card.anchoredPosition = Vector2.zero;
        AddImage(card, SteelBg);

        RectTransform iconTextRect = CreateRect(card, "IconText");
        AnchorWithin(iconTextRect, 0.1f, 0.62f, 0.9f, 0.92f);
        var iconTmp = AddTMP(iconTextRect, "OK", 19, TextAlignmentOptions.Center, Good, font);
        iconTmp.fontStyle = FontStyles.Bold;

        RectTransform messageTextRect = CreateRect(card, "MessageText");
        AnchorWithin(messageTextRect, 0.08f, 0.28f, 0.92f, 0.62f);
        var messageTmp = AddTMP(messageTextRect, "LINE CLEARED", 10, TextAlignmentOptions.Center, Ink, font);

        RectTransform okBtnRect = CreateRect(card, "OkButton");
        okBtnRect.anchorMin = new Vector2(0.5f, 0f);
        okBtnRect.anchorMax = new Vector2(0.5f, 0f);
        okBtnRect.pivot = new Vector2(0.5f, 0f);
        okBtnRect.sizeDelta = new Vector2(83f, 25f);
        okBtnRect.anchoredPosition = new Vector2(0f, 13f);
        Image okBtnImg = AddImage(okBtnRect, new Color(Amber.r, Amber.g, Amber.b, 1f));
        Button okButton = okBtnRect.gameObject.AddComponent<Button>();
        okButton.targetGraphic = okBtnImg;
        RectTransform okLabelRect = CreateRect(okBtnRect, "Label");
        StretchFull(okLabelRect);
        var okLabelTmp = AddTMP(okLabelRect, "OK", 10, TextAlignmentOptions.Center, new Color(0.08f, 0.06f, 0.03f, 1f), font);
        okLabelTmp.fontStyle = FontStyles.Bold;

        popupRoot.gameObject.SetActive(false);

        // ── Audio ────────────────────────────────────────────────────────
        // Two separate AudioSources on the panel root -- one dedicated,
        // looping source for the continuous belt sound, and one one-shot
        // source for correct/wrong stings, so a sting never interrupts the
        // loop. Matches the project's existing per-script AudioSource
        // convention (see DoorController.cs / PaintingInteractable.cs)
        // rather than routing through GameAudioManager, since this sound is
        // scoped to the puzzle overlay, not scene-level ambience.
        AudioSource beltAudioSource = root.gameObject.AddComponent<AudioSource>();
        beltAudioSource.loop = true;
        beltAudioSource.playOnAwake = false;
        beltAudioSource.spatialBlend = 0f;

        AudioSource sfxAudioSource = root.gameObject.AddComponent<AudioSource>();
        sfxAudioSource.loop = false;
        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.spatialBlend = 0f;

        // ── Components ────────────────────────────────────────────────
        HashTableFeedbackPopup feedbackPopup = popupRoot.gameObject.AddComponent<HashTableFeedbackPopup>();
        SerializedObject popupSo = new SerializedObject(feedbackPopup);
        popupSo.FindProperty("iconText").objectReferenceValue = iconTmp;
        popupSo.FindProperty("messageText").objectReferenceValue = messageTmp;
        popupSo.FindProperty("okButton").objectReferenceValue = okButton;
        popupSo.ApplyModifiedPropertiesWithoutUndo();

        HashTablePuzzleUI ui = root.gameObject.AddComponent<HashTablePuzzleUI>();
        SerializedObject uiSo = new SerializedObject(ui);
        uiSo.FindProperty("tierBadgeText").objectReferenceValue = tierBadgeTmp;
        uiSo.FindProperty("formulaText").objectReferenceValue = formula.GetComponent<TextMeshProUGUI>();
        uiSo.FindProperty("ruleHintText").objectReferenceValue = hintTmp;
        uiSo.FindProperty("sortedText").objectReferenceValue = sortedTmp;
        uiSo.FindProperty("spilledText").objectReferenceValue = spilledTmp;
        uiSo.FindProperty("timerText").objectReferenceValue = timerTmp;
        uiSo.FindProperty("beltTrack").objectReferenceValue = beltTrack;
        uiSo.FindProperty("orderTemplate").objectReferenceValue = orderTemplate;
        uiSo.FindProperty("chuteMarker").objectReferenceValue = chuteMarker;
        uiSo.FindProperty("bucketWindow").objectReferenceValue = bucketWindow;
        uiSo.FindProperty("bucketRowTemplate").objectReferenceValue = bucketRowTemplate;
        uiSo.FindProperty("rowHeight").floatValue = ROW_HEIGHT;
        uiSo.FindProperty("selectorFrame").objectReferenceValue = selectorFrame;
        uiSo.FindProperty("feedbackPopup").objectReferenceValue = feedbackPopup;
        uiSo.FindProperty("closeButton").objectReferenceValue = closeButton;
        uiSo.FindProperty("beltAudioSource").objectReferenceValue = beltAudioSource;
        uiSo.FindProperty("sfxAudioSource").objectReferenceValue = sfxAudioSource;
        // beltLoopClip / correctSound / wrongSound are left unassigned here --
        // drag the actual clips onto the prefab in the Inspector.

        SerializedProperty foodArrayProp = uiSo.FindProperty("foodSprites");
        foodArrayProp.arraySize = foodSprites.Length;
        for (int i = 0; i < foodSprites.Length; i++)
            foodArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = foodSprites[i];

        uiSo.ApplyModifiedPropertiesWithoutUndo();

        // ── Save as prefab asset ─────────────────────────────────────────
        EnsureFolder(PrefabDir);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
        Object.DestroyImmediate(root.gameObject);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[HashTablePuzzlePanelBuilder] Built {PrefabPath} -- drag it into the scene's Canvas, leave it inactive, and assign it to a HashTablePuzzleProp.");
    }

    // ── Layout helpers ───────────────────────────────────────────────────

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void StretchFull(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>Stretch with pixel margins from each edge of the parent.</summary>
    private static void SetMargins(RectTransform r, float left, float top, float right, float bottom)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
        r.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>Pin to the top of the parent with a fixed height and a top offset.</summary>
    private static void AnchorTop(RectTransform r, float height, float topOffset)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-50f, height); // 25px margin each side, matching Content Area's SetMargins
        r.anchoredPosition = new Vector2(0f, -topOffset);
    }

    private static void AnchorTopRightFixed(RectTransform r, float width, float height, float rightOffset, float topOffset)
    {
        r.anchorMin = new Vector2(1f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.sizeDelta = new Vector2(width, height);
        r.anchoredPosition = new Vector2(-rightOffset, -topOffset);
    }

    /// <summary>Anchor as a fractional rect within the parent (0-1 on both axes), no pixel offsets.</summary>
    private static void AnchorWithin(RectTransform r, float xMin, float yMin, float xMax, float yMax)
    {
        r.anchorMin = new Vector2(xMin, yMin);
        r.anchorMax = new Vector2(xMax, yMax);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
    }

    private static Image AddImage(RectTransform r, Color color, Sprite sprite = null)
    {
        Image img = r.gameObject.AddComponent<Image>();
        img.color = color;
        if (sprite != null) img.sprite = sprite;
        return img;
    }

    private static TextMeshProUGUI AddTMP(RectTransform r, string text, float fontSize, TextAlignmentOptions align, Color color, TMP_FontAsset font)
    {
        TextMeshProUGUI tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        if (font != null) tmp.font = font;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
