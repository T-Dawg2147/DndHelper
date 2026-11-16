namespace DndHelper.Core.Rules;

/// <summary>A set of damage types a creature resists, is immune to, or is vulnerable to.</summary>
public sealed class DamageTraits
{
    /// <summary>Types that always apply.</summary>
    public HashSet<DamageType> Always { get; private set; } = [];

    /// <summary>Types that only apply to damage that is not magical (e.g. "from nonmagical attacks").</summary>
    public HashSet<DamageType> NonmagicalOnly { get; private set; } = [];

    public bool Applies(DamageType type, bool isMagical)
        => Always.Contains(type) || (!isMagical && NonmagicalOnly.Contains(type));

    public DamageTraits Clone() => new()
    {
        Always = [.. Always],
        NonmagicalOnly = [.. NonmagicalOnly]
    };

    /// <summary>
    /// Parses stat block text such as "Fire", "Cold; Bludgeoning, Piercing, and Slashing from Nonmagical Attacks".
    /// A "nonmagical" qualifier only ever limits the physical damage types in its segment.
    /// </summary>
    public static DamageTraits Parse(IEnumerable<string> entries)
    {
        var traits = new DamageTraits();
        var text = string.Join(", ", entries);

        foreach (var segment in text.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var nonmagical = segment.Contains("nonmagical", StringComparison.OrdinalIgnoreCase)
                             || segment.Contains("non-magical", StringComparison.OrdinalIgnoreCase);

            foreach (var type in DamageTypeParser.FindAll(segment))
            {
                if (nonmagical && DamageTypeParser.IsPhysical(type))
                    traits.NonmagicalOnly.Add(type);
                else
                    traits.Always.Add(type);
            }
        }

        return traits;
    }
}
