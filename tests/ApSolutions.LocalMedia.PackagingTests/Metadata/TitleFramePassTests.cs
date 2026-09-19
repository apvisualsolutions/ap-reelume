// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Windows.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Metadata;

/// <summary>
/// The refusal of the frame pass's launcher (LIB-021), which is the one arm the assembled walk
/// cannot take: there the composition root always hands it the pass it wraps.
/// </summary>
/// <remarks>
/// Measured on 2026-09-19: with the walk alone the file read 100/50 in CI and the gate for new files
/// refused it, because half of the branches is this guard. What it protects is worth the line — a
/// launcher built with nothing would answer every request by doing nothing at all, and the grid
/// would simply never draw a frame, with no failure anywhere to say why.
/// </remarks>
public sealed class TitleFramePassTests
{
    [Fact]
    public void A_launcher_built_with_no_pass_refuses_at_once() =>
        Assert.Throws<ArgumentNullException>(() => new TitleFramePass(null!));
}
