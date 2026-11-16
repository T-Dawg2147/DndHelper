using DndHelper.Maps.Areas;
using DndHelper.Maps.Hexes;

namespace DndHelper.App.Services;

/// <summary>
/// The area effect currently being placed. The spell resolver sets its shape and size, the map sets where it is,
/// and the map publishes which tokens it covers.
/// </summary>
public sealed class AreaSession
{
    public AreaShapeKind Kind { get; private set; } = AreaShapeKind.Sphere;
    public int SizeFeet { get; private set; } = 20;
    public int WidthFeet { get; private set; } = 5;

    public Hex? Origin { get; private set; }
    public Hex? Aim { get; private set; }

    public IReadOnlyList<AreaTarget> Targets { get; private set; } = [];

    public AreaTemplate? Template
        => Origin is { } origin ? new AreaTemplate(Kind, origin, Aim ?? origin, SizeFeet, WidthFeet) : null;

    /// <summary>Raised when the shape, size or position changed. The map listens and recomputes the covered hexes.</summary>
    public event Action? SettingsChanged;

    /// <summary>Raised when the list of covered tokens was recomputed. The spell resolver listens.</summary>
    public event Action? TargetsChanged;

    public void Configure(AreaShapeKind kind, int sizeFeet, int widthFeet)
    {
        if (Kind == kind && SizeFeet == sizeFeet && WidthFeet == widthFeet) return;

        Kind = kind;
        SizeFeet = Math.Max(0, sizeFeet);
        WidthFeet = Math.Max(1, widthFeet);
        SettingsChanged?.Invoke();
    }

    /// <summary>Moves the area without raising <see cref="SettingsChanged"/>; the caller (the map) recomputes itself.</summary>
    public void Place(Hex origin, Hex aim)
    {
        Origin = origin;
        Aim = aim;
    }

    public void Clear()
    {
        Origin = null;
        Aim = null;
        SettingsChanged?.Invoke();
    }

    public void SetTargets(IReadOnlyList<AreaTarget> targets)
    {
        Targets = targets;
        TargetsChanged?.Invoke();
    }
}
