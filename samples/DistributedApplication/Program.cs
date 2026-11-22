using AIKernel.Abstractions.Hosting;
using AIKernel.Providers.Distributed;
using DistributedApplication.Sample;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var directory = Path.GetFullPath(args.FirstOrDefault() ?? "private");
var file = Path.Combine(directory, "connection.json");
builder.Services.AddSingleton<Notes>();
builder.Services.AddSingleton<IApiEndpointProvider>(s => s.GetRequiredService<Notes>());
builder.Services.AddSingleton<IMcpToolProvider>(s => s.GetRequiredService<Notes>());
builder.Services.AddDistributedApplicationProvider(new()
{
    Service = "Example.Notes", Name = "Local notes", PrivateDirectory = directory, ConnectionFile = file,
    BindAddress = new Uri(builder.Configuration["Distributed:BindAddress"] ?? "http://127.0.0.1:0"),
    AllowPlainHttp = bool.TryParse(builder.Configuration["Distributed:AllowPlainHttp"], out var allow) && allow
});
using var app = builder.Build();
var server = app.Services.GetRequiredService<IDistributedApplicationHost>();
await server.StartAsync();
Console.WriteLine(file);
try { await app.RunAsync(); }
finally { await server.StopAsync(); }
