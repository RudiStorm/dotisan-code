# Optional integration readiness

Generated integrations are opt-in. Select only capabilities whose operational requirements you can satisfy, and replace example implementations before production use.

| Capability | Generated behavior | Readiness |
| --- | --- | --- |
| Notifications | EF-backed records and SignalR delivery with authorization | Ready as a baseline; add retention, pagination, and delivery monitoring for scale |
| Local storage | Validated keys, streamed writes, configurable size limit | Development/single-node baseline; use a reviewed durable adapter for multi-node deployments |
| S3-compatible storage | No adapter is generated | Integrate and test an approved SDK explicitly |
| Imports/exports | CSV/JSON validation, bounded upload, EF-backed status, queued message, tenant-scoped reads when tenancy is enabled | Development-adapter; add durable payload storage, domain processing, retention, and monitoring before production |
| Webhooks | HMAC delivery records, URL validation, timeout, retries, replay authorization, fresh replay signatures | Development-adapter; move dispatch/retries to durable background execution and add destination policy, secret rotation, and observability before production |
| Memory cache | Process-local `IMemoryCache` implementation | Development/single-node baseline; use a distributed provider when multiple instances share state |

The CLI and generated README should describe these capabilities as baselines or examples, not as turnkey managed services. Production teams remain responsible for provider credentials, secret storage, retries, retention, tenant isolation, and operational monitoring.
