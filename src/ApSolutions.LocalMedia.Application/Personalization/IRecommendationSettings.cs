namespace ApSolutions.LocalMedia.Application.Personalization;

/// <summary>
/// Whether the person wants suggestions at all. The answer is remembered between sessions, which is
/// why it is a stored setting rather than a field on a view.
/// </summary>
public interface IRecommendationSettings
{
    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);
}
