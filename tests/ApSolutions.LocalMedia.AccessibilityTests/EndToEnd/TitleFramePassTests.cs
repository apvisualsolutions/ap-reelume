// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Windows.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The refusal of the frame pass's launcher (LIB-021), which is the one arm the assembled walk
/// cannot take: there the composition root always hands it the pass it wraps.
/// </summary>
/// <remarks>
/// <b>It lives in this suite and not in a tidier one, and that is the lesson.</b> Written first in
/// <c>PackagingTests</c>, it covered the refusal there while the walk covered the other arm here, and
/// the file still measured 100/50 in CI: the coverage gate <b>adds</b> branches across suites rather
/// than taking «covered anywhere» — 1 of 2 plus 1 of 2 is 2 of 4. Both arms have to be taken inside
/// one suite, so the refusal belongs beside the walk that takes the other one.
/// </remarks>
public sealed class TitleFramePassTests
{
    [Fact]
    public void A_launcher_built_with_no_pass_refuses_at_once() =>
        Assert.Throws<ArgumentNullException>(() => new TitleFramePass(null!));
}
