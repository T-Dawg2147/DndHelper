namespace DndHelper.Core.Rules;

public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource() => _random = new Random();

    public SystemRandomSource(int seed) => _random = new Random();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}