// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Windows.Shell;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Shell;

/// <summary>
/// The handover a run that does not own this machine's profile is built with: what would have gone
/// to Windows is written down, under that run's own root, and nothing opens or shuts down anywhere.
/// </summary>
/// <remarks>
/// Two controls were uncoverable for one reason each, and they are the same reason: pressing them
/// hands something to Windows itself. Opening the backup folder left an Explorer window on whoever
/// was measuring, and leaving ended the process doing the measuring.
/// </remarks>
public sealed class RecordingSystemHandoffTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"handoff-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task An_offered_folder_is_written_down_whole_and_named_as_one()
    {
        var handoff = new RecordingSystemHandoff(_root);
        var folder = Path.Combine(_root, "backups");

        Assert.True(handoff.TryOpenFolder(folder));

        Assert.Equal(
            [$"{RecordingSystemHandoff.OpenFolderVerb} {folder}"],
            await File.ReadAllLinesAsync(handoff.RecordPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Asking_to_leave_is_written_down_and_nothing_shuts_down()
    {
        var handoff = new RecordingSystemHandoff(_root);

        handoff.RequestExit();

        Assert.Equal(
            [RecordingSystemHandoff.ExitVerb],
            await File.ReadAllLinesAsync(handoff.RecordPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task An_offered_package_is_written_down_under_a_verb_of_its_own()
    {
        var handoff = new RecordingSystemHandoff(_root);
        var package = Path.Combine(_root, "apreelume-0.2.0.msix");

        Assert.True(handoff.TryOpenPackage(package));

        Assert.Equal(
            [$"{RecordingSystemHandoff.OpenPackageVerb} {package}"],
            await File.ReadAllLinesAsync(handoff.RecordPath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Each handover is added, in order, and each is told apart by its verb — which is what a probe
    /// reads instead of parsing anything.
    /// </summary>
    [Fact]
    public async Task Every_handover_is_added_in_the_order_it_was_asked_for()
    {
        var handoff = new RecordingSystemHandoff(_root);
        var package = Path.Combine(_root, "apreelume-0.2.0.msix");

        Assert.True(handoff.TryOpenFolder(_root));
        Assert.True(handoff.TryOpenPackage(package));
        handoff.RequestExit();

        Assert.Equal(
            [
                $"{RecordingSystemHandoff.OpenFolderVerb} {_root}",
                $"{RecordingSystemHandoff.OpenPackageVerb} {package}",
                RecordingSystemHandoff.ExitVerb,
            ],
            await File.ReadAllLinesAsync(handoff.RecordPath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The folder is made when it is needed, the same as the recorded link launcher's: a first press
    /// that failed because a folder was missing would report a refusal about the wrong thing.
    /// </summary>
    [Fact]
    public void The_handover_folder_does_not_have_to_exist_beforehand()
    {
        var handoff = new RecordingSystemHandoff(Path.Combine(_root, "never", "created"));

        Assert.True(handoff.TryOpenFolder(_root));
        Assert.True(File.Exists(handoff.RecordPath));
    }

    [Fact]
    public void Something_that_names_nothing_is_rejected_before_anything_is_written()
    {
        var handoff = new RecordingSystemHandoff(_root);

        _ = Assert.Throws<ArgumentException>(() => handoff.TryOpenFolder("   "));
        _ = Assert.Throws<ArgumentNullException>(() => handoff.TryOpenFolder(null!));
        _ = Assert.Throws<ArgumentException>(() => handoff.TryOpenPackage("   "));
        _ = Assert.Throws<ArgumentNullException>(() => handoff.TryOpenPackage(null!));
        Assert.False(File.Exists(handoff.RecordPath));
    }

    /// <summary>
    /// A record that cannot be written is a refusal rather than a crash — the same answer the Windows
    /// handover gives when the shell will not take the folder.
    /// </summary>
    [Fact]
    public async Task A_record_that_cannot_be_written_is_refused_rather_than_thrown()
    {
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "blocked");
        await File.WriteAllTextAsync(blocked, "in the way", TestContext.Current.CancellationToken);
        var handoff = new RecordingSystemHandoff(blocked);

        Assert.False(handoff.TryOpenFolder(_root));
        Assert.False(handoff.TryOpenPackage(Path.Combine(_root, "apreelume-0.2.0.msix")));

        // And leaving is refused just as quietly: a screen that could not write its record must not
        // become a screen that crashes on the way out.
        handoff.RequestExit();
        Assert.False(File.Exists(handoff.RecordPath));
    }

    [Fact]
    public void A_handover_with_nowhere_to_write_refuses_to_exist()
    {
        _ = Assert.Throws<ArgumentException>(() => new RecordingSystemHandoff("   "));
        _ = Assert.Throws<ArgumentNullException>(() => new RecordingSystemHandoff(null!));
    }
}
