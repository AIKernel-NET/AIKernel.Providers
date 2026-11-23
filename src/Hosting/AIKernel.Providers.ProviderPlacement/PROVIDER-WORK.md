# Provider Work Pull v1

`HttpProviderWorkPullClient` uses the configured AIKernel `IConnectionClient` and central server ID. That connection resolves the central address and sends its Bearer credential. A worker receives only jobs that the central API assigns to its authenticated identity.

| Request | Body | Success |
| --- | --- | --- |
| `POST /api/v1/provider-work/claim` | JSON `node_id` | 200 with a work item in snake case, or 204 when none is available |
| `POST /api/v1/provider-work/input` | JSON `job_id` and `claim_id` | 200 with input bytes |
| `POST /api/v1/provider-work/complete` | Multipart `metadata` JSON with `claim` and `result`, plus `payload` bytes | 204 after verification and commit |
| `POST /api/v1/provider-work/fail` | JSON `claim` and `error_code` | 204 after recording the failure |

The central host owns persistent job state, authentication, claim ownership, and store quotas. The receiver must verify each payload reference against its byte count and SHA-256 before exposing it. `ProviderPayloadVerifier` supports streaming verification into temporary storage. Discard staged data on any failure, and commit only after the transfer succeeds. Keep claim IDs and credentials out of URLs and logs.
