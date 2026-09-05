using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Subnet allocation puzzle: four servers each require a specific number of hosts.
/// The player selects the correct CIDR prefix for each server using left/right buttons.
/// The UI is styled after Cisco Packet Tracer -- dark navy panel, monospaced terminal
/// text, cyan/green accents.
///
/// DDA SCALING (via PuzzleDDAController.GetTierForConcept(ComputerNetworks)):
///   Tier 0/1: reference table visible ("2^n - 2 hosts per /prefix")
///   Tier 2+:  reference table hidden; player must recall the formula
///
/// SERVERS (fixed set, shuffled order each init):
///   Operations HQ    -- 100 hosts  -> /25 (126 usable)
///   Lab Network      --  28 hosts  -> /27 (30 usable)
///   Management VLAN  --  12 hosts  -> /28 (14 usable)
///   Server Backbone  --  60 hosts  -> /26 (62 usable)
///
/// On correct solve: fires OnSubnetSolved(int wrongSubmissions), updates BKT.
///
/// ── HIERARCHY ─────────────────────────────────────────────────────────────
///
///   SubnetPanel                (Canvas child; inactive by default)
///   ├── Background             Image, dark navy, stretch-fill
///   │   ├── HeaderRow          HorizontalLayoutGroup
///   │   │   ├── TitleText      TMP "ARBITEX NETWORK CONSOLE v2.1"
///   │   │   └── StatusText     TMP "[LOCKED]" / "[ACCESS GRANTED]"
///   │   ├── SubText            TMP "Assign the correct subnet prefix to restore each server link."
///   │   ├── ReferenceTable     VerticalLayoutGroup -- hidden at tier 2+
///   │   │   └── RefText        TMP (reference table content)
///   │   ├── ServerTable        VerticalLayoutGroup, spacing 6
///   │   │   ├── TableHeader    TMP row labels
///   │   │   ├── ServerRow_0 .. ServerRow_3   (SubnetServerRow components)
///   │   ├── SubmitButton       Button + TMP "APPLY CONFIG"
///   │   └── CloseButton        Button + TMP "EXIT"
///   └── FeedbackPopup          SubnetFeedbackPopup (starts ACTIVE in Inspector)
///
/// ── INSPECTOR WIRING ──────────────────────────────────────────────────────
///   serverRows[0..3]   -- drag ServerRow_0..3 here
///   statusText         -- StatusText TMP
///   referenceTable     -- ReferenceTable GameObject
///   submitButton       -- SubmitButton
///   closeButton        -- CloseButton
///   feedbackPopup      -- FeedbackPopup
/// </summary>
public class SubnetPuzzleUI : MonoBehaviour
{
    // ── Static Event ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when all four prefixes are assigned correctly.
    /// Argument: number of wrong submissions before the correct solve.
    /// </summary>
    public static event Action<int> OnSubnetSolved;

    /// <summary>Singleton set in Awake() -- lets SubnetServerRow play arrow SFX without a direct reference.</summary>
    public static SubnetPuzzleUI Instance { get; private set; }

    // ── Server Data ───────────────────────────────────────────────────────────

    private struct ServerDef
    {
        public string label;        // display name
        public int    hostsNeeded;  // requirement shown to player
        public int    correctPrefix; // /N answer
    }

    private static readonly ServerDef[] AllServers =
    {
        new ServerDef { label = "Operations HQ",   hostsNeeded = 100, correctPrefix = 25 },
        new ServerDef { label = "Server Backbone",  hostsNeeded =  60, correctPrefix = 26 },
        new ServerDef { label = "Lab Network",      hostsNeeded =  28, correctPrefix = 27 },
        new ServerDef { label = "Management VLAN",  hostsNeeded =  12, correctPrefix = 28 },
    };

    // CIDR options the cycling selector steps through (common /24 subnets)
    private static readonly int[] CidrOptions = { 24, 25, 26, 27, 28, 29, 30 };

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Server rows (4) -- drag ServerRow_0..3 in order")]
    [SerializeField] private SubnetServerRow[] serverRows;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject      referenceTable;

    [Header("Buttons")]
    [SerializeField] private Button submitButton;
    [SerializeField] private Button closeButton;

    [Header("Feedback")]
    [SerializeField] private SubnetFeedbackPopup feedbackPopup;

    [Header("Audio")]
    [Tooltip("One-shot source for left/right arrow clicks.")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Tooltip("Looping source for the continuous console hum while the panel is open.")]
    [SerializeField] private AudioSource ambientAudioSource;

    [Tooltip("Continuous ambient loop played for as long as the panel is open.")]
    [SerializeField] private AudioClip ambientLoopClip;

    [Tooltip("Played when a server row's LEFT (<) button is pressed.")]
    [SerializeField] private AudioClip leftArrowSound;

    [Tooltip("Played when a server row's RIGHT (>) button is pressed.")]
    [SerializeField] private AudioClip rightArrowSound;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float ambientVolume = 0.25f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private Action _onClose;
    private int    _wrongSubmissions;
    private bool   _solved;

    // Shuffled server order for this session
    private readonly int[] _order = new int[4];

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (submitButton != null) submitButton.onClick.AddListener(OnSubmit);
        if (closeButton  != null) closeButton.onClick.AddListener(OnClosePressed);
    }

    private void OnDisable()
    {
        if (submitButton != null) submitButton.onClick.RemoveListener(OnSubmit);
        if (closeButton  != null) closeButton.onClick.RemoveListener(OnClosePressed);
        StopAmbientLoop();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called from SubnetPuzzleProp.OpenPuzzle(). Resets and populates the UI.
    /// </summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose          = onClose;
        _wrongSubmissions = 0;
        _solved           = false;

        if (feedbackPopup != null)
            feedbackPopup.gameObject.SetActive(false);

        if (statusText != null)
            statusText.text = "[LOCKED]";

        // Shuffle server display order
        for (int i = 0; i < 4; i++) _order[i] = i;
        for (int i = 3; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }

        // Hide reference table at tier 2+
        bool showRef = CurrentTier() <= 1;
        if (referenceTable != null)
            referenceTable.SetActive(showRef);

        // Populate rows
        for (int i = 0; i < serverRows.Length; i++)
        {
            var def = AllServers[_order[i]];
            serverRows[i].Setup(def.label, def.hostsNeeded, CidrOptions);
        }

        PlayerMetricsTracker.Instance?.NotifySubnetStarted();
        StartAmbientLoop();
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    private void OnSubmit()
    {
        if (_solved) return;

        int wrongCount = 0;

        for (int i = 0; i < serverRows.Length; i++)
        {
            int selectedPrefix  = serverRows[i].SelectedPrefix;
            int correctPrefix   = AllServers[_order[i]].correctPrefix;
            bool correct        = selectedPrefix == correctPrefix;

            serverRows[i].SetHighlight(correct);
            if (!correct) wrongCount++;
        }

        if (wrongCount == 0)
        {
            _solved = true;

            if (statusText != null)
                statusText.text = "[ACCESS GRANTED]";

            feedbackPopup?.Show(
                true,
                "Subnet allocation verified.\nServer room access restored.",
                onDismiss: OnSolvedDismissed);
        }
        else
        {
            _wrongSubmissions++;

            string hint = "";
            if (_wrongSubmissions >= 3)
                hint = "\nHint: usable hosts = 2^(32 - prefix) - 2";

            feedbackPopup?.Show(
                false,
                $"{wrongCount} incorrect assignment(s).\n" +
                "Red rows need correcting." + hint);
        }
    }

    // ── Close / Solved ────────────────────────────────────────────────────────

    private void OnSolvedDismissed()
    {
        OnSubnetSolved?.Invoke(_wrongSubmissions);
        DoClose();
    }

    private void OnClosePressed() => DoClose();

    private void DoClose()
    {
        StopAmbientLoop();
        _onClose?.Invoke();
    }

    // ── Audio ─────────────────────────────────────────────────────────────────

    /// <summary>Called by SubnetServerRow when its left/right button is pressed.</summary>
    public void PlayArrowSound(bool isRight)
    {
        if (sfxAudioSource == null) return;
        AudioClip clip = isRight ? rightArrowSound : leftArrowSound;
        if (clip != null) sfxAudioSource.PlayOneShot(clip, sfxVolume);
    }

    private void StartAmbientLoop()
    {
        if (ambientAudioSource == null || ambientLoopClip == null) return;
        ambientAudioSource.clip   = ambientLoopClip;
        ambientAudioSource.loop   = true;
        ambientAudioSource.volume = ambientVolume;
        ambientAudioSource.Play();
    }

    private void StopAmbientLoop()
    {
        if (ambientAudioSource == null) return;
        ambientAudioSource.Stop();
    }

    // ── DDA Tier ──────────────────────────────────────────────────────────────

    private int CurrentTier()
    {
        return PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.ComputerNetworks)
            : 2;
    }
}
