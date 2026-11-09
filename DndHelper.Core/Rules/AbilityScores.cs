namespace DndHelper.Core.Rules;

public sealed class AbilityScores
{
    private readonly int[] _scores = [10, 10, 10, 10, 10, 10];

    public AbilityScores()
    {
    }

    public AbilityScores(int strength, int dexterity, int constitution, int intelligence, int wisdom, int charisma)
    {
        this[Ability.Strength] = strength;
        this[Ability.Dexterity] = dexterity;
        this[Ability.Constitution] = constitution;
        this[Ability.Intelligence] = intelligence;
        this[Ability.Wisdom] = wisdom;
        this[Ability.Charisma] = charisma;
    }

    public int this[Ability ability]
    {
        get => _scores[(int)ability];
        set => _scores[(int)ability] = Math.Clamp(value, 1, 30);
    }

    public int Modifier(Ability ability) => ModifierFor(this[ability]);

    public static int ModifierFor(int score) => (int)Math.Floor((score - 10) / 2.0);

    public AbilityScores Clone()
    {
        var copy = new AbilityScores();
        foreach (var ability in AbilityExtensions.All)
            copy[ability] = this[ability];
        return copy;
    }

    public static AbilityScores FromDictionary(IReadOnlyDictionary<string, int> values)
    {
        var scores = new AbilityScores();
        foreach (var (key, value) in values)
        {
            if (AbilityExtensions.TryParse(key, out var ability))
                scores[ability] = value;
        }

        return scores;
    }
}