using System.Buffers.Binary;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The two header facts that decide whether a file is code a given machine can run, read from the
/// COFF header rather than inferred from a file name or the folder it arrived in.
/// </summary>
/// <remarks>
/// Managed assemblies are architecture-neutral and say so in their own header, so they are read for
/// what they are instead of being counted as foreign code. This lives on its own because both
/// architectures ask the same question of their own payload, and two copies of this parser would be
/// two chances to answer it differently.
/// </remarks>
internal sealed record PortableExecutableHeader(ushort Machine, bool Managed)
{
    /// <summary><c>IMAGE_FILE_MACHINE_AMD64</c>.</summary>
    public const ushort Amd64 = 0x8664;

    /// <summary><c>IMAGE_FILE_MACHINE_ARM64</c>.</summary>
    public const ushort Arm64 = 0xAA64;

    /// <summary>The architecture name a failure message should use for a machine value.</summary>
    public static string NameOf(ushort machine) => machine switch
    {
        Amd64 => "x64",
        Arm64 => "arm64",
        0x14C => "x86",
        _ => $"0x{machine:X4}",
    };

    /// <summary>Reads the header, or returns <c>null</c> when the file is not a PE image at all.</summary>
    public static PortableExecutableHeader? Read(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[4096];
        var read = stream.ReadAtLeast(header, 512, throwOnEndOfStream: false);
        if (read < 512 || header[0] != (byte)'M' || header[1] != (byte)'Z')
        {
            return null;
        }

        var peOffset = BinaryPrimitives.ReadInt32LittleEndian(header[0x3C..]);
        if (peOffset <= 0 || peOffset + 24 > read
            || header[peOffset] != (byte)'P' || header[peOffset + 1] != (byte)'E')
        {
            return null;
        }

        var machine = BinaryPrimitives.ReadUInt16LittleEndian(header[(peOffset + 4)..]);
        var optional = peOffset + 24;
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(header[optional..]);

        // The CLI header lives in data directory 14, whose offset differs between PE32 and PE32+.
        var cliDirectory = optional + (magic == 0x20B ? 224 : 208);
        var managed = cliDirectory + 8 <= read
            && BinaryPrimitives.ReadUInt32LittleEndian(header[cliDirectory..]) != 0;
        return new PortableExecutableHeader(machine, managed);
    }
}
