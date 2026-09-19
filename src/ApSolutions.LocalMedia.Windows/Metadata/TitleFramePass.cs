// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Metadata;

namespace ApSolutions.LocalMedia.Windows.Metadata;

/// <summary>
/// Starts the frame pass of LIB-021 when the window opens and when a scan ends, and tells the grid
/// when a batch took something.
/// </summary>
/// <remarks>
/// <para>
/// <b>Started, never awaited.</b> A scan's summary goes back to whoever asked for it without waiting
/// for minutes of decoding, and the window paints before the first frame is taken. Both callers ask
/// the same way, and a second request while a pass runs does nothing: the pass itself refuses it.
/// </para>
/// <para>
/// <b>Told through an event, because the grid is built later and elsewhere.</b> The library's view
/// model is built with the shell, after this is registered, and it is the one that listens.
/// </para>
/// </remarks>
public sealed class TitleFramePass(CaptureTitleFrames capture)
{
    private readonly CaptureTitleFrames _capture = capture ?? throw new ArgumentNullException(nameof(capture));

    /// <summary>Raised off the interface thread after every batch that took at least one frame.</summary>
    public event EventHandler FramesTaken = delegate { };

    /// <summary>Starts a pass in the background, unless one is already running.</summary>
    public void Request() => CompositionRoot.PostSafely(() => _capture.ExecuteAsync(TellAsync));

    private Task TellAsync()
    {
        FramesTaken(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}
