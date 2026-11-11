using DndHelper.Core.Characters;
using DndHelper.Core.Models;
using DndHelper.Core.Rules;

namespace DndHelper.Core.Editing;

public sealed record NpcProfile(string Name, string Species, string Role, string Personality, string Quirk, string Motivation);

/// <summary>A starting stat block for a common NPC role. These are approximations to tweak, not official stat blocks.</summary>
public sealed record NpcRoleTemplate(
    string Role,
    int ArmorClass,
    string ArmorDetail,
    int HitPoints,
    string HitDice,
    string ChallengeRating,
    int[] Scores,
    string Skills,
    string AttackName,
    int ToHit,
    int DieCount,
    int DieSides,
    int DamageBonus,
    DamageType DamageType,
    int? Range = null,
    int? LongRange = null);

public static class NpcRoles
{
    public static readonly IReadOnlyList<NpcRoleTemplate> All =
    [
        new("Commoner", 10, "", 4, "1d8", "0", [10, 10, 10, 10, 10, 10], "",
            "Club", 2, 1, 4, 0, DamageType.Bludgeoning),

        new("Guard", 16, "chain shirt, shield", 11, "2d8+2", "1/8", [13, 12, 12, 10, 11, 10], "Perception +2",
            "Spear", 3, 1, 6, 1, DamageType.Piercing),

        new("Bandit", 12, "leather armor", 11, "2d8+2", "1/8", [11, 12, 12, 10, 10, 10], "",
            "Scimitar", 3, 1, 6, 1, DamageType.Slashing),

        new("Scout", 13, "leather armor", 16, "3d8+3", "1/2", [11, 14, 12, 11, 13, 11], "Perception +5, Stealth +6",
            "Shortbow", 4, 1, 6, 2, DamageType.Piercing, 80, 320),

        new("Noble", 15, "breastplate", 9, "2d8", "1/8", [11, 12, 11, 12, 14, 16], "Deception +5, Persuasion +5",
            "Rapier", 3, 1, 8, 1, DamageType.Piercing),

        new("Veteran", 17, "splint armor", 58, "9d8+18", "3", [16, 13, 14, 10, 11, 10], "Athletics +5, Perception +2",
            "Longsword", 5, 1, 8, 3, DamageType.Slashing)
    ];

    public static IReadOnlyList<string> Names => All.Select(r => r.Role).ToList();

    public static NpcRoleTemplate? Find(string? role)
        => All.FirstOrDefault(r => string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Creates random NPC names, personalities and a matching starting stat block.</summary>
public sealed class NpcGenerator(IRandomSource random)
{
    private static readonly string[] FirstNames =
    [
        "Alder", "Brenna", "Corwin", "Delphine", "Edric", "Fenna", "Garrick", "Hesper", "Isolde", "Joren", "Kestrel", "Lira",
        "Marek", "Nessa", "Orrin", "Perrin", "Quill", "Rowena", "Soren", "Tamsin", "Ulric", "Vesna", "Wren", "Yara"
    ];

    private static readonly string[] Surnames =
    [
        "Ashdown", "Blackwood", "Carrow", "Dunmere", "Eastwick", "Fairweather", "Greywater", "Hollis",
        "Ironside", "Juniper", "Kettleby", "Larkspur", "Marsh", "Northcott", "Oakheart", "Pennywhistle"
    ];

    private static readonly string[] Personalities =
    [
        "Cheerful and talkative", "Suspicious of strangers", "Calm and patient", "Boastful", "Nervous and jumpy",
        "Dry-witted", "Fiercely loyal", "Greedy", "Gentle and kind", "Short-tempered", "Curious about everything", "Quietly observant"
    ];

    private static readonly string[] Quirks =
    [
        "Hums while working", "Never makes eye contact", "Collects odd trinkets", "Speaks in proverbs", "Always eating something",
        "Taps a nervous rhythm", "Swears by an obscure saint", "Remembers everyone's name but never faces", "Constantly polishing a coin",
        "Whispers important words", "Laughs at the wrong moments", "Keeps a tiny journal"
    ];

    private static readonly string[] Motivations =
    [
        "Wants to pay off a family debt", "Seeks revenge on an old rival", "Dreams of leaving town for good",
        "Protects a dangerous secret", "Hopes to earn a noble's favour", "Wants to find a missing sibling",
        "Is saving for a business of their own", "Believes a prophecy is about them", "Needs a rare cure for a loved one",
        "Wants to be remembered", "Owes a favour to the wrong people", "Just wants a quiet life"
    ];

    /// <summary>Pass null, empty or "Random" for a random role or species.</summary>
    public NpcProfile Generate(string? role = null, string? species = null)
    {
        var chosenRole = NpcRoles.Find(role)?.Role ?? Pick(NpcRoles.Names);
        var chosenSpecies = SpeciesCatalog.Find(species)?.Name ?? Pick(SpeciesCatalog.Names);

        return new NpcProfile(
            $"{Pick(FirstNames)} {Pick(Surnames)}",
            chosenSpecies,
            chosenRole,
            Pick(Personalities),
            Pick(Quirks),
            Pick(Motivations));
    }

    /// <summary>Builds an editable stat block for the profile, using its role template and species size.</summary>
    public CreatureDraft CreateDraft(NpcProfile profile)
    {
        var template = NpcRoles.Find(profile.Role) ?? NpcRoles.All[0];
        var species = SpeciesCatalog.Find(profile.Species);

        var draft = new CreatureDraft
        {
            Key = CreatureDraft.NewKey(),
            Name = profile.Name,
            Size = species?.Size ?? "Medium",
            Type = "Humanoid",
            Alignment = "any alignment",
            ArmorClass = template.ArmorClass.ToString(),
            ArmorDetail = template.ArmorDetail,
            HitPoints = template.HitPoints.ToString(),
            HitDice = template.HitDice,
            ChallengeRating = template.ChallengeRating,
            Speed = $"{species?.Speed ?? 30} ft.",
            Strength = template.Scores[0].ToString(),
            Dexterity = template.Scores[1].ToString(),
            Constitution = template.Scores[2].ToString(),
            Intelligence = template.Scores[3].ToString(),
            Wisdom = template.Scores[4].ToString(),
            Charisma = template.Scores[5].ToString(),
            Skills = template.Skills,
            Senses = species is { DarkvisionFeet: > 0 } ? $"Darkvision {species.DarkvisionFeet} ft." : "",
            Languages = "Common"
        };

        draft.Traits.Add(new EntryDraft
        {
            Name = "Roleplay Notes",
            Description = $"{profile.Species} {profile.Role}. {profile.Personality}. {profile.Quirk}. {profile.Motivation}."
        });

        draft.Actions.Add(BuildAttack(template));
        return draft;
    }

    private static EntryDraft BuildAttack(NpcRoleTemplate t)
    {
        var average = (int)Math.Floor(t.DieCount * (t.DieSides + 1) / 2.0 + t.DamageBonus);
        var dice = t.DamageBonus == 0 ? $"{t.DieCount}d{t.DieSides}" : $"{t.DieCount}d{t.DieSides} + {t.DamageBonus}";
        var reach = t.Range is { } range ? $"range {range}/{t.LongRange} ft." : "reach 5 ft.";
        var kind = t.Range is null ? "Melee" : "Ranged";

        return new EntryDraft
        {
            Name = t.AttackName,
            ActionType = "ACTION",
            Description = $"{kind} Attack Roll: +{t.ToHit}, {reach} Hit: {average} ({dice}) {t.DamageType} damage.",
            Attacks =
            [
                new AttackEntry
                {
                    Name = t.AttackName + " attack",
                    ToHit = t.ToHit,
                    DamageDieCount = t.DieCount,
                    DamageDieType = "d" + t.DieSides,
                    DamageBonus = t.DamageBonus == 0 ? null : t.DamageBonus,
                    DamageType = t.DamageType.ToString(),
                    Reach = t.Range is null ? 5 : null,
                    Range = t.Range,
                    LongRange = t.LongRange
                }
            ]
        };
    }

    private string Pick(IReadOnlyList<string> items) => items[random.Next(0, items.Count)];
}