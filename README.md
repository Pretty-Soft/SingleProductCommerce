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

The API health check is available at `/health`. The public catalog read endpoint is `GET /api/v1/store/product`; it returns `404` until an administrator workflow publishes a product. Admin product editing and checkout are not implemented yet. The current frontend is only a starter shell.

## Project direction

Read [the product requirements](docs/PRODUCT_REQUIREMENTS.md) before implementing features. Confirmed requirements, recommendations, and unresolved decisions are separated there so future work does not treat assumptions as approvals.
