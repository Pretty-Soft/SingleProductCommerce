# OneCart Commerce

OneCart Commerce is a commercial, single-product commerce platform. The current implementation provides a responsive product storefront, a protected administration experience, and a PostgreSQL-backed Cash on Delivery workflow. Remaining work and unresolved product decisions are tracked in the roadmap and requirements documents.

## Current implementation

- `backend/OneCart.Api`: ASP.NET Core 10 HTTPS API for the storefront, admin catalog, media, user/role administration, audit history, cart quotes, checkout, and order management.
- `backend/OneCart.Persistence`: catalog, identity, cart quote, order, and audit persistence with PostgreSQL as the first-release provider.
- `backend/OneCart.SqlServer.Migrations`: experimental SQL Server migration set, isolated from PostgreSQL migrations.
- `frontend`: React and TypeScript storefront and admin interface powered by Vite.
- The storefront supports English, Bangla, and Spanish. Its selector is remembered per browser; System Admins, Catalog Managers, and Store Managers can edit localized catalog/storefront fields within their existing role scopes.
- `docs/PRODUCT_REQUIREMENTS.md`: requirements recovered from the product planning conversation, with open decisions marked clearly.
- `docs/ARCHITECTURE.md`: current architecture and implementation boundaries.
- `docs/ADMINISTRATOR_GUIDE.md`: admin roles and workflows.
- `docs/API_CONTRACTS.md`: versioned public and admin API route summary.
- `docs/INSTALLATION_GUIDE.md`: current manual local setup and production-readiness boundary.
- `docs/CUSTOMIZATION_GUIDE.md`: admin-configurable content and safe source changes.
- `docs/ROADMAP.md`: proposed implementation sequence and decision gates.
- `docs/OPERATIONS_POSTGRES.md`: PostgreSQL backup and restore procedures, including locally stored product images and Data Protection keys.
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

The System Admin can make authenticator-app MFA optional or mandatory for all administrators; it is optional by default. Enrolled admins enter a code from their authenticator app at sign-in. Configure SMTP and the HTTPS frontend reset URL in user secrets to enable email password recovery; SMTP credentials must never be stored in source control:

```powershell
dotnet user-secrets set --project backend/OneCart.Api "Email:Smtp:Host" "smtp.example.com"
dotnet user-secrets set --project backend/OneCart.Api "Email:Smtp:Port" "587"
dotnet user-secrets set --project backend/OneCart.Api "Email:Smtp:Username" "YOUR_SMTP_USERNAME"
dotnet user-secrets set --project backend/OneCart.Api "Email:Smtp:Password" "YOUR_SMTP_PASSWORD"
dotnet user-secrets set --project backend/OneCart.Api "Email:From" "store@example.com"
dotnet user-secrets set --project backend/OneCart.Api "Email:PasswordResetUrl" "https://store.example.com/admin/reset-password"
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

GitHub Actions runs the API workflow tests, persistence tests, storefront tests, and production frontend build for pushes and pull requests. SQL Server integration tests remain a separate opt-in local check because they require a disposable SQL Server instance.

The SQL Server integration test creates and drops a uniquely named disposable database. Run it only with a local/test SQL Server login that has database create/drop permission:

```powershell
$env:ONECART_SQLSERVER_TEST_CONNECTION = "Server=localhost\SQLEXPRESS;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True"
dotnet test backend/OneCart.Persistence.IntegrationTests/OneCart.Persistence.IntegrationTests.csproj
Remove-Item Env:\ONECART_SQLSERVER_TEST_CONNECTION
```

The API listens at `https://localhost:44300` in the Visual Studio IIS Express profile. Plain HTTP API requests are rejected. The production API base URL must also use `https://` with a certificate trusted by clients. When TLS terminates at a reverse proxy, configure only that proxy's IP under `ForwardedHeaders:KnownProxies`; do not trust forwarded headers from arbitrary clients.

Development stores ASP.NET Core Data Protection keys under `backend/OneCart.Api/App_Data/data-protection-keys` by default; this directory is Git-ignored, and Windows protects generated keys with DPAPI. For production, set `DataProtection:KeyDirectory` to a persistent, access-controlled directory available to every API instance. Protect the key material at rest using the platform's supported mechanism and keep the directory outside source control.

Frontend:

```powershell
cd frontend
npm install
npm run dev
```

The API health check is available at `/health`. The public catalog read endpoint is `GET /api/v1/store/product`; it returns `404` until a published product has an active variant. The storefront supports configurable selections, a session-only cart, and guest COD checkout. Adding an item requests a server-signed quote that locks its unit price and currency; checkout saves the order as Pending with an order number and atomically decrements stock. Mixed-currency carts are rejected. Open `/admin` to manage product content, storefront settings, orders, customer order contacts, and administrator accounts. Roles are limited by responsibility: CatalogManager manages catalog and product media; OrderManager manages orders and customer order contacts; StoreManager manages storefront presentation settings; SystemAdmin has all permissions, including account and role administration. The order view shows all-time order counts and totals grouped by current status and saved currency; currencies are never combined. Each customer entry represents one order, and orders are not merged into profiles. Admin cancellation restores ordered stock; status transitions currently allow Pending → Processing/Cancelled and Processing → Completed/Cancelled. Product media is stored locally under `backend/OneCart.Api/App_Data/product-images` by default (configurable with `ProductMedia:RootPath`); this folder is ignored by Git. JPEG, PNG, and WebP uploads up to 10 MB are currently accepted. Product, storefront, account, catalog, media, and order-status changes are audited. New accounts receive a temporary password and must change it at first sign-in, and may enroll an authenticator app; if the System Admin requires MFA, enrollment is required before using admin functions. Password recovery uses email reset links and requires the SMTP settings above. The frontend uses secure cookie authentication and anti-forgery tokens; production deployments must configure `Cors:AllowedOrigins` for the exact frontend origin.

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
