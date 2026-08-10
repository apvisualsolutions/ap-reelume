// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Application.Playback;

namespace ApSolutions.LocalMedia.Windows.MediaKeys;

/// <summary>
/// Listens to the hardware media keys while a session exists. Registrations are held on a private
/// message-only window on its own thread and are released completely on stop, so the keys go back to
/// whatever the person had using them before.
/// </summary>
public sealed class WindowsMediaKeyService : IMediaKeySource, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0012;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkMediaNextTrack = 0xB0;
    private const uint VkMediaPrevTrack = 0xB1;
    private const uint VkMediaStop = 0xB2;
    private const uint VkMediaPlayPause = 0xB3;

    private static readonly (int Id, uint VirtualKey, PlaybackInputCommand Command)[] Registrations =
    [
        (1, VkMediaPlayPause, PlaybackInputCommand.PlayPause),
        (2, VkMediaStop, PlaybackInputCommand.Stop),
        (3, VkMediaNextTrack, PlaybackInputCommand.SkipForward),
        (4, VkMediaPrevTrack, PlaybackInputCommand.SkipBackward),
    ];

    /// <summary>
    /// How long the caller waits for the pump to say it has claimed the keys. The keys are an extra,
    /// not a condition of playing anything, so passing this ceiling means the session goes on without
    /// them rather than not going on.
    /// </summary>
    public static readonly TimeSpan DefaultStartTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long stopping waits for the pump thread to end before letting go of it.</summary>
    public static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(2);

    private readonly Lock _sync = new();
    private readonly TimeSpan _startTimeout;
    private readonly TimeSpan _stopTimeout;
    private readonly Action<TaskCompletionSource>? _pumpOverride;
    private Thread? _pump;
    private uint _pumpThreadId;
    private bool _isDisposed;

    public WindowsMediaKeyService()
        : this(DefaultStartTimeout, DefaultStopTimeout)
    {
    }

    /// <summary>
    /// The ceilings, and optionally something to run instead of the real message pump.
    /// </summary>
    /// <remarks>
    /// The pump is substitutable because a ceiling nobody has watched expire is a ceiling nobody
    /// knows works — the same lesson the media generator's hang taught this repository. Production
    /// passes nothing and gets the real pump.
    /// </remarks>
    public WindowsMediaKeyService(
        TimeSpan startTimeout,
        TimeSpan stopTimeout,
        Action<TaskCompletionSource>? pump = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(startTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(stopTimeout, TimeSpan.Zero);
        _startTimeout = startTimeout;
        _stopTimeout = stopTimeout;
        _pumpOverride = pump;
    }

    public event EventHandler<PlaybackInputCommand>? CommandReceived;

    public bool IsListening { get; private set; }

    /// <summary>How many system registrations are held right now; zero when not listening.</summary>
    public int RegisteredKeyCount { get; private set; }

    /// <summary>The keys this service claims while a session is active.</summary>
    public static IReadOnlyList<PlaybackInputCommand> HandledCommands { get; } =
        [.. Registrations.Select(registration => registration.Command)];

    /// <summary>
    /// Claims the keys, waiting for the pump to say it has them — outside the lock, and with a
    /// ceiling.
    /// </summary>
    /// <remarks>
    /// ARQ-005. This used to block on the pump's signal with <c>GetAwaiter().GetResult()</c> while
    /// holding <c>_sync</c>, and it is called from the interface thread every time a video opens. Two
    /// things followed. The window stopped answering until a thread running native registration code
    /// answered back; and a pump that never signalled left the interface thread waiting forever while
    /// holding the lock, so the stop that could have rescued it could not get in either.
    /// <para>
    /// Passing the ceiling is not an error. The hardware keys are an extra, and a session that starts
    /// without them beats a session that does not start — <see cref="RegisteredKeyCount"/> tells the
    /// truth about what was actually claimed either way.
    /// </para>
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        TaskCompletionSource ready;
        lock (_sync)
        {
            if (IsListening)
            {
                return;
            }

            // Continuations run away from the pump thread: this is resumed by whoever is waiting,
            // not by the thread whose only job is to sit in GetMessage.
            ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _pump = new Thread(() => RunPump(ready))
            {
                IsBackground = true,
                Name = "ApSolutions.LocalMedia media keys",
            };
            _pump.SetApartmentState(ApartmentState.STA);
            _pump.Start();

            // Listening is true from the moment the pump exists, not from the moment it answers.
            // Otherwise a stop arriving in between would find nothing to stop and leave it running.
            IsListening = true;
        }

        try
        {
            await ready.Task.WaitAsync(_startTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    /// <summary>
    /// Releases the keys. The pump is asked to quit inside the lock and waited for outside it, so a
    /// thread that will not end cannot keep everybody else out on its way.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Thread? pump;
        lock (_sync)
        {
            if (!IsListening)
            {
                return;
            }

            if (_pumpThreadId != 0)
            {
                _ = PostThreadMessage(_pumpThreadId, WmQuit, nint.Zero, nint.Zero);
            }

            pump = _pump;
            _pump = null;
            _pumpThreadId = 0;
            RegisteredKeyCount = 0;
            IsListening = false;
        }

        if (pump is not null)
        {
            await Task.Run(() => pump.Join(_stopTimeout), CancellationToken.None).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // Blocking here is what IDisposable asks for, and it is bounded twice over: the pump is told
        // to quit before anybody waits, and the wait itself gives up after the stop ceiling.
        StopAsync().GetAwaiter().GetResult();
        _isDisposed = true;
    }

    private void RunPump(TaskCompletionSource ready)
    {
        if (_pumpOverride is not null)
        {
            _pumpOverride(ready);
            return;
        }

        Pump(ready);
    }

    private void Pump(TaskCompletionSource ready)
    {
        var registered = 0;
        try
        {
            CaptureThreadId();
            foreach (var (id, virtualKey, _) in Registrations)
            {
                if (RegisterHotKey(nint.Zero, id, ModNoRepeat, virtualKey))
                {
                    registered++;
                }
            }

            RegisteredKeyCount = registered;
            ready.TrySetResult();

            while (GetMessage(out var message, nint.Zero, 0, 0) > 0)
            {
                if (message.Message != WmHotkey)
                {
                    continue;
                }

                var id = (int)message.WParam;
                var match = Array.Find(Registrations, registration => registration.Id == id);
                if (match.Id != 0)
                {
                    CommandReceived?.Invoke(this, match.Command);
                }
            }
        }
        finally
        {
            foreach (var (id, _, _) in Registrations)
            {
                _ = UnregisterHotKey(nint.Zero, id);
            }

            RegisteredKeyCount = 0;
            ready.TrySetResult();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, nint window, uint filterMin, uint filterMax);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint Window;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }

    private void CaptureThreadId() => _pumpThreadId = GetCurrentThreadId();
}
