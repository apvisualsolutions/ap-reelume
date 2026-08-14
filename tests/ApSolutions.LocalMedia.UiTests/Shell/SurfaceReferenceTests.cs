// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The reachability gate reads what a file says, not what it once said (ARQ-013).
/// </summary>
/// <remarks>
/// A commented-out reference used to count as a reference, and that is the worst way for this gate
/// to fail: the orphan surface it exists to catch hides behind <c>&lt;!-- --&gt;</c> while the suite
/// stays green and reports the surface as reachable. Measured on 2026-08-14 before the fix: no
/// surface in the presentation project was hidden that way, so the blindness was real and was not
/// covering anything yet.
/// <para>
/// What the stripping can get wrong only goes one way. Trimming too much loses a reference and
/// produces an orphan, which fails loudly; it can never invent reachability. That is why the line
/// form is guarded against <c>://</c> and nothing further is attempted.
/// </para>
/// </remarks>
public sealed class SurfaceReferenceTests
{
    [Fact]
    public void A_commented_out_element_is_not_a_reference_in_markup()
    {
        const string markup = """
            <UserControl xmlns:views="clr-namespace:Views">
              <!-- <views:ReviewInboxView /> -->
            </UserControl>
            """;

        Assert.False(SurfaceReferences.InMarkup(markup, "ReviewInboxView"));
    }

    [Fact]
    public void A_reference_commented_out_across_several_lines_is_not_a_reference()
    {
        const string markup = """
            <UserControl xmlns:views="clr-namespace:Views">
              <!--
                Parked until the inbox lands:
                <views:ReviewInboxView />
              -->
            </UserControl>
            """;

        Assert.False(SurfaceReferences.InMarkup(markup, "ReviewInboxView"));
    }

    [Fact]
    public void A_commented_out_type_is_not_a_reference_in_code()
    {
        const string lineComment = "public sealed class Host { } // was: new ReviewInboxView()";
        const string blockComment = """
            public sealed class Host
            {
                /* var inbox = new ReviewInboxView(); */
            }
            """;

        Assert.False(SurfaceReferences.InCode(lineComment, "ReviewInboxView"));
        Assert.False(SurfaceReferences.InCode(blockComment, "ReviewInboxView"));
    }

    [Fact]
    public void A_real_reference_is_still_read_as_one()
    {
        const string markup = """
            <UserControl xmlns:views="clr-namespace:Views">
              <views:ReviewInboxView />
            </UserControl>
            """;
        const string code = "public sealed class Host { private readonly ReviewInboxView _inbox = new(); }";

        Assert.True(SurfaceReferences.InMarkup(markup, "ReviewInboxView"));
        Assert.True(SurfaceReferences.InCode(code, "ReviewInboxView"));
    }

    /// <summary>
    /// The two slashes of a scheme are not a comment. Without the guard, a line holding a URL would
    /// lose everything after it, and a reference sharing that line would be read as absent.
    /// </summary>
    [Fact]
    public void A_scheme_inside_a_string_does_not_start_a_comment()
    {
        const string code = """
            const string Docs = "https://example.invalid/inbox"; ReviewInboxView surface = new();
            """;

        Assert.True(SurfaceReferences.InCode(code, "ReviewInboxView"));
    }
}
