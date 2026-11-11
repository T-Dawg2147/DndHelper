using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.Core.Characters;
using DndHelper.Core.Services;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;

namespace DndHelper.App.ViewModels.Panels;

public partial class CharacterListPanelViewModel : Tool
{
    private readonly ICharacterStore _store;
    private readonly ILogger<CharacterListPanelViewModel> _logger;
    private bool _suppressSelection;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private PlayerCharacter? _selectedCharacter;

    public ObservableCollection<PlayerCharacter> Characters { get; } = [];

    public CharacterListPanelViewModel(ICharacterStore store, ILogger<CharacterListPanelViewModel> logger)
    {
        _store = store;
        _logger = logger;

        Id = "CharacterList";
        Title = "Characters";
        CanClose = false;
        
        WeakReferenceMessenger.Default.Register<CharacterListPanelViewModel, CharactersChangedMessage>(
            this, static (r, msg) => Dispatcher.UIThread.Post(() => _ = r.ReloadAsync(msg.SelectId)));

        _ = ReloadAsync(null);
    }

    partial void OnSelectedCharacterChanged(PlayerCharacter? value)
    {
        if (!_suppressSelection && value is not null)
            WeakReferenceMessenger.Default.Send(new CharacterSelectedMessage(value));
    }

    [RelayCommand]
    private void New()
    {
        _suppressSelection = true;
        SelectedCharacter = null;
        _suppressSelection = false;
        WeakReferenceMessenger.Default.Send(new NewCharacterMessage());
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedCharacter is not { } character) return;

        try
        {
            await _store.DeleteAsync(character.Id);
            WeakReferenceMessenger.Default.Send(new NewCharacterMessage());
            await ReloadAsync(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete character {Id}", character.Id);
        }
    }

    private bool CanDelete() => SelectedCharacter is not null;

    private async Task ReloadAsync(string? selectId)
    {
        try
        {
            var characters = await _store.GetAllAsync();

            _suppressSelection = true;
            Characters.Clear();
            foreach (var character in characters) Characters.Add(character);
            SelectedCharacter = selectId is null ? null : Characters.FirstOrDefault(c => c.Id == selectId);
            _suppressSelection = false;
        }
        catch (Exception ex)
        {
            _suppressSelection = false;
            _logger.LogError(ex, "Failed to load charcters");
        }
    }
}