using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Presentation.Review;

public sealed class MediaVersionItemViewModel(MediaVersion version)
{
    public MediaVersion Version { get; } = version ?? throw new ArgumentNullException(nameof(version));

    public string ShortPath
    {
        get
        {
            var fileName = Path.GetFileName(Version.Path);
            var parent = Path.GetFileName(Path.GetDirectoryName(Version.Path));
            return string.IsNullOrWhiteSpace(parent)
                ? $"…\\{fileName}"
                : $"…\\{parent}\\{fileName}";
        }
    }

    public string Quality => string.Join(
        ' ',
        new[]
        {
            Version.Width.HasValue && Version.Height.HasValue ? $"{Version.Width}×{Version.Height}" : null,
            Version.IsHdr ? "HDR" : null,
            string.IsNullOrWhiteSpace(Version.VideoCodec) ? null : Version.VideoCodec,
        }.Where(value => value is not null));

    public bool IsAvailable => Version.IsAvailable;

    public bool IsUnavailable => !Version.IsAvailable;

    public bool IsEffective { get; internal set; }

    public bool IsStoredPreferred { get; internal set; }
}

public sealed class DuplicateReviewViewModel : INotifyPropertyChanged
{
    private readonly MediaVersionSelectionPolicy _selectionPolicy;
    private readonly SetPreferredVersion _setPreferredVersion;
    private readonly MediaVersionPreferences _preferences;
    private MediaVersionGroup _group;

    public DuplicateReviewViewModel(
        MediaVersionGroup group,
        MediaVersionSelectionPolicy selectionPolicy,
        SetPreferredVersion setPreferredVersion,
        MediaVersionPreferences preferences)
    {
        _group = group ?? throw new ArgumentNullException(nameof(group));
        _selectionPolicy = selectionPolicy ?? throw new ArgumentNullException(nameof(selectionPolicy));
        _setPreferredVersion = setPreferredVersion ?? throw new ArgumentNullException(nameof(setPreferredVersion));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        Items = group.Versions.Select(version => new MediaVersionItemViewModel(version)).ToArray();
        SetPreferredCommand = new AsyncCommand(SetPreferredAsync);
        RefreshSelection();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<MediaVersionItemViewModel> Items { get; }

    public ICommand SetPreferredCommand { get; }

    private async Task SetPreferredAsync(object? parameter)
    {
        if (parameter is not MediaVersionItemViewModel item)
        {
            return;
        }

        _group = await _setPreferredVersion.ExecuteAsync(
            new SetPreferredVersionCommand(_group.Id, item.Version.MediaFileId),
            CancellationToken.None).ConfigureAwait(true);
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        var selection = _selectionPolicy.Select(_group, _preferences);
        foreach (var item in Items)
        {
            item.IsEffective = item.Version.MediaFileId == selection.EffectiveVersion?.MediaFileId;
            item.IsStoredPreferred = item.Version.MediaFileId == selection.StoredPreferredMediaFileId;
        }

        OnPropertyChanged(nameof(Items));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class AsyncCommand(Func<object?, Task> execute) : ICommand
    {
        private bool _isRunning;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isRunning;

        public async void Execute(object? parameter)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await execute(parameter).ConfigureAwait(true);
            }
            finally
            {
                _isRunning = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
