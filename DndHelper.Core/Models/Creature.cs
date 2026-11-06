using System.Text.Json.Serialization;

namespace DndHelper.Core.Models;

public sealed class Creature
{
    private static readonly string[] AbilityOrder = ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"];
    private static readonly string[] AbilityAbbreviations = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Alignment { get; init; } = string.Empty;
    
    public int ArmorClass { get; init; } 
    public string ArmorDetail { get; init; } = string.Empty;
    public int HitPoints { get; init; }
    public string HitDice { get; init; } = string.Empty;

    public double ChallengeRating { get; init; }
    public string ChallengeRatingText { get; init; } = string.Empty;
    public int? ExperiencePoints { get; init; }
    public int? ProficiencyBonus { get; init; }
    public int InitiativeBonus { get; init; }

    public string Speed { get; init; } = string.Empty;
    public int? WalkSpeed { get; init; }

    /// <summary>Ability scores keyed by lower-case ability name</summary>
    public Dictionary<string, int> AbilityScores { get; init; } = [];

    /// <summary> Total saving throw bonus for every ability. </summary>
    public Dictionary<string, int> SavingThrows { get; init; } = [];

    public List<string> SavingThrowProficiencies { get; init; } = [];

    public Dictionary<string, int> Skills { get; init; } = [];

    public int PassivePerception { get; init; }
    public string Senses { get; init; } = string.Empty;
    public string Languages { get; init; } = string.Empty;

    public List<string> DamageVulnerabilities { get; init; } = [];
    public List<string> DamageResistances { get; init; } = [];
    public List<string> DamageImmunities { get; init; } = [];
    public List<string> ConditionImmunities { get; init; } = [];

    public List<string> Environments { get; init; } = [];
    public List<NamedEntry> Traits { get; init; } = [];
    public List<ActionEntry> Actions { get; init; } = [];

    public string SourceKey { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;

    [JsonIgnore]
    public string Subtitle
    {
        get
        {
            var text = $"{Size} {Type}".Trim();
            return string.IsNullOrWhiteSpace(Alignment) ? text : $"{text}, {Alignment}";
        }
    }

    [JsonIgnore]
    public bool HasTraits => Traits.Count > 0;

    [JsonIgnore]
    public string ArmorClassText
        => string.IsNullOrWhiteSpace(ArmorDetail) ? ArmorClass.ToString() : $"{ArmorClass} ({ArmorDetail})";

    [JsonIgnore]
    public string HitPointsText
        => string.IsNullOrWhiteSpace(HitDice) ? HitPoints.ToString() : $"{HitPoints} ({HitDice})";
    
    [JsonIgnore]
    public string ChallengeText
        => ExperiencePoints is { } xp ? $"CR {ChallengeRatingText} ({xp:N0} XP)" : $"CR {ChallengeRatingText}";
    
    [JsonIgnore]
    public IReadOnlyList<AbilitiesDisplay> AbilitiesDisplays
    {
        get
        {
            var list = new List<AbilitiesDisplay>(6);
            for (var i = 0; i < AbilityOrder.Length; i++)
            {
                var key = AbilityOrder[i];
                var score = AbilityScores.GetValueOrDefault(key, 10);
                var save = SavingThrows.GetValueOrDefault(key, Modifier(score));
                list.Add(new AbilitiesDisplay(
                    AbilityAbbreviations[i],
                    score,
                    Signed(Modifier(score)),
                    Signed(save),
                    SavingThrowProficiencies.Contains(key)));
            }

            return list;
        }
    }

    [JsonIgnore]
    public IReadOnlyList<LabelValue> Properties
    {
        get
        {
            var list = new List<LabelValue>();
            Add(list, "Speed", Speed);
            Add(list, "Initiative", Signed(InitiativeBonus));
            Add(list, "Skills", string.Join(", ", Skills.OrderBy(s => s.Key).Select(s => $"{Humanize(s.Key)} {Signed(s.Value)}")));
            Add(list, "Vulnerabilities", string.Join(", ", DamageVulnerabilities));
            Add(list, "Resistances", string.Join(", ", DamageResistances));
            Add(list, "Damage Immunities", string.Join(", ", DamageImmunities));
            Add(list, "Condition Immunities", string.Join(", ", ConditionImmunities));
            Add(list, "Senses", string.IsNullOrWhiteSpace(Senses) ? $"Passive Perception {PassivePerception}" : $"{Senses}, Passive Perception {PassivePerception}");
            Add(list, "Languages", Languages);
            Add(list, "Environment", string.Join(", ", Environments));
            return list;
        }
    }

    [JsonIgnore]
    public IReadOnlyList<ActionGroup> ActionGroups
    {
        get
        {
            (string Type, string Title)[] order =
            [
                ("ACTION", "Actions"),
                ("BONUS_ACTION", "Bonus Actions"),
                ("REACTION", "Reactions"),
                ("LEGENDARY_ACTION", "Legendary Actions"),
                ("LAIR_ACTION", "Lair Actions")
            ];

            var groups = new List<ActionGroup>();
            foreach (var (type, title) in order)
            {
                var entries = Actions
                    .Where(a => string.Equals(a.ActionType, type, StringComparison.OrdinalIgnoreCase)
                    || (type == "ACTION" && !order.Any(o => string.Equals(o.Type, a.ActionType, StringComparison.OrdinalIgnoreCase))))
                    .ToList();
                
                if (entries.Count > 0)
                    groups.Add(new ActionGroup(title, entries));
            }

            return groups;
        }
    }

    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    public static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static void Add(List<LabelValue> list, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            list.Add(new LabelValue(label, value));
    }

    private static string Humanize(string key)
    {
        var words = key.Replace('_', ' ');
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }
}