using DndHelper.Core.Encounters;
using DndHelper.Core.Rules;
using DndHelper.Tests.Rules;
using FluentAssertions;

namespace DndHelper.Tests.Encounters;

public class EncounterSpellTests
{
    private static (Encounter Encounter, List<string> Log) Make(params int[] faces)
    {
        var encounter = new Encounter(new SequenceRandom(faces));
        var log = new List<string>();
        encounter.Logged += e => log.Add(e.Text);
        return (encounter, log);
    }

    private static SpellEffectRequest Fireball(Combatant? caster = null) => new()
    {
        Name = "Fireball",
        Caster = caster,
        Damage = DiceExpression.Parse("2d6"),
        DamageType = DamageType.Fire,
        SaveAbility = Ability.Dexterity,
        SaveDc = 15
    };

    [Fact]
    public void PlanSpell_changes_nothing_and_logs_nothing()
    {
        var (e, log) = Make(3, 4, 5);
        var goblin = e.Add(TestCombatants.Monster("Goblin"), Side.Enemy);
        var revision = e.Revision;

        var plan = e.PlanSpell(Fireball(), [goblin]);

        plan.Targets.Should().ContainSingle();
        goblin.Combatant.CurrentHp.Should().Be(20);
        e.Revision.Should().Be(revision);
        log.Should().NotContain(l => l.Contains("casts"));
    }

    [Fact]
    public void CommitSpell_applies_full_and_half_damage_and_logs()
    {
        // Damage 3+4 = 7, then Goblin A fails (5) and Goblin B saves (20).
        var (e, log) = Make(3, 4, 5, 20);
        var a = e.Add(TestCombatants.Monster("Goblin A"), Side.Enemy);
        var b = e.Add(TestCombatants.Monster("Goblin B"), Side.Enemy);

        var plan = e.PlanSpell(Fireball(), [a, b]);
        e.CommitSpell(plan);

        a.Combatant.CurrentHp.Should().Be(13);
        b.Combatant.CurrentHp.Should().Be(17);
        log.Should().Contain(l => l.Contains("casts Fireball"));
        log.Should().Contain(l => l.Contains("Goblin A takes 7 Fire damage"));
    }

    [Fact]
    public void UndoSpell_restores_everyone()
    {
        var (e, log) = Make(3, 4, 5);
        var goblin = e.Add(TestCombatants.Monster("Goblin"), Side.Enemy);

        var committed = e.CommitSpell(e.PlanSpell(Fireball(), [goblin]));
        goblin.Combatant.CurrentHp.Should().Be(13);

        e.UndoSpell(committed, "Fireball");

        goblin.Combatant.CurrentHp.Should().Be(20);
        log.Last().Should().Contain("undone");
    }

    [Fact]
    public void Revision_changes_after_any_later_action()
    {
        var (e, _) = Make(3, 4, 5);
        var goblin = e.Add(TestCombatants.Monster("Goblin"), Side.Enemy);

        e.CommitSpell(e.PlanSpell(Fireball(), [goblin]));
        var afterCommit = e.Revision;

        e.Heal(goblin, 1);

        e.Revision.Should().BeGreaterThan(afterCommit);
    }

    [Fact]
    public void Failed_save_applies_a_concentration_effect_and_the_caster_concentrates()
    {
        var (e, log) = Make(5);
        var wizard = e.Add(TestCombatants.Player("Wizard"), Side.Party);
        var goblin = e.Add(TestCombatants.Monster("Goblin"), Side.Enemy);

        var effect = new ActiveEffect { Name = "Hold", RequiresConcentration = true, RemainingRounds = 10 };
        effect.GrantedConditions.Add(Condition.Paralyzed);

        var request = new SpellEffectRequest
        {
            Name = "Hold Person",
            Caster = wizard.Combatant,
            SaveAbility = Ability.Wisdom,
            SaveDc = 15,
            Effect = effect
        };

        e.CommitSpell(e.PlanSpell(request, [goblin]));

        goblin.Combatant.Effects.Should().ContainSingle(x => x.Name == "Hold");
        goblin.Combatant.HasCondition(Condition.Paralyzed).Should().BeTrue();
        wizard.Combatant.ConcentratingOn.Should().Be("Hold Person");
        log.Should().Contain(l => l.Contains("Goblin is affected by Hold"));
        log.Should().Contain(l => l.Contains("Wizard is concentrating on Hold Person"));
    }

    [Fact]
    public void Total_cover_target_is_skipped_and_logged()
    {
        var (e, log) = Make(3, 4);
        var goblin = e.Add(TestCombatants.Monster("Goblin"), Side.Enemy);
        var request = new SpellEffectRequest
        {
            Name = "Fireball",
            Damage = DiceExpression.Parse("2d6"),
            SaveAbility = Ability.Dexterity,
            SaveDc = 15,
            CoverByTargetId = new Dictionary<string, CoverType> { [goblin.Combatant.Id] = CoverType.Total }
        };

        e.CommitSpell(e.PlanSpell(request, [goblin]));

        goblin.Combatant.CurrentHp.Should().Be(20);
        log.Should().Contain(l => l.Contains("Total cover"));
    }
}
