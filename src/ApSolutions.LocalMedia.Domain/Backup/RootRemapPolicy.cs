namespace ApSolutions.LocalMedia.Domain.Backup;

/// <summary>One answer to "the library used to be here; where is it now?".</summary>
public sealed record RootRemap(string OldPath, string NewPath)
{
    /// <summary>
    /// The same pair with both sides in comparable form. Paths arrive typed by a person, so trailing
    /// separators and mixed slashes are normal and must not turn one folder into two.
    /// </summary>
    public RootRemap Normalized() => new(
        RootRemapPolicy.Normalize(OldPath),
        RootRemapPolicy.Normalize(NewPath));
}

public enum RootRemapStatus
{
    /// <summary>The folder is where the backup left it.</summary>
    Unchanged,

    /// <summary>A person pointed this root somewhere else.</summary>
    Remapped,

    /// <summary>The folder is not there and nobody said where it went. The restore may still proceed.</summary>
    Missing,

    /// <summary>Two roots aim at one folder. Restoring would merge two libraries into one.</summary>
    Conflict,
}

public sealed record RootRemapDecision(string OldPath, string NewPath, RootRemapStatus Status)
{
    /// <summary>Only a conflict stops a restore. A missing folder is a fact, not a mistake.</summary>
    public bool IsBlocking => Status == RootRemapStatus.Conflict;
}

/// <summary>
/// Decides where each stored root ends up and rewrites the paths that hang from it.
/// <para>
/// The rule that matters is the conflict: pointing two different roots at one folder would merge two
/// libraries silently, and no later step could tell the two apart again. So it is refused here, before
/// anything is written, rather than discovered afterwards.
/// </para>
/// </summary>
public static class RootRemapPolicy
{
    public static IReadOnlyList<RootRemapDecision> Resolve(
        IEnumerable<string> storedRoots,
        IEnumerable<RootRemap> remaps,
        Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(storedRoots);
        ArgumentNullException.ThrowIfNull(remaps);
        ArgumentNullException.ThrowIfNull(exists);

        var roots = storedRoots.Select(Normalize).ToArray();
        var requested = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var remap in remaps.Select(remap => remap.Normalized()))
        {
            if (!roots.Contains(remap.OldPath, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"The backup has no root at '{remap.OldPath}'.",
                    nameof(remaps));
            }

            requested[remap.OldPath] = remap.NewPath;
        }

        var decisions = roots
            .Select(root => new RootRemapDecision(root, Destination(root, requested), Status(root, requested, exists)))
            .ToArray();

        var contested = decisions
            .GroupBy(decision => decision.NewPath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return contested.Count == 0
            ? decisions
            : [.. decisions.Select(decision => contested.Contains(decision.NewPath)
                ? decision with { Status = RootRemapStatus.Conflict }
                : decision)];
    }

    /// <summary>
    /// Moves one stored path under whichever root now owns it. The longest matching root wins, so a
    /// nested root keeps its own files instead of being swallowed by its parent.
    /// </summary>
    public static string Rewrite(string storedPath, IReadOnlyList<RootRemapDecision> decisions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedPath);
        ArgumentNullException.ThrowIfNull(decisions);
        var normalized = Normalize(storedPath);
        var owner = decisions
            .Where(decision => IsUnder(normalized, decision.OldPath))
            .OrderByDescending(decision => decision.OldPath.Length)
            .FirstOrDefault();
        if (owner is null || owner.OldPath.Equals(owner.NewPath, StringComparison.OrdinalIgnoreCase))
        {
            return storedPath;
        }

        return owner.NewPath + normalized[owner.OldPath.Length..];
    }

    /// <summary>Comparable form: one kind of separator, no trailing one, and nothing blank.</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var trimmed = path.Trim().Replace('/', '\\');
        return trimmed.Length > 3 ? trimmed.TrimEnd('\\') : trimmed;
    }

    private static string Destination(string root, IReadOnlyDictionary<string, string> requested) =>
        requested.TryGetValue(root, out var destination) ? destination : root;

    private static RootRemapStatus Status(
        string root,
        IReadOnlyDictionary<string, string> requested,
        Func<string, bool> exists)
    {
        var destination = Destination(root, requested);
        if (!destination.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return RootRemapStatus.Remapped;
        }

        return exists(root) ? RootRemapStatus.Unchanged : RootRemapStatus.Missing;
    }

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root + '\\', StringComparison.OrdinalIgnoreCase);
}
