# OneCart Commerce — Product Requirements

**Status:** Initial baseline for implementation  
**Source:** Product planning conversation linked in the project kickoff  
**Product name:** OneCart Commerce (working name)  
**Last captured:** 2026-11-15

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
- Product reviews and ratings must be displayable on the product page. Reviews are admin-managed only; public customer review submission is disabled.
- The storefront should use a polished, responsive single-product presentation: clear brand/header, product imagery, concise headline and description, social proof, a prominent purchase action, and configurable trust/service details. Use the attached skincare-store mockup as visual direction; do not add its multi-category navigation to the single-product catalog.
- **Confirmed visual direction:** Use the owner-provided “OGX Coconut Curls Shampoo Landing Page” reference as inspiration for a richer, polished product landing page. Include a slim trust/contact strip; branded header; large product gallery with thumbnails; product title, concise benefits, admin-managed ratings/reviews, selectable variants, flash-sale countdown and old/new prices when active, and a prominent purchase action; followed by product benefits, product details, usage instructions, trust highlights, and the existing footer. Keep every section responsive and product-specific content configurable through admin. Treat the reference as layout and styling inspiration only: do not copy its brand, product claims, prices, ratings, origin, contact details, or other sample content into Pretty Shop.
- The landing-page implementation must provide matching admin configuration and storefront rendering for product benefits, usage steps, trust highlights, reviews, and theme settings. Theme controls must let the owner choose a coordinated palette—including white or other background colors with complementary text, accent, and button colors—and preview the resulting storefront. Keep layout and styling within approved UI controls; do not require custom CSS. Customer reviews are not submitted publicly; an admin enters, translates, publishes, orders, or removes them.
- Administrators must be able to configure the storefront presentation and product-page content, including branding, headline/copy, product media, and trust/service details, without editing source code.
- Provide an administrator-managed About Us section, with translated content where configured, and display it on the storefront with a navigation link when content exists.
- The storefront theme editor must preview the branded header and product image zone using the current brand/logo/theme choices and an uploaded product image when available.
- Product image and logo files must be stored on local project-host storage; cloud object storage is not required for this product.
- Initial storefront branding is “Pretty Shop” with the headline “Your trust, Your Love.” Show the headline beneath the shop name in the top bar only; use the product name as the main product-page heading.
- Keep the storefront product heading visually prominent with an admin-selected size from 26–42 px; product details and supporting text remain capped at 12–14 px, and the shop name and slogan stay compact in the top bar.
- Use a full-width storefront layout that adapts across desktop, tablet, and mobile screens, stacking product content and purchase controls when the available width is limited.
- Show the regular (old) price and sale (new) price together when a discount is active, with a clear discount indicator.
- Support administrator-configured flash sales with a start/end schedule and a visible countdown while the sale is active. The sale price and countdown must stop when the configured sale window ends.
- Discount and flash-sale settings must be manageable from the admin module, with an enable/disable control and a clear preview of the resulting customer-facing price.
- Provide an administrator-controlled product sharing action that uses the canonical product URL and the browser's native share sheet or a copy-link fallback.
- Provide a WhatsApp contact action for customer questions. Administrators must configure whether it is enabled, the store contact number, and the prefilled message; the storefront must not expose a contact action with a missing or invalid number.
- Reference for the configurable product and product page experience: [nopCommerce “Build your own computer” demo](https://demo.nopcommerce.com/build-your-own-computer), which presents required Processor, RAM, HDD, and OS selections, optional Software add-ons, price adjustments, quantity, and cart/wishlist/compare/email-a-friend actions.

### Orders and payments

- Support Cash on Delivery (COD) as the confirmed payment method.
- Allow guest checkout with customer name, phone number, and delivery address.
- Customers do not sign in or have customer profiles. Contact details belong to each guest order and are not merged across orders.
- Save a new order in Pending status and show its order number after placement. Administrators may update or cancel an order.
- Decrease inventory atomically when an order is placed, restore it if an administrator cancels the order, and do not impose an additional per-order quantity cap.
- Retain the signed unit-price and currency quote captured when each cart line is added. Later store changes do not rewrite existing cart lines; each shopping period stays in one currency, and mixed-currency carts/orders are rejected. Checkout must not accept client-edited prices and must preserve order-line price/product snapshots.
- Capture and manage customer orders through the admin experience.
- Track order status and provide the administrator with order and sales visibility.
- Do not assume online payment gateways are in scope.

### Administration

- Provide an administrator area for managing orders, customers, inventory, product content, store settings, and sales information.
- Organize the administrator workspace with a persistent left navigation on desktop and a mobile menu on smaller screens, grouped around the implemented Overview, Catalog, Orders, Accounts/Roles, and Audit sections.
- Include authentication and role-based authorization appropriate to the actions available.
- Use built-in email/password administrator accounts. Only a System Admin may create administrator accounts and assign roles; public registration is disabled.
- Additional roles have distinct, limited responsibilities: CatalogManager manages product catalog and media; OrderManager manages orders and order contact records; StoreManager manages storefront configuration; SystemAdmin can perform all administrative actions, including account and role management.
- Provide email password-reset links for administrator account recovery.
- Let the System Admin configure authenticator-app MFA as optional or mandatory for all administrator accounts; MFA is optional by default.
- Require new admin users to change the temporary password at first sign-in.
- Create the first System Admin through a one-time local bootstrap command that prompts for the password, rather than shipping a default credential.
- Maintain audit records for important administrative changes.

### Languages, currency, and theme

- Support English (`en`), Bangla (`bn`), and Spanish (`es`) storefront languages. English is the default and fallback; storefront interface labels ship in all three languages, and administrators maintain translated product and storefront content.
- The administrator selects one active currency for the whole store. A cart line retains both its numeric unit price and currency from when it was added. COD orders cannot mix currencies and currency conversion is not performed.
- After initial product/currency setup, changing the store currency is locked until the owner defines the price-conversion rule. Existing order currency and totals remain historical snapshots.
- Provide administrator UI controls for storefront colors and constrained typography sizes, with a live preview. Product headings are limited to 26–42 px and details to 12–14 px. Admins cannot enter custom CSS or arbitrary style rules; layout remains application-controlled.
- **Open decision:** Currency catalog and rounding rules; the owner-provided conversion rule for changing existing product prices when the store currency changes.

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

- Variant option structure (for example size/color).
- Product attribute types and presentation controls (for example dropdown, radio, checkbox), whether multiple choices may be selected per attribute, and how required/optional rules are configured.
- Whether attribute combinations need compatibility rules, combination-specific SKUs/stock, or only independent price adjustments.
- Whether product-page wishlist, compare, email-a-friend, and reviews are included in the first release, and their account, privacy, moderation, and notification rules.
- How attribute price adjustments interact with variants, discounts, taxes, and currency conversion.
- Discount configuration rules: whether to retain both percentage and fixed-amount modes, whether promotions apply to all variants or selected ones, sale-price validation, currency-specific rounding, and how a fixed amount applies when variants use different currencies.
- Flash-sale schedule input timezone, boundary behavior, and whether administrators can schedule multiple or overlapping promotions.
- Product media upload formats and size limits, image processing, and replacement/retention rules. Local project-host storage is confirmed; cloud storage is not required.
- Storefront branding/theme controls, configurable trust/service details, and which content blocks may be hidden or reordered.
- Additional social networks beyond the native share sheet/copy-link action, and WhatsApp contact display location, phone-number format/validation, and default message content.
- Guest checkout with customer name, phone number, and delivery address is confirmed. New COD orders start Pending and display an order number. Admins can update or cancel orders. Placement atomically reduces stock; cancellation restores it; there is no additional per-order quantity cap. Cart lines preserve the signed add-time unit price and currency, and mixed-currency carts/orders are rejected. **Open decision:** Address field structure and validation, phone-number validation, and the exact allowed admin status transitions.
- Email delivery configuration and password-reset link expiry/rate-limit settings.
- Authenticator-app recovery-code policy and MFA reset procedure.
- Which additional database providers, beyond the confirmed first-release PostgreSQL provider, are required for release.
- Whether “hybrid” means multiple providers used together, relational plus document storage, or another deployment pattern.
- How the owner-provided conversion rule will translate existing product prices when the admin changes the one active store currency. Historical order totals and their currencies remain unchanged.
- Installer and upgrade strategy for IIS, Linux, and Docker.
- Certificate provisioning, renewal, and trusted reverse-proxy addresses for supported hosting environments.
- License activation behavior, offline installations, and local resale obligations.
- Marketplace and direct-sale packaging, support, and update commitments.

## 7. Current implementation baseline

- Repository contains an ASP.NET Core 10 API and a React/TypeScript storefront and admin application.
- The catalog model is one product with selectable variants, persisted in PostgreSQL for the first release.
- SQL Server has an experimental provider configuration and isolated initial migration set for local evaluation; it is not included in the supported-provider claim.
- The public API can read the single published product and its active variants over HTTPS. Signed cart quotes and guest COD checkout are implemented: orders start Pending, persist price/product snapshots, show a generated order number, and atomically reduce inventory. Authorized admins can review orders, move Pending orders to Processing or Cancelled, move Processing orders to Completed or Cancelled, and cancellation restores stock exactly once. These status transitions are current implementation behavior; broader transition policy remains an open product decision.
- SystemAdmin and CatalogManager can edit product content, variants, attributes, and product media; store settings and brand logo management are separated behind StoreManager access. Catalog saves by CatalogManager cannot overwrite storefront-only settings, and catalog media access excludes the brand logo. OrderManager can review orders, update their status, view sales summaries, and search customer contacts captured per order. SystemAdmin retains account/role and audit access. Administrative changes create audit records.
- The storefront displays a published product and selectable variants/attributes, updates the displayed total for option price adjustments, and supports a session-only cart with quantity editing and estimated subtotals. Guest COD checkout captures name, phone, and address; shows a Pending order number; and decrements stock atomically. Admin order cancellation restores stock.
- The System Admin panel can configure percentage/fixed flash-sale discounts and schedules, sharing, and WhatsApp contact details. The storefront shows regular/sale prices only during the sale window, with a countdown while active, and provides share and WhatsApp contact actions.
- The System Admin order view provides all-time order counts and order totals grouped by current status and each order's saved currency; it does not combine currencies or redefine historical order values.
- System Admins can search and paginate customer contact details captured with orders. Each result is a separate order contact; customer profiles and cross-order merging are out of scope.
- The store uses one administrator-selected currency across product variants, cart totals, and orders; mixed-currency carts/orders are rejected. A signed quote locks each cart line's numeric unit price and currency at add time. Currency changes are locked after product setup until the owner defines the conversion rule; existing orders retain their historical currency and totals.
- System Admin can upload and manage up to eight product images plus one brand logo, edit image alternative text/order, configure the brand name and product-page headline/subtitle, and manage up to eight trust/service details. Image bytes are stored locally, as confirmed; the current implementation uses `backend/OneCart.Api/App_Data/product-images` by default and allows a local path through `ProductMedia:RootPath`. JPEG/PNG/WebP up to 10 MB are current implementation defaults; format, size, processing, and replacement/retention policies remain open. Image transformations/CDN storage are not implemented.
- The sale schedule is entered in the administrator's local time and stored in UTC. Percentage discounts currently round the displayed sale price to four decimal places and apply to the selected variant's base price; attribute add-on prices are then added at their configured amounts. Confirmation of currency-specific rounding and attribute-discount interaction remains open.
- Only System Admin accounts can create users and assign roles through the admin panel. SystemAdmin has all permissions; other role scopes are described above.
- Admin account recovery uses email password-reset links sent through configured SMTP; reset requests are rate-limited and use generic responses. System Admins can configure authenticator-app MFA as optional or mandatory (optional by default); enrolled users verify a code at sign-in. A System Admin can reset another account's authenticator enrollment. Email delivery, reset-link expiry/rate-limit policy, MFA recovery codes, and recovery for a locked-out System Admin remain open operational decisions.
- Product scope and confirmed technology direction are documented here.
- Unresolved requirements remain visible and are not invented by implementation.
- Starter documentation explains how to run the two applications locally.
