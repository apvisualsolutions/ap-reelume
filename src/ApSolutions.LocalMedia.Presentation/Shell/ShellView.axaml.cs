using System.ComponentModel;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Shell;

/// <summary>
/// The shell, and the one place that owns the window the mini player lives in.
/// <para>
/// Modes move the same stage rather than build a new one: the surface that is playing is handed to
/// the mini window and handed back, so the session, its position, and its tracks survive a mode
/// change. Building a second player for the mini window is how two sessions end up running at once.
/// </para>
/// </summary>
public sealed partial class ShellView : UserControl
{
    private readonly PlayerWindowCoordinator _windowCoordinator = new();
    private MiniPlayerWindow? _miniWindow;
    private ShellViewModel? _viewModel;

    public ShellView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelChanged;
        }

        _viewModel = DataContext as ShellViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelChanged;
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ShellViewModel.PlaybackMode) && _viewModel is not null)
        {
            ApplyPlaybackMode(_viewModel.PlaybackMode);
        }
    }

    private void ApplyPlaybackMode(PlaybackMode mode)
    {
        if (this.FindControl<Panel>("PlayerStage") is not { } stage
            || this.FindControl<ContentControl>("PlayerHost") is not { } host)
        {
            return;
        }

        if (mode == PlaybackMode.Mini)
        {
            var window = _miniWindow ??= new MiniPlayerWindow();
            var screen = window.Screens.Primary?.Bounds ?? new PixelRect(0, 0, 1920, 1080);
            _windowCoordinator.Apply(window, stage, PlaybackMode.Mini, screen, window.RenderScaling);
            window.Show();
            return;
        }

        // Back inside the shell: the stage returns to its host and the mini window closes rather than
        // lingering empty behind the main window.
        if (_miniWindow is { } mini)
        {
            mini.Content = null;
            mini.Close();
            _miniWindow = null;
        }

        host.Content = stage;
    }
}
