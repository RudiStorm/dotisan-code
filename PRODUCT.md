# Dotisan portal product baseline

Dotisan generates a working application portal, not a finished product for a specific business domain. The baseline provides authenticated account flows, health/status visibility, navigation, and pages that demonstrate the generated integrations when they are selected.

Generated applications should replace domain copy, navigation labels, information architecture, and workflows with product-specific decisions. Keep the generated API client as the contract boundary, but do not treat placeholder pages or sample jobs as business requirements.

## Product principles

- Make the next useful action obvious.
- Keep authentication and destructive actions explicit and recoverable.
- Surface pending, success, empty, and failure states.
- Preserve server-side authorization and validation as the source of truth.
- Prefer progressive disclosure over exposing every optional integration at once.

