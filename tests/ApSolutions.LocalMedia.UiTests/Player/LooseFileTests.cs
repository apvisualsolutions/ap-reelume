using System.Globalization;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The banner that says, in words, that what is playing is not in the library — and the one explicit
/// way to change that, which adds the folder and never the single file.
/// </summary>
public sealed class LooseFileTests
{
    private static readonly LooseFileSession Session = new(
        new Domain.Catalog.MediaFileId(Guid.NewGuid()),
        @"D:\somewhere\una película.mkv",
        "una película.mkv",
        @"D:\somewhere");

    [Fact]
    public void The_banner_names_the_file_and_offers_the_folder_not_the_file()
    {
        var viewModel = new LooseFileViewModel();

        Assert.False(viewModel.IsLooseSession);
        Assert.Equal(string.Empty, viewModel.DisplayName);
        Assert.False(viewModel.AddFolderCommand.CanExecute(null));

        viewModel.Apply(Session);

        Assert.True(viewModel.IsLooseSession);
        Assert.Equal("una película.mkv", viewModel.DisplayName);
        Assert.Equal(@"D:\somewhere", viewModel.FolderPath);
        Assert.True(viewModel.AddFolderCommand.CanExecute(null));
    }

    [Fact]
    public void Adding_the_folder_asks_for_confirmation_first_and_sends_the_folder()
    {
        var requested = new List<string>();
        var viewModel = new LooseFileViewModel(folder =>
        {
            requested.Add(folder);
            return Task.CompletedTask;
        });
        viewModel.Apply(Session);

        viewModel.AddFolderCommand.Execute(null);
        Assert.Empty(requested);
        Assert.True(viewModel.IsAddFolderConfirmationPending);

        viewModel.ConfirmAddFolderCommand.Execute(null);
        Assert.Equal([@"D:\somewhere"], requested);
        Assert.False(viewModel.IsAddFolderConfirmationPending);
    }

    [Fact]
    public void Declining_the_confirmation_adds_nothing_at_all()
    {
        var requested = new List<string>();
        var viewModel = new LooseFileViewModel(folder =>
        {
            requested.Add(folder);
            return Task.CompletedTask;
        });
        viewModel.Apply(Session);

        viewModel.AddFolderCommand.Execute(null);
        viewModel.CancelAddFolderCommand.Execute(null);

        Assert.Empty(requested);
        Assert.False(viewModel.IsAddFolderConfirmationPending);
        Assert.True(viewModel.IsLooseSession);
    }

    [Fact]
    public void Clearing_the_session_puts_the_banner_away()
    {
        var viewModel = new LooseFileViewModel();
        viewModel.Apply(Session);
        viewModel.AddFolderCommand.Execute(null);

        viewModel.Clear();

        Assert.False(viewModel.IsLooseSession);
        Assert.False(viewModel.IsAddFolderConfirmationPending);
        Assert.Equal(string.Empty, viewModel.DisplayName);
        Assert.Equal(string.Empty, viewModel.FolderPath);
    }

    [Fact]
    public void Confirming_without_a_session_and_a_missing_session_are_both_refused()
    {
        var viewModel = new LooseFileViewModel();

        viewModel.ConfirmAddFolderCommand.Execute(null);
        Assert.False(viewModel.IsAddFolderConfirmationPending);
        Assert.Throws<ArgumentNullException>(() => viewModel.Apply(null!));
    }

    [AvaloniaFact]
    public void Every_control_of_the_banner_is_named_and_focusable_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
            var viewModel = new LooseFileViewModel();
            viewModel.Apply(Session);
            var view = new LooseFileBanner { DataContext = viewModel };
            var window = new Window { Width = 900, Height = 300, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var buttons = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.TemplatedParent is null)
                .ToArray();
            Assert.NotEmpty(buttons);
            Assert.All(buttons, button =>
            {
                Assert.True(button.Focusable);
                Assert.False(
                    string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                    "A loose-file control has no accessible name.");
            });

            window.Close();
        }
    }

    [Fact]
    public void The_banner_shows_the_file_name_and_never_the_whole_path()
    {
        var path = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Player",
            "LooseFileBanner.axaml");
        var document = XDocument.Load(path);

        var bindings = document.Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName == "Text")
            .Select(attribute => attribute.Value)
            .ToArray();

        Assert.Contains(bindings, value => value.Contains("DisplayName", StringComparison.Ordinal));
        Assert.DoesNotContain(bindings, value => value.Contains("{Binding Path}", StringComparison.Ordinal));

        var literals = document.Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "Header")
            .Where(attribute => !attribute.Value.TrimStart().StartsWith('{'))
            .ToArray();
        Assert.Empty(literals);
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
