# Installation and local setup

This is the current manual installation path for local development. A guided web installer and a supported production deployment process are not implemented; hosting topology and installer behavior remain open decisions.

## Requirements

- .NET 10 SDK
- Node.js 20.19+ or 22.12+ and npm
- PostgreSQL, configured for local connections
- Git, if cloning the repository

PostgreSQL is the default and first-release provider. SQL Server has an experimental local path and is not a supported release provider. The Visual Studio Development launch profiles use local SQL Server (`localhost`, database `OneCartCommerce`) with Windows Authentication; edit those local launch-profile values if your instance differs. The normal provider default in code remains PostgreSQL.

## Configure PostgreSQL

Create an empty database and configure its connection string in .NET user secrets. Update the username/password to match the local PostgreSQL instance. Do not put credentials in `appsettings.json`, `.env`, or Git.

```powershell
dotnet user-secrets set --project backend/OneCart.Api "ConnectionStrings:Store" "Host=localhost;Port=5432;Database=onecart;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

Restore the local .NET tool manifest and apply pending EF Core migrations:

```powershell
dotnet tool restore
dotnet ef database update --project backend/OneCart.Api
```

Create the first System Admin only when the database does not already have one. The command migrates the database, seeds built-in roles, and prompts for a password without placing it in command history:

```powershell
dotnet run --project backend/OneCart.Api -- --bootstrap-system-admin admin@example.com
```

Bootstrap is one-time. It refuses to create a second System Admin.

## Run from Visual Studio or the command line

Open `SingleProductCommerce.sln` in Visual Studio. It contains the API, persistence library, migration assembly, and backend test projects. The React application remains a separate Vite project.

Trust a local HTTPS development certificate once:

```powershell
dotnet dev-certs https --trust
```

Run the API from the repository root in a terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/OneCart.Api
```

In Development, API startup applies pending PostgreSQL migrations automatically. Restart the API after pulling schema changes; production deployments should apply reviewed migrations through the operator's release process.

Run the storefront from a second terminal:

```powershell
Set-Location frontend
npm ci
npm run dev
```

The default local URLs are `https://localhost:7241` for the API and `http://127.0.0.1:5173` for the storefront. The API rejects plain HTTP requests. Set `VITE_API_BASE_URL` only to an HTTPS API URL. Stop a running API process before rebuilding the same configuration in Visual Studio; a running .NET process may hold its output assemblies open. Alternatively, use separate Debug and Release outputs for concurrent runs.

## Local files and operational settings

Product images are stored on the API host under `backend/OneCart.Api/App_Data/product-images` by default. Data Protection keys are stored under `backend/OneCart.Api/App_Data/data-protection-keys`; Windows protects local keys with DPAPI. These folders are ignored by Git and must be included in a store backup. See the [PostgreSQL backup and restore runbook](OPERATIONS_POSTGRES.md).

To enable administrator password-reset email, configure SMTP and the HTTPS reset URL using user secrets. See the [README setup section](../README.md#run-locally). Email recovery is unavailable until SMTP is configured.

## Production boundary

The project currently documents local setup only. Before production use, operators still need an approved hosting topology, valid TLS certificate provisioning and renewal, trusted reverse-proxy addresses, persistent and appropriately protected Data Protection keys, SMTP delivery, PostgreSQL backup/restore operations, monitoring, and a tested upgrade plan. Do not treat a local development certificate or this manual setup as production deployment guidance.
