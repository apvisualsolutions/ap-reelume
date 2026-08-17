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
/// They live behind one port because they are one promise rather than one caller's convenience: a
/// run either hands things to this machine or it does not, and letting some of them go one way and
/// some the other is how a rule stops being one.
/// </para>
/// </remarks>
public interface ISystemHandoff
{
    /// <summary>
    /// Shows a folder to whoever is at the machine, and says whether the handover was accepted.
    /// </summary>
    bool TryOpenFolder(string folder);

    /// <summary>
    /// Opens a verified update package the way a person would, and says whether anything took it.
    /// </summary>
    /// <remarks>
    /// Opening rather than installing, because this application installs nothing: on Windows 11 the
    /// package goes to the App Installer and what replaces the running binary is Windows.
    /// </remarks>
    bool TryOpenPackage(string package);

    /// <summary>Asks for the application to end.</summary>
    void RequestExit();
}
