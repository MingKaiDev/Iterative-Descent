using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scheduling-puzzle-specific feedback pop-up.
///
/// Extends PuzzleFeedbackPopup with HMI amber terminal sprite swapping.
/// Three warning backgrounds map to the three feedback states:
///   Amber  -- wrong attempt 1 or 2 (keep trying)
///   Red    -- wrong attempt 3+ / lockout (reveal correct answer)
///   Ok     -- correct solve
///
/// Inspector: wire warningAmber, warningRed, warningOk sprites and popupCardImage.
/// Call SetWarningSprite() immediately before Show() from SchedulingPuzzleUI.
/// </summary>
public class SchedulingFeedbackPopup : PuzzleFeedbackPopup
{
    public enum WarningState { Amber, Red, Ok }

    [Header("HMI Warning Sprites")]
    [SerializeField] private Sprite warningAmber;
    [SerializeField] private Sprite warningRed;
    [SerializeField] private Sprite warningOk;

    [Tooltip("The Image component on PopupCard. Swapped to match the warning state before Show().")]
    [SerializeField] private Image popupCardImage;

    /// <summary>
    /// Swaps the PopupCard background sprite to match the feedback state.
    /// Call this immediately before Show().
    /// </summary>
    public void SetWarningSprite(WarningState state)
    {
        if (popupCardImage == null) return;

        popupCardImage.sprite = state switch
        {
            WarningState.Ok  => warningOk,
            WarningState.Red => warningRed,
            _                => warningAmber,
        };
        popupCardImage.type             = Image.Type.Sliced;
        popupCardImage.color            = Color.white;
        popupCardImage.preserveAspect   = false;
    }
}
