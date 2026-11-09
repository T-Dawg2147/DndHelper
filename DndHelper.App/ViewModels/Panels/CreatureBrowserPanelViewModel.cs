using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Models;
using DndHelper.Core.Services;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;

namespace DndHelper.App.ViewModels.Panels;

public partial class CreatureBrowserPanelViewModel : Tool
{
    private const string AnyType = "Any type";

    private readonly IContentStore _store;
    private readonly ILogger<CreatureBrowserPanelViewModel> _logger;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedType = AnyType;
    [ObservableProperty] private decimal? _minChallengeRating;
    [ObservableProperty] private decimal? _maxChallengeRating;
    [ObservableProperty] private CreatureSummary? _selectedCreature;
    [ObservableProperty] private IReadOnlyList<CreatureSummary> _results = [];
    [ObservableProperty] private string _resultText = string.Empty;

    public ObservableCollection<string> Types { get; } = [AnyType];

    public CreatureBrowserPanelViewModel(IContentStore store, ILogger<CreatureBrowserPanelViewModel> logger)
    {
        _store = store;
        _logger = logger;

        Id = "CreatureBrowser";
        Title = "Creatures";
        CanClose = false;
        
        WeakReferenceMessenger.Default.Register<CreatureBrowserPanelViewModel, ContentUpdatedMessage>(
            this, static (r, msg) => Dispatcher.UIThread.Post(() => _ = r.ReloadAsync()));

        _ = ReloadAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(debounce: true);
    partial void OnSelectedTypeChanged(string value) => _ = SearchAsync();
    partial void OnMinChallengeRatingChanged(decimal? value) => _ = SearchAsync(debounce: true);
    partial void OnMaxChallengeRatingChanged(decimal? value) => _ = SearchAsync(debounce: true);
    partial void OnSelectedCreatureChanged(CreatureSummary? value) => _ = SelectAsync(value);

    private async Task ReloadAsync()
    {
        try
        {
            var types = await _store.GetCreatureTypesAsync();
            var current = SelectedType;

            Types.Clear();
            Types.Add(AnyType);
            foreach (var type in types) Types.Add(type);

            SelectedType = Types.Contains(current) ? current : AnyType;
            await SearchAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load creatures");
        }
    }

    private async Task SearchAsync(bool debounce = false)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            if (debounce) await Task.Delay(250, cts.Token);

            var query = new CreatureQuery(
                SearchText,
                SelectedType == AnyType ? null : SelectedType,
                (double?)MinChallengeRating,
                (double?)MaxChallengeRating);

            var results = await _store.SearchCreaturesAsync(query, cts.Token);
            if (cts.IsCancellationRequested) return;

            Results = results;
            ResultText = $"{results.Count} creature{(results.Count == 1 ? "" : "s")}";
        }
        catch (OperationCanceledException)
        {
            // A newer search took priority over this one; throw
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Creature search failed.");
        }
    }

    private async Task SelectAsync(CreatureSummary? summary)
    {
        if (summary is null) return;

        try
        {
            var creature = await _store.GetCreatureAsync(summary.Key);
            if (creature is not null)
                WeakReferenceMessenger.Default.Send(new CreatureSelectedMessaged(creature));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load creature {Key}", summary.Key);
        }
    }
}