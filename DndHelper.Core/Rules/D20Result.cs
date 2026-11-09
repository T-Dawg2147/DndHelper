namespace DndHelper.Core.Rules;

public sealed record D20Result(int Natural, int? Discarded, RollMode Mode, int Modifier)
{
    public int Total => Natural + Modifier;
    public bool IsNatural20 => Natural == 20;
    public bool IsNatural1 => Natural == 1;

    public override string ToString()
    {
        var kept = Discarded is { } d ? $"{Natural} (dropped {d})" : $"{Natural}";
        var modifier = Modifier == 0 ? "" : Modifier > 0 ? $" +{Modifier}" : $" {Modifier}";
        return $"d20 {kept}{modifier} = {Total}";
    }
}