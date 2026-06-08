# SIMS — SMM Insurance Management System

## Roadmap & current priorities
The active coordination doc is **`docs/GO-LIVE-UNIFIED-PLAN.md`** — it reconciles all prior plan docs and sequences the work to internal UAT/staging across workstreams WS0–WS10 (tree stabilization, config hardening, authorization, bugs, UI alignment, program/carrier setup, rating, bordereaux, deploy/UAT, production reporting, claims/loss-run import). Treat it as the source of truth for what to work on and in what order; older phase docs are reconciled into it (see its Appendix A), and live audit findings are in its §8. Post-launch initiatives (e.g., direct bill + electronic payments + notices) live in `docs/DIRECT-BILL-AND-NOTICES-ARCHITECTURE.md`.

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
cd backend && dotnet test            # service-layer tests (CI gate per go-live plan WS8)
~/.dotnet/tools/dotnet-ef migrations add <Name> --project src/SIMS.Infrastructure --startup-project src/SIMS.API
~/.dotnet/tools/dotnet-ef database update --project src/SIMS.Infrastructure --startup-project src/SIMS.API

# Frontend
cd frontend && npm run dev
npx tsc --noEmit   # type check
```

## Git workflow
This is a solo developer project. **Commit directly to `main`** — do not create feature branches or worktrees. Use `git push origin main` for all changes.
Line endings are normalized to **LF** via `.gitattributes` — let git handle EOL; don't reformat files to re-introduce CRLF. `.agents/`, `plugins/`, and `*.docx` planning docs are gitignored (local tooling / binaries).

## Deployment & dev servers
Normal testing runs against the **Azure test environment**, not local servers: commit to `main` → `git push origin main` → smoke-test the Azure apps (`sims-frontend-test` / `sims-api-test`, resource group `simes-test-rg`). Local dev servers are for **explicit local debugging only**. See `AGENTS.md` for the full deployment + engineering protocol.

Local logs (debug only):
Backend: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\SIMS\backend_ms.log' -Tail 20"`
Frontend: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\SIMS\frontend_ms.log' -Tail 15"`
