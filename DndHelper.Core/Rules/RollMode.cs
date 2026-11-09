namespace DndHelper.Core.Rules;

public enum RollMode
{
    Normal,
    Advantage,
    Disadvantage
}

public static class RollModeExtensions
{
    public static RollMode Combine(bool hasAdvantage, bool hasDisadvantage)
        => hasAdvantage == hasDisadvantage
            ? RollMode.Normal
            : hasAdvantage ? RollMode.Advantage : RollMode.Disadvantage;
}