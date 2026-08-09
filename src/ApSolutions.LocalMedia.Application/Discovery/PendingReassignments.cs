using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Discovery;

/// <summary>One cataloged file the discovered one may really be: where it was, and which row it is.</summary>
public sealed record ReassignmentCandidate(MediaFileId MediaFileId, string Path);

/// <summary>
/// A discovered file that matches cataloged content without the certainty to act alone. The command
/// is ready to confirm; the candidates are the rows a person chooses between.
/// </summary>
public sealed record PendingReassignment(
    ReconcileFileCommand Command,
    IReadOnlyList<ReassignmentCandidate> Candidates);

/// <summary>
/// What reconciliation held for a person, until they decide. The held file keeps no stored
/// identity, so every later scan re-derives the same offer: a restart loses nothing but the list
/// itself, never the decision.
/// </summary>
public sealed class PendingReassignments
{
    private readonly Lock _sync = new();
    private readonly List<PendingReassignment> _items = [];

    public IReadOnlyList<PendingReassignment> List()
    {
        lock (_sync)
        {
            return [.. _items];
        }
    }

    /// <summary>Offers one path once: a rescan replaces the offer instead of stacking a copy.</summary>
    public void Offer(PendingReassignment item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_sync)
        {
            _items.RemoveAll(existing => PathEquals(existing, item.Command.NewPath));
            _items.Add(item);
        }
    }

    public void Remove(string newPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        lock (_sync)
        {
            _items.RemoveAll(existing => PathEquals(existing, newPath));
        }
    }

    private static bool PathEquals(PendingReassignment item, string path) =>
        string.Equals(item.Command.NewPath, path, StringComparison.OrdinalIgnoreCase);
}
