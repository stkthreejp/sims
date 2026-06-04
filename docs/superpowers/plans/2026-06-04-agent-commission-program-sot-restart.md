# Agent Commission Program SOT Restart Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the in-progress Phase 3 Agent Commission Program SOT slice so agent commission rows store canonical Program setup references and reject drift at service and database boundaries.

**Architecture:** Follow the pattern from Carrier Commissions, Policy Numbers, Proposal Documents, Policy Packages, Surplus Lines, Bordereaux, and Fees. Agent commission remains agent-specific, but any Program-scoped row must resolve to exactly one canonical Program setup level: Program, Program/Carrier, Program/Carrier/LOB, or Program/Carrier/LOB/State.

**Tech Stack:** ASP.NET Core 8, EF Core migrations, PostgreSQL trigger/check-constraint validation, xUnit service and migration tests, React TypeScript DTO types.

---

## Current State From Repo Reconstruction

- `origin/main` is at `2b8ca0a feat: enforce carrier commission program setup scope`.
- GitHub has no open issues and only two old merged PRs; this work has been committed directly to `main`.
- The spec says the next target is Agent Commissions: `docs/superpowers/specs/2026-05-30-program-sot-database-contract-design.md`.
- In-progress Agent Commission files are modified locally:
  - `backend/src/SIMS.Application/DTOs/AgentCommissionDtos.cs`
  - `backend/src/SIMS.Application/Services/AgentCommissionService.cs`
  - `backend/src/SIMS.Domain/Entities/AgentCommission.cs`
  - `backend/src/SIMS.Infrastructure/Data/Configurations/AgentCommissionConfiguration.cs`
  - `backend/tests/SIMS.Application.Tests/Services/CommissionProgramScopeTests.cs`
- In-progress Agent Commission test file is untracked:
  - `backend/tests/SIMS.Application.Tests/Infrastructure/AgentCommissionProgramScopeMigrationTests.cs`
- Expected migration is missing:
  - `backend/src/SIMS.Infrastructure/Migrations/*_AddAgentCommissionProgramScopeRefs.cs`
  - `backend/src/SIMS.Infrastructure/Migrations/*_AddAgentCommissionProgramScopeRefs.Designer.cs`
- Frontend design-system files are also modified but appear unrelated to Program SOT. Leave them unstaged unless the user explicitly asks to handle them.

## Success Criteria

- Agent commission service tests pass for Program/Carrier, Program/Carrier/LOB, and Program/Carrier/LOB/State rows.
- Migration tests pass for check constraint, preflight SQL, backfill SQL, canonical validation triggers, and reverse Program setup identity triggers.
- `dotnet build` passes from `backend`.
- `npx tsc --noEmit` passes from `frontend` if Agent Commission frontend types are changed.
- Only Agent Commission Program SOT files are staged and committed for this slice.

---

### Task 1: Verify Current Failing Point

**Files:**
- Read: `backend/tests/SIMS.Application.Tests/Infrastructure/AgentCommissionProgramScopeMigrationTests.cs`
- Read: `backend/tests/SIMS.Application.Tests/Services/CommissionProgramScopeTests.cs`

- [ ] **Step 1: Run focused Agent Commission tests**

Run:

```powershell
dotnet test backend/tests/SIMS.Application.Tests/SIMS.Application.Tests.csproj --filter "FullyQualifiedName~CommissionProgramScopeTests|FullyQualifiedName~AgentCommissionProgramScopeMigrationTests"
```

Expected before finishing migration: fail because `AddAgentCommissionProgramScopeRefs` does not exist.

- [ ] **Step 2: Confirm no missing generated migration is already tracked**

Run:

```powershell
git ls-files backend/src/SIMS.Infrastructure/Migrations/*AgentCommissionProgramScopeRefs*
```

Expected before migration generation: no output.

---

### Task 2: Complete Agent Commission DTO and Frontend Type Shape

**Files:**
- Modify: `frontend/src/types/agentCommission.types.ts`
- Already modified: `backend/src/SIMS.Application/DTOs/AgentCommissionDtos.cs`

- [ ] **Step 1: Add canonical IDs to frontend AgentCommission type**

Add these fields immediately after `stateCode`:

```ts
  programCarrierId: string | null
  programCarrierLineOfBusinessId: string | null
  programCarrierLobStateId: string | null
```

- [ ] **Step 2: Verify frontend typecheck**

Run:

```powershell
Push-Location frontend; npx tsc --noEmit; Pop-Location
```

Expected: TypeScript succeeds, or any failures are unrelated pre-existing frontend design-system edits and must be reported separately.

---

### Task 3: Generate Agent Commission Program Scope Migration

**Files:**
- Create: `backend/src/SIMS.Infrastructure/Migrations/<timestamp>_AddAgentCommissionProgramScopeRefs.cs`
- Create: `backend/src/SIMS.Infrastructure/Migrations/<timestamp>_AddAgentCommissionProgramScopeRefs.Designer.cs`
- Modify: `backend/src/SIMS.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate EF migration**

Run:

```powershell
Push-Location backend; ~/.dotnet/tools/dotnet-ef migrations add AddAgentCommissionProgramScopeRefs --project src/SIMS.Infrastructure --startup-project src/SIMS.API; Pop-Location
```

Expected: EF creates migration files and updates `ApplicationDbContextModelSnapshot.cs`.

- [ ] **Step 2: Compare generated migration against Carrier Commission migration**

Use `backend/src/SIMS.Infrastructure/Migrations/20260603194109_AddCarrierCommissionProgramScopeRefs.cs` as the closest reference. Agent Commission must include the additional state-specific `ProgramCarrierLobStateId` path.

---

### Task 4: Add Migration Preflight, Backfill, and Triggers

**Files:**
- Modify: generated `backend/src/SIMS.Infrastructure/Migrations/<timestamp>_AddAgentCommissionProgramScopeRefs.cs`
- Test: `backend/tests/SIMS.Application.Tests/Infrastructure/AgentCommissionProgramScopeMigrationTests.cs`

- [ ] **Step 1: Add normalization SQL**

The migration `Up` SQL must normalize legacy loose fields:

```sql
UPDATE "agent_commissions"
SET "LineOfBusiness" = NULLIF(TRIM("LineOfBusiness"), '');

UPDATE "agent_commissions"
SET "StateCode" = NULLIF(UPPER(TRIM("StateCode")), '');
```

- [ ] **Step 2: Add preflight SQL**

The migration must fail before partial backfill when rows are impossible to canonicalize. Include these failure messages because the migration test asserts them:

```text
unsupported LineOfBusiness value
inactive or deleted Program
Program-scoped agent commissions cannot skip carrier or LOB levels before state
Program/Carrier agent commission has no matching active ProgramCarrier path
Program/Carrier/LOB agent commission has no matching active ProgramCarrierLineOfBusiness path
Program/Carrier/LOB/State agent commission has no matching active ProgramCarrierLobState path
```

- [ ] **Step 3: Add backfill SQL**

Backfill canonical fields by matching effective-dated active Program setup paths:

```sql
SET "ProgramCarrierId" = pc."Id"
SET "ProgramCarrierLineOfBusinessId" = pcl."Id"
SET "ProgramCarrierLobStateId" = pcs."Id"
pc."EffectiveDate" <= c."EffectiveDate"
pcl."EffectiveDate" <= c."EffectiveDate"
pcs."EffectiveDate" <= c."EffectiveDate"
```

- [ ] **Step 4: Add canonical validation trigger**

Create `validate_agent_commission_program_scope()` and `trg_validate_agent_commission_program_scope`. It must reject canonical ID mismatches with the messages asserted by `AgentCommissionProgramScopeMigrationTests`.

- [ ] **Step 5: Add reverse Program setup identity triggers**

Create `validate_existing_agent_commission_program_scopes()` and triggers:

```text
trg_validate_agent_commissions_after_program_carrier_change
trg_validate_agent_commissions_after_program_lob_change
trg_validate_agent_commissions_after_program_state_change
```

They must reject Program setup identity changes that would invalidate existing agent commission canonical references.

---

### Task 5: Verify Service Behavior

**Files:**
- Already modified: `backend/src/SIMS.Application/Services/AgentCommissionService.cs`
- Already modified: `backend/tests/SIMS.Application.Tests/Services/CommissionProgramScopeTests.cs`

- [ ] **Step 1: Run focused service tests**

Run:

```powershell
dotnet test backend/tests/SIMS.Application.Tests/SIMS.Application.Tests.csproj --filter "FullyQualifiedName~CommissionProgramScopeTests"
```

Expected: pass.

- [ ] **Step 2: Inspect service scope behavior**

Confirm the service stores:

```text
Program/Carrier rows -> ProgramCarrierId only
Program/Carrier/LOB rows -> ProgramCarrierLineOfBusinessId only
Program/Carrier/LOB/State rows -> ProgramCarrierLobStateId only
Program-only rows -> no lower canonical scope IDs
Global rows -> no Program scope IDs
```

---

### Task 6: Verify Migration Behavior

**Files:**
- Test: `backend/tests/SIMS.Application.Tests/Infrastructure/AgentCommissionProgramScopeMigrationTests.cs`

- [ ] **Step 1: Run focused migration tests**

Run:

```powershell
dotnet test backend/tests/SIMS.Application.Tests/SIMS.Application.Tests.csproj --filter "FullyQualifiedName~AgentCommissionProgramScopeMigrationTests"
```

Expected: pass.

- [ ] **Step 2: Run backend build**

Run:

```powershell
Push-Location backend; dotnet build; Pop-Location
```

Expected: build succeeds.

---

### Task 7: Update Spec Status and Commit

**Files:**
- Modify: `docs/superpowers/specs/2026-05-30-program-sot-database-contract-design.md`

- [ ] **Step 1: Update Implementation Status**

Add Agent Commissions as implemented and set the next target to Rating Assignments or Intermediary/brokerage setup, whichever is chosen next.

- [ ] **Step 2: Stage only Agent Commission Program SOT files**

Run:

```powershell
git add backend/src/SIMS.Application/DTOs/AgentCommissionDtos.cs backend/src/SIMS.Application/Services/AgentCommissionService.cs backend/src/SIMS.Domain/Entities/AgentCommission.cs backend/src/SIMS.Infrastructure/Data/Configurations/AgentCommissionConfiguration.cs backend/src/SIMS.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs backend/src/SIMS.Infrastructure/Migrations/*_AddAgentCommissionProgramScopeRefs.cs backend/src/SIMS.Infrastructure/Migrations/*_AddAgentCommissionProgramScopeRefs.Designer.cs backend/tests/SIMS.Application.Tests/Infrastructure/AgentCommissionProgramScopeMigrationTests.cs backend/tests/SIMS.Application.Tests/Services/CommissionProgramScopeTests.cs frontend/src/types/agentCommission.types.ts docs/superpowers/specs/2026-05-30-program-sot-database-contract-design.md
```

- [ ] **Step 3: Commit directly to main**

Run:

```powershell
git commit -m "feat: enforce agent commission program setup scope"
```

- [ ] **Step 4: Push main**

Run:

```powershell
git push origin main
```

Expected: push succeeds. Do not stage unrelated frontend design-system edits.

---

## Next Slice After Agent Commissions

Choose one:

1. Rating Assignments, if existing rating assignment scope is already Program/Carrier/LOB shaped and can be finished with a narrow canonical-reference slice.
2. Intermediary/brokerage setup, if brokerage/reporting setup is more urgent for operations.

Do not start Phase 4 transaction snapshots until all Phase 3 setup surfaces are either implemented or explicitly deferred.
