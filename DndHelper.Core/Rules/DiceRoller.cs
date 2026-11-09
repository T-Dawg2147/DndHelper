namespace DndHelper.Core.Rules;

public sealed class DiceRoller(IRandomSource random)
{
    public int RollDie(int sides) => random.Next(1, sides + 1);

    public DiceRollResult Roll(DiceExpression expression, bool critical = false)
    {
        var effective = critical ? expression.WithCriticalDice() : expression;
        var termRolls = new List<DiceTermRoll>(effective.Terms.Count);
        var total = effective.Modifier;

        foreach (var term in effective.Terms)
        {
            var values = new List<int>(term.Count);
            for (var i = 0; i < term.Count; i++)
                values.Add(RollDie(term.Sides));
            
            var roll = new DiceTermRoll(term, values);
            termRolls.Add(roll);
            total += roll.Subtotal;
        }
        
        return new DiceRollResult(effective, termRolls, effective.Modifier, total, critical);
    }

    public DiceRollResult Roll(string notation, bool critical = false)
        => Roll(DiceExpression.Parse(notation), critical);

    public D20Result RollD20(RollMode mode = RollMode.Normal, int modifier = 0)
    {
        var first = RollDie(20);
        if (mode == RollMode.Normal)
            return new D20Result(first, null, mode, modifier);

        var second = RollDie(20);
        var keepFirst = mode == RollMode.Advantage ? first >= second : first <= second;
        return keepFirst
            ? new D20Result(first, second, mode, modifier)
            : new D20Result(second, first, mode, modifier);
    }

}