# Authentication Security and Integrations Design

## Goal

Extend authenticated generated applications with verified email lifecycle, MFA, recovery, session/device controls, provider-neutral external login, and replaceable mail providers without coupling the generated source to one vendor.

## Scope

Authenticated projects only. Unauthenticated projects continue to omit Identity, account screens, mail configuration, and authorization screens.

## Architecture

Generated applications use ordinary ASP.NET Core Identity and explicit application services. `IEmailProvider` is the mail boundary; built-in adapters are console, Mailpit, and SMTP, while vendor-specific adapters can be added in application source. `IExternalLoginProvider` is the external-login boundary; provider registration, challenge, callback, linking, and unlinking remain explicit and configuration-driven.

Email confirmation and password reset use Identity token providers and anti-enumeration responses. MFA uses Identity's authenticator-key primitives with TOTP and one-time recovery codes stored as hashes. Session management uses an application-owned session record with device metadata, revocation, expiry, and security-stamp invalidation.

## Required flows

- Registration creates an unconfirmed account, sends a confirmation email, and does not treat email delivery as proof of confirmation.
- Confirmation accepts a single-use Identity token and reports an invalid/expired token without exposing account details.
- Password reset request always returns the same accepted response; confirmation requires a valid token and password policy.
- TOTP enrollment requires an authenticated user, an authenticator key, a verification code, and generated recovery codes shown once.
- MFA challenge is required after password authentication when enabled; recovery codes are one-time use.
- Sessions show current and other devices, support revoking one session or all other sessions, and invalidate cookies through the security stamp.
- External login uses a provider-neutral challenge/callback contract and never stores provider secrets in source control.
- Mail provider selection is environment configuration; Mailpit is Development-only, SMTP is production-safe, and custom adapters can replace either.

## Vue screens

Authenticated projects generate confirmation-result, MFA enrollment/challenge/recovery-code, sessions/devices, and connected-providers screens. Routes are protected by authentication; management actions are protected by API authorization. Unauthenticated projects generate none of these screens.

## Security requirements

- Never reveal whether an email exists during recovery or confirmation requests.
- Store recovery codes only as hashes and invalidate each code after use.
- Do not log tokens, passwords, authenticator keys, provider secrets, or recovery codes.
- Use antiforgery protection for cookie-authenticated state-changing requests.
- Reject Mailpit configuration outside Development.
- Keep external-provider client secrets in environment/secret configuration.

## Verification

Each stage adds generator assertions and generated API tests. CI runs a Docker-backed Mailpit reset-email smoke test. MFA, sessions, confirmation, and external-login tests cover anonymous access, invalid/expired tokens, replay, revocation, and provider configuration boundaries.
