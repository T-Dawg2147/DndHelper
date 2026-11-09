using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace DndHelper.Core.Rules;

/// <summary>One group of identical dice, e.g. 8d6. Sign is -1 when the group is subtracted.</summary>
public sealed record DiceTerm(int Count, int Sides, int Sign = 1);

public sealed partial class DiceExpression
{
    public const int MaxDiceCount = 100;
    public const int MaxSides = 1000;

    public IReadOnlyList<DiceTerm> Terms { get; }
    public int Modifier { get; }

    public DiceExpression(IEnumerable<DiceTerm> terms, int modifier = 0)
    {
        Terms = terms.ToList();
        Modifier = modifier;

        foreach (var term in Terms)
        {
            if (term.Count < 1 || term.Sides < 2)
                throw new ArgumentException($"Invalid dice term {term.Count}d{term.Sides}.", nameof(terms));
        }
    }

    public int Min => Terms.Sum(t => t.Sign > 0 ? t.Count : -t.Count * t.Sides) + Modifier;

    public int Max => Terms.Sum(t => t.Sign > 0 ? t.Count * t.Sides : -t.Count) + Modifier;

    public double Average => Terms.Sum(t => t.Sign * t.Count * (t.Sides + 1) / 2.0) + Modifier;

    public DiceExpression WithCriticalDice()
        => new(Terms.Select(t => t with { Count = t.Count * 2 }), Modifier);

    public static DiceExpression Parse(string notation)
        => TryParse(notation, out var result)
            ? result
            : throw new FormatException($"Invalid dice notation: '{notation}'.");

    public static bool TryParse(string? notation, [NotNullWhen(true)] out DiceExpression? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(notation)) return false;

        var text = OperatorSpacePattern().Replace(notation.Trim(), "$1");
        if (text.Any(char.IsWhiteSpace) || !ValidPattern().IsMatch(text)) return false;

        var terms = new List<DiceTerm>();
        var modifier = 0;

        foreach (Match match in TokenPattern().Matches(text))
        {
            var sign = match.Groups[1].Value == "-" ? -1 : 1;

            if (match.Groups[3].Success)
            {
                var count = 1;
                if (match.Groups[2].Length > 0 && !int.TryParse(match.Groups[2].Value, out count)) return false;
                if (!int.TryParse(match.Groups[3].Value, out var sides)) return false;
                if (count is < 1 or > MaxDiceCount || sides is < 2 or > MaxSides) return false;

                terms.Add(new DiceTerm(count, sides, sign));
            }
            else
            {
                if (!int.TryParse(match.Groups[4].Value, out var constant)) return false;
                modifier += sign * constant;
            }
        }

        result = new DiceExpression(terms, modifier);
        return true;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        foreach (var term in Terms)
        {
            if (builder.Length > 0) builder.Append(term.Sign > 0 ? '+' : '-');
            else if (term.Sign < 0) builder.Append('-');
            builder.Append(term.Count).Append('d').Append(term.Sides);
        }

        if (Modifier != 0 || builder.Length == 0)
        {
            if (builder.Length > 0 && Modifier >= 0) builder.Append('+');
            builder.Append(Modifier);
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\s*([+-])\s*")]
    private static partial Regex OperatorSpacePattern();

    [GeneratedRegex(@"^(?:[+-]?(?:\d*d\d+|\d+))+$", RegexOptions.IgnoreCase)]
    private static partial Regex ValidPattern();

    [GeneratedRegex(@"([+-]?)(?:(\d*)d(\d+)|(\d+))", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();
}