/// <summary>
/// Scheduling-puzzle-specific feedback pop-up.
///
/// All behaviour lives in <see cref="PuzzleFeedbackPopup"/>.
/// This subclass exists solely so the existing Unity scene component reference
/// (SchedulingPuzzleUI.feedbackPopup field wired to a SchedulingFeedbackPopup
/// component) is preserved without any Inspector rewiring.
/// </summary>
public class SchedulingFeedbackPopup : PuzzleFeedbackPopup { }
