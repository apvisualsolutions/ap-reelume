// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Presentation.Updates;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.Windows;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// Assertions about the composition made against the registered descriptors rather than the source
/// file's text (the start of ARQ-006). A textual assertion is satisfied by a comment, a dead
/// registration, or a coincidence of characters; a descriptor is only satisfied by the collection
/// the application actually builds from.
/// </summary>
public sealed class CompositionDescriptorTests
{
    /// <summary>
    /// Identification has its provider, its candidate source, and — since 2026-08-28 — its artwork.
    /// </summary>
    /// <remarks>
    /// This assertion was the opposite one until that date, and ART-A01 (2026-08-09) is what it
    /// recorded: nothing fetched remote artwork and no surface showed it, so the registration left
    /// rather than promise a behaviour that did not exist. Both halves exist now, and both are asked
    /// for here — the store the identification fetches through, and the use case that decides
    /// whether there is anything to fetch. A store registered with nothing resolving it would be the
    /// same defect wearing the opposite sign.
    /// </remarks>
    [Fact]
    public void Identification_has_its_provider_its_candidate_source_and_its_artwork()
    {
        var services = Compose();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMetadataProvider));
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IIdentificationCandidateSource));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IArtworkStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(CacheTitleArtwork));

        // Resolved and not merely registered, which is the whole of this repository's characteristic
        // defect: the use case is built from the store, so resolving it proves the store is
        // reachable rather than only declared. ApplyIdentification is deliberately not resolved
        // here — it would open the database, and what this suite is about is the collection.
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<CacheTitleArtwork>());
    }

    /// <summary>
    /// Moved from SqliteIsolationTests, which asserted the registration's source text. The claim is
    /// that the migration runner's constructor is chosen explicitly through a factory, never left
    /// to the container's own constructor selection.
    /// </summary>
    [Fact]
    public void The_migration_runner_is_registered_through_a_factory_that_picks_its_constructor()
    {
        var descriptor = Assert.Single(
            Compose(),
            candidate => candidate.ServiceType == typeof(MigrationRunner));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

    /// <summary>
    /// Moved from SurfaceReachabilityTests, which looked for the coordinator's name in the source.
    /// Sessions start through the single-session coordinator: one concrete singleton, with the
    /// contract resolving to it through a factory rather than a second instance. The card actually
    /// opening a session through it is walked by the assembled physical walk with real decoding.
    /// </summary>
    [Fact]
    public void Playback_sessions_go_through_one_coordinator_registered_once()
    {
        var services = Compose();

        var concrete = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(PlaybackSessionCoordinator));
        Assert.Equal(ServiceLifetime.Singleton, concrete.Lifetime);

        var contract = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(IPlaybackSessionCoordinator));
        Assert.Equal(ServiceLifetime.Singleton, contract.Lifetime);
        Assert.NotNull(contract.ImplementationFactory);
    }

    /// <summary>
    /// Moved from SurfaceReachabilityTests. The automatic update check runs on the surface the
    /// shell shows, so the view model must be one instance, not one per resolution — a transient
    /// would check for updates on a view model nobody can see, which is indistinguishable from not
    /// checking at all.
    /// </summary>
    [Fact]
    public void The_update_surface_is_one_instance_shared_with_the_shell()
    {
        var descriptor = Assert.Single(
            Compose(),
            candidate => candidate.ServiceType == typeof(UpdateViewModel));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

    /// <summary>
    /// Moved from UpdateSourceTests, which read the address constants out of the composition
    /// root's text with a pattern. This asserts against the provider the application actually
    /// builds: the address it will ask is the address both changelogs publish for people.
    /// </summary>
    [Fact]
    public void The_update_source_the_application_builds_looks_where_the_changelogs_publish()
    {
        using var provider = ComposeAsTheRunThatConnects().BuildServiceProvider();
        var source = Assert.IsType<GitHubReleaseUpdateProvider>(
            provider.GetRequiredService<IUpdateSource>());

        foreach (var changelog in new[] { "docs/CHANGELOG.es.md", "docs/CHANGELOG.en.md" })
        {
            var (owner, name) = PublishedReleaseAddress(changelog);
            Assert.Equal(owner, source.RepositoryOwner);
            Assert.Equal(name, source.RepositoryName);
        }
    }

    private static (string Owner, string Name) PublishedReleaseAddress(string changelog)
    {
        var path = Path.Combine(RepositoryLayout.Root, changelog);
        Assert.True(File.Exists(path), $"{changelog} is missing.");
        var match = Regex.Match(
            File.ReadAllText(path),
            @"https://github\.com/(?<owner>[A-Za-z0-9._-]+)/(?<name>[A-Za-z0-9._-]+)/releases/",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        Assert.True(match.Success, $"{changelog} publishes no release address for anybody to follow.");
        return (match.Groups["owner"].Value, match.Groups["name"].Value);
    }

    private static IServiceCollection Compose()
    {
        var dataRoot = Path.Combine(
            Path.GetTempPath(),
            "APSolutions.LocalMedia.Tests",
            $"composition-{Guid.NewGuid():N}");
        return new ServiceCollection().AddLocalMedia(
            new AppDataPaths(dataRoot),
            new CompositionRoot.ShellHost());
    }

    /// <summary>
    /// The composition as the person whose profile this is gets it, which is the only one that has
    /// an update provider to ask about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other test here composes against a root of its own, and since the isolation rule
    /// reached the updater that root is exactly what decides <em>not</em> to build the provider this
    /// one is about — so the assertion above went blind rather than false, which is the worse of the
    /// two. The rule is now: a test about what the application connects to has to compose as the run
    /// that connects.
    /// </para>
    /// <para>
    /// Nothing is written anywhere. The default paths are composed, not created, and the only thing
    /// resolved is a source that holds an address — asking it anything is what would open a
    /// connection, and nothing here asks.
    /// </para>
    /// </remarks>
    private static IServiceCollection ComposeAsTheRunThatConnects() =>
        new ServiceCollection().AddLocalMedia(new AppDataPaths(), new CompositionRoot.ShellHost());
}
