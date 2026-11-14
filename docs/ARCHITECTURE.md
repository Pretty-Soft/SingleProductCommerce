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

The initial catalog uses Entity Framework Core migrations. `StoreProduct` represents the single storefront product; `ProductVariant` stores the selectable SKU, display name, price/currency code, active state, and stock quantity. `ProductAttributeGroup` and `ProductAttributeOption` represent required or optional shopper selections and non-negative price adjustments. `StoreProduct` also carries admin-configured flash-sale, share/WhatsApp, and storefront brand/copy settings. `ProductImage` metadata is relational; image bytes use the `IProductImageStorage` adapter, which currently stores random-keyed files under `App_Data/product-images` by default (`ProductMedia:RootPath` can set another local path). The API accepts JPEG, PNG, and WebP signatures up to 10 MB and exposes files publicly only when the product is published; admin previews require SystemAdmin. `ProductTrustItem` stores configurable service details. The public catalog query returns only the published product and active variants/options, and exposes availability without exposing exact stock counts. Attribute adjustments and scheduled sale prices are shown in the storefront total; cart/order price validation and snapshotting remain part of the future checkout workflow.

The catalog shape is one product with selectable variants. Variant option dimensions and inventory reservation/decrement rules remain open and must be confirmed before checkout is implemented.

## Installer and deployment

Treat first installation and upgrades as explicit workflows. The installer must validate prerequisites and configuration, protect secrets, prevent repeated setup from weakening security, and support supported hosting targets. Exact installer and topology decisions remain open in the requirements document.

## HTTPS requirement

The API accepts requests only when ASP.NET Core sees an HTTPS scheme. The local launch profile binds to HTTPS at `https://localhost:7241`. Production deployments must use a valid TLS certificate. If TLS terminates at a reverse proxy, set `ForwardedHeaders:KnownProxies` to the proxy's IP address so the API can safely recognize the forwarded HTTPS scheme. Forwarded headers from untrusted clients must not be accepted.

## Administrator authentication

Administrator accounts use ASP.NET Core Identity with the configured relational provider and secure cookies. Mutating browser API calls require an anti-forgery request token. Credentialed CORS accepts only configured frontend origins (development permits the local Vite origins). Public registration is not mapped. The first System Admin is created by the one-time local bootstrap command, which prompts for a password; later account creation and role assignment require SystemAdmin authorization. New accounts must change their temporary password on first sign-in.

## Current implementation boundary

The API exposes health and published catalog/image endpoints, SystemAdmin-only product editing and media management, and SystemAdmin-only user, role, and audit APIs. Product, image, and account/role changes create audit records. The frontend includes a configurable storefront gallery/header/trust details, admin sign-in, catalog and media editor, session-only shopping cart, user management, and recent audit history. COD order creation, order administration, general store settings, and installer workflows are not implemented yet. Guest checkout fields are confirmed as name, phone, and delivery address; address validation, confirmation/cancellation, quantity, and inventory policies remain open. Catalog permissions are limited to System Admin pending the open role-mapping decision.
