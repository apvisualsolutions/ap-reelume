// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Startup;
using Microsoft.Win32;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Windows;

/// <summary>
/// The Windows startup entry, against the real registry.
/// <para>
/// The tests write under a key of their own rather than the real Run key: a test suite must not leave
/// anything behind in the key Windows reads at sign-in. The real key is exercised by hand and recorded
/// in the task evidence, including running the command it holds.
/// </para>
/// </summary>
public sealed class WindowsStartupTests : IDisposable
{
    private const string TestSubKey = @"Software\APSolutions\LocalMedia\Tests\Run";
    private const string ValueName = "APSolutions.LocalMedia";

    private readonly string _executable = Path.Combine(AppContext.BaseDirectory, "reelume-startup-test.exe");

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(TestSubKey, throwOnMissingSubKey: false);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void No_entry_exists_until_startup_is_enabled()
    {
        var service = CreateService();

        Assert.Equal(StartupEntryState.Absent, service.Inspect());
        Assert.Null(ReadStoredValue());
    }

    [Fact]
    public void Enabling_writes_exactly_the_quoted_executable_path()
    {
        var service = CreateService();

        service.Enable();

        Assert.Equal(StartupEntryState.Present, service.Inspect());
        Assert.Equal($"\"{_executable}\"", ReadStoredValue());
        Assert.Equal($"\"{_executable}\"", service.ExpectedCommand);
    }

    [Fact]
    public void Enabling_twice_and_disabling_twice_are_both_idempotent()
    {
        var service = CreateService();

        service.Enable();
        var afterFirst = ReadStoredValue();
        service.Enable();
        Assert.Equal(afterFirst, ReadStoredValue());
        Assert.Equal(StartupEntryState.Present, service.Inspect());

        service.Disable();
        Assert.Equal(StartupEntryState.Absent, service.Inspect());
        service.Disable();
        Assert.Equal(StartupEntryState.Absent, service.Inspect());
        Assert.Null(ReadStoredValue());
    }

    [Fact]
    public void An_entry_that_points_somewhere_else_is_reported_invalid_and_repaired()
    {
        var service = CreateService();
        using (var key = Registry.CurrentUser.CreateSubKey(TestSubKey))
        {
            key.SetValue(ValueName, @"""C:\somewhere\else\other.exe""");
        }

        Assert.Equal(StartupEntryState.Invalid, service.Inspect());

        Assert.True(service.Repair());

        Assert.Equal(StartupEntryState.Present, service.Inspect());
        Assert.Equal($"\"{_executable}\"", ReadStoredValue());
        Assert.False(service.Repair());
    }

    [Fact]
    public void Repairing_when_startup_was_never_wanted_does_not_create_an_entry()
    {
        var service = CreateService();

        Assert.False(service.Repair());

        Assert.Equal(StartupEntryState.Absent, service.Inspect());
        Assert.Null(ReadStoredValue());
    }

    [Fact]
    public void Disabling_removes_the_value_and_leaves_no_other_value_behind()
    {
        var service = CreateService();
        using (var key = Registry.CurrentUser.CreateSubKey(TestSubKey))
        {
            key.SetValue("SomethingElse", "untouched");
        }

        service.Enable();
        service.Disable();

        using var reopened = Registry.CurrentUser.OpenSubKey(TestSubKey);
        Assert.NotNull(reopened);
        Assert.Equal(["SomethingElse"], reopened.GetValueNames());
    }

    [Fact]
    public void The_service_refuses_a_missing_executable_or_key()
    {
        Assert.Throws<ArgumentException>(() => new WindowsStartupService(" ", TestSubKey));
        Assert.Throws<ArgumentException>(() => new WindowsStartupService(_executable, " "));
    }

    /// <summary>
    /// Which key a run writes to is decided by whose data it is keeping, not by who is asking.
    /// </summary>
    /// <remarks>
    /// The run that owns this machine's profile gets the key Windows reads at sign-in, because that
    /// is the whole point of the feature. Any other run — a harness, a walk, a lifecycle check —
    /// gets a key named after its own root, so it can press "start with Windows" and register
    /// nothing on the machine of whoever ran it. Until this existed, the walk could not press that
    /// control at all, and the one control nobody can cover is the one nobody finds broken.
    /// </remarks>
    [Fact]
    public void Only_the_run_that_owns_the_profile_writes_where_windows_reads()
    {
        var owned = new AppDataPaths();
        var isolated = new AppDataPaths(Path.Combine(Path.GetTempPath(), "reelume-isolated-run"));
        var alsoIsolated = new AppDataPaths(Path.Combine(Path.GetTempPath(), "reelume-other-run"));

        Assert.Equal(WindowsStartupService.WindowsRunKey, owned.StartupRegistrySubKey);
        Assert.NotEqual(WindowsStartupService.WindowsRunKey, isolated.StartupRegistrySubKey);
        Assert.NotEqual(isolated.StartupRegistrySubKey, alsoIsolated.StartupRegistrySubKey);

        // Same root, same key, however it is spelt: a run that is restarted has to find its own
        // entry rather than leave a second one behind.
        Assert.Equal(
            isolated.StartupRegistrySubKey,
            new AppDataPaths(Path.Combine(Path.GetTempPath(), "REELUME-ISOLATED-RUN") + Path.DirectorySeparatorChar)
                .StartupRegistrySubKey);
    }

    private WindowsStartupService CreateService() => new(_executable, TestSubKey);

    private static string? ReadStoredValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(TestSubKey);
        return key?.GetValue(ValueName) as string;
    }
}
