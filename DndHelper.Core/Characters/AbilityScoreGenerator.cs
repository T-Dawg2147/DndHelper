using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public enum ScoreMethod
{
    StandardArray,
    PointBuy,
    Roll,
    Manual
}

public static class AbilityScoreGenerator
{
    public static readonly IReadOnlyList<int> StandardArray = [15, 14, 13, 12, 10, 8];

    public const int PointBuyBudget = 27;

    /// <summary>Point cost of a score from 8 to 15, or -1 when the score is outside the point buy range.</summary>
    public static int PointBuyCost(int score) => score switch
    {
        8 => 0,
        9 => 1,
        10 => 2,
        11 => 3,
        12 => 4,
        13 => 5,
        14 => 7,
        15 => 9,
        _ => -1
    };

    public static bool IsValidPointBuy(IEnumerable<int> scores, out int spent)
    {
        spent = 0;
        foreach (var score in scores)
        {
            var cost = PointBuyCost(score);
            if (cost < 0) return false;
            spent += cost;
        }
        return spent <= PointBuyBudget;
    }

    public static bool IsStandardArray(IEnumerable<int> scores)
        => scores.OrderByDescending(s => s).SequenceEqual(StandardArray);

    /// <summary>Rolls 4d6 and drops the lowest die, six times.</summary>
    public static IReadOnlyList<int> RollScores(DiceRoller roller)
    {
        var scores = new List<int>(6);
        for (var i = 0; i < 6; i++)
        {
            var dice = Enumerable.Range(0, 4).Select(_ => roller.RollDie(6)).OrderDescending().ToList();
            scores.Add(dice.Take(3).Sum());
        }
        return scores;
    }
}