// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Presentation.Review;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Review;

/// <summary>
/// What one card of the review tray says about the file it is asking about, and what its three
/// decisions do when nobody is listening to them.
/// </summary>
/// <remarks>
/// The card gained the file, the kind and the three commands on 2026-08-25. Every one of them has a
/// silent half — a candidate whose file the catalogue no longer knows, and a card built with no
/// handlers, which is what a headless mount produces — and a silent half that throws is a tray that
/// dies on the row nobody looked at.
/// </remarks>
public sealed class CandidateCardTests
{
    [Fact]
    public void A_card_over_no_candidate_refuses_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new CandidateCardViewModel(null!));
    }

    /// <summary>
    /// The file's name and its folder, split from the one path the projection carries.
    /// </summary>
    [AvaloniaFact]
    public void A_card_names_the_file_and_the_folder_it_sits_in()
    {
        var card = new CandidateCardViewModel(Candidate(@"D:\Series\_entrada\puerto.sombra.s02e05.mkv"));

        Assert.True(card.HasFile);
        Assert.Equal("puerto.sombra.s02e05.mkv", card.FileName);
        Assert.Equal(@"D:\Series\_entrada", card.FileFolder);

        // And the silence: a candidate whose file the catalogue no longer knows says nothing rather
        // than printing an empty name under an eyebrow that promises one.
        var orphan = new CandidateCardViewModel(Candidate(path: null));
        Assert.False(orphan.HasFile);
        Assert.Equal(string.Empty, orphan.FileName);
        Assert.Equal(string.Empty, orphan.FileFolder);

        // And a path that is there and says nothing, which is a different absence from no path at
        // all and reaches the card the same way.
        var blank = new CandidateCardViewModel(Candidate(path: string.Empty));
        Assert.False(blank.HasFile);
        Assert.Equal(string.Empty, blank.FileName);
        Assert.Equal(string.Empty, blank.FileFolder);

        // A path with no folder in it at all — a file the scan met at the root of a drive, or a name
        // stored bare — has a name and no folder, and the card says exactly that.
        var rootless = new CandidateCardViewModel(Candidate("arrival.mkv"));
        Assert.True(rootless.HasFile);
        Assert.Equal("arrival.mkv", rootless.FileName);
        Assert.Equal(string.Empty, rootless.FileFolder);

        // And a path that IS a folder and nothing else, which is the one shape Windows answers with
        // no parent at all: the card says nothing rather than propagating the null.
        var root = new CandidateCardViewModel(Candidate(@"C:\"));
        Assert.False(root.HasFile);
        Assert.Equal(string.Empty, root.FileFolder);
    }

    /// <summary>
    /// «Película» or «Serie», as a key: an episode candidate is a series to whoever is deciding.
    /// </summary>
    [AvaloniaFact]
    public void A_card_says_which_kind_of_thing_is_proposed()
    {
        Assert.Equal("CatalogKindMovie", new CandidateCardViewModel(Candidate(null)).KindKey);
        Assert.Equal(
            "CatalogKindShow",
            new CandidateCardViewModel(Candidate(null, CandidateContentKind.Episode)).KindKey);
    }

    /// <summary>
    /// A card with no handlers behind it answers every one of its three decisions by doing nothing.
    /// </summary>
    /// <remarks>
    /// It is the shape a headless mount produces, and the shape the tray hands out while it is being
    /// assembled. What matters is that the commands exist and refuse rather than throwing: a button
    /// that cannot act is disabled, and a disabled button that still crashes when something presses
    /// it is worse than one that was never offered.
    /// </remarks>
    [AvaloniaFact]
    public void A_card_with_nobody_listening_decides_nothing_and_throws_nothing()
    {
        var card = new CandidateCardViewModel(Candidate(@"D:\Cine\arrival.mkv"));

        Assert.False(card.AcceptCommand.CanExecute(null));
        Assert.False(card.RejectCommand.CanExecute(null));
        Assert.False(card.SearchManuallyCommand.CanExecute(null));

        card.AcceptCommand.Execute(null);
        card.RejectCommand.Execute(null);
        card.SearchManuallyCommand.Execute(null);
    }

    /// <summary>And with handlers, each of the three reaches its own.</summary>
    [AvaloniaFact]
    public void Each_decision_reaches_the_hand_that_was_given_it()
    {
        var accepted = 0;
        var rejected = 0;
        var searched = 0;
        var card = new CandidateCardViewModel(
            Candidate(@"D:\Cine\arrival.mkv"),
            _ =>
            {
                accepted++;
                return Task.CompletedTask;
            },
            _ =>
            {
                rejected++;
                return Task.CompletedTask;
            },
            _ => searched++);

        Assert.True(card.AcceptCommand.CanExecute(null));
        card.AcceptCommand.Execute(null);
        card.RejectCommand.Execute(null);
        card.SearchManuallyCommand.Execute(null);

        Assert.Equal(1, accepted);
        Assert.Equal(1, rejected);
        Assert.Equal(1, searched);
    }

    private static MatchCandidate Candidate(
        string? path,
        CandidateContentKind kind = CandidateContentKind.Movie) => new(
        new CandidateId(Guid.NewGuid()),
        new MediaFileId(Guid.NewGuid()),
        "movie:329865",
        kind,
        0.86,
        1,
        ReviewState.Suggested,
        [],
        ["Identification.Signal.Title"],
        Revision: 0,
        IsDecisionLocked: false,
        path);
}
