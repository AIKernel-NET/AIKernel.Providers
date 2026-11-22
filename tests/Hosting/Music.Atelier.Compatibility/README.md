# TheoriaCast.Music の互換性試験

Release ビルド済みの Music.Api の業務クラスと MusicTools を読み込み、ローカル NuGet フィードの `AIKernel.Providers.Distributed` で 2 台のサーバーを起動します。テストアダプターの HTTP ルートは相談画面で使う操作を対象にしています。各サーバーの SQLite、Atelier の接続先、Edge のプロファイルは試験ごとの一時ディレクトリに置きます。GPU ワーカーの登録は不要です。

.NET 10、Python 3.12、Node.js 22 以降、Edge、Codex CLI と、同じ版の TheoriaCast.Music.Api／Atelier の Release ビルドを用意します。次の例はこのディレクトリで実行します。`$music` は TheoriaCast のチェックアウト、`$config` は候補パッケージのローカルフィードと依存パッケージを含む NuGet 設定です。

```powershell
dotnet build -c Release "-p:MusicBinaryDirectory=$music/src/TheoriaCast.Music.Api/bin/Release/net10.0" --configfile $config
py check.py --music-root $music --edge "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"
```

同じ Atelier の 2 タブで別々の相談を作成し、Codex の MCP クライアントが片方の ID を読み、提案を返します。元タブだけに提案が表示されること、別サーバーへの切り替え後に元サーバーの下書きを復元できることを確認します。Codex には一時セッションを使い、モデル推論や作曲の予約は行いません。
