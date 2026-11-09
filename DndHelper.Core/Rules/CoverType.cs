namespace DndHelper.Core.Rules;

public enum CoverType
{
    None,
    Half,
    ThreeQuarters,
    Total
}

public static class CoverExtensions
{
    public static int ArmorClassBonus(this CoverType cover) => cover switch
    {
        CoverType.Half => 2,
        CoverType.ThreeQuarters => 5,
        _ => 0
    };

    public static int DexteritySaveBonus(this CoverType cover) => cover.ArmorClassBonus();
    public static bool CanBeTargeted(this CoverType cover) => cover != CoverType.Total;
}