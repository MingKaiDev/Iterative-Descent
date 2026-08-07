// ConceptTutorials.cs
// Tracks which BKT concept tags have had their tutorial panel shown this
// session, and drives ConceptTutorialUI to show it the first time.
//
// Supersedes the old dialogue-based ConceptPrimers.cs (deleted 2026-08-07):
// same caption text, same "once per concept per session" behaviour, same
// call site (PuzzleProp.OpenPuzzle) -- but shown as a plain graphical HUD
// panel via ConceptTutorialUI instead of an ARBITEX dialogue line. That
// switch was made because every DialogueSequence .asset in this project was
// found to intermittently fail to resolve its script reference on load
// (a Unity/Library asset-resolution issue, not a logic bug); routing this
// specific feature through a runtime-built UI panel instead of a
// DialogueSequence asset sidesteps that class of failure entirely, and the
// user separately asked for a graphical "how to play"-style panel rather
// than a spoken line either way.
//
// Wording note: the captions below are adapted from the old ConceptPrimers
// dialogue lines, with first-person ARBITEX phrasing ("I", "tell me") removed
// since this is now a neutral instructional panel, not a character speaking.
//
// Only "arrays_and_lists" has a diagram built in the prefab right now (see
// ConceptTutorialUI's diagramRoot child list). Every other tag below already
// has caption text ready -- it will just be skipped (falls straight through
// to onDone) until a diagram child is added to the prefab for that tag.
//
// Usage (see PuzzleProp.OpenPuzzle):
//   ConceptTutorials.ShowIfUnseenThenContinue(conceptTag, OpenPuzzleUI);

using System;
using System.Collections.Generic;

public static class ConceptTutorials
{
    private static readonly Dictionary<string, string> _captionText = new Dictionary<string, string>
    {
        { "arrays_and_lists", "An array is a straight line of boxes in memory, sitting side by side. Because they sit side by side, you can jump straight to any box if you know its position. No searching required. That is why array access is instant." },
        { "basic_sorts", "You will be asked to sort. Every sorting method makes the same trade: how much time you spend comparing against how much time you spend moving things around. Bubble sort compares neighbours and swaps them. Merge sort splits the problem in half and stitches it back together." },
        { "bfs_dfs", "Two ways to explore a maze. Breadth First Search spreads outward evenly, checking every nearby room before going further, guaranteeing the shortest route. Depth First Search commits to one corridor and follows it as far as it goes before backtracking." },
        { "complexity_big_o", "Big O measures how much slower a task gets as the input grows. O(1) never slows down. O(log n) barely slows down. O(n squared) slows down fast enough to matter." },
        { "computer_networks", "Every network is a house divided into rooms, and every room needs an address. A /24 subnet has 256 addresses. Shrink the prefix number and the room gets bigger. Grow it and the room gets smaller." },
        { "cpu_scheduling", "The processor can only run one instruction at a time, yet many things seem to run at once. That illusion is scheduling. Round Robin gives everyone a slice of time. First Come First Served makes you wait your turn, no matter how long the process ahead of you takes." },
        { "hash_tables", "A hash table trades order for speed. It scrambles every key into a number, then uses that number to jump straight to a storage slot. Fast, unless two keys scramble to the same slot. That is a collision." },
        { "linked_lists", "Not every structure lives side by side in memory. A linked list is a chain, each piece only knows where the next one is. Finding something means following the chain link by link. But if you already hold a link, joining or removing pieces nearby costs almost nothing." },
        { "network_security", "A firewall reads its rules in order and stops at the first match. Block too broadly and you lock out the innocent along with the guilty. Block too narrowly and the attacker walks straight through the gap you left open." },
        { "networking_ports", "Every service listens on its own door. Port 80 for the unlocked web. Port 443 for the same door, sealed. Port 22 for a private, encrypted way in." },
        { "processes_threads", "A process is a sealed room with its own walls and its own air. A thread is a worker who can share a room with others, using the same walls, the same shared memory. Share too carelessly between threads and you get a race, where the outcome depends on who moves first." },
        { "stacks_and_queues", "Two disciplines of order. A stack serves whoever arrived last, like a pile of plates. A queue serves whoever arrived first, like a line at a door." },
        { "trees_bst", "A binary search tree keeps a simple promise at every node: smaller values to the left, larger values to the right. Follow that promise and you can find anything in a tree of a million nodes in about twenty steps." },
    };

    // Tracks which concept tags have already had their tutorial shown this
    // session (process lifetime). Intentionally NOT persisted to disk -- a
    // fresh launch of the build (e.g. a new pretrial participant) should see
    // every tutorial again.
    private static readonly HashSet<string> _shown = new HashSet<string>();

    /// <summary>
    /// Shows the tutorial panel for conceptTag if caption text and a diagram
    /// exist for it and it hasn't been shown yet this session, then invokes
    /// onDone once the panel is closed. Otherwise onDone fires immediately.
    /// </summary>
    public static void ShowIfUnseenThenContinue(string conceptTag, Action onDone)
    {
        if (string.IsNullOrEmpty(conceptTag) || _shown.Contains(conceptTag))
        {
            onDone?.Invoke();
            return;
        }

        if (!_captionText.TryGetValue(conceptTag, out string caption))
        {
            onDone?.Invoke();
            return;
        }

        if (ConceptTutorialUI.Instance == null || !ConceptTutorialUI.Instance.HasContentFor(conceptTag))
        {
            // Panel not in the scene, or no diagram built for this tag yet.
            onDone?.Invoke();
            return;
        }

        _shown.Add(conceptTag);
        ConceptTutorialUI.Instance.Show(conceptTag, caption, onDone);
    }

    /// <summary>
    /// Clears the "already shown" set so tutorials replay from scratch.
    /// Call this from a "New Game" / restart flow if the project ever adds
    /// one mid-process (e.g. a test harness that resets state without
    /// relaunching).
    /// </summary>
    public static void ResetSession() => _shown.Clear();
}
