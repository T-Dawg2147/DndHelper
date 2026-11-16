using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.App.Services;
using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.ViewModels.Panels;

/// <summary>Running log of rolls, damage and effects, fed by the encounter tracker.</summary>
public partial class BattleLogPanelViewModel : Tool
{
    private readonly IFileDialogService _dialogs;

    public ObservableCollection<string> Lines { get; } = [];

    public BattleLogPanelViewModel(IFileDialogService dialogs)
    {
        _dialogs = dialogs;

        Id = "BattleLog";
        Title = "Battle Log";
        CanClose = false;

        WeakReferenceMessenger.Default.Register<BattleLogPanelViewModel, BattleLogMessage>(
            this, static (r, m) => r.Lines.Add(m.Text));
        WeakReferenceMessenger.Default.Register<BattleLogPanelViewModel, ClearBattleLogMessage>(
            this, static (r, msg) => r.Lines.Clear());
    }

    [RelayCommand]
    private void Clear() => Lines.Clear();

    [RelayCommand]
    private async Task ExportAsync()
    {
        try
        {
            var path = await _dialogs.PickSaveAsync("Export battle log", $"battle-log-{DateTime.Now:yyyyMMdd-HHmm}", FileKind.Text);
            if (path is null) return;

            await File.WriteAllLinesAsync(path, Lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Lines.Add($"Could not export the log: {ex.Message}");
        }
    }
}
