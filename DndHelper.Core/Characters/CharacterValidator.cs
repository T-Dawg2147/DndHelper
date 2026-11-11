using DndHelper.Core.Rules;

namespace DndHelper.Core.Characters;

public static class CharacterValidator
{
    /// <summary>Returns every problem that stops this character being saved. An empty list means it is valid.</summary>
    public static IReadOnlyList<string> Validate(PlayerCharacter character)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(character.Name))
            errors.Add("Enter a name.");

        var cls = ClassCatalog.Find(character.ClassName);
        var background = BackgroundCatalog.Find(character.Background);

        if (SpeciesCatalog.Find(character.Species) is null) errors.Add("Choose a species.");
        if (cls is null) errors.Add("Choose a class.");
        if (background is null) errors.Add("Choose a background.");

        if (character.Level is < 1 or > 20)
            errors.Add("Level must be between 1 and 20.");

        ValidateBaseScores(character, errors);

        if (background is not null)
        {
            ValidateBackgroundBonuses(character, background, errors);
            if (cls is not null) ValidateSkills(character, cls, background, errors);
        }

        return errors;
    }

    private static void ValidateBaseScores(PlayerCharacter character, List<string> errors)
    {
        var scores = AbilityExtensions.All.Select(a => character.BaseScores.GetValueOrDefault(a, 10)).ToList();

        switch (character.Method)
        {
            case ScoreMethod.StandardArray when !AbilityScoreGenerator.IsStandardArray(scores):
                errors.Add("Standard array scores must be exactly 15, 14, 13, 12, 10 and 8, one per ability.");
                break;

            case ScoreMethod.PointBuy:
                if (!AbilityScoreGenerator.IsValidPointBuy(scores, out var spent))
                {
                    errors.Add(scores.Any(s => AbilityScoreGenerator.PointBuyCost(s) < 0)
                        ? "Point buy scores must be between 8 and 15."
                        : $"Point buy spends {spent} points; the limit is {AbilityScoreGenerator.PointBuyBudget}.");
                }
                break;

            case ScoreMethod.Roll or ScoreMethod.Manual when scores.Any(s => s is < 3 or > 18):
                errors.Add("Base scores must be between 3 and 18.");
                break;
        }
    }

    private static void ValidateBackgroundBonuses(PlayerCharacter character, BackgroundDefinition background, List<string> errors)
    {
        var bonuses = character.BackgroundBonuses;
        var values = bonuses.Values.OrderDescending().ToList();
        var isSplit = values.SequenceEqual([2, 1]);
        var isSpread = values.SequenceEqual([1, 1, 1]);

        if (!isSplit && !isSpread)
        {
            errors.Add("Background ability increases must be +2 and +1 on two abilities, or +1 on all three.");
            return;
        }

        if (bonuses.Keys.Any(a => !background.AbilityOptions.Contains(a)))
            errors.Add($"The {background.Name} background can only raise {string.Join(", ", background.AbilityOptions)}.");
    }

    private static void ValidateSkills(
        PlayerCharacter character, ClassDefinition cls, BackgroundDefinition background, List<string> errors)
    {
        var chosen = character.ClassSkills;

        if (chosen.Distinct().Count() != chosen.Count)
            errors.Add("Each skill can only be chosen once.");

        if (chosen.Count != cls.SkillChoiceCount)
            errors.Add($"Choose exactly {cls.SkillChoiceCount} class skills (you chose {chosen.Count}).");

        if (chosen.Any(s => !cls.SkillOptions.Contains(s)))
            errors.Add($"Some chosen skills are not on the {cls.Name} skill list.");

        if (chosen.Any(background.Skills.Contains))
            errors.Add($"The {background.Name} background already gives {string.Join(" and ", background.Skills.Select(s => s.DisplayName()))}; choose different class skills.");
    }
}