// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Courses;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.TestSupport;

using Avalonia.Headless.XUnit;

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// Every destination takes the stage from the welcome card, and none of them shares it.
/// </summary>
/// <remarks>
/// <b>Written on 2026-09-05, after Courses spent its whole life drawn on top of the welcome card.</b>
/// The screen was unreadable: «Cursos» and «Tu biblioteca, en tu PC» on the same line, and both
/// descriptions over each other. It was found by photographing the real application beside the
/// prototype — the first pair nobody had ever looked at.
/// <para>
/// <b>No gate could have seen it.</b> The overflow gates mount each view ON ITS OWN, which is what
/// makes them see every branch at once; two surfaces drawn over one another is precisely what that
/// cannot show. And a test did exist —
/// <c>ShellAssemblyTests.The_review_route_shows_the_inbox_instead_of_the_welcome_card</c> — written
/// for ONE route and never generalised. Courses arrived afterwards.
/// </para>
/// <para>
/// So this is a closed table over the enum rather than another test per route: a destination that is
/// not named here fails, and a destination the welcome card does not stand down for fails. One test
/// per route is exactly the shape that let this through.
/// </para>
/// </remarks>
public sealed class WelcomeCardRouteTests
{
    /// <summary>
    /// Every route, and the property the shell exposes for the surface that route shows. Adding a
    /// member to <see cref="AppRoute"/> without adding it here fails
    /// <see cref="Every_route_is_named_in_this_table"/>.
    /// </summary>
    private static readonly Dictionary<AppRoute, string> SurfaceOf = new()
    {
        [AppRoute.Home] = "IsHomeVisible",
        [AppRoute.Library] = "IsLibraryVisible",
        [AppRoute.Courses] = "IsCoursesVisible",
        [AppRoute.Review] = "IsReviewVisible",
        [AppRoute.Duplicates] = "IsDuplicatesVisible",
        [AppRoute.Settings] = "IsSettingsVisible",
    };

    [Fact]
    public void Every_route_is_named_in_this_table()
    {
        var missing = Enum.GetValues<AppRoute>()
            .Where(route => !SurfaceOf.ContainsKey(route))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "These destinations have no surface named in this table, so nothing says whether the "
                + "welcome card stands down for them: " + string.Join(", ", missing));
    }

    /// <summary>
    /// The welcome card stands down for every one of them, read from the property itself.
    /// </summary>
    /// <remarks>
    /// Read from the source rather than by navigating with every surface built, and the reason is
    /// what the defect was: a surface this test could not construct would report "not visible", the
    /// welcome card would correctly stay up, and the assertion would pass while saying nothing. The
    /// question is whether the property CONSIDERS the route at all.
    /// </remarks>
    [Fact]
    public void The_welcome_card_stands_down_for_every_destination()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Shell",
            "ShellViewModel.cs"));

        var match = Regex.Match(
            source,
            @"public bool IsPrimaryContentVisible =>(?<body>.*?);",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));
        Assert.True(match.Success, "IsPrimaryContentVisible was not found; the shell moved.");
        var body = match.Groups["body"].Value;

        var ignored = SurfaceOf
            .Where(pair => !body.Contains("!" + pair.Value, StringComparison.Ordinal))
            .Select(pair => $"{pair.Key} ({pair.Value})")
            .ToArray();

        Assert.True(
            ignored.Length == 0,
            "The welcome card is drawn over these destinations, so their own screen appears "
                + "underneath it: " + string.Join(", ", ignored));
    }

    /// <summary>
    /// And the behaviour itself, for the route it actually happened to.
    /// </summary>
    /// <remarks>
    /// The reading above says the property considers Courses; this says the answer is the right way
    /// round. Both, because one is blind to a sign flipped and the other is blind to a route nobody
    /// could build.
    /// </remarks>
    [AvaloniaFact]
    public void The_courses_route_shows_its_grid_instead_of_the_welcome_card()
    {
        var navigation = new NavigationService();
        var shell = new ShellViewModel(
            navigation,
            new ShellSurfaces { Courses = new CoursesViewModel(new GetCourses(new StubCourses(), new NoLessons())) });

        Assert.True(shell.IsPrimaryContentVisible, "Home has no surface here, so the card is what shows.");

        navigation.Navigate(AppRoute.Courses);

        Assert.True(shell.IsCoursesVisible);
        Assert.False(
            shell.IsPrimaryContentVisible,
            "the welcome card is still up on the Courses route, so its title and description are "
                + "drawn on top of the grid's own.");
    }

    /// <summary>No lessons, because what is under test is the route and not the grid.</summary>
    private sealed class NoLessons : ICourseLessonReader
    {
        public Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CourseLessonProgress>>([]);

        public Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>>(
                new Dictionary<CourseId, IReadOnlyList<CourseLessonProgress>>());
    }
}
