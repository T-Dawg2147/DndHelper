namespace DndHelper.Core.Rules;

public sealed class DamageTraits
{
    public HashSet<DamageType> Always { get; private set; } = [];

    public HashSet<DamageType> NonmagicalOnly { get; private set; } = [];

    public bool Applies(DamageType type, bool isMagical)
        => Always.Contains(type) || (!isMagical && NonmagicalOnly.Contains(type));

    public DamageTraits Clone() => new()
    {
        Always = [..Always],
        NonmagicalOnly = [..NonmagicalOnly]
    };

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