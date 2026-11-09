namespace DndHelper.Core.Rules;

public sealed class DiceRoller(IRandomSource random)
{
    public int RollDie(int sides) => random.Next(1, sides + 1);

    public DiceRoleResult Roll(DiceExpression expression, bool critical = false)
    {

    }

    public DiceRoleResult Roll(string notation, bool critical = false)
        => Roll(DiceExpression.Parse(notation), critical);

    public D20Result RollD20(RollMode mode = RollMode.Normal, int modifier = 0)
    {
        
    }

}