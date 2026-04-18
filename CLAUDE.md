# IMS — Insurance Management System

## Stack
- **Backend:** ASP.NET Core 8, Entity Framework Core, PostgreSQL, Azure Blob Storage
- **Frontend:** React + TypeScript + Vite, running on port 5173
- **API:** runs on port 5000
- **Auth:** Microsoft/Azure AD (MSAL)
- **Docs editor:** TipTap + Mammoth

## Project layout
```
IMS/
├── backend/src/
│   ├── IMS.API/          # ASP.NET Web API entry point
│   ├── IMS.Application/  # Business logic / CQRS
│   ├── IMS.Infrastructure/ # EF Core, migrations, blob storage
│   └── IMS.Domain/       # Entities, value objects
├── frontend/src/
│   ├── pages/            # React pages
│   └── components/       # Shared components
├── docker-compose.yml
└── .env                  # DB connection string, Azure config
```

## Common commands
```bash
# Backend
cd backend && dotnet build
~/.dotnet/tools/dotnet-ef migrations add <Name> --project src/IMS.Infrastructure --startup-project src/IMS.API
~/.dotnet/tools/dotnet-ef database update --project src/IMS.Infrastructure --startup-project src/IMS.API

# Frontend
cd frontend && npm run dev
npx tsc --noEmit   # type check
```

## Dev server startup
Use the backend_ms.log and frontend_ms.log files in the root to check server status.
Backend: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\IMS\backend_ms.log' -Tail 20"`
Frontend: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\IMS\frontend_ms.log' -Tail 15"`
