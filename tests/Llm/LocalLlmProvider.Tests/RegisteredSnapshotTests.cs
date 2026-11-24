using System.Text.Json.Nodes;
using AIKernel.Dtos.Capabilities;
using AIKernel.Providers.LocalLlm;

namespace AIKernel.Providers.Tests;

public class RegisteredSnapshotTests
{
    public static bool HasSnapshots => Directory.Exists(Environment.GetEnvironmentVariable("AIKERNEL_TEST_QWEN3_SNAPSHOTS"));

    [Theory(SkipUnless=nameof(HasSnapshots), Skip="Requires pinned Qwen3 CPU snapshots.")]
    [InlineData("onnx","onnx/onnx/model.onnx","en")]
    [InlineData("safetensors","safe/model.safetensors","ja")]
    public async Task ChatLocalRecoversFromARejectedProfileAndCancellation(string format,string relative,string language)
    {
        var root=Environment.GetEnvironmentVariable("AIKERNEL_TEST_QWEN3_SNAPSHOTS")!;
        string scratch=Path.GetTempFileName();
        try
        {
            using var stream=typeof(LocalLlmInvoker).Assembly.GetManifestResourceStream("AIKernel.LocalLlm.qwen3-06b-"+format)!;
            using var reader=new StreamReader(stream);
            var token=TestContext.Current.CancellationToken;
            var profile=JsonNode.Parse(await reader.ReadToEndAsync(token))!;
            var correct=profile.ToJsonString();
            profile["weightsSha256"]=new string('0',64);
            await File.WriteAllTextAsync(scratch,profile.ToJsonString(),token);
            bool en=language=="en";
            using var invoker=new LocalLlmInvoker(new RegisteredLocalLlmSettings {
                BonsaiModelPath=Path.Combine(root,relative),RegistrationFile=scratch,ContextTokens=128,MaxNewTokens=16,
                SystemPrompt=en?"Answer briefly.":"簡潔に答えてください。"});
            var request=new CapabilityInvocationRequest("snap","local-llm","chat.local",
                new Dictionary<string,string>{{"prompt",en?"What is the capital of France?":"日本の首都はどこですか？"}},null,null,new Dictionary<string,string>());
            Assert.Equal("LOCAL_LLM_MODEL_UNAVAILABLE",(await invoker.InvokeAsync(request,token)).ErrorCode);
            await File.WriteAllTextAsync(scratch,correct,token);
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async ()=>await invoker.InvokeAsync(request,cancel.Token));
            var result=await invoker.InvokeAsync(request,token);
            Assert.True(result.Succeeded,result.ErrorMessage);
            Assert.Equal(en?"The capital of France is Paris.":"日本の首都は、大阪市です。",result.Metadata["text"]);
        }
        finally { File.Delete(scratch); }
    }
}
