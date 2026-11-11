using DndHelper.Core.Models;
using DndHelper.Core.Rules;
using FluentAssertions;

namespace DndHelper.Tests.Rules;

public class SavingThrowTests
{
    [Fact]
    public void Save_AddsAbilityModifierAndProficiency()
    {
        var target = TestCombatants.Monster();
        target.Abilities[Ability.Dexterity] = 14;
        target.SaveProficiencies.Add(Ability.Dexterity);
        target.ProficiencyBonus = 3;

        var result = new SavingThrowResolver(TestCombatants.Dice(10)).Resolve(target, Ability.Dexterity, 15);

        result.Roll!.Total.Should().Be(15);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Save_BelowDc_Fails()
    {
        var result = new SavingThrowResolver(TestCombatants.Dice(9))
            .Resolve(TestCombatants.Monster(), Ability.Wisdom, 10);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Save_NaturalTwenty_DoesNotAutoSucceed()
    {
        var target = TestCombatants.Monster();
        target.Abilities[Ability.Wisdom] = 1;
        target.Effects.Add(new ActiveEffect { SaveBonus = -5 });

        var result = new SavingThrowResolver(TestCombatants.Dice(20)).Resolve(target, Ability.Wisdom, 25);

        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData(Condition.Paralyzed)]
    [InlineData(Condition.Stunned)]
    [InlineData(Condition.Unconscious)]
    [InlineData(Condition.Petrified)]
    public void Save_StrengthAndDexterity_AutoFailWhenDisabled(Condition condition)
    {
        var target = TestCombatants.Monster();
        target.Conditions.Add(condition);
        var resolver = new SavingThrowResolver(TestCombatants.Dice());

        resolver.Resolve(target, Ability.Strength, 1).AutoFailed.Should().BeTrue();
        resolver.Resolve(target, Ability.Dexterity, 1).Success.Should().BeFalse();
    }

    [Fact]
    public void Save_OtherAbilities_AreRolledWhileParalyzed()
    {
        var target = TestCombatants.Monster();
        target.Conditions.Add(Condition.Paralyzed);

        var result = new SavingThrowResolver(TestCombatants.Dice(15)).Resolve(target, Ability.Wisdom, 10);

        result.AutoFailed.Should().BeFalse();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Save_Restrained_HasDisadvantageOnDexterity()
    {
        var target = TestCombatants.Monster();
        target.Conditions.Add(Condition.Restrained);

        var result = new SavingThrowResolver(TestCombatants.Dice(18, 4)).Resolve(target, Ability.Dexterity, 10);

        result.Mode.Should().Be(RollMode.Disadvantage);
        result.Roll!.Natural.Should().Be(4);
    }

    [Fact]
    public void Save_AdvantageAndDisadvantage_Cancel()
    {
        var target = TestCombatants.Monster();
        target.SaveAdvantages.Add(Ability.Wisdom);

        var result = new SavingThrowResolver(TestCombatants.Dice(8))
            .Resolve(target, Ability.Wisdom, 10, RollMode.Disadvantage);

        result.Mode.Should().Be(RollMode.Normal);
    }

    [Fact]
    public void Save_CoverAddsToDexterityOnly()
    {
        var target = TestCombatants.Monster();
        var resolver = new SavingThrowResolver(TestCombatants.Dice(8, 8));

        resolver.Resolve(target, Ability.Dexterity, 10, cover: CoverType.Half).Roll!.Total.Should().Be(10);
        resolver.Resolve(target, Ability.Wisdom, 10, cover: CoverType.Half).Roll!.Total.Should().Be(8);
    }

    [Fact]
    public void Save_ExhaustionAppliesPenalty()
    {
        var target = TestCombatants.Monster();
        target.ExhaustionLevel = 2;

        var result = new SavingThrowResolver(TestCombatants.Dice(12)).Resolve(target, Ability.Constitution, 10);

        result.Roll!.Total.Should().Be(8);
        result.Success.Should().BeFalse();
    }
}

public class AttackTests
{
    private static AttackRequest Request(Combatant attacker, Combatant target, string? damage = "1d8+3", int toHit = 5) => new()
    {
        Attacker = attacker,
        Target = target,
        ToHitBonus = toHit,
        Damage = damage is null ? null : DiceExpression.Parse(damage),
        DamageType = DamageType.Slashing
    };

    [Fact]
    public void Attack_MeetingAc_Hits_AndRollsDamage()
    {
        var attacker = TestCombatants.Player();
        var target = TestCombatants.Monster(ac: 15);

        var result = new AttackResolver(TestCombatants.Dice(10, 6)).Resolve(Request(attacker, target));

        result.Hit.Should().BeTrue();
        result.Critical.Should().BeFalse();
        result.Damage!.Amount.Should().Be(9);
        result.Damage.Type.Should().Be(DamageType.Slashing);
    }

    [Fact]
    public void Attack_BelowAc_Misses()
    {
        var result = new AttackResolver(TestCombatants.Dice(9))
            .Resolve(Request(TestCombatants.Player(), TestCombatants.Monster(ac: 15)));

        result.Hit.Should().BeFalse();
        result.Damage.Should().BeNull();
    }

    [Fact]
    public void Attack_NaturalTwenty_AlwaysHitsAndCrits_DoublingDice()
    {
        var target = TestCombatants.Monster(ac: 30);

        var result = new AttackResolver(TestCombatants.Dice(20, 2, 3)).Resolve(Request(TestCombatants.Player(), target));

        result.Hit.Should().BeTrue();
        result.Critical.Should().BeTrue();
        result.Damage!.Amount.Should().Be(2 + 3 + 3);
        result.Damage.IsCritical.Should().BeTrue();
    }

    [Fact]
    public void Attack_NaturalOne_AlwaysMisses()
    {
        var result = new AttackResolver(TestCombatants.Dice(1))
            .Resolve(Request(TestCombatants.Player(), TestCombatants.Monster(ac: 1), toHit: 20));

        result.Hit.Should().BeFalse();
    }

    [Fact]
    public void Attack_Cover_RaisesArmorClass()
    {
        var request = new AttackRequest
        {
            Attacker = TestCombatants.Player(),
            Target = TestCombatants.Monster(ac: 12),
            ToHitBonus = 0,
            Cover = CoverType.ThreeQuarters
        };

        var result = new AttackResolver(TestCombatants.Dice(16)).Resolve(request);

        result.TargetArmorClass.Should().Be(17);
        result.Hit.Should().BeFalse();
    }

    [Fact]
    public void Attack_TotalCover_CannotTarget()
    {
        var request = new AttackRequest
        {
            Attacker = TestCombatants.Player(),
            Target = TestCombatants.Monster(),
            Cover = CoverType.Total
        };

        var result = new AttackResolver(TestCombatants.Dice()).Resolve(request);

        result.Hit.Should().BeFalse();
        result.Roll.Should().BeNull();
    }

    [Fact]
    public void Attack_EffectBonusAndExhaustion_AdjustToHit()
    {
        var attacker = TestCombatants.Player();
        attacker.Effects.Add(new ActiveEffect { AttackBonus = 4 });
        attacker.ExhaustionLevel = 1;

        var result = new AttackResolver(TestCombatants.Dice(10))
            .Resolve(Request(attacker, TestCombatants.Monster(), null, toHit: 5));

        result.Roll!.Total.Should().Be(10 + 5 + 4 - 2);
    }

    [Fact]
    public void Attack_AgainstParalyzedTargetWithin5Feet_HasAdvantageAndAutoCrits()
    {
        var target = TestCombatants.Monster(ac: 12);
        target.Conditions.Add(Condition.Paralyzed);

        var result = new AttackResolver(TestCombatants.Dice(12, 5, 4, 4)).Resolve(Request(TestCombatants.Player(), target));

        result.Mode.Should().Be(RollMode.Advantage);
        result.Hit.Should().BeTrue();
        result.Critical.Should().BeTrue();
    }

    [Fact]
    public void Attack_AgainstParalyzedTargetFarAway_IsNotAnAutoCrit()
    {
        var target = TestCombatants.Monster(ac: 12);
        target.Conditions.Add(Condition.Paralyzed);
        var request = new AttackRequest
        {
            Attacker = TestCombatants.Player(),
            Target = target,
            ToHitBonus = 5,
            TargetWithin5Feet = false,
            IsRanged = true,
            Damage = DiceExpression.Parse("1d6")
        };

        var result = new AttackResolver(TestCombatants.Dice(12, 5, 3)).Resolve(request);

        result.Hit.Should().BeTrue();
        result.Critical.Should().BeFalse();
    }
}

public class AdvantageRuleTests
{
    private static RollMode Mode(Combatant attacker, Combatant target, bool within5 = true)
        => AdvantageRules.ForAttack(attacker, target, within5).Mode;

    [Theory]
    [InlineData(Condition.Blinded)]
    [InlineData(Condition.Poisoned)]
    [InlineData(Condition.Prone)]
    [InlineData(Condition.Restrained)]
    [InlineData(Condition.Frightened)]
    public void AttackerConditions_GiveDisadvantage(Condition condition)
    {
        var attacker = TestCombatants.Player();
        attacker.Conditions.Add(condition);

        Mode(attacker, TestCombatants.Monster()).Should().Be(RollMode.Disadvantage);
    }

    [Fact]
    public void Frightened_OnlyMattersWhenFearSourceIsVisible()
    {
        var attacker = TestCombatants.Player();
        attacker.Conditions.Add(Condition.Frightened);

        AdvantageRules.ForAttack(attacker, TestCombatants.Monster(), true, attackerSeesFearSource: false)
            .Mode.Should().Be(RollMode.Normal);
    }

    [Fact]
    public void InvisibleAttacker_GainsAdvantage()
        => Mode(WithCondition(Condition.Invisible), TestCombatants.Monster()).Should().Be(RollMode.Advantage);

    [Theory]
    [InlineData(Condition.Blinded)]
    [InlineData(Condition.Paralyzed)]
    [InlineData(Condition.Restrained)]
    [InlineData(Condition.Stunned)]
    [InlineData(Condition.Unconscious)]
    [InlineData(Condition.Petrified)]
    public void TargetConditions_GiveAdvantage(Condition condition)
        => Mode(TestCombatants.Player(), WithCondition(condition)).Should().Be(RollMode.Advantage);

    [Fact]
    public void InvisibleTarget_GivesDisadvantage()
        => Mode(TestCombatants.Player(), WithCondition(Condition.Invisible)).Should().Be(RollMode.Disadvantage);

    [Fact]
    public void ProneTarget_AdvantageNearDisadvantageFar()
    {
        var target = WithCondition(Condition.Prone);

        Mode(TestCombatants.Player(), target, within5: true).Should().Be(RollMode.Advantage);
        Mode(TestCombatants.Player(), target, within5: false).Should().Be(RollMode.Disadvantage);
    }

    [Fact]
    public void AdvantageAndDisadvantage_Cancel()
    {
        var attacker = WithCondition(Condition.Poisoned);
        var target = WithCondition(Condition.Stunned);

        Mode(attacker, target).Should().Be(RollMode.Normal);
    }

    [Fact]
    public void Reasons_ExplainTheResult()
    {
        var result = AdvantageRules.ForAttack(TestCombatants.Player(), WithCondition(Condition.Blinded), true);

        result.Reasons.Should().ContainSingle().Which.Should().Contain("Blinded");
    }

    private static Combatant WithCondition(Condition condition)
    {
        var combatant = TestCombatants.Monster();
        combatant.Conditions.Add(condition);
        return combatant;
    }
}

public class CombatantTests
{
    [Fact]
    public void ImpliedConditions_AreIncluded()
    {
        var target = TestCombatants.Monster();
        target.Conditions.Add(Condition.Unconscious);

        target.HasCondition(Condition.Incapacitated).Should().BeTrue();
        target.HasCondition(Condition.Prone).Should().BeTrue();
    }

    [Fact]
    public void ConditionImmunity_BlocksConditionFromEffects()
    {
        var target = TestCombatants.Monster();
        target.ConditionImmunities.Add(Condition.Paralyzed);
        target.Effects.Add(new ActiveEffect { Name = "Hold Person", GrantedConditions = { Condition.Paralyzed } });

        target.HasCondition(Condition.Paralyzed).Should().BeFalse();
        target.HasCondition(Condition.Incapacitated).Should().BeFalse();
    }

    [Fact]
    public void Effects_ModifyArmorClassAndSpeed()
    {
        var target = TestCombatants.Monster(ac: 12);
        target.BaseSpeed = 30;
        target.Effects.Add(new ActiveEffect { ArmorClassBonus = 5, SpeedBonus = 10 });

        target.ArmorClass.Should().Be(17);
        target.Speed.Should().Be(40);
    }

    [Fact]
    public void Speed_IsZeroWhenRestrainedOrGrappled()
    {
        var target = TestCombatants.Monster();
        target.Conditions.Add(Condition.Grappled);

        target.Speed.Should().Be(0);
    }

    [Fact]
    public void Exhaustion_ReducesSpeedAndD20Tests()
    {
        var target = TestCombatants.Monster();
        target.ExhaustionLevel = 3;

        target.Speed.Should().Be(15);
        target.D20Penalty.Should().Be(6);
    }

    [Fact]
    public void Capture_Restore_RoundTripsEverything()
    {
        var target = TestCombatants.Player(hp: 30);
        target.TempHp = 3;
        target.Conditions.Add(Condition.Poisoned);
        target.Effects.Add(new ActiveEffect { Name = "Bless", RemainingRounds = 10 });
        var snapshot = target.Capture();

        target.CurrentHp = 1;
        target.TempHp = 0;
        target.Conditions.Clear();
        target.Effects[0].RemainingRounds = 1;
        target.Effects.Clear();

        target.Restore(snapshot);

        target.CurrentHp.Should().Be(30);
        target.TempHp.Should().Be(3);
        target.Conditions.Should().Contain(Condition.Poisoned);
        target.Effects.Single().RemainingRounds.Should().Be(10);
    }

    [Fact]
    public void Snapshot_IsNotAffectedByLaterChanges()
    {
        var target = TestCombatants.Monster();
        var effect = new ActiveEffect { Name = "Haste", RemainingRounds = 10 };
        target.Effects.Add(effect);
        var snapshot = target.Capture();

        effect.RemainingRounds = 2;

        snapshot.Effects.Single().RemainingRounds.Should().Be(10);
    }

    [Fact]
    public void DamageTraits_Parse_HandlesNonmagicalQualifier()
    {
        var traits = DamageTraits.Parse(["Cold", "Bludgeoning, Piercing, and Slashing from Nonmagical Attacks"]);

        traits.Always.Should().BeEquivalentTo([DamageType.Cold]);
        traits.NonmagicalOnly.Should().BeEquivalentTo([DamageType.Bludgeoning, DamageType.Piercing, DamageType.Slashing]);
    }

    [Fact]
    public void DamageTraits_Parse_HandlesSemicolonSegments()
    {
        var traits = DamageTraits.Parse(["Fire; Bludgeoning, Piercing, and Slashing from Nonmagical Attacks"]);

        traits.Always.Should().BeEquivalentTo([DamageType.Fire]);
        traits.NonmagicalOnly.Should().HaveCount(3);
    }

    [Fact]
    public void FromCreature_CopiesStatsTraitsAndImmunities()
    {
        var creature = new Creature
        {
            Name = "Skeleton",
            ArmorClass = 13,
            HitPoints = 13,
            ChallengeRating = 0.25,
            WalkSpeed = 30,
            AbilityScores = new Dictionary<string, int> { ["dexterity"] = 14, ["constitution"] = 15 },
            SavingThrowProficiencies = ["dexterity"],
            DamageVulnerabilities = ["Bludgeoning"],
            DamageImmunities = ["Poison"],
            ConditionImmunities = ["Exhaustion", "Poisoned"]
        };

        var combatant = CombatantFactory.FromCreature(creature);

        combatant.Name.Should().Be("Skeleton");
        combatant.MaxHp.Should().Be(13);
        combatant.BaseArmorClass.Should().Be(13);
        combatant.Abilities[Ability.Dexterity].Should().Be(14);
        combatant.ProficiencyBonus.Should().Be(2);
        combatant.SaveProficiencies.Should().BeEquivalentTo([Ability.Dexterity]);
        combatant.Vulnerabilities.Always.Should().Contain(DamageType.Bludgeoning);
        combatant.Immunities.Always.Should().Contain(DamageType.Poison);
        combatant.ConditionImmunities.Should().BeEquivalentTo([Condition.Poisoned]);
        combatant.IsPlayerCharacter.Should().BeFalse();
    }
}