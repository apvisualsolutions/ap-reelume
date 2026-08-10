// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

public sealed class LibraryRootTests
{
    [Fact]
    public void Catalog_identifiers_are_immutable_stable_value_types()
    {
        AssertStableValue(value => new TitleId(value));
        AssertStableValue(value => new MediaFileId(value));
        AssertStableValue(value => new MediaVersionId(value));
        AssertStableValue(value => new LibraryRootId(value));
        AssertStableValue(value => new SeriesId(value));
        AssertStableValue(value => new EpisodeId(value));
    }

    [Fact]
    public void Library_root_tracks_normalized_path_kind_policy_and_availability()
    {
        var id = new LibraryRootId(Guid.NewGuid());
        var scanPolicy = ScanPolicy.Startup | ScanPolicy.Manual;
        var root = new LibraryRoot(id, @"C:\Media", RootKind.Local, RootAvailability.Available, scanPolicy);

        Assert.Equal(id, root.Id);
        Assert.Equal(@"C:\Media", root.Path);
        Assert.Equal(RootKind.Local, root.Kind);
        Assert.Equal(RootAvailability.Available, root.Availability);
        Assert.Equal(scanPolicy, root.ScanPolicy);
    }

    [Fact]
    public void Availability_changes_preserve_root_identity_path_kind_and_policy()
    {
        var id = new LibraryRootId(Guid.NewGuid());
        var root = new LibraryRoot(
            id,
            @"\\server\share",
            RootKind.Unc,
            RootAvailability.Available,
            ScanPolicy.Manual);

        var updated = root.WithAvailability(RootAvailability.Unavailable);

        Assert.Equal(id, updated.Id);
        Assert.Equal(@"\\server\share", updated.Path);
        Assert.Equal(RootKind.Unc, updated.Kind);
        Assert.Equal(ScanPolicy.Manual, updated.ScanPolicy);
        Assert.Equal(RootAvailability.Unavailable, updated.Availability);
    }

    private static void AssertStableValue<T>(Func<Guid, T> create)
        where T : struct
    {
        var value = Guid.NewGuid();
        Assert.Equal(create(value), create(value));
        Assert.NotEqual(create(value), create(Guid.NewGuid()));
    }
}
