using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public enum RootKind
{
    Local,
    Usb,
    Unc,
}

public enum RootAvailability
{
    Available,
    Unavailable,
    AccessDenied,
}

[Flags]
public enum ScanPolicy
{
    Manual = 1,
    Startup = 2,
    Continuous = 4,
}

public sealed record LibraryRoot(
    LibraryRootId Id,
    string Path,
    RootKind Kind,
    RootAvailability Availability,
    ScanPolicy ScanPolicy)
{
    public LibraryRoot WithAvailability(RootAvailability availability) =>
        this with { Availability = availability };
}
