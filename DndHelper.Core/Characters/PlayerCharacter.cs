using System.Text.Json.Serialization;
using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

/// <summary>A saved player character: only the choices made, never the derived numbers.</summary>
public sealed class PlayerCharacter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Species { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Background { get; set; } = "";
    public int Level { get; set; } = 1;

    public ScoreMethod Method { get; set; } = ScoreMethod.StandardArray;

    /// <summary>Scores before background increases.</summary>
    public Dictionary<Ability, int> BaseScores { get; set; } = AbilityExtensions.All.ToDictionary(a => a, _ => 10);

    /// <summary>The background's ability increases, e.g. { Strength: 2, Dexterity: 1 }.</summary>
    public Dictionary<Ability, int> BackgroundBonuses { get; set; } = [];

    /// <summary>Skills chosen from the class list (background skills are added automatically).</summary>
    public List<Skill> ClassSkills { get; set; } = [];

    /// <summary>Set when armor, a shield or class features give an Armor Class other than 10 + Dexterity modifier.</summary>
    public int? ArmorClassOverride { get; set; }

    public string Notes { get; set; } = "";

    [JsonIgnore]
    public string Summary => $"Level {Level} {Species} {ClassName}".Replace("  ", " ").Trim();
}