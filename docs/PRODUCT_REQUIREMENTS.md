# OneCart Commerce — Product Requirements

**Status:** Initial baseline for implementation  
**Source:** Product planning conversation linked in the project kickoff  
**Product name:** OneCart Commerce (working name)  
**Last captured:** 2026-11-14

This document records requirements that were stated in the planning conversation. Implementation suggestions and unknowns are labeled separately; they are not silently treated as confirmed requirements.

## 1. Product goal

Build a professionally engineered, maintainable commerce application for selling a single product through an owned storefront. It should be usable by small businesses, installable without editing source code, adaptable to different hosting environments, and suitable for commercial distribution through a marketplace where eligible or direct/local sales.

The project should be developed with human review and release quality in mind. Codex may assist with implementation, tests, refactoring, and documentation; generated changes still require review and verification.

## 2. Confirmed product requirements

### Storefront and catalog

- Support a single-product commerce storefront.
- Present product information and a clear purchase path on desktop and mobile.
- Keep product content and store settings manageable without editing application source.
- The catalog is one product with selectable variants. Each variant can have its own SKU, display name/options, price, active state, and stock quantity.
- A product may have multiple configurable attributes in addition to its variant selection. The product page must present each attribute with its available choices, including required selections and optional add-ons.
- Attribute choices may add a price adjustment. The page must show the current total price and update it when the shopper changes selections.
- The product detail page must show the product title, images, SKU, availability/shipping information when configured, description, price, quantity control, and a clear add-to-cart action.
- Product page actions should include wishlist, compare, and email/share-with-a-friend controls, subject to the policy decisions below.
- Product reviews and ratings should be displayable on the product page, with review submission governed by the store's configured review policy.
- Reference for the configurable product and product page experience: [nopCommerce “Build your own computer” demo](https://demo.nopcommerce.com/build-your-own-computer), which presents required Processor, RAM, HDD, and OS selections, optional Software add-ons, price adjustments, quantity, and cart/wishlist/compare/email-a-friend actions.

### Orders and payments

- Support Cash on Delivery (COD) as the confirmed payment method.
- Capture and manage customer orders through the admin experience.
- Track order status and provide the administrator with order and sales visibility.
- Do not assume online payment gateways are in scope.

### Administration

- Provide an administrator area for managing orders, customers, inventory, product content, store settings, and sales information.
- Include authentication and role-based authorization appropriate to the actions available.
- Use built-in email/password administrator accounts. Only a System Admin may create administrator accounts and assign roles; public registration is disabled.
- Require new admin users to change the temporary password at first sign-in.
- Create the first System Admin through a one-time local bootstrap command that prompts for the password, rather than shipping a default credential.
- Maintain audit records for important administrative changes.

### Languages, currency, and theme

- Support multiple storefront languages managed through administration.
- Support multiple currencies with administrator-configured exchange rates.
- Provide a dynamic theme system for colors, typography, layouts, and live preview.
- **Open decision:** Translation workflow, fallback language, currency conversion/rounding rules, and whether exchange rates are manually entered or imported.

### Installation and hosting

- Provide a guided web installer so a store owner can configure the application without editing source code.
- Target portable deployment across IIS, Linux, Docker, and compatible .NET hosting.
- Support configurable persistence rather than baking one database vendor into domain behavior.
- Require SSL/TLS for all public API traffic. The canonical API base URL must use HTTPS, and production certificates must be valid and trusted by clients.
- Plain HTTP requests to the API must not reach application endpoints. If a trusted reverse proxy terminates TLS, the app may rely on forwarded protocol information only from explicitly configured proxy addresses.
- Installation, upgrades, backups, and recovery must be documented.
- **Open decision:** Exact installer behavior, supported deployment topology, and minimum server requirements.

### Commercial quality

- Keep code organized, documented, maintainable, and reviewable.
- Provide installation, administrator, customization, and API documentation.
- Plan for unit, integration, end-to-end, and deployment verification.
- Consider commercial license activation as an optional mechanism, without blocking legitimate local installations.
- Target CodeCanyon or other software marketplaces only if the seller and product meet their current eligibility and review rules; retain direct/local distribution as an alternative.
- **Open decision:** License model, update policy, support period, marketplace packaging, and final distribution channels.

## 3. Confirmed technology direction

- **Backend:** C# and ASP.NET Core 10.
- **Frontend:** React with TypeScript.
- **First-release database:** PostgreSQL.
- **Persistence direction:** configurable relational, document, or hybrid database support.
- **Named database targets from planning:** SQL Server, PostgreSQL, MySQL, and MongoDB.
- **Architecture quality:** SOLID principles and maintainable separation of concerns.

Named database targets are goals, not a claim that provider implementations already exist. Each provider needs a documented support matrix and verification before it is advertised as supported.

## 4. Quality and security requirements

The finished product is expected to include:

- Authentication, authorization, and secure session handling.
- Validation at API boundaries and safe handling of untrusted input.
- OWASP-aligned application security practices.
- Audit logging for sensitive administrative actions.
- Automated unit, integration, end-to-end, and deployment checks appropriate to the release.
- Clear installation, operations, backup, restore, and troubleshooting documentation.
- Responsive, accessible storefront and administration experiences.
- No secrets or real customer data in source control.

## 5. Suggested implementation shape

These are recommendations for a maintainable first release, not finalized product approvals:

1. Build a modular monolith with explicit application/domain boundaries.
2. Start with one relational provider for a complete end-to-end vertical slice before adding more providers.
3. Keep storefront and admin APIs versionable and document their contracts.
4. Make COD order placement and order management the first commerce workflow.
5. Treat the installer, theme editor, language management, currency handling, and optional licensing as later milestones after core order flows are reliable.

## 6. Open decisions to resolve before corresponding implementation

- Variant option structure (for example size/color), quantity limits, and inventory decrement/reservation rules.
- Product attribute types and presentation controls (for example dropdown, radio, checkbox), whether multiple choices may be selected per attribute, and how required/optional rules are configured.
- Whether attribute combinations need compatibility rules, combination-specific SKUs/stock, or only independent price adjustments.
- Whether product-page wishlist, compare, email-a-friend, and reviews are included in the first release, and their account, privacy, moderation, and notification rules.
- How attribute price adjustments interact with variants, discounts, taxes, currency conversion, and order-price snapshots.
- Customer checkout fields, address format, guest checkout, order confirmation, and cancellation policy.
- Admin roles, account recovery, and optional administrator MFA.
- Detailed permission mapping for the initial `SystemAdmin`, `CatalogManager`, `OrderManager`, and `StoreManager` roles; current role names are built-in starting defaults.
- Which additional database providers, beyond the confirmed first-release PostgreSQL provider, are required for release.
- Whether “hybrid” means multiple providers used together, relational plus document storage, or another deployment pattern.
- Supported languages and translation ownership.
- Store base currency, display currency, conversion source, rounding, and historical order-price rules.
- Theme customization boundaries and whether custom CSS is allowed.
- Installer and upgrade strategy for IIS, Linux, and Docker.
- Certificate provisioning, renewal, and trusted reverse-proxy addresses for supported hosting environments.
- License activation behavior, offline installations, and local resale obligations.
- Marketplace and direct-sale packaging, support, and update commitments.

## 7. Current implementation baseline

- Repository contains an ASP.NET Core 10 API project and a React/TypeScript frontend shell.
- The catalog model is one product with selectable variants, persisted in PostgreSQL for the first release.
- SQL Server has an experimental provider configuration and isolated initial migration set for local evaluation; it is not included in the supported-provider claim.
- The public API can read the single published product and its active variants over HTTPS.
- The System Admin panel can edit product content and variants; catalog edits create before/after audit records. Other role permissions remain open.
- The storefront displays a published product and selectable variants. COD order submission remains pending the checkout policy decisions.
- Only System Admin accounts can create users and assign roles through the admin panel.
- Product scope and confirmed technology direction are documented here.
- Unresolved requirements remain visible and are not invented by implementation.
- Starter documentation explains how to run the two applications locally.
