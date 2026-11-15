# Initial Architecture Notes

## Starter layout

- `backend/OneCart.Api`: ASP.NET Core HTTP entry point.
- `backend/OneCart.Persistence`: relational context, catalog/admin persistence, and provider selection.
- `backend/OneCart.SqlServer.Migrations`: isolated experimental SQL Server migrations.
- `frontend`: React and TypeScript single-page application.
- `docs`: product and implementation documentation.

## Direction

The initial recommendation is a modular monolith: a single deployable backend with clear boundaries for catalog, checkout, orders, customers, administration, localization, currency, themes, installation, and persistence. Keep domain and application logic independent of HTTP and provider-specific APIs.

The frontend should communicate through explicit JSON API contracts. Keep public storefront routes and protected administration routes separate in navigation and authorization policy.

## Persistence

PostgreSQL is the confirmed first-release provider and the default when `Database:Provider` is omitted. SQL Server has a separate experimental migration assembly and can be selected for local development. A local integration test covers migration plus catalog write/read behavior, but SQL Server is not a supported release provider until installation, deployment, transaction/concurrency behavior, backup/restore guidance, and broader integration coverage are verified. MySQL, MongoDB, and hybrid configurations remain broader product goals, not current support claims. Keep provider-specific behavior behind the persistence boundary and do not add superficial provider switches.

The catalog uses Entity Framework Core migrations. `StoreProduct` represents the single storefront product; `ProductVariant` stores the selectable SKU, display name, price/currency code, active state, and stock quantity. `ProductAttributeGroup` and `ProductAttributeOption` represent required or optional shopper selections and non-negative price adjustments. `StoreProduct` also carries admin-configured flash-sale, share/WhatsApp, and storefront brand/copy settings. `ProductImage` metadata is relational; image bytes use the `IProductImageStorage` adapter, which currently stores random-keyed files under `App_Data/product-images` by default (`ProductMedia:RootPath` can set another local path). The API accepts JPEG, PNG, and WebP signatures up to 10 MB and exposes files publicly only when the product is published; admin previews require SystemAdmin. `ProductTrustItem` stores configurable service details. The public catalog query returns only the published product and active variants/options, and exposes availability without exposing exact stock counts. Attribute adjustments and scheduled sale prices are included in the storefront total. Signed cart quotes bind the add-time unit price and currency; COD checkout validates those quotes and stores order-line product/price snapshots.

Localization stores catalog and storefront dictionaries separately as JSON on the single product. Catalog Managers can update localized product, variant, and attribute labels; Store Managers can update brand, campaign, contact-message, and trust-item copy. The storefront accepts `?language=en|bn|es`, resolves each missing field to the English source value, and does not localize price, currency, inventory, or order snapshots. The frontend stores its language choice in browser local storage.

The catalog shape is one product with selectable variants. Variant option dimensions and compatibility rules remain open. COD checkout decrements inventory atomically when an order is placed and cancellation restores it; these behaviors are implemented from the confirmed requirements.

## Installer and deployment

Treat first installation and upgrades as explicit workflows. The installer must validate prerequisites and configuration, protect secrets, prevent repeated setup from weakening security, and support supported hosting targets. Exact installer and topology decisions remain open in the requirements document.

## HTTPS requirement

The API accepts requests only when ASP.NET Core sees an HTTPS scheme. The local launch profile binds to HTTPS at `https://localhost:7241`. Production deployments must use a valid TLS certificate. If TLS terminates at a reverse proxy, set `ForwardedHeaders:KnownProxies` to the proxy's IP address so the API can safely recognize the forwarded HTTPS scheme. Forwarded headers from untrusted clients must not be accepted.

## Administrator authentication

Administrator accounts use ASP.NET Core Identity with the configured relational provider and secure cookies. Mutating browser API calls require an anti-forgery request token. Credentialed CORS accepts only configured frontend origins (development permits the local Vite origins). Public registration is not mapped. The first System Admin is created by the one-time local bootstrap command, which prompts for the password; later account creation and role assignment require SystemAdmin authorization. New accounts must change their temporary password on first sign-in. SystemAdmin, CatalogManager, OrderManager, and StoreManager have separate server-enforced permissions. SystemAdmin controls the persisted optional/mandatory authenticator-MFA policy; enrollment uses Identity TOTP, and enabled MFA is challenged at sign-in. Anonymous email password-reset requests use Identity reset tokens and an SMTP sender; responses are generic and the endpoint has an IP-based rate limit.

## Current implementation boundary

The API exposes health and published catalog/image endpoints, role-scoped product editing and media management, storefront settings, signed cart quotes, guest COD checkout, role-scoped order administration, searchable per-order customer-contact lookup, and SystemAdmin-only user, role, MFA-policy, and audit APIs. Product, image, storefront, account/role, MFA-policy, order, and status changes create audit records. The frontend includes a configurable storefront gallery/header/trust details, role-aware admin navigation, optional authenticator enrollment and sign-in challenges, password reset, section-based catalog editing, storefront settings, media management, session-only shopping cart, guest checkout, customer contact lookup, user management, order management, and recent audit history. New COD orders start Pending, show an order number, and atomically reduce stock; cancellation restores stock once. Current order transitions are Pending to Processing or Cancelled, and Processing to Completed or Cancelled. The admin order view summarizes all recorded order counts and totals by status and saved currency, keeping currencies separate. Customers use guest checkout only; contact entries remain attached to individual orders, with no customer sign-in, profile, or cross-order merging. The owner confirmed CatalogManager catalog/media access, OrderManager order/contact access, StoreManager storefront configuration, SystemAdmin all permissions, email reset links, and SystemAdmin control of optional or mandatory authenticator-app MFA. Email delivery and certain account recovery policies remain to be finalized. General store settings and installer workflows are also pending. Address/phone validation and currency conversion policy remain open.
