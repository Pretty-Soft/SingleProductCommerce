# Initial Architecture Notes

## Starter layout

- `backend/OneCart.Api`: ASP.NET Core HTTP entry point.
- `frontend`: React and TypeScript single-page application.
- `docs`: product and implementation documentation.

## Direction

The initial recommendation is a modular monolith: a single deployable backend with clear boundaries for catalog, checkout, orders, customers, administration, localization, currency, themes, installation, and persistence. Keep domain and application logic independent of HTTP and provider-specific APIs.

The frontend should communicate through explicit JSON API contracts. Keep public storefront routes and protected administration routes separate in navigation and authorization policy.

## Persistence

PostgreSQL is the confirmed first-release provider. SQL Server, MySQL, MongoDB, and hybrid configurations remain broader product goals, not current support claims. Do not add superficial provider switches. For each claimed provider, define schema/migration behavior, transactions, indexes, concurrency behavior, backup/restore guidance, and automated integration coverage.

The initial catalog uses Entity Framework Core migrations. `StoreProduct` represents the single storefront product; `ProductVariant` stores the selectable SKU, display name, price/currency code, active state, and stock quantity. The public catalog query returns only the published product and active variants, and exposes availability without exposing exact stock counts.

The catalog shape is one product with selectable variants. Variant option dimensions and inventory reservation/decrement rules remain open and must be confirmed before checkout is implemented.

## Installer and deployment

Treat first installation and upgrades as explicit workflows. The installer must validate prerequisites and configuration, protect secrets, prevent repeated setup from weakening security, and support supported hosting targets. Exact installer and topology decisions remain open in the requirements document.

## HTTPS requirement

The API accepts requests only when ASP.NET Core sees an HTTPS scheme. The local launch profile binds to HTTPS at `https://localhost:7241`. Production deployments must use a valid TLS certificate. If TLS terminates at a reverse proxy, set `ForwardedHeaders:KnownProxies` to the proxy's IP address so the API can safely recognize the forwarded HTTPS scheme. Forwarded headers from untrusted clients must not be accepted.

## Administrator authentication

Administrator accounts use ASP.NET Core Identity with PostgreSQL persistence and secure cookies. Mutating browser API calls require an anti-forgery request token. Credentialed CORS accepts only configured frontend origins (development permits the local Vite origins). Public registration is not mapped. The first System Admin is created by the one-time local bootstrap command, which prompts for a password; later account creation and role assignment require SystemAdmin authorization. New accounts must change their temporary password on first sign-in.

## Current implementation boundary

The API exposes health and read-only published catalog endpoints plus SystemAdmin-only user and role assignment APIs. The frontend includes admin sign-in and user management. Product administration, checkout/orders, audit history, and installer workflows are not implemented yet.
