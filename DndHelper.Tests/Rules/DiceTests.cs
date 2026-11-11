using DndHelper.Core.Rules;
using FluentAssertions;

namespace DndHelper.Tests.Rules;

public class DiceTests
{
    [Theory]
    [InlineData("1d20", "1d20")]
    [InlineData("d6", "1d6")]
    [InlineData("2D6+3", "2d6+3")]
    [InlineData(" 8d6 ", "8d6")]
    [InlineData("1d8 + 1d4 - 2", "1d8+1d4-2")]
    [InlineData("1d20-1d4", "1d20-1d4")]
    [InlineData("5", "5")]
    [InlineData("-1d4", "-1d4")]
    public void Parse_NormalizesNotation(string input, string expected)
        => DiceExpression.Parse(input).ToString().Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("2d")]
    [InlineData("2d6+")]
    [InlineData("0d6")]
    [InlineData("1d1")]
    [InlineData("101d6")]
    [InlineData("2d6 3")]
    public void Parse_RejectsInvalidNotation(string input)
    {
        DiceExpression.TryParse(input, out _).Should().BeFalse();
        var act = () => DiceExpression.Parse(input);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void MinMaxAverage_AreCorrect()
    {
        var expression = DiceExpression.Parse("2d6+3");

        expression.Min.Should().Be(5);
        expression.Max.Should().Be(15);
        expression.Average.Should().Be(10);
    }

    [Fact]
    public void MinMax_HandleSubtractedDice()
    {
        var expression = DiceExpression.Parse("1d20-1d4");

        expression.Min.Should().Be(-3);
        expression.Max.Should().Be(19);
    }

    [Fact]
    public void Roll_SumsDiceAndModifier()
    {
        var result = TestCombatants.Dice(3, 4).Roll("2d6+3");

        result.Total.Should().Be(10);
        result.Terms.Single().Values.Should().Equal(3, 4);
    }

    [Fact]
    public void Roll_SubtractsNegativeTerms()
    {
        TestCombatants.Dice(15, 3).Roll("1d20-1d4").Total.Should().Be(12);
    }

    [Fact]
    public void Roll_Critical_DoublesDiceButNotModifier()
    {
        var result = TestCombatants.Dice(1, 2, 3, 4).Roll("2d6+3", critical: true);

        result.Total.Should().Be(13);
        result.Terms.Single().Values.Should().HaveCount(4);
        result.IsCritical.Should().BeTrue();
    }

    [Fact]
    public void RollD20_Normal_RollsOneDie()
    {
        var result = TestCombatants.Dice(14).RollD20(RollMode.Normal, 5);

        result.Natural.Should().Be(14);
        result.Discarded.Should().BeNull();
        result.Total.Should().Be(19);
    }

    [Fact]
    public void RollD20_Advantage_KeepsHigher()
    {
        var result = TestCombatants.Dice(7, 16).RollD20(RollMode.Advantage);

        result.Natural.Should().Be(16);
        result.Discarded.Should().Be(7);
    }

    [Fact]
    public void RollD20_Disadvantage_KeepsLower()
    {
        var result = TestCombatants.Dice(7, 16).RollD20(RollMode.Disadvantage);

        result.Natural.Should().Be(7);
        result.Discarded.Should().Be(16);
    }

    [Fact]
    public void SeededRandom_IsReproducible()
    {
        var first = new DiceRoller(new SystemRandomSource(1234)).Roll("10d20").Total;
        var second = new DiceRoller(new SystemRandomSource(1234)).Roll("10d20").Total;

        first.Should().Be(second);
    }

    [Theory]
    [InlineData(true, true, RollMode.Normal)]
    [InlineData(false, false, RollMode.Normal)]
    [InlineData(true, false, RollMode.Advantage)]
    [InlineData(false, true, RollMode.Disadvantage)]
    public void RollMode_Combine_CancelsMixedSources(bool advantage, bool disadvantage, RollMode expected)
        => RollModeExtensions.Combine(advantage, disadvantage).Should().Be(expected);
}

public class AbilityAndProficiencyTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(8, -1)]
    [InlineData(9, -1)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(14, 2)]
    [InlineData(20, 5)]
    [InlineData(30, 10)]
    public void Modifier_UsesFlooredHalf(int score, int expected)
        => AbilityScores.ModifierFor(score).Should().Be(expected);

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(9, 4)]
    [InlineData(13, 5)]
    [InlineData(17, 6)]
    [InlineData(20, 6)]
    public void ProficiencyForLevel_MatchesTable(int level, int expected)
        => Proficiency.ForLevel(level).Should().Be(expected);

    [Theory]
    [InlineData(0, 2)]
    [InlineData(0.25, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(9, 4)]
    [InlineData(17, 6)]
    [InlineData(21, 7)]
    [InlineData(30, 9)]
    public void ProficiencyForChallengeRating_MatchesTable(double cr, int expected)
        => Proficiency.ForChallengeRating(cr).Should().Be(expected);

    [Theory]
    [InlineData("dexterity", Ability.Dexterity)]
    [InlineData("STR", Ability.Strength)]
    [InlineData(" Wis ", Ability.Wisdom)]
    public void Ability_TryParse_AcceptsNamesAndAbbreviations(string text, Ability expected)
    {
        AbilityExtensions.TryParse(text, out var ability).Should().BeTrue();
        ability.Should().Be(expected);
    }

    [Fact]
    public void FromDictionary_ReadsScoresAndDefaultsTheRest()
    {
        var scores = AbilityScores.FromDictionary(new Dictionary<string, int> { ["strength"] = 18, ["DEX"] = 14 });

        scores[Ability.Strength].Should().Be(18);
        scores[Ability.Dexterity].Should().Be(14);
        scores[Ability.Charisma].Should().Be(10);
    }
}