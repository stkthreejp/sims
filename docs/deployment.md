# Deployment

## Current State

The application runs locally. The database and secrets are hosted on Azure but the API and frontend are not yet deployed.

| Component | Status | Location |
|---|---|---|
| Database | ✅ Azure | `sims.postgres.database.azure.com` |
| Secrets | ✅ Azure | `simskey.vault.azure.net` |
| Blob Storage | ✅ Azure | `smmimsdocuments` |
| Backend API | ⬜ Not deployed | Local only |
| Frontend | ⬜ Not deployed | Local only |

## Deploying the Backend (Azure App Service)

### Step 1 — Create App Service
1. Azure Portal → Create App Service
2. Runtime: **.NET 8**
3. OS: Linux (smaller/cheaper) or Windows
4. Region: **East US** (same as database)
5. Plan: B1 or higher

### Step 2 — Enable Managed Identity
App Service → Identity → System assigned → **On**

This gives the App Service an Azure AD identity without any credentials.

### Step 3 — Grant Key Vault Access
Key Vault `simskey` → Access Control (IAM) → Add role assignment:
- Role: **Key Vault Secrets User**
- Member: the App Service's managed identity

### Step 4 — Set App Settings
App Service → Configuration → Application settings:
```
ASPNETCORE_ENVIRONMENT = Production
```

The connection string and all other secrets will be pulled from Key Vault automatically via `DefaultAzureCredential` → Managed Identity.

### Step 5 — Update PostgreSQL Firewall
1. Add the App Service's outbound IP addresses to the PostgreSQL firewall
2. Or set up VNet integration + private endpoint (recommended for compliance)
3. Remove the developer IP rule

### Step 6 — Deploy
```bash
# Via Azure CLI
az webapp deploy --resource-group <rg> --name <app-name> --src-path ./backend
```

Or set up GitHub Actions / Azure DevOps pipeline for CI/CD.

## Deploying the Frontend (Azure Static Web Apps or CDN)

### Option A — Azure Static Web Apps (recommended)
1. Create Static Web App in Azure Portal
2. Connect to GitHub repo, branch: `main`, app location: `frontend`
3. Set environment variable: `VITE_API_URL=https://<your-api-app>.azurewebsites.net`
4. Azure will auto-build and deploy on push

### Option B — Azure Blob Storage + CDN
1. Build: `cd frontend && npm run build`
2. Upload `dist/` to a Blob Storage static website container
3. Put Azure CDN in front for HTTPS and caching

## Post-Deployment Checklist

- [ ] Update `AllowedOrigins` in App Service config to include the frontend URL
- [ ] Switch QBO from `sandbox` to `production` in Key Vault (`Qbo--Environment`)
- [ ] Increase PostgreSQL backup retention to 35 days
- [ ] Enable PostgreSQL High Availability
- [ ] Set up monitoring / Application Insights
- [ ] Configure custom domain and SSL certificate
- [ ] Remove developer IP from PostgreSQL firewall once VNet is set up
- [ ] Rotate `GraphApi:ClientSecret` and store in Key Vault

## Environment Variables Reference

### Backend
| Key | Source | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | App Service config | `Development` or `Production` |
| `ConnectionStrings:DefaultConnection` | Key Vault | PostgreSQL connection string |
| `Storage:AzureBlobConnectionString` | Key Vault | Azure Blob Storage |
| `Qbo:ClientSecret` | Key Vault | QuickBooks OAuth client secret |
| `Qbo:RefreshToken` | Key Vault | QuickBooks OAuth refresh token |
| `Syncfusion:LicenseKey` | Key Vault | Syncfusion license |
| `GraphApi:ClientSecret` | Key Vault (pending) | Microsoft Graph API |
| `GeminiApi:ApiKey` | Key Vault (pending) | Google Gemini API |

### Frontend
| Key | Description |
|---|---|
| `VITE_API_URL` | Backend API base URL |
