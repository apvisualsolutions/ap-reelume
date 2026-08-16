// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Windows.Backup;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Backup;

/// <summary>
/// The file dialogs a run with a data root of its own answers for itself, and the confinement that
/// makes doing so acceptable: every answer is inside the handover folder it was handed, and there is
/// no answer at all when the folder holds nothing.
/// </summary>
/// <remarks>
/// The whole class lives in one suite on purpose. Merged Cobertura reports keep the better of two
/// readings per line rather than their union, so a branch split across suites reads as half covered
/// for good — measured on <c>ReviewInboxViewModel.LoadMoreAsync</c>, 2026-08-16.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class HandoffArchivePickerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"handoff-{Guid.NewGuid():N}");

    private string Handoff => Path.Combine(_root, "handoff");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void A_picker_without_a_folder_to_answer_with_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new HandoffArchivePicker(null!));
        Assert.Throws<ArgumentException>(() => new HandoffArchivePicker("   "));
    }

    /// <summary>
    /// The export lands inside the handover folder, and the folder exists afterwards — the dialog this
    /// stands in for can only ever hand back somewhere that does.
    /// </summary>
    [Fact]
    public async Task The_destination_is_a_zip_inside_the_handover_folder()
    {
        var chosen = await new HandoffArchivePicker(Handoff)
            .ChooseDestinationAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(chosen);
        Assert.True(Directory.Exists(Handoff), "The picker named a folder it had not created.");
        Assert.Equal(Handoff, Path.GetDirectoryName(chosen));
        Assert.Equal(".zip", Path.GetExtension(chosen));
    }

    /// <summary>
    /// With nothing in the folder there is nothing to restore, and the answer is the one a cancelled
    /// dialog gives. It matters that the two are the same: the wizard reads null as "somebody changed
    /// their mind" and does nothing, which is the correct thing to do in both cases.
    /// </summary>
    [Fact]
    public async Task An_empty_or_absent_folder_answers_the_way_a_cancelled_dialog_does()
    {
        var picker = new HandoffArchivePicker(Handoff);
        Assert.Null(await picker.ChooseSourceAsync(TestContext.Current.CancellationToken));

        Directory.CreateDirectory(Handoff);
        await File.WriteAllTextAsync(
            Path.Combine(Handoff, "external-links.txt"),
            "https://example.invalid/",
            TestContext.Current.CancellationToken);
        Assert.Null(await picker.ChooseSourceAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The newest archive, because a run that exported twice means the second one.
    /// </summary>
    [Fact]
    public async Task The_source_is_the_newest_archive_lying_in_the_folder()
    {
        var older = await WriteArchiveAsync("first.zip", new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));
        var newer = await WriteArchiveAsync("second.zip", new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc));

        var chosen = await new HandoffArchivePicker(Handoff)
            .ChooseSourceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(newer, chosen);
        Assert.True(File.Exists(older), "Choosing a source must not consume the others.");
    }

    /// <summary>
    /// And two written inside the same clock tick still resolve to one answer, rather than to whatever
    /// the file system happened to enumerate first.
    /// </summary>
    [Fact]
    public async Task Archives_sharing_a_timestamp_are_still_decided()
    {
        var stamp = new DateTime(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);
        await WriteArchiveAsync("aaa.zip", stamp);
        var last = await WriteArchiveAsync("zzz.zip", stamp);

        var picker = new HandoffArchivePicker(Handoff);
        Assert.Equal(last, await picker.ChooseSourceAsync(TestContext.Current.CancellationToken));
        Assert.Equal(last, await picker.ChooseSourceAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Both halves answer a cancellation the way the dialogs they replace do: by stopping, rather than
    /// by naming a path nobody is going to use.
    /// </summary>
    [Fact]
    public async Task A_cancelled_request_names_nothing()
    {
        var picker = new HandoffArchivePicker(Handoff);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => picker.ChooseDestinationAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => picker.ChooseSourceAsync(cancelled.Token));
        Assert.False(
            Directory.Exists(Handoff),
            "A cancelled request created the folder it had just refused to answer with.");
    }

    private async Task<string> WriteArchiveAsync(string name, DateTime writtenUtc)
    {
        Directory.CreateDirectory(Handoff);
        var path = Path.Combine(Handoff, name);
        await File.WriteAllBytesAsync(path, [0x50, 0x4B], TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(path, writtenUtc);
        return path;
    }
}
