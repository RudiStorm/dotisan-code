# Integration readiness

Optional integrations are deliberately labeled so generated code is not mistaken
for a production-complete service. Select a capability only after confirming the
operational work in its row.

| Capability | Required behavior before production-foundation |
| --- | --- |
| Imports | bounded upload, durable status, real processing handler, format validation, and tenant ownership |
| Storage | streaming limits, ownership policy, preserved content type, and a durable provider |
| Webhooks | background dispatch, timeout, destination allowlist, fresh replay signatures, and observable terminal failures |
| Notifications | authenticated user mapping, authorization, persistence, pagination, and delivery monitoring |
| Caching | distributed implementation guidance, namespaced keys, and an invalidation example |

Current generated capabilities are development-adapter unless explicitly
stated otherwise. Local and S3-compatible storage are example-only because
durable metadata/ownership and a provider adapter are not generated. Replace or extend the generated implementation, review
tenant boundaries, configure secret storage, and add operational monitoring
before promoting any capability to production.

See the detailed generated behavior matrix in integrations-readiness.md for the
current implementation notes and follow-up work.
