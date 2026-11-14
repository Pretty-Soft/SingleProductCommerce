# OneCart Commerce — Implementation Roadmap

This roadmap sequences work using confirmed product requirements. It contains no delivery dates. Open product choices remain gates; recommendations are not approvals.

## Stage 0 — Repository starter (complete)

- ASP.NET Core 10 API shell with HTTPS-only request enforcement.
- React and TypeScript storefront shell.
- Product requirements, architecture notes, and Codex instructions.
- Local setup instructions for the API and storefront.

## Stage 1 — Resolve remaining checkout decisions (in progress)

Confirmed: the catalog is one product with selectable variants, and PostgreSQL is the first-release database provider.

Still confirm these before implementing checkout fields and order behavior:

- Checkout customer/address fields, guest checkout, order confirmation, and cancellation behavior.
- Variant option dimensions (such as size/color), quantity limits, and inventory decrement/reservation rules.
- Initial store language and base/display currency rules.

The catalog schema and read API can now be implemented without guessing checkout behavior. Remaining choices can be confirmed before their corresponding features are implemented.

## Stage 2 — Commerce vertical slice (catalog setup in progress)

Build a working path through product setup, public product display, COD checkout, order creation, and order status management. Keep API contracts explicit and versionable. Validate customer input and preserve the price and product details recorded when the order was placed. Catalog persistence and a public read contract are the first increment; secure product editing and order placement depend on the approved admin authentication and checkout rules.

Acceptance: an administrator can configure the approved product shape; a customer can submit a valid COD order over HTTPS; the order is persisted with a stable status and can be reviewed by an authorized administrator.

The catalog persistence, public read endpoint, protected product editor, variant management, configurable required/optional product attributes, scheduled percentage/fixed flash-sale discounts, live sale countdown, social sharing, WhatsApp contact, configurable branding, product image/logo uploads, and trust/service content are implemented. The endpoint returns `404` until a published product with an active variant exists. The editor is temporarily restricted to System Admin while detailed role permissions remain open. Checkout remains gated on the open customer/address and order policy decisions.

## Stage 3 — Store administration and security (user management in progress)

Use built-in email/password accounts with secure cookies, anti-forgery protection, and lockout. Bootstrap the first System Admin from a local command; only System Admins can create accounts and assign the initial roles. User management, recent audit history, and audit records for account and product changes are implemented. Then add store settings, order and customer views, and inventory controls. Define account recovery and MFA behavior before implementing those flows.

Acceptance: protected operations enforce authorization server-side, validate inputs, and produce audit records where required.

## Stage 4 — Localization and presentation settings

Implement administrator-managed languages and translations, currency configuration and conversion rules, theme editing, and preview. Follow the owner-approved fallback language, rounding, and custom styling policies.

Acceptance: storefront content can be presented in configured languages and currencies; theme settings preview and persist without source edits.

## Stage 5 — Installation and supported hosting

Design the guided installer and upgrade path, then document deployment, TLS certificate provisioning and renewal, trusted proxy configuration, backups, restores, and minimum server requirements for the approved IIS, Linux, and Docker targets.

Acceptance: a clean installation and a supported upgrade can be completed from documented steps without editing application source; public API traffic requires trusted HTTPS.

## Stage 6 — Additional persistence providers

An experimental SQL Server provider and isolated initial migration set exist for local evaluation. A local integration test covers schema migration and catalog write/read behavior. Keep PostgreSQL as the default. Complete provider-specific installation, deployment, concurrency, backup/restore, and broader integration verification before advertising SQL Server support. Add any other required provider only after the first complete workflow establishes the persistence contract.

Acceptance: the published support matrix matches verified provider capabilities. “Hybrid” remains undefined until the owner confirms its intended meaning.

## Stage 7 — Commercial release readiness

Complete unit, integration, end-to-end, security, installation, upgrade, and deployment verification. Prepare administrator, installation, customization, and API documentation; packaging; licensing behavior if approved; support/update policy; and target-market review.

Acceptance: release artifacts can be installed and operated using the supplied documentation, with no placeholder user-facing or security-critical flows.
