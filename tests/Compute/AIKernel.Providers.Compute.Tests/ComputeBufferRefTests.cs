namespace AIKernel.Providers.Compute.Tests;

using AIKernel.Providers.Compute;

public sealed class ComputeBufferRefTests
{
    [Fact]
    public void Validate_StandardDTypeAndShape_Succeeds()
    {
        var result = new ComputeBufferRefValidator().Validate(new ComputeBufferRef
        {
            BufferId = "input",
            DType = ComputeBufferDTypes.F32,
            Shape = "1,256,256"
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_UnknownDType_FailsClosed()
    {
        var result = new ComputeBufferRefValidator().Validate(new ComputeBufferRef
        {
            BufferId = "input",
            DType = "bf4",
            Shape = "1,256,256"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("COMPUTE_BUFFER_DTYPE_UNSUPPORTED", result.ErrorCode);
    }

    [Fact]
    public void ToMetadata_ProjectsShapeStrideLayoutAndBackendMetadata()
    {
        var metadata = new ComputeBufferRef
        {
            BufferId = "input",
            DType = ComputeBufferDTypes.U8,
            Shape = "1,256,256",
            Stride = "65536,256,1",
            Layout = "contiguous",
            Hash = HashMetadata.FromExpression("sha256:abc"),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["backend.buffer"] = "device:0"
            }
        }.ToMetadata();

        Assert.Equal("u8", metadata["compute.buffer.dtype"]);
        Assert.Equal("1,256,256", metadata["compute.buffer.shape"]);
        Assert.Equal("65536,256,1", metadata["compute.buffer.stride"]);
        Assert.Equal("contiguous", metadata["compute.buffer.layout"]);
        Assert.Equal("sha256:abc", metadata["compute.buffer.hash.expression"]);
        Assert.Equal("device:0", metadata["compute.buffer.metadata.backend.buffer"]);
    }

    [Fact]
    public void ComputeEntryPointDescriptor_CarriesModuleBoundaryWithoutBackendTypes()
    {
        var descriptor = new ComputeEntryPointDescriptor
        {
            Name = "matmul",
            Operation = "tensor.matmul",
            Parameters =
            [
                new ComputeParameterDescriptor
                {
                    Name = "input",
                    DType = ComputeBufferDTypes.F32,
                    Shape = "1,256"
                }
            ],
            Return = new ComputeReturnDescriptor
            {
                DType = ComputeBufferDTypes.F32,
                Shape = "1,256"
            }
        };

        Assert.Equal("tensor.matmul", descriptor.Operation);
        Assert.Equal(ComputeBufferDTypes.F32, descriptor.Parameters.Single().DType);
        Assert.Equal("1,256", descriptor.Return.Shape);
    }

    [Fact]
    public void ComputeProviderBase_CarriesBackendNeutralCapabilityDescriptor()
    {
        var provider = new TestComputeProvider(new ComputeCapabilityDescriptor
        {
            ProviderId = "compute.test",
            Capabilities = ["tensor.compute"],
            SupportedDTypes = [ComputeBufferDTypes.F32],
            SupportedOperations = ["tensor.matmul"]
        });

        Assert.Equal("compute.test", provider.Capability.ProviderId);
        Assert.Equal(["tensor.matmul"], provider.Capability.SupportedOperations);
    }

    private sealed class TestComputeProvider(ComputeCapabilityDescriptor capability)
        : ComputeProviderBase(capability);
}
