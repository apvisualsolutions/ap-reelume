// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// What kind of root a path is, read from the path alone: UNC from the prefix, USB from the drive
/// being removable, local otherwise — and local again whenever the question cannot be answered,
/// because a wrong "local" costs a slower watch policy while a thrown question costs the dialog.
/// </summary>
/// <remarks>
/// The one drive query is handed in rather than made here, which is what keeps this a policy: the
/// composition passes <c>DriveInfo</c>'s answer, tests pass whatever the case needs, and the branch
/// no hosted runner can reach — a removable drive — is reachable by every test that wants it.
/// </remarks>
public static class RootKindPolicy
{
    public static RootKind Detect(string path, Func<string, DriveType> driveTypeOf)
    {
        ArgumentNullException.ThrowIfNull(driveTypeOf);
        if (string.IsNullOrWhiteSpace(path))
        {
            return RootKind.Local;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return RootKind.Unc;
        }

        try
        {
            return Path.GetPathRoot(path) is { Length: > 0 } root
                && driveTypeOf(root) == DriveType.Removable
                    ? RootKind.Usb
                    : RootKind.Local;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return RootKind.Local;
        }
    }
}
