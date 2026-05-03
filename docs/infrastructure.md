# Infrastructure

## Azure Services

| Service | Resource | Purpose |
|---|---|---|
| Azure Database for PostgreSQL Flexible Server | `sims.postgres.database.azure.com` | Primary database |
| Azure Blob Storage | `smmimsdocuments` | Document and attachment storage |
| Azure Key Vault | `simskey.vault.azure.net` | Secrets management |
| Azure Active Directory | Tenant `49037468-...` | Authentication (MSAL) |

## Database

- **Server:** `sims.postgres.database.azure.com`
- **Database:** `postgres`
- **Admin user:** `sims_admin`
- **Tier:** Burstable B1ms (1 vCore, 2GB RAM) — scale up as needed
- **Region:** East US
- **Backups:** 7-day retention (increase when going live with real users)
- **High Availability:** Disabled (enable before going live)

### Firewall Rules
Currently allows the developer's IP address only. When deploying the backend to Azure App Service, add the App Service outbound IPs and remove the developer IP, or set up VNet integration for a fully private connection.

## Secrets Management

All secrets are stored in Azure Key Vault (`simskey`). The app uses `DefaultAzureCredential` which:
- **Locally:** authenticates via Azure CLI (`az login`)
- **Production:** will use Managed Identity (no credentials needed)

### Secrets in Key Vault

| Key Vault Secret Name | Maps To Config Key | Description |
|---|---|---|
| `ConnectionStrings--DefaultConnection` | `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `Storage--AzureBlobConnectionString` | `Storage:AzureBlobConnectionString` | Azure Blob Storage |
| `Qbo--ClientSecret` | `Qbo:ClientSecret` | QuickBooks Online OAuth |
| `Qbo--RefreshToken` | `Qbo:RefreshToken` | QuickBooks Online refresh token |

> Note: Key Vault uses `--` as the separator for nested config keys (maps to `:` in .NET configuration).

## Local Development Setup

### Prerequisites
1. Install [Azure CLI](https://aka.ms/installazurecliwindows)
2. Log in: `az login`
3. Install [PostgreSQL client tools](https://www.postgresql.org/download/windows/) (for migrations/restore)

### Connection String (User Secrets)
The connection string is stored in .NET user secrets, not in `appsettings.json`:

```bash
cd backend/src/SIMS.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=sims.postgres.database.azure.com;Database=postgres;Username=sims_admin;Password=<password>;SSL Mode=Require;Trust Server Certificate=true"
```

### Starting the Backend
```powershell
# Must set environment so user secrets and Key Vault are loaded
$env:ASPNETCORE_ENVIRONMENT = "Development"
# Must have az in PATH
$env:PATH += ";C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin"
cd backend/src/SIMS.API
dotnet run
```

### appsettings.json
`appsettings.json` is in `.gitignore` — it is not committed. The file on disk contains placeholder values. All real secrets come from:
- User secrets (local dev)
- Key Vault (production)

## Docker Compose (Local Alternative)

`docker-compose.yml` defines three services for fully local development:

```
PostgreSQL 16   → port 5432
Backend API     → port 5000 (maps to container 8080)
Frontend        → port 3000
```

Run with:
```bash
docker-compose up
```

## Deployment Roadmap

The backend is not yet deployed to Azure. When ready:

1. **Create Azure App Service** (or Container App)
2. **Enable Managed Identity** on the App Service
3. **Grant Managed Identity** the `Key Vault Secrets User` role on `simskey`
4. **Set environment variable** `ASPNETCORE_ENVIRONMENT=Production` on the App Service
5. **Add App Service outbound IPs** to the PostgreSQL firewall (or set up VNet + private endpoint)
6. **Remove developer IP** from PostgreSQL firewall
7. **Point frontend** `VITE_API_URL` to the deployed API URL

After this, `DefaultAzureCredential` will automatically use Managed Identity in production — no credential changes needed in code.
