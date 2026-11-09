namespace DndHelper.Core.Rules;

public static class Proficiency
{
    public static int ForLevel(int level)
        => 2 + (Math.Clamp(level, 1, 20) - 1) / 4;

    public static int ForChallengeRating(double challengeRating)
        => challengeRating < 1 ? 2 : 2 + ((int)Math.Ceiling(challengeRating) - 1) / 4;
}