using UnityEngine;

/// <summary>
/// ScriptableObject holding the four BKT parameters for a single CS concept.
/// Create one asset per concept via: Assets > Create > ARBITEX > Concept Profile.
///
/// Attach all ConceptProfile assets to the conceptProfiles array on
/// PlayerMetricsTracker in the Inspector, then set conceptTag on each
/// QuestionData asset to match the conceptKey here.
/// </summary>
[CreateAssetMenu(menuName = "ARBITEX/Concept Profile", fileName = "ConceptProfile_New")]
public class ConceptProfile : ScriptableObject
{
    [Tooltip("Unique key used in BayesianKnowledgeTracker. " +
             "Must match the conceptTag field on QuestionData assets " +
             "and the constant strings on BayesianKnowledgeTracker.")]
    public string conceptKey;

    [Tooltip("Human-readable name shown in debug HUDs.")]
    public string displayName;

    [Header("BKT Parameters (Corbett-Anderson 1994)")]

    [Range(0f, 1f)]
    [Tooltip("P(L0) -- Prior probability the player already knows this concept " +
             "before any evidence is observed. Typical range: 0.1 to 0.5.")]
    public float priorKnown = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("P(T) -- Probability of learning the concept after each attempt " +
             "(transition from not-knowing to knowing). Typical range: 0.05 to 0.2.")]
    public float learningRate = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("P(S) -- Probability of slipping: player knows the concept but " +
             "answers incorrectly anyway (e.g. misread question). Typical: 0.05 to 0.15.")]
    public float slipRate = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("P(G) -- Probability of guessing: player does not know the concept " +
             "but answers correctly by chance. Typical: 0.2 to 0.3.")]
    public float guessRate = 0.25f;
}
