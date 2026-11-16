using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DndHelper.App.Messages;
using DndHelper.App.Services;
using DndHelper.Core.Characters;
using DndHelper.Core.Encounters;
using DndHelper.Core.Models;
using DndHelper.Maps.Areas;
using DndHelper.Maps.Generation;
using DndHelper.Maps.Hexes;
using DndHelper.Maps.Maps;
using DndHelper.Maps.Navigation;
using DndHelper.Maps.Persistence;
using DndHelper.Maps.Tokens;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;

namespace DndHelper.App.ViewModels.Panels;

public enum MapTool
{
    Move,
    Paint,
    Erase,
    Area
}

/// <summary>The hex map: painting terrain, placing tokens, moving them within their speed, saving and loading.</summary>
public partial class MapPanelViewModel : Document
{
    private const int MaxCoordinate = 150;

    private readonly IFileDialogService _dialogs;
    private readonly ILogger<MapPanelViewModel> _logger;
    private readonly EncounterSession _session;
    private readonly AreaSession _area;

    private Creature? _lastCreature;
    private PlayerCharacter? _lastCharacter;

    // True between the first click (origin) and the second click (aim) of a cone, line or cube.
    private bool _areaPlacing;

    [ObservableProperty] private IReadOnlyCollection<Hex> _areaHexes = [];
    [ObservableProperty] private Hex? _areaOrigin;

    [ObservableProperty] private HexMap _map = CreateDefaultMap();
    [ObservableProperty] private int _revision;
    [ObservableProperty] private int _fitRequest;
    [ObservableProperty] private MapTool _selectedTool = MapTool.Move;
    [ObservableProperty] private Terrain _selectedTerrain = Terrain.Grass;
    [ObservableProperty] private IReadOnlyCollection<Hex> _highlight = [];
    [ObservableProperty] private IReadOnlyList<Hex>? _pathPreview;
    [ObservableProperty] private string _hoverText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _mapName = "Untitled map";

    [ObservableProperty] private decimal? _newRadius = 8;
    [ObservableProperty] private decimal? _newColumns = 20;
    [ObservableProperty] private decimal? _newRows = 14;

    [ObservableProperty] private MapStyle _selectedStyle = MapStyle.Dungeon;
    [ObservableProperty] private decimal? _seed = 1;

    [ObservableProperty] private string _tokenName = "";
    [ObservableProperty] private TokenTeam _selectedTeam = TokenTeam.Enemy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedToken))]
    [NotifyCanExecuteChangedFor(nameof(RemoveTokenCommand))]
    private string? _selectedTokenId;

    public ObservableCollection<MapToken> Tokens { get; } = [];

    public IReadOnlyList<MapTool> Tools { get; } = Enum.GetValues<MapTool>();
    public IReadOnlyList<Terrain> Terrains { get; } = Enum.GetValues<Terrain>();
    public IReadOnlyList<MapStyle> Styles { get; } = Enum.GetValues<MapStyle>();
    public IReadOnlyList<TokenTeam> Teams { get; } = Enum.GetValues<TokenTeam>();

    public bool HasSelectedToken => SelectedToken is not null;

    private MapToken? SelectedToken => Tokens.FirstOrDefault(t => t.Id == SelectedTokenId);

    public MapPanelViewModel(
        IFileDialogService dialogs,
        ILogger<MapPanelViewModel> logger,
        EncounterSession session,
        AreaSession area)
    {
        _dialogs = dialogs;
        _logger = logger;
        _session = session;
        _area = area;

        Id = "Map";
        Title = "Map";
        CanClose = false;

        _session.Changed += SyncFromEncounter;
        _area.SettingsChanged += RecomputeArea;

        WeakReferenceMessenger.Default.Register<MapPanelViewModel, CreatureSelectedMessage>(
            this, static (r, m) => r._lastCreature = m.Creature);
        WeakReferenceMessenger.Default.Register<MapPanelViewModel, CharacterSelectedMessage>(
            this, static (r, m) => r._lastCharacter = m.Character);

        StatusText = "Left click uses the tool. Right or middle drag pans. Wheel zooms.";
    }

    partial void OnMapNameChanged(string value) => Map.Name = value;

    partial void OnSelectedTokenIdChanged(string? value) => RefreshOverlay();

    private static HexMap CreateDefaultMap()
    {
        var map = HexMap.CreateHexagon(8, Terrain.Grass);
        map.Name = "Untitled map";
        return map;
    }

    private void Touch() => Revision++;

    private void RefreshOverlay()
    {
        PathPreview = null;

        Highlight = SelectedToken is { } token
            ? MovementRules.Reachable(Map, token, Tokens).Keys.Where(h => h != token.Hex).ToList()
            : [];

        RecomputeArea();
    }

    private void Replace(HexMap map, IEnumerable<MapToken> tokens)
    {
        SelectedTokenId = null;
        Tokens.Clear();
        foreach (var token in tokens) Tokens.Add(token);

        Map = map;
        MapName = map.Name;
        Highlight = [];
        PathPreview = null;
        FitRequest++;
        SyncFromEncounter();
        Touch();
    }

    [RelayCommand]
    private void NewHexMap()
    {
        var radius = (int)Math.Clamp(NewRadius ?? 8, 1, 40);
        var map = HexMap.CreateHexagon(radius, SelectedTerrain);
        map.Name = "Untitled map";
        Replace(map, []);
        StatusText = $"New hex map, radius {radius} ({map.Count} hexes).";
    }

    [RelayCommand]
    private void NewRectangleMap()
    {
        var columns = (int)Math.Clamp(NewColumns ?? 20, 1, 80);
        var rows = (int)Math.Clamp(NewRows ?? 14, 1, 80);
        var map = HexMap.CreateRectangle(columns, rows, SelectedTerrain);
        map.Name = "Untitled map";
        Replace(map, []);
        StatusText = $"New map, {columns} by {rows} ({map.Count} hexes).";
    }

    [RelayCommand]
    private void GenerateMap()
    {
        var seed = (int)Math.Clamp(Math.Round(Seed ?? 1), 0, int.MaxValue);
        var columns = (int)Math.Clamp(NewColumns ?? 20, MapGenerator.MinSize, MapGenerator.MaxSize);
        var rows = (int)Math.Clamp(NewRows ?? 14, MapGenerator.MinSize, MapGenerator.MaxSize);

        var map = MapGenerator.Generate(new GenerationOptions(SelectedStyle, columns, rows, seed));
        Replace(map, []);
        StatusText = $"Generated {map.Name}: {columns} by {rows}.";
    }

    [RelayCommand]
    private void RandomSeed()
    {
        Seed = Random.Shared.Next(1, 1_000_000);
        GenerateMap();
    }

    // existing:
    [RelayCommand]
    private void FitView() => FitRequest++;

    [RelayCommand]
    private void HexPressed(Hex? hex)
    {
        if (hex is not { } h) return;

        switch (SelectedTool)
        {
            case MapTool.Move:
                MoveClick(h);
                break;
            case MapTool.Area:
                AreaClick(h);
                break;
            default:
                EditHex(h);
                break;
        }
    }

    [RelayCommand]
    private void HexDragged(Hex? hex)
    {
        if (hex is { } h && SelectedTool is MapTool.Paint or MapTool.Erase)
            EditHex(h);
    }

    [RelayCommand]
    private void HexHovered(Hex? hex)
    {
        PathPreview = null;

        if (hex is not { } h)
        {
            HoverText = "";
            return;
        }

        if (_areaPlacing && SelectedTool == MapTool.Area && _area.Origin is { } origin && Map.Contains(h))
        {
            _area.Place(origin, h);
            RecomputeArea();
        }

        var terrain = Map.TerrainAt(h);
        var text = terrain is { } t ? $"{h} - {TerrainRules.DisplayName(t)}" : $"{h} - off map";

        if (SelectedToken is { } token)
        {
            text += $" - {token.Hex.DistanceFeetTo(h, Map.FeetPerHex)} ft. from {token.Name}";

            if (SelectedTool == MapTool.Move && h != token.Hex && MovementRules.PathTo(Map, token, h, Tokens) is { } path)
            {
                PathPreview = path.Path;
                text += $" (move {path.CostFeet(Map)} ft.)";
            }
        }

        HoverText = text;
    }

    private void EditHex(Hex hex)
    {
        if (Math.Abs(hex.Q) > MaxCoordinate || Math.Abs(hex.R) > MaxCoordinate) return;

        if (SelectedTool == MapTool.Paint)
        {
            if (Map.TerrainAt(hex) == SelectedTerrain) return;
            Map.Set(hex, SelectedTerrain);
        }
        else
        {
            if (Tokens.Any(t => t.Hex == hex) || !Map.Remove(hex)) return;
        }

        RefreshOverlay();
    }

    private void MoveClick(Hex hex)
    {
        if (Tokens.FirstOrDefault(t => t.Hex == hex) is { } clicked)
        {
            SelectedTokenId = clicked.Id;
            StatusText = $"{clicked.Name} selected. Speed {clicked.SpeedFeet} ft.";
            return;
        }

        if (SelectedToken is { } token && MovementRules.TryMove(Map, token, hex, Tokens, out var feet))
        {
            StatusText = $"{token.Name} moved {feet} ft.";
            RefreshOverlay();
            return;
        }

        SelectedTokenId = null;
    }

    private void AreaClick(Hex hex)
    {
        if (!Map.Contains(hex)) return;

        var directional = _area.Kind is AreaShapeKind.Cone or AreaShapeKind.Line or AreaShapeKind.Cube;

        if (_areaPlacing)
        {
            _area.Place(_area.Origin ?? hex, hex);
            _areaPlacing = false;
            StatusText = "Area placed. Click again to start a new one, or use Clear area.";
        }
        else
        {
            _area.Place(hex, hex);
            _areaPlacing = directional;
            StatusText = directional
                ? "Move the mouse to aim, then click to fix the direction."
                : "Area placed. Click again to move it, or use Clear area.";
        }

        RecomputeArea();
    }

    /// <summary>Recalculates the covered hexes and which tokens are inside, then redraws.</summary>
    private void RecomputeArea()
    {
        foreach (var token in Tokens)
            token.AreaState = TokenAreaState.None;

        if (_area.Template is not { } template)
        {
            AreaHexes = [];
            AreaOrigin = null;
            _area.SetTargets([]);
            Touch();
            return;
        }

        var targets = AreaTargeting.FindTargets(Map, template, Tokens, Map.FeetPerHex);

        foreach (var target in targets)
            target.Token.AreaState = target.Reached ? TokenAreaState.Hit : TokenAreaState.Blocked;

        AreaHexes = AreaShapes.Cover(template, Map.FeetPerHex).Where(Map.Contains).ToList();
        AreaOrigin = template.Origin;
        _area.SetTargets(targets);
        Touch();
    }

    /// <summary>Refreshes health bars and drops links to combatants that have left the encounter.</summary>
    private void SyncFromEncounter()
    {
        var encounter = _session.Encounter;

        foreach (var token in Tokens)
        {
            var participant = encounter.Find(token.ParticipantId);
            if (participant is null)
            {
                token.ParticipantId = null;
                token.HealthFraction = null;
                token.IsDown = false;
                continue;
            }

            var combatant = participant.Combatant;
            token.HealthFraction = combatant.MaxHp > 0 ? (double)combatant.CurrentHp / combatant.MaxHp : 0;
            token.IsDown = combatant.IsDead || combatant.CurrentHp <= 0;
        }

        RecomputeArea();
    }

    [RelayCommand]
    private void ClearArea()
    {
        _areaPlacing = false;
        _area.Clear();
        StatusText = "Area cleared.";
    }

    partial void OnSelectedToolChanged(MapTool value)
    {
        if (value != MapTool.Area) _areaPlacing = false;
    }

    /// <summary>Links existing tokens to combatants with the same name, and places a new token for each combatant still without one.</summary>
    [RelayCommand]
    private void PlaceCombatants()
    {
        var linked = Tokens.Select(t => t.ParticipantId).OfType<string>().ToHashSet();
        var occupied = Tokens.Select(t => t.Hex).ToHashSet();
        var near = SelectedToken?.Hex ?? Hex.Origin;
        var placed = 0;
        var matched = 0;

        foreach (var participant in _session.Encounter.Turns)
        {
            if (linked.Contains(participant.Id)) continue;

            var existing = Tokens.FirstOrDefault(t =>
                t.ParticipantId is null && string.Equals(t.Name, participant.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.ParticipantId = participant.Id;
                matched++;
                continue;
            }

            if (TokenPlacement.FindFreeHex(Map, occupied, near) is not { } hex)
            {
                StatusText = "There is no free hex to place the remaining combatants.";
                break;
            }

            occupied.Add(hex);
            Tokens.Add(new MapToken
            {
                Name = participant.Name,
                Hex = hex,
                Team = participant.Side switch
                {
                    Side.Party => TokenTeam.Player,
                    Side.Enemy => TokenTeam.Enemy,
                    _ => TokenTeam.Neutral
                },
                SpeedFeet = participant.Combatant.Speed,
                ParticipantId = participant.Id
            });
            placed++;
        }

        StatusText = $"Placed {placed} new token{(placed == 1 ? "" : "s")}, linked {matched} existing by name.";
        SyncFromEncounter();
    }

    [RelayCommand]
    private void AddToken()
        => AddTokenAt(TokenName, SelectedTeam, 30);

    [RelayCommand]
    private void AddCreatureToken()
    {
        if (_lastCreature is not { } creature)
        {
            StatusText = "Select a creature in the Creatures list first.";
            return;
        }

        AddTokenAt(creature.Name, TokenTeam.Enemy, creature.WalkSpeed ?? 30);
    }

    [RelayCommand]
    private void AddCharacterToken()
    {
        if (_lastCharacter is not { } character)
        {
            StatusText = "Select a character in the Characters list first.";
            return;
        }

        var speed = 30;
        try
        {
            speed = CharacterCalculator.Compute(character).Speed;
        }
        catch (ArgumentException)
        {
            // An incomplete character still gets a token at the default speed.
        }

        AddTokenAt(character.Name, TokenTeam.Player, speed);
    }

    private void AddTokenAt(string name, TokenTeam team, int speed)
    {
        var occupied = Tokens.Select(t => t.Hex).ToHashSet();
        var near = SelectedToken?.Hex ?? Hex.Origin;

        if (TokenPlacement.FindFreeHex(Map, occupied, near) is not { } hex)
        {
            StatusText = "There is no free hex to place a token on.";
            return;
        }

        var token = new MapToken
        {
            Name = TokenNaming.Unique(name, Tokens.Select(t => t.Name)),
            Hex = hex,
            Team = team,
            SpeedFeet = speed
        };

        Tokens.Add(token);
        SelectedTokenId = token.Id;
        StatusText = $"Added {token.Name}.";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedToken))]
    private void RemoveToken()
    {
        if (SelectedToken is not { } token) return;

        SelectedTokenId = null;
        Tokens.Remove(token);
        StatusText = $"Removed {token.Name}.";
        RecomputeArea();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var path = await _dialogs.PickSaveAsync("Save map", SafeFileName(Map.Name));
            if (path is null) return;

            await File.WriteAllTextAsync(path, MapSerializer.Serialize(Map, Tokens));
            StatusText = $"Saved {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save map");
            StatusText = $"Could not save the map: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var path = await _dialogs.PickOpenAsync("Open map");
            if (path is null) return;

            var document = MapSerializer.Deserialize(await File.ReadAllTextAsync(path));
            Replace(document.Map, document.Tokens);
            StatusText = $"Opened {Path.GetFileName(path)}.";
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to open map");
            StatusText = $"Could not open the map: {ex.Message}";
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return cleaned.Length == 0 ? "map" : cleaned;
    }
}
