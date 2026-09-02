# Mailpit TODO

- [x] Add a Docker-backed CI end-to-end test that requests a password reset and verifies the message appears in Mailpit. (Local execution requires Docker Desktop.)
- [x] Add a `dotisan mail` helper that opens the configured Mailpit inbox URL.
- [x] Add Mailpit availability and configuration checks to `dotisan doctor`.
- [x] Prevent Mailpit from starting for non-Development environments unless explicitly opted in.
- [x] Prevent Mailpit from being selected in production configuration.
- [x] Expand SMTP TLS, credential, and secret-management documentation.
- [x] Verify the generated Compose stack on a Docker-enabled CI runner.
