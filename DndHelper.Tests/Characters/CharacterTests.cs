using DndHelper.Core.Characters;
using DndHelper.Core.Rules;
using FluentAssertions;
using DndHelper.Tests.Rules;

namespace DndHelper.Tests.Characters;

public class AbilityScoreGeneratorTests
{
    [Theory]
    [InlineData(8, 0)]
    [InlineData(13, 5)]
    [InlineData(14, 7)]
    [InlineData(15, 9)]
    [InlineData(7, -1)]
    [InlineData(16, -1)]
    public void PointBuyCost_MatchesTable(int score, int expected)
        => AbilityScoreGenerator.PointBuyCost(score).Should().Be(expected);

    [Fact]
    public void PointBuy_StandardSpread_Costs27()
    {
        AbilityScoreGenerator.IsValidPointBuy([15, 15, 15, 8, 8, 8], out var spent).Should().BeTrue();
        spent.Should().Be(27);
    }

    [Fact]
    public void PointBuy_OverBudget_IsInvalid()
        => AbilityScoreGenerator.IsValidPointBuy([15, 15, 15, 15, 8, 8], out _).Should().BeFalse();

    [Fact]
    public void PointBuy_OutOfRange_IsInvalid()
        => AbilityScoreGenerator.IsValidPointBuy([16, 8, 8, 8, 8, 8], out _).Should().BeFalse();

    [Fact]
    public void StandardArray_AcceptsAnyOrder_AndRejectsOthers()
    {
        AbilityScoreGenerator.IsStandardArray([8, 10, 12, 13, 14, 15]).Should().BeTrue();
        AbilityScoreGenerator.IsStandardArray([15, 15, 13, 12, 10, 8]).Should().BeFalse();
    }

    [Fact]
    public void RollScores_DropsTheLowestOfFourDice()
    {
        // Each set of four dice is listed highest to lowest so the last die is the dropped one.
        var roller = TestCombatants.Dice(
            6, 5, 4, 1,
            3, 3, 3, 3,
            6, 6, 6, 1,
            2, 2, 2, 1,
            5, 4, 3, 2,
            1, 1, 1, 1);

        AbilityScoreGenerator.RollScores(roller).Should().Equal(15, 9, 18, 6, 12, 3);
    }

    [Fact]
    public void RollScores_AreWithinRange_ForRandomRolls()
    {
        var scores = AbilityScoreGenerator.RollScores(new DiceRoller(new SystemRandomSource(42)));

        scores.Should().HaveCount(6).And.OnlyContain(s => s >= 3 && s <= 18);
    }
}

public class CatalogTests
{
    [Fact]
    public void Classes_AllTwelveHaveTwoSavesAndSkills()
    {
        ClassCatalog.All.Should().HaveCount(12);
        ClassCatalog.All.Should().OnlyContain(c => c.SavingThrows.Count == 2);
        ClassCatalog.All.Should().OnlyContain(c => c.SkillOptions.Count >= c.SkillChoiceCount);
        ClassCatalog.All.Select(c => c.Name).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("Barbarian", 12)]
    [InlineData("Fighter", 10)]
    [InlineData("Rogue", 8)]
    [InlineData("Wizard", 6)]
    public void Classes_HaveCorrectHitDie(string name, int die)
        => ClassCatalog.Find(name)!.HitDie.Should().Be(die);

    [Fact]
    public void Classes_FindIsCaseInsensitive()
        => ClassCatalog.Find("wIzArD").Should().NotBeNull();

    [Fact]
    public void Backgrounds_AllGiveTwoSkillsAndThreeAbilities()
    {
        BackgroundCatalog.All.Should().HaveCount(4);
        BackgroundCatalog.All.Should().OnlyContain(b => b.Skills.Count == 2 && b.AbilityOptions.Count == 3);
    }

    [Fact]
    public void Species_AreAllPresent()
        => SpeciesCatalog.Names.Should().BeEquivalentTo(
            ["Dragonborn", "Dwarf", "Elf", "Gnome", "Goliath", "Halfling", "Human", "Orc", "Tiefling"]);

    [Theory]
    [InlineData(Skill.Athletics, Ability.Strength)]
    [InlineData(Skill.SleightOfHand, Ability.Dexterity)]
    [InlineData(Skill.Arcana, Ability.Intelligence)]
    [InlineData(Skill.Perception, Ability.Wisdom)]
    [InlineData(Skill.Persuasion, Ability.Charisma)]
    public void Skills_UseTheRightAbility(Skill skill, Ability ability)
        => skill.AbilityOf().Should().Be(ability);

    [Fact]
    public void Skills_HaveReadableNames()
    {
        Skill.SleightOfHand.DisplayName().Should().Be("Sleight of Hand");
        Skill.AnimalHandling.DisplayName().Should().Be("Animal Handling");
    }

    [Theory]
    [InlineData(1, new[] { 2, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(5, new[] { 4, 3, 2, 0, 0, 0, 0, 0, 0 })]
    [InlineData(11, new[] { 4, 3, 3, 3, 2, 1, 0, 0, 0 })]
    [InlineData(20, new[] { 4, 3, 3, 3, 3, 2, 2, 1, 1 })]
    public void SpellSlots_Full_MatchTable(int level, int[] expected)
        => SpellSlotTable.For(CasterType.Full, level).Should().Equal(expected);

    [Theory]
    [InlineData(1, new[] { 2, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(5, new[] { 4, 2, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData(20, new[] { 4, 3, 3, 3, 2, 0, 0, 0, 0 })]
    public void SpellSlots_Half_MatchTable(int level, int[] expected)
        => SpellSlotTable.For(CasterType.Half, level).Should().Equal(expected);

    [Fact]
    public void SpellSlots_NonCasters_HaveNone()
        => SpellSlotTable.For(CasterType.None, 10).Should().OnlyContain(s => s == 0);
}

public class CharacterCalculatorTests
{
    // A 1st-level Soldier Human Fighter: STR 15+2, DEX 14+1, CON 13, INT 12, WIS 10, CHA 8.
    private static PlayerCharacter Fighter(int level = 1) => new()
    {
        Name = "Brannock",
        Species = "Human",
        ClassName = "Fighter",
        Background = "Soldier",
        Level = level,
        Method = ScoreMethod.StandardArray,
        BaseScores = new Dictionary<Ability, int>
        {
            [Ability.Strength] = 15, [Ability.Dexterity] = 14, [Ability.Constitution] = 13,
            [Ability.Intelligence] = 12, [Ability.Wisdom] = 10, [Ability.Charisma] = 8
        },
        BackgroundBonuses = new Dictionary<Ability, int> { [Ability.Strength] = 2, [Ability.Dexterity] = 1 },
        ClassSkills = [Skill.Perception, Skill.Survival]
    };

    [Fact]
    public void FinalScores_AddBackgroundBonuses()
    {
        var scores = CharacterCalculator.FinalScores(Fighter());

        scores[Ability.Strength].Should().Be(17);
        scores[Ability.Dexterity].Should().Be(15);
        scores[Ability.Constitution].Should().Be(13);
    }

    [Fact]
    public void FinalScores_AreCappedAt20()
    {
        var character = Fighter();
        character.BaseScores[Ability.Strength] = 19;

        CharacterCalculator.FinalScores(character)[Ability.Strength].Should().Be(20);
    }

    [Theory]
    [InlineData(10, 1, 1, 11)]
    [InlineData(10, 5, 1, 39)]
    [InlineData(6, 1, 2, 8)]
    [InlineData(6, 3, 2, 8 + 2 * 6)]
    [InlineData(12, 1, -3, 9)]
    [InlineData(6, 4, -5, 1 + 3 * 1)]
    public void MaxHitPoints_FollowsFixedHpRule(int hitDie, int level, int con, int expected)
        => CharacterCalculator.MaxHitPoints(hitDie, level, con).Should().Be(expected);

    [Fact]
    public void Compute_FighterLevel1_HasExpectedNumbers()
    {
        var sheet = CharacterCalculator.Compute(Fighter());

        sheet.ProficiencyBonus.Should().Be(2);
        sheet.MaxHp.Should().Be(11);
        sheet.ArmorClass.Should().Be(12);
        sheet.Initiative.Should().Be(2);
        sheet.Speed.Should().Be(30);
        sheet.SaveBonus(Ability.Strength).Should().Be(5);
        sheet.SaveBonus(Ability.Constitution).Should().Be(3);
        sheet.SaveBonus(Ability.Wisdom).Should().Be(0);
        sheet.SkillBonuses[Skill.Athletics].Should().Be(5);
        sheet.SkillBonuses[Skill.Perception].Should().Be(2);
        sheet.PassivePerception.Should().Be(12);
        sheet.SpellSaveDc.Should().BeNull();
    }

    [Fact]
    public void Compute_IncludesBackgroundSkillsAndClassSkills()
    {
        var sheet = CharacterCalculator.Compute(Fighter());

        sheet.SkillProficiencies.Should().BeEquivalentTo(
            [Skill.Athletics, Skill.Intimidation, Skill.Perception, Skill.Survival]);
    }

    [Fact]
    public void Compute_Level5_RaisesProficiencyAndHp()
    {
        var sheet = CharacterCalculator.Compute(Fighter(5));

        sheet.ProficiencyBonus.Should().Be(3);
        sheet.MaxHp.Should().Be(39);
    }

    [Fact]
    public void Compute_ArmorClassOverride_Wins()
    {
        var character = Fighter();
        character.ArmorClassOverride = 18;

        CharacterCalculator.Compute(character).ArmorClass.Should().Be(18);
    }

    [Fact]
    public void Compute_Wizard_HasSpellcastingAndSlots()
    {
        var wizard = new PlayerCharacter
        {
            Name = "Mira",
            Species = "Elf",
            ClassName = "Wizard",
            Background = "Sage",
            Level = 3,
            BaseScores = new Dictionary<Ability, int>
            {
                [Ability.Strength] = 8, [Ability.Dexterity] = 14, [Ability.Constitution] = 13,
                [Ability.Intelligence] = 15, [Ability.Wisdom] = 12, [Ability.Charisma] = 10
            },
            BackgroundBonuses = new Dictionary<Ability, int> { [Ability.Intelligence] = 2, [Ability.Wisdom] = 1 },
            ClassSkills = [Skill.Investigation, Skill.Medicine]
        };

        var sheet = CharacterCalculator.Compute(wizard);

        sheet.SpellAttackBonus.Should().Be(2 + 3);
        sheet.SpellSaveDc.Should().Be(8 + 2 + 3);
        sheet.SpellSlots.Should().Equal(4, 2, 0, 0, 0, 0, 0, 0, 0);
        sheet.DarkvisionFeet.Should().Be(60);
        sheet.Properties.Should().Contain(p => p.Label == "Spell Slots" && p.Value == "L1: 4, L2: 2");
    }

    [Fact]
    public void Compute_UnknownClass_Throws()
    {
        var character = Fighter();
        character.ClassName = "Artificer";

        var act = () => CharacterCalculator.Compute(character);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Properties_ListsKeyStats()
    {
        var properties = CharacterCalculator.Compute(Fighter()).Properties;

        properties.Should().Contain(p => p.Label == "Hit Points" && p.Value == "11");
        properties.Should().Contain(p => p.Label == "Armor Class" && p.Value == "12");
        properties.Should().Contain(p => p.Label == "Saving Throws" && p.Value.Contains("STR +5*"));
    }

    [Fact]
    public void ToCombatant_CopiesDerivedStats()
    {
        var combatant = CharacterCombatantFactory.ToCombatant(Fighter());

        combatant.IsPlayerCharacter.Should().BeTrue();
        combatant.MaxHp.Should().Be(11);
        combatant.CurrentHp.Should().Be(11);
        combatant.BaseArmorClass.Should().Be(12);
        combatant.Abilities[Ability.Strength].Should().Be(17);
        combatant.SaveModifier(Ability.Strength).Should().Be(5);
    }

    [Fact]
    public void ToCombatant_DwarfResistsPoison()
    {
        var character = Fighter();
        character.Species = "Dwarf";

        var combatant = CharacterCombatantFactory.ToCombatant(character);

        combatant.Resistances.Always.Should().Contain(DamageType.Poison);
    }
}

public class CharacterValidatorTests
{
    private static PlayerCharacter Valid() => new()
    {
        Name = "Brannock",
        Species = "Human",
        ClassName = "Fighter",
        Background = "Soldier",
        Method = ScoreMethod.StandardArray,
        BaseScores = new Dictionary<Ability, int>
        {
            [Ability.Strength] = 15, [Ability.Dexterity] = 14, [Ability.Constitution] = 13,
            [Ability.Intelligence] = 12, [Ability.Wisdom] = 10, [Ability.Charisma] = 8
        },
        BackgroundBonuses = new Dictionary<Ability, int> { [Ability.Strength] = 2, [Ability.Dexterity] = 1 },
        ClassSkills = [Skill.Perception, Skill.Survival]
    };

    [Fact]
    public void ValidCharacter_HasNoErrors()
        => CharacterValidator.Validate(Valid()).Should().BeEmpty();

    [Fact]
    public void MissingName_IsAnError()
    {
        var character = Valid();
        character.Name = "  ";

        CharacterValidator.Validate(character).Should().Contain("Enter a name.");
    }

    [Fact]
    public void UnknownChoices_AreErrors()
    {
        var character = Valid();
        character.Species = "";
        character.ClassName = "Artificer";
        character.Background = "Hermit";

        CharacterValidator.Validate(character).Should().HaveCount(3);
    }

    [Fact]
    public void Level_MustBeInRange()
    {
        var character = Valid();
        character.Level = 21;

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("Level");
    }

    [Fact]
    public void StandardArray_RejectsOtherScores()
    {
        var character = Valid();
        character.BaseScores[Ability.Strength] = 16;

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("Standard array");
    }

    [Fact]
    public void PointBuy_OverBudget_IsAnError()
    {
        var character = Valid();
        character.Method = ScoreMethod.PointBuy;
        foreach (var ability in AbilityExtensions.All)
            character.BaseScores[ability] = 15;

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("limit is 27");
    }

    [Fact]
    public void Manual_RejectsExtremeScores()
    {
        var character = Valid();
        character.Method = ScoreMethod.Manual;
        character.BaseScores[Ability.Strength] = 25;

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("between 3 and 18");
    }

    [Fact]
    public void BackgroundBonuses_AcceptSpread()
    {
        var character = Valid();
        character.BackgroundBonuses = new Dictionary<Ability, int>
        {
            [Ability.Strength] = 1, [Ability.Dexterity] = 1, [Ability.Constitution] = 1
        };

        CharacterValidator.Validate(character).Should().BeEmpty();
    }

    [Fact]
    public void BackgroundBonuses_RejectWrongShape()
    {
        var character = Valid();
        character.BackgroundBonuses = new Dictionary<Ability, int> { [Ability.Strength] = 2 };

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("+2 and +1");
    }

    [Fact]
    public void BackgroundBonuses_MustUseTheBackgroundsAbilities()
    {
        var character = Valid();
        character.BackgroundBonuses = new Dictionary<Ability, int> { [Ability.Charisma] = 2, [Ability.Strength] = 1 };

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("can only raise");
    }

    [Fact]
    public void ClassSkills_MustMatchCountAndList()
    {
        var character = Valid();
        character.ClassSkills = [Skill.Perception];

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("exactly 2");

        character.ClassSkills = [Skill.Perception, Skill.Arcana];
        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("not on the Fighter");
    }

    [Fact]
    public void ClassSkills_CannotRepeatBackgroundSkills()
    {
        var character = Valid();
        character.ClassSkills = [Skill.Athletics, Skill.Perception];

        CharacterValidator.Validate(character).Should().ContainSingle().Which.Should().Contain("already gives");
    }

    [Fact]
    public void ClassSkills_CannotBeDuplicated()
    {
        var character = Valid();
        character.ClassSkills = [Skill.Perception, Skill.Perception];

        CharacterValidator.Validate(character).Should().Contain("Each skill can only be chosen once.");
    }
}