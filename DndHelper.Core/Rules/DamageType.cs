using System.Text.RegularExpressions;

namespace DndHelper.Core.Rules;

public enum DamageType
{
    Acid,
    Bludgeoning,
    Cold,
    Fire,
    Force,
    Lighting,
    Necrotic,
    Piercing,
    Poison,
    Psychic,
    Radiant,
    Slashing,
    Thunder
}

public static class DamageTypeParser
{
    public static bool TryParse(string? text, out DamageType type)
        => Enum.TryParse(text?.Trim(), ignoreCase: true, out type) && Enum.IsDefined(type);

    public static IReadOnlyList<DamageType> FindAll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        return Enum.GetValues<DamageType>()
            .Where(t => Regex.IsMatch(text, $@"\b{t}\b", RegexOptions.IgnoreCase))
            .ToList();
    }

    public static bool IsPhysical(DamageType type)
        => type is DamageType.Bludgeoning or DamageType.Piercing or DamageType.Slashing;
}