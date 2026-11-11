using System.Text.Json;
using DndHelper.Data.Open5e;
using FluentAssertions;

namespace DndHelper.Tests.Data;

public class Open5eMapperTests
{
    internal const string GoblinJson = """
    {
      "key": "srd_goblin", "name": "Goblin", "alignment": "neutral evil",
      "armor_class": 15, "armor_detail": "leather armor, shield",
      "hit_points": 7, "hit_dice": "2d6", "challenge_rating": 0.25, "experience_points": 50,
      "initiative_bonus": 2, "passive_perception": 9, "darkvision_range": 60, "blindsight_range": null,
      "ability_scores": { "strength": 8, "dexterity": 14, "constitution": 10, "intelligence": 10, "wisdom": 8, "charisma": 8 },
      "saving_throws": {},
      "saving_throws_all": { "strength": -1, "dexterity": 2, "constitution": 0, "intelligence": 0, "wisdom": -1, "charisma": -1 },
      "skill_bonuses": { "stealth": 6 },
      "speed_all": { "walk": 30, "climb": 15, "fly": 0, "swim": 15, "burrow": 0, "crawl": 15, "hover": false, "unit": "feet" },
      "size": { "key": "small", "name": "Small" },
      "type": { "key": "humanoid", "name": "Humanoid" },
      "languages": { "as_string": "Common, Goblin", "data": [] },
      "document": { "key": "srd-2014", "name": "System Reference Document 5.1", "display_name": "5e 2014 Rules" },
      "environments": [ { "key": "caves", "name": "Caves" }, { "key": "forest", "name": "Forest or Jungle" } ],
      "resistances_and_immunities": {
        "damage_resistances": [], "damage_resistances_display": "",
        "damage_immunities": [], "damage_immunities_display": "",
        "damage_vulnerabilities": [], "damage_vulnerabilities_display": "",
        "condition_immunities": [], "condition_immunities_display": ""
      },
      "traits": [ { "name": "Nimble Escape", "desc": "Disengage or Hide as a bonus action." } ],
      "actions": [
        { "name": "Shortbow", "desc": "Ranged attack.", "action_type": "ACTION", "order_in_statblock": 1, "legendary_action_cost": null,
          "attacks": [ { "name": "Shortbow attack", "to_hit_mod": 4, "damage_die_count": 1, "damage_die_type": "D6",
                         "damage_bonus": null, "damage_type": { "key": "piercing", "name": "Piercing" },
                         "reach": null, "range": 80, "long_range": 320 } ] },
        { "name": "Scimitar", "desc": "Melee attack.", "action_type": "ACTION", "order_in_statblock": 0, "attacks": [] },
        { "name": "Redirect Attack", "desc": "Swap places.", "action_type": "REACTION", "order_in_statblock": 2, "attacks": [] }
      ]
    }
    """;

    private const string FireballJson = """
    {
      "key": "srd-2024_fireball", "name": "Fireball", "level": 3,
      "school": { "key": "evocation", "name": "Evocation" },
      "desc": "A bright streak flashes.", "higher_level": "Damage increases by 1d6.",
      "range_text": "150 feet", "range": 150, "range_unit": "feet",
      "casting_time": "action", "duration": "instantaneous",
      "verbal": true, "somatic": true, "material": true, "material_specified": "a ball of bat guano and sulfur",
      "ritual": false, "concentration": false, "attack_roll": false,
      "saving_throw_ability": "dexterity", "damage_roll": "8d6", "damage_types": ["fire"],
      "shape_type": "sphere", "shape_size": 20.0, "shape_size_unit": "feet",
      "classes": [ { "key": "wizard", "name": "Wizard" }, { "key": "sorcerer", "name": "Sorcerer" } ],
      "document": { "key": "srd-2024", "name": "SRD 5.2" }
    }
    """;

    internal static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void ParseCreature_MapsCoreStats()
    {
        var c = Open5eMapper.ParseCreature(Parse(GoblinJson));

        c.Key.Should().Be("srd_goblin");
        c.Name.Should().Be("Goblin");
        c.Size.Should().Be("Small");
        c.Type.Should().Be("Humanoid");
        c.ArmorClassText.Should().Be("15 (leather armor, shield)");
        c.HitPointsText.Should().Be("7 (2d6)");
        c.ChallengeRating.Should().Be(0.25);
        c.ChallengeRatingText.Should().Be("1/4");
        c.ChallengeText.Should().Be("CR 1/4 (50 XP)");
        c.SourceKey.Should().Be("srd-2014");
        c.SourceName.Should().Be("5e 2014 Rules");
    }

    [Fact]
    public void ParseCreature_BuildsSpeedAndSenses()
    {
        var c = Open5eMapper.ParseCreature(Parse(GoblinJson));

        c.Speed.Should().Be("30 ft., climb 15 ft., crawl 15 ft., swim 15 ft.");
        c.WalkSpeed.Should().Be(30);
        c.Senses.Should().Be("Darkvision 60 ft.");
    }

    [Fact]
    public void ParseCreature_AbilityTableAndSkills()
    {
        var c = Open5eMapper.ParseCreature(Parse(GoblinJson));

        c.AbilityDisplays.Should().HaveCount(6);
        c.AbilityDisplays[1].Should().Be(new DndHelper.Core.Models.AbilityDisplays("DEX", 14, "+2", "+2", false));
        c.Skills.Should().ContainKey("stealth").WhoseValue.Should().Be(6);
        c.Skills.Should().HaveCount(1);
    }

    [Fact]
    public void ParseCreature_OrdersAndGroupsActions()
    {
        var c = Open5eMapper.ParseCreature(Parse(GoblinJson));

        c.Actions.Select(a => a.Name).Should().Equal("Scimitar", "Shortbow", "Redirect Attack");
        c.ActionGroups.Select(g => g.Title).Should().Equal("Actions", "Reactions");

        var attack = c.Actions[1].Attacks.Single();
        attack.ToHit.Should().Be(4);
        attack.DamageDieCount.Should().Be(1);
        attack.DamageDieType.Should().Be("d6");
        attack.DamageType.Should().Be("Piercing");
        attack.LongRange.Should().Be(320);
    }

    [Fact]
    public void ParseCreature_ToleratesMissingFields()
    {
        var c = Open5eMapper.ParseCreature(Parse("""{ "key": "x", "name": "Blob" }"""));

        c.Name.Should().Be("Blob");
        c.AbilityScores.Should().HaveCount(6).And.OnlyContain(kv => kv.Value == 10);
        c.Actions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(0.125, "1/8")]
    [InlineData(0.5, "1/2")]
    [InlineData(5, "5")]
    [InlineData(30, "30")]
    public void FormatChallengeRating_UsesFractions(double cr, string expected)
        => Open5eMapper.FormatChallengeRating(cr).Should().Be(expected);

    [Fact]
    public void ParseSpell_MapsAreaOfEffect()
    {
        var s = Open5eMapper.ParseSpell(Parse(FireballJson));

        s.Name.Should().Be("Fireball");
        s.Subtitle.Should().Be("Level 3 Evocation");
        s.CastingTime.Should().Be("1 action");
        s.ComponentsText.Should().Be("V, S, M (a ball of bat guano and sulfur)");
        s.SavingThrowAbility.Should().Be("dexterity");
        s.DamageRoll.Should().Be("8d6");
        s.DamageTypes.Should().Equal("fire");
        s.ShapeType.Should().Be("sphere");
        s.ShapeSize.Should().Be(20);
        s.AreaText.Should().Be("20 feet sphere");
        s.Classes.Should().Equal("Wizard", "Sorcerer");
        s.Range.Should().Be("150 feet");
    }
}