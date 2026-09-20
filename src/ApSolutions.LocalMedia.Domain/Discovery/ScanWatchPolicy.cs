// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// Whether a root is followed live by a file watcher, rather than only swept.
/// </summary>
/// <remarks>
/// <para>
/// This exists because of ENG-044. <see cref="ScanPolicy.Continuous"/> was the only thing that
/// switched the live watcher on, and <b>nothing in the tree ever assigned it</b>: every way of
/// adding a root produced <c>Startup | Manual</c> or <c>Manual</c>, and no screen offered the
/// choice. So the whole watching slice — the debounced watcher, its coordinator, its background
/// host — was built, registered, covered by tests and never reached in the assembled application,
/// while the scope record called continuous watching verified.
/// </para>
/// <para>
/// The fix is not to make <c>Continuous</c> the default behind everyone's back, which would follow
/// a network share nobody asked to be followed. It is to let the decision be made where a person
/// can make it — the one setting in Settings that already says «watch changes in local roots» — and
/// to leave the per-root flag as the stronger, explicit statement it was always meant to be.
/// </para>
/// <para>
/// <b>And the setting stops at <see cref="ScanPolicy.Startup"/>, which an existing test taught.</b>
/// <c>WatchCoordinatorTests.A_manual_root_is_not_watched_behind_its_owners_back</c> holds that a
/// root whose owner chose Manual is not followed, and that is not a formality:
/// <c>DeclareCourseFolder</c> adds a course folder as Manual alone on purpose, because the dialog's
/// own help promises the rest of the drive is left alone. A setting that reached every local root
/// would break a promise made in writing. So it reaches the roots that already asked to be kept up
/// to date — the ones carrying <c>Startup</c>, which every folder added the normal way gets — and
/// never the ones that said «only when I ask».
/// </para>
/// </remarks>
public static class ScanWatchPolicy
{
    /// <param name="kind">Where the root lives: a fixed disk, a removable drive, or a share.</param>
    /// <param name="policy">What that root itself declares.</param>
    /// <param name="watchLocalRoots">The setting a person can reach.</param>
    public static bool ShouldWatchLive(RootKind kind, ScanPolicy policy, bool watchLocalRoots) =>
        policy.HasFlag(ScanPolicy.Continuous)
        || (watchLocalRoots && kind == RootKind.Local && policy.HasFlag(ScanPolicy.Startup));
}
