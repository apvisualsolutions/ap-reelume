// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.Tracing;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>
/// Detection is local by contract. This measures the managed side of that claim with the same event
/// sources `PRI-001` uses: a real extraction and comparison of a whole series, with every HTTP
/// request and name resolution the process makes recorded — and the record must stay empty. The
/// native engine is governed by its own no-network options, set where the factory is built.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class SegmentDetectionPrivacyTests
{
    [Fact]
    public async Task A_full_detection_run_opens_no_managed_connection_and_resolves_no_name()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var missing = SegmentCorpus.MissingEncoders();
        Assert.SkipWhen(missing.Count > 0, $"The local ffmpeg build lacks: {string.Join(", ", missing)}.");

        var series = SegmentCorpus.Series.Single(candidate => candidate.Id == "S03");
        var episodes = new List<SegmentDetectionEpisode>();
        foreach (var episode in series.Episodes)
        {
            var path = await SegmentCorpus.MaterialiseAsync(episode, TestContext.Current.CancellationToken);
            episodes.Add(new SegmentDetectionEpisode(
                new MediaFileId(Guid.NewGuid()),
                path,
                SegmentCorpus.EpisodeDuration(episode)));
        }

        using var listener = new NetworkListener();
        await using var factory = LibVlcFactory.CreateHeadless();
        var detector = new AutomaticSegmentDetector(new LocalSegmentFeatureExtractor(factory));

        var detection = await detector.DetectAsync(
            new SeriesId(Guid.NewGuid()),
            episodes,
            progress: null,
            TestContext.Current.CancellationToken);

        // The measurement means nothing over idle work: the run must have really read and compared.
        Assert.NotEmpty(detection.Segments);
        Assert.True(
            listener.Requests.Count == 0,
            $"Detection made an HTTP request: {string.Join(" | ", listener.Requests.Take(3))}");
        Assert.True(
            listener.Resolutions.Count == 0,
            $"Detection resolved a name: {string.Join(" | ", listener.Resolutions.Take(3))}");
    }

    /// <summary>The same in-process observation `PRI-001` verified end to end, reduced to what this needs.</summary>
    private sealed class NetworkListener : EventListener
    {
        private readonly List<string> _requests = [];
        private readonly List<string> _resolutions = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<string> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public IReadOnlyList<string> Resolutions
        {
            get
            {
                lock (_gate)
                {
                    return [.. _resolutions];
                }
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name is "System.Net.Http" or "System.Net.NameResolution")
            {
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var payload = string.Join(
                ' ',
                eventData.Payload?.Select(value => value?.ToString() ?? string.Empty) ?? []);
            if (payload.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                || payload.Contains("127.0.0.1", StringComparison.Ordinal)
                || payload.Contains("::1", StringComparison.Ordinal)
                || payload.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                // The test host talks to its own runner over loopback; that is the harness, not the
                // detector, and counting it would make the measurement about the wrong process.
                return;
            }

            lock (_gate)
            {
                if (eventData.EventSource.Name == "System.Net.Http" && eventData.EventName == "RequestStart")
                {
                    _requests.Add(payload);
                }
                else if (eventData.EventSource.Name == "System.Net.NameResolution"
                    && eventData.EventName == "ResolutionStart")
                {
                    _resolutions.Add(payload);
                }
            }
        }
    }
}
