namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// The containers this release recognises, in one place.
/// <para>
/// The scanner and loose-file activation must agree: a file the library would catalogue has to be a
/// file "Open with…" will play, and the reverse. Two lists would drift.
/// </para>
/// </summary>
public static class MediaFileExtensions
{
    private static readonly HashSet<string> Approved = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mkv",
        ".avi",
        ".mov",
        ".webm",
        ".m4v",
        ".ts",
        ".m2ts",
    };

    /// <summary>The approved containers, lower-case and ordered as the specification lists them.</summary>
    public static IReadOnlyCollection<string> All { get; } =
        [".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".ts", ".m2ts"];

    /// <summary>True when the extension belongs to the approved set, ignoring case.</summary>
    public static bool IsApproved(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && Approved.Contains(extension);
}
