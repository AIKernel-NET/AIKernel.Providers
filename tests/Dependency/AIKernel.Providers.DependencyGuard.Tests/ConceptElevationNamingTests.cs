namespace AIKernel.Providers.DependencyGuard.Tests;

using System.Text.RegularExpressions;

/// <summary>
/// EN: Guards provider substrate names so concept vocabulary does not leak into low-level provider types.
/// JA: 概念語彙が低レイヤ provider 型へ漏れないよう provider substrate 名を保護します。
/// </summary>
public sealed class ConceptElevationNamingTests
{
    private static readonly string[] PhilosophicalPrefixes =
    [
        "Ethos",
        "Pathos",
        "Logos",
        "Nomos",
        "Dike",
        "Kratos",
        "Aisthesis",
        "Phantasia",
        "Chronos",
        "Kairos",
        "Dynamis",
        "Energeia",
        "Nous",
        "Telos",
        "Apatheia",
        "Ataraxia",
        "Eidos",
    ];

    private static readonly string[] ForbiddenTechnicalSuffixes =
    [
        "Dto",
        "Request",
        "Result",
        "Mapper",
        "Adapter",
        "Serializer",
        "Converter",
        "HttpClient",
        "JSInterop",
        "JsInterop",
        "NativeBridge",
        "Provider",
    ];

    private static readonly ISet<string> CompatibilityExceptions = new HashSet<string>(StringComparer.Ordinal)
    {
        "EthosSemanticEvaluationProvider",
        "PathosSemanticEvaluationProvider",
        "LogosSemanticEvaluationProvider",
    };

    private static readonly Regex TypeDeclarationPattern = new(
        @"\b(?:public|internal|private|protected)?\s*(?:sealed\s+|abstract\s+|static\s+|partial\s+)*\b(?:class|record|interface|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    /// <summary>
    /// EN: Confirms the council concept surface exists next to the compatibility provider names.
    /// JA: 互換 provider 名の横に council concept surface が存在することを確認します。
    /// </summary>
    [Fact]
    public void ConceptSurface_WhenCouncilProvidersRemainForCompatibility_IsPresent()
    {
        var repositoryRoot = FindRepositoryRoot("AIKernel.Providers.slnx");
        var conceptSurface = Path.Combine(
            repositoryRoot,
            "src",
            "Council",
            "AIKernel.Providers.Council",
            "Concepts",
            "CouncilConceptSurfaces.cs");
        var source = File.ReadAllText(conceptSurface);

        Assert.Contains("EthosCouncil", source, StringComparison.Ordinal);
        Assert.Contains("PathosSignal", source, StringComparison.Ordinal);
        Assert.Contains("LogosVerifier", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// EN: Rejects any new philosophical prefix on low-level provider substrate names.
    /// JA: 低レイヤ provider substrate 名への新規の哲学語 prefix を拒否します。
    /// </summary>
    [Fact]
    public void SourceTypes_WhenUsingPhilosophicalPrefix_DoNotUseForbiddenTechnicalSuffix()
    {
        var violations = FindViolations("AIKernel.Providers.slnx");

        Assert.Empty(violations);
    }

    private static IReadOnlyList<string> FindViolations(string solutionFileName)
    {
        var repositoryRoot = FindRepositoryRoot(solutionFileName);
        var sourceRoot = Path.Combine(repositoryRoot, "src");

        return Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => FindViolationsInFile(repositoryRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> FindViolationsInFile(string repositoryRoot, string path)
    {
        var source = File.ReadAllText(path);
        foreach (Match match in TypeDeclarationPattern.Matches(source))
        {
            var typeName = match.Groups["name"].Value;
            if (CompatibilityExceptions.Contains(typeName))
            {
                continue;
            }

            var hasPhilosophicalPrefix = PhilosophicalPrefixes.Any(prefix => typeName.StartsWith(prefix, StringComparison.Ordinal));
            var hasForbiddenTechnicalSuffix = ForbiddenTechnicalSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));
            var isConceptSurface = path.Contains($"{Path.DirectorySeparatorChar}Concepts{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

            if (hasPhilosophicalPrefix && (hasForbiddenTechnicalSuffix || !isConceptSurface))
            {
                yield return $"{Path.GetRelativePath(repositoryRoot, path)}: {typeName}";
            }
        }
    }

    private static string FindRepositoryRoot(string solutionFileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, solutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate {solutionFileName}.");
    }
}
