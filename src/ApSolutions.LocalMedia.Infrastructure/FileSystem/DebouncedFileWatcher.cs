// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

public sealed class DebouncedFileWatcher : IRootWatcher
{
    private readonly IClock _clock;
    private readonly TimeSpan _debounce;

    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(750);

    public DebouncedFileWatcher(IClock clock, TimeSpan? debounce = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _debounce = debounce ?? DefaultDebounce;
        if (_debounce <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce));
        }
    }

    public async IAsyncEnumerable<FileChangeBatch> StartAsync(
        LibraryRoot root,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        var changes = Channel.CreateUnbounded<FileChange>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        using var watcher = CreateWatcher(root.Path, changes.Writer);
        watcher.EnableRaisingEvents = true;

        while (await changes.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var pending = new Dictionary<string, FileChange>(StringComparer.OrdinalIgnoreCase);
            Drain(changes.Reader, pending);
            while (true)
            {
                using var debounceCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var delay = _clock.DelayAsync(_debounce, debounceCancellation.Token);
                var nextEvent = changes.Reader.WaitToReadAsync(cancellationToken).AsTask();
                var completed = await Task.WhenAny(delay, nextEvent).ConfigureAwait(false);
                if (completed == nextEvent && await nextEvent.ConfigureAwait(false))
                {
                    debounceCancellation.Cancel();
                    try
                    {
                        await delay.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (debounceCancellation.IsCancellationRequested)
                    {
                    }

                    Drain(changes.Reader, pending);
                    continue;
                }

                await delay.ConfigureAwait(false);
                break;
            }

            yield return new FileChangeBatch(root.Id, pending.Values.ToArray(), _clock.UtcNow);
        }
    }

    private static FileSystemWatcher CreateWatcher(string path, ChannelWriter<FileChange> writer)
    {
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size,
        };
        watcher.Created += (_, eventArgs) =>
            writer.TryWrite(new FileChange(FileChangeKind.Created, eventArgs.FullPath));
        watcher.Changed += (_, eventArgs) =>
            writer.TryWrite(new FileChange(FileChangeKind.Changed, eventArgs.FullPath));
        watcher.Renamed += (_, eventArgs) =>
            writer.TryWrite(new FileChange(FileChangeKind.Renamed, eventArgs.FullPath, eventArgs.OldFullPath));
        watcher.Deleted += (_, eventArgs) =>
            writer.TryWrite(new FileChange(FileChangeKind.Deleted, eventArgs.FullPath));
        watcher.Error += (_, eventArgs) => writer.TryComplete(eventArgs.GetException());
        return watcher;
    }

    private static void Drain(
        ChannelReader<FileChange> reader,
        IDictionary<string, FileChange> pending)
    {
        while (reader.TryRead(out var change))
        {
            Coalesce(pending, change);
        }
    }

    private static void Coalesce(IDictionary<string, FileChange> pending, FileChange change)
    {
        FileChange? previous = null;
        if (change.Kind == FileChangeKind.Renamed &&
            change.PreviousPath is not null &&
            pending.Remove(change.PreviousPath, out var renamedPrevious))
        {
            previous = renamedPrevious;
        }

        if (pending.TryGetValue(change.Path, out var samePath))
        {
            previous = samePath;
        }

        pending[change.Path] = (previous?.Kind, change.Kind) switch
        {
            (FileChangeKind.Created, FileChangeKind.Changed) => previous,
            (FileChangeKind.Created, FileChangeKind.Renamed) =>
                new FileChange(FileChangeKind.Created, change.Path),
            (FileChangeKind.Deleted, FileChangeKind.Created) =>
                new FileChange(FileChangeKind.Changed, change.Path),
            (_, FileChangeKind.Deleted) =>
                new FileChange(FileChangeKind.Deleted, change.Path, change.PreviousPath),
            _ => change,
        };
    }
}
