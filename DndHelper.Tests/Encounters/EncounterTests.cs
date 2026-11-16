using DndHelper.Core.Encounters;
using DndHelper.Core.Rules;
using DndHelper.Tests.Rules;
using FluentAssertions;

namespace DndHelper.Tests.Encounters;

public class EncounterTests
{
    private static (Encounter Encounter, List<string> Log) Make(params int[] faces)
    {
        var encounter = new Encounter(new SequenceRandom(faces));
        var log = new List<string>();
        encounter.Logged += e => log.Add(e.Text);
        return (encounter, log);
    }

    [Fact]
    public void Add_DuplicateNamesAreNumbered()
    {
        var (e, _) = Make();

        e.Add(TestCombatants.Monster("Goblin"), Side.Enemy);
        e.Add(TestCombatants.Monster("Goblin"), Side.Enemy);
        e.Add(TestCombatants.Monster("goblin"), Side.Enemy);

        e.Turns.Select(t => t.Name).Should().Equal("Goblin", "Goblin 2", "goblin 3");
    }

    [Fact]
    public void RollInitiative_OrdersHighestFirst_UsingBonus()
    {
        var (e, _) = Make(10, 15);
        e.Add(TestCombatants.Monster("A"), Side.Enemy, 2);
        e.Add(TestCombatants.Monster("B"), Side.Enemy, 0);

        e.RollInitiative();

        e.Turns.Select(t => t.Name).Should().Equal("B", "A");
        e.Turns[0].Initiative.Should().Be(15);
        e.Turns[1].Initiative.Should().Be(12);
    }

    [Fact]
    public void Ties_GoToHigherBonus_ThenEarlierAdded()
    {
        var (e, _) = Make();
        var a = e.Add(TestCombatants.Monster("A"), Side.Enemy, 1);
        var b = e.Add(TestCombatants.Monster("B"), Side.Enemy, 3);
        var c = e.Add(TestCombatants.Monster("C"), Side.Enemy, 3);
        foreach (var p in new[] { a, b, c }) e.SetInitiative(p, 10);

        e.Turns.Select(t => t.Name).Should().Equal("B", "C", "A");
    }

    [Fact]
    public void Start_BeginsRoundOneWithTheFirstCombatant()
    {
        var (e, log) = Make();
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.Add(TestCombatants.Monster("B"), Side.Enemy);

        e.Start().Should().BeTrue();

        e.IsStarted.Should().BeTrue();
        e.Round.Should().Be(1);
        e.Current!.Name.Should().Be("A");
        log.Should().Contain("A's turn.");
        e.Start().Should().BeFalse();
    }

    [Fact]
    public void Start_NeedsParticipants()
        => Make().Encounter.Start().Should().BeFalse();

    [Fact]
    public void NextTurn_WrapsAndCountsRounds()
    {
        var (e, _) = Make();
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.Add(TestCombatants.Monster("B"), Side.Enemy);
        e.Start();

        e.NextTurn();
        e.Current!.Name.Should().Be("B");
        e.Round.Should().Be(1);

        e.NextTurn();
        e.Current!.Name.Should().Be("A");
        e.Round.Should().Be(2);
    }

    [Fact]
    public void NextTurn_SkipsDeadCombatants()
    {
        var (e, _) = Make();
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        var b = e.Add(TestCombatants.Monster("B", hp: 1), Side.Enemy);
        e.Add(TestCombatants.Monster("C"), Side.Enemy);
        e.ApplyDamage(b, 5, DamageType.Fire);
        e.Start();

        e.NextTurn();

        e.Current!.Name.Should().Be("C");
    }

    [Fact]
    public void TurnStart_ResetsActionEconomy()
    {
        var (e, _) = Make();
        var a = e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.Add(TestCombatants.Monster("B"), Side.Enemy);
        e.Start();
        a.ActionUsed = a.BonusActionUsed = a.ReactionUsed = true;

        e.NextTurn();
        e.NextTurn();

        a.ActionUsed.Should().BeFalse();
        a.BonusActionUsed.Should().BeFalse();
        a.ReactionUsed.Should().BeFalse();
    }

    [Fact]
    public void EffectDurations_TickAtTheEndOfTheAffectedTurn()
    {
        var (e, log) = Make();
        var a = e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.Add(TestCombatants.Monster("B"), Side.Enemy);
        a.Combatant.Effects.Add(new ActiveEffect { Name = "Shield of Faith", RemainingRounds = 1 });
        e.Start();

        e.NextTurn();

        a.Combatant.Effects.Should().BeEmpty();
        log.Should().Contain(l => l.Contains("expires"));
    }

    [Fact]
    public void DyingPlayer_IsToldToRollADeathSave()
    {
        var (e, log) = Make();
        var hero = e.Add(TestCombatants.Player("Hero", hp: 10), Side.Party);
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.ApplyDamage(hero, 10, DamageType.Fire);

        e.Start();

        log.Should().Contain(l => l.Contains("death saving throw"));
    }

    [Fact]
    public void DeathSave_IsRolledAndLogged()
    {
        var (e, log) = Make(12);
        var hero = e.Add(TestCombatants.Player("Hero", hp: 10), Side.Party);
        e.ApplyDamage(hero, 10, DamageType.Fire);

        var result = e.RollDeathSave(hero);

        result.Outcome.Should().Be(DeathSaveOutcome.Success);
        hero.Combatant.DeathSaveSuccesses.Should().Be(1);
        log.Last().Should().Contain("Success");
    }

    [Fact]
    public void Damage_IsAppliedWithTheAuditTrail()
    {
        var (e, log) = Make();
        var goblin = e.Add(TestCombatants.Monster("Goblin", hp: 20), Side.Enemy);
        goblin.Combatant.Resistances.Always.Add(DamageType.Fire);

        e.ApplyDamage(goblin, 9, DamageType.Fire);

        goblin.Combatant.CurrentHp.Should().Be(16);
        log.Should().Contain("9 Fire damage to Goblin.");
        log.Should().Contain(l => l.Contains("Resistance to Fire"));
    }

    [Fact]
    public void Damage_ToAConcentratingCreature_RollsTheSave()
    {
        var (e, log) = Make(4);
        var wizard = e.Add(TestCombatants.Player("Wizard", hp: 30), Side.Party);
        wizard.Combatant.ConcentratingOn = "Bless";

        e.ApplyDamage(wizard, 10, DamageType.Fire);

        wizard.Combatant.ConcentratingOn.Should().BeNull();
        log.Should().Contain(l => l.Contains("loses concentration"));
    }

    [Fact]
    public void HealAndTempHp_AreLogged()
    {
        var (e, log) = Make();
        var p = e.Add(TestCombatants.Monster("A", hp: 20), Side.Enemy);
        e.ApplyDamage(p, 10, DamageType.Fire);

        e.Heal(p, 4);
        e.GrantTemporaryHp(p, 6);

        p.Combatant.CurrentHp.Should().Be(14);
        p.Combatant.TempHp.Should().Be(6);
        log.Should().Contain(l => l.Contains("regains 4 HP"));
    }

    [Fact]
    public void ToggleCondition_AddsThenRemoves()
    {
        var (e, log) = Make();
        var p = e.Add(TestCombatants.Monster("A"), Side.Enemy);

        e.ToggleCondition(p, Condition.Prone).Should().BeTrue();
        p.Combatant.HasCondition(Condition.Prone).Should().BeTrue();

        e.ToggleCondition(p, Condition.Prone).Should().BeFalse();
        p.Combatant.HasCondition(Condition.Prone).Should().BeFalse();
        log.Should().Contain("A is no longer Prone.");
    }

    [Fact]
    public void RemovingTheCurrentParticipant_PassesTheTurnOn()
    {
        var (e, _) = Make();
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        var b = e.Add(TestCombatants.Monster("B"), Side.Enemy);
        e.Add(TestCombatants.Monster("C"), Side.Enemy);
        e.Start();
        e.NextTurn();

        e.Remove(b);

        e.Current!.Name.Should().Be("C");
        e.Round.Should().Be(1);
    }

    [Fact]
    public void RemovingTheLastInOrder_StartsANewRound()
    {
        var (e, _) = Make();
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        var b = e.Add(TestCombatants.Monster("B"), Side.Enemy);
        e.Start();
        e.NextTurn();

        e.Remove(b);

        e.Current!.Name.Should().Be("A");
        e.Round.Should().Be(2);
    }

    [Fact]
    public void RemovingEveryone_EndsTheEncounter()
    {
        var (e, _) = Make();
        var a = e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.Start();

        e.Remove(a);

        e.IsStarted.Should().BeFalse();
    }

    [Fact]
    public void JoiningMidEncounter_RollsInitiativeAndKeepsTheCurrentTurn()
    {
        var (e, _) = Make(20);
        var a = e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.SetInitiative(a, 10);
        e.Start();

        var late = e.Add(TestCombatants.Monster("Late"), Side.Enemy);

        late.Initiative.Should().Be(20);
        e.Turns[0].Should().Be(late);
        e.Current.Should().Be(a);
    }

    [Fact]
    public void End_StopsTheEncounter()
    {
        var (e, log) = Make();
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.Start();
        e.NextTurn();

        e.End();

        e.IsStarted.Should().BeFalse();
        e.Current.Should().BeNull();
        log.Last().Should().Contain("ends after 2 rounds");
    }

    [Fact]
    public void LogEntries_CarryTheRound()
    {
        var entries = new List<LogEntry>();
        var e = new Encounter(new SequenceRandom());
        e.Logged += entries.Add;
        e.Add(TestCombatants.Monster("A"), Side.Enemy);
        e.Start();

        entries.First().ToString().Should().Be("A joins the encounter.");
        entries.Last().ToString().Should().Be("[R1] A's turn.");
    }
}
