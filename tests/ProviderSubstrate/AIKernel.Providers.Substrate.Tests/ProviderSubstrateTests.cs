namespace AIKernel.Providers.Substrate.Tests;

using AIKernel.Providers.Substrate;

public sealed class ProviderSubstrateTests
{
    [Fact]
    public void ProviderManifestLoader_ExistingManifest_LoadsDescriptor()
    {
        const string json = """
            {
              "providerId": "openai.chat",
              "name": "Chat OpenAI",
              "version": "0.1.1",
              "manifestVersion": "1.0",
              "schemaVersion": "1.0",
              "assembly": "ChatOpenAIProvider.dll",
              "packageId": "AIKernel.Providers.ChatOpenAI",
              "capabilities": [ "chat.completion", "embedding" ],
              "dependencies": [
                { "id": "AIKernel.NET", "kind": "package", "version": "0.1.1.1", "optional": false }
              ],
              "compatibility": {
                "minimumAIKernelVersion": "0.1.1",
                "targetFrameworks": [ "net10.0" ]
              },
              "metadata": {
                "providerType": "AIKernel.Providers.ChatOpenAI.ChatOpenAIProvider",
                "invokerType": "AIKernel.Providers.ChatOpenAI.ChatOpenAIInvoker"
              },
              "backendMetadata": {
                "endpoint": "https://example.test"
              },
              "backendDescriptors": [
                {
                  "backend": "local",
                  "kind": "local",
                  "rank": 1,
                  "capabilities": [ "chat.completion" ],
                  "metadata": { "device": "cpu" }
                }
              ],
              "vendorMetadata": {
                "providerType": "Vendor.Provider"
              },
              "cli": { "command": "openai", "customHint": true },
              "futureBlock": { "enabled": true }
            }
            """;

        var result = new ProviderManifestLoader().LoadJson(json, "openai.provider.json");

        Assert.True(result.Succeeded);
        Assert.Equal("openai.chat", result.Descriptor?.ProviderId);
        Assert.Equal("ChatOpenAIProvider.dll", result.Descriptor?.AssemblyName);
        Assert.Equal(["chat.completion", "embedding"], result.Descriptor?.Capabilities);
        Assert.Equal("AIKernel.Providers.ChatOpenAI.ChatOpenAIProvider", result.Descriptor?.ProviderType);
        Assert.Equal("1.0", result.Descriptor?.SchemaVersion);
        Assert.Equal("1.0", result.Descriptor?.ManifestVersion);
        Assert.Equal("AIKernel.Providers.ChatOpenAI", result.Descriptor?.PackageId);
        Assert.Equal("AIKernel.NET", result.Descriptor?.Dependencies.Single().Id);
        Assert.False(result.Descriptor?.Dependencies.Single().Optional);
        Assert.Equal("0.1.1", result.Descriptor?.Compatibility.MinimumAIKernelVersion);
        Assert.Equal(["net10.0"], result.Descriptor?.Compatibility.TargetFrameworks);
        Assert.Equal("https://example.test", result.Descriptor?.BackendMetadata["endpoint"]);
        Assert.Equal("local", result.Descriptor?.BackendDescriptors.Single().BackendName);
        Assert.Equal(1, result.Descriptor?.BackendDescriptors.Single().Rank);
        Assert.Equal("openai", result.Descriptor?.CliHints.Command);
        Assert.True(result.Descriptor?.CliHints.ExtensionJson.ContainsKey("customHint"));
        Assert.True(result.Descriptor?.ExtensionJson.ContainsKey("futureBlock"));
    }

    [Fact]
    public async Task ProviderManifestLoader_InterfaceRequest_LoadsJson()
    {
        IProviderManifestLoader loader = new ProviderManifestLoader();

        var result = await loader.LoadAsync(new ProviderManifestLoadRequest
        {
            Json = """
                {
                  "providerId": "sample.provider",
                  "name": "Sample",
                  "version": "0.1.1",
                  "assembly": "Sample.dll"
                }
                """
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("sample.provider", result.Descriptor?.ProviderId);
    }

    [Fact]
    public async Task ProviderManifestValidator_InterfaceRequest_ValidatesDescriptor()
    {
        IProviderManifestValidator validator = new ProviderManifestValidator();

        var result = await validator.ValidateAsync(new ProviderManifestDescriptor
        {
            ProviderId = "sample.provider",
            Name = "Sample",
            Version = "0.1.1",
            AssemblyName = "Sample.dll"
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ProviderManifestValidator_MissingCapability_IsAllowedByDefault()
    {
        var descriptor = new ProviderManifestDescriptor
        {
            ProviderId = "sample",
            Name = "Sample",
            Version = "0.1.1",
            AssemblyName = "Sample.dll"
        };

        var result = new ProviderManifestValidator().Validate(descriptor);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ProviderManifestValidator_RequireCapabilities_FailsClosed()
    {
        var descriptor = new ProviderManifestDescriptor
        {
            ProviderId = "sample",
            Name = "Sample",
            Version = "0.1.1",
            AssemblyName = "Sample.dll"
        };

        var result = new ProviderManifestValidator().Validate(
            descriptor,
            new ProviderManifestValidationOptions { RequireCapabilities = true });

        Assert.False(result.Succeeded);
        Assert.Equal("PROVIDER_MANIFEST_INVALID", result.ErrorCode);
        Assert.Contains(result.Errors, error => error.Code == "PROVIDER_MANIFEST_CAPABILITIES_MISSING" && error.Field == "capabilities");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PROVIDER_MANIFEST_CAPABILITIES_MISSING");
    }

    [Fact]
    public void ProviderManifestValidator_MissingRequiredField_EmitsValidationErrorDto()
    {
        var descriptor = new ProviderManifestDescriptor
        {
            ProviderId = "sample",
            Name = "Sample",
            Version = "0.1.1",
            Source = "sample.provider.json"
        };

        var result = new ProviderManifestValidator().Validate(descriptor);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal("PROVIDER_MANIFEST_REQUIRED_FIELD_MISSING", error.Code);
        Assert.Equal("assembly", error.Field);
        Assert.Equal("sample.provider.json", error.Source);
        Assert.Equal("assembly", error.Metadata["field"]);
    }

    [Fact]
    public void ProviderManifestMergePolicy_Metadata_UsesProviderBackendVendorPriority()
    {
        var descriptor = new ProviderManifestDescriptor
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["timeout"] = "10",
                ["endpoint"] = "provider"
            },
            BackendMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["endpoint"] = "backend"
            },
            VendorMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["vendor"] = "sample"
            }
        };

        var metadata = new ProviderManifestMergePolicy().MergeMetadata(descriptor);

        Assert.Equal("10", metadata["timeout"]);
        Assert.Equal("backend", metadata["endpoint"]);
        Assert.Equal("sample", metadata["vendor"]);
    }

    [Fact]
    public void ProviderManifestMergePolicy_CliHints_InheritAndOverrideDeterministically()
    {
        var hints = new ProviderManifestMergePolicy().MergeCliHints(
            new ProviderCliHints
            {
                Command = "provider",
                ConfigKeys = ["endpoint"]
            },
            new ProviderCliHints
            {
                Command = "backend",
                ConfigKeys = ["apiKey"]
            });

        Assert.Equal("backend", hints.Command);
        Assert.Equal(["apiKey", "endpoint"], hints.ConfigKeys);
    }

    [Fact]
    public void ProviderRegistry_Register_ListsProvidersDeterministically()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("z.provider", "chat"));
        registry.Register(CreateDescriptor("a.provider", "chat"));

        var providers = registry.List();

        Assert.Equal(["a.provider", "z.provider"], providers.Select(provider => provider.ProviderId));
    }

    [Fact]
    public void ProviderRouter_MissingProvider_ReturnsStructuredFailure()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("chat.provider", "chat.completion"));

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionPolicy
        {
            RequiredCapability = "embedding"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("PROVIDER_NOT_FOUND", result.ErrorCode);
        Assert.Equal("embedding", result.MissingProvider?.RequiredCapability);
    }

    [Fact]
    public void ProviderRouter_MultipleProviders_SelectsLexicalProviderIdDeterministically()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("b.provider", "chat.completion"));
        registry.Register(CreateDescriptor("a.provider", "chat.completion"));

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionPolicy
        {
            RequiredCapability = "chat.completion"
        });

        Assert.True(result.Succeeded);
        Assert.Equal("a.provider", result.Provider?.ProviderId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PROVIDER_DETERMINISTIC_SELECTION");
    }

    [Fact]
    public void ProviderRouter_DuplicateProviderRank_ReturnsDeterministicDiagnostic()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("a.provider", "chat.completion"));
        registry.Register(CreateDescriptor("a.provider", "chat.completion"));

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionPolicy
        {
            RequiredCapability = "chat.completion"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("PROVIDER_DUPLICATE", result.ErrorCode);
        Assert.Equal(["a.provider", "a.provider"], result.DuplicateProvider?.ProviderIds);
    }

    [Fact]
    public void ProviderRouter_RuntimeBackendHint_SelectsMatchingBackendFirst()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("z.provider", "chat.completion") with
        {
            BackendDescriptors =
            [
                new ProviderBackendDescriptor
                {
                    BackendName = "remote",
                    Kind = "remote",
                    Rank = 0,
                    Capabilities = ["chat.completion"]
                },
                new ProviderBackendDescriptor
                {
                    BackendName = "local",
                    Kind = "local",
                    Rank = 9,
                    Capabilities = ["chat.completion"]
                }
            ]
        });

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionPolicy
        {
            RequiredCapability = "chat.completion",
            RuntimeHints = new ProviderRuntimeHints { Backend = "local" }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("local", result.Backend?.BackendName);
    }

    [Fact]
    public void ProviderRouter_PriorityRanksBeforeBackendPreference()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("a.provider", "chat.completion") with
        {
            Priority = 10,
            BackendDescriptors =
            [
                new ProviderBackendDescriptor
                {
                    BackendName = "local",
                    Kind = "local",
                    Rank = 0,
                    Capabilities = ["chat.completion"]
                }
            ]
        });
        registry.Register(CreateDescriptor("b.provider", "chat.completion") with
        {
            Priority = 0,
            BackendDescriptors =
            [
                new ProviderBackendDescriptor
                {
                    BackendName = "remote",
                    Kind = "remote",
                    Rank = 0,
                    Capabilities = ["chat.completion"]
                }
            ]
        });

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionPolicy
        {
            RequiredCapability = "chat.completion",
            RuntimeHints = new ProviderRuntimeHints { Backend = "local" }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("b.provider", result.Provider?.ProviderId);
        Assert.Equal("remote", result.Backend?.BackendName);
    }

    [Fact]
    public void ProviderRouter_LocalPreference_IsOptionalAndDeterministic()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("provider", "chat.completion") with
        {
            BackendDescriptors =
            [
                new ProviderBackendDescriptor
                {
                    BackendName = "remote-a",
                    Kind = "remote",
                    Rank = 0,
                    Capabilities = ["chat.completion"]
                },
                new ProviderBackendDescriptor
                {
                    BackendName = "local-a",
                    Kind = "local",
                    Rank = 5,
                    Capabilities = ["chat.completion"]
                }
            ]
        });

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionPolicy
        {
            RequiredCapability = "chat.completion",
            RuntimeHints = new ProviderRuntimeHints { PreferLocalBackend = true }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("local-a", result.Backend?.BackendName);
    }

    [Fact]
    public void ProviderRouter_GetAvailabilityProbeOrder_UsesStableSort()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("b.provider", "chat.completion") with { Priority = 0 });
        registry.Register(CreateDescriptor("a.provider", "chat.completion") with { Priority = 0 });

        var order = new ProviderRouter(registry).GetAvailabilityProbeOrder(new ProviderResolutionPolicy
        {
            RequiredCapability = "chat.completion"
        });

        Assert.Equal(["a.provider", "b.provider"], order.Select(candidate => candidate.Provider.ProviderId));
        Assert.Equal([0, 1], order.Select(candidate => candidate.Ordinal));
    }

    [Fact]
    public async Task ProviderManifestCatalog_ExistingManifests_LoadDeterministically()
    {
        var root = FindRepositoryRoot();
        var manifestPaths = Directory
            .EnumerateFiles(Path.Combine(root.FullName, "src"), "*.provider.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var result = await new ProviderManifestCatalog().LoadFilesAsync(
            manifestPaths,
            new ProviderManifestValidationOptions { RequireCapabilities = true },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Providers, provider => provider.ProviderId == "openai.chat");
        Assert.Contains(result.Providers, provider => provider.ProviderId == "cuda.compute");
        Assert.Equal(
            result.Providers.Select(provider => provider.ProviderId).Order(StringComparer.Ordinal),
            result.Providers.Select(provider => provider.ProviderId));
    }

    [Fact]
    public void ProviderRouter_ExplicitFallback_SelectsDeterministically()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("chat.provider", "chat.completion"));
        registry.Register(CreateDescriptor("fallback.provider", "logging.console"));

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionPolicy
        {
            RequiredCapability = "embedding",
            Fallback = new DeterministicFallbackPolicy
            {
                Enabled = true,
                ProviderIds = ["fallback.provider"]
            }
        });

        Assert.True(result.Succeeded);
        Assert.Equal("fallback.provider", result.Provider?.ProviderId);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "PROVIDER_FALLBACK_USED");
    }

    [Fact]
    public void ProviderRouter_StrictRequest_DisablesFallback()
    {
        var registry = new ProviderRegistry();
        registry.Register(CreateDescriptor("fallback.provider", "logging.console"));

        var result = new ProviderRouter(registry).Resolve(new ProviderResolutionRequest
        {
            Mode = ProviderResolutionMode.Strict,
            Policy = new ProviderResolutionPolicy
            {
                RequiredCapability = "embedding",
                Fallback = new DeterministicFallbackPolicy
                {
                    Enabled = true,
                    ProviderIds = ["fallback.provider"]
                }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("PROVIDER_NOT_FOUND", result.ErrorCode);
    }

    private static ProviderManifestDescriptor CreateDescriptor(
        string providerId,
        string capability)
        => new()
        {
            ProviderId = providerId,
            Name = providerId,
            Version = "0.1.1",
            AssemblyName = providerId + ".dll",
            Capabilities = [capability]
        };

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
}
