namespace ApSolutions.LocalMedia.Windows.Shell;

/// <summary>
/// Reads the command line Windows hands the application when a file is opened with it.
/// <para>
/// The parsing is deliberately narrow: the first positional argument and nothing else. It never runs
/// a shell, never expands a wildcard, and never accepts a switch, so a crafted argument list cannot
/// turn an activation into a command.
/// </para>
/// </summary>
public static class FileActivationHandler
{
    private static readonly char[] SwitchPrefixes = ['-', '/'];

    /// <summary>
    /// Returns the path the activation carries, or null when the arguments do not describe one.
    /// </summary>
    public static string? Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            return null;
        }

        var first = arguments[0];
        if (string.IsNullOrWhiteSpace(first))
        {
            return null;
        }

        return SwitchPrefixes.Contains(first[0]) ? null : first;
    }
}
