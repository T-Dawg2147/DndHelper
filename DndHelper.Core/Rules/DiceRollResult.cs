namespace DndHelper.Core.Rules;

public sealed record DiceTermRoll(DiceTerm Term, IReadOnlyList<int> Values)
{
    public int Subtotal => Term.Sign * Values.Sum();
}

public record DiceRollResult(
    DiceExpression Expression,
    IReadOnlyList<DiceTermRoll> Terms,
    int Modifier,
    int Total,
    bool IsCritical)
{
    public override string ToString()
    {
        var dice = string.Join(" ", Terms.Select(t => $"{t.Term.Count}d{t.Term.Sides}[{string.Join(",", t.Values)}]"));
        var modifier = Modifier == 0 ? "" : Modifier > 0 ? $" +{Modifier}" : $" {Modifier}";
        return $"{dice}{modifier} = {Total}".Trim();
    }
}