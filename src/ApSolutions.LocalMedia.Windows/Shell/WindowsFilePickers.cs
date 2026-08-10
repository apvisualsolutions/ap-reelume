// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace ApSolutions.LocalMedia.Windows.Shell;

/// <summary>
/// The two places the application asks Windows for a path instead of composing one.
/// </summary>
/// <remarks>
/// Both pickers belong to Windows, which is the point: the application never touches a folder it was
/// not handed, and a cancelled dialog simply means nothing happens. Titles arrive as arguments so
/// this stays a thin wrapper over the platform rather than a second place that reads resources
/// (ARQ-006).
/// </remarks>
public static class WindowsFilePickers
{
    /// <summary>
    /// Asks where the exported archive should go, suggesting a timestamped name. Returns
    /// <see langword="null"/> when there is no window to ask from, or when the person cancels.
    /// </summary>
    public static async Task<string?> ChooseArchiveDestinationAsync(
        string title,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (MainWindowStorage() is not { } storage)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = $"apsolutions-localmedia-{utcNow.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}.zip",
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    /// <summary>
    /// Asks which archive to restore from, filtered to ZIP. Returns <see langword="null"/> when
    /// there is no window to ask from, or when the person picks nothing.
    /// </summary>
    public static async Task<string?> ChooseArchiveSourceAsync(
        string title,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (MainWindowStorage() is not { } storage)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("ZIP") { Patterns = ["*.zip"] }],
        }).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private static IStorageProvider? MainWindowStorage() =>
        Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window }
            ? window.StorageProvider
            : null;
}
