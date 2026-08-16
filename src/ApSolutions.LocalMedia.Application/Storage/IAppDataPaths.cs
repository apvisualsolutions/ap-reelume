// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Storage;

/// <summary>
/// Every place the application is allowed to keep something on this machine. It is one contract rather
/// than a path per feature so that a backup can state exactly what it copies and what it leaves behind
/// without composing folder names of its own.
/// </summary>
public interface IAppDataPaths
{
    /// <summary>The folder that holds everything below; nothing the application writes lives outside it.</summary>
    string DataRoot { get; }

    string DatabasePath { get; }

    string SettingsPath { get; }

    /// <summary>Where rotating copies are kept. Copies are never written next to the live database.</summary>
    string BackupsDirectory { get; }

    /// <summary>Artwork a person chose. It is theirs, so it travels in a backup.</summary>
    string PersonalArtworkDirectory { get; }

    /// <summary>Artwork downloaded from a provider. It regenerates, so it never travels.</summary>
    string RemoteCacheDirectory { get; }

    /// <summary>Where an exported diagnostic report is written, and nowhere a backup ever looks.</summary>
    string DiagnosticsDirectory { get; }

    /// <summary>
    /// The registry key under the current user that holds the sign-in startup entry.
    /// <para>
    /// It belongs to this contract for the same reason the folders do: it is a place the application
    /// writes on this machine, and a run that keeps its data somewhere of its own must keep this
    /// somewhere of its own too. A harness that pressed "start with Windows" against the real key
    /// would register whatever binary it just built to start at sign-in, on the machine of whoever
    /// ran it — which is why the suites were never allowed near that key, and why the control could
    /// not be covered until it moved here.
    /// </para>
    /// </summary>
    string StartupRegistrySubKey { get; }
}
