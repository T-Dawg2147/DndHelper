using DndHelper.App.ViewModels.Panels;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace DndHelper.App.Docking;

public class DockFactory : Factory
{
    public override IRootDock CreateLayout()
    {
        var map = new MapPanelViewModel();
        var details = new CreatureDetailsPanelViewModel();
        var tracker = new EncounterTrackerPanelViewModel();
        var log = new BattleLogPanelViewModel();

        var detailsDock = new ToolDock()
        {
            Id = "DetailsDock",
            Proportion = 0.22,
            ActiveDockable = details,
            VisibleDockables = CreateList<IDockable>(details),
            Alignment = Alignment.Left
        };

        var mapDock = new DocumentDock
        {
            Id = "MapDock",
            IsCollapsable = false,
            ActiveDockable = map,
            VisibleDockables = CreateList<IDockable>(map),
            CanCreateDocument = false
        };

        var trackerDock = new ToolDock
        {
            Id = "TrackerDock",
            Proportion = 0.22,
            ActiveDockable = tracker,
            VisibleDockables = CreateList<IDockable>(tracker),
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
                detailsDock,
                new ProportionalDockSplitter(),
                middle,
                new ProportionalDockSplitter(),
                trackerDock)
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