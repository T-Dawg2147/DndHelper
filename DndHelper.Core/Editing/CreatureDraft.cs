using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using DndHelper.Core.Characters;
using DndHelper.Core.Models;
using DndHelper.Core.Rules;

namespace DndHelper.Core.Editing;

/// <summary>One editable trait or action. Structured attack data is carried through untouched.</summary>
public sealed class EntryDraft
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ActionType { get; set; } = "ACTION";
    public int? LegendaryActionCost { get; set; }
    public List<AttackEntry> Attacks { get; set; } = [];
}

/// <summary>
/// An editable stat block. Everything is plain text so form fields can bind to it directly;
/// <see cref="TryBuild"/> validates the text and produces a real <see cref="Creature"/>.
/// </summary>
public sealed partial class CreatureDraft
{
    public static readonly IReadOnlyList<string> Sizes = ["Tiny", "Small", "Medium", "Large", "Huge", "Gargantuan"];

    public static readonly IReadOnlyList<string> ActionTypes =
        ["ACTION", "BONUS_ACTION", "REACTION", "LEGENDARY_ACTION", "LAIR_ACTION"];

    private static readonly Dictionary<string, double> FractionalChallengeRatings = new()
    {
        ["1/8"] = 0.125, ["1/4"] = 0.25, ["1/2"] = 0.5
    };

    private static readonly Dictionary<int, int> ExperienceTable = new()
    {
        [0] = 10, [1] = 200, [2] = 450, [3] = 700, [4] = 1100, [5] = 1800, [6] = 2300, [7] = 2900, [8] = 3900,
        [9] = 5000, [10] = 5900, [11] = 7200, [12] = 8400, [13] = 10000, [14] = 11500, [15] = 13000, [16] = 15000,
        [17] = 18000, [18] = 20000, [19] = 22000, [20] = 25000, [21] = 33000, [22] = 41000, [23] = 50000,
        [24] = 62000, [25] = 75000, [26] = 90000, [27] = 105000, [28] = 120000, [29] = 135000, [30] = 155000
    };

    private List<string> _environments = [];

    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Size { get; set; } = "Medium";
    public string Type { get; set; } = "Humanoid";
    public string Alignment { get; set; } = "";

    public string ArmorClass { get; set; } = "10";
    public string ArmorDetail { get; set; } = "";
    public string HitPoints { get; set; } = "4";
    public string HitDice { get; set; } = "";
    public string ChallengeRating { get; set; } = "0";

    public string Speed { get; set; } = "30 ft.";

    public string Strength { get; set; } = "10";
    public string Dexterity { get; set; } = "10";
    public string Constitution { get; set; } = "10";
    public string Intelligence { get; set; } = "10";
    public string Wisdom { get; set; } = "10";
    public string Charisma { get; set; } = "10";

    /// <summary>Abilities the creature is proficient in saves for, e.g. "DEX, WIS".</summary>
    public string SaveProficiencies { get; set; } = "";

    /// <summary>Skill bonuses for skills the creature is proficient in, e.g. "Stealth +6, Perception +4".</summary>
    public string Skills { get; set; } = "";

    public string Senses { get; set; } = "";
    public string Languages { get; set; } = "";

    /// <summary>Damage and condition lists use semicolons between entries, e.g. "Fire; Cold".</summary>
    public string Vulnerabilities { get; set; } = "";
    public string Resistances { get; set; } = "";
    public string DamageImmunities { get; set; } = "";
    public string ConditionImmunities { get; set; } = "";

    public ObservableCollection<EntryDraft> Traits { get; } = [];
    public ObservableCollection<EntryDraft> Actions { get; } = [];

    public static CreatureDraft Blank() => new()
    {
        Key = NewKey(),
        Name = "New Creature",
        HitDice = "1d8"
    };

    /// <summary>
    /// Drafts an existing creature. A custom creature keeps its key (editing in place); anything else becomes a
    /// new custom copy with a fresh key, so the original is never changed.
    /// </summary>
    public static CreatureDraft FromCreature(Creature creature)
    {
        var isCustom = creature.SourceKey == ContentSources.Custom;

        var draft = new CreatureDraft
        {
            Key = isCustom && !string.IsNullOrWhiteSpace(creature.Key) ? creature.Key : NewKey(),
            Name = isCustom ? creature.Name : creature.Name + " (Copy)",
            Size = creature.Size,
            Type = creature.Type,
            Alignment = creature.Alignment,
            ArmorClass = creature.ArmorClass.ToString(CultureInfo.InvariantCulture),
            ArmorDetail = creature.ArmorDetail,
            HitPoints = creature.HitPoints.ToString(CultureInfo.InvariantCulture),
            HitDice = creature.HitDice,
            ChallengeRating = FormatChallengeRating(creature.ChallengeRating),
            Speed = creature.Speed,
            Strength = Score(creature, Ability.Strength),
            Dexterity = Score(creature, Ability.Dexterity),
            Constitution = Score(creature, Ability.Constitution),
            Intelligence = Score(creature, Ability.Intelligence),
            Wisdom = Score(creature, Ability.Wisdom),
            Charisma = Score(creature, Ability.Charisma),
            SaveProficiencies = string.Join(", ", creature.SavingThrowProficiencies
                .Where(s => AbilityExtensions.TryParse(s, out _))
                .Select(s => { AbilityExtensions.TryParse(s, out var a); return a.Abbreviation(); })),
            Skills = string.Join(", ", creature.Skills.OrderBy(s => s.Key)
                .Select(s => $"{Capitalize(s.Key.Replace('_', ' '))} {Creature.Signed(s.Value)}")),
            Senses = creature.Senses,
            Languages = creature.Languages,
            Vulnerabilities = string.Join("; ", creature.DamageVulnerabilities),
            Resistances = string.Join("; ", creature.DamageResistances),
            DamageImmunities = string.Join("; ", creature.DamageImmunities),
            ConditionImmunities = string.Join("; ", creature.ConditionImmunities),
            _environments = [.. creature.Environments]
        };

        foreach (var trait in creature.Traits)
            draft.Traits.Add(new EntryDraft { Name = trait.Name, Description = trait.Description });

        foreach (var action in creature.Actions)
        {
            draft.Actions.Add(new EntryDraft
            {
                Name = action.Name,
                Description = action.Description,
                ActionType = action.ActionType,
                LegendaryActionCost = action.LegendaryActionCost,
                Attacks = [.. action.Attacks]
            });
        }

        return draft;
    }

    public static string NewKey() => "custom_" + Guid.NewGuid().ToString("N");

    /// <summary>Checks every field. On success returns a complete creature; otherwise lists every problem found.</summary>
    public bool TryBuild(out Creature? creature, out IReadOnlyList<string> errors)
    {
        var problems = new List<string>();
        creature = null;

        if (string.IsNullOrWhiteSpace(Name)) problems.Add("Enter a name.");

        var size = Sizes.FirstOrDefault(s => s.Equals(Size?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (size is null) problems.Add($"Size must be one of: {string.Join(", ", Sizes)}.");

        if (string.IsNullOrWhiteSpace(Type)) problems.Add("Enter a creature type.");

        var armorClass = ParseInt(ArmorClass, 1, 40, "Armor Class", problems);
        var hitPoints = ParseInt(HitPoints, 1, 9999, "Hit Points", problems);

        if (!string.IsNullOrWhiteSpace(HitDice) && !DiceExpression.TryParse(HitDice, out _))
            problems.Add($"Hit Dice '{HitDice}' is not valid dice notation (try 2d8+2).");

        var scores = new Dictionary<Ability, int>
        {
            [Ability.Strength] = ParseInt(Strength, 1, 30, "Strength", problems),
            [Ability.Dexterity] = ParseInt(Dexterity, 1, 30, "Dexterity", problems),
            [Ability.Constitution] = ParseInt(Constitution, 1, 30, "Constitution", problems),
            [Ability.Intelligence] = ParseInt(Intelligence, 1, 30, "Intelligence", problems),
            [Ability.Wisdom] = ParseInt(Wisdom, 1, 30, "Wisdom", problems),
            [Ability.Charisma] = ParseInt(Charisma, 1, 30, "Charisma", problems)
        };

        if (!TryParseChallengeRating(ChallengeRating, out var cr))
            problems.Add("Challenge Rating must be 0, 1/8, 1/4, 1/2 or a whole number up to 30.");

        var saveProficiencies = ParseSaveProficiencies(problems);
        var skills = ParseSkills(problems);
        var traits = BuildEntries(Traits, "Trait", problems);
        var actions = BuildEntries(Actions, "Action", problems);

        foreach (var action in Actions.Where(a => !ActionTypes.Contains(a.ActionType)))
            problems.Add($"Action '{action.Name}' has an unknown type '{action.ActionType}'.");

        errors = problems;
        if (problems.Count > 0) return false;

        var proficiency = Proficiency.ForChallengeRating(cr);
        var dexMod = AbilityScores.ModifierFor(scores[Ability.Dexterity]);
        var wisMod = AbilityScores.ModifierFor(scores[Ability.Wisdom]);

        creature = new Creature
        {
            Key = string.IsNullOrWhiteSpace(Key) ? NewKey() : Key,
            Name = Name.Trim(),
            Size = size!,
            Type = Type.Trim(),
            Alignment = Alignment.Trim(),
            ArmorClass = armorClass,
            ArmorDetail = ArmorDetail.Trim(),
            HitPoints = hitPoints,
            HitDice = HitDice.Trim(),
            ChallengeRating = cr,
            ChallengeRatingText = FormatChallengeRating(cr),
            ExperiencePoints = ExperienceFor(cr),
            ProficiencyBonus = proficiency,
            InitiativeBonus = dexMod,
            Speed = Speed.Trim(),
            WalkSpeed = ParseWalkSpeed(Speed),
            AbilityScores = scores.ToDictionary(s => s.Key.ToString().ToLowerInvariant(), s => s.Value),
            SavingThrows = scores.ToDictionary(
                s => s.Key.ToString().ToLowerInvariant(),
                s => AbilityScores.ModifierFor(s.Value) + (saveProficiencies.Contains(s.Key) ? proficiency : 0)),
            SavingThrowProficiencies = saveProficiencies.Select(a => a.ToString().ToLowerInvariant()).ToList(),
            Skills = skills,
            PassivePerception = 10 + (skills.TryGetValue("perception", out var perception) ? perception : wisMod),
            Senses = Senses.Trim(),
            Languages = Languages.Trim(),
            DamageVulnerabilities = SplitList(Vulnerabilities),
            DamageResistances = SplitList(Resistances),
            DamageImmunities = SplitList(DamageImmunities),
            ConditionImmunities = SplitList(ConditionImmunities),
            Environments = [.. _environments],
            Traits = traits.Select(t => new NamedEntry { Name = t.Name, Description = t.Description }).ToList(),
            Actions = actions.Select(a => new ActionEntry
            {
                Name = a.Name,
                Description = a.Description,
                ActionType = a.ActionType,
                LegendaryActionCost = a.LegendaryActionCost,
                Attacks = [.. a.Attacks]
            }).ToList(),
            SourceKey = ContentSources.Custom,
            SourceName = "Custom"
        };

        return true;
    }

    public static bool TryParseChallengeRating(string? text, out double value)
    {
        value = 0;
        var trimmed = text?.Trim() ?? "";

        if (FractionalChallengeRatings.TryGetValue(trimmed, out value)) return true;

        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var whole) && whole is >= 0 and <= 30)
        {
            value = whole;
            return true;
        }

        return false;
    }

    public static string FormatChallengeRating(double cr) => cr switch
    {
        0.125 => "1/8",
        0.25 => "1/4",
        0.5 => "1/2",
        _ => cr.ToString("0.##", CultureInfo.InvariantCulture)
    };

    /// <summary>XP value for a challenge rating (CR 0 uses 10 XP).</summary>
    public static int ExperienceFor(double cr) => cr switch
    {
        0.125 => 25,
        0.25 => 50,
        0.5 => 100,
        _ => ExperienceTable.GetValueOrDefault((int)Math.Round(cr), 0)
    };

    private static int? ParseWalkSpeed(string speed)
    {
        var match = WalkSpeedPattern().Match(speed ?? "");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static int ParseInt(string? text, int min, int max, string label, List<string> problems)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
            return value;

        problems.Add($"{label} must be a whole number from {min} to {max}.");
        return min;
    }

    private HashSet<Ability> ParseSaveProficiencies(List<string> problems)
    {
        var result = new HashSet<Ability>();
        foreach (var part in SplitCommas(SaveProficiencies))
        {
            if (AbilityExtensions.TryParse(part, out var ability)) result.Add(ability);
            else problems.Add($"'{part}' is not an ability (use STR, DEX, CON, INT, WIS or CHA).");
        }
        return result;
    }

    private Dictionary<string, int> ParseSkills(List<string> problems)
    {
        var result = new Dictionary<string, int>();

        foreach (var part in SplitCommas(Skills))
        {
            var match = SkillBonusPattern().Match(part);
            var skill = match.Success
                ? SkillExtensions.All.Cast<Skill?>().FirstOrDefault(s =>
                    string.Equals(s!.Value.DisplayName(), match.Groups[1].Value.Trim(), StringComparison.OrdinalIgnoreCase))
                : null;

            if (skill is null)
            {
                problems.Add($"Could not read skill '{part}'. Use a name and bonus such as 'Stealth +6'.");
                continue;
            }

            result[SkillKey(skill.Value)] = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static List<EntryDraft> BuildEntries(IEnumerable<EntryDraft> entries, string label, List<string> problems)
    {
        var result = new List<EntryDraft>();
        foreach (var entry in entries)
        {
            var hasName = !string.IsNullOrWhiteSpace(entry.Name);
            var hasText = !string.IsNullOrWhiteSpace(entry.Description);

            if (!hasName && !hasText) continue;

            if (!hasName)
            {
                problems.Add($"A {label.ToLowerInvariant()} has text but no name.");
                continue;
            }

            result.Add(new EntryDraft
            {
                Name = entry.Name.Trim(),
                Description = entry.Description.Trim(),
                ActionType = entry.ActionType,
                LegendaryActionCost = entry.LegendaryActionCost,
                Attacks = entry.Attacks
            });
        }
        return result;
    }

    private static string SkillKey(Skill skill) => SkillKeyPattern().Replace(skill.ToString(), "_").ToLowerInvariant();

    private static string Score(Creature creature, Ability ability)
        => creature.AbilityScores.GetValueOrDefault(ability.ToString().ToLowerInvariant(), 10).ToString(CultureInfo.InvariantCulture);

    private static string Capitalize(string text)
        => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static List<string> SplitList(string? text)
        => (text ?? "").Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    private static string[] SplitCommas(string? text)
        => (text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"^\s*(?:walk\s+)?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex WalkSpeedPattern();

    [GeneratedRegex(@"^(.+?)\s*([+-]\d+)$")]
    private static partial Regex SkillBonusPattern();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex SkillKeyPattern();
}