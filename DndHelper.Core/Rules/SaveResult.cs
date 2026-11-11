namespace DndHelper.Core.Rules;

public sealed record SaveResult(
    Ability Ability,
    int Dc, 
    bool Success, bool AutoFailed,
    D20Result? Roll,
    RollMode Mode,
    IReadOnlyList<string> Reasons)
{
    public override string ToString()
        => AutoFailed
            ? $"{Ability.Abbreviation()} save vs DC {Dc}: automatic failure ({string.Join("; ", Reasons)}"
            : $"{Ability.Abbreviation()} save VS DC {Dc}: {Roll} - {(Success ? "success" : "failure")}";
}