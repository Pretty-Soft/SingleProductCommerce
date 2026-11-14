# OneCart Commerce

OneCart Commerce is a commercial, single-product commerce platform. This repository starts with a minimal ASP.NET Core API and React storefront so implementation can proceed in reviewed, testable milestones.

## Current starter

- `backend/OneCart.Api`: ASP.NET Core 10 API with a health endpoint.
- `frontend`: React and TypeScript storefront shell powered by Vite.
- `docs/PRODUCT_REQUIREMENTS.md`: requirements recovered from the product planning conversation, with open decisions marked clearly.
- `AGENTS.md`: project-specific instructions for future Codex work.

## Prerequisites

- .NET 10 SDK
- Node.js 20.19+ or 22.12+ and npm

## Run locally

Backend:

```powershell
dotnet run --project backend/OneCart.Api
```

Frontend:

```powershell
cd frontend
npm install
npm run dev
```

The API health check is available at `/health`. The current frontend is only a starter shell; commerce workflows are not implemented yet.

## Project direction

Read [the product requirements](docs/PRODUCT_REQUIREMENTS.md) before implementing features. Confirmed requirements, recommendations, and unresolved decisions are separated there so future work does not treat assumptions as approvals.
