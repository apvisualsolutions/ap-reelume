using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// A lambda handed to Dispatcher.Post is an async void in disguise: an exception that escapes it is
/// rethrown on the interface thread and takes the application down. The deep audit found three of
/// them at the entry points a machine controls — startup's automatic check, tray exit, and loose
/// file activation (BUG-005).
/// </summary>
public sealed class DispatcherWiringTests
{
    [Fact]
    public void No_dispatcher_post_carries_a_bare_async_lambda()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs");
        Assert.True(File.Exists(path), "CompositionRoot.cs was not found where the assembly keeps it.");

        Assert.DoesNotContain("Post(async", File.ReadAllText(path), StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
