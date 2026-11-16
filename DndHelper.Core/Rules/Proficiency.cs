namespace DndHelper.Core.Rules;

public static class Proficiency
{
    public static int ForLevel(int level)
        => 2 + (Math.Clamp(level, 1, 20) - 1) / 4;

    /// <summary>Proficiency bonus by challenge rating: CR 0-4 is +2, 5-8 is +3, and so on up to +9 at CR 29-30.</summary>
    public static int ForChallengeRating(double challengeRating)
        => challengeRating < 1 ? 2 : 2 + ((int)Math.Ceiling(challengeRating) - 1) / 4;
}
