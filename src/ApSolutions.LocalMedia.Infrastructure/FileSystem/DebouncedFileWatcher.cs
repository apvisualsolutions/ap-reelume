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
    private readonly int _internalBufferBytes;

    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// The buffer the operating system fills with change records while this process is busy. It
    /// defaults to 8 KiB, which a folder receiving a season at once overflows; 64 KiB is the hard
    /// ceiling the platform allows, and the whole point of asking for it is that overflowing costs
    /// a full rescan.
    /// </summary>
    public const int InternalBufferBytes = 64 * 1024;

    /// <summary>
    /// The smallest buffer the platform honours; anything under it is silently raised to this.
    /// Only a test asks for it, and it asks so that the overflow it exists to prove is certain
    /// rather than likely: at the product's ceiling a storm overflows on some runs and not on
    /// others, so the handler BUG-012 added ran or did not run with nothing saying which, and this
    /// file's coverage swung by four lines between two runs of the same binary.
    /// </summary>
    public const int MinimumInternalBufferBytes = 4 * 1024;

    public DebouncedFileWatcher(
        IClock clock,
        TimeSpan? debounce = null,
        int? internalBufferBytes = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _debounce = debounce ?? DefaultDebounce;
        if (_debounce <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(debounce));
        }

        _internalBufferBytes = internalBufferBytes ?? InternalBufferBytes;
        if (_internalBufferBytes is < MinimumInternalBufferBytes or > InternalBufferBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(internalBufferBytes));
        }
    }

    public async IAsyncEnumerable<FileChangeBatch> StartAsync(
        LibraryRoot root,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        var changes = Channel.CreateUnbounded<WatchSignal>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        using var watcher = CreateWatcher(root.Path, changes.Writer, _internalBufferBytes);
        watcher.EnableRaisingEvents = true;

        while (await changes.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var pending = new Dictionary<string, FileChange>(StringComparer.OrdinalIgnoreCase);
            var eventsLost = Drain(changes.Reader, pending);
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

                    eventsLost |= Drain(changes.Reader, pending);
                    continue;
                }

                await delay.ConfigureAwait(false);
                break;
            }

            yield return new FileChangeBatch(
                root.Id,
                pending.Values.ToArray(),
                _clock.UtcNow,
                eventsLost);
        }
    }

    private static FileSystemWatcher CreateWatcher(
        string path,
        ChannelWriter<WatchSignal> writer,
        int internalBufferBytes)
    {
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = internalBufferBytes,
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size,
        };
        watcher.Created += (_, eventArgs) =>
            writer.TryWrite(new WatchSignal(new FileChange(FileChangeKind.Created, eventArgs.FullPath)));
        watcher.Changed += (_, eventArgs) =>
            writer.TryWrite(new WatchSignal(new FileChange(FileChangeKind.Changed, eventArgs.FullPath)));
        watcher.Renamed += (_, eventArgs) => writer.TryWrite(new WatchSignal(
            new FileChange(FileChangeKind.Renamed, eventArgs.FullPath, eventArgs.OldFullPath)));
        watcher.Deleted += (_, eventArgs) =>
            writer.TryWrite(new WatchSignal(new FileChange(FileChangeKind.Deleted, eventArgs.FullPath)));
        watcher.Error += (_, eventArgs) =>
        {
            // An overflow means "I have lost events", not "I cannot go on": the watcher keeps
            // raising events afterwards, and what the lost ones need is a full pass over the root.
            // Every other error is the end of this watcher, and the reader learns why.
            var error = eventArgs.GetException();
            _ = WatchErrorPolicy.MeansEventsWereLost(error)
                ? writer.TryWrite(WatchSignal.EventsLost)
                : writer.TryComplete(error);
        };
        return watcher;
    }

    private static bool Drain(
        ChannelReader<WatchSignal> reader,
        IDictionary<string, FileChange> pending)
    {
        var eventsLost = false;
        while (reader.TryRead(out var signal))
        {
            if (signal.Change is { } change)
            {
                Coalesce(pending, change);
            }
            else
            {
                eventsLost = true;
            }
        }

        return eventsLost;
    }

    /// <summary>
    /// A change the watcher saw, or — with no change in it — the news that the system dropped
    /// changes nobody will ever see.
    /// </summary>
    private readonly record struct WatchSignal(FileChange? Change)
    {
        public static WatchSignal EventsLost { get; } = new((FileChange?)null);
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
