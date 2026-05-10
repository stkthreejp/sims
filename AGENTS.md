# SIMS — SMM Insurance Management System

## Stack
- **Backend:** ASP.NET Core 8, Entity Framework Core, PostgreSQL, Azure Blob Storage
- **Frontend:** React + TypeScript + Vite, running on port 5173
- **API:** runs on port 5000
- **Auth:** Microsoft/Azure AD (MSAL)
- **Docs editor:** TipTap + Mammoth

## Project layout
```
SIMS/
├── backend/src/
│   ├── SIMS.API/          # ASP.NET Web API entry point
│   ├── SIMS.Application/  # Business logic / CQRS
│   ├── SIMS.Infrastructure/ # EF Core, migrations, blob storage
│   └── SIMS.Domain/       # Entities, value objects
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
~/.dotnet/tools/dotnet-ef migrations add <Name> --project src/SIMS.Infrastructure --startup-project src/SIMS.API
~/.dotnet/tools/dotnet-ef database update --project src/SIMS.Infrastructure --startup-project src/SIMS.API

# Frontend
cd frontend && npm run dev
npx tsc --noEmit   # type check
```

## Git workflow
This is a solo developer project. **Commit directly to `main`** — do not create feature branches or worktrees. Use `git push origin main` for all changes.

## Dev server startup
Use the backend_ms.log and frontend_ms.log files in the root to check server status.
Backend: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\SIMS\backend_ms.log' -Tail 20"`
Frontend: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\SIMS\frontend_ms.log' -Tail 15"`

## Codex workspace notes
- Keep project helper scripts in `C:\Users\JeremiahPODonovan\SIMS\scripts`.
- Keep scratch files, temporary outputs, and generated diagnostics in `C:\Users\JeremiahPODonovan\SIMS\temp` or `C:\tmp`.
- Avoid writing project files outside the SIMS workspace unless explicitly requested.
- It is acceptable to request network access up front for package restore, npm/NuGet installs, GitHub work, documentation lookup, or dependency troubleshooting.
