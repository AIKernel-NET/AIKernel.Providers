using System.Globalization;
using System.Text;

namespace AIKernel.Providers.DocumentSearch;

internal sealed class KeywordMatcher
{
    private const CompareOptions Comparison = CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth;
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;
    private readonly Keyword[] _keywords;
    private readonly MatchLocation[] _matches;
    private readonly int _excerptLimit;

    public KeywordMatcher(string query, int excerptLimit)
    {
        _keywords = ReadKeywords(query).ToArray();
        if (_keywords.Length == 0)
            throw new ArgumentException("The query must contain a letter or digit.", nameof(query));

        _matches = new MatchLocation[_keywords.Length];
        _excerptLimit = excerptLimit;
    }

    public Excerpt? Match(string line, CancellationToken cancellationToken)
    {
        MatchLocation first = default;
        for (int i = 0; i < _keywords.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = Find(line, _keywords[i], cancellationToken);
            _matches[i] = match;
            if (match.Length > 0 && (first.Length == 0 || match.Start < first.Start))
                first = match;
        }

        if (first.Length == 0)
            return null;

        int capacity = Math.Min(line.Length, _excerptLimit);
        int start = Math.Clamp(first.Start - capacity / 4,
            Math.Max(0, first.End - capacity), Math.Min(first.Start, line.Length - capacity));
        if (start > 0 && char.IsLowSurrogate(line[start]))
            start += start - 1 + capacity >= first.End ? -1 : 1;
        int end = start + Math.Min(capacity, line.Length - start);
        if (end < line.Length && char.IsLowSurrogate(line[end]))
            end--;

        int covered = 0;
        foreach (var match in _matches)
            if (match.Length > 0 && match.Start >= start && match.End <= end)
                covered++;

        return new Excerpt(start, end - start, (double)covered / _keywords.Length);
    }

    private MatchLocation Find(string text, Keyword keyword, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = Compare.IndexOf(text.AsSpan(offset), keyword.Text, Comparison, out int length);
            if (index < 0)
                break;

            int start = offset + index;
            int end = start + length;
            if (length > 0 && length <= _excerptLimit && (!keyword.WholeWord
                || ((start == 0 || !IsAsciiWordCharacter(text[start - 1]))
                    && (end == text.Length || !IsAsciiWordCharacter(text[end])))))
                return new MatchLocation(start, length);
            offset = start + 1;
        }

        return default;
    }

    private static IEnumerable<Keyword> ReadKeywords(string query)
    {
        var seen = new HashSet<string>(Compare.GetStringComparer(Comparison));
        int start = -1;
        int offset = 0;
        foreach (Rune rune in query.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
                start = start < 0 ? offset : start;
            else if (start >= 0 && Rune.GetUnicodeCategory(rune) is not
                (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
            {
                string term = query[start..offset];
                if (seen.Add(term))
                    yield return new Keyword(term, term.All(IsAsciiWordCharacter));
                start = -1;
            }
            offset += rune.Utf16SequenceLength;
        }

        if (start >= 0)
        {
            string term = query[start..];
            if (seen.Add(term))
                yield return new Keyword(term, term.All(IsAsciiWordCharacter));
        }
    }

    private static bool IsAsciiWordCharacter(char character)
        => char.IsAsciiLetterOrDigit(character)
            || character is >= '０' and <= '９' or >= 'Ａ' and <= 'Ｚ' or >= 'ａ' and <= 'ｚ';

    private readonly record struct Keyword(string Text, bool WholeWord);

    private readonly record struct MatchLocation(int Start, int Length)
    {
        public int End => Start + Length;
    }

    internal readonly record struct Excerpt(int Start, int Length, double Score);
}
