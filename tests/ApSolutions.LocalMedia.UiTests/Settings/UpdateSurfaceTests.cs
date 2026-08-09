using System.Globalization;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Presentation.Updates;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The surface an update is confirmed on. Everything it is allowed to do is a consequence of what
/// somebody just read, and it says what happened in every case — including the cases where nothing
/// did.
/// </summary>
/// <remarks>
/// The claim being defended is that no sequence of interactions reaches the installer without a
/// confirmation for that exact version. The view model is the last place that could break it, because
/// it is the only place that constructs a consent.
/// </remarks>
public sealed class UpdateSurfaceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_surface_starts_with_no_offer_and_has_contacted_nothing()
    {
        var checks = 0;
        var viewModel = Build(check: _ =>
        {
            checks++;
            return UpToDate();
        });

        Assert.Equal("UpdateStatusIdle", viewModel.StatusKey);
        Assert.False(viewModel.HasOffer);
        Assert.False(viewModel.IsReadyToInstall);
        Assert.Null(viewModel.OfferedVersion);
        Assert.Equal(0, checks);
    }

    [Fact]
    public async Task An_offer_shows_the_version_and_the_summary_in_both_languages()
    {
        var viewModel = Build(check: _ => Offered());

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusOffered", viewModel.StatusKey);
        Assert.True(viewModel.HasOffer);
        Assert.Equal("0.2.0", viewModel.OfferedVersion);
        Assert.Equal("Corrige la reanudación.", viewModel.SummarySpanish);
        Assert.Equal("Fixes resume.", viewModel.SummaryEnglish);
        Assert.False(viewModel.IsReadyToInstall);
    }

    /// <summary>
    /// The summary somebody reads is in the language the application is running in, and the other one
    /// stays available rather than being discarded: a screenshot of an update dialogue is one of the
    /// things people send each other.
    /// </summary>
    [Theory]
    [InlineData("es-ES", "Corrige la reanudación.")]
    [InlineData("en-US", "Fixes resume.")]
    [InlineData("fr-FR", "Fixes resume.")]
    public async Task The_summary_on_screen_follows_the_interface_language(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var viewModel = Build(check: _ => Offered());

            await viewModel.CheckAsync(Cancellation);

            Assert.Equal(expected, viewModel.Summary);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public async Task Nothing_new_is_said_plainly_rather_than_as_a_failure()
    {
        var viewModel = Build(check: _ => UpToDate());

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusUpToDate", viewModel.StatusKey);
        Assert.False(viewModel.HasOffer);
    }

    /// <summary>
    /// Being unable to ask is not being up to date. Somebody who is offline has to be told that the
    /// question was never answered.
    /// </summary>
    [Fact]
    public async Task A_source_that_could_not_be_reached_says_so_instead_of_saying_up_to_date()
    {
        var viewModel = Build(check: _ => Task.FromResult(
            new UpdateCheckResult(UpdateCheckStatus.Unreachable, null, null)));

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusUnreachable", viewModel.StatusKey);
        Assert.False(viewModel.HasOffer);
    }

    /// <summary>
    /// A release the policy refuses is reported as a release that cannot be used, and it names which
    /// rule stopped it. "Something went wrong" would leave nobody able to act.
    /// </summary>
    [Theory]
    [InlineData(UpdateRejection.InsecureDownload, "UpdateRefusedInsecureDownload")]
    [InlineData(UpdateRejection.UnusableHash, "UpdateRefusedUnusableHash")]
    [InlineData(UpdateRejection.WrongRuntime, "UpdateRefusedWrongRuntime")]
    [InlineData(UpdateRejection.UndeclaredSize, "UpdateRefusedUndeclaredSize")]
    [InlineData(UpdateRejection.IncompleteSummary, "UpdateRefusedIncompleteSummary")]
    public async Task A_release_that_cannot_be_used_names_the_rule_that_stopped_it(
        UpdateRejection rejection,
        string expectedDetail)
    {
        var viewModel = Build(check: _ => Task.FromResult(new UpdateCheckResult(
            UpdateCheckStatus.Answered,
            UpdateDecision.Refused(rejection, "because"),
            null)));

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusUnusableRelease", viewModel.StatusKey);
        Assert.Equal(expectedDetail, viewModel.DetailKey);
        Assert.False(viewModel.HasOffer);
    }

    /// <summary>
    /// Being told there is nothing newer is the same news whether the source said so or the policy
    /// worked it out, so both arrive on screen as "up to date".
    /// </summary>
    [Theory]
    [InlineData(UpdateRejection.NotNewer)]
    [InlineData(UpdateRejection.NoReleaseAvailable)]
    public async Task Having_nothing_newer_is_the_same_news_however_it_was_established(UpdateRejection rejection)
    {
        var viewModel = Build(check: _ => Task.FromResult(new UpdateCheckResult(
            UpdateCheckStatus.Answered,
            UpdateDecision.Refused(rejection, "because"),
            null)));

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusUpToDate", viewModel.StatusKey);
        Assert.Null(viewModel.DetailKey);
    }

    /// <summary>
    /// Downloading is not installing. A finished download leaves a package ready and the installer
    /// untouched.
    /// </summary>
    [Fact]
    public async Task Downloading_reports_progress_and_installs_nothing()
    {
        var installs = 0;
        var viewModel = Build(
            check: _ => Offered(),
            stage: (release, progress, _) =>
            {
                progress.Report(new UpdateDownloadProgress(512, 1024));
                progress.Report(new UpdateDownloadProgress(1024, 1024));
                return Task.FromResult(new StagedUpdate(release, StagedPath));
            },
            install: (_, _, _) =>
            {
                installs++;
                return Task.FromResult(new UpdateInstallation(UpdateOutcome.HandedToWindows, "handed"));
            });
        await viewModel.CheckAsync(Cancellation);

        await viewModel.DownloadAsync(Cancellation);

        Assert.Equal("UpdateStatusReady", viewModel.StatusKey);
        Assert.True(viewModel.IsReadyToInstall);
        Assert.Equal(1024L, viewModel.Completed);
        Assert.Equal(1024L, viewModel.Total);
        Assert.Equal(0, installs);
    }

    /// <summary>
    /// The screen names the package and never the folder it is in. A path on screen is how a
    /// screenshot stops being safe to share.
    /// </summary>
    [Fact]
    public async Task The_screen_names_the_package_and_never_the_folder_it_is_in()
    {
        var viewModel = Build(
            check: _ => Offered(),
            stage: (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)));
        await viewModel.CheckAsync(Cancellation);

        await viewModel.DownloadAsync(Cancellation);

        Assert.Equal("APSolutions.LocalMedia_0.2.0_x64.msix", viewModel.PackageName);
        Assert.DoesNotContain(
            Path.DirectorySeparatorChar.ToString(),
            viewModel.PackageName,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The installer cannot be reached before there is a verified package to install. This is the
    /// property that has to survive every future edit of this surface.
    /// </summary>
    [Fact]
    public async Task Installing_is_impossible_until_a_verified_package_exists()
    {
        var installs = 0;
        var viewModel = Build(
            check: _ => Offered(),
            install: (_, _, _) =>
            {
                installs++;
                return Task.FromResult(new UpdateInstallation(UpdateOutcome.HandedToWindows, "handed"));
            });

        Assert.False(viewModel.InstallCommand.CanExecute(null));
        viewModel.InstallCommand.Execute(null);
        await viewModel.CheckAsync(Cancellation);
        Assert.False(viewModel.InstallCommand.CanExecute(null));
        viewModel.InstallCommand.Execute(null);
        await viewModel.InstallAsync(Cancellation);

        Assert.Equal(0, installs);
    }

    /// <summary>
    /// Confirming produces a consent for the version that was on screen, dated when the person said
    /// yes. A consent for anything else would not be the confirmation they gave.
    /// </summary>
    [Fact]
    public async Task Confirming_hands_over_a_consent_for_exactly_the_version_that_was_read()
    {
        UpdateConsent? given = null;
        var viewModel = Build(
            check: _ => Offered(),
            stage: (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)),
            install: (_, consent, _) =>
            {
                given = consent;
                return Task.FromResult(new UpdateInstallation(UpdateOutcome.HandedToWindows, "handed"));
            });
        await viewModel.CheckAsync(Cancellation);
        await viewModel.DownloadAsync(Cancellation);

        Assert.True(viewModel.InstallCommand.CanExecute(null));
        await viewModel.InstallAsync(Cancellation);

        Assert.NotNull(given);
        Assert.Equal("0.2.0", given.Version);
        Assert.Equal(Noon, given.GrantedUtc);
        Assert.Equal("UpdateStatusHandedToWindows", viewModel.StatusKey);
    }

    /// <summary>
    /// Every answer the installer can give reaches the screen as itself. An update Windows refused
    /// and one it accepted must not look the same.
    /// </summary>
    [Theory]
    [InlineData(UpdateOutcome.HandedToWindows, "UpdateStatusHandedToWindows")]
    [InlineData(UpdateOutcome.LaunchRefused, "UpdateStatusLaunchRefused")]
    [InlineData(UpdateOutcome.Tampered, "UpdateStatusTampered")]
    [InlineData(UpdateOutcome.ConsentMismatch, "UpdateStatusNotConfirmed")]
    [InlineData(UpdateOutcome.NotConfirmed, "UpdateStatusNotConfirmed")]
    public async Task Each_answer_the_installer_gives_reaches_the_screen_as_itself(
        UpdateOutcome outcome,
        string expected)
    {
        var viewModel = Build(
            check: _ => Offered(),
            stage: (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)),
            install: (_, _, _) => Task.FromResult(new UpdateInstallation(outcome, "detail")));
        await viewModel.CheckAsync(Cancellation);
        await viewModel.DownloadAsync(Cancellation);

        await viewModel.InstallAsync(Cancellation);

        Assert.Equal(expected, viewModel.StatusKey);
    }

    /// <summary>
    /// A package that turned out not to be what it promised is named as that, and the surface goes
    /// back to having nothing installable rather than staying armed.
    /// </summary>
    [Fact]
    public async Task A_download_that_fails_verification_leaves_nothing_installable()
    {
        var viewModel = Build(
            check: _ => Offered(),
            stage: (_, _, _) => throw new UpdateVerificationException(
                "APSolutions.LocalMedia_0.2.0_x64.msix",
                new string('a', 64),
                new string('b', 64),
                1024,
                1024));
        await viewModel.CheckAsync(Cancellation);

        await viewModel.DownloadAsync(Cancellation);

        Assert.Equal("UpdateStatusVerificationFailed", viewModel.StatusKey);
        Assert.False(viewModel.IsReadyToInstall);
        Assert.False(viewModel.InstallCommand.CanExecute(null));
    }

    /// <summary>
    /// An interrupted download is a download that can be tried again, and it says so: the offer stays
    /// on screen so the button is still there to press.
    /// </summary>
    [Fact]
    public async Task An_interrupted_download_can_be_tried_again_without_checking_first()
    {
        var attempts = 0;
        var viewModel = Build(
            check: _ => Offered(),
            stage: (release, _, _) => ++attempts == 1
                ? throw new UpdateInterruptedException("APSolutions.LocalMedia_0.2.0_x64.msix", 512, 1024)
                : Task.FromResult(new StagedUpdate(release, StagedPath)));
        await viewModel.CheckAsync(Cancellation);

        await viewModel.DownloadAsync(Cancellation);
        Assert.Equal("UpdateStatusInterrupted", viewModel.StatusKey);
        Assert.True(viewModel.HasOffer);
        Assert.True(viewModel.DownloadCommand.CanExecute(null));

        await viewModel.DownloadAsync(Cancellation);

        Assert.Equal("UpdateStatusReady", viewModel.StatusKey);
        Assert.Equal(2, attempts);
    }

    /// <summary>
    /// Cancelling reaches the download itself. Pressing the button while it runs has to cancel the
    /// token the downloader is holding, not merely change what the screen says.
    /// </summary>
    [Fact]
    public async Task Cancelling_a_download_stops_it_and_arms_nothing()
    {
        UpdateViewModel? viewModel = null;
        viewModel = Build(
            check: _ => Offered(),
            stage: (_, _, cancellationToken) =>
            {
                viewModel!.CancelCommand.Execute(null);
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new StagedUpdate(Release(), StagedPath));
            });
        await viewModel.CheckAsync(Cancellation);

        await viewModel.DownloadAsync(Cancellation);

        Assert.Equal("UpdateStatusCancelled", viewModel.StatusKey);
        Assert.False(viewModel.IsReadyToInstall);
    }

    /// <summary>
    /// Looking on its own is off until somebody turns it on, and turning it on is remembered. This is
    /// the whole of what "configurable" has to mean for something that opens a connection.
    /// </summary>
    [Fact]
    public void Automatic_checks_start_off_and_turning_them_on_is_remembered()
    {
        var settings = new RememberedUpdateSettings();
        var viewModel = Build(settings: settings);

        Assert.False(viewModel.AutomaticCheckEnabled);

        viewModel.AutomaticCheckEnabled = true;

        Assert.True(settings.AutomaticCheckEnabled);
        Assert.True(viewModel.AutomaticCheckEnabled);

        viewModel.AutomaticCheckEnabled = false;

        Assert.False(settings.AutomaticCheckEnabled);
    }

    /// <summary>
    /// Checking again after an offer was staged clears the staged package. Otherwise the button would
    /// still install yesterday's package while the screen described today's.
    /// </summary>
    [Fact]
    public async Task Checking_again_clears_whatever_was_staged_for_the_previous_answer()
    {
        var viewModel = Build(
            check: _ => Offered(),
            stage: (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)));
        await viewModel.CheckAsync(Cancellation);
        await viewModel.DownloadAsync(Cancellation);
        Assert.True(viewModel.IsReadyToInstall);

        await viewModel.CheckAsync(Cancellation);

        Assert.False(viewModel.IsReadyToInstall);
        Assert.Null(viewModel.PackageName);
    }

    /// <summary>
    /// A check somebody pressed is a requested one; the one the application starts by itself is an
    /// automatic one. They are different questions, and only the second is governed by the setting.
    /// </summary>
    [Fact]
    public async Task Pressing_the_button_asks_differently_from_the_application_looking_on_its_own()
    {
        var triggers = new List<UpdateCheckTrigger>();
        var viewModel = Build(check: trigger =>
        {
            triggers.Add(trigger);
            return UpToDate();
        });

        await viewModel.CheckAsync(Cancellation);
        await viewModel.CheckAutomaticallyAsync(Cancellation);

        Assert.Equal([UpdateCheckTrigger.Requested, UpdateCheckTrigger.Automatic], triggers);
    }

    /// <summary>
    /// The automatic check the application starts at launch leaves the surface exactly as it found
    /// it when nobody has allowed it. That is what stops a preference from being a switch nothing
    /// reads, and what stops the switch from being a connection nobody asked for.
    /// </summary>
    [Fact]
    public async Task The_automatic_check_changes_nothing_while_the_setting_is_off()
    {
        var viewModel = Build(check: _ => Task.FromResult(
            new UpdateCheckResult(UpdateCheckStatus.NotAsked, null, null)));

        await viewModel.CheckAutomaticallyAsync(Cancellation);

        Assert.Equal("UpdateStatusIdle", viewModel.StatusKey);
        Assert.False(viewModel.HasOffer);
        Assert.False(viewModel.IsBusy);
    }

    /// <summary>
    /// The buttons do what the methods do. Testing the methods and leaving the commands unexercised
    /// would prove the behaviour of something nobody can press.
    /// </summary>
    [Fact]
    public async Task The_three_buttons_walk_the_same_path_the_methods_do()
    {
        UpdateConsent? given = null;
        var viewModel = Build(
            check: _ => Offered(),
            stage: (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)),
            install: (_, consent, _) =>
            {
                given = consent;
                return Task.FromResult(new UpdateInstallation(UpdateOutcome.HandedToWindows, "handed"));
            });

        Assert.True(viewModel.IsIdle);
        Assert.True(viewModel.CheckCommand.CanExecute(null));
        viewModel.CheckCommand.Execute(null);
        await WaitUntilIdleAsync(viewModel);
        Assert.True(viewModel.HasOffer);

        viewModel.DownloadCommand.Execute(null);
        await WaitUntilIdleAsync(viewModel);
        Assert.True(viewModel.IsReadyToInstall);

        viewModel.InstallCommand.Execute(null);
        await WaitUntilIdleAsync(viewModel);

        Assert.Equal("0.2.0", given?.Version);
    }

    /// <summary>
    /// Downloading with nothing on offer does nothing at all. The button is hidden in that state,
    /// but a surface whose only defence is a hidden button is one edit away from not having one.
    /// </summary>
    [Fact]
    public async Task Downloading_with_nothing_on_offer_fetches_nothing()
    {
        var attempts = 0;
        var viewModel = Build(
            check: _ => UpToDate(),
            stage: (release, _, _) =>
            {
                attempts++;
                return Task.FromResult(new StagedUpdate(release, StagedPath));
            });
        await viewModel.CheckAsync(Cancellation);

        await viewModel.DownloadAsync(Cancellation);

        Assert.Equal(0, attempts);
        Assert.False(viewModel.IsReadyToInstall);
    }

    /// <summary>
    /// One thing at a time. A second press while the first is still running is ignored rather than
    /// queued, because two downloads writing the same staging file is not a race worth having.
    /// </summary>
    [Fact]
    public async Task A_second_request_while_one_is_running_is_ignored()
    {
        var checks = 0;
        var release = new TaskCompletionSource();
        var viewModel = Build(check: async _ =>
        {
            checks++;
            await release.Task;
            return await Offered();
        });

        var first = viewModel.CheckAsync(Cancellation);
        await viewModel.CheckAsync(Cancellation);
        await viewModel.DownloadAsync(Cancellation);
        await viewModel.InstallAsync(Cancellation);
        release.SetResult();
        await first;

        Assert.Equal(1, checks);
        Assert.True(viewModel.HasOffer);
    }

    /// <summary>
    /// Every failure a step can produce reaches the screen as something, and never as silence. A
    /// surface that says nothing after a button press is one somebody presses again.
    /// </summary>
    [Fact]
    public async Task A_check_that_fails_outright_says_so_rather_than_staying_silent()
    {
        var viewModel = Build(check: _ => throw new IOException("no route"));

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusUnreachable", viewModel.StatusKey);
    }

    [Fact]
    public async Task A_check_that_is_cancelled_says_so()
    {
        var viewModel = Build(check: _ => throw new OperationCanceledException());

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusCancelled", viewModel.StatusKey);
    }

    [Fact]
    public async Task A_download_that_fails_outright_says_so()
    {
        var viewModel = Build(
            check: _ => Offered(),
            stage: (_, _, _) => throw new IOException("no route"));
        await viewModel.CheckAsync(Cancellation);

        await viewModel.DownloadAsync(Cancellation);

        Assert.Equal("UpdateStatusUnreachable", viewModel.StatusKey);
        Assert.False(viewModel.IsReadyToInstall);
    }

    [Fact]
    public async Task An_installation_that_is_cancelled_or_fails_outright_says_which()
    {
        var cancelled = Build(
            check: _ => Offered(),
            stage: (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)),
            install: (_, _, _) => throw new OperationCanceledException());
        await cancelled.CheckAsync(Cancellation);
        await cancelled.DownloadAsync(Cancellation);

        await cancelled.InstallAsync(Cancellation);
        Assert.Equal("UpdateStatusCancelled", cancelled.StatusKey);

        var failed = Build(
            check: _ => Offered(),
            stage: (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)),
            install: (_, _, _) => throw new IOException("gone"));
        await failed.CheckAsync(Cancellation);
        await failed.DownloadAsync(Cancellation);

        await failed.InstallAsync(Cancellation);
        Assert.Equal("UpdateStatusLaunchRefused", failed.StatusKey);
    }

    /// <summary>
    /// A check that was never run leaves the surface saying what it said before, which is that
    /// nothing has been checked.
    /// </summary>
    [Fact]
    public async Task A_check_nobody_asked_for_leaves_the_surface_where_it_was()
    {
        var viewModel = Build(check: _ => Task.FromResult(
            new UpdateCheckResult(UpdateCheckStatus.NotAsked, null, null)));

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusIdle", viewModel.StatusKey);
        Assert.False(viewModel.HasOffer);
    }

    /// <summary>Setting the switch to what it already is changes nothing and announces nothing.</summary>
    [Fact]
    public void Setting_the_switch_to_what_it_already_is_changes_nothing()
    {
        var announcements = 0;
        var viewModel = Build();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(UpdateViewModel.AutomaticCheckEnabled))
            {
                announcements++;
            }
        };

        viewModel.AutomaticCheckEnabled = false;
        Assert.Equal(0, announcements);

        viewModel.AutomaticCheckEnabled = true;
        Assert.Equal(1, announcements);
    }

    /// <summary>Nothing can be built without the parts it needs to do any of this.</summary>
    [Fact]
    public void The_surface_cannot_be_built_without_the_three_steps_and_a_clock()
    {
        Func<UpdateCheckTrigger, CancellationToken, Task<UpdateCheckResult>> check = (_, _) => UpToDate();
        Func<UpdateRelease, IProgress<UpdateDownloadProgress>, CancellationToken, Task<StagedUpdate>> stage =
            (release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath));
        Func<StagedUpdate, UpdateConsent?, CancellationToken, Task<UpdateInstallation>> install =
            (_, _, _) => Task.FromResult(new UpdateInstallation(UpdateOutcome.HandedToWindows, "handed"));
        Func<DateTimeOffset> now = () => Noon;
        var settings = new RememberedUpdateSettings();

        Assert.Throws<ArgumentNullException>(() => new UpdateViewModel(null!, stage, install, now, settings));
        Assert.Throws<ArgumentNullException>(() => new UpdateViewModel(check, null!, install, now, settings));
        Assert.Throws<ArgumentNullException>(() => new UpdateViewModel(check, stage, null!, now, settings));
        Assert.Throws<ArgumentNullException>(() => new UpdateViewModel(check, stage, install, null!, settings));
        Assert.Throws<ArgumentNullException>(() => new UpdateViewModel(check, stage, install, now, null!));
    }

    /// <summary>
    /// With nothing on offer there is nothing to read, and every part of the offer says so rather
    /// than carrying whatever was there before.
    /// </summary>
    [Fact]
    public void With_nothing_on_offer_there_is_nothing_to_read()
    {
        var viewModel = Build();

        Assert.Null(viewModel.OfferedVersion);
        Assert.Null(viewModel.SummarySpanish);
        Assert.Null(viewModel.SummaryEnglish);
        Assert.Null(viewModel.Summary);
        Assert.Null(viewModel.PackageName);
        Assert.Null(viewModel.DetailKey);
    }

    /// <summary>
    /// While a step is running, nothing else can be started and cancelling is the only thing that
    /// can. The buttons say so on their own, before anybody presses them.
    /// </summary>
    [Fact]
    public async Task While_a_step_runs_only_cancelling_is_offered()
    {
        var release = new TaskCompletionSource();
        var announcements = 0;
        var viewModel = Build(check: async _ =>
        {
            await release.Task;
            return await Offered();
        });
        viewModel.DownloadCommand.CanExecuteChanged += (_, _) => announcements++;

        var running = viewModel.CheckAsync(Cancellation);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.CheckCommand.CanExecute(null));
        Assert.False(viewModel.DownloadCommand.CanExecute(null));
        Assert.False(viewModel.InstallCommand.CanExecute(null));
        Assert.True(viewModel.CancelCommand.CanExecute(null));

        release.SetResult();
        await running;

        Assert.True(announcements > 0, "The buttons were never told the state had changed.");
    }

    /// <summary>Cancelling when there is nothing to cancel does nothing, and says nothing.</summary>
    [Fact]
    public void Cancelling_with_nothing_running_does_nothing()
    {
        var viewModel = Build();

        viewModel.Cancel();
        viewModel.CancelCommand.Execute(null);

        Assert.Equal("UpdateStatusIdle", viewModel.StatusKey);
        Assert.False(viewModel.IsBusy);
    }

    /// <summary>
    /// A release that does not say which version it is gets a consent that says the same. The version
    /// is what the confirmation is about, so an empty one is refused downstream rather than being
    /// invented here.
    /// </summary>
    [Fact]
    public async Task A_release_with_no_version_produces_a_consent_that_names_no_version()
    {
        UpdateConsent? given = null;
        var release = new UpdateRelease(null, "win-x64", "https://example.invalid/x.msix", null, 1, "Es", "En");
        var viewModel = Build(
            check: _ => Task.FromResult(new UpdateCheckResult(
                UpdateCheckStatus.Answered,
                UpdateDecision.Offered(release),
                release)),
            stage: (_, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath)),
            install: (_, consent, _) =>
            {
                given = consent;
                return Task.FromResult(new UpdateInstallation(UpdateOutcome.ConsentMismatch, "no version"));
            });
        await viewModel.CheckAsync(Cancellation);
        await viewModel.DownloadAsync(Cancellation);

        await viewModel.InstallAsync(Cancellation);

        Assert.Equal(string.Empty, given?.Version);
        Assert.Equal("UpdateStatusNotConfirmed", viewModel.StatusKey);
    }

    /// <summary>Waits for a command to finish, since a button hands back before the work does.</summary>
    private static async Task WaitUntilIdleAsync(UpdateViewModel viewModel)
    {
        for (var attempt = 0; attempt < 100 && viewModel.IsBusy; attempt++)
        {
            await Task.Delay(10, Cancellation);
        }

        Assert.True(viewModel.IsIdle, "The surface never went back to idle.");
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static string StagedPath =>
        Path.Combine("staging", "0.2.0", "APSolutions.LocalMedia_0.2.0_x64.msix");

    [Fact]
    public async Task An_unexpected_failure_reads_as_unreachable_instead_of_checking_forever()
    {
        // The automatic check runs on the interface thread at startup; an exception that escapes
        // here is an application that dies for a network answer. The surface must land on a state,
        // and "checking…" frozen forever is not one.
        var viewModel = Build(check: _ => throw new FormatException("the source answered nonsense"));

        await viewModel.CheckAsync(Cancellation);

        Assert.Equal("UpdateStatusUnreachable", viewModel.StatusKey);
        Assert.False(viewModel.IsBusy);
    }

    private static UpdateViewModel Build(
        Func<UpdateCheckTrigger, Task<UpdateCheckResult>>? check = null,
        Func<UpdateRelease, IProgress<UpdateDownloadProgress>, CancellationToken, Task<StagedUpdate>>? stage = null,
        Func<StagedUpdate, UpdateConsent?, CancellationToken, Task<UpdateInstallation>>? install = null,
        IUpdateSettings? settings = null) =>
        new(
            (trigger, _) => (check ?? (_ => UpToDate()))(trigger),
            stage ?? ((release, _, _) => Task.FromResult(new StagedUpdate(release, StagedPath))),
            install ?? ((_, _, _) =>
                Task.FromResult(new UpdateInstallation(UpdateOutcome.HandedToWindows, "handed"))),
            () => Noon,
            settings ?? new RememberedUpdateSettings());

    /// <summary>The setting as a person would leave it, without a file to leave it in.</summary>
    private sealed class RememberedUpdateSettings : IUpdateSettings
    {
        public bool AutomaticCheckEnabled { get; private set; }

        public void SetAutomaticCheckEnabled(bool enabled) => AutomaticCheckEnabled = enabled;
    }

    private static UpdateRelease Release() => new(
        "0.2.0",
        "win-x64",
        "https://example.invalid/APSolutions.LocalMedia_0.2.0_x64.msix",
        new string('a', 64),
        1024,
        "Corrige la reanudación.",
        "Fixes resume.");

    private static Task<UpdateCheckResult> Offered()
    {
        var release = Release();
        return Task.FromResult(new UpdateCheckResult(
            UpdateCheckStatus.Answered,
            UpdateDecision.Offered(release),
            release));
    }

    private static Task<UpdateCheckResult> UpToDate() => Task.FromResult(new UpdateCheckResult(
        UpdateCheckStatus.Answered,
        UpdateDecision.Refused(UpdateRejection.NotNewer, "nothing newer"),
        null));
}
