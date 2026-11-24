using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AIKernel.Dtos.Context;

namespace AIKernel.Providers.DocumentSearch;

internal sealed record SearchHit(string Path, int Line, int Start, string Text, double Score, string DocumentHash)
{
    internal static IComparer<SearchHit> Ranking { get; } = Comparer<SearchHit>.Create((a, b) =>
    {
        int byScore = b.Score.CompareTo(a.Score);
        if (byScore != 0)
            return byScore;

        int byPath = StringComparer.Ordinal.Compare(a.Path, b.Path);
        return byPath != 0 ? byPath : a.Line.CompareTo(b.Line);
    });

    internal MaterialContextDto ToMaterial(DateTime retrievedAt)
    {
        string escapedPath = string.Join('/', Path.Split('/').Select(Uri.EscapeDataString));
        return new MaterialContextDto
        {
            Source = escapedPath + "#L" + Line.ToString(CultureInfo.InvariantCulture),
            RawData = Text,
            RelevanceScore = Score,
            RetrievedAt = retrievedAt,
            Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["relative_path"] = Path,
                ["line_start"] = Line,
                ["line_end"] = Line,
                ["column_start"] = Start + 1,
                ["column_end"] = Start + Text.Length,
                ["document_sha256"] = DocumentHash,
                ["excerpt_sha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text))),
                ["retrieval_kind"] = "keyword"
            }
        };
    }
}
