namespace ApSolutions.LocalMedia.Domain.Discovery;

public sealed class RenamePolicy
{
    private const int MaximumFileNameLength = 255;
    private const int MaximumWindowsPathLength = 32_767;
    private static readonly HashSet<char> InvalidFileNameCharacters =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
    private static readonly HashSet<string> ReservedBaseNames = CreateReservedBaseNames();
    private readonly StringComparer _pathComparer = StringComparer.OrdinalIgnoreCase;

    public RenamePlan CreatePlan(string rootPath, IReadOnlyList<RenameRequest> requests)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(requests);
        var root = NormalizeRoot(rootPath);
        var operations = new List<RenameOperation>(requests.Count);
        var conflicts = new List<RenameConflict>();

        for (var index = 0; index < requests.Count; index++)
        {
            AddRequest(root, requests[index], index, operations, conflicts);
        }

        foreach (var collision in operations
            .GroupBy(operation => operation.DestinationPath, _pathComparer)
            .Where(group => group.Count() > 1))
        {
            foreach (var operation in collision)
            {
                conflicts.Add(new RenameConflict(
                    operation.Sequence,
                    RenameConflictKind.DuplicateDestination,
                    operation.DestinationPath));
            }
        }

        return new RenamePlan(Guid.NewGuid(), root, operations, conflicts);
    }

    public static bool IsConfinedToRoot(string path, string rootPath)
    {
        var root = NormalizeRoot(rootPath);
        var normalized = Path.GetFullPath(path);
        return normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddRequest(
        string root,
        RenameRequest request,
        int index,
        List<RenameOperation> operations,
        List<RenameConflict> conflicts)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SourcePath))
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.InvalidPath, "Source path is required."));
            return;
        }

        string source;
        try
        {
            source = Path.GetFullPath(request.SourcePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.InvalidPath, "Source path is invalid."));
            return;
        }

        if (!IsConfinedToRoot(source, root))
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.SourceOutsideRoot, source));
            return;
        }

        if (string.IsNullOrWhiteSpace(request.DesiredFileName))
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.FolderMove, request.DesiredFileName));
            return;
        }

        if (Path.IsPathRooted(request.DesiredFileName)
            || request.DesiredFileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            var requestedDestination = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, request.DesiredFileName));
            var kind = IsConfinedToRoot(requestedDestination, root)
                ? RenameConflictKind.FolderMove
                : RenameConflictKind.DestinationOutsideRoot;
            conflicts.Add(new RenameConflict(index, kind, requestedDestination));
            return;
        }

        var safeName = SanitizeFileName(request.DesiredFileName);
        if (safeName.Length == 0)
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.InvalidPath, "Destination filename is empty."));
            return;
        }

        if (safeName.Length > MaximumFileNameLength)
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.PathTooLong, safeName));
            return;
        }

        var sourceDirectory = Path.GetDirectoryName(source)!;
        var destinationCandidate = Path.Combine(sourceDirectory, safeName);
        if (destinationCandidate.Length >= MaximumWindowsPathLength)
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.PathTooLong, destinationCandidate));
            return;
        }

        var destination = Path.GetFullPath(destinationCandidate);

        if (string.Equals(source, destination, StringComparison.Ordinal))
        {
            conflicts.Add(new RenameConflict(index, RenameConflictKind.NoChange, destination));
            return;
        }

        operations.Add(new RenameOperation(index, source, destination));
    }

    private static string NormalizeRoot(string rootPath) =>
        Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string SanitizeFileName(string desiredFileName)
    {
        var characters = desiredFileName
            .Select(character => character < ' ' || InvalidFileNameCharacters.Contains(character) ? '-' : character)
            .ToArray();
        var safeName = new string(characters).Trim().TrimEnd('.', ' ');
        var baseName = Path.GetFileNameWithoutExtension(safeName);
        return ReservedBaseNames.Contains(baseName) ? $"_{safeName}" : safeName;
    }

    private static HashSet<string> CreateReservedBaseNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON",
            "PRN",
            "AUX",
            "NUL",
        };
        for (var suffix = 1; suffix <= 9; suffix++)
        {
            names.Add($"COM{suffix}");
            names.Add($"LPT{suffix}");
        }

        return names;
    }
}
