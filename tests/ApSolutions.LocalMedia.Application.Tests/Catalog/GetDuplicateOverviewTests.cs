// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Catalog;

/// <summary>
/// The use case is a door to the reader and nothing else, so what is asked is the door frame: built
/// over no reader it must say so at construction, and built over one it hands back exactly what the
/// reader said. Both halves live in this one suite on purpose - the merge keeps the best single
/// measurement of a condition, so sides split across suites never add up.
/// </summary>
public sealed class GetDuplicateOverviewTests
{
    [Fact]
    public void A_use_case_over_nothing_refuses_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new GetDuplicateOverview(null!));
    }

    [Fact]
    public async Task What_the_reader_lists_is_what_the_use_case_answers()
    {
        var entry = new DuplicateOverviewEntry(new TitleId(Guid.NewGuid()), "Arrival", 2);

        var entries = await new GetDuplicateOverview(new FixedReader(entry))
            .ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Same(entry, Assert.Single(entries));
    }

    private sealed class FixedReader(DuplicateOverviewEntry entry) : IDuplicateOverviewReader
    {
        public Task<IReadOnlyList<DuplicateOverviewEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DuplicateOverviewEntry>>([entry]);
    }
}
