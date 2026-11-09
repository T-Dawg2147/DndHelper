namespace DndHelper.Core.Rules;

public enum Ability
{
    Strength,
    Dexterity,
    Constitution,
    Intelligence,
    Wisdom,
    Charisma
}

public static class AbilityExtensions
{
    public static readonly IReadOnlyList<Ability> All = Enum.GetValues<Ability>();

    public static string Abbreviation(this Ability ability) => ability switch
    {
        Ability.Strength => "STR",
        Ability.Dexterity => "DEX",
        Ability.Constitution => "CON",
        Ability.Intelligence => "INT",
        Ability.Wisdom => "WIS",
        Ability.Charisma => "CHA",
        _ => ability.ToString()
    };

    public static bool TryParse(string? text, out Ability ability)
    {
        ability = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();
        foreach (var candidate in All)
        {
            if (value.Equals(candidate.ToString(), StringComparison.OrdinalIgnoreCase)
                || value.Equals(candidate.Abbreviation(), StringComparison.OrdinalIgnoreCase))
            {
                ability = candidate;
                return true;
            }
        }

        return false;
    }
}