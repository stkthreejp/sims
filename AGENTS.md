# SIMS — SMM Insurance Management System

## Roadmap & current priorities
The active coordination doc is **`docs/GO-LIVE-UNIFIED-PLAN.md`** — it reconciles all prior plan docs and sequences the work to internal UAT/staging across workstreams WS0–WS10. Treat it as the source of truth for what to work on and in what order; older phase docs are reconciled into it (Appendix A) and the latest security/route audit findings are in §8. Post-launch initiatives (direct bill + electronic payments + notices/reminders) are specced in `docs/DIRECT-BILL-AND-NOTICES-ARCHITECTURE.md`. The read-only AI review harness is in `docs/ai-review/runbook.md`.

## Stack
- **Backend:** ASP.NET Core 8, Entity Framework Core, PostgreSQL, Azure Blob Storage
- **Frontend:** React + TypeScript + Vite, deployed to Azure from Git
- **API:** Azure App Service for normal testing; local port 5000 only when explicitly requested
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

# Frontend checks
cd frontend && npx tsc --noEmit
cd frontend && npm run build

# Local-only debugging, when explicitly requested
cd frontend && npm run dev
```

## Git workflow
This is a solo developer project. **Commit directly to `main`** — do not create feature branches or worktrees. Use `git push origin main` for all changes.
After each completed scoped step, commit and push to `main` as long as only Codex-owned files for that step are staged.
When the worktree contains unrelated changes, stage explicit files only and leave unrelated files untouched.
Line endings are normalized to **LF** via `.gitattributes` — let git handle EOL; don't reformat files to re-introduce CRLF (this previously caused ~270-file phantom diffs). `.agents/`, `plugins/`, and `*.docx` planning docs are gitignored.

## Deployment / smoke testing
Do not use the local frontend or local backend for normal SIMS smoke tests.
The normal flow is: commit to `main`, push to `origin/main`, then smoke test the Azure frontend and Azure API.
Git deployment sends frontend changes to Azure; the local frontend is not the source of truth for pushed changes.

- Frontend app: `sims-frontend-test`
- API app: `sims-api-test`
- Resource group: `simes-test-rg`
- Known API host: `sims-api-test-f9htbma5aee5babz.eastus2-01.azurewebsites.net`

Local dev servers and logs are only for explicit local debugging.
Backend local log: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\SIMS\backend_ms.log' -Tail 20"`
Frontend local log: `powershell -Command "Get-Content 'C:\Users\JeremiahPODonovan\SIMS\frontend_ms.log' -Tail 15"`

## Codex workspace notes
- Keep project helper scripts in `C:\Users\JeremiahPODonovan\SIMS\scripts`.
- Keep scratch files, temporary outputs, and generated diagnostics in `C:\Users\JeremiahPODonovan\SIMS\temp` or `C:\tmp`.
- Avoid writing project files outside the SIMS workspace unless explicitly requested.
- It is acceptable to request network access up front for package restore, npm/NuGet installs, GitHub work, documentation lookup, or dependency troubleshooting.

# 10-rule template

These rules apply to every task in this project unless explicitly overridden.
Bias: caution over speed on non-trivial work. Use judgment on trivial tasks.

## Rule 1 — Think Before Coding
State assumptions explicitly. If uncertain, ask rather than guess.
Present multiple interpretations when ambiguity exists.
Push back when a simpler approach exists.
Stop when confused. Name what's unclear.

## Rule 2 — Simplicity First
Minimum code that solves the problem. Nothing speculative.
No features beyond what was asked. No abstractions for single-use code.
Test: would a senior engineer say this is overcomplicated? If yes, simplify.

## Rule 3 — Surgical Changes
Touch only what you must. Clean up only your own mess.
Don't "improve" adjacent code, comments, or formatting.
Don't refactor what isn't broken. Match existing style.

## Rule 4 — Goal-Driven Execution
Define success criteria. Loop until verified.
Don't follow steps. Define success and iterate.
Strong success criteria let you loop independently.


## Rule 5 — Surface conflicts, don't average them
If two patterns contradict, pick one (more recent / more tested).
Explain why. Flag the other for cleanup.
Don't blend conflicting patterns.

## Rule 6 — Read before you write
Before adding code, read exports, immediate callers, shared utilities.
"Looks orthogonal" is dangerous. If unsure why code is structured a way, ask.

## Rule 7 — Tests verify intent, not just behavior
Tests must encode WHY behavior matters, not just WHAT it does.
A test that can't fail when business logic changes is wrong.

## Rule 8 — Checkpoint after every significant step
Summarize what was done, what's verified, what's left.
Don't continue from a state you can't describe back.
If you lose track, stop and restate.

## Rule 9 — Match the codebase's conventions, even if you disagree
Conformance > taste inside the codebase.
If you genuinely think a convention is harmful, surface it. Don't fork silently.

## Rule 10 — Fail loud
"Completed" is wrong if anything was skipped silently.
"Tests pass" is wrong if any were skipped.
Default to surfacing uncertainty, not hiding it.
