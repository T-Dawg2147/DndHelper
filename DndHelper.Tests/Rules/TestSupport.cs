using DndHelper.Core.Rules;

namespace DndHelper.Tests.Rules;

/// <summary>A random source that returns scripted die faces in order, so tests are fully deterministic.</summary>
public sealed class SequenceRandom(params int[] values) : IRandomSource
{
    private readonly Queue<int> _values = new(values);

    public int Next(int minInclusive, int maxExclusive)
    {
        if (_values.Count == 0)
            throw new InvalidOperationException("SequenceRandom ran out of scripted values.");

        var value = _values.Dequeue();
        if (value < minInclusive || value >= maxExclusive)
            throw new InvalidOperationException($"Scripted value {value} is outside [{minInclusive}, {maxExclusive}).");

        return value;
    }
}

public static class TestCombatants
{
    public static Combatant Monster(string name = "Goblin", int hp = 20, int ac = 12) => new()
    {
        Name = name,
        MaxHp = hp,
        CurrentHp = hp,
        BaseArmorClass = ac
    };

    public static Combatant Player(string name = "Hero", int hp = 20, int ac = 15) => new()
    {
        Name = name,
        IsPlayerCharacter = true,
        MaxHp = hp,
        CurrentHp = hp,
        BaseArmorClass = ac
    };

    public static DiceRoller Dice(params int[] faces) => new(new SequenceRandom(faces));
}