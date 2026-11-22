# HTTP と MCP で同じメモを扱う

[English](README.en.md)

.NET 10 で次を実行すると、OS が割り当てたループバックポートでサンプルが起動します。引数は接続ファイルを保管する非公開ディレクトリです。

```sh
dotnet run --project samples/DistributedApplication -- ./private-notes
```

表示された `connection.json` をクライアントに読み込ませます。MCP クライアントにはファイルの `mcp_url` と Bearer トークンを指定します。`save_note` に `{"text":"音源を確認する"}` を渡すと ID が返ります。同じ資格情報で `GET <api_url>/notes/<id>` を呼ぶと、保存した文が返ります。メモはプロセス内に保持されます。

既存の Generic Host では、アプリのサービスを `IApiEndpointProvider` と `IMcpToolProvider` に登録し、以下の配線で待受を開始できます。モジュールを使う場合は `WithDistributedApplication(module)` に `IKernelModule` を渡せます。

```csharp
builder.Services.AddDistributedApplicationProvider(new DistributedHostOptions {
    Service = "Example.Notes", Name = "Local notes",
    PrivateDirectory = privateDirectory,
    ConnectionFile = Path.Combine(privateDirectory, "connection.json")
});
builder.Services.AddAIKernelCore().WithDistributedApplication();
```

クライアントでは `AddDistributedApplicationClient(privateDirectory)`、`IConnectionProfileStore.Import(stream)` の順に使います。各画面でサーバー ID を保持し、`IConnectionClient.SendAsync(serverId, ...)` に渡します。生存状態は `IHeartbeatProvider.Get(serverId)` と `Changed` から取得できます。標準の監視間隔は15秒、応答待ちは4秒です。

TLS を終端するプロキシでは、`BindAddress` にバックエンドの待受、`AdvertisedAddress` に公開 HTTPS URL と接頭辞、`TrustedProxies` にプロキシの IP を設定します。LAN で平文 HTTP を許可する場合は、サーバーの `AllowPlainHttp` とクライアント登録の `allowPlainHttp` を有効にします。秘密ファイルのディレクトリは Web の配信対象外に置きます。

標準の資格情報を更新するには、サーバー側で `ApplicationCredentials.Rotate()` を呼び、稼働中の `IDistributedApplicationHost.ExportConnectionAsync()` で新しいファイルを書き出します。クライアントが再インポートすると、同じサーバー ID のまま接続が復旧します。

```sh
dotnet test tests/Hosting/AIKernel.Providers.Distributed.Tests -c Release
```
