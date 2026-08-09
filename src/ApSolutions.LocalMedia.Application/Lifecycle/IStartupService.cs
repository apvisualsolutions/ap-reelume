namespace ApSolutions.LocalMedia.Application.Lifecycle;

/// <summary>What the operating system currently holds for automatic start.</summary>
public enum StartupEntryState
{
    /// <summary>Nothing is registered, which is what a machine that was never asked looks like.</summary>
    Absent,

    /// <summary>An entry exists and points exactly where it should.</summary>
    Present,

    /// <summary>An entry exists but points somewhere else, so it would start the wrong thing.</summary>
    Invalid,
}

/// <summary>
/// Registration for starting with the session. The contract deliberately has no "toggle": enabling
/// and disabling are separate, idempotent acts, so a caller can never flip the state by accident.
/// </summary>
public interface IStartupService
{
    /// <summary>The exact value a correct entry holds, so a wrong one can be recognised.</summary>
    string ExpectedCommand { get; }

    StartupEntryState Inspect();

    /// <summary>Registers automatic start. Calling it twice leaves one entry.</summary>
    void Enable();

    /// <summary>Removes the registration and nothing else. Calling it twice is harmless.</summary>
    void Disable();

    /// <summary>
    /// Rewrites an entry that points somewhere else. Returns whether anything was repaired; an
    /// absent entry is not a fault, so it is left absent.
    /// </summary>
    bool Repair();
}
