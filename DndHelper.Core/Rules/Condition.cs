namespace DndHelper.Core.Rules;

public enum Condition
{
    Blinded,
    Charmed,
    Deafened,
    Frightened,
    Grappled,
    Incapacitated,
    Invisible,
    Paralyzed,
    Petrified,
    Poisoned,
    Prone,
    Restrained,
    Stunned,
    Unconscious
}

public static class ConditionRules
{
    /// <summary>Conditions that stop a creature moving (Speed 0).</summary>
    public static readonly IReadOnlySet<Condition> ZeroSpeed = new HashSet<Condition>
    {
        Condition.Grappled, Condition.Restrained, Condition.Paralyzed,
        Condition.Stunned, Condition.Unconscious, Condition.Petrified
    };

    /// <summary>Returns the given conditions plus any conditions they include (e.g. Paralyzed includes Incapacitated).</summary>
    public static HashSet<Condition> WithImplied(IEnumerable<Condition> conditions)
    {
        var result = new HashSet<Condition>(conditions);

        if (result.Overlaps([Condition.Paralyzed, Condition.Petrified, Condition.Stunned, Condition.Unconscious]))
            result.Add(Condition.Incapacitated);

        if (result.Contains(Condition.Unconscious))
            result.Add(Condition.Prone);

        return result;
    }
}