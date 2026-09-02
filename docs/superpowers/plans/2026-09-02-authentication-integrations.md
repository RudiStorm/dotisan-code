# Authentication and Application Integrations Plan

## Goal

Complete the authenticated generated-app experience without generating misleading UI. When authentication is enabled, generated projects will include account recovery, authorization-management screens, protected authorization APIs, and an explicit email integration seam.

## Tasks

- [ ] Add a test-first email integration boundary with a development console implementation and production replacement point.
- [ ] Add password-reset request and confirmation endpoints with anti-enumeration responses and generated contract metadata.
- [ ] Add authorization-management APIs protected by a dedicated permission and backed by standard Identity users, roles, and role claims.
- [ ] Generate Vue account-recovery and authorization screens, routes, navigation, and permission-aware API calls only for authenticated projects.
- [ ] Verify authenticated and unauthenticated generated projects, update documentation, and run the release checks.

## Explicitly deferred

TOTP MFA, recovery codes, external OAuth providers, session/device management, and vendor-specific SMTP SDKs remain separate slices. The email interface is intentionally the integration boundary for those additions.
