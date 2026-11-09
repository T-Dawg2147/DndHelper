namespace DndHelper.Core.Rules;

public interface IRandomSource
{
    int Next(int minInclusive, int maxExclusive);
}