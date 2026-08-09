using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Applies the requested gain and holds the result at or below the normalised peak the policy
/// allows. It is a peak limiter, not a compressor: gain is reduced only while a sample would exceed
/// the threshold, and a final clamp guarantees the ceiling even for a step transient.
/// </summary>
public sealed class PeakLimiterAudioFilter
{
    private readonly double _threshold;
    private readonly double _attackCoefficient;
    private readonly double _releaseCoefficient;
    private double _gainReduction = 1.0;

    public PeakLimiterAudioFilter(
        int sampleRate = 48_000,
        double thresholdLevel = VolumeBoostPolicy.LimiterThreshold,
        double attackMilliseconds = 1.5,
        double releaseMilliseconds = 60)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(thresholdLevel, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(thresholdLevel, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attackMilliseconds, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(releaseMilliseconds, 0);
        _threshold = thresholdLevel;
        _attackCoefficient = Coefficient(attackMilliseconds, sampleRate);
        _releaseCoefficient = Coefficient(releaseMilliseconds, sampleRate);
    }

    /// <summary>The ceiling the output never exceeds, in normalised sample level.</summary>
    public double Threshold => _threshold;

    /// <summary>Highest absolute output level observed since the last reset; never above the threshold.</summary>
    public double ObservedPeak { get; private set; }

    /// <summary>True once the limiter has had to reduce gain for the current material.</summary>
    public bool HasEngaged { get; private set; }

    public void Reset()
    {
        _gainReduction = 1.0;
        ObservedPeak = 0;
        HasEngaged = false;
    }

    /// <summary>
    /// Processes one block in place. <paramref name="gain"/> is the linear gain the volume policy
    /// decided; values above one are exactly the case the limiter exists for.
    /// </summary>
    public void Process(Span<float> samples, double gain)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gain);
        for (var index = 0; index < samples.Length; index++)
        {
            var amplified = samples[index] * gain;
            var magnitude = Math.Abs(amplified);
            var target = magnitude > _threshold ? _threshold / magnitude : 1.0;
            if (target < _gainReduction)
            {
                _gainReduction += (target - _gainReduction) * _attackCoefficient;
                HasEngaged = true;
            }
            else
            {
                _gainReduction += (target - _gainReduction) * _releaseCoefficient;
            }

            var limited = amplified * _gainReduction;

            // The smoothed reduction can lag a step transient by a few samples; the clamp is what
            // makes the ceiling a guarantee rather than an average.
            limited = Math.Clamp(limited, -_threshold, _threshold);
            samples[index] = (float)limited;
            ObservedPeak = Math.Max(ObservedPeak, Math.Abs(limited));
        }
    }

    /// <summary>Processes interleaved 16-bit PCM in place, which is what the decoder delivers.</summary>
    public void Process(Span<short> samples, double gain)
    {
        var scratch = new float[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            scratch[index] = samples[index] / (float)short.MaxValue;
        }

        Process(scratch.AsSpan(), gain);
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)Math.Clamp(
                Math.Round(scratch[index] * short.MaxValue),
                short.MinValue,
                short.MaxValue);
        }
    }

    private static double Coefficient(double milliseconds, int sampleRate) =>
        1.0 - Math.Exp(-1.0 / (milliseconds / 1000.0 * sampleRate));
}
