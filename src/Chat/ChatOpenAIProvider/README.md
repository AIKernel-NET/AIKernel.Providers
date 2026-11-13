# Bounded chat completions

Set `ChatOpenAISettings.MaxCompletionTokens` to cap generation. The field is omitted when null for compatibility with existing clients. Values at or below zero are rejected before dispatch.

`ChatOpenAIProvider.GenerateDetailedAsync(messages, cancellationToken)` returns `ChatCompletionResult`: content, finish reason, reported prompt tokens and reported completion tokens. Missing usage stays null; it must not be treated as a zero-cost call. A `length` finish reason means the reply may be incomplete.

`GenerateAsync` remains available and returns only the text. Both non-streaming calls honor the configured timeout and caller cancellation. The provider does not itself persist a spending budget. A host should reserve before dispatch and settle only with authoritative usage; AIKernel.Hosting.DurableTokenBudget provides the local single-host implementation used by the inquiry demo.

日本語: `MaxCompletionTokens`で最大出力を指定し、`GenerateDetailedAsync`で終了理由・使用量を取得できます。使用量が欠けた応答を無料扱いせず、タイムアウト時も予約を保持してください。キーはサーバー側の資格情報プロバイダーから読み込み、ブラウザーへ渡しません。
