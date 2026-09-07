# Dotisan portal design baseline

The generated Vue portal uses a compact, accessible application shell with a responsive sidebar, consistent cards, form controls, status badges, and inline validation. This is a starting system for application teams, not a brand identity or a promise of visual uniqueness.

## Customization guidance

- Replace colors, typography, spacing, and imagery with the product design system.
- Preserve visible focus states, semantic labels, keyboard navigation, and sufficient contrast.
- Keep layouts usable from narrow mobile widths through large desktop screens.
- Use `role="alert"` or live regions for asynchronous errors and status changes.
- Confirm destructive actions and provide a clear recovery path where possible.

The generated `src/<Name>.Web/src` files are intentionally local and editable. Applications should customize them rather than modifying the Dotisan CLI templates after generation.

