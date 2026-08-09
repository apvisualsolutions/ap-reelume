namespace ApSolutions.LocalMedia.Presentation.Navigation;

public enum AppRoute
{
    Home,
    Library,
    Review,
    Backups,
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
