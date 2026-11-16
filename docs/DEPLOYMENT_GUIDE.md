# Deployment guide

This guide defines the initial supported hosting paths for OneCart Commerce:

- Docker Compose with Linux containers and PostgreSQL.
- Linux with systemd, Nginx, and PostgreSQL.
- Windows IIS with the ASP.NET Core Hosting Bundle and PostgreSQL.

PostgreSQL is the supported first-release database. SQL Server remains experimental and is not covered by these deployment paths. Keep production instances to one API replica while applying migrations; the current migration command is a single-instance operation.

## Shared requirements

- .NET 10 runtime for non-container hosts, PostgreSQL, and a supported OS architecture.
- At least 2 vCPU, 4 GiB RAM, and 20 GiB SSD for a small, low-traffic first deployment. Add capacity for PostgreSQL, uploaded images, logs, and backups as the store grows.
- A stable DNS name and a valid, publicly trusted TLS certificate. Local development certificates are not suitable for public deployment.
- Persistent, access-controlled storage for PostgreSQL, uploaded product images, and ASP.NET Core Data Protection keys.
- SMTP settings if administrators need password-reset email.
- Store secrets in the hosting platform's secret manager or an access-controlled environment file. Do not place them in source control, a public artifact, or a customer-accessible web root.

All deployments must set these API configuration values through the host environment:

| Setting | Purpose |
| --- | --- |
| `Database__Provider=PostgreSql` | Selects PostgreSQL, the supported provider. |
| `ConnectionStrings__Store` | PostgreSQL connection string supplied through a host secret mechanism. |
| `ConnectionStrings__StoreFile` | Optional path to a mounted secret file containing the PostgreSQL connection string. When set, this takes precedence over `ConnectionStrings__Store`. |
| `Setup__BootstrapTokenFile` or `Setup__BootstrapToken` | Optional protected-file path or secret setting containing a random setup token of at least 32 bytes. Enables the one-time `/install` wizard until a SystemAdmin exists. |
| `Cors__AllowedOrigins__0` | Exact HTTPS storefront origin, including scheme and host. |
| `DataProtection__KeyDirectory` | Persistent directory for authentication and signed-cart keys. |
| `ProductMedia__RootPath` | Persistent directory for product image bytes. |
| `Email__PasswordResetUrl` | HTTPS storefront password-reset page. |

Configure SMTP with `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Username`, `Email__Smtp__Password`, and `Email__From`. Password reset remains unavailable until a working mail service is configured.

After configuring the database, apply migrations by running the published API with `--migrate`. Create the first System Admin with `--bootstrap-system-admin admin@example.com`; enter the password at its hidden console prompt. Both commands run against the same production environment configuration as the API.

## Docker Compose (recommended portable path)

The root `docker-compose.yml` builds the API and storefront, starts PostgreSQL, applies migrations using a one-shot migration container, and serves the site through Caddy. Caddy obtains and renews certificates automatically. The API is not published directly to the host; it trusts forwarded HTTPS only from Caddy's fixed address on a dedicated API↔proxy network. The public-facing edge network has dynamically assigned container addresses, so the storefront cannot take the proxy's trusted address. PostgreSQL is isolated on an internal Docker network.

Prerequisites: Docker Engine or Docker Desktop with Compose v2, a DNS name pointed at the host, and inbound TCP ports 80 and 443 available. UDP 443 is optional and enables HTTP/3.

From the repository root:

```sh
cp deploy/.env.example .env
mkdir -p secrets
umask 077
password="$(openssl rand -hex 32)"
printf '%s' "$password" > secrets/postgres_password
printf 'Host=db;Port=5432;Database=onecart;Username=onecart;Password=%s' "$password" > secrets/store_connection
unset password
setup_token="$(openssl rand -hex 32)"
printf '%s' "$setup_token" > secrets/bootstrap_token
printf 'One-time browser setup token (save securely): %s\n' "$setup_token"
unset setup_token
chmod 600 secrets/postgres_password secrets/store_connection secrets/bootstrap_token .env
```

The command generates and stores unique database and setup secrets. Set `ONECART_DOMAIN` and `ACME_EMAIL` in `.env` using a text editor, then save the file with operator-only permissions. Compose mounts source secrets into PostgreSQL and a one-shot initializer. The initializer copies the API connection string and setup token to a named volume owned by the API's non-root user; the API and migration containers mount that volume read-only. Passwords are not passed through container environment variables. SMTP variables in `.env` are optional until password-reset mail is required. Protect `.env`, `secrets/`, the Docker daemon account, and Docker volume storage as production secrets.

The example file assigns a private subnet to the dedicated API↔proxy network and fixes the API and Caddy addresses there, so the API trusts forwarded HTTPS only from Caddy. The storefront network uses Docker-assigned addresses. If `ONECART_PROXY_SUBNET` overlaps a Docker, VPN, or host network, choose a non-overlapping subnet and keep `ONECART_CADDY_IP` and `ONECART_API_IP` inside it and unique.

Validate and start the stack:

```sh
docker compose config --quiet
docker compose up --build --detach
docker compose ps
docker compose logs --tail=100 migrate api proxy
```

Open `https://ONECART_DOMAIN/install` and enter the one-time token printed during secret setup, the System Admin email, and the password. The installer only runs while no SystemAdmin account exists, requires the exact configured HTTPS storefront origin, checks the token in constant time, rate-limits attempts, and writes an audit event. The setup endpoint does not create any more accounts after the initial System Admin exists. Rotate `secrets/bootstrap_token` to a newly generated random value and refresh its in-volume copy:

```sh
setup_token="$(openssl rand -hex 32)"
printf '%s' "$setup_token" > secrets/bootstrap_token
unset setup_token
chmod 600 secrets/bootstrap_token
docker compose up --detach --force-recreate secrets-init api
```

Open `https://ONECART_DOMAIN` in a browser, replacing the name with the configured domain. The storefront and API share one HTTPS origin; the storefront API base is set at image build time. To change the domain, update `.env` and rebuild with `docker compose up --build --detach`.

Uploaded media, data-protection keys, database files, Caddy certificates, and runtime secret copies are in named volumes. Back up PostgreSQL data with the `product-media` and `data-protection-keys` volumes together; also preserve `caddy-data` for certificate continuity. The `runtime-secrets` volume is regenerated from `secrets/`; keep the source secret files in a separate encrypted backup. See [PostgreSQL backup and restore](OPERATIONS_POSTGRES.md).

For upgrades, take a verified backup first, update the deployment source or image version, then run `docker compose up --build --detach`. Compose runs the migration container before starting the API. Do not scale the API service above one replica until migrations use a separately coordinated deployment process.

## Linux systemd and Nginx

Use a dedicated unprivileged service account, a persistent directory such as `/var/lib/onecart` for media and keys, and an access-controlled environment file outside the application directory. Publish the API and build the storefront:

```sh
dotnet publish backend/OneCart.Api/OneCart.Api.csproj --configuration Release --output ./artifacts/api
cd frontend
npm ci
VITE_API_BASE_URL=https://shop.example.com npm run build
```

Deploy `artifacts/api` to `/opt/onecart/api` and `frontend/dist` to `/var/www/onecart`. Set the API environment to `Production`, bind Kestrel only to `http://127.0.0.1:8080`, set `ForwardedHeaders__KnownProxies__0=127.0.0.1`, and configure the environment variables listed above. Set `Cors__AllowedOrigins__0=https://shop.example.com`.

Run migrations and one-time bootstrap from a terminal that has loaded the same protected environment variables as the systemd service:

```sh
cd /opt/onecart/api
dotnet OneCart.Api.dll --migrate
dotnet OneCart.Api.dll --bootstrap-system-admin admin@example.com
```

Set `Setup__BootstrapTokenFile=/etc/onecart/setup-token` in the protected service environment file, and make the token file readable only to the API service account and deployment operator. Create a random 32-byte token with `openssl rand -hex 32`, put it in that file, and enter it at `https://shop.example.com/install`. After setup, rotate the token file and restart `onecart-api` so the one-time endpoint is no longer accessible with the setup token.

Configure Nginx with a valid certificate and redirect HTTP to HTTPS. Serve the SPA from `/var/www/onecart`; proxy `/api/` and `/health` to `http://127.0.0.1:8080`, preserving `Host` and setting `X-Forwarded-Proto $scheme`. Do not expose port 8080 publicly. The API accepts forwarded protocol information from the explicitly configured loopback proxy only.

Start from [`deploy/linux/onecart-api.service`](../deploy/linux/onecart-api.service) and [`deploy/linux/nginx-onecart.conf`](../deploy/linux/nginx-onecart.conf). Replace the example hostname and certificate paths. Nginx TLS certificates can be provisioned and renewed with the distribution's Certbot packages; open only ports 80 and 443 at the host firewall. Configure systemd to start after PostgreSQL; monitor logs with `journalctl -u onecart-api`.

## Windows IIS

Install the .NET 10 ASP.NET Core Hosting Bundle on a supported Windows Server, PostgreSQL, and a valid TLS certificate. Create two IIS sites: the React static files at `https://shop.example.com` and the published API at `https://api.example.com`. Bind a trusted certificate to both hostnames. Configure `VITE_API_BASE_URL=https://api.example.com` before building the storefront, and set `Cors__AllowedOrigins__0=https://shop.example.com` for the API.

Publish the API and build the storefront:

```powershell
dotnet publish backend/OneCart.Api/OneCart.Api.csproj --configuration Release --output .\artifacts\api
Set-Location frontend
npm ci
$env:VITE_API_BASE_URL = 'https://api.example.com'
npm run build
```

Deploy the API output (including its generated `web.config`) to a dedicated IIS application pool with **No Managed Code**. Deploy `frontend/dist` as the static site. Install IIS URL Rewrite so direct `/admin` and `/install` navigation reaches the SPA. Run the API app pool with a dedicated identity that can write only to the configured media and data-protection directories. Restrict the IIS bindings and firewall to HTTPS; redirect HTTP at IIS. IIS supplies the HTTPS scheme to the hosted ASP.NET Core application, so do not enable forwarded headers unless a separately configured, trusted proxy is in front of IIS.

Supply production configuration through the IIS host environment or the organization's protected secret store, not by adding passwords to the published `web.config`. Run `dotnet OneCart.Api.dll --migrate` and then `--bootstrap-system-admin` from the published API directory using the same secure environment. Configure database, CORS, reset URL, media path, key path, and SMTP as described in [Shared requirements](#shared-requirements).

Set `Setup__BootstrapTokenFile` to an ACL-protected file available to the API app-pool identity, or supply `Setup__BootstrapToken` from the host's secret manager. Enter the generated token at `https://shop.example.com/install`. Rotate the token secret after creating the first System Admin.

For both API and storefront sites, configure the IIS **Physical Path**, enable anonymous authentication, and bind each trusted HTTPS certificate to the correct hostname. Store writable media and key directories outside the publish directory. The API can read a custom `ConnectionStrings:StoreFile` path when the host's secret store exposes connection secrets as files; otherwise supply `ConnectionStrings:Store` securely.

## Readiness and recovery

Check `GET /health` over the public HTTPS URL after deployment. Check `GET /api/v1/store/product` after publishing a product in the administrator area. Configure SMTP and test administrator password reset. Confirm an uploaded product image survives an API restart. Before accepting orders, complete a backup and isolated restore rehearsal, verify Data Protection keys survive a restart, and review the firewall, TLS renewal, log retention, and access to backups.

For incidents, inspect API and reverse-proxy logs first, verify database reachability and disk space, and restore database and media/key files from the same backup point. The current runbooks cover PostgreSQL logical backup and restore; point-in-time recovery and high availability require a separate operator design.
