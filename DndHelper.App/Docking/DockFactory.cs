using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using DndHelper.App.ViewModels.Panels;
using Microsoft.Extensions.DependencyInjection;

namespace DndHelper.App.Docking;

/// <summary>
/// Builds the default dock layout and wires up floating (undocked) windows.
/// Any panel can be dragged out of the main window onto another monitor.
/// </summary>
public class DockFactory(IServiceProvider services) : Factory
{
    public override IRootDock CreateLayout()
    {
        var creatures = ActivatorUtilities.CreateInstance<CreatureBrowserPanelViewModel>(services);
        var spells = ActivatorUtilities.CreateInstance<SpellBrowserPanelViewModel>(services);
        var creatureDetails = ActivatorUtilities.CreateInstance<CreatureDetailsPanelViewModel>(services);
        var spellDetails = ActivatorUtilities.CreateInstance<SpellDetailsPanelViewModel>(services);
        var characters = ActivatorUtilities.CreateInstance<CharacterListPanelViewModel>(services);
        var builder = ActivatorUtilities.CreateInstance<CharacterBuilderPanelViewModel>(services);
        var editor = ActivatorUtilities.CreateInstance<CreatureEditorPanelViewModel>(services);
        var map = new MapPanelViewModel();
        var tracker = new EncounterTrackerPanelViewModel();
        var log = new BattleLogPanelViewModel();

        var browserDock = new ToolDock
        {
            Id = "BrowserDock",
            Proportion = 0.24,
            ActiveDockable = creatures,
            VisibleDockables = CreateList<IDockable>(creatures, spells, characters),
            Alignment = Alignment.Left
        };

        var mapDock = new DocumentDock
        {
            Id = "MapDock",
            IsCollapsable = false,
            ActiveDockable = map,
            VisibleDockables = CreateList<IDockable>(map, builder, editor),
            CanCreateDocument = false
        };

        var detailsDock = new ToolDock
        {
            Id = "DetailsDock",
            Proportion = 0.3,
            ActiveDockable = creatureDetails,
            VisibleDockables = CreateList<IDockable>(creatureDetails, spellDetails, tracker),
            Alignment = Alignment.Right
        };

        var logDock = new ToolDock
        {
            Id = "LogDock",
            Proportion = 0.2,
            ActiveDockable = log,
            VisibleDockables = CreateList<IDockable>(log),
            Alignment = Alignment.Bottom
        };

        var middle = new ProportionalDock
        {
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                mapDock,
                new ProportionalDockSplitter(),
                logDock)
        };

        var main = new ProportionalDock
        {
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                browserDock,
                new ProportionalDockSplitter(),
                middle,
                new ProportionalDockSplitter(),
                detailsDock)
        };

        var root = CreateRootDock();
        root.IsCollapsable = false;
        root.ActiveDockable = main;
        root.DefaultDockable = main;
        root.VisibleDockables = CreateList<IDockable>(main);
        return root;
    }

    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };

        base.InitLayout(layout);
    }
}