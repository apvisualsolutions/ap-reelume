using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The overlay that counts down to the next episode. It announces itself politely rather than stealing
/// focus, it says how long is left, and either button ends the wait.
/// </summary>
public sealed class NextEpisodeOverlayTests
{
    [AvaloniaFact]
    public void Nothing_is_shown_until_a_next_episode_is_offered()
    {
        var view = Build(out var viewModel);
        var surface = view.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "NextEpisodeSurface");

        Assert.False(viewModel.IsVisible);
        Assert.False(surface.IsVisible);
    }

    [AvaloniaFact]
    public void The_episode_and_the_remaining_seconds_are_shown_and_updated()
    {
        var view = Build(out var viewModel);
        var lines = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => !string.IsNullOrEmpty(block.Name))
            .ToDictionary(block => block.Name!, block => block);

        viewModel.Offer("T1 E2", 10);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsVisible);
        Assert.Equal("T1 E2", lines["NextEpisodeLabel"].Text);
        Assert.Equal("10", lines["NextEpisodeCountdown"].Text);

        viewModel.Tick(3);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("3", lines["NextEpisodeCountdown"].Text);

        viewModel.Hide();
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsVisible);
    }

    [AvaloniaFact]
    public void Both_buttons_end_the_wait_and_report_what_was_chosen()
    {
        var actions = new List<NextEpisodeAction>();
        var view = Build(out var viewModel, action =>
        {
            actions.Add(action);
            return Task.CompletedTask;
        });
        var buttons = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => !string.IsNullOrEmpty(button.Name))
            .ToDictionary(button => button.Name!, button => button);

        viewModel.Offer("T1 E2", 10);
        Dispatcher.UIThread.RunJobs();
        buttons["PlayNextNowButton"].Command?.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsVisible);

        viewModel.Offer("T1 E3", 10);
        Dispatcher.UIThread.RunJobs();
        buttons["CancelNextButton"].Command?.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([NextEpisodeAction.PlayNow, NextEpisodeAction.Cancel], actions);
        Assert.False(viewModel.IsVisible);
    }

    [AvaloniaFact]
    public void The_overlay_announces_itself_politely_and_names_every_control()
    {
        var view = Build(out var viewModel);

        viewModel.Offer("T1 E2", 10);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(view));
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(view)));
        foreach (var button in view.GetVisualDescendants().OfType<Button>())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                $"{button.Name} has no automation name.");
        }
    }

    [AvaloniaFact]
    public void The_overlay_is_captured_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var captures = Path.Combine(GetRepositoryRoot(), "artifacts", "ui-captures", "T28");
        _ = Directory.CreateDirectory(captures);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var viewModel = new NextEpisodeViewModel();
            var view = new NextEpisodeOverlay { DataContext = viewModel };
            var window = new Window { Width = 480, Height = 200, Content = view };
            window.Show();
            viewModel.Offer("T1 E2", 10);
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(captures, $"next-episode-{cultureName}.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    private static NextEpisodeOverlay Build(
        out NextEpisodeViewModel viewModel,
        Func<NextEpisodeAction, Task>? onAction = null)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        viewModel = new NextEpisodeViewModel(onAction);
        var view = new NextEpisodeOverlay { DataContext = viewModel };
        var window = new Window { Width = 480, Height = 200, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
