using DndHelper.Core.Rules;
using FluentAssertions;

namespace DndHelper.Tests.Rules;

public class ConcentrationTests
{
    private static ConcentrationManager Manager(params int[] faces)
        => new(new SavingThrowResolver(TestCombatants.Dice(faces)));

    [Theory]
    [InlineData(1, 10)]
    [InlineData(20, 10)]
    [InlineData(21, 10)]
    [InlineData(30, 15)]
    [InlineData(100, 30)]
    public void RequiredDc_IsTenOrHalfDamage_CappedAt30(int damage, int expected)
        => ConcentrationManager.RequiredDc(damage).Should().Be(expected);

    [Fact]
    public void Begin_EndsPreviousConcentrationEffects()
    {
        var caster = TestCombatants.Player("Caster");
        var target = TestCombatants.Monster();
        var manager = Manager();
        target.Effects.Add(new ActiveEffect { Name = "Hold Person", SourceId = caster.Id, RequiresConcentration = true });
        caster.ConcentratingOn = "Hold Person";

        manager.Begin(caster, "Bless", [caster, target]);

        target.Effects.Should().BeEmpty();
        caster.ConcentratingOn.Should().Be("Bless");
    }

    [Fact]
    public void End_OnlyRemovesThisCastersConcentrationEffects()
    {
        var caster = TestCombatants.Player("Caster");
        var other = TestCombatants.Player("Other");
        var target = TestCombatants.Monster();
        target.Effects.Add(new ActiveEffect { Name = "Mine", SourceId = caster.Id, RequiresConcentration = true });
        target.Effects.Add(new ActiveEffect { Name = "Theirs", SourceId = other.Id, RequiresConcentration = true });
        target.Effects.Add(new ActiveEffect { Name = "Mine, no concentration", SourceId = caster.Id });

        var removed = Manager().End(caster, [caster, other, target]);

        removed.Should().Be(1);
        target.Effects.Select(e => e.Name).Should().BeEquivalentTo(["Theirs", "Mine, no concentration"]);
    }

    [Fact]
    public void Check_PassingSave_KeepsConcentration()
    {
        var caster = TestCombatants.Player();
        caster.ConcentratingOn = "Bless";

        var result = Manager(12).CheckAfterDamage(caster, 8, [caster]);

        result.Maintained.Should().BeTrue();
        result.RequiredDc.Should().Be(10);
        caster.ConcentratingOn.Should().Be("Bless");
    }

    [Fact]
    public void Check_FailingSave_EndsConcentrationAndEffects()
    {
        var caster = TestCombatants.Player();
        var ally = TestCombatants.Player("Ally");
        caster.ConcentratingOn = "Bless";
        ally.Effects.Add(new ActiveEffect { Name = "Bless", SourceId = caster.Id, RequiresConcentration = true });

        var result = Manager(4).CheckAfterDamage(caster, 8, [caster, ally]);

        result.Maintained.Should().BeFalse();
        caster.ConcentratingOn.Should().BeNull();
        ally.Effects.Should().BeEmpty();
    }

    [Fact]
    public void Check_HigherDamage_RaisesDc()
    {
        var caster = TestCombatants.Player(hp: 100);
        caster.ConcentratingOn = "Wall of Fire";

        var result = Manager(14).CheckAfterDamage(caster, 40, [caster]);

        result.RequiredDc.Should().Be(20);
        result.Maintained.Should().BeFalse();
    }

    [Fact]
    public void Check_WarCaster_RollsWithAdvantage()
    {
        var caster = TestCombatants.Player();
        caster.ConcentratingOn = "Bless";
        caster.ConcentrationAdvantage = true;

        var result = Manager(3, 18).CheckAfterDamage(caster, 4, [caster]);

        result.Save!.Mode.Should().Be(RollMode.Advantage);
        result.Maintained.Should().BeTrue();
    }

    [Fact]
    public void Check_Incapacitated_LosesConcentrationWithoutRolling()
    {
        var caster = TestCombatants.Player();
        caster.ConcentratingOn = "Bless";
        caster.Conditions.Add(Condition.Stunned);

        var result = Manager().CheckAfterDamage(caster, 4, [caster]);

        result.Maintained.Should().BeFalse();
        result.Save.Should().BeNull();
        caster.ConcentratingOn.Should().BeNull();
    }

    [Fact]
    public void Check_NotConcentrating_DoesNothing()
    {
        var result = Manager().CheckAfterDamage(TestCombatants.Player(), 10, []);

        result.WasConcentrating.Should().BeFalse();
    }
}

public class EffectProcessorTests
{
    private static EffectProcessor Processor(params int[] faces)
    {
        var dice = TestCombatants.Dice(faces);
        return new EffectProcessor(dice, new SavingThrowResolver(dice));
    }

    [Fact]
    public void Duration_CountsDownAtEndOfTurnAndExpires()
    {
        var target = TestCombatants.Monster();
        target.Effects.Add(new ActiveEffect { Name = "Shield of Faith", RemainingRounds = 2 });
        var processor = Processor();

        processor.EndTurn(target).Removed.Should().BeEmpty();
        target.Effects.Single().RemainingRounds.Should().Be(1);

        var result = processor.EndTurn(target);

        result.Removed.Should().ContainSingle();
        target.Effects.Should().BeEmpty();
    }

    [Fact]
    public void Duration_DoesNotTickAtStartOfTurn()
    {
        var target = TestCombatants.Monster();
        target.Effects.Add(new ActiveEffect { Name = "Haste", RemainingRounds = 1 });

        Processor().StartTurn(target);

        target.Effects.Should().HaveCount(1);
        target.Effects[0].RemainingRounds.Should().Be(1);
    }

    [Fact]
    public void EffectsWithoutDuration_NeverExpire()
    {
        var target = TestCombatants.Monster();
        target.Effects.Add(new ActiveEffect { Name = "Curse" });

        var processor = Processor();
        for (var i = 0; i < 5; i++) processor.EndTurn(target);

        target.Effects.Should().HaveCount(1);
    }

    [Fact]
    public void DamageOverTime_DamagesAtConfiguredTiming()
    {
        var target = TestCombatants.Monster(hp: 30);
        target.Effects.Add(new ActiveEffect
        {
            Name = "Acid Arrow",
            DamageOverTime = new DamageOverTime(DiceExpression.Parse("2d4"), DamageType.Acid, TurnTiming.EndOfTurn)
        });
        var processor = Processor(3, 4);

        processor.StartTurn(target);
        target.CurrentHp.Should().Be(30);

        var result = processor.EndTurn(target);

        target.CurrentHp.Should().Be(23);
        result.Damage.Should().ContainSingle();
    }

    [Fact]
    public void DamageOverTime_RespectsResistance()
    {
        var target = TestCombatants.Monster(hp: 30);
        target.Resistances.Always.Add(DamageType.Fire);
        target.Effects.Add(new ActiveEffect
        {
            Name = "Burning",
            DamageOverTime = new DamageOverTime(DiceExpression.Parse("1d10"), DamageType.Fire, TurnTiming.StartOfTurn)
        });

        Processor(9).StartTurn(target);

        target.CurrentHp.Should().Be(26);
    }

    [Fact]
    public void RepeatSave_Success_EndsEffect()
    {
        var target = TestCombatants.Monster();
        target.Effects.Add(new ActiveEffect
        {
            Name = "Hold Person",
            GrantedConditions = { Condition.Paralyzed },
            RepeatSave = new RepeatSave(Ability.Wisdom, 13, TurnTiming.EndOfTurn)
        });

        var result = Processor(15).EndTurn(target);

        result.Removed.Should().ContainSingle();
        target.HasCondition(Condition.Paralyzed).Should().BeFalse();
    }

    [Fact]
    public void RepeatSave_Failure_KeepsEffect()
    {
        var target = TestCombatants.Monster();
        target.Effects.Add(new ActiveEffect
        {
            Name = "Hold Person",
            GrantedConditions = { Condition.Paralyzed },
            RepeatSave = new RepeatSave(Ability.Wisdom, 13, TurnTiming.EndOfTurn)
        });

        Processor(5).EndTurn(target);

        target.HasCondition(Condition.Paralyzed).Should().BeTrue();
    }
}

public class SpellResolverTests
{
    private static (SpellResolver Resolver, ConcentrationManager Concentration) Build(params int[] faces)
    {
        var dice = TestCombatants.Dice(faces);
        var saves = new SavingThrowResolver(dice);
        var concentration = new ConcentrationManager(saves);
        return (new SpellResolver(dice, saves, concentration), concentration);
    }

    private static SpellEffectRequest Fireball(Combatant? caster = null, int dc = 15) => new()
    {
        Name = "Fireball",
        Caster = caster,
        Damage = DiceExpression.Parse("8d6"),
        DamageType = DamageType.Fire,
        SaveAbility = Ability.Dexterity,
        SaveDc = dc
    };

    // Eight d6 faces summing to 28, followed by the saves.
    private static readonly int[] EightDice = [4, 4, 4, 4, 3, 3, 3, 3];

    [Fact]
    public void Plan_RollsDamageOnce_AndHalvesOnSave()
    {
        var failer = TestCombatants.Monster("Failer", hp: 50);
        var saver = TestCombatants.Monster("Saver", hp: 50);
        var (resolver, _) = Build([.. EightDice, 5, 18]);

        var plan = resolver.Plan(Fireball(), [failer, saver]);

        plan.DamageRoll!.Total.Should().Be(28);
        plan.Targets[0].DamageAmount.Should().Be(28);
        plan.Targets[1].DamageAmount.Should().Be(14);
        plan.Targets[1].Save!.Success.Should().BeTrue();
    }

    [Fact]
    public void Plan_DoesNotChangeAnyone_UntilCommitted()
    {
        var target = TestCombatants.Monster(hp: 50);
        var (resolver, _) = Build([.. EightDice, 5]);

        var plan = resolver.Plan(Fireball(), [target]);

        target.CurrentHp.Should().Be(50);
        plan.Targets[0].Preview!.HpLost.Should().Be(28);

        plan.Commit([target]);
        target.CurrentHp.Should().Be(22);
    }

    [Fact]
    public void Plan_AppliesResistanceAfterHalving()
    {
        var target = TestCombatants.Monster(hp: 50);
        target.Resistances.Always.Add(DamageType.Fire);
        var (resolver, _) = Build([.. EightDice, 18]);

        var plan = resolver.Plan(Fireball(), [target]);
        plan.Commit([target]);

        // Saved for 14, then resistance halves it again to 7.
        target.CurrentHp.Should().Be(43);
    }

    [Fact]
    public void Plan_Evasion_NoDamageOnSuccess_HalfOnFailure()
    {
        var saver = TestCombatants.Monster("Saver", hp: 50);
        var failer = TestCombatants.Monster("Failer", hp: 50);
        saver.HasEvasion = true;
        failer.HasEvasion = true;
        var (resolver, _) = Build([.. EightDice, 18, 4]);

        var plan = resolver.Plan(Fireball(), [saver, failer]);

        plan.Targets[0].DamageAmount.Should().Be(0);
        plan.Targets[1].DamageAmount.Should().Be(14);
    }

    [Fact]
    public void Plan_NoDamageOnSave_WhenNotHalf()
    {
        var target = TestCombatants.Monster(hp: 50);
        var (resolver, _) = Build([.. EightDice, 18]);
        var request = new SpellEffectRequest
        {
            Damage = DiceExpression.Parse("8d6"),
            DamageType = DamageType.Thunder,
            SaveAbility = Ability.Constitution,
            SaveDc = 10,
            HalfDamageOnSave = false
        };

        resolver.Plan(request, [target]).Targets[0].DamageAmount.Should().Be(0);
    }

    [Fact]
    public void Plan_CoverBonus_CanTurnAFailureIntoASuccess()
    {
        var target = TestCombatants.Monster(hp: 50);
        var (resolver, _) = Build([.. EightDice, 8]);
        var request = new SpellEffectRequest
        {
            Damage = DiceExpression.Parse("8d6"),
            DamageType = DamageType.Fire,
            SaveAbility = Ability.Dexterity,
            SaveDc = 10,
            CoverByTargetId = new Dictionary<string, CoverType> { [target.Id] = CoverType.Half }
        };

        var planned = resolver.Plan(request, [target]).Targets[0];

        planned.Save!.Success.Should().BeTrue();
        planned.DamageAmount.Should().Be(14);
    }

    [Fact]
    public void Plan_TotalCover_SkipsTarget()
    {
        var target = TestCombatants.Monster();
        var (resolver, _) = Build(EightDice);
        var request = new SpellEffectRequest
        {
            Damage = DiceExpression.Parse("8d6"),
            SaveAbility = Ability.Dexterity,
            SaveDc = 10,
            CoverByTargetId = new Dictionary<string, CoverType> { [target.Id] = CoverType.Total }
        };

        var planned = resolver.Plan(request, [target]).Targets[0];

        planned.Skipped.Should().BeTrue();
        planned.DamageAmount.Should().Be(0);
    }

    [Fact]
    public void Commit_AppliesEffectOnlyToThoseWhoFail()
    {
        var failer = TestCombatants.Monster("Failer");
        var saver = TestCombatants.Monster("Saver");
        var caster = TestCombatants.Player("Caster");
        var (resolver, _) = Build(5, 18);
        var request = new SpellEffectRequest
        {
            Name = "Hold Person",
            Caster = caster,
            SaveAbility = Ability.Wisdom,
            SaveDc = 14,
            Effect = new ActiveEffect
            {
                Name = "Hold Person",
                RequiresConcentration = true,
                RemainingRounds = 10,
                GrantedConditions = { Condition.Paralyzed }
            }
        };

        resolver.Plan(request, [failer, saver]).Commit([caster, failer, saver]);

        failer.HasCondition(Condition.Paralyzed).Should().BeTrue();
        failer.Effects.Single().SourceId.Should().Be(caster.Id);
        saver.Effects.Should().BeEmpty();
        caster.ConcentratingOn.Should().Be("Hold Person");
    }

    [Fact]
    public void Commit_RespectsConditionImmunity()
    {
        var target = TestCombatants.Monster();
        target.ConditionImmunities.Add(Condition.Paralyzed);
        var (resolver, _) = Build(3);
        var request = new SpellEffectRequest
        {
            SaveAbility = Ability.Wisdom,
            SaveDc = 15,
            Effect = new ActiveEffect { Name = "Hold", GrantedConditions = { Condition.Paralyzed } }
        };

        resolver.Plan(request, [target]).Commit([target]);

        target.HasCondition(Condition.Paralyzed).Should().BeFalse();
    }

    [Fact]
    public void Commit_DamagedConcentratingTarget_RollsConcentrationSave()
    {
        var wizard = TestCombatants.Player("Wizard", hp: 60);
        wizard.ConcentratingOn = "Bless";
        var (resolver, _) = Build([.. EightDice, 5, 4]);

        var committed = resolver.Plan(Fireball(), [wizard]).Commit([wizard]);

        committed.ConcentrationChecks.Should().ContainSingle();
        committed.ConcentrationChecks[0].RequiredDc.Should().Be(14);
        committed.ConcentrationChecks[0].Maintained.Should().BeFalse();
        wizard.ConcentratingOn.Should().BeNull();
    }

    [Fact]
    public void Commit_Undo_RestoresEveryone()
    {
        var caster = TestCombatants.Player("Caster");
        var target = TestCombatants.Monster(hp: 50);
        var ally = TestCombatants.Player("Ally");
        caster.ConcentratingOn = "Bless";
        ally.Effects.Add(new ActiveEffect { Name = "Bless", SourceId = caster.Id, RequiresConcentration = true });
        var (resolver, _) = Build([.. EightDice, 5]);
        var request = new SpellEffectRequest
        {
            Name = "Flaming Sphere",
            Caster = caster,
            Damage = DiceExpression.Parse("8d6"),
            DamageType = DamageType.Fire,
            SaveAbility = Ability.Dexterity,
            SaveDc = 15,
            Effect = new ActiveEffect { Name = "Burning", RequiresConcentration = true }
        };

        var committed = resolver.Plan(request, [target]).Commit([caster, target, ally]);

        target.CurrentHp.Should().Be(22);
        ally.Effects.Should().BeEmpty();
        caster.ConcentratingOn.Should().Be("Flaming Sphere");

        committed.Undo();

        target.CurrentHp.Should().Be(50);
        target.Effects.Should().BeEmpty();
        ally.Effects.Should().ContainSingle();
        caster.ConcentratingOn.Should().Be("Bless");
    }

    [Fact]
    public void Commit_Twice_Throws()
    {
        var target = TestCombatants.Monster();
        var (resolver, _) = Build([.. EightDice, 5]);
        var plan = resolver.Plan(Fireball(), [target]);

        plan.Commit([target]);

        var act = () => plan.Commit([target]);
        act.Should().Throw<InvalidOperationException>();
    }
}