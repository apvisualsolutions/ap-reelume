using ApSolutions.LocalMedia.Domain.Updates;

namespace ApSolutions.LocalMedia.Application.Updates;

/// <summary>What this installation is, from its own point of view.</summary>
/// <remarks>
/// Both halves are read from the running application rather than configured, because an installation
/// that could be told what version it is could be told it is older than it is.
/// </remarks>
public sealed record InstalledRelease(string Version, string Runtime);

/// <summary>Whether the application may look for updates nobody asked it to look for.</summary>
public interface IUpdateSettings
{
    bool AutomaticCheckEnabled { get; }

    void SetAutomaticCheckEnabled(bool enabled);
}

/// <summary>Where a release description comes from.</summary>
/// <remarks>
/// The runtime is a parameter rather than a filter applied afterwards: a source that cannot answer
/// for this architecture answers with nothing, which is a different thing from answering with a
/// package for another one.
/// </remarks>
public interface IUpdateSource
{
    Task<UpdateRelease?> GetLatestAsync(string runtime, CancellationToken cancellationToken = default);
}

/// <summary>One package on disk, with the release whose promises it has been proved to keep.</summary>
public sealed record StagedUpdate(UpdateRelease Release, string Path);

/// <summary>
/// How much of the package has arrived. <paramref name="BytesReceived"/> counts everything on disk,
/// including what an earlier attempt left there, so a resumed download never appears to go backwards.
/// </summary>
public sealed record UpdateDownloadProgress(long BytesReceived, long TotalBytes);

/// <summary>
/// Fetches a release into a staging file and proves that what arrived is what was promised.
/// </summary>
/// <remarks>
/// Nothing outside the staging directory is ever written. That is what makes every failure here
/// survivable: the installation somebody is using is not a participant in the download.
/// </remarks>
public interface IUpdateDownloader
{
    Task<StagedUpdate> DownloadAsync(
        UpdateRelease release,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Hands a verified package to Windows, which is the only thing that installs anything here.
/// </summary>
/// <remarks>
/// The answer is whether Windows took it, not whether the update succeeded: what happens after the
/// handover belongs to the installer, and claiming otherwise would be reporting an outcome nobody
/// observed.
/// </remarks>
public interface IUpdateLauncher
{
    Task<bool> LaunchAsync(StagedUpdate staged, CancellationToken cancellationToken = default);
}

/// <summary>
/// Somebody read what changed and said yes to this exact version.
/// </summary>
/// <remarks>
/// A consent names a version because that is what was on screen when it was given. Anything that
/// would let yesterday's yes install today's package turns the confirmation into a formality.
/// </remarks>
public sealed record UpdateConsent(string Version, DateTimeOffset GrantedUtc);

/// <summary>Why a check ended the way it did, before anything the policy had to say about it.</summary>
public enum UpdateCheckStatus
{
    /// <summary>Nobody asked and automatic checks are off, so nothing was contacted.</summary>
    NotAsked,

    /// <summary>The source answered, and <see cref="UpdateCheckResult.Decision"/> is that answer.</summary>
    Answered,

    /// <summary>The source could not be reached. This is not the same as being up to date.</summary>
    Unreachable,
}

/// <summary>Whether somebody asked for this check or the application ran it on its own.</summary>
public enum UpdateCheckTrigger
{
    Requested,
    Automatic,
}

/// <summary>
/// What one check established. The release travels only when it is being offered, so nothing
/// downstream can act on a description the policy refused.
/// </summary>
public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    UpdateDecision? Decision,
    UpdateRelease? Release)
{
    public bool HasOffer =>
        Status == UpdateCheckStatus.Answered && Decision is { IsOffered: true } && Release is not null;
}

/// <summary>What happened when somebody tried to install a staged package.</summary>
public enum UpdateOutcome
{
    /// <summary>Nobody confirmed, so nothing was handed to anybody.</summary>
    NotConfirmed,

    /// <summary>The confirmation was for a different version than the one staged.</summary>
    ConsentMismatch,

    /// <summary>What is on disk is no longer what was verified, or is no longer there at all.</summary>
    Tampered,

    /// <summary>Windows would not take the package. The installation is untouched and can try again.</summary>
    LaunchRefused,

    /// <summary>Windows accepted the package. What it does with it is Windows's business.</summary>
    HandedToWindows,
}

public sealed record UpdateInstallation(UpdateOutcome Outcome, string Reason);

/// <summary>
/// Raised when a caller asks to download a release the policy refuses. It is a defect rather than a
/// condition: the decision is made before the offer, so nothing should be able to reach here.
/// </summary>
public sealed class UpdateRefusedException(UpdateRejection rejection, string reason)
    : InvalidOperationException(reason)
{
    public UpdateRejection Rejection { get; } = rejection;
}

/// <summary>
/// Raised when the source cannot be reached at all — no answer, an answer that is not one, or a
/// destination that never resolves to a package.
/// </summary>
/// <remarks>
/// This is deliberately distinct from "there is no release". Collapsing the two would make an
/// offline machine indistinguishable from an up-to-date one, and a security fix would sit unoffered
/// for as long as the network stayed broken.
/// </remarks>
public sealed class UpdateSourceUnavailableException(string reason, Exception? innerException = null)
    : IOException(reason, innerException);

/// <summary>
/// Raised when the connection died before the package finished arriving. What did arrive is kept, so
/// the next attempt resumes rather than starting again.
/// </summary>
public sealed class UpdateInterruptedException(string fileName, long bytesReceived, long totalBytes)
    : IOException($"{fileName} stopped arriving after {bytesReceived} of {totalBytes} bytes.")
{
    public string FileName { get; } = fileName;

    public long BytesReceived { get; } = bytesReceived;

    public long TotalBytes { get; } = totalBytes;
}

/// <summary>
/// Raised when the package that arrived is not the package that was promised. The file is deleted
/// rather than offered, because the only thing anybody could do with it is install it.
/// </summary>
public sealed class UpdateVerificationException(
    string fileName,
    string expectedSha256,
    string actualSha256,
    long expectedSize,
    long actualSize)
    : IOException($"{fileName} is not what the release described.")
{
    public string FileName { get; } = fileName;

    public string ExpectedSha256 { get; } = expectedSha256;

    public string ActualSha256 { get; } = actualSha256;

    public long ExpectedSize { get; } = expectedSize;

    public long ActualSize { get; } = actualSize;
}
