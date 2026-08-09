using System.ComponentModel;
using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Microsoft.Win32.SafeHandles;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

public sealed class NtfsFileIdentityProvider : IFileIdentityProvider
{
    public Task<FileIdentity> GetAsync(
        string path,
        TechnicalMetadata technicalMetadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(technicalMetadata);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("NTFS file identity is available only on Windows.");
        }

        using var handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (!NativeMethods.GetFileInformationByHandle(handle, out var information))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var volumeId = information.VolumeSerialNumber.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        var fileIndex = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        var fileId = fileIndex.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
        return Task.FromResult(new FileIdentity(volumeId, fileId, null));
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out ByHandleFileInformation fileInformation);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }
}
