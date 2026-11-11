using DndHelper.Core.Characters;
using DndHelper.Core.Editing;
using DndHelper.Core.Models;
using DndHelper.Core.Rules;
using FluentAssertions;

namespace DndHelper.Tests.Editing;

public class CreatureDraftTests
{
    private static Creature Goblin() => new()
    {
        Key = "srd_goblin",
        Name = "Goblin",
        Size = "Small",
        Type = "Humanoid",
        Alignment = "neutral evil",
        ArmorClass = 15,
        ArmorDetail = "leather armor, shield",
        HitPoints = 7,
        HitDice = "2d6",
        ChallengeRating = 0.25,
        ChallengeRatingText = "1/4",
        Speed = "30 ft.",
        WalkSpeed = 30,
        AbilityScores = new Dictionary<string, int>
        {
            ["strength"] = 8, ["dexterity"] = 14, ["constitution"] = 10,
            ["intelligence"] = 10, ["wisdom"] = 8, ["charisma"] = 8
        },
        SavingThrowProficiencies = ["dexterity"],
        Skills = new Dictionary<string, int> { ["stealth"] = 6, ["sleight_of_hand"] = 4 },
        Senses = "Darkvision 60 ft.",
        Languages = "Common, Goblin",
        DamageResistances = ["Fire"],
        ConditionImmunities = ["Poisoned"],
        Environments = ["Caves"],
        Traits = [new NamedEntry { Name = "Nimble Escape", Description = "Disengage or Hide as a bonus action." }],
        Actions =
        [
            new ActionEntry
            {
                Name = "Scimitar",
                Description = "Melee attack.",
                Attacks = [new AttackEntry { Name = "Scimitar attack", ToHit = 4, DamageDieCount = 1, DamageDieType = "d6" }]
            }
        ],
        SourceKey = "srd-2014",
        SourceName = "5e 2014 Rules"
    };

    private static Creature Build(CreatureDraft draft)
    {
        draft.TryBuild(out var creature, out var errors).Should().BeTrue(string.Join("; ", errors));
        return creature!;
    }

    [Fact]
    public void FromCreature_Downloaded_BecomesANewCustomCopy()
    {
        var draft = CreatureDraft.FromCreature(Goblin());

        draft.Key.Should().StartWith("custom_").And.NotBe("srd_goblin");
        draft.Name.Should().Be("Goblin (Copy)");
    }

    [Fact]
    public void FromCreature_Custom_KeepsKeyAndName()
    {
        var original = Build(CreatureDraft.Blank());

        var draft = CreatureDraft.FromCreature(original);

        draft.Key.Should().Be(original.Key);
        draft.Name.Should().Be("New Creature");
    }

    [Fact]
    public void RoundTrip_PreservesTheStatBlock()
    {
        var creature = Build(CreatureDraft.FromCreature(Goblin()));

        creature.Name.Should().Be("Goblin (Copy)");
        creature.Size.Should().Be("Small");
        creature.ArmorClass.Should().Be(15);
        creature.ArmorDetail.Should().Be("leather armor, shield");
        creature.HitPoints.Should().Be(7);
        creature.HitDice.Should().Be("2d6");
        creature.ChallengeRating.Should().Be(0.25);
        creature.ChallengeRatingText.Should().Be("1/4");
        creature.ExperiencePoints.Should().Be(50);
        creature.ProficiencyBonus.Should().Be(2);
        creature.WalkSpeed.Should().Be(30);
        creature.AbilityScores["dexterity"].Should().Be(14);
        creature.SavingThrowProficiencies.Should().Equal("dexterity");
        creature.SavingThrows["dexterity"].Should().Be(2 + 2);
        creature.SavingThrows["strength"].Should().Be(-1);
        creature.Skills.Should().BeEquivalentTo(new Dictionary<string, int> { ["stealth"] = 6, ["sleight_of_hand"] = 4 });
        creature.DamageResistances.Should().Equal("Fire");
        creature.ConditionImmunities.Should().Equal("Poisoned");
        creature.Environments.Should().Equal("Caves");
        creature.Traits.Should().ContainSingle().Which.Name.Should().Be("Nimble Escape");
        creature.SourceKey.Should().Be(ContentSources.Custom);
        creature.SourceName.Should().Be("Custom");
    }

    [Fact]
    public void RoundTrip_KeepsStructuredAttacks()
    {
        var creature = Build(CreatureDraft.FromCreature(Goblin()));

        creature.Actions.Should().ContainSingle();
        creature.Actions[0].Attacks.Should().ContainSingle().Which.ToHit.Should().Be(4);
    }

    [Fact]
    public void Build_DerivesPassivePerceptionAndInitiative()
    {
        var draft = CreatureDraft.Blank();
        draft.Dexterity = "16";
        draft.Wisdom = "14";
        var withoutSkill = Build(draft);

        withoutSkill.InitiativeBonus.Should().Be(3);
        withoutSkill.PassivePerception.Should().Be(12);

        draft.Skills = "Perception +6";
        Build(draft).PassivePerception.Should().Be(16);
    }

    [Fact]
    public void Build_SaveProficiencyAddsProficiencyBonus()
    {
        var draft = CreatureDraft.Blank();
        draft.Wisdom = "12";
        draft.ChallengeRating = "5";
        draft.SaveProficiencies = "wis";

        var creature = Build(draft);

        creature.ProficiencyBonus.Should().Be(3);
        creature.SavingThrows["wisdom"].Should().Be(1 + 3);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1/8", 0.125)]
    [InlineData("1/4", 0.25)]
    [InlineData(" 1/2 ", 0.5)]
    [InlineData("17", 17)]
    [InlineData("30", 30)]
    public void ChallengeRating_ParsesValidValues(string text, double expected)
    {
        CreatureDraft.TryParseChallengeRating(text, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("31")]
    [InlineData("-1")]
    [InlineData("1/3")]
    [InlineData("2.5")]
    [InlineData("abc")]
    public void ChallengeRating_RejectsInvalidValues(string text)
        => CreatureDraft.TryParseChallengeRating(text, out _).Should().BeFalse();

    [Theory]
    [InlineData(0, 10)]
    [InlineData(0.125, 25)]
    [InlineData(0.25, 50)]
    [InlineData(0.5, 100)]
    [InlineData(1, 200)]
    [InlineData(5, 1800)]
    [InlineData(17, 18000)]
    [InlineData(30, 155000)]
    public void ExperienceFor_MatchesTable(double cr, int xp)
        => CreatureDraft.ExperienceFor(cr).Should().Be(xp);

    [Fact]
    public void Validation_ReportsEveryProblem()
    {
        var draft = CreatureDraft.Blank();
        draft.Name = " ";
        draft.Size = "Colossal";
        draft.ArmorClass = "abc";
        draft.HitPoints = "0";
        draft.HitDice = "not dice";
        draft.Strength = "99";
        draft.ChallengeRating = "7.5";
        draft.SaveProficiencies = "luck";
        draft.Skills = "Basketweaving +3";

        draft.TryBuild(out var creature, out var errors).Should().BeFalse();

        creature.Should().BeNull();
        errors.Should().HaveCountGreaterThanOrEqualTo(9);
        errors.Should().Contain(e => e.Contains("name"));
        errors.Should().Contain(e => e.Contains("Size"));
        errors.Should().Contain(e => e.Contains("Armor Class"));
        errors.Should().Contain(e => e.Contains("Hit Points"));
        errors.Should().Contain(e => e.Contains("Hit Dice"));
        errors.Should().Contain(e => e.Contains("Strength"));
        errors.Should().Contain(e => e.Contains("Challenge Rating"));
        errors.Should().Contain(e => e.Contains("luck"));
        errors.Should().Contain(e => e.Contains("Basketweaving"));
    }

    [Fact]
    public void Skills_AcceptMultiWordNamesInAnyCase()
    {
        var draft = CreatureDraft.Blank();
        draft.Skills = "sleight of hand +5, Animal Handling -1";

        var creature = Build(draft);

        creature.Skills.Should().BeEquivalentTo(new Dictionary<string, int> { ["sleight_of_hand"] = 5, ["animal_handling"] = -1 });
    }

    [Fact]
    public void Entries_BlankRowsAreIgnored_ButNamelessTextIsAnError()
    {
        var draft = CreatureDraft.Blank();
        draft.Traits.Add(new EntryDraft());
        draft.Traits.Add(new EntryDraft { Name = "Keen Smell", Description = "Advantage on Perception." });

        Build(draft).Traits.Should().ContainSingle().Which.Name.Should().Be("Keen Smell");

        draft.Actions.Add(new EntryDraft { Description = "Does something." });
        draft.TryBuild(out _, out var errors).Should().BeFalse();
        errors.Should().ContainSingle().Which.Should().Contain("no name");
    }

    [Fact]
    public void Actions_RejectUnknownTypes()
    {
        var draft = CreatureDraft.Blank();
        draft.Actions.Add(new EntryDraft { Name = "Bite", ActionType = "FREE_ACTION" });

        draft.TryBuild(out _, out var errors).Should().BeFalse();
        errors.Should().ContainSingle().Which.Should().Contain("unknown type");
    }

    [Fact]
    public void Lists_UseSemicolonsSoCommasStayInsideOneEntry()
    {
        var draft = CreatureDraft.Blank();
        draft.Resistances = "Cold; Bludgeoning, Piercing, and Slashing from Nonmagical Attacks";

        var creature = Build(draft);

        creature.DamageResistances.Should().Equal("Cold", "Bludgeoning, Piercing, and Slashing from Nonmagical Attacks");
    }

    [Fact]
    public void EditedCreature_StillWorksInTheRulesEngine()
    {
        var draft = CreatureDraft.Blank();
        draft.Resistances = "Bludgeoning, Piercing, and Slashing from Nonmagical Attacks";

        var combatant = CombatantFactory.FromCreature(Build(draft));

        combatant.Resistances.NonmagicalOnly.Should().BeEquivalentTo(
            [DamageType.Bludgeoning, DamageType.Piercing, DamageType.Slashing]);
    }

    [Theory]
    [InlineData("30 ft.", 30)]
    [InlineData("walk 40 ft., fly 60 ft.", 40)]
    [InlineData("fly 60 ft.", null)]
    [InlineData("", null)]
    public void WalkSpeed_IsReadFromSpeedText(string speed, int? expected)
    {
        var draft = CreatureDraft.Blank();
        draft.Speed = speed;

        Build(draft).WalkSpeed.Should().Be(expected);
    }

    [Fact]
    public void BlankDraft_IsValid()
        => CreatureDraft.Blank().TryBuild(out _, out var errors).Should().BeTrue(string.Join("; ", errors));
}

public class NpcGeneratorTests
{
    [Fact]
    public void Roles_AreAllPresent()
        => NpcRoles.Names.Should().BeEquivalentTo(["Commoner", "Guard", "Bandit", "Scout", "Noble", "Veteran"]);

    [Fact]
    public void Generate_SameSeed_GivesSameNpc()
    {
        var first = new NpcGenerator(new SystemRandomSource(7)).Generate();
        var second = new NpcGenerator(new SystemRandomSource(7)).Generate();

        second.Should().Be(first);
    }

    [Fact]
    public void Generate_HonoursRequestedRoleAndSpecies()
    {
        var profile = new NpcGenerator(new SystemRandomSource(1)).Generate("guard", "dwarf");

        profile.Role.Should().Be("Guard");
        profile.Species.Should().Be("Dwarf");
        profile.Name.Should().Contain(" ");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Random")]
    public void Generate_RandomOptions_PickRealValues(string? choice)
    {
        var profile = new NpcGenerator(new SystemRandomSource(3)).Generate(choice, choice);

        NpcRoles.Find(profile.Role).Should().NotBeNull();
        SpeciesCatalog.Find(profile.Species).Should().NotBeNull();
    }

    [Fact]
    public void EveryRoleAndSpecies_ProducesAValidCreature()
    {
        var generator = new NpcGenerator(new SystemRandomSource(11));

        foreach (var role in NpcRoles.Names)
        foreach (var species in SpeciesCatalog.Names)
        {
            var draft = generator.CreateDraft(generator.Generate(role, species));

            draft.TryBuild(out var creature, out var errors).Should().BeTrue($"{species} {role}: {string.Join("; ", errors)}");
            creature!.Actions.Should().ContainSingle();
            creature.Traits.Should().ContainSingle().Which.Name.Should().Be("Roleplay Notes");
        }
    }

    [Fact]
    public void Draft_UsesSpeciesSizeSpeedAndDarkvision()
    {
        var generator = new NpcGenerator(new SystemRandomSource(5));

        var halfling = generator.CreateDraft(generator.Generate("Commoner", "Halfling"));
        var goliath = generator.CreateDraft(generator.Generate("Commoner", "Goliath"));
        var dwarf = generator.CreateDraft(generator.Generate("Commoner", "Dwarf"));

        halfling.Size.Should().Be("Small");
        goliath.Speed.Should().Be("35 ft.");
        dwarf.Senses.Should().Be("Darkvision 120 ft.");
        halfling.Senses.Should().BeEmpty();
    }

    [Fact]
    public void ScoutDraft_HasRangedAttackWithStructuredData()
    {
        var generator = new NpcGenerator(new SystemRandomSource(2));

        generator.CreateDraft(generator.Generate("Scout", "Human")).TryBuild(out var scout, out _).Should().BeTrue();

        var action = scout!.Actions.Single();
        action.Name.Should().Be("Shortbow");
        action.Description.Should().Contain("Ranged").And.Contain("80/320");
        action.Description.Should().Contain("5 (1d6 + 2) Piercing");
        action.Attacks.Single().Range.Should().Be(80);
        action.Attacks.Single().ToHit.Should().Be(4);
    }

    [Fact]
    public void RoleTemplates_HaveSixScoresAndSensibleDifficulty()
    {
        NpcRoles.All.Should().OnlyContain(r => r.Scores.Length == 6);
        NpcRoles.Find("Veteran")!.HitPoints.Should().BeGreaterThan(NpcRoles.Find("Commoner")!.HitPoints);
    }
}