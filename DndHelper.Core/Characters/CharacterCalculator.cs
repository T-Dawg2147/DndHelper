using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public static class CharacterCalculator
{
    public const int MaxScore = 20;

    /// <summary>Base score plus background increases, capped at 20.</summary>
    public static AbilityScores FinalScores(PlayerCharacter character)
    {
        var scores = new AbilityScores();
        foreach (var ability in AbilityExtensions.All)
        {
            var total = character.BaseScores.GetValueOrDefault(ability, 10)
                        + character.BackgroundBonuses.GetValueOrDefault(ability, 0);
            scores[ability] = Math.Min(MaxScore, total);
        }
        return scores;
    }

    /// <summary>Level 1 gives the maximum hit die; each later level gives the average (rounded up), plus Constitution each time.</summary>
    public static int MaxHitPoints(int hitDie, int level, int constitutionModifier)
    {
        var clamped = Math.Clamp(level, 1, 20);
        var first = Math.Max(1, hitDie + constitutionModifier);
        var perLevel = Math.Max(1, hitDie / 2 + 1 + constitutionModifier);
        return first + (clamped - 1) * perLevel;
    }

    public static CharacterSheet Compute(PlayerCharacter character)
    {
        var cls = ClassCatalog.Find(character.ClassName)
                  ?? throw new ArgumentException($"Unknown class '{character.ClassName}'.", nameof(character));
        var species = SpeciesCatalog.Find(character.Species)
                      ?? throw new ArgumentException($"Unknown species '{character.Species}'.", nameof(character));
        var background = BackgroundCatalog.Find(character.Background)
                         ?? throw new ArgumentException($"Unknown background '{character.Background}'.", nameof(character));

        var level = Math.Clamp(character.Level, 1, 20);
        var scores = FinalScores(character);
        var proficiency = Proficiency.ForLevel(level);

        var skillProficiencies = new HashSet<Skill>(background.Skills);
        skillProficiencies.UnionWith(character.ClassSkills);

        var skillBonuses = SkillExtensions.All.ToDictionary(
            skill => skill,
            skill => scores.Modifier(skill.AbilityOf()) + (skillProficiencies.Contains(skill) ? proficiency : 0));

        int? spellDc = null, spellAttack = null;
        if (cls.SpellcastingAbility is { } casting)
        {
            spellAttack = proficiency + scores.Modifier(casting);
            spellDc = 8 + spellAttack;
        }

        return new CharacterSheet
        {
            Scores = scores,
            ProficiencyBonus = proficiency,
            MaxHp = MaxHitPoints(cls.HitDie, level, scores.Modifier(Ability.Constitution)),
            ArmorClass = character.ArmorClassOverride ?? 10 + scores.Modifier(Ability.Dexterity),
            Speed = species.Speed,
            Initiative = scores.Modifier(Ability.Dexterity),
            PassivePerception = 10 + skillBonuses[Skill.Perception],
            DarkvisionFeet = species.DarkvisionFeet,
            Size = species.Size,
            SaveProficiencies = new HashSet<Ability>(cls.SavingThrows),
            SkillBonuses = skillBonuses,
            SkillProficiencies = skillProficiencies,
            Resistances = species.Resistances,
            SpellSaveDc = spellDc,
            SpellAttackBonus = spellAttack,
            SpellSlots = SpellSlotTable.For(cls.Caster, level),
            Caster = cls.Caster,
            OriginFeat = background.OriginFeat,
            ToolProficiency = background.ToolProficiency
        };
    }
}