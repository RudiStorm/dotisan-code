# Authentication and Integration Completion TODO

Target patch release: v0.6.6

- [x] Add OAuth provider buttons, callback success/error handling, and link/unlink actions to the generated Vue portal.
- [x] Protect and validate OAuth state values.
- [x] Add generated external-provider contract/runtime coverage, including safe no-provider behavior and protected return URLs.
- [x] Add resend-confirmation endpoint and Vue state handling.
- [x] Add email-confirmation invalid/replay-safe behavior coverage.
- [x] Update session `LastSeenAt`, expiry filtering, and visible expiry details.
- [x] Confirm revoke-all behavior for the current device through security-stamp/session invalidation.
- [x] Add provider-neutral SendGrid and Mailgun adapter examples with configuration boundaries.
- [x] Verify generated authenticated and unauthenticated artifact conditionality.
- [x] Update release documentation and version references to v0.6.3.
- [x] Rebuild and verify the v0.6.6 NuGet package.
- [x] Run repository and generated API verification; Mailpit smoke coverage remains established from the prior integration pass.

The generated Vue install/build could not be rerun in this sandbox because the package-manager network step stalled; the generated API and repository test suite are green.
