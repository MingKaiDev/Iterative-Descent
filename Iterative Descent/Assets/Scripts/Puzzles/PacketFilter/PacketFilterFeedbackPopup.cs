/// <summary>
/// Thin subclass of PuzzleFeedbackPopup for the Packet Filter puzzle.
/// Exists only to preserve Inspector component references in the Unity scene.
///
/// Do NOT override Awake() to call SetActive(false) -- see PuzzleFeedbackPopup
/// header for why this causes a silent first-show failure.
/// </summary>
public class PacketFilterFeedbackPopup : PuzzleFeedbackPopup { }
