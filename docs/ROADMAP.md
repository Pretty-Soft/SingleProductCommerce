# OneCart Commerce — Implementation Roadmap

This roadmap sequences work using confirmed product requirements. It contains no delivery dates. Open product choices remain gates; recommendations are not approvals.

## Stage 0 — Repository starter (complete)

- ASP.NET Core 10 API shell with HTTPS-only request enforcement.
- React and TypeScript storefront shell.
- Product requirements, architecture notes, and Codex instructions.
- Local setup instructions for the API and storefront.

## Stage 1 — Resolve core checkout decisions (complete; some policies remain open)

Confirmed: the catalog is one product with selectable variants, and PostgreSQL is the first-release database provider.

Still open for owner decision:

- Address structure/validation, phone-number validation, and exact admin status transitions. Guest checkout with customer name, phone, and delivery address is confirmed; orders start Pending, display an order number, and admins can update or cancel them.
- Variant option dimensions (such as size/color). Stock will be decremented atomically when an order is placed and restored when an admin cancels it; no extra per-order quantity cap is required.
- Currency conversion rule for changing existing product prices. One active currency for the whole store is confirmed, cart lines retain numeric price and currency from add time, mixed-currency carts/orders are rejected, and historical order totals remain unchanged.

The core order workflow proceeds with the confirmed rules. Remaining policies stay explicit until the owner decides them.

## Stage 2 — Commerce vertical slice (complete)

Build a working path through product setup, public product display, COD checkout, order creation, and order status management. Keep API contracts explicit and versionable. Validate customer input and preserve the price and product details recorded when the order was placed. Catalog persistence and a public read contract are the first increment; secure product editing and order placement depend on the approved admin authentication and checkout rules.

Acceptance: an administrator can configure the approved product shape; a customer can submit a valid COD order over HTTPS; the order is persisted with a stable status and can be reviewed by an authorized administrator.

The catalog persistence, public read endpoint, protected product editor, variant management, configurable required/optional product attributes, scheduled percentage/fixed flash-sale discounts, live sale countdown, social sharing, WhatsApp contact, configurable branding, product image/logo uploads, trust/service content, and session-only shopping cart are implemented. Signed price quotes and guest COD checkout create Pending orders, capture order-line snapshots, decrement inventory atomically, and show an order number. System Admins can review orders and change their status; cancellation restores stock. Address/phone validation, exact status-transition policy, and currency conversion policy remain open.

## Stage 3 — Store administration and security (complete; operational setup remains)

Built-in email/password accounts, secure cookies, anti-forgery protection, lockout, first-System-Admin bootstrap, System Admin-only account and role management, recent audit history, and audit records are implemented. Catalog variant stock is editable, and order review/status management is implemented. The order view summarizes counts and totals by current status and saved currency. System Admins can search paginated customer contact records captured per order. Customers use guest checkout with no sign-in or profile; contact details stay with each order. CatalogManager has catalog/media access, OrderManager has order/contact access, StoreManager has storefront settings/media access, and SystemAdmin has all access. Email password reset and configurable optional/mandatory authenticator MFA are implemented; SMTP still needs deployment configuration. The System Admin can reset another administrator's authenticator enrollment. General language/theme settings and installation remain later roadmap stages. Reset-token expiry/rate-limit policy and recovery for a locked-out System Admin remain open.

Acceptance: protected operations enforce authorization server-side, validate inputs, and produce audit records where required.

## Stage 4 — Localization and presentation settings (localization content slice complete)

The storefront language selector supports English, Bangla, and Spanish, remembers the visitor's selection, and loads localized product/storefront text from the API. English is the default and per-field fallback. Administrators can edit translations for product names/descriptions, variants, attribute groups/options, store branding, sale labels, WhatsApp copy, and trust/service details. UI-language strings ship with the application. Currency conversion/configuration and theme editing/preview remain future work and retain their open decisions.

Acceptance: storefront UI and configured content can be presented in the three supported languages. Currency conversion and theme acceptance remain pending.

## Stage 5 — Installation and supported hosting

Design the guided installer and upgrade path, then document deployment, TLS certificate provisioning and renewal, trusted proxy configuration, backups, restores, and minimum server requirements for the approved IIS, Linux, and Docker targets.

Acceptance: a clean installation and a supported upgrade can be completed from documented steps without editing application source; public API traffic requires trusted HTTPS.

## Stage 6 — Additional persistence providers

An experimental SQL Server provider and isolated initial migration set exist for local evaluation. A local integration test covers schema migration and catalog write/read behavior. Keep PostgreSQL as the default. Complete provider-specific installation, deployment, concurrency, backup/restore, and broader integration verification before advertising SQL Server support. Add any other required provider only after the first complete workflow establishes the persistence contract.

Acceptance: the published support matrix matches verified provider capabilities. “Hybrid” remains undefined until the owner confirms its intended meaning.

## Stage 7 — Commercial release readiness

Automated CI now runs the API workflow tests, persistence tests, storefront tests, and production frontend build for pushes and pull requests. Complete end-to-end, security, installation, upgrade, and deployment verification. Prepare administrator, installation, customization, and API documentation; packaging; licensing behavior if approved; support/update policy; and target-market review. SQL Server integration tests remain opt-in until CI has a disposable SQL Server service configured.

Acceptance: release artifacts can be installed and operated using the supplied documentation, with no placeholder user-facing or security-critical flows.
