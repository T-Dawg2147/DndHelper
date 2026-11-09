namespace DndHelper.Data.Entities;

public class CreatureEntity
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public double ChallengeRating { get; set; }
    public string ChallengeRatingText { get; set; } = string.Empty;
    public string SourceKey { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
}