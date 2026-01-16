using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FireworksApp.Simulation;

public sealed record class FireworksProfileSet(
    IReadOnlyDictionary<string, CanisterProfile> Canisters,
    IReadOnlyDictionary<string, FireworkShellProfile> Shells,
    IReadOnlyDictionary<string, GroundEffectProfile> GroundEffects,
    IReadOnlyDictionary<string, ColorScheme> ColorSchemes,
    IReadOnlyDictionary<string, SubShellProfile> SubShells,
    IReadOnlyDictionary<string, TrailProfile> TrailProfiles,
    IReadOnlyDictionary<string, TrailProfile> SubShellTrailProfiles);

public static class ProfileValidator
{
    public static void Validate(FireworksProfileSet profileSet)
    {
        ArgumentNullException.ThrowIfNull(profileSet);

        var shells = profileSet.Shells;
        var subshells = profileSet.SubShells;
        var colorSchemes = profileSet.ColorSchemes;
        var trailProfiles = profileSet.TrailProfiles;
        var subshellTrailProfiles = profileSet.SubShellTrailProfiles;

        foreach (var canister in profileSet.Canisters.Values)
        {
            EnsureExists(shells, canister.DefaultShellProfileId, $"Canister {canister.Id} references missing shell profile");
        }

        if (subshellTrailProfiles.Count > 0)
        {
            foreach (var shell in shells.Values)
            {
                if (shell.FinaleSalute is { TrailProfile: { } finaleTrail })
                    EnsureExists(subshellTrailProfiles, finaleTrail.Id, $"Shell {shell.Id} finale trail references missing subshell trail profile {finaleTrail.Id}");

                if (shell.SubShellSpokeWheelPop is { TrailProfile: { } spokeTrail })
                    EnsureExists(subshellTrailProfiles, spokeTrail.Id, $"Shell {shell.Id} spoke wheel trail references missing subshell trail profile {spokeTrail.Id}");
            }
        }

        foreach (var shell in shells.Values)
        {
            EnsureExists(colorSchemes, shell.ColorSchemeId, $"Shell {shell.Id} references missing color scheme");

            if (trailProfiles.Count > 0)
            {
                EnsureExists(trailProfiles, shell.Trail.Id, $"Shell {shell.Id} references missing trail profile {shell.Trail.Id}");
            }

            if (shell.PeonyToWillow is { } peonyToWillow)
            {
                EnsureExists(subshells, peonyToWillow.WillowSubshellProfileId, $"Shell {shell.Id} references missing subshell profile {peonyToWillow.WillowSubshellProfileId}");
            }

            if (shell.Comet is { SubShellProfileId: { } cometSubshell })
            {
                EnsureExists(subshells, cometSubshell, $"Shell {shell.Id} comet references missing subshell profile {cometSubshell}");
            }

            if (shell.SubShellSpokeWheelPop is { PopFlashColorSchemeId: { } popFlashScheme })
            {
                EnsureExists(colorSchemes, popFlashScheme, $"Shell {shell.Id} spoke pop references missing color scheme {popFlashScheme}");
            }
        }

        foreach (var subshell in subshells.Values)
        {
            EnsureExists(shells, subshell.ShellProfileId, $"Subshell {subshell.Id} references missing shell profile {subshell.ShellProfileId}");

            if (subshell.ColorSchemeId is { } subshellScheme)
            {
                EnsureExists(colorSchemes, subshellScheme, $"Subshell {subshell.Id} references missing color scheme {subshellScheme}");
            }
        }

        foreach (var groundEffect in profileSet.GroundEffects.Values)
        {
            EnsureExists(colorSchemes, groundEffect.ColorSchemeId, $"Ground effect {groundEffect.Id} references missing color scheme");
        }

        DetectShellSubshellCycles(shells, subshells);

        LogSummary(profileSet);
        LogDetails(profileSet);
    }

    private static void DetectShellSubshellCycles(
        IReadOnlyDictionary<string, FireworkShellProfile> shells,
        IReadOnlyDictionary<string, SubShellProfile> subshells)
    {
        var adjacency = new Dictionary<string, List<string>>();

        static string ShellNode(string id) => $"shell:{id}";
        static string SubshellNode(string id) => $"subshell:{id}";

        void AddEdge(string from, string to)
        {
            if (!adjacency.TryGetValue(from, out var list))
            {
                list = new List<string>();
                adjacency[from] = list;
            }
            list.Add(to);
        }

        foreach (var shell in shells.Values)
        {
            var shellNode = ShellNode(shell.Id);

            if (shell.PeonyToWillow is { } peonyToWillow)
            {
                AddEdge(shellNode, SubshellNode(peonyToWillow.WillowSubshellProfileId));
            }

            if (shell.Comet is { SubShellProfileId: { } cometSubshell })
            {
                AddEdge(shellNode, SubshellNode(cometSubshell));
            }
        }

        foreach (var subshell in subshells.Values)
        {
            AddEdge(SubshellNode(subshell.Id), ShellNode(subshell.ShellProfileId));
        }

        var visited = new HashSet<string>();
        var stack = new Stack<string>();
        var inPath = new HashSet<string>();

        void Dfs(string node)
        {
            if (!inPath.Add(node))
            {
                throw new InvalidOperationException($"Cycle detected in shell/subshell references: {string.Join(" -> ", stack.Reverse().Append(node))}");
            }

            visited.Add(node);
            stack.Push(node);

            if (adjacency.TryGetValue(node, out var next))
            {
                foreach (var neighbor in next)
                {
                    if (!visited.Contains(neighbor))
                        Dfs(neighbor);
                    else if (inPath.Contains(neighbor))
                        throw new InvalidOperationException($"Cycle detected in shell/subshell references: {string.Join(" -> ", stack.Reverse().Append(neighbor))}");
                }
            }

            stack.Pop();
            inPath.Remove(node);
        }

        foreach (var node in adjacency.Keys)
        {
            if (!visited.Contains(node))
            {
                Dfs(node);
            }
        }
    }

    private static void EnsureExists<T>(
        IReadOnlyDictionary<string, T> dictionary,
        string id,
        string message)
    {
        if (!dictionary.ContainsKey(id))
            throw new InvalidOperationException(message);
    }

    [Conditional("DEBUG")]
    public static void LogSummary(FireworksProfileSet profileSet)
    {
        ArgumentNullException.ThrowIfNull(profileSet);
        Debug.WriteLine($"[Profiles] Canisters={profileSet.Canisters.Count}, Shells={profileSet.Shells.Count}, SubShells={profileSet.SubShells.Count}, GroundEffects={profileSet.GroundEffects.Count}, ColorSchemes={profileSet.ColorSchemes.Count}, TrailProfiles={profileSet.TrailProfiles.Count}, SubShellTrailProfiles={profileSet.SubShellTrailProfiles.Count}");
    }

    [Conditional("DEBUG")]
    public static void LogDetails(FireworksProfileSet profileSet)
    {
        ArgumentNullException.ThrowIfNull(profileSet);

        var shapeCounts = new Dictionary<FireworkBurstShape, int>();
        foreach (var shell in profileSet.Shells.Values)
        {
            var shape = shell.BurstShape;
            shapeCounts[shape] = shapeCounts.TryGetValue(shape, out var n) ? n + 1 : 1;
        }

        var groundTypeCounts = new Dictionary<GroundEffectType, int>();
        foreach (var ge in profileSet.GroundEffects.Values)
        {
            var type = ge.Type;
            groundTypeCounts[type] = groundTypeCounts.TryGetValue(type, out var n) ? n + 1 : 1;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("[Profiles] Shapes:");
        foreach (var kvp in shapeCounts)
        {
            sb.Append(' ').Append(kvp.Key).Append('=').Append(kvp.Value).Append(';');
        }

        sb.Append(" GroundTypes:");
        foreach (var kvp in groundTypeCounts)
        {
            sb.Append(' ').Append(kvp.Key).Append('=').Append(kvp.Value).Append(';');
        }

        Debug.WriteLine(sb.ToString());
    }
}
