# Scene v1 状態・エラーコード一覧

更新: 2026-11-11 / AIKernel.Providers.Cosmos 0.1.4-dev6（未完了要求の失効永続化とホスト通知。理由コード追加なし）。
受付失敗は envelope.ErrorCode、受付後は result.ErrorCode、稼働状態は readiness.ReasonCode を確認する。
status は COSMOS_SCENE_P0.md と result.schema.json に従う。未知のコードを成功に読み替えない。
以下は標準実装のコード。独自バックエンド・認可アダプターも短い非機密コードだけを返すこと。

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
| 終了理由 | caller_canceled, service_stopping, runtime_restarted | 元の要求の終端状態を保持 |

ready は正常な readiness 理由。backendCancellation は not_requested / requested / unsupported / confirmed / unconfirmed。
API用 CancellationToken の OperationCanceledException/TaskCanceledException は例外として伝播し、成功に変換しない。
メディア登録・ホスト運用APIのエラーは SceneException(Code) で返る。モデル本文・HTTPエラー本文を理由へ転写しない。

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
