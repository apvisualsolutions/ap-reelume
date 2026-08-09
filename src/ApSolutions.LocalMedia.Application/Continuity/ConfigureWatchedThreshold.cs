using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// Reads and changes the point at which content counts as watched. Moving the threshold reconsiders
/// only the states the player computed: a decision made by hand is never recomputed underneath it.
/// </summary>
public sealed class ConfigureWatchedThreshold
{
    /// <summary>Where the chosen threshold is stored between sessions.</summary>
    public const string SettingKey = "continuity.watched-threshold";

    private readonly ISettingsStore _settings;
    private readonly IWatchStateRepository _repository;
    private readonly IClock _clock;

    public ConfigureWatchedThreshold(
        ISettingsStore settings,
        IWatchStateRepository repository,
        IClock clock)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>The threshold in force, always inside the accepted range.</summary>
    public double Current => WatchStatePolicy.ClampThreshold(
        _settings.Read<double?>(SettingKey) ?? WatchStatePolicy.DefaultWatchedThreshold);

    /// <summary>Stores the threshold and returns how many automatic states it moved.</summary>
    public async Task<int> ExecuteAsync(double requested, CancellationToken cancellationToken = default)
    {
        var threshold = WatchStatePolicy.ClampThreshold(requested);
        _settings.Write(SettingKey, threshold);

        var recalculated = 0;
        foreach (var state in await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (state.IsManualOverride)
            {
                continue;
            }

            var status = WatchStatePolicy.Evaluate(state.Position, state.ObservedDuration, threshold);
            if (status == state.Status)
            {
                continue;
            }

            await _repository
                .SaveAsync(state with { Status = status, UpdatedUtc = _clock.UtcNow }, cancellationToken)
                .ConfigureAwait(false);
            recalculated++;
        }

        return recalculated;
    }
}
