using System.Text.RegularExpressions;
using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public enum Skill
{
    Acrobatics,
    AnimalHandling,
    Arcana,
    Athletics,
    Deception,
    History,
    Insight,
    Intimidation,
    Investigation,
    Medicine,
    Nature,
    Perception,
    Persuasion,
    Religion,
    SleightOfHand,
    Stealth,
    Survival
}

public static partial class SkillExtensions
{
    public static readonly IReadOnlyList<Skill> All = Enum.GetValues<Skill>();

    public static Ability AbilityOf(this Skill skill) => skill switch
    {
        Skill.Athletics => Ability.Strength,
        Skill.Acrobatics or Skill.SleightOfHand or Skill.Stealth => Ability.Dexterity,
        Skill.Arcana or Skill.History or Skill.Investigation or Skill.Nature or Skill.Religion => Ability.Intelligence,
        Skill.AnimalHandling or Skill.Insight or Skill.Medicine or Skill.Perception or Skill.Survival => Ability.Wisdom,
        _ => Ability.Charisma
    };

    public static string DisplayName(this Skill skill)
        => WordBoundary().Replace(skill.ToString(), " ").Replace(" Of ", " of ");

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}