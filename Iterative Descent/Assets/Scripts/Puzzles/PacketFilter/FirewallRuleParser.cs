using System;
using System.Collections.Generic;
using UnityEngine;

public enum RuleAction { Allow, Deny }

/// <summary>
/// One firewall rule written by the player in Phase 3.
/// All fields except Action are optional (null = match any).
/// </summary>
public class FirewallRule
{
    public RuleAction Action;
    public string Proto;          // null = any; "TCP", "UDP", "ICMP"
    public string SrcIp;          // null = any; "10.0.x.x" wildcard supported
    public string DstIp;          // null = any
    public int?   DstPort;        // null = any
    public string OriginalText;   // raw player input, stored for display
}

public class RuleParseResult
{
    public FirewallRule Rule;
    public string       Error;
    public bool         Success => Error == null;
}

/// <summary>
/// Result of evaluating the player's rule list against the full packet set.
/// </summary>
public class EvaluationResult
{
    public HashSet<string> BlockedC2Channels  = new HashSet<string>();
    public HashSet<string> CollateralServices = new HashSet<string>();
}

/// <summary>
/// Parses and evaluates player-written firewall rules.
///
/// Simplified positional syntax -- no tag names needed:
///   [ALLOW|DENY] [TCP|UDP|ICMP] [port] [src_ip]
///
/// Each token after the action is auto-detected by shape:
///   TCP / UDP / ICMP  ->  protocol filter
///   integer (1-65535) ->  destination port filter
///   contains a dot    ->  source IP filter (supports x wildcard, e.g. 10.0.x.x)
///
/// All tokens except the action are optional and order does not matter.
///
/// Examples:
///   DENY TCP 4444 10.0.1.x
///   DENY UDP 53 10.0.1.2
///   DENY TCP 8080
///   ALLOW TCP 4444 10.0.2.x
/// </summary>
public static class FirewallRuleParser
{
    private static readonly HashSet<string> Protocols =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "TCP", "UDP", "ICMP" };

    // ── Parse ──────────────────────────────────────────────────────────────────

    public static RuleParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Fail("Rule cannot be empty.");

        input = input.Trim();
        var tokens = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return Fail("Rule cannot be empty.");

        var rule = new FirewallRule { OriginalText = input };

        // First token: ALLOW or DENY
        string actionStr = tokens[0].ToUpperInvariant();
        if      (actionStr == "ALLOW") rule.Action = RuleAction.Allow;
        else if (actionStr == "DENY")  rule.Action = RuleAction.Deny;
        else return Fail($"First word must be ALLOW or DENY -- got '{tokens[0]}'.");

        // Remaining tokens: auto-detect by shape
        for (int i = 1; i < tokens.Length; i++)
        {
            string tok = tokens[i];

            if (Protocols.Contains(tok))
            {
                if (rule.Proto != null)
                    return Fail($"Protocol specified twice ('{rule.Proto}' and '{tok}').");
                rule.Proto = tok.ToUpperInvariant();
            }
            else if (int.TryParse(tok, out int port))
            {
                if (port < 1 || port > 65535)
                    return Fail($"Port {port} is out of range (1-65535).");
                if (rule.DstPort.HasValue)
                    return Fail($"Port specified twice ({rule.DstPort} and {port}).");
                rule.DstPort = port;
            }
            else if (tok.Contains('.'))
            {
                if (rule.SrcIp != null)
                    return Fail($"IP specified twice ('{rule.SrcIp}' and '{tok}').");
                rule.SrcIp = tok;
            }
            else
            {
                return Fail($"'{tok}' is not a valid protocol, port, or IP.");
            }
        }

        return new RuleParseResult { Rule = rule };
    }

    // ── Match ──────────────────────────────────────────────────────────────────

    /// <summary>Returns true if the rule matches the given packet.</summary>
    public static bool Matches(FirewallRule rule, PacketData packet)
    {
        if (rule.Proto != null &&
            !string.Equals(rule.Proto, packet.Proto, StringComparison.OrdinalIgnoreCase))
            return false;

        if (rule.SrcIp != null && !IpMatches(rule.SrcIp, packet.SrcIp))
            return false;

        if (rule.DstIp != null && !IpMatches(rule.DstIp, packet.DstIp))
            return false;

        if (rule.DstPort.HasValue && rule.DstPort.Value != packet.DstPort)
            return false;

        return true;
    }

    /// <summary>
    /// First-match-wins evaluation across the rule list.
    /// Returns the action of the first matching rule, or null (implicit Allow) if none match.
    /// </summary>
    public static RuleAction? Evaluate(List<FirewallRule> rules, PacketData packet)
    {
        foreach (var rule in rules)
            if (Matches(rule, packet))
                return rule.Action;
        return null;
    }

    /// <summary>
    /// Evaluates all rules against the full packet set and returns:
    ///   BlockedC2Channels  -- C2 channel IDs that are fully covered by a DENY rule
    ///   CollateralServices -- Legitimate service names that would be blocked
    /// </summary>
    public static EvaluationResult EvaluateAll(List<FirewallRule> rules, List<PacketData> packets)
    {
        var result = new EvaluationResult();

        foreach (var packet in packets)
        {
            RuleAction? action = Evaluate(rules, packet);
            if (action != RuleAction.Deny) continue;

            if (packet.IsMalicious && packet.C2Channel != null)
                result.BlockedC2Channels.Add(packet.C2Channel);

            if (!packet.IsMalicious && packet.IsCollateralRisk && packet.ServiceName != null)
                result.CollateralServices.Add(packet.ServiceName);
        }

        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Supports exact IP match and "x" as a wildcard octet.
    /// "10.0.x.x" matches "10.0.1.4" and "10.0.255.0".
    /// </summary>
    private static bool IpMatches(string pattern, string ip)
    {
        if (string.Equals(pattern, ip, StringComparison.OrdinalIgnoreCase)) return true;
        if (!pattern.Contains("x")) return false;

        string[] pOctets = pattern.Split('.');
        string[] iOctets = ip.Split('.');
        if (pOctets.Length != 4 || iOctets.Length != 4) return false;

        for (int i = 0; i < 4; i++)
        {
            if (pOctets[i] == "x") continue;
            if (!string.Equals(pOctets[i], iOctets[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static RuleParseResult Fail(string error) =>
        new RuleParseResult { Error = error };
}
