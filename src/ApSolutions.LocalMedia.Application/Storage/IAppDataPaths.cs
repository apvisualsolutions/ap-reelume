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

    /// <summary>
    /// Frames taken from a course's own video to stand as its picture (CRS-006).
    /// </summary>
    /// <remarks>
    /// Beside the downloaded artwork rather than beside the personal kind, and that is the decision
    /// this makes: nobody chose these, the application took them, and every one can be taken again
    /// from a file that is still on the disk. So they regenerate and they never travel — the backup's
    /// allow-list carries «personal-artwork» and nothing else, which is what already refuses them.
    /// A cover somebody actually chose for a course does travel, because a course's identity is a
    /// title's identity and it lands in the personal folder like any other.
    /// </remarks>
    string CourseThumbnailDirectory { get; }

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

    /// <summary>
    /// Where a run that does not own this machine's profile writes what it would otherwise have
    /// handed to the operating system, and <see langword="null"/> for the run that does — which
    /// hands it over for real.
    /// <para>
    /// The same rule as <see cref="StartupRegistrySubKey"/>, applied to the exits that leave the
    /// application altogether: an address goes to the browser, a chosen path comes back from a modal
    /// dialog. A harness cannot answer a dialog and must not open a browser on the machine measuring
    /// it, so an isolated run writes the address down and reads the path from what its own root
    /// declares. <see langword="null"/> rather than a folder nobody uses, because the distinction is
    /// not <em>where</em> the handover goes but <em>whether</em> it happens at all.
    /// </para>
    /// </summary>
    string? SystemHandoffDirectory { get; }
}
