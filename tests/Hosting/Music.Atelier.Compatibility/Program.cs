using AIKernel.Abstractions.Hosting;
using AIKernel.Dtos.Hosting;
using AIKernel.Providers.Distributed;
using TheoriaCast.Music;
using TheoriaCast.Music.Api;
using TheoriaCast.Music.Mcp;
using System.Text.Json;
using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

var root = args[0];
var opts = new DistributedHostOptions { Name=args[1], Service="TheoriaCast.Music.Api",
    PrivateDirectory=root, ConnectionFile=Path.Combine(root,"connection.json") };
var identity = new ApplicationCredentials(root); var api = new MusicOperations(new MusicStore(root));
await using var host = new DistributedApplicationHost(opts,identity,identity,new ConnectionFiles(),[api],[api]);
await host.StartAsync();
await Task.Delay(Timeout.Infinite);

sealed class MusicOperations(MusicStore db) : IApiEndpointProvider, IMcpToolProvider
{
    static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    readonly Dictionary<string,AIFunction> functions = typeof(MusicTools).GetMethods()
        .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
        .Select(m=> AIFunctionFactory.Create(m,new MusicTools(db),new AIFunctionFactoryOptions {
            Name=m.GetCustomAttribute<McpServerToolAttribute>()!.Name,Description=m.GetCustomAttribute<DescriptionAttribute>()?.Description,
            SerializerOptions=Json })).ToDictionary(f=>f.Name);
    public IEnumerable<McpOperation> Tools => functions.Values.Select(f=>new McpOperation(f.Name,f.Description??"",f.JsonSchema,f.Name,"client"));
    public async ValueTask<JsonElement> CallAsync(string id,JsonElement input,RequestIdentity caller,CancellationToken ct)
    {
        var args=new AIFunctionArguments();foreach(var property in input.EnumerateObject())args[property.Name]=property.Value;
        return JsonSerializer.SerializeToElement(await functions[id].InvokeAsync(args,ct),Json);
    }
    public IEnumerable<ApiEndpoint> Endpoints => [new("POST","studio/requests","ask","client"),new("GET","studio/requests/{id}","get","client"),
        new("POST","studio/requests/{id}/decision","decide","client"),new("GET","nodes","nodes","client"),new("GET","tracks","tracks","client"),
        new("GET","jobs","jobs","client"),new("GET","works","works","client")];
    public async ValueTask<ApiResponse> InvokeAsync(string op,ApiInvocation req,CancellationToken ct)
    {
        object val=op switch {
            "ask"=>db.Ask((await JsonSerializer.DeserializeAsync<StudioQuestion>(req.Body,Json,ct))!),
            "get"=>db.ConsultationById(req.RouteValues["id"]),
            "decide"=>db.Decide(req.RouteValues["id"],(await JsonSerializer.DeserializeAsync<StudioDecision>(req.Body,Json,ct))!.decision),
            "nodes"=>db.Nodes(),"tracks"=>db.Tracks(),"jobs"=>db.Jobs(),"works"=>db.Works(),_=>throw new KeyNotFoundException() };
        return new(200,JsonSerializer.SerializeToElement(val,Json));
    }
}
