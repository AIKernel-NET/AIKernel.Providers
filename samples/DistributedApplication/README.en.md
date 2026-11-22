# Save a note over MCP and read it over HTTP

From the root of this repository, run the sample with the .NET 10 SDK. Choose a private directory for its credentials.

```sh
dotnet run --project samples/DistributedApplication -- ./private-notes
```

The sample prints the path to `connection.json` after it starts listening on a loopback port. Configure your MCP client with the file's `mcp_url` and its bearer token. Call `save_note` with `{"text":"Check the recording"}` and copy the returned ID. Send an authenticated `GET` to `<api_url>/notes/<id>`. The JSON response contains the same text. Notes live in memory for the lifetime of this sample process.

For your own Generic Host, reference `AIKernel.Hosting` and `AIKernel.Providers.Distributed` at version 0.1.4. Register your application service, then expose it through `IApiEndpointProvider` and `IMcpToolProvider`. Use `AddDistributedApplicationProvider` to configure the private directory and exported file. Chain `WithDistributedApplication()` after `AddAIKernelCore()` to start and stop the listener with your host.

On the client, call `AddDistributedApplicationClient`, import the connection file into `IConnectionProfileStore`, and pass the chosen server ID to `IConnectionClient.SendAsync`. Store connection files outside web roots and source control. If a token is rotated, import the new file for the same server ID.

[日本語](README.md) · [Sample service and endpoint definitions](Notes.cs)
