namespace DndHelper.Core.Rules;

/// <summary>Source of random integers. Swap in a seeded or scripted implementation for tests and reproducible maps.</summary>
public interface IRandomSource
{
    /// <summary>Returns a random integer in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);
}
