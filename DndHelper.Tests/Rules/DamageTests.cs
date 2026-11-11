using DndHelper.Core.Rules;
using FluentAssertions;

namespace DndHelper.Tests.Rules;

public class DamageTests
{
    private static DamageInstance Hit(int amount, DamageType type = DamageType.Fire, bool magical = true, bool crit = false)
        => new(amount, type, magical, crit);

    [Fact]
    public void PlainDamage_ReducesHp()
    {
        var target = TestCombatants.Monster(hp: 20);

        var result = DamageResolver.Apply(target, [Hit(7)]);

        target.CurrentHp.Should().Be(13);
        result.HpLost.Should().Be(7);
        result.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public void Resistance_HalvesAndRoundsDown()
    {
        var target = TestCombatants.Monster();
        target.Resistances.Always.Add(DamageType.Fire);

        DamageResolver.Apply(target, [Hit(9)]);

        target.CurrentHp.Should().Be(16);
    }

    [Fact]
    public void Vulnerability_Doubles()
    {
        var target = TestCombatants.Monster(hp: 30);
        target.Vulnerabilities.Always.Add(DamageType.Fire);

        DamageResolver.Apply(target, [Hit(9)]);

        target.CurrentHp.Should().Be(12);
    }

    [Fact]
    public void ResistanceAndVulnerability_ApplyInOrder_ResistanceFirst()
    {
        var target = TestCombatants.Monster(hp: 30);
        target.Resistances.Always.Add(DamageType.Fire);
        target.Vulnerabilities.Always.Add(DamageType.Fire);

        DamageResolver.Apply(target, [Hit(9)]);

        // 9 halved (rounded down) is 4, then doubled is 8.
        target.CurrentHp.Should().Be(22);
    }

    [Fact]
    public void Immunity_PreventsAllDamage()
    {
        var target = TestCombatants.Monster();
        target.Immunities.Always.Add(DamageType.Poison);

        var result = DamageResolver.Apply(target, [Hit(50, DamageType.Poison)]);

        target.CurrentHp.Should().Be(20);
        result.AdjustedTotal.Should().Be(0);
    }

    [Fact]
    public void NonmagicalResistance_OnlyAppliesToNonmagicalDamage()
    {
        var target = TestCombatants.Monster();
        target.Resistances.NonmagicalOnly.Add(DamageType.Slashing);

        DamageResolver.Apply(target, [Hit(10, DamageType.Slashing, magical: false)]);
        target.CurrentHp.Should().Be(15);

        DamageResolver.Apply(target, [Hit(10, DamageType.Slashing, magical: true)]);
        target.CurrentHp.Should().Be(5);
    }

    [Fact]
    public void EffectGrantedResistance_Applies()
    {
        var target = TestCombatants.Monster();
        target.Effects.Add(new ActiveEffect { Name = "Protection from Energy", GrantedResistances = { DamageType.Fire } });

        DamageResolver.Apply(target, [Hit(10)]);

        target.CurrentHp.Should().Be(15);
    }

    [Fact]
    public void TemporaryHp_IsConsumedFirst()
    {
        var target = TestCombatants.Monster(hp: 20);
        target.TempHp = 5;

        var result = DamageResolver.Apply(target, [Hit(8)]);

        target.TempHp.Should().Be(0);
        target.CurrentHp.Should().Be(17);
        result.AbsorbedByTempHp.Should().Be(5);
        result.HpLost.Should().Be(3);
    }

    [Fact]
    public void TemporaryHp_CanAbsorbEverything()
    {
        var target = TestCombatants.Monster(hp: 20);
        target.TempHp = 10;

        DamageResolver.Apply(target, [Hit(4)]);

        target.TempHp.Should().Be(6);
        target.CurrentHp.Should().Be(20);
    }

    [Fact]
    public void Monster_DiesAtZeroHp()
    {
        var target = TestCombatants.Monster(hp: 10);

        var result = DamageResolver.Apply(target, [Hit(10)]);

        target.IsDead.Should().BeTrue();
        result.Died.Should().BeTrue();
        result.DroppedToZero.Should().BeTrue();
    }

    [Fact]
    public void Player_DroppingToZero_FallsUnconsciousAndDying()
    {
        var target = TestCombatants.Player(hp: 20);

        DamageResolver.Apply(target, [Hit(25)]);

        target.CurrentHp.Should().Be(0);
        target.IsDead.Should().BeFalse();
        target.IsDying.Should().BeTrue();
        target.HasCondition(Condition.Unconscious).Should().BeTrue();
    }

    [Fact]
    public void Player_MassiveDamage_IsInstantDeath()
    {
        var target = TestCombatants.Player(hp: 10);
        target.CurrentHp = 5;

        var result = DamageResolver.Apply(target, [Hit(20)]);

        target.IsDead.Should().BeTrue();
        result.InstantDeath.Should().BeTrue();
    }

    [Fact]
    public void Player_AtZero_TakesDeathSaveFailureFromDamage()
    {
        var target = TestCombatants.Player(hp: 20);
        DamageResolver.Apply(target, [Hit(20)]);

        var result = DamageResolver.Apply(target, [Hit(3)]);

        target.DeathSaveFailures.Should().Be(1);
        result.DeathSaveFailuresAdded.Should().Be(1);
    }

    [Fact]
    public void Player_AtZero_CriticalHitCountsAsTwoFailures()
    {
        var target = TestCombatants.Player(hp: 20);
        DamageResolver.Apply(target, [Hit(20)]);

        DamageResolver.Apply(target, [Hit(3, crit: true)]);

        target.DeathSaveFailures.Should().Be(2);
    }

    [Fact]
    public void Player_AtZero_ThreeFailuresKill()
    {
        var target = TestCombatants.Player(hp: 20);
        DamageResolver.Apply(target, [Hit(20)]);

        DamageResolver.Apply(target, [Hit(1), Hit(1), Hit(1)]);

        target.IsDead.Should().BeTrue();
    }

    [Fact]
    public void Player_AtZero_DamageEqualToMaxHp_IsInstantDeath()
    {
        var target = TestCombatants.Player(hp: 20);
        DamageResolver.Apply(target, [Hit(20)]);

        DamageResolver.Apply(target, [Hit(20)]);

        target.IsDead.Should().BeTrue();
    }

    [Fact]
    public void Player_AtZero_DamageBreaksStability()
    {
        var target = TestCombatants.Player(hp: 20);
        DamageResolver.Apply(target, [Hit(20)]);
        RecoveryResolver.Stabilize(target);
        target.IsStable.Should().BeTrue();

        DamageResolver.Apply(target, [Hit(2)]);

        target.IsStable.Should().BeFalse();
        target.DeathSaveFailures.Should().Be(1);
    }

    [Fact]
    public void MultipleInstances_AreAppliedInOrder()
    {
        var target = TestCombatants.Monster(hp: 30);
        target.Resistances.Always.Add(DamageType.Fire);

        var result = DamageResolver.Apply(target, [Hit(10, DamageType.Fire), Hit(6, DamageType.Cold)]);

        target.CurrentHp.Should().Be(19);
        result.RawTotal.Should().Be(16);
        result.AdjustedTotal.Should().Be(11);
    }

    [Fact]
    public void Undo_RestoresEverything()
    {
        var target = TestCombatants.Player(hp: 20);
        target.TempHp = 4;

        var result = DamageResolver.Apply(target, [Hit(30)]);
        target.IsDying.Should().BeTrue();

        result.Undo();

        target.CurrentHp.Should().Be(20);
        target.TempHp.Should().Be(4);
        target.IsDying.Should().BeFalse();
        target.HasCondition(Condition.Unconscious).Should().BeFalse();
    }

    [Fact]
    public void Preview_DoesNotChangeTarget()
    {
        var target = TestCombatants.Monster(hp: 20);

        var result = DamageResolver.Preview(target, [Hit(8)]);

        result.HpLost.Should().Be(8);
        target.CurrentHp.Should().Be(20);
    }

    [Fact]
    public void ZeroOrNegativeAmounts_AreIgnored()
    {
        var target = TestCombatants.Monster(hp: 20);

        DamageResolver.Apply(target, [Hit(0), Hit(-5)]);

        target.CurrentHp.Should().Be(20);
    }
}

public class RecoveryTests
{
    [Fact]
    public void Heal_CapsAtMaxHp()
    {
        var target = TestCombatants.Monster(hp: 20);
        target.CurrentHp = 15;

        var result = RecoveryResolver.Heal(target, 20);

        target.CurrentHp.Should().Be(20);
        result.HpRestored.Should().Be(5);
    }

    [Fact]
    public void Heal_WakesDyingPlayer_AndResetsDeathSaves()
    {
        var target = TestCombatants.Player(hp: 20);
        DamageResolver.Apply(target, [new DamageInstance(20, DamageType.Fire)]);
        target.DeathSaveFailures = 2;

        var result = RecoveryResolver.Heal(target, 4);

        target.CurrentHp.Should().Be(4);
        target.DeathSaveFailures.Should().Be(0);
        target.HasCondition(Condition.Unconscious).Should().BeFalse();
        result.Revived.Should().BeTrue();
    }

    [Fact]
    public void Heal_DoesNothingForDeadCreatures()
    {
        var target = TestCombatants.Monster(hp: 20);
        DamageResolver.Apply(target, [new DamageInstance(20, DamageType.Fire)]);

        var result = RecoveryResolver.Heal(target, 10);

        target.CurrentHp.Should().Be(0);
        result.HpRestored.Should().Be(0);
    }

    [Fact]
    public void TemporaryHp_DoNotStack()
    {
        var target = TestCombatants.Monster();
        target.TempHp = 8;

        RecoveryResolver.GrantTemporaryHp(target, 5);
        target.TempHp.Should().Be(8);

        RecoveryResolver.GrantTemporaryHp(target, 12);
        target.TempHp.Should().Be(12);
    }

    [Fact]
    public void Heal_CanBeUndone()
    {
        var target = TestCombatants.Monster(hp: 20);
        target.CurrentHp = 5;

        RecoveryResolver.Heal(target, 10).Undo();

        target.CurrentHp.Should().Be(5);
    }
}

public class DeathSaveTests
{
    private static Combatant DyingPlayer()
    {
        var player = TestCombatants.Player(hp: 20);
        DamageResolver.Apply(player, [new DamageInstance(20, DamageType.Fire)]);
        return player;
    }

    [Fact]
    public void Roll_TenOrMore_IsSuccess()
    {
        var player = DyingPlayer();

        var result = new DeathSaveResolver(TestCombatants.Dice(10)).Roll(player);

        result.Outcome.Should().Be(DeathSaveOutcome.Success);
        player.DeathSaveSuccesses.Should().Be(1);
    }

    [Fact]
    public void Roll_NineOrLess_IsFailure()
    {
        var player = DyingPlayer();

        var result = new DeathSaveResolver(TestCombatants.Dice(9)).Roll(player);

        result.Outcome.Should().Be(DeathSaveOutcome.Failure);
        player.DeathSaveFailures.Should().Be(1);
    }

    [Fact]
    public void Roll_NaturalOne_IsTwoFailures()
    {
        var player = DyingPlayer();

        var result = new DeathSaveResolver(TestCombatants.Dice(1)).Roll(player);

        result.Outcome.Should().Be(DeathSaveOutcome.CriticalFailure);
        player.DeathSaveFailures.Should().Be(2);
    }

    [Fact]
    public void Roll_NaturalTwenty_RestoresOneHp()
    {
        var player = DyingPlayer();
        player.DeathSaveFailures = 2;

        var result = new DeathSaveResolver(TestCombatants.Dice(20)).Roll(player);

        result.Outcome.Should().Be(DeathSaveOutcome.CriticalSuccess);
        player.CurrentHp.Should().Be(1);
        player.DeathSaveFailures.Should().Be(0);
        player.HasCondition(Condition.Unconscious).Should().BeFalse();
    }

    [Fact]
    public void ThreeSuccesses_Stabilize()
    {
        var player = DyingPlayer();
        var resolver = new DeathSaveResolver(TestCombatants.Dice(12, 15, 11));

        resolver.Roll(player);
        resolver.Roll(player);
        var result = resolver.Roll(player);

        result.Outcome.Should().Be(DeathSaveOutcome.Stabilized);
        player.IsStable.Should().BeTrue();
        player.IsDying.Should().BeFalse();
        player.CurrentHp.Should().Be(0);
    }

    [Fact]
    public void ThreeFailures_Kill()
    {
        var player = DyingPlayer();
        var resolver = new DeathSaveResolver(TestCombatants.Dice(5, 5, 5));

        resolver.Roll(player);
        resolver.Roll(player);
        var result = resolver.Roll(player);

        result.Outcome.Should().Be(DeathSaveOutcome.Died);
        player.IsDead.Should().BeTrue();
    }

    [Fact]
    public void Roll_WhenNotDying_DoesNothing()
    {
        var player = TestCombatants.Player();

        var result = new DeathSaveResolver(TestCombatants.Dice()).Roll(player);

        result.Outcome.Should().Be(DeathSaveOutcome.NotDying);
    }

    [Fact]
    public void Roll_CanBeUndone()
    {
        var player = DyingPlayer();

        new DeathSaveResolver(TestCombatants.Dice(3)).Roll(player).Undo();

        player.DeathSaveFailures.Should().Be(0);
    }
}