# OneCart Commerce

OneCart Commerce is a commercial, single-product commerce platform. This repository starts with a minimal ASP.NET Core API and React storefront so implementation can proceed in reviewed, testable milestones.

## Current starter

- `backend/OneCart.Api`: ASP.NET Core 10 API with a health endpoint.
- `frontend`: React and TypeScript storefront shell powered by Vite.
- `docs/PRODUCT_REQUIREMENTS.md`: requirements recovered from the product planning conversation, with open decisions marked clearly.
- `docs/ROADMAP.md`: proposed implementation sequence and decision gates.
- `AGENTS.md`: project-specific instructions for future Codex work.

## Prerequisites

- .NET 10 SDK
- Node.js 20.19+ or 22.12+ and npm
- PostgreSQL

## Run locally

Configure the PostgreSQL connection string in local .NET user secrets. Replace the password with the one configured for your local PostgreSQL server; do not commit it.

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

The API listens at `https://localhost:7241`. Plain HTTP API requests are rejected. The production API base URL must also use `https://` with a certificate trusted by clients. When TLS terminates at a reverse proxy, configure only that proxy's IP under `ForwardedHeaders:KnownProxies`; do not trust forwarded headers from arbitrary clients.

Frontend:

```powershell
cd frontend
npm install
npm run dev
```

The API health check is available at `/health`. The public catalog read endpoint is `GET /api/v1/store/product`; it returns `404` until a published product has an active variant. Open `/admin` in the frontend to sign in, manage users, and edit the product catalog as a System Admin. Product and variant changes are audited. Catalog editing is limited to System Admin until detailed role permissions are decided. New accounts receive a temporary password and must change it at first sign-in. The frontend uses secure cookie authentication and anti-forgery tokens; production deployments must configure `Cors:AllowedOrigins` for the exact frontend origin.

## Project direction

Read [the product requirements](docs/PRODUCT_REQUIREMENTS.md) before implementing features. Confirmed requirements, recommendations, and unresolved decisions are separated there so future work does not treat assumptions as approvals.
