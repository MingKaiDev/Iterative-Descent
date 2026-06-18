/// <summary>
/// Thin subclass of PuzzleFeedbackPopup for the subnet puzzle.
/// No additional logic -- exists solely to give the Inspector component a
/// distinct type so it can be wired to SubnetPuzzleUI.feedbackPopup without
/// requiring a generic reference.
///
/// Scene setup: attach this to the FeedbackPopup child of SubnetPanel.
/// Starts ACTIVE in the Inspector (see PuzzleFeedbackPopup Awake() note).
/// </summary>
public class SubnetFeedbackPopup : PuzzleFeedbackPopup { }
