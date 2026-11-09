using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Docking;
using DndHelper.App.Messages;
using DndHelper.Core;
using DndHelper.Core.Services;
using Dock.Model.Controls;
using Microsoft.Extensions.Logging;

namespace DndHelper.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DockFactory _factory;
    private readonly IContentStore _store;
    private readonly IContentSyncService _sync;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty] private IRootDock? _layout;
    [ObservableProperty] private string _statusText = "Ready";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SyncContentCommand))]
    private bool _isBusy;

    public string Title => $"{AppInfo.Name} - {AppInfo.RulesEdition}";

    public MainWindowViewModel(
        DockFactory factory,
        IContentStore store,
        IContentSyncService sync,
        ILogger<MainWindowViewModel> logger)
    {
        _factory = factory;
        _store = store;
        _sync = sync;
        _logger = logger;

        BuildLayout();
        _ = SyncIfEmptyAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSync))]
    private async Task SyncContentAsync()
    {
        IsBusy = true;
        try
        {
            var progress = new Progress<string>(message => StatusText = message);
            var result = await _sync.SyncAsync(progress);

            WeakReferenceMessenger.Default.Send(new ContentUpdatedMessage());
            StatusText = $"Loaded {result.CreatureCount} creatures and {result.SpellCount} spells.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content sync failed");
            StatusText = $"Content download failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSync() => !IsBusy;

    [RelayCommand]
    private void ResetLayout()
    {
        if (Layout is { } old && old.Close.CanExecute(null))
            old.Close.Execute(null);

        BuildLayout();
    }

    private async Task SyncIfEmptyAsync()
    {
        try
        {
            if (await _store.CountCreaturesAsync() == 0 && await _store.CountSpellsAsync() == 0)
            {
                StatusText = "No local content found. Downloading SRD data...";
                await SyncContentAsync();
            }
            else
            {
                StatusText = "Ready";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial content check failed");
            StatusText = $"Content check failed: {ex.Message}";
        }
    }

    private void BuildLayout()
    {
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        Layout = layout;
    }
}