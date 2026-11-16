using DndHelper.Core.Rules;

namespace DndHelper.Core.Encounters;

public enum Side
{
    Party,
    Enemy,
    Neutral
}

/// <summary>A combatant's place in an encounter: initiative and what they have used this turn.</summary>
public sealed class Participant
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public required Combatant Combatant { get; init; }
    public Side Side { get; init; }
    public int InitiativeBonus { get; init; }
    public int Initiative { get; internal set; }

    public bool ActionUsed { get; set; }
    public bool BonusActionUsed { get; set; }
    public bool ReactionUsed { get; set; }

    internal int Sequence { get; set; }

    public string Name => Combatant.Name;

    internal void ResetTurn()
    {
        ActionUsed = false;
        BonusActionUsed = false;
        ReactionUsed = false;
    }
}

public sealed record LogEntry(int Round, string Text)
{
    public override string ToString() => Round > 0 ? $"[R{Round}] {Text}" : Text;
}
