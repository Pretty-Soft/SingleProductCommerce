# OneCart Commerce

OneCart Commerce is a commercial, single-product commerce platform. This repository starts with a minimal ASP.NET Core API and React storefront so implementation can proceed in reviewed, testable milestones.

## Current starter

- `backend/OneCart.Api`: ASP.NET Core 10 API with a health endpoint.
- `backend/OneCart.Persistence`: catalog and identity persistence with provider selection.
- `backend/OneCart.SqlServer.Migrations`: experimental SQL Server migration set, isolated from PostgreSQL migrations.
- `frontend`: React and TypeScript storefront shell powered by Vite.
- `docs/PRODUCT_REQUIREMENTS.md`: requirements recovered from the product planning conversation, with open decisions marked clearly.
- `docs/ROADMAP.md`: proposed implementation sequence and decision gates.
- `AGENTS.md`: project-specific instructions for future Codex work.

## Prerequisites

- .NET 10 SDK
- Node.js 20.19+ or 22.12+ and npm
- PostgreSQL for the default first-release path
- SQL Server for the experimental local option

## Run locally

PostgreSQL remains the default and first-release database provider. Configure its connection string in local .NET user secrets. Replace the password with the one configured for your local PostgreSQL server; do not commit it.

```powershell
dotnet user-secrets set --project backend/OneCart.Api "ConnectionStrings:Store" "Host=localhost;Port=5432;Database=onecart;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
dotnet tool restore
dotnet ef database update --project backend/OneCart.Api
```

Create the requested first System Admin account. The bootstrap command applies pending migrations, seeds the built-in roles, and prompts for the password without displaying or storing it in the repository:

```powershell
dotnet run --project backend/OneCart.Api -- --bootstrap-system-admin zs.islam93@gmail.com
```

Create and trust the local ASP.NET Core development certificate once:

```powershell
dotnet dev-certs https --trust
```

Backend (HTTPS only):

```powershell
dotnet run --project backend/OneCart.Api
```

Run the focused persistence and validation tests with:

```powershell
dotnet test backend/OneCart.Persistence.Tests/OneCart.Persistence.Tests.csproj
dotnet test backend/OneCart.Api.Tests/OneCart.Api.Tests.csproj
```

Run the storefront cart tests and production build with:

```powershell
cd frontend
npm test
npm run build
```

The SQL Server integration test creates and drops a uniquely named disposable database. Run it only with a local/test SQL Server login that has database create/drop permission:

```powershell
$env:ONECART_SQLSERVER_TEST_CONNECTION = "Server=localhost\SQLEXPRESS;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True"
dotnet test backend/OneCart.Persistence.IntegrationTests/OneCart.Persistence.IntegrationTests.csproj
Remove-Item Env:\ONECART_SQLSERVER_TEST_CONNECTION
```

The API listens at `https://localhost:7241`. Plain HTTP API requests are rejected. The production API base URL must also use `https://` with a certificate trusted by clients. When TLS terminates at a reverse proxy, configure only that proxy's IP under `ForwardedHeaders:KnownProxies`; do not trust forwarded headers from arbitrary clients.

Frontend:

```powershell
cd frontend
npm install
npm run dev
```

The API health check is available at `/health`. The public catalog read endpoint is `GET /api/v1/store/product`; it returns `404` until a published product has an active variant. The storefront supports configurable selections, a session-only cart, and guest COD checkout. Adding an item requests a server-signed quote that locks its unit price and currency; checkout saves the order as Pending with an order number and atomically decrements stock. Mixed-currency carts are rejected. Open `/admin` to manage accounts, product variants and attributes, branding, media, flash sales, contact actions, recent audit history, and customer orders as a System Admin. Admin cancellation restores the ordered stock; status transitions currently allow Pending → Processing/Cancelled and Processing → Completed/Cancelled. Product media is stored locally under `backend/OneCart.Api/App_Data/product-images` by default (configurable with `ProductMedia:RootPath`); this folder is ignored by Git. JPEG, PNG, and WebP uploads up to 10 MB are currently accepted. Admins can schedule percentage or fixed-amount flash sales, enable product sharing, configure WhatsApp contact details, and review customer orders. Product, media, account, catalog, and order-status changes are audited. Catalog editing and order management are limited to System Admin until detailed role permissions are decided. New accounts receive a temporary password and must change it at first sign-in. The frontend uses secure cookie authentication and anti-forgery tokens; production deployments must configure `Cors:AllowedOrigins` for the exact frontend origin.

Store currency is selected by the administrator across the product and its variants. Changing currency after product setup is blocked until the owner provides the price-conversion rule. Existing orders retain their original currency and totals. Address-field and phone-number validation rules, and the final order status-transition policy, remain product decisions.

## Experimental SQL Server development path

SQL Server is an experimental local development option and is not yet a supported release provider. A focused local integration test covers migration and catalog round-trip behavior; installation, broader transaction behavior, backup/restore, and deployment coverage still need verification. PostgreSQL remains the default when `Database:Provider` is unset.

For a local SQL Server instance, configure the provider and connection string in local user secrets. This example uses Windows authentication and trusts the local server certificate; hosted environments need a valid database certificate and a least-privilege login:

```powershell
dotnet user-secrets set --project backend/OneCart.Api "Database:Provider" "SqlServer"
dotnet user-secrets set --project backend/OneCart.Api "ConnectionStrings:Store" "Server=localhost\SQLEXPRESS;Database=OneCart;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True"
dotnet ef database update --project backend/OneCart.SqlServer.Migrations --startup-project backend/OneCart.Api --context OneCart.Api.Data.StoreDbContext
dotnet run --project backend/OneCart.Api -- --bootstrap-system-admin zs.islam93@gmail.com
```

## Project direction

Read [the product requirements](docs/PRODUCT_REQUIREMENTS.md) before implementing features. Confirmed requirements, recommendations, and unresolved decisions are separated there so future work does not treat assumptions as approvals.
