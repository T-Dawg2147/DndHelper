using System.Globalization;
using System.Text.Json;
using DndHelper.Core.Models;

namespace DndHelper.Data.Open5e;

/// <summary>Converts Open5e v2 JSON payloads into the app's own models. Tolerant of missing/null fields.</summary>
public static class Open5eMapper
{
    private static readonly string[] Abilities = ["strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma"];

    private static readonly Dictionary<string, string> SkillAbility = new()
    {
        ["acrobatics"] = "dexterity", ["animal_handling"] = "wisdom", ["arcana"] = "intelligence",
        ["athletics"] = "strength", ["deception"] = "charisma", ["history"] = "intelligence",
        ["insight"] = "wisdom", ["intimidation"] = "charisma", ["investigation"] = "intelligence",
        ["medicine"] = "wisdom", ["nature"] = "intelligence", ["perception"] = "wisdom",
        ["performance"] = "charisma", ["persuasion"] = "charisma", ["religion"] = "intelligence",
        ["sleight_of_hand"] = "dexterity", ["stealth"] = "dexterity", ["survival"] = "wisdom"
    };

    public static Creature ParseCreature(JsonElement e)
    {
        var scores = new Dictionary<string, int>();
        var scoreObj = e.Obj("ability_scores");
        foreach (var ability in Abilities)
            scores[ability] = scoreObj?.Int(ability) ?? 10;

        var proficiencies = new List<string>();
        var proficientSaves = e.Obj("saving_throws");
        if (proficientSaves is { } ps)
        {
            foreach (var ability in Abilities)
                if (ps.Has(ability)) proficiencies.Add(ability);
        }

        var saves = new Dictionary<string, int>();
        var savesAll = e.Obj("saving_throws_all");
        foreach (var ability in Abilities)
            saves[ability] = savesAll?.Int(ability) ?? Creature.Modifier(scores[ability]);

        var skills = new Dictionary<string, int>();
        if (e.Obj("skill_bonuses") is { } skillObj)
        {
            foreach (var prop in skillObj.EnumerateObject())
            {
                if (!prop.Value.TryGetInt32(out var bonus)) continue;
                var baseMod = SkillAbility.TryGetValue(prop.Name, out var ab) ? Creature.Modifier(scores[ab]) : int.MinValue;
                if (bonus != baseMod) skills[prop.Name] = bonus;
            }
        }

        var cr = e.Dbl("challenge_rating_decimal") ?? e.Dbl("challenge_rating") ?? 0;
        var crText = e.Str("challenge_rating_text");
        if (string.IsNullOrWhiteSpace(crText)) crText = FormatChallengeRating(cr);

        var (sourceKey, sourceName) = ReadDocument(e);
        var (speedText, walk) = ReadSpeed(e);

        return new Creature
        {
            Key = e.Str("key"),
            Name = e.Str("name"),
            Size = e.NameOf("size"),
            Type = e.NameOf("type"),
            Alignment = e.Str("alignment"),
            ArmorClass = e.Int("armor_class") ?? 10,
            ArmorDetail = e.Str("armor_detail"),
            HitPoints = e.Int("hit_points") ?? 0,
            HitDice = e.Str("hit_dice"),
            ChallengeRating = cr,
            ChallengeRatingText = crText,
            ExperiencePoints = e.Int("experience_points"),
            ProficiencyBonus = e.Int("proficiency_bonus"),
            InitiativeBonus = e.Int("initiative_bonus") ?? Creature.Modifier(scores["dexterity"]),
            Speed = speedText,
            WalkSpeed = walk,
            AbilityScores = scores,
            SavingThrows = saves,
            SavingThrowProficiencies = proficiencies,
            Skills = skills,
            PassivePerception = e.Int("passive_perception") ?? 10 + Creature.Modifier(scores["wisdom"]),
            Senses = ReadSenses(e),
            Languages = e.Obj("languages")?.Str("as_string") ?? "",
            DamageVulnerabilities = ReadResist(e, "damage_vulnerabilities"),
            DamageResistances = ReadResist(e, "damage_resistances"),
            DamageImmunities = ReadResist(e, "damage_immunities"),
            ConditionImmunities = ReadResist(e, "condition_immunities"),
            Environments = e.Arr("environments").Select(env => env.ValueKind == JsonValueKind.Object ? env.Str("name") : env.GetString() ?? "")
                .Where(s => s.Length > 0).ToList(),
            Traits = e.Arr("traits").Select(t => new NamedEntry { Name = t.Str("name"), Description = t.Str("desc") }).ToList(),
            Actions = e.Arr("actions")
                .OrderBy(a => a.Int("order_in_statblock") ?? 0)
                .Select(ParseAction)
                .ToList(),
            SourceKey = sourceKey,
            SourceName = sourceName
        };
    }

    public static Spell ParseSpell(JsonElement e)
    {
        var (sourceKey, sourceName) = ReadDocument(e);
        var unit = e.Str("range_unit");
        var shapeUnit = e.Str("shape_size_unit");

        return new Spell
        {
            Key = e.Str("key"),
            Name = e.Str("name"),
            Level = e.Int("level") ?? 0,
            School = e.NameOf("school"),
            Description = e.Str("desc"),
            HigherLevel = e.Str("higher_level"),
            CastingTime = PrettyCastingTime(e.Str("casting_time")),
            Range = !string.IsNullOrWhiteSpace(e.Str("range_text"))
                ? e.Str("range_text")
                : e.Dbl("range") is { } r ? $"{r:0.##} {unit}".Trim() : "",
            Duration = e.Str("duration"),
            Ritual = e.Bool("ritual"),
            Concentration = e.Bool("concentration"),
            Verbal = e.Bool("verbal"),
            Somatic = e.Bool("somatic"),
            Material = e.Bool("material"),
            MaterialDescription = e.Str("material_specified"),
            SavingThrowAbility = e.Str("saving_throw_ability").ToLowerInvariant(),
            AttackRoll = e.Bool("attack_roll"),
            DamageRoll = e.Str("damage_roll"),
            DamageTypes = e.Arr("damage_types").Select(NameOrString).Where(s => s.Length > 0).ToList(),
            ShapeType = NullIfEmpty(e.Str("shape_type")),
            ShapeSize = e.Dbl("shape_size"),
            ShapeSizeUnit = NullIfEmpty(shapeUnit),
            TargetType = NullIfEmpty(e.Str("target_type")),
            Classes = e.Arr("classes").Select(NameOrString).Where(s => s.Length > 0).Distinct().ToList(),
            SourceKey = sourceKey,
            SourceName = sourceName
        };
    }

    public static string FormatChallengeRating(double cr) => cr switch
    {
        0.125 => "1/8",
        0.25 => "1/4",
        0.5 => "1/2",
        _ => cr.ToString("0.##", CultureInfo.InvariantCulture)
    };

    private static ActionEntry ParseAction(JsonElement a) => new()
    {
        Name = a.Str("name"),
        Description = a.Str("desc"),
        ActionType = string.IsNullOrWhiteSpace(a.Str("action_type")) ? "ACTION" : a.Str("action_type").ToUpperInvariant(),
        LegendaryActionCost = a.Int("legendary_action_cost"),
        Attacks = a.Arr("attacks").Select(at => new AttackEntry
        {
            Name = at.Str("name"),
            ToHit = at.Int("to_hit_mod"),
            DamageDieCount = at.Int("damage_die_count"),
            DamageDieType = NullIfEmpty(at.Str("damage_die_type").ToLowerInvariant()),
            DamageBonus = at.Int("damage_bonus"),
            DamageType = NullIfEmpty(at.NameOf("damage_type")),
            Reach = at.Int("reach"),
            Range = at.Int("range"),
            LongRange = at.Int("long_range")
        }).ToList()
    };

    private static (string Key, string Name) ReadDocument(JsonElement e)
    {
        if (!e.TryGetProperty("document", out var doc)) return ("", "");
        return doc.ValueKind switch
        {
            JsonValueKind.Object => (doc.Str("key"), doc.Str("display_name") is { Length: > 0 } dn ? dn : doc.Str("name")),
            JsonValueKind.String => (doc.GetString()!.TrimEnd('/').Split('/').Last(), ""),
            _ => ("", "")
        };
    }

    private static (string Text, int? Walk) ReadSpeed(JsonElement e)
    {
        var speed = e.Obj("speed_all") ?? e.Obj("speed");
        if (speed is not { } s) return ("", null);

        var unit = s.Str("unit") is { Length: > 0 } u ? (u == "feet" ? "ft." : u) : "ft.";
        var parts = new List<string>();
        foreach (var mode in new[] { "walk", "burrow", "climb", "crawl", "fly", "swim" })
        {
            if (s.Int(mode) is > 0 and var v)
                parts.Add(mode == "walk" ? $"{v} {unit}" : $"{mode} {v} {unit}" + (mode == "fly" && s.Bool("hover") ? " (hover)" : ""));
        }
        return (string.Join(", ", parts), s.Int("walk"));
    }

    private static string ReadSenses(JsonElement e)
    {
        var parts = new List<string>();
        void AddSense(string label, string field)
        {
            if (e.Int(field) is > 0 and var v) parts.Add($"{label} {v} ft.");
        }

        AddSense("Blindsight", "blindsight_range");
        AddSense("Darkvision", "darkvision_range");
        AddSense("Tremorsense", "tremorsense_range");
        AddSense("Truesight", "truesight_range");
        return string.Join(", ", parts);
    }

    private static List<string> ReadResist(JsonElement e, string field)
    {
        if (e.Obj("resistances_and_immunities") is not { } r) return [];

        // Prefer the human-readable display string; fall back to the structured list.
        var display = r.Str(field + "_display");
        if (!string.IsNullOrWhiteSpace(display))
            return display.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

        return r.Arr(field).Select(NameOrString).Where(s => s.Length > 0).ToList();
    }

    private static string PrettyCastingTime(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        var match = System.Text.RegularExpressions.Regex.Match(key, @"^(\d+)([a-z]+)$");
        if (match.Success) return $"{match.Groups[1].Value} {match.Groups[2].Value}";

        var text = key.Replace('_', ' ');
        text = char.ToUpperInvariant(text[0]) + text[1..];
        return key is "action" or "bonus_action" or "reaction" ? "1 " + text.ToLowerInvariant() : text;
    }

    private static string NameOrString(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString() ?? "",
        JsonValueKind.Object => e.Str("name") is { Length: > 0 } n ? n : e.Str("key"),
        _ => ""
    };

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // ---- tolerant JSON helpers ----

    private static bool Has(this JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null;

    private static string Str(this JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static bool Bool(this JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static double? Dbl(this JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }

    private static int? Int(this JsonElement e, string name)
        => e.Dbl(name) is { } d ? (int)Math.Round(d) : null;

    private static JsonElement? Obj(this JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static IEnumerable<JsonElement> Arr(this JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray()
            : [];

    /// <summary>Reads a field that is either a plain string or an object with a "name" (e.g. { key, name }).</summary>
    private static string NameOf(this JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return "";
        return NameOrString(v);
    }
}