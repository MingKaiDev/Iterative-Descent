/// <summary>
/// Stack-puzzle-specific feedback pop-up.
///
/// All behaviour lives in <see cref="PuzzleFeedbackPopup"/>.
/// This subclass exists solely so the existing Unity scene component reference
/// (StackPuzzleUI.feedbackPopup field wired to a StackFeedbackPopup component)
/// is preserved without any Inspector rewiring.
/// </summary>
public class StackFeedbackPopup : PuzzleFeedbackPopup { }
