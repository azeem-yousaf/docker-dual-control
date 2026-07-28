using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockerDualControl.Core;
using DockerDualControl.Core.Models;

namespace DockerDualControl.App.ViewModels;

public partial class ContainersViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly HashSet<string> _busyKeys = new();
    private readonly ContainerStateTracker _stateTracker = new();
    private bool _refreshInProgress;
    private string _fingerprint = "";

    public ObservableCollection<ContainerRowViewModel> Rows { get; } = new();

    /// <summary>Raised after a refresh when containers started or stopped since the
    /// previous successful listing; feeds the tray notifications.</summary>
    public event Action<IReadOnlyList<ContainerStateChange>>? StateChangesDetected;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isEmpty;

    public ContainersViewModel(MainViewModel main) => _main = main;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

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

            var engines = _main.AvailableEngines;
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
                        var containers = await engine.Service.ListContainersAsync();
                        return (engine, containers, error: (string?)null);
                    }
                    catch (Exception ex) when (ex is DockerCliException or TimeoutException)
                    {
                        return (engine, containers: new List<ContainerInfo>(), error: ex.Message);
                    }
                }));

                // Only successful listings feed the tracker: an engine that failed
                // this tick must not read as "all its containers stopped".
                var stateChanges = results
                    .Where(r => r.error is null)
                    .SelectMany(r => _stateTracker.Update(r.engine.Engine.Id, r.containers))
                    .ToList();
                if (stateChanges.Count > 0)
                    StateChangesDetected?.Invoke(stateChanges);

                var errors = results
                    .Where(r => r.error is not null)
                    .Select(r => $"{r.engine.DisplayName}: {r.error}")
                    .ToList();
                if (!silent)
                    ErrorMessage = errors.Count > 0 ? string.Join("\n", errors) : null;
                else if (errors.Count == 0)
                    ErrorMessage = null;

                var fresh = results
                    .SelectMany(r => r.containers.Select(c => new ContainerRowViewModel(c, r.engine, this)))
                    .OrderBy(r => r.EngineShortName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Names, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Skip UI churn when nothing changed (auto-refresh runs every 3 s).
                var fingerprint = string.Join(";", fresh.Select(r =>
                    $"{r.EngineShortName}|{r.Id}|{r.State}|{r.Status}|{r.Ports}|{r.Names}|{r.Image}"));
                if (fingerprint != _fingerprint)
                {
                    _fingerprint = fingerprint;
                    SyncRows(fresh);
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
    private async Task RunContainerAsync()
    {
        var engines = _main.AvailableEngines;
        if (engines.Count == 0)
            return;
        if (DialogService.ShowRunContainerDialog(engines, engines[0], prefillImage: null))
            await RefreshAsync(clear: false);
    }

    public void ReportError(string message) => ErrorMessage = message;

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    /// <summary>
    /// Busy state is keyed here, not on row objects: rows are rebuilt on refresh,
    /// and an in-flight action must re-enable whichever row instance is current.
    /// </summary>
    public void SetBusy(string rowKey, bool busy)
    {
        if (busy)
            _busyKeys.Add(rowKey);
        else
            _busyKeys.Remove(rowKey);
        foreach (var row in Rows.Where(r => r.RowKey == rowKey))
            row.IsWorking = busy;
    }

    private void SyncRows(List<ContainerRowViewModel> fresh)
    {
        Rows.Clear();
        foreach (var row in fresh)
        {
            row.IsWorking = _busyKeys.Contains(row.RowKey);
            Rows.Add(row);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        foreach (var row in Rows)
            row.IsVisible = query.Length == 0
                || row.Names.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Image.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.EngineShortName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
