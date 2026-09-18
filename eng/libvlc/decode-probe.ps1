# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Plays one sample per codec the application promises through a LibVLC tree, and fails unless
    each one delivers video frames or audio samples, a fake file delivers none, and a subtitle is
    actually drawn.

.DESCRIPTION
    ENG-013 removes code from the engine — every third-party library whose recipe says GPL, and
    VLC's own GPL modules — and the claim that "nothing the program plays today depends on them"
    is only a claim until the rebuilt tree plays it. Three of the libraries it loses decoded
    formats the application promises: a52 (AC-3), dca (DTS), faad2 (AAC) and mad (MP3). After the
    build those formats have to come from FFmpeg's libavcodec, and this is where that is measured.

    It runs in its own process because it has to: the test host already carries the NuGet LibVLC,
    and a process cannot hold two libvlc.dll. It loads libvlccore.dll and then libvlc.dll from
    -LibVlc by full path — the Windows loader does not look in a DLL's own folder for its
    dependencies — and drives them through the same memory route the engine uses: RV32 video
    callbacks and S16N audio callbacks.

    Every row carries its control:
      * each sample must deliver something, and a text file named .mp4 must deliver nothing;
      * the subtitle sample is played twice, with and without --spu, over black video: bright
        pixels must appear with it and must not appear without it. A subtitle "track" that is
        demuxed and never drawn — FreeType missing — passes a decode count and fails this.

    Run it against VideoLAN's NuGet tree too (-LibVlc .../build/x64): that is the reference the
    rebuilt tree is compared with, and a row that fails there is a broken sample, not a finding.

.PARAMETER LibVlc
    The folder holding libvlc.dll, libvlccore.dll and plugins/.

.PARAMETER Samples
    Where to generate the samples (ffmpeg must be on the PATH). Reused if already there.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $LibVlc,
    [Parameter(Mandatory)] [string] $Samples
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$LibVlc = (Resolve-Path $LibVlc).Path
New-Item -ItemType Directory -Force $Samples | Out-Null
$Samples = (Resolve-Path $Samples).Path

# --- samples -------------------------------------------------------------------------------------
# One per codec in the scope matrix, plus the four whose GPL decoder library the build drops.
# Synthetic, generated here, never redistributed. Two seconds is enough to reach steady decoding.
$video = '-f lavfi -i testsrc2=size=320x240:rate=25:duration=2'
$tone = '-f lavfi -i sine=frequency=440:sample_rate=48000:duration=2'
$cases = @(
    @{ Id = 'h264';    Kind = 'video'; Args = "$video -c:v libx264 -pix_fmt yuv420p" },
    @{ Id = 'hevc';    Kind = 'video'; Args = "$video -c:v libx265 -pix_fmt yuv420p" },
    @{ Id = 'mpeg2';   Kind = 'video'; Args = "$video -c:v mpeg2video" },
    @{ Id = 'mpeg4';   Kind = 'video'; Args = "$video -c:v mpeg4" },
    @{ Id = 'vp9';     Kind = 'video'; Args = "$video -c:v libvpx-vp9 -b:v 200k" },
    @{ Id = 'av1';     Kind = 'video'; Args = "$video -c:v libaom-av1 -cpu-used 8 -b:v 200k" },
    @{ Id = 'aac';     Kind = 'audio'; Args = "$tone -c:a aac" },
    @{ Id = 'ac3';     Kind = 'audio'; Args = "$tone -c:a ac3" },
    @{ Id = 'eac3';    Kind = 'audio'; Args = "$tone -c:a eac3" },
    @{ Id = 'dts';     Kind = 'audio'; Args = "$tone -c:a dca -strict -2" },
    @{ Id = 'mp3';     Kind = 'audio'; Args = "$tone -c:a libmp3lame" },
    @{ Id = 'flac';    Kind = 'audio'; Args = "$tone -c:a flac" },
    @{ Id = 'opus';    Kind = 'audio'; Args = "$tone -c:a libopus" },
    @{ Id = 'pcm';     Kind = 'audio'; Args = "$tone -c:a pcm_s16le" }
)
foreach ($case in $cases) {
    $case.Path = Join-Path $Samples "$($case.Id).mkv"
    if (-not (Test-Path $case.Path)) {
        # The recipes hold no quoted argument, so splitting on spaces is exact; the path goes apart.
        & ffmpeg -hide_banner -loglevel error -y @($case.Args -split ' ') $case.Path
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $case.Path)) { throw "ffmpeg could not produce the $($case.Id) sample." }
    }
}
# Black video with one subtitle cue covering the whole clip, so any bright pixel is subtitle ink.
$subtitled = Join-Path $Samples 'subtitle.mkv'
if (-not (Test-Path $subtitled)) {
    $srt = Join-Path $Samples 'subtitle.srt'
    Set-Content $srt "1`n00:00:00,000 --> 00:00:03,000`nSUBTITLE PROBE`n" -Encoding utf8NoBOM
    & ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'color=c=black:size=640x360:rate=25:duration=3' -i $srt `
        -c:v libx264 -pix_fmt yuv420p -c:s srt -disposition:s:0 default $subtitled
    if ($LASTEXITCODE -ne 0) { throw 'ffmpeg could not produce the subtitle sample.' }
}
$fake = Join-Path $Samples 'fake.mp4'
Set-Content $fake 'This is text, not a video.' -Encoding ascii

# --- the probe -----------------------------------------------------------------------------------
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class VlcProbe
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate IntPtr LockCb(IntPtr opaque, IntPtr planes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void UnlockCb(IntPtr opaque, IntPtr picture, IntPtr planes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void DisplayCb(IntPtr opaque, IntPtr picture);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void PlayCb(IntPtr data, IntPtr samples, uint count, long pts);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate void TimeCb(IntPtr data, long pts);

    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr libvlc_new(int argc, IntPtr[] argv);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_release(IntPtr instance);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr libvlc_media_new_path(IntPtr instance, byte[] path);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_add_option(IntPtr media, byte[] option);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_release(IntPtr media);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_release(IntPtr player);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern int libvlc_media_player_play(IntPtr player);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_media_player_stop(IntPtr player);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern int libvlc_media_player_get_state(IntPtr player);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_video_set_callbacks(IntPtr player, LockCb l, UnlockCb u, DisplayCb d, IntPtr opaque);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_video_set_format(IntPtr player, byte[] chroma, uint width, uint height, uint pitch);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_audio_set_callbacks(IntPtr player, PlayCb play, TimeCb pause, TimeCb resume, TimeCb flush, IntPtr drain, IntPtr opaque);
    [DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)] static extern void libvlc_audio_set_format(IntPtr player, byte[] format, uint rate, uint channels);

    const uint Width = 640, Height = 360;
    static IntPtr buffer;
    static int frames, maxBright;
    static long audioSamples;
    // Kept in fields so the collector cannot free a delegate LibVLC still calls.
    static readonly LockCb OnLock = (opaque, planes) => { Marshal.WriteIntPtr(planes, buffer); return IntPtr.Zero; };
    static readonly UnlockCb OnUnlock = (opaque, picture, planes) => { };
    static readonly DisplayCb OnDisplay = (opaque, picture) =>
    {
        int bright = 0;
        // RV32 is B,G,R,X per pixel; a subtitle glyph is near-white on the black probe video.
        unsafe
        {
            byte* p = (byte*)buffer;
            for (int i = 0; i < Width * Height; i++, p += 4)
                if (p[0] > 200 && p[1] > 200 && p[2] > 200) bright++;
        }
        if (bright > maxBright) maxBright = bright;
        Interlocked.Increment(ref frames);
    };
    static readonly PlayCb OnPlay = (data, samples, count, pts) => Interlocked.Add(ref audioSamples, count);
    static readonly TimeCb OnTime = (data, pts) => { };

    public static void Load(string folder)
    {
        NativeLibrary.Load(System.IO.Path.Combine(folder, "libvlccore.dll"));
        NativeLibrary.Load(System.IO.Path.Combine(folder, "libvlc.dll"));
        buffer = Marshal.AllocHGlobal((int)(Width * Height * 4));
    }

    static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s + "\0");

    // Returns frames, audio samples and the most bright pixels seen in one frame.
    public static long[] Play(string path, bool subtitles, int seconds)
    {
        frames = 0; maxBright = 0; audioSamples = 0;
        string[] args = { "--no-video-title-show", "--no-sub-autodetect-file", "--no-metadata-network-access", "--no-osd", subtitles ? "--spu" : "--no-spu" };
        var handles = new GCHandle[args.Length];
        var argv = new IntPtr[args.Length];
        for (int i = 0; i < args.Length; i++) { handles[i] = GCHandle.Alloc(Utf8(args[i]), GCHandleType.Pinned); argv[i] = handles[i].AddrOfPinnedObject(); }
        IntPtr instance = libvlc_new(args.Length, argv);
        foreach (var h in handles) h.Free();
        if (instance == IntPtr.Zero) throw new InvalidOperationException("libvlc_new returned null: the tree did not initialise.");
        IntPtr media = libvlc_media_new_path(instance, Utf8(path));
        libvlc_media_add_option(media, Utf8(":avcodec-hw=none"));
        IntPtr player = libvlc_media_player_new_from_media(media);
        libvlc_media_release(media);
        libvlc_video_set_callbacks(player, OnLock, OnUnlock, OnDisplay, IntPtr.Zero);
        libvlc_video_set_format(player, Utf8("RV32"), Width, Height, Width * 4);
        libvlc_audio_set_callbacks(player, OnPlay, OnTime, OnTime, OnTime, IntPtr.Zero, IntPtr.Zero);
        libvlc_audio_set_format(player, Utf8("S16N"), 48000, 2);
        libvlc_media_player_play(player);
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            int state = libvlc_media_player_get_state(player);
            if (state == 6 || state == 7) break; // Ended, Error
            Thread.Sleep(50);
        }
        libvlc_media_player_stop(player);
        libvlc_media_player_release(player);
        libvlc_release(instance);
        return new long[] { frames, audioSamples, maxBright };
    }
}
'@ -CompilerOptions '/unsafe'

[VlcProbe]::Load($LibVlc)

$failures = [System.Collections.Generic.List[string]]::new()
$rows = foreach ($case in $cases) {
    $r = [VlcProbe]::Play($case.Path, $true, 10)
    $delivered = if ($case.Kind -eq 'video') { $r[0] } else { $r[1] }
    if ($delivered -le 0) { $failures.Add("$($case.Id): delivered no $($case.Kind)") }
    [pscustomobject]@{ sample = $case.Id; kind = $case.Kind; frames = $r[0]; audioSamples = $r[1] }
}
$f = [VlcProbe]::Play($fake, $true, 5)
if ($f[0] -ne 0 -or $f[1] -ne 0) { $failures.Add("fake.mp4: delivered $($f[0]) frames and $($f[1]) samples from a text file — the counters are not measuring decoding") }
$on = [VlcProbe]::Play($subtitled, $true, 10)
$off = [VlcProbe]::Play($subtitled, $false, 10)
if ($on[0] -le 0 -or $off[0] -le 0) { $failures.Add("subtitle sample: no frames (with $($on[0]), without $($off[0]))") }
if ($off[2] -ne 0) { $failures.Add("subtitle control: $($off[2]) bright pixels with subtitles OFF — the probe video is not black, so bright pixels prove nothing") }
if ($on[2] -lt 200) { $failures.Add("subtitle: only $($on[2]) bright pixels with subtitles ON — the text was not drawn") }

$rows | Format-Table -AutoSize | Out-String -Width 200
"fake.mp4: frames $($f[0]), audio samples $($f[1])"
"subtitle: bright pixels with $($on[2]), without $($off[2])"
if ($failures.Count -gt 0) {
    'FAILED:'
    foreach ($x in $failures) { "  $x" }
    exit 1
}
"decode-probe: every sample played through $LibVlc"
