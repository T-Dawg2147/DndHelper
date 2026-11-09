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

public sealed record LevelOption(string Label, int? Value);

/// <summary>Searchable, filterable list of spells. Selecting one shows it in the Spell Details panel.</summary>
public partial class SpellBrowserPanelViewModel : Tool
{
    private const string AnySchool = "Any school";
    private const string AnyClass = "Any class";

    private readonly IContentStore _store;
    private readonly ILogger<SpellBrowserPanelViewModel> _logger;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private LevelOption _selectedLevel;
    [ObservableProperty] private string _selectedSchool = AnySchool;
    [ObservableProperty] private string _selectedClass = AnyClass;
    [ObservableProperty] private SpellSummary? _selectedSpell;
    [ObservableProperty] private IReadOnlyList<SpellSummary> _results = [];
    [ObservableProperty] private string _resultText = "";

    public IReadOnlyList<LevelOption> Levels { get; } =
    [
        new("Any level", null),
        new("Cantrip", 0),
        .. Enumerable.Range(1, 9).Select(i => new LevelOption($"Level {i}", i))
    ];

    public ObservableCollection<string> Schools { get; } = [AnySchool];
    public ObservableCollection<string> Classes { get; } = [AnyClass];

    public SpellBrowserPanelViewModel(IContentStore store, ILogger<SpellBrowserPanelViewModel> logger)
    {
        _store = store;
        _logger = logger;
        _selectedLevel = Levels[0];

        Id = "SpellBrowser";
        Title = "Spells";
        CanClose = false;

        WeakReferenceMessenger.Default.Register<SpellBrowserPanelViewModel, ContentUpdatedMessage>(
            this, static (r, msg) => Dispatcher.UIThread.Post(() => _ = r.ReloadAsync()));

        _ = ReloadAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = SearchAsync(debounce: true);
    partial void OnSelectedLevelChanged(LevelOption value) => _ = SearchAsync();
    partial void OnSelectedSchoolChanged(string value) => _ = SearchAsync();
    partial void OnSelectedClassChanged(string value) => _ = SearchAsync();
    partial void OnSelectedSpellChanged(SpellSummary? value) => _ = SelectAsync(value);

    private async Task ReloadAsync()
    {
        try
        {
            var schools = await _store.GetSpellSchoolsAsync();
            var classes = await _store.GetSpellClassesAsync();
            var currentSchool = SelectedSchool;
            var currentClass = SelectedClass;

            Schools.Clear();
            Schools.Add(AnySchool);
            foreach (var s in schools) Schools.Add(s);

            Classes.Clear();
            Classes.Add(AnyClass);
            foreach (var c in classes) Classes.Add(c);

            SelectedSchool = Schools.Contains(currentSchool) ? currentSchool : AnySchool;
            SelectedClass = Classes.Contains(currentClass) ? currentClass : AnyClass;
            await SearchAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load spells");
        }
    }

    private async Task SearchAsync(bool debounce = false)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            if (debounce) await Task.Delay(250, cts.Token);

            var query = new SpellQuery(
                SearchText,
                SelectedLevel.Value,
                SelectedSchool == AnySchool ? null : SelectedSchool,
                SelectedClass == AnyClass ? null : SelectedClass);

            var results = await _store.SearchSpellsAsync(query, cts.Token);
            if (cts.IsCancellationRequested) return;

            Results = results;
            ResultText = $"{results.Count} spell{(results.Count == 1 ? "" : "s")}";
        }
        catch (OperationCanceledException)
        {
            // A newer search superseded this one.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spell search failed");
        }
    }

    private async Task SelectAsync(SpellSummary? summary)
    {
        if (summary is null) return;

        try
        {
            var spell = await _store.GetSpellAsync(summary.Key);
            if (spell is not null)
                WeakReferenceMessenger.Default.Send(new SpellSelectedMessage(spell));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load spell {Key}", summary.Key);
        }
    }
}