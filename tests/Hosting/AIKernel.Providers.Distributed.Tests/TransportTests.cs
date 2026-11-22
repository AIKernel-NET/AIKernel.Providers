using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AIKernel.Providers.Distributed.Tests;

public sealed class TransportTests
{
    [Fact]
    public async Task TlsTerminatorPreservesAdvertisedPrefixAndRanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var dir = Directory.CreateTempSubdirectory("aikernel-proxy-");
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback);
        certificateRequest.CertificateExtensions.Add(san.Build());
        using var generated = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
        var builder = WebApplication.CreateSlimBuilder(); builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, o => o.UseHttps(certificate)));
        await using var proxy = builder.Build();
        using var upstream = new HttpClient(new SocketsHttpHandler {AllowAutoRedirect=false,UseCookies=false});
        Uri? backend = null;
        proxy.MapGet("/family/audio", async (HttpContext ctx) =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(backend!, "/family/audio"));
            request.Headers.TryAddWithoutValidation("Authorization", ctx.Request.Headers.Authorization.ToString());
            request.Headers.TryAddWithoutValidation("Range", ctx.Request.Headers.Range.ToString());
            request.Headers.Add("X-Forwarded-Proto","https");
            using var response = await upstream.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ctx.RequestAborted);
            ctx.Response.StatusCode = (int)response.StatusCode;
            ctx.Response.Headers.ContentRange = response.Content.Headers.ContentRange?.ToString();
            await response.Content.CopyToAsync(ctx.Response.Body,ctx.RequestAborted);
        });
        try
        {
            await proxy.StartAsync(ct);
            var advertised = new Uri(proxy.Urls.Single() + "/family/");
            // Use an isolated dynamic listener and capture the OS-assigned backend address.
            var identity = new ApplicationCredentials(dir.FullName);
            var descriptor = new ConnectionFiles();
            var options = new DistributedHostOptions {Name="studio",Service="sample",PrivateDirectory=dir.FullName,
                ConnectionFile=Path.Combine(dir.FullName,"connection.json"),AdvertisedAddress=advertised,TrustedProxies=[IPAddress.Loopback]};
            var media = new Transfer();
            await using var host = new DistributedApplicationHost(options,identity,identity,descriptor,[media],[]);
            await host.StartAsync(ct);
            backend = host.ListeningAddress;
            using var file = File.OpenRead(options.ConnectionFile); var exported = descriptor.Read(file);
            Assert.Equal(advertised.AbsoluteUri.TrimEnd('/'),exported.ApiUrl);
            using var handler = new HttpClientHandler {AllowAutoRedirect=false};
            handler.ServerCertificateCustomValidationCallback = (_,peer,_,errors) =>
                peer != null && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == 0 && peer.RawData.SequenceEqual(certificate.RawData);
            using var client = new HttpClient(handler);
            using var req = new HttpRequestMessage(HttpMethod.Get,new Uri(advertised,"audio"));
            req.Headers.Authorization = new("Bearer",exported.Token); req.Headers.Range=new(1,3);
            using var result = await client.SendAsync(req,ct);
            Assert.Equal(HttpStatusCode.PartialContent,result.StatusCode);
            Assert.Equal("bytes 1-3/6",result.Content.Headers.ContentRange!.ToString());
            Assert.Equal("bcd",await result.Content.ReadAsStringAsync(ct));
        }
        finally { await proxy.StopAsync(ct); dir.Delete(true); }
    }

    [Fact]
    public async Task NoRedirectAndClientCancellationReachesHandler()
    {
        var ct = TestContext.Current.CancellationToken; var dir = Directory.CreateTempSubdirectory("aikernel-stream-");
        var o = new DistributedHostOptions {Name="transfer",Service="sample",PrivateDirectory=dir.FullName,ConnectionFile=Path.Combine(dir.FullName,"connection.json")};
        var identity = new ApplicationCredentials(dir.FullName); var descriptors = new ConnectionFiles(); var transfer = new Transfer();
        await using var host = new DistributedApplicationHost(o,identity,identity,descriptors,[transfer],[]);
        try
        {
            await host.StartAsync(ct); using var store = new ConnectionProfiles(Path.Combine(dir.FullName,"client"), descriptors);
            using(var file=File.OpenRead(o.ConnectionFile)) store.Import(file);
            var id = store.Selected!.Value;
            using var redirect = await store.SendAsync(id,HttpMethod.Get,"redirect",cancellationToken:ct);
            Assert.Equal(HttpStatusCode.Redirect,redirect.StatusCode);
            using var cancel=CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending=store.SendAsync(id,HttpMethod.Get,"wait",cancellationToken:cancel.Token);
            await transfer.Started.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);
            cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
            await transfer.Aborted.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);
        }
        finally { await host.StopAsync(ct); dir.Delete(true); }
    }

    private sealed class Transfer : IApiEndpointProvider
    {
        public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Aborted {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<ApiEndpoint> Endpoints => new[] {"audio","wait","redirect"}.Select(name=>new ApiEndpoint("GET",name,name,"client"));
        public async ValueTask<ApiResponse> InvokeAsync(string operationId,ApiInvocation request,CancellationToken ct)
        {
            if(operationId=="redirect") return new(302,Location:"https://example.invalid/");
            if(operationId=="audio") return new(200,Body:new MemoryStream("abcdef"u8.ToArray()),ContentType:"audio/wav",EnableRanges:true);
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite,ct); }
            finally { if(ct.IsCancellationRequested) Aborted.TrySetResult(); }
            return new(204);
        }
    }
}
