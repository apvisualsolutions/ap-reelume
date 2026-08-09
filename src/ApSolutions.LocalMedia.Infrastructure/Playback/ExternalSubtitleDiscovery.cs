using System.Text;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>One external subtitle file found beside its media, with the encoding it declares.</summary>
public sealed record ExternalSubtitle(string Path, string Format, string Encoding, string? LanguageHint);

/// <summary>
/// Finds subtitle files that sit next to a media file and share its base name. The search never
/// leaves the media's own directory and never leaves the library root, so a preference can never
/// pull a file in from elsewhere on the disk.
/// </summary>
public static class ExternalSubtitleDiscovery
{
    private static readonly string[] AllowedExtensions = [".srt", ".ass", ".vtt"];

    /// <summary>The extensions the application will ever load from disk.</summary>
    public static IReadOnlyList<string> SupportedExtensions => AllowedExtensions;

    /// <summary>
    /// Lists the subtitles for one media file. A media file outside the root, or a candidate that
    /// resolves outside it, is refused rather than loaded.
    /// </summary>
    public static IReadOnlyList<ExternalSubtitle> Discover(string mediaPath, string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var media = Path.GetFullPath(mediaPath);
        var root = Path.GetFullPath(rootPath);
        if (!IsInside(media, root))
        {
            return [];
        }

        var directory = Path.GetDirectoryName(media);
        if (directory is null || !Directory.Exists(directory))
        {
            return [];
        }

        var baseName = Path.GetFileNameWithoutExtension(media);
        var found = new List<ExternalSubtitle>();
        foreach (var candidate in Directory.EnumerateFiles(directory))
        {
            var extension = Path.GetExtension(candidate);
            if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidateName = Path.GetFileNameWithoutExtension(candidate);
            if (!candidateName.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var full = Path.GetFullPath(candidate);
            if (!IsInside(full, root))
            {
                continue;
            }

            found.Add(new ExternalSubtitle(
                full,
                extension.TrimStart('.').ToUpperInvariant(),
                DetectEncoding(full),
                LanguageHint(candidateName, baseName)));
        }

        return [.. found.OrderBy(subtitle => subtitle.Path, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Reads the byte order mark so UTF-8 and both UTF-16 orders are reported accurately.</summary>
    public static string DetectEncoding(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        Span<byte> prefix = stackalloc byte[4];
        var read = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
        if (read >= 3 && prefix[0] == 0xEF && prefix[1] == 0xBB && prefix[2] == 0xBF)
        {
            return "UTF-8-BOM";
        }

        if (read >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            return "UTF-16LE";
        }

        if (read >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            return "UTF-16BE";
        }

        return "UTF-8";
    }

    /// <summary>Reads the file with the encoding it declares, so both UTF families render as text.</summary>
    public static string ReadText(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var encoding = DetectEncoding(path) switch
        {
            "UTF-16LE" => Encoding.Unicode,
            "UTF-16BE" => Encoding.BigEndianUnicode,
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static bool IsInside(string candidate, string root)
    {
        var normalisedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalisedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? LanguageHint(string candidateName, string baseName)
    {
        if (candidateName.Length <= baseName.Length)
        {
            return null;
        }

        var suffix = candidateName[baseName.Length..].Trim('.', '-', '_', ' ');
        return string.IsNullOrWhiteSpace(suffix) ? null : suffix;
    }
}
