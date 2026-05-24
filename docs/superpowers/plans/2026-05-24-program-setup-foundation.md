# Program Setup Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first Phase 7 Program setup slice: nested `Program > Carrier > LOB > State` setup, plus Program-aware fee scoping.

**Architecture:** Extend the existing Program Configuration service/API instead of creating a parallel setup module. Add child setup entities under `ProgramConfiguration`, keep downstream assignments deferred, and add optional `ProgramConfigurationId` to fee rules so in-house fees can vary by Program while existing all-program fee rules keep working.

**Tech Stack:** ASP.NET Core 8, EF Core 8, PostgreSQL migrations, xUnit, React, TypeScript, Vite, TanStack Query.

---

## File Structure

- Modify `backend/src/SIMS.Domain/Entities/ProgramConfiguration.cs`: add navigation to nested setup records.
- Create `backend/src/SIMS.Domain/Entities/ProgramCarrier.cs`: Program-to-Carrier participation.
- Create `backend/src/SIMS.Domain/Entities/ProgramCarrierLineOfBusiness.cs`: Carrier LOB setup under Program.
- Create `backend/src/SIMS.Domain/Entities/ProgramCarrierLobState.cs`: state setup under Program Carrier LOB.
- Modify `backend/src/SIMS.Domain/Entities/Accounting/FeeRuleVersion.cs`: add nullable `ProgramConfigurationId`.
- Create EF configurations under `backend/src/SIMS.Infrastructure/Data/Configurations/`.
- Modify `backend/src/SIMS.Infrastructure/Data/ApplicationDbContext.cs`: add DbSets and query filters.
- Add EF migration under `backend/src/SIMS.Infrastructure/Migrations/`: create nested setup tables and add fee rule Program FK/index.
- Modify `backend/src/SIMS.Application/DTOs/Underwriting/ProgramConfigurationDtos.cs`: add nested DTOs and upsert/copy requests.
- Modify `backend/src/SIMS.Application/Interfaces/Services/IProgramConfigurationService.cs`: add methods for nested setup.
- Modify `backend/src/SIMS.Application/Services/ProgramConfigurationService.cs`: implement nested CRUD and copy-state behavior.
- Modify `backend/src/SIMS.API/Controllers/Admin/ProgramConfigurationsController.cs`: add nested endpoints.
- Modify `backend/src/SIMS.Application/DTOs/Accounting/FeeDtos.cs`: expose Program on fee rules.
- Modify `backend/src/SIMS.Application/DTOs/Accounting/FeeCalculationDtos.cs`: add `Guid? ProgramConfigurationId`.
- Modify `backend/src/SIMS.Application/Services/FeeAdminService.cs`: map/build Program fee scope.
- Modify `backend/src/SIMS.Application/Services/FeeCalculationService.cs`: match Program-specific fees and prefer them over all-program defaults.
- Modify `backend/src/SIMS.Infrastructure/Data/Configurations/Accounting/FeeRuleVersionConfiguration.cs`: configure Program FK/index.
- Modify `backend/tests/SIMS.Application.Tests/Services/ProgramConfigurationServiceTests.cs`: add nested setup service tests.
- Create `backend/tests/SIMS.Application.Tests/Services/FeeCalculationServiceTests.cs`: add Program-specific fee resolution tests.
- Modify `frontend/src/types/programConfiguration.types.ts`: add nested setup types.
- Modify `frontend/src/api/programConfigurations.api.ts`: add nested endpoints.
- Modify `frontend/src/pages/admin/ProgramConfigurationAdminPage.tsx`: add nested Program setup UI.
- Modify `frontend/src/types/fee.types.ts`: add `programConfigurationId`.
- Modify `frontend/src/pages/admin/FeesAdminPage.tsx`: add Program selector to fee rule scope.

---

### Task 1: Backend Nested Program Setup Model

**Files:**
- Modify: `backend/src/SIMS.Domain/Entities/ProgramConfiguration.cs`
- Create: `backend/src/SIMS.Domain/Entities/ProgramCarrier.cs`
- Create: `backend/src/SIMS.Domain/Entities/ProgramCarrierLineOfBusiness.cs`
- Create: `backend/src/SIMS.Domain/Entities/ProgramCarrierLobState.cs`
- Create: `backend/src/SIMS.Infrastructure/Data/Configurations/ProgramCarrierConfiguration.cs`
- Create: `backend/src/SIMS.Infrastructure/Data/Configurations/ProgramCarrierLineOfBusinessConfiguration.cs`
- Create: `backend/src/SIMS.Infrastructure/Data/Configurations/ProgramCarrierLobStateConfiguration.cs`
- Modify: `backend/src/SIMS.Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Add failing tests for nested setup creation and duplicate prevention**

Add these tests to `backend/tests/SIMS.Application.Tests/Services/ProgramConfigurationServiceTests.cs`:

```csharp
[Fact]
public async Task AddCarrierAsync_AddsCarrierUnderProgram()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = new Carrier { Name = "Falls Lake", IsActive = true };
    db.AddRange(program, carrier);
    await db.SaveChangesAsync();

    var service = new ProgramConfigurationService(db);
    var result = await service.AddCarrierAsync(program.Id, new UpsertProgramCarrierRequest(
        CarrierId: carrier.Id,
        IsActive: true,
        EffectiveDate: new DateOnly(2026, 1, 1),
        ExpirationDate: null,
        Notes: "Primary carrier"));

    Assert.True(result.IsSuccess);
    Assert.Equal(program.Id, result.Value!.ProgramConfigurationId);
    Assert.Equal(carrier.Id, result.Value.CarrierId);
    Assert.Equal("Falls Lake", result.Value.CarrierName);
}

[Fact]
public async Task AddCarrierAsync_RejectsDuplicateCarrierForSameProgram()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = new Carrier { Name = "Falls Lake", IsActive = true };
    db.AddRange(program, carrier);
    await db.SaveChangesAsync();

    var service = new ProgramConfigurationService(db);
    var request = new UpsertProgramCarrierRequest(carrier.Id, true, new DateOnly(2026, 1, 1), null, null);

    await service.AddCarrierAsync(program.Id, request);
    var duplicate = await service.AddCarrierAsync(program.Id, request);

    Assert.False(duplicate.IsSuccess);
    Assert.Equal("PROGRAM_CARRIER_DUPLICATE", duplicate.ErrorCode);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:

```powershell
dotnet test backend\tests\SIMS.Application.Tests\SIMS.Application.Tests.csproj --filter FullyQualifiedName~ProgramConfigurationServiceTests
```

Expected: fails because `UpsertProgramCarrierRequest` and `AddCarrierAsync` do not exist yet.

- [ ] **Step 3: Add domain entities and EF mappings**

Add `ProgramCarrier`, `ProgramCarrierLineOfBusiness`, and `ProgramCarrierLobState` with `BaseEntity`, parent FK, active flag, effective/expiration dates, notes, and navigation properties. Configure unique indexes:

```csharp
builder.HasIndex(x => new { x.ProgramConfigurationId, x.CarrierId }).IsUnique();
builder.HasIndex(x => new { x.ProgramCarrierId, x.LineOfBusiness }).IsUnique();
builder.HasIndex(x => new { x.ProgramCarrierLineOfBusinessId, x.StateCode }).IsUnique();
```

Use cascade delete from Program to Carrier setup, from Carrier setup to LOB setup, and from LOB setup to State setup.

- [ ] **Step 4: Add DbSets and query filters**

Add DbSets for the three new entities in `ApplicationDbContext`, plus soft-delete query filters:

```csharp
builder.Entity<ProgramCarrier>().HasQueryFilter(e => !e.IsDeleted);
builder.Entity<ProgramCarrierLineOfBusiness>().HasQueryFilter(e => !e.IsDeleted);
builder.Entity<ProgramCarrierLobState>().HasQueryFilter(e => !e.IsDeleted);
```

- [ ] **Step 5: Add DTOs and service contract methods**

Add DTOs:

```csharp
public record ProgramCarrierDto(Guid Id, Guid ProgramConfigurationId, Guid CarrierId, string CarrierName, bool IsActive, DateOnly EffectiveDate, DateOnly? ExpirationDate, string? Notes, IReadOnlyList<ProgramCarrierLineOfBusinessDto> LinesOfBusiness);
public record ProgramCarrierLineOfBusinessDto(Guid Id, Guid ProgramCarrierId, PolicyLineOfBusiness LineOfBusiness, string LineOfBusinessLabel, bool IsActive, DateOnly EffectiveDate, DateOnly? ExpirationDate, string? Notes, IReadOnlyList<ProgramCarrierLobStateDto> States);
public record ProgramCarrierLobStateDto(Guid Id, Guid ProgramCarrierLineOfBusinessId, string StateCode, bool IsActive, DateOnly EffectiveDate, DateOnly? ExpirationDate, string? Notes);
public record UpsertProgramCarrierRequest(Guid CarrierId, bool IsActive, DateOnly EffectiveDate, DateOnly? ExpirationDate, string? Notes);
public record UpsertProgramCarrierLineOfBusinessRequest(PolicyLineOfBusiness LineOfBusiness, bool IsActive, DateOnly EffectiveDate, DateOnly? ExpirationDate, string? Notes);
public record UpsertProgramCarrierLobStateRequest(string StateCode, bool IsActive, DateOnly EffectiveDate, DateOnly? ExpirationDate, string? Notes);
public record CopyProgramCarrierLobStateRequest(string SourceStateCode, string TargetStateCode);
```

Add service methods for add/update carrier, LOB, state, and copy state.

- [ ] **Step 6: Implement service methods**

Implement minimal validation:

```csharp
if (expirationDate.HasValue && expirationDate.Value < effectiveDate)
    return Result<T>.Failure("INVALID_DATE_RANGE", "Expiration date cannot be before effective date.");
```

Normalize state code to two uppercase characters, trim notes to null, reject missing parent records, and reject duplicates within the same parent.

- [ ] **Step 7: Run Program service tests**

Run:

```powershell
dotnet test backend\tests\SIMS.Application.Tests\SIMS.Application.Tests.csproj --filter FullyQualifiedName~ProgramConfigurationServiceTests
```

Expected: all Program configuration tests pass.

---

### Task 2: Program-Aware Fee Scoping

**Files:**
- Modify: `backend/src/SIMS.Domain/Entities/Accounting/FeeRuleVersion.cs`
- Modify: `backend/src/SIMS.Application/DTOs/Accounting/FeeDtos.cs`
- Modify: `backend/src/SIMS.Application/DTOs/Accounting/FeeCalculationDtos.cs`
- Modify: `backend/src/SIMS.Application/Services/FeeAdminService.cs`
- Modify: `backend/src/SIMS.Application/Services/FeeCalculationService.cs`
- Modify: `backend/src/SIMS.Infrastructure/Data/Configurations/Accounting/FeeRuleVersionConfiguration.cs`
- Create: `backend/tests/SIMS.Application.Tests/Services/FeeCalculationServiceTests.cs`

- [ ] **Step 1: Add failing fee calculation tests**

Create tests that seed one all-program fee and one Longleaf-specific fee for the same fee definition, then calculate with Longleaf:

```csharp
[Fact]
public async Task CalculateAsync_PrefersProgramSpecificFeeRuleOverAllProgramDefault()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var fee = new FeeDefinition { Code = "MGA", DisplayName = "MGA Fee", FeeCategory = "PolicyFee", IsTaxable = false, CalculationOrder = 100, LedgerAccountId = 1 };
    db.AddRange(program, fee);
    await db.SaveChangesAsync();

    db.AddRange(
        BuildFlatFeeRule(fee.Id, null, 25m),
        BuildFlatFeeRule(fee.Id, program.Id, 75m));
    await db.SaveChangesAsync();

    var service = new FeeCalculationService(new TestServiceProvider(db));
    var result = await service.CalculateAsync(new PolicyContext(
        EffectiveDate: new DateOnly(2026, 1, 1),
        GrossPremium: 1000m,
        StateCode: "TX",
        IsEndorsement: false,
        IsFilingState: true,
        CarrierId: null,
        CompanyId: null,
        ProducerId: null,
        LineOfBusiness: "InlandMarine",
        City: null,
        LicenseType: "Non-Admitted",
        ProgramConfigurationId: program.Id));

    var line = Assert.Single(result.Lines);
    Assert.Equal(75m, line.Amount);
}
```

- [ ] **Step 2: Run fee tests to verify failure**

Run:

```powershell
dotnet test backend\tests\SIMS.Application.Tests\SIMS.Application.Tests.csproj --filter FullyQualifiedName~FeeCalculationServiceTests
```

Expected: fails because fee rule Program scope and context do not exist.

- [ ] **Step 3: Add Program scope to fee rule model and DTOs**

Add `Guid? ProgramConfigurationId` to `FeeRuleVersion`, `FeeRuleVersionDto`, and `CreateFeeRuleVersionRequest`. Add `Guid? ProgramConfigurationId` to `PolicyContext`.

- [ ] **Step 4: Update fee admin mapping/building**

In `MapVersion`, return `ProgramConfigurationId: v.ProgramConfigurationId`. In `BuildVersion`, set `ProgramConfigurationId = req.ProgramConfigurationId`.

- [ ] **Step 5: Update fee calculation matching and specificity**

Candidate rule matching must include:

```csharp
(v.ProgramConfigurationId == null || v.ProgramConfigurationId == ctx.ProgramConfigurationId)
```

Specificity must include Program:

```csharp
(v.ProgramConfigurationId != null ? 1 : 0)
```

- [ ] **Step 6: Configure EF relationship/index**

Configure optional Program relationship on fee rule versions with restrict delete and an index:

```csharp
b.HasOne(x => x.ProgramConfiguration)
    .WithMany()
    .HasForeignKey(x => x.ProgramConfigurationId)
    .OnDelete(DeleteBehavior.Restrict);

b.HasIndex(x => new { x.FeeDefinitionId, x.ProgramConfigurationId, x.CarrierId, x.LineOfBusiness, x.StateCode, x.EffectiveDate })
    .HasDatabaseName("ix_fee_rule_program_carrier_lob_lookup");
```

- [ ] **Step 7: Run fee tests**

Run:

```powershell
dotnet test backend\tests\SIMS.Application.Tests\SIMS.Application.Tests.csproj --filter FullyQualifiedName~FeeCalculationServiceTests
```

Expected: fee calculation tests pass.

---

### Task 3: Admin API Endpoints And Migration

**Files:**
- Modify: `backend/src/SIMS.API/Controllers/Admin/ProgramConfigurationsController.cs`
- Add: `backend/src/SIMS.Infrastructure/Migrations/<timestamp>_AddProgramSetupFoundation.cs`
- Modify: `backend/src/SIMS.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Add nested API endpoints**

Add endpoints under the existing controller:

```csharp
[HttpPost("{programId:guid}/carriers")]
public async Task<IActionResult> AddCarrier(Guid programId, [FromBody] UpsertProgramCarrierRequest request, CancellationToken ct)

[HttpPut("{programId:guid}/carriers/{programCarrierId:guid}")]
public async Task<IActionResult> UpdateCarrier(Guid programId, Guid programCarrierId, [FromBody] UpsertProgramCarrierRequest request, CancellationToken ct)

[HttpPost("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business")]
public async Task<IActionResult> AddLineOfBusiness(Guid programId, Guid programCarrierId, [FromBody] UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct)

[HttpPut("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}")]
public async Task<IActionResult> UpdateLineOfBusiness(Guid programId, Guid programCarrierId, Guid programCarrierLobId, [FromBody] UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct)

[HttpPost("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}/states")]
public async Task<IActionResult> AddState(Guid programId, Guid programCarrierId, Guid programCarrierLobId, [FromBody] UpsertProgramCarrierLobStateRequest request, CancellationToken ct)

[HttpPut("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}/states/{stateId:guid}")]
public async Task<IActionResult> UpdateState(Guid programId, Guid programCarrierId, Guid programCarrierLobId, Guid stateId, [FromBody] UpsertProgramCarrierLobStateRequest request, CancellationToken ct)

[HttpPost("{programId:guid}/carriers/{programCarrierId:guid}/lines-of-business/{programCarrierLobId:guid}/states/copy")]
public async Task<IActionResult> CopyState(Guid programId, Guid programCarrierId, Guid programCarrierLobId, [FromBody] CopyProgramCarrierLobStateRequest request, CancellationToken ct)
```

- [ ] **Step 2: Add EF migration**

Run:

```powershell
dotnet ef migrations add AddProgramSetupFoundation --project backend\src\SIMS.Infrastructure --startup-project backend\src\SIMS.API
```

Expected: migration creates the three setup tables, foreign keys, unique indexes, and adds `ProgramConfigurationId` to `fee_rule_versions`.

- [ ] **Step 3: Build backend**

Run:

```powershell
dotnet build backend\src\SIMS.API\SIMS.API.csproj
```

Expected: build succeeds.

---

### Task 4: Program Setup Admin UI

**Files:**
- Modify: `frontend/src/types/programConfiguration.types.ts`
- Modify: `frontend/src/api/programConfigurations.api.ts`
- Modify: `frontend/src/pages/admin/ProgramConfigurationAdminPage.tsx`

- [ ] **Step 1: Add frontend types**

Add nested types matching the backend DTOs:

```ts
export interface ProgramCarrier {
  id: string
  programConfigurationId: string
  carrierId: string
  carrierName: string
  isActive: boolean
  effectiveDate: string
  expirationDate: string | null
  notes: string | null
  linesOfBusiness: ProgramCarrierLineOfBusiness[]
}
```

- [ ] **Step 2: Add frontend API functions**

Add functions to create/update carriers, LOBs, states, and copy state setup using the endpoint paths from Task 3.

- [ ] **Step 3: Add nested UI to Program admin page**

Keep the existing program create/edit panel. Add a selected Program details area that shows:

```text
Program
  Carrier
    Line of Business
      State
```

Include add/edit controls for each level, active checkbox, effective date, expiration date, notes, and copy-state form at the state level.

- [ ] **Step 4: Run frontend type check**

Run:

```powershell
cd frontend
npx tsc --noEmit
```

Expected: type check succeeds.

---

### Task 5: Fees Admin Program Selector

**Files:**
- Modify: `frontend/src/types/fee.types.ts`
- Modify: `frontend/src/pages/admin/FeesAdminPage.tsx`

- [ ] **Step 1: Add `programConfigurationId` to fee types**

Add:

```ts
programConfigurationId: string | null
```

to `FeeRuleVersion`.

- [ ] **Step 2: Load active programs in Fees admin**

Use `programConfigurationsApi.getAll(true)` in `FeesAdminPage` and add a Program selector in the scope section of the fee rule form. Empty value means all programs.

- [ ] **Step 3: Include Program when creating versions**

Make sure `programConfigurationId` is included in `initialVersionForm`, edit loading, create, and new-version payloads.

- [ ] **Step 4: Run frontend type check**

Run:

```powershell
cd frontend
npx tsc --noEmit
```

Expected: type check succeeds.

---

### Task 6: Final Verification

**Files:**
- No additional files unless verification finds a defect.

- [ ] **Step 1: Run targeted backend tests**

Run:

```powershell
dotnet test backend\tests\SIMS.Application.Tests\SIMS.Application.Tests.csproj --filter "FullyQualifiedName~ProgramConfigurationServiceTests|FullyQualifiedName~FeeCalculationServiceTests"
```

Expected: all targeted tests pass.

- [ ] **Step 2: Run backend build**

Run:

```powershell
dotnet build backend\src\SIMS.API\SIMS.API.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Run frontend type check**

Run:

```powershell
cd frontend
npx tsc --noEmit
```

Expected: type check succeeds.

- [ ] **Step 4: Review diff for scope**

Confirm the diff only covers Program setup foundation, Program-aware fee scoping, related tests, migration, and admin UI changes.
