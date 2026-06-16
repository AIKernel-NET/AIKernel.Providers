namespace AIKernel.Providers.CudaCompute;

using AIKernel.Providers.Compute;
using AIKernel.Providers.Substrate;

/// <summary>
/// [EN] Resolves CUDA backend descriptors without loading native modules.
/// [JA] native module を load せずに CUDA backend descriptor を解決します。
/// </summary>
public sealed class CudaBackendResolver
{
    /// <summary>
    /// [EN] Resolves a backend descriptor against a descriptor-only policy.
    /// [JA] descriptor-only policy に対して backend descriptor を解決します。
    /// </summary>
    /// <param name="descriptor">EN:  JA: descriptor パラメーターです。
    /// [EN] CUDA backend descriptor.
    /// [JA] CUDA backend descriptor です。
    /// </param>
    /// <param name="policy">EN:  JA: policy パラメーターです。
    /// [EN] CUDA backend resolution policy.
    /// [JA] CUDA backend resolution policy です。
    /// </param>
    /// <returns>EN:  JA: 結果を返します。
    /// [EN] Structured backend resolution result.
    /// [JA] 構造化された backend resolution result です。
    /// </returns>
    public CudaBackendResolutionResult Resolve(
        CudaBackendDescriptor? descriptor,
        CudaBackendResolutionPolicy? policy)
    {
        if (descriptor is null)
        {
            return Failure(
                "CUDA_BACKEND_DESCRIPTOR_MISSING",
                "CUDA backend descriptor is required.",
                ComputeAvailabilityReason.BackendNotInstalled);
        }

        if (policy is null)
        {
            return Failure(
                "CUDA_BACKEND_POLICY_MISSING",
                "CUDA backend resolution policy is required.",
                ComputeAvailabilityReason.Unknown);
        }

        if (!string.IsNullOrWhiteSpace(policy.RequiredBackend) &&
            !string.Equals(descriptor.BackendName, policy.RequiredBackend, StringComparison.Ordinal))
        {
            return Failure(
                "CUDA_BACKEND_MISMATCH",
                "CUDA backend name does not match the requested backend.",
                ComputeAvailabilityReason.BackendNotInstalled);
        }

        if (!string.IsNullOrWhiteSpace(policy.RequiredDeviceProfile) &&
            !string.Equals(descriptor.DeviceProfile, policy.RequiredDeviceProfile, StringComparison.Ordinal))
        {
            return Failure(
                "CUDA_DEVICE_PROFILE_MISMATCH",
                "CUDA device profile does not match the requested profile.",
                ComputeAvailabilityReason.DeviceUnsupported);
        }

        if (string.IsNullOrWhiteSpace(descriptor.NativeModule.ModuleRef))
        {
            return Failure(
                "CUDA_NATIVE_MODULE_MISSING",
                "CUDA backend descriptor does not carry a native module reference.",
                ComputeAvailabilityReason.NativeModuleMissing);
        }

        if (!descriptor.NativeModule.ModuleRef.StartsWith("aikernel-cuda://", StringComparison.Ordinal))
        {
            return Failure(
                "CUDA_NATIVE_MODULE_REF_INVALID",
                "CUDA backend descriptor must use the aikernel-cuda:// native module reference scheme.",
                ComputeAvailabilityReason.NativeModuleMissing);
        }

        var missingOperations = policy.RequiredOperations
            .Where(operation => !descriptor.Operations.Contains(operation, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missingOperations.Length > 0)
        {
            return Failure(
                "CUDA_OPERATION_NOT_ADVERTISED",
                "CUDA backend descriptor does not advertise all requested operations.",
                ComputeAvailabilityReason.UnsupportedOperation);
        }

        return new CudaBackendResolutionResult
        {
            Succeeded = true,
            Backend = descriptor,
            AvailabilityReason = ComputeAvailabilityReason.Unknown
        };
    }

    private static CudaBackendResolutionResult Failure(
        string code,
        string message,
        ComputeAvailabilityReason reason)
        => new()
        {
            Succeeded = false,
            AvailabilityReason = reason,
            ErrorCode = code,
            ErrorMessage = message,
            Diagnostics =
            [
                new ProviderDiagnostic
                {
                    Code = code,
                    Message = message,
                    Severity = "Error",
                    Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["availabilityReason"] = reason.ToString()
                    }
                }
            ]
        };
}
