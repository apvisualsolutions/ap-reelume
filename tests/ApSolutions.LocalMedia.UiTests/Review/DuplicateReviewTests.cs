using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Review;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Review;

public sealed class DuplicateReviewTests
{
    [AvaloniaFact]
    public async Task Panel_lists_every_file_with_short_path_quality_availability_and_no_destructive_action()
    {
        var group = new MediaVersionGroup(
            new MediaVersionId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
            "tv:show:s05e10",
            [
                Version(1, "C:\\Private\\Shows\\Season 5\\Show.5x10.1080p.mkv", true, 1920, 1080, false, "H264"),
                Version(2, "Z:\\Shows\\Season 5\\Show.5x10.2160p.HDR.mkv", false, 3840, 2160, true, "HEVC"),
            ],
            PreferredMediaFileId: null);
        var viewModel = new DuplicateReviewViewModel(
            group,
            new MediaVersionSelectionPolicy(),
            new SetPreferredVersion(new ReadOnlyVersionRepository(group)),
            new MediaVersionPreferences(PreferHdr: true));
        Assert.Equal(2, viewModel.Items.Count);
        Assert.All(viewModel.Items, item => Assert.StartsWith("…\\", item.ShortPath, StringComparison.Ordinal));
        Assert.DoesNotContain(viewModel.Items, item => item.ShortPath.Contains("Private", StringComparison.Ordinal));
        Assert.Contains(viewModel.Items, item => item.Quality.Contains("3840×2160", StringComparison.Ordinal));
        viewModel.SetPreferredCommand.Execute(viewModel.Items[1]);
        await WaitForAsync(() => viewModel.Items[1].IsStoredPreferred);
        Assert.True(viewModel.Items[0].IsEffective);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
            var view = new DuplicateReviewView { DataContext = viewModel };
            var window = new Window { Width = 900, Height = 600, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
            Assert.All(buttons, button =>
            {
                Assert.DoesNotContain("delete", button.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("hide", button.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("remove", button.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            });
            Assert.Equal(2, view.GetVisualDescendants().OfType<RadioButton>().Count());

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var artifactPath = Path.Combine(
                GetRepositoryRoot(),
                "artifacts",
                "ui-captures",
                "T15",
                $"duplicates-{cultureName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            frame.Save(artifactPath, PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    private static MediaVersion Version(
        int seed,
        string path,
        bool available,
        int width,
        int height,
        bool hdr,
        string codec)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new MediaVersion(
            new MediaFileId(new Guid(bytes)),
            path,
            available,
            TimeSpan.FromMinutes(50),
            width,
            height,
            hdr,
            codec,
            seed * 1000L);
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

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Fail("The preferred-version action did not complete.");
    }

    private sealed class ReadOnlyVersionRepository(MediaVersionGroup group) : IMediaVersionGroupRepository
    {
        public Task<MediaVersionGroup?> FindByContentKeyAsync(string contentKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(group.ContentKey == contentKey ? group : null);

        public Task<MediaVersionGroup?> FindByIdAsync(MediaVersionId groupId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(group.Id == groupId ? group : null);

        public Task<MediaVersionGroup?> FindByMemberAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MediaVersionGroup?>(
                group.Versions.Any(version => version.MediaFileId == mediaFileId) ? group : null);

        public Task SaveAsync(MediaVersionGroup updated, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
