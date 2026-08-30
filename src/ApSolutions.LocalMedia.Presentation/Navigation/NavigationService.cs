// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Navigation;

public enum AppRoute
{
    Home,
    Library,

    /// <summary>
    /// Folders of numbered videos studied in order (CRS-003). It sits between the library and the
    /// review because that is where the prototype's rail puts it, and because a course is a kind of
    /// title rather than a kind of chore.
    /// </summary>
    Courses,
    Review,

    /// <summary>The prototype's fifth destination. Copias moved into Settings on 2026-08-23.</summary>
    Duplicates,
    Settings,
}

public interface INavigationService
{
    AppRoute CurrentRoute { get; }

    event EventHandler<AppRoute>? Navigated;

    void Navigate(AppRoute route);
}

public sealed class NavigationService : INavigationService
{
    public AppRoute CurrentRoute { get; private set; } = AppRoute.Home;

    public event EventHandler<AppRoute>? Navigated;

    public void Navigate(AppRoute route)
    {
        if (!Enum.IsDefined(route))
        {
            throw new ArgumentOutOfRangeException(nameof(route));
        }

        CurrentRoute = route;
        Navigated?.Invoke(this, route);
    }
}
