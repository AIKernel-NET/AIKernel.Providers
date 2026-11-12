# Scene v1 状態・エラーコード一覧

更新: 2026-11-12 / AIKernel.Providers.Cosmos 0.1.4-dev13（停止確認の保存失敗後も物理処理フラグを保持。新コード追加なし）。
受付失敗は envelope.ErrorCode、受付後は result.ErrorCode、稼働状態は readiness.ReasonCode を確認する。
status は COSMOS_SCENE_P0.md と result.schema.json に従う。未知のコードを成功に読み替えない。
以下は標準実装のコード。独自バックエンド・認可アダプターも短い非機密コードだけを返すこと。

## dev7の受付エラー許可リスト

認可アダプター由来のSceneException.Codeは、次の6種類だけを採用する。

`unauthenticated`, `permission_denied`, `actor_revision_mismatch`, `action_not_authorized`,
`target_not_authorized`, `authorization_unavailable`。

それ以外の認可コード、通常例外、不正なgrantはauthorization_unavailableに置き換える。
認可アダプターがbackend_oom等の他分類のコードを返しても、この6種類以外は採用しない。

最終的なCapability受付エラーおよびセッション開始・メディア登録のSceneException.Codeは次の40種類だけを返す。

| 分類 | 受付時に許可するコード |
|---|---|
| API | capability_unsupported, operation_unsupported, unknown_argument |
| 認可 | unauthenticated, permission_denied, authorization_unavailable, action_not_authorized, target_not_authorized |
| 形式 | invalid_request, invalid_json, duplicate_field, invalid_identifier, duplicate_identifier, schema_unsupported, mode_unsupported |
| 整合 | hash_mismatch, inconsistent_snapshot, actor_revision_mismatch, catalog_mismatch |
| 期限・世代 | invalid_deadline, future_capture, observation_expired, stale_context |
| 冪等性 | request_id_conflict, request_not_found |
| メディア | media_unsupported, media_unavailable, media_expired, invalid_media |
| 容量 | payload_too_large, input_limit, media_too_large, media_capacity, media_busy, job_capacity, caller_capacity, session_capacity |
| 稼働・台帳 | backend_work_unconfirmed, disabled, journal_unavailable |

比較は大文字小文字を区別した完全一致。trim、大小文字変換、接頭辞一致、例外本文からのコード抽出は行わない。
許可外はinvalid_requestへ置換する。ErrorMessageは安全なErrorCodeと同じ文字列、Metadataは空、OutputHashはnull。
ホストの元例外やDataを保持せず、WorkMayStillBeRunning等のホスト例外の付加情報も受付例外へコピーしない。
独自コード・詳細本文を公開する設定は用意しない。

境界で捕捉した取消は成功・通常のエラー応答へ変換せず、OperationCanceledExceptionまたはTaskCanceledExceptionを新しく生成して伝播する。
本文は固定の`Scene request canceled.`、InnerExceptionはnull、Dataは空。呼出元が渡したCancellationTokenは維持する。
認可内部の取消も既存どおり取消例外で返すが、内部トークンやTaskを転送しない。
Task.Run開始前に呼出元が取り消していた場合は、.NETの既定の取消例外となり、認可・メディア処理は呼ばない。
通常の入力JSONのinvalid_identifier等は維持し、不正grant由来はauthorization_unavailableとなる。
保存失敗のjournal_unavailableを認可エラーで上書きせず、従来どおり候補・新規推論を停止する。

この許可リストは受付境界に適用する。受付後のjob result.ErrorCodeとreadiness.ReasonCodeには次のdev8契約を適用する。
HTTPホストはSDK例外のスタックトレースを公開応答へ転写しないこと。

## dev8のreadiness理由コード（25種類）

| 分類 | 許可コード |
|---|---|
| 正常 | ready |
| 稼働・probe | disabled, not_probed, probe_required, probe_timed_out, probe_failed |
| 負荷・物理処理 | backend_not_ready, inference_capacity, backend_busy, backend_unavailable, backend_oom, backend_work_unconfirmed |
| 配置・接続 | model_revision_mismatch, endpoint_not_allowed, deployment_evidence_invalid, deployment_mismatch, deployment_state_unknown, runtime_mismatch, model_mismatch |
| 配置状態 | deployment_disabled, deployment_loading, deployment_warming_up, deployment_busy, deployment_degraded, deployment_unavailable |

stateもdisabled/loading/warming_up/ready/busy/degraded/unavailableの7種類に限る。
理由は大文字小文字を区別した完全一致。未知理由はprobe_failedとし、既知の非ready状態は維持する。
未知state、null応答、不正なready主張はunavailable / probe_failedにし、deploymentVerified=false、retryAfterMs=nullとする。
ready主張はstate=ready、reasonCode=ready、deploymentVerified=trueがそろう場合だけ受け付ける。
非ready状態でreasonCode=readyも不整合としてprobe_failedにする。正しい既知の組合せとretryAfterMsは維持する。
providerの一致検査も維持し、不一致はdegraded / model_revision_mismatchとして設定済みidentityを返す。
直接ProbeAsyncとscene.capabilitiesの両方に適用し、unknownを成功・実行可能と解釈しない。
probeの通常例外はprobe_failed。取消例外は例外本文・InnerException・Dataを除去し、呼出元トークンで取消として伝播する。

## dev8のジョブ理由コード（77種類）

上記readiness理由からreadyを除いた24種類と、次の53種類だけを許可する。

| 分類 | 追加で許可するジョブコード |
|---|---|
| 認可 | unauthenticated, permission_denied, actor_revision_mismatch, action_not_authorized, target_not_authorized, authorization_unavailable |
| 形式・容量 | invalid_request, invalid_json, duplicate_field, invalid_identifier, duplicate_identifier, schema_unsupported, mode_unsupported, payload_too_large, input_limit, output_limit |
| 整合 | inconsistent_snapshot, catalog_mismatch, hash_mismatch, invalid_deadline |
| メディア | media_unsupported, media_unavailable, media_expired, invalid_media, media_too_large |
| 観測・候補 | invalid_evidence, unknown_actor, unknown_target, invalid_unknown, invalid_region, duplicate_proposal, temporal_evidence_required |
| 推論応答 | invalid_backend_response, response_truncated, content_filtered, completion_incomplete, unexpected_tool_call, backend_response_too_large, backend_request_rejected |
| 物理処理 | backend_transport_unconfirmed, backend_failed_unconfirmed, backend_canceled |
| 期限・履歴 | observation_deadline, clock_unverified, freshness_unverified_or_expired, stale_context, context_changed, context_invalidated, newer_observation, caller_canceled, service_stopping, runtime_restarted |
| 台帳 | journal_unavailable |

未知コード、大小文字違い、接頭辞・空白・改行付き、空文字はinvalid_backend_responseへ置換する。
nullはaccepted/queued/running/completedでは正常な「理由なし」。その他の状態でのnullは不正理由として置換する。
Finishで保存前に、応答生成で返却前に正規化する。旧台帳は起動時に正規化して保存する。
不正コードを置換した記録は候補を空にしてfreshnessVerified=falseにするが、観測履歴・状態・期限・物理処理フラグは維持する。
既知コードの台帳バイト列・hashは変更しない。不正コードを置換した記録の応答内容とOutputHashは変わるが、hash計算式と台帳v1形式は変えない。
backend_busyは1000ms、backend_unavailable/backend_oomは5000msという既存のジョブ再試行目安を維持する。
WorkMayStillBeRunningは未知コードでも保持し、物理処理の隔離を解除しない。通常例外・内部取消によるbackend_failed_unconfirmedも維持する。
先に確定したcanceled/timed_out/supersededは遅い失敗で上書きしない。
backendCancellationは既存どおり許可値以外をunconfirmedにする。モデル観測のreasonCodeも従来の別許可リストで検証する。

## dev9のホスト運用エラー（9種類）

許可コードはinvalid_identifier、request_not_found、disabled、backend_still_active、
journal_invalid、journal_capacity、journal_unavailable、deployment_evidence_invalid、host_operation_failedのみ。
大文字小文字を区別した完全一致であり、接頭辞・空白・独自コード・空文字・nullは認めない。
ホストの元例外を再throwせず、新しい例外を作り、Message/InnerException/Data/Task/内部取消トークンを引き継がない。
SceneExceptionのMessageはCodeと同じ。WorkMayStillBeRunningだけは公開された安全判断の真偽値として保持する。

| 境界 | 未知の失敗の扱い |
|---|---|
| SceneService初期化のbackend identity取得、ConfirmBackendQuiescent、RevokeRequestAuthorization、DisposeAsync | host_operation_failed |
| 台帳作成・読込みを伴う初期化 | journal_unavailable。既知のjournal_invalid等は維持 |
| SceneDeploymentFileSource.ReadAsync | deployment_evidence_invalid。原パス・I/O例外・JSON内容は返さない |
| CosmosNimBackend.Dispose | host_operation_failed |
| CosmosNimBackend設定・SceneDeploymentFileSourceのパス設定 | ArgumentExceptionの固定メッセージと固定ParamName。元の例外は保持しない |

SceneServiceのnull引数・上限制約のArgumentException系は従来の固定文を維持する。
台帳初期化から出ていた生のIOException/パス例外等はjournal_unavailableへ変わるため、その型に依存した接続はCodeで処理すること。
取消はOperationCanceledException/TaskCanceledExceptionのまま。トークンを受け取るAPIでは呼出元トークンを保持し、
同期運用APIやDisposeAsyncには呼出元トークンがないためNoneにする。直接CosmosNimBackend.ProbeAsyncの取消にも適用する。
既存のprobe内部timeoutはprobe_timed_out、その他のprobe例外はprobe_failedのまま。

ConfirmBackendQuiescentはbackend identityを取得してから隔離解除を保存する。取得失敗で物理処理フラグや隔離を消さない。
初期化のidentity取得は台帳lease取得前に行い、読込み失敗時はleaseを解放する。
既知のbackend_still_active、request_not_found、disabled、journal_unavailable等を維持し、例外を成功や停止完了へ読み替えない。
DisposeAsyncは従来どおり物理処理を待つ。例外を受けたホストは停止/失効通知/復旧の完了を仮定せず、状態確認と再試行を管理する。
host_operation_failedは受付・ジョブの許可リストへ追加しない。ホスト操作をSceneSessionのoperationとして公開しない。
SDK例外のスタックトレースや設定済みidentityをHTTP応答へ無条件にコピーしてよいという契約ではない。

## dev10の停止確認と保存失敗

ConfirmBackendQuiescentは解除後の記録を別に作り、台帳保存が成功してからメモリへ反映する。
処理中は同じサービスロックを保持し、保存完了まで元のPhysicalOutstandingと隔離を解除しない。
失敗時は従来どおりjournal_unavailableを返す。元の記録が後続のホスト操作や取消応答の書込みにも使われるため、
物理処理の未確認状態が台帳から消えず、サービス再起動と保持期限切れを経ても隔離が維持される。
停止確認の保存が成功した場合にだけフラグを解除し、readinessはunavailable / probe_requiredへ戻す。
稼働中のbackend_still_active、既存の例外許可リスト、結果・終端状態・OutputHashは維持する。
公開DTO、台帳v1形式、hash計算式は変更しない。台帳の手動削除や巻き戻しによる復旧を許可する変更ではない。

## 標準実装全体のコード

| 分類 | コード | 呼出側の扱い |
|---|---|---|
| API | capability_unsupported, operation_unsupported, unknown_argument | 能力・引数を修正。既存observerへ混ぜない |
| 認証・認可 | unauthenticated, permission_denied, action_not_authorized, target_not_authorized | 認証主体/grantをホストで確認。JSON自己申告で回避しない |
| 認可情報取得 | authorization_unavailable | 推論前・完了時・未完了要求の照会時の現在grant取得失敗。failedのまま保持。ホストの認可処理を確認し、同一IDの自動再実行はしない |
| 要求形式 | invalid_request, invalid_json, duplicate_field, invalid_identifier, duplicate_identifier, schema_unsupported, mode_unsupported | 入力修正 |
| 内容整合 | hash_mismatch, inconsistent_snapshot, actor_revision_mismatch, catalog_mismatch | 整合した新しいsnapshotを採取 |
| 期限・世代 | invalid_deadline, future_capture, observation_expired, observation_deadline, stale_context, context_changed, context_invalidated, newer_observation | 元の期限は延長せず、最新のepoch/版で撮り直す |
| freshness | clock_unverified, freshness_unverified_or_expired | 候補採用しない。ホスト時計較正を確認 |
| 冪等性 | request_id_conflict, request_not_found | ID/保持期限/主体を確認し二重実行しない |
| メディア | media_unsupported, media_unavailable, media_expired, invalid_media | PNG形式/所有者/参照/期限を確認 |
| 容量 | payload_too_large, input_limit, output_limit, media_too_large, media_capacity, media_busy, job_capacity, caller_capacity, session_capacity | 入力削減/待機。既存冪等性記録を追い出さない |
| 出力内容 | invalid_evidence, unknown_actor, unknown_target, invalid_unknown, invalid_region, duplicate_proposal, temporal_evidence_required | 候補を破棄。原因を評価セットで検証 |
| 出力プロトコル | invalid_backend_response, response_truncated, content_filtered, completion_incomplete, unexpected_tool_call, backend_response_too_large, model_revision_mismatch | 成功扱いしない。truncatedとfilteredを区別 |
| 接続・負荷 | endpoint_not_allowed, backend_request_rejected, backend_busy, backend_unavailable, backend_oom | 設定確認。自動fallbackしない。busyの目安1000ms、unavailable/oomは5000ms、保証ではない |
| 物理処理不確実 | backend_transport_unconfirmed, backend_failed_unconfirmed, backend_work_unconfirmed, backend_still_active | 新規推論を停止。終了確認まで物理枠を再利用しない |
| 稼働・運用 | disabled, not_probed, probe_required, probe_timed_out, probe_failed, backend_not_ready, inference_capacity | readonly probeと運用系の起動を分離 |
| 配置証跡 | deployment_evidence_invalid, deployment_mismatch, deployment_state_unknown, runtime_mismatch, model_mismatch | 証跡/期限/実配置/runtime APIを確認 |
| 配置の状態 | deployment_disabled, deployment_loading, deployment_warming_up, deployment_busy, deployment_degraded, deployment_unavailable | 短いprobe失敗で再起動しない |
| 台帳 | journal_invalid, journal_capacity, journal_unavailable | fail closed。台帳削除で重複実行を許さない |
| ホスト操作 | host_operation_failed | 独自コード・予期しない例外。操作完了と扱わず運用状態を確認 |
| 終了理由 | caller_canceled, service_stopping, runtime_restarted | 元の要求の終端状態を保持 |

ready は正常な readiness 理由。backendCancellation は not_requested / requested / unsupported / confirmed / unconfirmed。
API用 CancellationToken の OperationCanceledException/TaskCanceledException は例外として伝播し、成功に変換しない。
メディア登録・ホスト運用APIの失敗は上記のSceneException(Code)で返る。設定引数の固定ArgumentExceptionと取消は別扱い。
モデル本文・HTTPエラー本文を理由へ転写しない。

dev3の推論前再認可では、unauthenticated / permission_denied / actor_revision_mismatch /
action_not_authorized / target_not_authorized を rejected の理由として返す。
認可処理の例外、内部の取消、不正grant、未知の理由コードは authorization_unavailable に統一し、
failed とする。API呼出元の CancellationToken と認可アダプター内部の取消を混同しない。
再認可中にscene.cancelまたは元の期限が先に確定した場合、そのcanceled/timed_outを維持する。
これらは全て推論前の終端でありbackendCancellation=not_requested。照会のSucceeded=trueは解析成功を意味しない。

dev4は完了済み候補の権限不一致を検知すると、stale_context / freshnessVerified=false / proposals=[]を台帳へ保存する。
completedは解析履歴として維持する。scope/policy不一致の照会は従来どおりrequest_not_found、
固定したセッションCallerIdと現在grantのCallerIdの不一致はpermission_denied。
認可情報取得に失敗した照会・同一入力再送でも既存候補を失効させるが、受付エラーのコードと例外伝播は維持する。
保存不能ならjournal_unavailableを返し、候補返却・新規推論を停止する。台帳を削除して再実行しない。

dev5はモデル応答の検証後、結果確定前にもdev3と同じ再認可を行う。
不一致はrejected、取得障害はfailed / authorization_unavailableで、観測・候補を保存しない。
先に確定したcanceled / timed_out / supersededを上書きしない。
バックエンドの正常終了を確認した後はbackendCancellation=not_requestedのまま（既に要求した取消の記録は維持）。
完了済みの物理処理を認可障害だけで未確認GPU処理として隔離しない。台帳障害時の停止は従来どおり。

dev6は未完了要求の照会・同一入力再送で失効を検知した時点で同じrejected/failedを永続化する。
権限が復旧しても終端状態を維持し、遅い推論応答の観測・候補を保存しない。
ホスト専用RevokeRequestAuthorization(callerId, requestId)は未完了要求をrejected / permission_deniedにする。
完了済みならcompletedの履歴を維持し、候補を除きstale_contextとする。他の終端状態は上書きしない。
通知は対象なしならrequest_not_found、保存失敗ならjournal_unavailableをthrowする。後者を通知成功と扱わない。
この操作はCapabilityのoperationではなく、認可ホストが管理する受付済み要求への通知である。

## dev11: 受付・取消・完了時の台帳障害

新しい公開理由コードは追加しません。受付/予約/完了保存の失敗はjournal_unavailable、隔離中の受付/readinessはbackend_work_unconfirmedです。
取消・期限切れ・失効の終端状態を成功へ戻さず、保存前の完了候補を返しません。読み取り専用probeの進行中に隔離された場合もreadyと返しません。
recovery-requiredはサービス公開前から保持し、通常の再保存・再起動では消しません。失敗後はホストの停止確認の保存成功と正常終了が必要です。
補助記録除去に失敗した終了もjournal_unavailableとなり、書込ロックは解放します。旧版は補助記録を認識しないため、存在中のダウングレードは禁止です。

## dev12: 停止済みインスタンスと台帳所有権

停止後の有効な運用API/能力/メディア要求はdisabled。probeもdisabledとしdeploymentVerified=false、retryAfterMs=nullを返します。
停止後の内部台帳Load/Saveはjournal_unavailableで、例外本文やパスを公開しません。Close/Disposeの反復は新所有者へ作用しません。
停止開始確定後のDisposeAsyncは完了・失敗を共有し、遅い物理処理が残る間に二回目だけ成功を返しません。
停止準備で発生するホストタイマー例外は従来の秘匿・取消の扱いを保ち、所有権を保持して再試行可能とします。新しい理由コードは追加しません。

## dev13: readiness障害処理内の時計例外

失敗時刻の取得が通常例外/独自コードで失敗してもunavailable / probe_failedとし、承認・再試行目安・例外詳細を返しません。
時計のOperationCanceledException/TaskCanceledExceptionはCanceled状態の取消として伝播し、本文・内部例外・Data・内部トークンを除きます。
backend等の失敗確定後は時計を読む前に過去の承認を無効化し、時刻取得不能を新鮮な判定にしません。時計復旧後は正常probeで再確認します。
停止・隔離時はdisabled / backend_work_unconfirmedを優先し、台帳や復旧記録を変更しません。新しい理由コードは追加しません。
