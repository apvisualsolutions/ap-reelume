// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Lifecycle;

/// <summary>
/// What a run hands to the operating system itself: a folder for somebody to look at, and the
/// request that this application stop.
/// </summary>
/// <remarks>
/// <para>
/// Both are exits in the same sense the trailer link and the archive dialogs are, and they are
/// decided the same way — once, in the composition, by the resolved data root. The person whose
/// profile this is gets Explorer and a real shutdown; a run keeping its data somewhere of its own
/// writes down what it would have handed over and leaves the machine it runs on alone.
/// </para>
/// <para>
/// The two live behind one port because they have one caller: the screen that appears when the
/// database will not open offers exactly these two things to do about it.
/// </para>
/// </remarks>
public interface ISystemHandoff
{
    /// <summary>
    /// Shows a folder to whoever is at the machine, and says whether the handover was accepted.
    /// </summary>
    bool TryOpenFolder(string folder);

    /// <summary>Asks for the application to end.</summary>
    void RequestExit();
}
