// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The four lists of the player's 320 px column: rows of 36, never a sideways scroll, and text that
/// truncates into a tooltip rather than running off the edge.
/// </summary>
/// <remarks>
/// <para>
/// That is what §4 asks. What measuring asked first was <b>what those rows say</b>, and the answer on
/// 2026-08-21 was the compiler's: neither marker list declared an <c>ItemTemplate</c>, so each row
/// painted the record's generated <c>ToString()</c> — <c>IntroMarker { Id = …, SeriesId = SeriesId
/// { Value = … }, Kind = Intro, … }</c> — two GUIDs and a type name, in a column 320 wide, clipped
/// with no ellipsis and no tooltip. <b>Fixing the height without fixing the label would have
/// formalised that</b>: a 36 px row truncating a GUID into a tooltip showing the same GUID.
/// </para>
/// <para>
/// The same absence reached the kind selector, which painted <c>Intro</c>, <c>Recap</c> and
/// <c>Credits</c> — the enum's own names, untranslated, in Spanish — because no key existed for the
/// three values of <c>MarkerKind</c>.
/// </para>
/// <para>
/// And it reached the detections. <c>UserCorrected</c> is what accepting or correcting a detection
/// writes, and it is what protects that range from the next detector run; <b>nothing painted it</b>,
/// so pressing "accept" changed the model and left the list looking identical. <c>Confidence</c>
/// stays unpainted, and that is a decision rather than the same oversight: it is the detector arguing
/// with itself, and a percentage on the row invites a person to argue back about a number they cannot
/// act on.
/// </para>
/// </remarks>
public sealed class SideListRowTests
{
    /// <summary>The height §4 gives a row in the side column.</summary>
    private const double RowHeight = 36;

    private static readonly SeriesId Series = new(Guid.Parse("d1f70001-0000-4000-8000-000000000001"));

    private static readonly MediaFileId FileId = new(Guid.Parse("c2d40001-0000-4000-8000-00000000000a"));

    /// <summary>
    /// The two marker lists say what the range is, in words, and never the record that holds it.
    /// </summary>
    /// <remarks>
    /// The type name and the GUID are asserted <b>absent</b> rather than the sentence asserted
    /// present, because a template that painted both would satisfy "contains the kind" while still
    /// showing the dump. Both halves are here: the words that must be there and the debris that must
    /// not.
    /// </remarks>
    [AvaloniaFact]
    public void Neither_marker_list_paints_the_record_that_holds_the_range()
    {
        using var scope = Mount();

        var markerRow = Assert.Single(RowTexts(scope.Markers, "MarkerList"));
        Assert.Contains(Resource("MarkerKindIntro"), markerRow, StringComparison.Ordinal);
        Assert.Contains("0:30", markerRow, StringComparison.Ordinal);
        Assert.Contains("2:00", markerRow, StringComparison.Ordinal);
        Assert.DoesNotContain("IntroMarker", markerRow, StringComparison.Ordinal);
        Assert.DoesNotContain("d1f70001", markerRow, StringComparison.Ordinal);

        var detectionRows = RowTexts(scope.Detections, "DetectedMarkerList");
        Assert.Equal(2, detectionRows.Length);
        Assert.All(detectionRows, row =>
        {
            Assert.DoesNotContain("DetectedMarker", row, StringComparison.Ordinal);
            Assert.DoesNotContain("c2d40001", row, StringComparison.Ordinal);
        });
        Assert.Contains(Resource("MarkerKindIntro"), detectionRows[0], StringComparison.Ordinal);
        Assert.Contains(Resource("MarkerKindCredits"), detectionRows[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A detection somebody accepted says so, because accepting it is what protects it.
    /// </summary>
    /// <remarks>
    /// The unconfirmed row is asserted <b>not</b> to carry the suffix, which is the half that makes
    /// this about the flag: a template appending it unconditionally would pass on the confirmed row
    /// alone.
    /// </remarks>
    [AvaloniaFact]
    public void A_confirmed_detection_is_the_one_that_says_it_is_confirmed()
    {
        using var scope = Mount();
        var rows = RowTexts(scope.Detections, "DetectedMarkerList");
        var suffix = Resource("DetectedMarkerReviewConfirmed");

        Assert.DoesNotContain(suffix, rows[0], StringComparison.Ordinal);
        Assert.Contains(suffix, rows[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The kind selector offers words in the chosen language, not the names of the enum's members.
    /// </summary>
    /// <remarks>
    /// Asserted in both languages and required to differ: three labels that survived translation by
    /// not being translated are exactly what was there before, and one language alone cannot tell.
    /// </remarks>
    [AvaloniaFact]
    public void The_kind_selector_offers_words_and_they_differ_between_languages()
    {
        var byLanguage = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var view = new MarkerEditorView { DataContext = new MarkerEditorViewModel() };
            var window = new Window { Width = 320, Height = 900, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var selector = Assert.Single(
                view.GetVisualDescendants().OfType<ComboBox>(),
                box => box.Name == "MarkerKindSelector");
            byLanguage[cultureName] = selector.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty)
                .Where(text => text.Length > 0)
                .ToArray();
            window.Close();
        }

        foreach (var enumName in Enum.GetNames<MarkerKind>())
        {
            Assert.DoesNotContain(enumName, byLanguage["es-ES"], StringComparer.Ordinal);
        }

        Assert.NotEmpty(byLanguage["es-ES"]);
        Assert.NotEqual(byLanguage["es-ES"], byLanguage["en-US"], StringComparer.Ordinal);
    }

    /// <summary>
    /// Every row in the four is 36 tall, truncates, and hands the whole text to a tooltip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The height is read off the container and not off the template, because the row is the
    /// container: measured on 2026-08-21 the marker rows came out at <b>44</b> with
    /// <c>MinHeight 0</c>, which is the <c>ListBoxItem</c>'s padding plus a line of text — the same
    /// shape as the progress bar that read 4 where 3 was asked, so <c>Height</c> alone does not do it.
    /// </para>
    /// <para>
    /// The tooltip is asserted to <b>equal the text</b>. A tooltip carrying something else would
    /// leave a truncated row with no way to read the rest, which is the only reason to truncate at
    /// all.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Every_row_of_the_four_is_thirty_six_tall_and_truncates_into_its_tooltip()
    {
        using var scope = Mount();

        foreach (var (view, listName) in new (Control View, string Name)[]
        {
            (scope.Markers, "MarkerList"),
            (scope.Detections, "DetectedMarkerList"),
        })
        {
            var list = Assert.Single(
                view.GetVisualDescendants().OfType<ListBox>(),
                box => box.Name == listName);
            Assert.Equal(
                ScrollBarVisibility.Disabled,
                ScrollViewer.GetHorizontalScrollBarVisibility(list));

            var rows = list.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
            Assert.NotEmpty(rows);
            foreach (var row in rows)
            {
                Assert.Equal(RowHeight, row.Bounds.Height);
                AssertTruncatesIntoTooltip(row, listName);
            }
        }

        // The versions list is an ItemsControl and its row is the template's own root.
        var versionRows = scope.Versions.GetVisualDescendants()
            .OfType<Grid>()
            .Where(grid => grid.ColumnDefinitions.Count == 2)
            .ToArray();
        Assert.NotEmpty(versionRows);
        foreach (var row in versionRows)
        {
            Assert.Equal(RowHeight, row.Bounds.Height);
            AssertTruncatesIntoTooltip(row, "PlayerVersions");
        }

        // The track selector's rows are its options, and both pickers carry the same template. A
        // template is what DisplayMemberBinding cannot be: the two are exclusive, so the old one has
        // to be gone rather than merely overridden.
        var pickers = scope.Tracks.GetVisualDescendants().OfType<ComboBox>().ToArray();
        Assert.Equal(2, pickers.Length);
        Assert.All(pickers, box =>
        {
            Assert.Null(box.DisplayMemberBinding);
            Assert.NotNull(box.ItemTemplate);
            Assert.Contains("side-list", box.Classes);
        });
    }

    /// <summary>
    /// The option of a picker wearing the same class is 36 tall too.
    /// </summary>
    /// <remarks>
    /// Measured on a picker of its own rather than on the track selector's, because a dropdown has to
    /// be open for its containers to exist and opening the real one needs an engine and a preference
    /// repository to answer a question about a style. What the two share is asserted next door: both
    /// pickers carry <c>side-list</c>, and this is what <c>side-list</c> does to an option.
    /// </remarks>
    [AvaloniaFact]
    public void An_option_of_a_side_column_picker_is_thirty_six_tall()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var picker = new ComboBox { ItemsSource = new[] { "one", "two" } };
        picker.Classes.Add("side-list");
        var window = new Window { Width = 320, Height = 400, Content = picker };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        picker.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();

        // The logical tree and not the visual one: an open dropdown is hosted in a popup root of its
        // own, so the containers are nowhere below the picker's visuals even though they exist.
        var options = picker.GetLogicalDescendants().OfType<ComboBoxItem>().ToArray();
        Assert.NotEmpty(options);
        Assert.All(options, option => Assert.Equal(RowHeight, option.Bounds.Height));
        window.Close();
    }

    /// <summary>
    /// The label of one row: found by the class that marks it, so a button's own text in the same row
    /// is not mistaken for it.
    /// </summary>
    private static void AssertTruncatesIntoTooltip(Control row, string listName)
    {
        var block = Assert.Single(
            row.GetVisualDescendants().OfType<TextBlock>(),
            candidate => candidate.Classes.Contains("row-label"));
        Assert.Equal(TextTrimming.CharacterEllipsis, block.TextTrimming);
        Assert.Equal(TextWrapping.NoWrap, block.TextWrapping);
        Assert.False(
            string.IsNullOrWhiteSpace(block.Text),
            $"{listName} has a row label with nothing in it.");
        Assert.Equal(block.Text, ToolTip.GetTip(block) as string);
    }

    private static string[] RowTexts(Control view, string listName)
    {
        var list = Assert.Single(
            view.GetVisualDescendants().OfType<ListBox>(),
            box => box.Name == listName);
        return list.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Select(row => string.Concat(row.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty)))
            .ToArray();
    }

    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }

    private static Scope Mount()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private readonly List<Window> _windows = [];

        internal Scope()
        {
            var markers = new MarkerEditorViewModel();
            markers.Markers.Add(new IntroMarker(
                Guid.Parse("11110001-0000-4000-8000-000000000001"),
                Series,
                MarkerKind.Intro,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(120),
                MarkerOrigin.Manual,
                null,
                false));
            Markers = Open(new MarkerEditorView { DataContext = markers });

            var detections = new DetectedMarkerReviewViewModel();
            detections.Detections.Add(Detection(MarkerKind.Intro, 10, 35, confirmed: false));
            detections.Detections.Add(Detection(MarkerKind.Credits, 2_800, 3_000, confirmed: true));
            Detections = Open(new DetectedMarkerReviewView { DataContext = detections });

            var version = new MediaVersion(
                new MediaFileId(Guid.Parse("33330001-0000-4000-8000-000000000001")),
                @"R:\media\film-4k.mkv",
                IsAvailable: true,
                TimeSpan.FromMinutes(100),
                3840,
                2160,
                IsHdr: true,
                "HEVC",
                4_000_000_000);
            var versions = new PlayerVersionsViewModel(
                [new PlayerVersionRowViewModel(version, new VersionSwitchViewModel(), _ => Task.CompletedTask)]);
            Versions = Open(new PlayerVersionsView { DataContext = versions });

            // No data context here on purpose: what is asked of this one is which template its two
            // pickers carry, and building a real SelectTrack would drag an engine and a repository in
            // to answer a question that does not depend on either.
            Tracks = Open(new TrackSelectorView());
        }

        internal MarkerEditorView Markers { get; }

        internal DetectedMarkerReviewView Detections { get; }

        internal PlayerVersionsView Versions { get; }

        internal TrackSelectorView Tracks { get; }

        public void Dispose()
        {
            foreach (var window in _windows)
            {
                window.Close();
            }
        }

        private static DetectedMarker Detection(MarkerKind kind, int start, int end, bool confirmed) =>
            new(
                Guid.Parse($"2222000{(int)kind + 1}-0000-4000-8000-000000000001"),
                Series,
                FileId,
                kind,
                TimeSpan.FromSeconds(start),
                TimeSpan.FromSeconds(end),
                0.87,
                3,
                confirmed);

        private T Open<T>(T view)
            where T : Control
        {
            var window = new Window { Width = 320, Height = 900, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            _windows.Add(window);
            return view;
        }
    }
}
