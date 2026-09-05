// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Library;

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// What the Library says about roots it cannot read, one root at a time.
/// </summary>
/// <remarks>
/// The strip's geometry is asserted in <see cref="LibraryNoticesPlacementTests"/>; this is the
/// bookkeeping underneath it, which has more branches than the screen shows: a root can go from gone
/// to refused without passing through available, and a notice that never clears is the same lie told
/// the other way round.
/// </remarks>
public sealed class RootNoticeViewModelTests
{
    private static readonly LibraryRootId First = new(Guid.NewGuid());
    private static readonly LibraryRootId Second = new(Guid.NewGuid());

    [Fact]
    public void A_root_that_cannot_be_read_gets_one_line_that_says_which_failure()
    {
        var notices = new RootNoticeViewModel();
        Assert.False(notices.HasNotices);
        Assert.Empty(notices.Notices);

        notices.Apply(new RootAvailabilityChanged(First, "E:\\Respaldo", RootAvailability.Unavailable));

        var gone = Assert.Single(notices.Notices);
        Assert.True(notices.HasNotices);
        Assert.Equal(First, gone.Id);
        Assert.Equal("E:\\Respaldo", gone.Path);
        Assert.False(gone.IsAccessDenied);
        Assert.Equal("LibraryNoticeRootGoneTitle", gone.TitleKey);
        Assert.Equal("LibraryNoticeRootGoneBody", gone.BodyKey);
    }

    [Fact]
    public void A_refused_root_says_something_else_entirely()
    {
        var notices = new RootNoticeViewModel();

        notices.Apply(new RootAvailabilityChanged(First, "\\\\nas\\cine", RootAvailability.AccessDenied));

        var refused = Assert.Single(notices.Notices);
        Assert.True(refused.IsAccessDenied);
        Assert.Equal("LibraryNoticeAccessDeniedTitle", refused.TitleKey);
        Assert.Equal("LibraryNoticeAccessDeniedBody", refused.BodyKey);
    }

    /// <summary>
    /// A share that comes back up and then rejects the credentials never passes through available, so
    /// the row is replaced rather than added beside itself.
    /// </summary>
    [Fact]
    public void One_root_keeps_one_line_however_its_failure_changes()
    {
        var notices = new RootNoticeViewModel();

        notices.Apply(new RootAvailabilityChanged(First, "\\\\nas\\cine", RootAvailability.Unavailable));
        notices.Apply(new RootAvailabilityChanged(First, "\\\\nas\\cine", RootAvailability.AccessDenied));

        var only = Assert.Single(notices.Notices);
        Assert.True(only.IsAccessDenied);
    }

    [Fact]
    public void Two_roots_get_two_lines_and_each_clears_on_its_own()
    {
        var notices = new RootNoticeViewModel();
        notices.Apply(new RootAvailabilityChanged(First, "E:\\Respaldo", RootAvailability.Unavailable));
        notices.Apply(new RootAvailabilityChanged(Second, "\\\\nas\\cine", RootAvailability.AccessDenied));
        Assert.Equal(2, notices.Notices.Count);

        notices.Apply(new RootAvailabilityChanged(First, "E:\\Respaldo", RootAvailability.Available));

        var left = Assert.Single(notices.Notices);
        Assert.Equal(Second, left.Id);
        Assert.True(notices.HasNotices);

        notices.Apply(new RootAvailabilityChanged(Second, "\\\\nas\\cine", RootAvailability.Available));

        Assert.Empty(notices.Notices);
        Assert.False(notices.HasNotices);
    }

    /// <summary>
    /// A root coming back that was never complained about changes nothing, which is the ordinary case
    /// on every scan of a healthy library.
    /// </summary>
    [Fact]
    public void A_root_that_was_fine_all_along_adds_nothing()
    {
        var notices = new RootNoticeViewModel();

        notices.Apply(new RootAvailabilityChanged(First, "D:\\Cine", RootAvailability.Available));

        Assert.Empty(notices.Notices);
        Assert.False(notices.HasNotices);
    }

    /// <summary>
    /// The bus is what feeds this in the running application, and an event of another kind passes
    /// through it untouched.
    /// </summary>
    [Fact]
    public async Task The_bus_feeds_it_and_ignores_what_is_not_its_business()
    {
        var bus = new InProcessApplicationEventPublisher();
        var notices = new RootNoticeViewModel(bus);

        await bus.PublishAsync(new CatalogChanged(First, 3), TestContext.Current.CancellationToken);
        Assert.Empty(notices.Notices);

        await bus.PublishAsync(
            new RootAvailabilityChanged(First, "E:\\Respaldo", RootAvailability.Unavailable),
            TestContext.Current.CancellationToken);

        Assert.Single(notices.Notices);
    }

    [Fact]
    public void It_refuses_what_it_cannot_use()
    {
        Assert.Throws<ArgumentNullException>(() => new RootNoticeViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new RootNoticeViewModel().Apply(null!));
        Assert.Throws<ArgumentNullException>(
            () => new RootNoticeRowViewModel(First, null!, RootAvailability.Unavailable));
    }

    /// <summary>
    /// The collection announces itself, because the strip binds to it and a row added in silence is a
    /// notice nobody sees.
    /// </summary>
    [Fact]
    public void The_count_announces_when_it_changes_and_stays_quiet_when_it_does_not()
    {
        var notices = new RootNoticeViewModel();
        var announcements = 0;
        notices.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RootNoticeViewModel.HasNotices))
            {
                announcements++;
            }
        };

        notices.Apply(new RootAvailabilityChanged(First, "E:\\Respaldo", RootAvailability.Unavailable));
        Assert.Equal(1, announcements);

        // The same root failing again replaces its row: the count did not move, so nothing is said.
        notices.Apply(new RootAvailabilityChanged(First, "E:\\Respaldo", RootAvailability.AccessDenied));
        Assert.Equal(1, announcements);

        notices.Apply(new RootAvailabilityChanged(First, "E:\\Respaldo", RootAvailability.Available));
        Assert.Equal(2, announcements);

        // And a root nobody complained about coming back says nothing at all.
        notices.Apply(new RootAvailabilityChanged(Second, "D:\\Cine", RootAvailability.Available));
        Assert.Equal(2, announcements);
    }
}
