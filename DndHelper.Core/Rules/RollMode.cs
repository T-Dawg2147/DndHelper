namespace DndHelper.Core.Rules;

public enum RollMode
{
    Normal,
    Advantage,
    Disadvantage
}

public static class RollModeExtensions
{
    /// <summary>Any number of advantage sources and any number of disadvantage sources cancel out completely.</summary>
    public static RollMode Combine(bool hasAdvantage, bool hasDisadvantage)
        => hasAdvantage == hasDisadvantage
            ? RollMode.Normal
            : hasAdvantage ? RollMode.Advantage : RollMode.Disadvantage;
}
