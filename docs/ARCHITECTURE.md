# Initial Architecture Notes

## Starter layout

- `backend/OneCart.Api`: ASP.NET Core HTTP entry point.
- `frontend`: React and TypeScript single-page application.
- `docs`: product and implementation documentation.

## Direction

The initial recommendation is a modular monolith: a single deployable backend with clear boundaries for catalog, checkout, orders, customers, administration, localization, currency, themes, installation, and persistence. Keep domain and application logic independent of HTTP and provider-specific APIs.

The frontend should communicate through explicit JSON API contracts. Keep public storefront routes and protected administration routes separate in navigation and authorization policy.

## Persistence

The product goal names SQL Server, PostgreSQL, MySQL, MongoDB, and hybrid configurations. Do not add superficial provider switches. For each claimed provider, define schema/migration behavior, transactions, indexes, concurrency behavior, backup/restore guidance, and automated integration coverage.

Select a first-release default only after the owner confirms operational needs. A relational provider is recommended for orders and inventory because transaction boundaries matter; the exact vendor remains open.

## Installer and deployment

Treat first installation and upgrades as explicit workflows. The installer must validate prerequisites and configuration, protect secrets, prevent repeated setup from weakening security, and support supported hosting targets. Exact installer and topology decisions remain open in the requirements document.

## HTTPS requirement

The API accepts requests only when ASP.NET Core sees an HTTPS scheme. The local launch profile binds to HTTPS at `https://localhost:7241`. Production deployments must use a valid TLS certificate. If TLS terminates at a reverse proxy, set `ForwardedHeaders:KnownProxies` to the proxy's IP address so the API can safely recognize the forwarded HTTPS scheme. Forwarded headers from untrusted clients must not be accepted.

## Current implementation boundary

The starter API exposes a health endpoint only. The starter frontend is a shell. Neither contains commerce, authentication, persistence, or installer functionality yet.
