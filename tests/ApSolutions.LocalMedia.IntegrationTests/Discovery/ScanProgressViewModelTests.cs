// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Library;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

public sealed class ScanProgressViewModelTests
{
    [Fact]
    public void View_model_exposes_non_blocking_progress_and_cancellation_contract()
    {
        var type = Assembly.Load("ApSolutions.LocalMedia.Presentation").GetType(
            "ApSolutions.LocalMedia.Presentation.Library.ScanProgressViewModel",
            throwOnError: false);

        Assert.NotNull(type);
        Assert.NotNull(type.GetProperty("EnumeratedCount"));
        Assert.NotNull(type.GetProperty("ProbeCount"));
        Assert.NotNull(type.GetProperty("CurrentPath"));
        Assert.NotNull(type.GetProperty("IsRunning"));
        Assert.NotNull(type.GetProperty("CanCancel"));
        Assert.NotNull(type.GetMethod("Begin", [typeof(CancellationTokenSource)]));
        Assert.NotNull(type.GetMethod("Apply", [typeof(ScanProgressChanged)]));
        Assert.NotNull(type.GetMethod("Cancel", Type.EmptyTypes));
    }

    [Fact]
    public async Task View_model_tracks_typed_events_and_cancels_the_active_scan()
    {
        var publisher = new InProcessApplicationEventPublisher();
        var viewModel = new ScanProgressViewModel(publisher);
        using var cancellation = new CancellationTokenSource();
        viewModel.Begin(cancellation);

        await publisher.PublishAsync(
            new ScanProgressChanged(
                new LibraryRootId(Guid.NewGuid()),
                EnumeratedCount: 128,
                ProbeCount: 64,
                CurrentPath: @"C:\Media\item.mkv",
                IsCompleted: false),
            TestContext.Current.CancellationToken);

        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.CanCancel);
        Assert.Equal(128, viewModel.EnumeratedCount);
        Assert.Equal(64, viewModel.ProbeCount);
        Assert.Equal(@"C:\Media\item.mkv", viewModel.CurrentPath);

        viewModel.Cancel();
        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(viewModel.CanCancel);

        using var completedCancellation = new CancellationTokenSource();
        viewModel.Begin(completedCancellation);
        await publisher.PublishAsync(
            new ScanProgressChanged(
                new LibraryRootId(Guid.NewGuid()),
                EnumeratedCount: 256,
                ProbeCount: 65,
                CurrentPath: null,
                IsCompleted: true),
            TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.CanCancel);
        Assert.Equal(256, viewModel.EnumeratedCount);
    }
}
