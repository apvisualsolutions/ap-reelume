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

    private readonly Lock _sync = new();
    private Thread? _pump;
    private uint _pumpThreadId;
    private bool _isDisposed;

    public event EventHandler<PlaybackInputCommand>? CommandReceived;

    public bool IsListening { get; private set; }

    /// <summary>How many system registrations are held right now; zero when not listening.</summary>
    public int RegisteredKeyCount { get; private set; }

    /// <summary>The keys this service claims while a session is active.</summary>
    public static IReadOnlyList<PlaybackInputCommand> HandledCommands { get; } =
        [.. Registrations.Select(registration => registration.Command)];

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (IsListening)
            {
                return Task.CompletedTask;
            }

            var ready = new TaskCompletionSource();
            _pump = new Thread(() => Pump(ready))
            {
                IsBackground = true,
                Name = "ApSolutions.LocalMedia media keys",
            };
            _pump.SetApartmentState(ApartmentState.STA);
            _pump.Start();
            ready.Task.GetAwaiter().GetResult();
            IsListening = true;
            return Task.CompletedTask;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!IsListening)
            {
                return Task.CompletedTask;
            }

            if (_pumpThreadId != 0)
            {
                _ = PostThreadMessage(_pumpThreadId, WmQuit, nint.Zero, nint.Zero);
            }

            _pump?.Join(TimeSpan.FromSeconds(2));
            _pump = null;
            _pumpThreadId = 0;
            RegisteredKeyCount = 0;
            IsListening = false;
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        _isDisposed = true;
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
