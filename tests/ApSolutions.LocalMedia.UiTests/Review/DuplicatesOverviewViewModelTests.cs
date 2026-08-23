// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Review;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Review;

/// <summary>
/// The corners of the overview the assembled scenes never stand in: models built over nothing, a
/// load announced to nobody, and the set that changes nothing and must say nothing.
/// </summary>
public sealed class DuplicatesOverviewViewModelTests
{
    [Fact]
    public void Models_built_over_nothing_refuse_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new DuplicateGroupRowViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new DuplicatesOverviewViewModel(null!));
    }

    /// <summary>
    /// Loaded once with nobody listening and once with a listener: the announcement's null half is
    /// a branch, and the thirteen forms of the house defect say a surface nobody hears about is a
    /// surface nobody sees.
    /// </summary>
    [Fact]
    public async Task A_load_is_safe_to_announce_to_nobody_and_heard_by_a_listener()
    {
        var first = NewModel();
        await first.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(first.HasGroups);
        Assert.False(first.IsEmpty);
        Assert.Equal("Arrival", Assert.Single(first.Groups).Title);

        var second = NewModel();
        var announced = new List<string>();
        second.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);
        await second.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Contains(nameof(second.Groups), announced);
        Assert.Contains(nameof(second.HasGroups), announced);
        Assert.Contains(nameof(second.IsEmpty), announced);
    }

    /// <summary>
    /// The set that changes nothing says nothing. Every public path hands the list a fresh
    /// instance, so the unchanged side of the comparison is reached the way the composition
    /// root's private seams are: by reflection, on the member itself.
    /// </summary>
    [Fact]
    public async Task Setting_the_same_list_again_announces_nothing()
    {
        var model = NewModel();
        await model.LoadAsync(TestContext.Current.CancellationToken);

        var announced = new List<string>();
        model.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        var groups = typeof(DuplicatesOverviewViewModel)
            .GetProperty(nameof(model.Groups), BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(groups);
        groups!.SetValue(model, model.Groups);

        Assert.Empty(announced);
    }

    private static DuplicatesOverviewViewModel NewModel() => new(
        new GetDuplicateOverview(new SingleGroupReader()));

    private sealed class SingleGroupReader : IDuplicateOverviewReader
    {
        public Task<IReadOnlyList<DuplicateOverviewEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DuplicateOverviewEntry>>(
                [new DuplicateOverviewEntry(new TitleId(Guid.NewGuid()), "Arrival", 2)]);
    }
}
