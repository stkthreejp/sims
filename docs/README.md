# SIMS — SMM Insurance Management System

SIMS is an internal insurance management platform for Specialty Market Managers, LLC. It manages the full insurance workflow from submission through policy issuance, billing, accounting, and reporting.

## Documentation

| File | Contents |
|---|---|
| [architecture.md](architecture.md) | System architecture, project structure, layer responsibilities |
| [infrastructure.md](infrastructure.md) | Azure services, secrets management, local dev setup |
| [backend.md](backend.md) | API controllers, services, domain entities |
| [frontend.md](frontend.md) | Pages, components, API modules, tech stack |
| [integrations.md](integrations.md) | QBO, Azure AD, Graph API, Gemini, Syncfusion |
| [deployment.md](deployment.md) | Deployment guide and next steps |

## Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- PostgreSQL client tools
- Azure CLI (`az login` required for local dev)
- Docker (optional)

### Backend
```bash
cd backend/src/SIMS.API
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
# API available at http://localhost:5000
# Swagger at http://localhost:5000/swagger
```

### Frontend
```bash
cd frontend
npm install
npm run dev
# App available at http://localhost:5173
```

### Type Check
```bash
cd frontend && npx tsc --noEmit
```

### Database Migrations
```bash
# Add a migration
~/.dotnet/tools/dotnet-ef migrations add <Name> --project backend/src/SIMS.Infrastructure --startup-project backend/src/SIMS.API

# Apply migrations
~/.dotnet/tools/dotnet-ef database update --project backend/src/SIMS.Infrastructure --startup-project backend/src/SIMS.API
```
