using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain C# implementation of the Corbett-Anderson Bayesian Knowledge Tracing model.
/// Maintains a P(knows) belief per CS concept and updates it after every puzzle attempt.
///
/// Instantiate via: new BayesianKnowledgeTracker(conceptProfiles)
/// Held as a field on PlayerMetricsTracker (DontDestroyOnLoad), so state persists
/// across scene transitions automatically.
///
/// Concept key constants are defined here so callers never hardcode strings.
/// All 50 concepts from the BKT concept map are listed below, grouped by category.
/// Use these as the conceptTag value on QuestionData assets and as the
/// conceptKey value on ConceptProfile assets.
/// </summary>
public class BayesianKnowledgeTracker
{
    // ── Concept Key Constants ─────────────────────────────────────────────────
    // Grouped to match the concept map. One constant per leaf concept node.
    // There are no category-level constants -- categories are not tracked,
    // only their individual concepts are.

    // Category 1 -- Foundations (16)
    public const string ArraysAndLists      = "arrays_and_lists";
    public const string LinkedLists         = "linked_lists";
    public const string StacksAndQueues     = "stacks_and_queues";
    public const string HashTables          = "hash_tables";
    public const string TreesBst            = "trees_bst";
    public const string Heaps               = "heaps";
    public const string Graphs              = "graphs";
    public const string BfsDfs              = "bfs_dfs";
    public const string Dijkstra            = "dijkstra";
    public const string DynamicProgramming  = "dynamic_programming";
    public const string GreedyAlgorithms    = "greedy_algorithms";
    public const string BasicSorts          = "basic_sorts";
    public const string MergeSort           = "merge_sort";
    public const string QuickSort           = "quick_sort";
    public const string HeapSort            = "heap_sort";
    public const string ComplexityBigO      = "complexity_big_o";

    // Category 2 -- Systems and Architecture (9)
    // Note: these are all subtopics of operating systems / computer architecture.
    // There is no generic "operating_systems" key -- use the specific concept below.
    public const string CpuMemoryBasics     = "cpu_memory_basics";
    public const string CacheHierarchy      = "cache_hierarchy";
    public const string OsiModel            = "osi_model";
    public const string TcpIp               = "tcp_ip";
    public const string ProcessesThreads    = "processes_threads";
    public const string MemoryManagement    = "memory_management";
    public const string FileSystems         = "file_systems";
    public const string CpuScheduling       = "cpu_scheduling";
    public const string Concurrency         = "concurrency";

    // Category 3 -- SWE Advanced (9)
    public const string OopBasics              = "oop_basics";
    public const string InheritancePolymorphism = "inheritance_polymorphism";
    public const string DesignPatterns         = "design_patterns";
    public const string SqlRelational          = "sql_relational";
    public const string Normalisation          = "normalisation";
    public const string NosqlTransactions      = "nosql_transactions";
    public const string AgileSdlc             = "agile_sdlc";
    public const string WebDevBasics           = "web_dev_basics";
    public const string TestingQa              = "testing_qa";

    // Category 4 -- Cybersecurity (6)
    public const string SymmetricEncryption    = "symmetric_encryption";
    public const string AsymmetricEncryption   = "asymmetric_encryption";
    public const string Hashing                = "hashing";
    public const string PkiCertificates        = "pki_certificates";
    public const string OwaspWebVulns          = "owasp_web_vulns";
    public const string SecurityFrameworks     = "security_frameworks";

    // Category 5 -- Artificial Intelligence (6)
    public const string MlFundamentals         = "ml_fundamentals";
    public const string SupervisedLearning     = "supervised_learning";
    public const string NeuralNetworks         = "neural_networks";
    public const string UnsupervisedLearning   = "unsupervised_learning";
    public const string ReinforcementLearning  = "reinforcement_learning";
    public const string SearchPlanning         = "search_planning";

    // Category 6 -- Misc (4)
    public const string CsHistory              = "cs_history";
    public const string BooleanLogic           = "boolean_logic";
    public const string VersionControl         = "version_control";
    public const string CliDebugging           = "cli_debugging";

    // Category 7 -- Networking (added Story 15)
    public const string NetworkingPorts        = "networking_ports";
    public const string ComputerNetworks       = "computer_networks";

    // ── Default BKT Parameters ────────────────────────────────────────────────
    // Applied when a concept is encountered that has no ConceptProfile asset.

    private const float DefaultPrior      = 0.3f;
    private const float DefaultLearnRate  = 0.1f;
    private const float DefaultSlip       = 0.1f;
    private const float DefaultGuess      = 0.25f;

    // ── Internal State ────────────────────────────────────────────────────────

    private class ConceptState
    {
        public float PKnows;    // Current P(knows) — updated each attempt
        public readonly float PT;   // Learning rate P(T)
        public readonly float PS;   // Slip rate P(S)
        public readonly float PG;   // Guess rate P(G)

        public ConceptState(float pKnows, float pt, float ps, float pg)
        {
            PKnows = Mathf.Clamp01(pKnows);
            PT     = Mathf.Clamp01(pt);
            PS     = Mathf.Clamp01(ps);
            PG     = Mathf.Clamp01(pg);
        }
    }

    private readonly Dictionary<string, ConceptState>  _states   = new();
    private readonly Dictionary<string, ConceptProfile> _profiles = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="profiles">
    /// ConceptProfile assets from the Inspector. May be null or empty;
    /// unknown concepts will use default parameters and bootstrap from the
    /// global average P(knows) when first encountered.
    /// </param>
    public BayesianKnowledgeTracker(ConceptProfile[] profiles)
    {
        if (profiles == null) return;

        foreach (ConceptProfile p in profiles)
        {
            if (p == null || string.IsNullOrEmpty(p.conceptKey)) continue;
            _profiles[p.conceptKey] = p;
            _states[p.conceptKey]   = new ConceptState(
                p.priorKnown, p.learningRate, p.slipRate, p.guessRate);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Update P(knows) for <paramref name="concept"/> after one observed attempt.
    /// Safe to call for concepts not listed in any ConceptProfile; they will be
    /// created on demand using default parameters and bootstrapped from the
    /// current global average P(knows).
    /// </summary>
    public void UpdateAfterAttempt(string concept, bool correct)
    {
        if (string.IsNullOrEmpty(concept)) return;

        ConceptState state = GetOrCreateState(concept);

        float pKnows = state.PKnows;
        float ps     = state.PS;
        float pg     = state.PG;
        float pt     = state.PT;

        // Step 1 — Bayesian posterior update
        float posterior;
        if (correct)
        {
            // P(knows | correct) = P(knows)*(1-slip) / [P(knows)*(1-slip) + P(notKnows)*guess]
            float num   = pKnows * (1f - ps);
            float denom = num + (1f - pKnows) * pg;
            posterior   = denom > 0f ? num / denom : pKnows;
        }
        else
        {
            // P(knows | incorrect) = P(knows)*slip / [P(knows)*slip + P(notKnows)*(1-guess)]
            float num   = pKnows * ps;
            float denom = num + (1f - pKnows) * (1f - pg);
            posterior   = denom > 0f ? num / denom : pKnows;
        }

        // Step 2 — Learning transition: even after an incorrect attempt the
        // player may have learned from the exposure.
        state.PKnows = Mathf.Clamp01(posterior + (1f - posterior) * pt);

        Debug.Log($"[BKT] {concept} | Attempt: {(correct ? "correct" : "wrong")} | " +
                  $"P(knows): {pKnows:F3} -> {state.PKnows:F3} | " +
                  $"Tier: {GetTierForConcept(concept)}");
    }

    /// <summary>Returns current P(knows) for <paramref name="concept"/>, 0 if unseen.</summary>
    public float GetKnowledgeState(string concept)
    {
        return _states.TryGetValue(concept, out ConceptState s) ? s.PKnows : 0f;
    }

    /// <summary>
    /// Maps the concept's P(knows) to a difficulty tier 0-4.
    /// Higher P(knows) means the player understands it well, so difficulty goes up.
    ///   0 = Very Easy  (P(knows) < 0.20)
    ///   1 = Easy       (0.20 - 0.39)
    ///   2 = Normal     (0.40 - 0.59)
    ///   3 = Hard       (0.60 - 0.79)
    ///   4 = Very Hard  (>= 0.80)
    /// Returns 2 (Normal) if the concept has never been observed.
    /// </summary>
    public int GetTierForConcept(string concept)
    {
        if (!_states.ContainsKey(concept)) return 2;
        return PKnowsToTier(GetKnowledgeState(concept));
    }

    /// <summary>
    /// Average P(knows) across all tracked concepts, mapped to a tier.
    /// Used as the global difficulty fallback and as the bootstrap value for
    /// new concepts introduced in later sprints (e.g. cybersecurity).
    /// Returns 2 (Normal) if no concepts have been observed yet.
    /// </summary>
    public int GetGlobalTier()
    {
        return PKnowsToTier(GetGlobalPKnows());
    }

    /// <summary>
    /// Raw average P(knows) across all tracked concepts.
    /// Returns 0.5 if no concepts have been observed.
    /// </summary>
    public float GetGlobalPKnows()
    {
        if (_states.Count == 0) return 0.5f;
        float sum = 0f;
        foreach (ConceptState s in _states.Values) sum += s.PKnows;
        return sum / _states.Count;
    }

    /// <summary>True if at least one concept has been updated via UpdateAfterAttempt.</summary>
    public bool HasAnyData() => _states.Count > 0;

    /// <summary>True if the specific concept has been encountered at least once.</summary>
    public bool HasConcept(string concept) => _states.ContainsKey(concept);

    /// <summary>
    /// Snapshot of all current P(knows) values keyed by concept string.
    /// Use for debug display or serialisation.
    /// </summary>
    public IReadOnlyDictionary<string, float> GetAllKnowledgeStates()
    {
        var result = new Dictionary<string, float>(_states.Count);
        foreach (KeyValuePair<string, ConceptState> kvp in _states)
            result[kvp.Key] = kvp.Value.PKnows;
        return result;
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private ConceptState GetOrCreateState(string concept)
    {
        if (_states.TryGetValue(concept, out ConceptState existing)) return existing;

        // Bootstrap P(knows) from global average so a new concept starts at the
        // player's current overall level, not at a fixed cold prior.
        float priorKnows = _states.Count > 0 ? GetGlobalPKnows() : DefaultPrior;
        float pt         = DefaultLearnRate;
        float ps         = DefaultSlip;
        float pg         = DefaultGuess;

        if (_profiles.TryGetValue(concept, out ConceptProfile profile))
        {
            // Use the profile's tuning params but keep bootstrapped prior
            // (unless this is the very first concept — then trust the profile prior).
            pt = profile.learningRate;
            ps = profile.slipRate;
            pg = profile.guessRate;
            if (_states.Count == 0) priorKnows = profile.priorKnown;
        }

        ConceptState state = new ConceptState(priorKnows, pt, ps, pg);
        _states[concept] = state;

        Debug.Log($"[BKT] New concept '{concept}' initialised | " +
                  $"P(knows)={priorKnows:F3} | PT={pt} PS={ps} PG={pg}");

        return state;
    }

    private static int PKnowsToTier(float pKnows)
    {
        if (pKnows < 0.20f) return 0;
        if (pKnows < 0.40f) return 1;
        if (pKnows < 0.60f) return 2;
        if (pKnows < 0.80f) return 3;
        return 4;
    }
}
