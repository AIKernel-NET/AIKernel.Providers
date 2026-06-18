namespace AIKernel.Providers.DependencyGuard.Tests;

using System.Xml.Linq;

public sealed class DependencyBoundaryTests
{
    private static readonly string[] GenericForbiddenReferences =
    [
        "NAudio",
        "SDL",
        "Microsoft.JSInterop",
        "AIKernel.Wasm",
        "AIKernel.Tools",
        "AIKernel.Cuda13.0"
    ];

    [Fact]
    public void GenericSubstrateProjects_DoNotReferenceForbiddenPackages()
    {
        var root = FindRepositoryRoot();
        string[] projects =
        [
            "src/ProviderSubstrate/AIKernel.Providers.Substrate/AIKernel.Providers.Substrate.csproj",
            "src/Audio/AIKernel.Providers.Audio/AIKernel.Providers.Audio.csproj",
            "src/Compute/AIKernel.Providers.Compute/AIKernel.Providers.Compute.csproj",
            "src/Council/AIKernel.Providers.Council/AIKernel.Providers.Council.csproj"
        ];

        foreach (var project in projects)
        {
            var references = ReadReferences(Path.Combine(root.FullName, project));

            foreach (var forbidden in GenericForbiddenReferences)
            {
                Assert.DoesNotContain(references, reference =>
                    reference.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void CudaComputeProvider_DoesNotReferenceDedicatedCudaImplementation()
    {
        var root = FindRepositoryRoot();
        var references = ReadReferences(Path.Combine(
            root.FullName,
            "src/Compute/CudaComputeProvider/CudaComputeProvider.csproj"));

        Assert.DoesNotContain(references, reference =>
            reference.Contains("AIKernel.Cuda13.0", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, reference =>
            reference.Contains("libtorch", StringComparison.OrdinalIgnoreCase));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AIKernel.Providers.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate AIKernel.Providers repository root.");
    }

    private static IReadOnlyList<string> ReadReferences(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return document
            .Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
