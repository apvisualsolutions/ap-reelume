// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Lifecycle;

namespace ApSolutions.LocalMedia.Windows.Shell;

/// <summary>
/// Writes down what would have been handed to Windows instead of handing it over, for a run that
/// does not own this machine's profile.
/// </summary>
/// <remarks>
/// <para>
/// It sits beside <see cref="WindowsSystemHandoff"/> because the two are the same exit: which one
/// the application is built with is decided by the data root, once, in the composition — the same
/// choice the trailer link and the archive dialogs make. A harness that pressed "Exit" against the
/// other one would end the suite measuring it, and one that pressed "Open the backup folder" would
/// leave an Explorer window on somebody's desktop.
/// </para>
/// <para>
/// One line per handover, in the order they were asked for, because the order is part of what a walk
/// wants to be able to read back: a verb and, where there is one, the single thing handed over. A
/// verb of its own for each is what lets a probe tell "the folder was offered" from "leaving was
/// asked for" without parsing anything.
/// </para>
/// </remarks>
public sealed class RecordingSystemHandoff : ISystemHandoff
{
    /// <summary>The file inside the handover folder, named for what it holds.</summary>
    public const string FileName = "system-handoff.txt";

    /// <summary>What a line says when a folder was offered to whoever is at the machine.</summary>
    public const string OpenFolderVerb = "open-folder";

    /// <summary>What a line says when the application was asked to end.</summary>
    public const string ExitVerb = "exit";

    private readonly Lock _gate = new();

    public RecordingSystemHandoff(string handoffDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        RecordPath = Path.Combine(handoffDirectory, FileName);
    }

    /// <summary>Where the handovers land, so whoever asked can read what would have happened.</summary>
    public string RecordPath { get; }

    public bool TryOpenFolder(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        return Record($"{OpenFolderVerb} {folder}");
    }

    public void RequestExit() => _ = Record(ExitVerb);

    private bool Record(string line)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!);
                File.AppendAllText(RecordPath, line + Environment.NewLine);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The same answer the Windows exit gives when the shell will not take the folder: the
            // offer could not be honoured, and the screen goes on offering everything else.
            return false;
        }
    }
}
