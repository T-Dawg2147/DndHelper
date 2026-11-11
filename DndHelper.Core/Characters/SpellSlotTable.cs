namespace DndHelper.Core.Characters;

/// <summary>Spell slots per spell level (index 0 is 1st-level slots) for each character level.</summary>
public static class SpellSlotTable
{
    private static readonly int[][] Full = Parse(
        "2", "3", "4 2", "4 3", "4 3 2", "4 3 3", "4 3 3 1", "4 3 3 2", "4 3 3 3 1", "4 3 3 3 2",
        "4 3 3 3 2 1", "4 3 3 3 2 1", "4 3 3 3 2 1 1", "4 3 3 3 2 1 1", "4 3 3 3 2 1 1 1", "4 3 3 3 2 1 1 1",
        "4 3 3 3 2 1 1 1 1", "4 3 3 3 3 1 1 1 1", "4 3 3 3 3 2 1 1 1", "4 3 3 3 3 2 2 1 1");

    private static readonly int[][] Half = Parse(
        "2", "2", "3", "3", "4 2", "4 2", "4 3", "4 3", "4 3 2", "4 3 2",
        "4 3 3", "4 3 3", "4 3 3 1", "4 3 3 1", "4 3 3 2", "4 3 3 2",
        "4 3 3 3 1", "4 3 3 3 1", "4 3 3 3 2", "4 3 3 3 2");

    /// <summary>Always nine entries. Pact Magic (Warlock) works differently and is not modelled yet, so it returns no slots.</summary>
    public static int[] For(CasterType caster, int level)
    {
        var result = new int[9];
        var table = caster switch
        {
            CasterType.Full => Full,
            CasterType.Half => Half,
            _ => null
        };

        if (table is null) return result;

        var row = table[Math.Clamp(level, 1, 20) - 1];
        row.CopyTo(result, 0);
        return result;
    }

    private static int[][] Parse(params string[] rows)
        => rows.Select(r => r.Split(' ').Select(int.Parse).ToArray()).ToArray();
}