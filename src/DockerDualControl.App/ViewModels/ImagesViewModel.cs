using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockerDualControl.Core;
using DockerDualControl.Core.Models;

namespace DockerDualControl.App.ViewModels;

public partial class ImagesViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly HashSet<string> _busyKeys = new();
    private bool _refreshInProgress;
    private string _fingerprint = "";

    public ObservableCollection<ImageRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private string _pullReference = "";

    [ObservableProperty]
    private EngineItemViewModel? _pullEngine;

    [ObservableProperty]
    private bool _isPulling;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isEmpty;

    public ImagesViewModel(MainViewModel main) => _main = main;

    public IReadOnlyList<EngineItemViewModel> AvailableEngines => _main.AvailableEngines;

    public async Task RefreshAsync(bool clear, bool silent = false)
    {
        if (_refreshInProgress)
            return;
        _refreshInProgress = true;
        try
        {
            if (clear)
            {
                Rows.Clear();
                _fingerprint = "";
                ErrorMessage = null;
                IsEmpty = false;
            }

            OnPropertyChanged(nameof(AvailableEngines));
            var engines = _main.AvailableEngines;
            if (PullEngine is null || !engines.Contains(PullEngine))
                PullEngine = engines.FirstOrDefault();

            if (engines.Count == 0)
            {
                Rows.Clear();
                _fingerprint = "";
                IsEmpty = false;
                return;
            }

            if (!silent)
                IsLoading = true;
            try
            {
                var results = await Task.WhenAll(engines.Select(async engine =>
                {
                    try
                    {
                        var images = await engine.Service.ListImagesAsync();
                        return (engine, images, error: (string?)null);
                    }
                    catch (Exception ex) when (ex is DockerCliException or TimeoutException)
                    {
                        return (engine, images: new List<ImageInfo>(), error: ex.Message);
                    }
                }));

                var errors = results
                    .Where(r => r.error is not null)
                    .Select(r => $"{r.engine.DisplayName}: {r.error}")
                    .ToList();
                if (!silent)
                    ErrorMessage = errors.Count > 0 ? string.Join("\n", errors) : null;
                else if (errors.Count == 0)
                    ErrorMessage = null;

                var fresh = results
                    .SelectMany(r => r.images.Select(i => new ImageRowViewModel(i, r.engine, this)))
                    .OrderBy(r => r.EngineShortName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Repository, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var fingerprint = string.Join(";", fresh.Select(r =>
                    $"{r.EngineShortName}|{r.Repository}|{r.Tag}|{r.Size}"));
                if (fingerprint != _fingerprint)
                {
                    _fingerprint = fingerprint;
                    Rows.Clear();
                    foreach (var row in fresh)
                    {
                        row.IsWorking = _busyKeys.Contains(row.RowKey);
                        Rows.Add(row);
                    }
                }
                IsEmpty = Rows.Count == 0 && ErrorMessage is null;
            }
            finally
            {
                IsLoading = false;
            }
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    [RelayCommand]
    private Task RefreshClickedAsync() => RefreshAsync(clear: false);

    [RelayCommand]
    private async Task PullAsync()
    {
        var reference = PullReference.Trim();
        if (PullEngine is not { } engine || reference.Length == 0)
            return;

        IsPulling = true;
        try
        {
            await engine.Service.PullImageAsync(reference);
            PullReference = "";
            await RefreshAsync(clear: false);
        }
        catch (Exception ex) when (ex is DockerCliException or TimeoutException)
        {
            ErrorMessage = $"{engine.DisplayName}: {ex.Message}";
        }
        finally
        {
            IsPulling = false;
        }
    }

    /// <summary>Keyed busy state that survives row rebuilds; see ContainersViewModel.SetBusy.</summary>
    public void SetBusy(string rowKey, bool busy)
    {
        if (busy)
            _busyKeys.Add(rowKey);
        else
            _busyKeys.Remove(rowKey);
        foreach (var row in Rows.Where(r => r.RowKey == rowKey))
            row.IsWorking = busy;
    }

    public void ReportError(string message) => ErrorMessage = message;

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;
}
