// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
                Version(1, @"C:\\Private\\Shows\\Season 5\\Show.5x10.1080p.mkv", true, 1920, 1080, false, "H264"),
                Version(2, @"Z:\\Shows\\Season 5\\Show.5x10.2160p.HDR.mkv", false, 3840, 2160, true, "HEVC"),
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
                RepositoryLayout.Root,
                "artifacts",
                "ui-captures",
                "T15",
                $"duplicates-{cultureName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            frame.Save(artifactPath, PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    /// <summary>
    /// The versions sit side by side, and a third wraps under the first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// §4 asks for a two-column comparison and says why: with the rows stacked one under another, two
    /// files are compared by scrolling between them. Two columns put them side by side, and a
    /// <c>UniformGrid</c> of two wraps a third under the first without anybody choosing where.
    /// </para>
    /// <para>
    /// It does not virtualize, and that is fine here and nowhere else: a version group is the copies
    /// of one title, and <c>GroupMediaVersions</c> throws under two — this list is two or three long,
    /// not ten thousand.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_versions_sit_side_by_side_and_a_third_wraps_under_the_first()
    {
        var group = new MediaVersionGroup(
            new MediaVersionId(Guid.Parse("50000000-0000-0000-0000-000000000009")),
            "movie:603",
            [
                Version(11, @"C:\Films\Matrix.1080p.mkv", true, 1920, 1080, false, "H264"),
                Version(12, @"Z:\Films\Matrix.2160p.HDR.mkv", true, 3840, 2160, true, "HEVC"),
                Version(13, @"Z:\Films\Matrix.720p.mkv", true, 1280, 720, false, "H264"),
            ],
            PreferredMediaFileId: null);
        var (window, view) = ShowDuplicates(group);

        var grid = Assert.Single(view.GetVisualDescendants().OfType<UniformGrid>());
        Assert.Equal(2, grid.Columns);

        var rows = grid.GetVisualChildren()
            .OfType<Control>()
            .Select(child => Math.Round(child.Bounds.Y, 0))
            .Distinct()
            .ToArray();
        Assert.Equal(2, rows.Length);

        window.Close();
    }

    /// <summary>
    /// The figures that tell one copy from another are fixed width, and every card has a visible edge.
    /// </summary>
    /// <remarks>
    /// "3840×2160 HDR HEVC" under "1920×1080 H264" only reads as a comparison when the characters line
    /// up. And the cards carried <c>BorderThickness="1"</c> with <b>no brush at all</b>, so the border
    /// that separates one file's facts from the next was whatever the base theme happened to give.
    /// </remarks>
    [AvaloniaFact]
    public void The_quality_figures_are_fixed_width_and_every_card_has_an_edge()
    {
        var group = new MediaVersionGroup(
            new MediaVersionId(Guid.Parse("50000000-0000-0000-0000-00000000000a")),
            "movie:604",
            [
                Version(21, @"C:\Films\Arrival.1080p.mkv", true, 1920, 1080, false, "H264"),
                Version(22, @"Z:\Films\Arrival.2160p.HDR.mkv", true, 3840, 2160, true, "HEVC"),
            ],
            PreferredMediaFileId: null);
        var (window, view) = ShowDuplicates(group);
        var mono = Assert.IsType<Avalonia.Media.FontFamily>(DuplicateResource("FontFamilyMono"));

        var figures = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => (block.Text ?? string.Empty).Contains('×', StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, figures.Length);
        Assert.All(figures, block => Assert.Equal(mono.Name, block.FontFamily.Name));

        var cards = view.GetVisualDescendants()
            .OfType<Border>()
            .Where(border => border.BorderThickness.Top > 0)
            .ToArray();
        Assert.NotEmpty(cards);
        Assert.All(cards, border => Assert.True(
            border.BorderBrush is not null,
            "a card declares a border thickness and no brush, so its edge is whatever the base theme gives."));

        window.Close();
    }

    /// <summary>The duplicates are a section of the review page, whose level one is the inbox's.</summary>
    [AvaloniaFact]
    public void The_duplicates_title_as_a_section_of_the_review_page()
    {
        var group = new MediaVersionGroup(
            new MediaVersionId(Guid.Parse("50000000-0000-0000-0000-00000000000b")),
            "movie:605",
            [
                Version(31, @"C:\Films\Dune.1080p.mkv", true, 1920, 1080, false, "H264"),
                Version(32, @"Z:\Films\Dune.2160p.mkv", true, 3840, 2160, false, "HEVC"),
            ],
            PreferredMediaFileId: null);
        var (window, view) = ShowDuplicates(group);

        var heading = Assert.Single(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => (int)Avalonia.Automation.AutomationProperties.GetHeadingLevel(block) > 0);
        Assert.Equal(2, (int)Avalonia.Automation.AutomationProperties.GetHeadingLevel(heading));
        Assert.Equal(Assert.IsType<double>(DuplicateResource("FontSizeSubtitle")), heading.FontSize);

        window.Close();
    }

    private static (Window Window, DuplicateReviewView View) ShowDuplicates(MediaVersionGroup group)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var view = new DuplicateReviewView
        {
            DataContext = new DuplicateReviewViewModel(
                group,
                new MediaVersionSelectionPolicy(),
                new SetPreferredVersion(new ReadOnlyVersionRepository(group)),
                new MediaVersionPreferences(PreferHdr: true)),
        };
        var window = new Window { Width = 1000, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static object DuplicateResource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        Assert.NotNull(value);
        return value!;
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
