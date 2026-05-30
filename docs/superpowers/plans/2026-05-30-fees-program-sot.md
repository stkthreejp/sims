# Fees Program SOT Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make fee rule versions use Program setup as the source of truth for Program-scoped fee paths while preserving current global fee behavior.

**Architecture:** Keep the existing loose fee scope columns for search, display, and fee calculation compatibility, but add canonical Program-path foreign keys for Program/Carrier, Program/Carrier/LOB, and Program/Carrier/LOB/State scopes. Fee admin save paths resolve and stamp the canonical IDs server-side; the database rejects Program-scoped child rows that are not attached to the matching Program setup level and uses PostgreSQL trigger validation to prove the canonical row matches the denormalized Program/Carrier/LOB/State fields. The Fees admin UI becomes a cascading Program setup selector so users cannot choose a carrier, LOB, or state outside the selected Program path.

**Tech Stack:** ASP.NET Core 8, EF Core, PostgreSQL, React, TypeScript, Vite, xUnit, EF Core InMemory and SQLite test providers.

---

## Scope

This plan implements the first coded slice from `docs/superpowers/specs/2026-05-30-program-sot-database-contract-design.md`: Fees Program SOT enforcement.

The slice includes:

- Program-level fee rules using `ProgramConfigurationId`.
- Program/Carrier fee rules using `ProgramCarrierId`.
- Program/Carrier/LOB all-state fee rules using `ProgramCarrierLineOfBusinessId`.
- Program/Carrier/LOB/State fee rules using `ProgramCarrierLobStateId`.
- Existing global, all-program, carrier-only, LOB-only, and state-only fee rules remain valid.
- Existing fee calculation behavior continues using the denormalized scope fields.

The slice excludes:

- Bordereaux profiles.
- Surplus lines setup.
- Historical policy, invoice, or bordereaux snapshots.
- Removing existing fee scope columns.
- Generic Program scope tables.

## Assumptions

- A Program-scoped state fee must select Program, Carrier, LOB, and State because state lives under `ProgramCarrierLineOfBusiness`.
- A Program-scoped LOB fee with no state is an all-state default for that Program/Carrier/LOB.
- A Program-scoped carrier fee with no LOB or state is an all-LOB default for that Program/Carrier.
- Incoming API payloads do not get to choose canonical Program-path IDs directly. The server resolves them from `ProgramConfigurationId`, `CarrierId`, `LineOfBusiness`, `StateCode`, and `EffectiveDate`.
- Existing fee rules without `ProgramConfigurationId` are global or non-Program scoped and do not need canonical Program-path IDs.

## Agent Review Corrections

These corrections supersede the initial task snippets below where they differ. They came from the read-only specialist review run after the first draft.

- The canonical FK check constraint is shape-only. The migration must also add PostgreSQL trigger validation so `ProgramCarrierId`, `ProgramCarrierLineOfBusinessId`, and `ProgramCarrierLobStateId` are joined back to Program setup and proven to match the same `ProgramConfigurationId`, `CarrierId`, `LineOfBusiness`, and `StateCode` stored on the fee rule.
- Migration backfill must be effective-date aware. Every ProgramCarrier, ProgramCarrierLineOfBusiness, and ProgramCarrierLobState join must require `EffectiveDate <= fee_rule_versions."EffectiveDate"` and `(ExpirationDate IS NULL OR ExpirationDate >= fee_rule_versions."EffectiveDate")`.
- Migration preflight must normalize `StateCode = UPPER(TRIM(StateCode))` for Program-scoped fee rows, reject unsupported `LineOfBusiness` values before backfill, and fail clearly when a Program-scoped row cannot be resolved to an active Program path for the fee effective date.
- Service validation must normalize and validate `LineOfBusiness` whenever it is provided, not only for Program-scoped rows. Store enum `ToString()` values so fee calculation matches consistently.
- Service validation should use the existing state error convention: `STATE_CODE_INVALID` with message `State code must be two characters.`
- Tests must include wrong-Program and wrong-parent paths, not only missing paths.
- Tests must include a relational mismatch case where a canonical ID points at a different Program path and the database rejects it.
- Tests must include migration/preflight coverage for date-window matching and state normalization.
- The fee calculator tests must assert the selected `FeeRuleVersionId`, not just the calculated amount.
- Frontend save blocking must test missing parents, not scope depth. A complete Program/Carrier/LOB all-state rule and a complete Program/Carrier/LOB/State rule must be saveable.
- Frontend selector filtering must respect Program setup effective/expiration dates once `form.effectiveDate` is set.
- Program setup currently has path-only unique constraints and cannot represent multiple historical intervals for the same Program path. Do not solve that in this Fees slice; document it as a follow-up identity/versioning task before claiming full historical Program setup versioning.

## Files

- Modify: `backend/src/SIMS.Domain/Entities/Accounting/FeeRuleVersion.cs`
  Add canonical Program-path nullable foreign key properties and navigation properties.

- Modify: `backend/src/SIMS.Infrastructure/Data/Configurations/Accounting/FeeRuleVersionConfiguration.cs`
  Configure canonical foreign keys, lookup indexes, and a check constraint that requires canonical IDs for Program-scoped child rows.

- Create: `backend/src/SIMS.Infrastructure/Migrations/*_AddFeeRuleProgramScopeRefs.cs`
  EF migration generated by the migration command, then edited to include deterministic backfill SQL and preflight failures for unsafe existing data.

- Modify: `backend/src/SIMS.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`
  Generated by EF migration.

- Modify: `backend/src/SIMS.Application/Services/FeeAdminService.cs`
  Resolve canonical Program-path IDs, validate active paths by effective date, normalize state codes, and stamp canonical IDs on new versions.

- Modify: `backend/tests/SIMS.Application.Tests/Services/FeeAdminProgramScopeTests.cs`
  Add service validation tests and a SQLite database-constraint regression test.

- Modify: `backend/tests/SIMS.Application.Tests/Services/FeeCalculationServiceTests.cs`
  Add a fee resolution regression test for state-specific Program fees overriding all-state LOB defaults.

- Create: `backend/tests/SIMS.Application.Tests/Infrastructure/FeeRuleProgramScopeMigrationTests.cs`
  Add migration SQL/preflight coverage for trigger validation, date-window backfill, unsupported LOBs, and state normalization.

- Create: `backend/tests/SIMS.Application.Tests/Controllers/FeesControllerProgramScopeTests.cs`
  Add controller-level contract coverage for Program scope failures and normalized successful responses.

- Modify: `frontend/src/pages/admin/FeesAdminPage.tsx`
  Filter Carrier, LOB, and State options from selected Program setup and clear invalid child selections when parents change.

## Task 1: Add Backend Program Scope Tests

**Files:**
- Modify: `backend/tests/SIMS.Application.Tests/Services/FeeAdminProgramScopeTests.cs`
- Modify: `backend/tests/SIMS.Application.Tests/Services/FeeCalculationServiceTests.cs`

- [ ] **Step 1: Add missing usings to fee admin tests**

In `backend/tests/SIMS.Application.Tests/Services/FeeAdminProgramScopeTests.cs`, add these usings at the top:

```csharp
using Microsoft.Data.Sqlite;
using SIMS.Domain.Enums;
```

- [ ] **Step 2: Add helper methods to fee admin tests**

Add these helpers near the existing `ValidRequest` helper:

```csharp
private static Carrier BuildCarrier(string name = "BRACE") =>
    new()
    {
        Name = name,
        IsActive = true
    };

private static ProgramCarrier BuildProgramCarrier(ProgramConfiguration program, Carrier carrier) =>
    new()
    {
        ProgramConfigurationId = program.Id,
        CarrierId = carrier.Id,
        IsActive = true,
        EffectiveDate = new DateOnly(2026, 1, 1)
    };

private static ProgramCarrierLineOfBusiness BuildProgramLob(ProgramCarrier programCarrier, PolicyLineOfBusiness lob) =>
    new()
    {
        ProgramCarrierId = programCarrier.Id,
        LineOfBusiness = lob,
        IsActive = true,
        EffectiveDate = new DateOnly(2026, 1, 1)
    };

private static ProgramCarrierLobState BuildProgramState(ProgramCarrierLineOfBusiness programLob, string stateCode) =>
    new()
    {
        ProgramCarrierLineOfBusinessId = programLob.Id,
        StateCode = stateCode,
        IsActive = true,
        EffectiveDate = new DateOnly(2026, 1, 1)
    };
```

- [ ] **Step 3: Add a service test for rejecting invalid Program paths**

Add this test to `FeeAdminProgramScopeTests`:

```csharp
[Fact]
public async Task CreateVersionAsync_RejectsProgramCarrierLobStateOutsideProgramSetupPath()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = BuildCarrier();
    var fee = new FeeDefinition
    {
        Code = "SL_TAX",
        DisplayName = "Surplus Lines Tax",
        FeeCategory = "Tax",
        IsTaxable = false,
        CalculationOrder = 10,
        LedgerAccountId = 1,
    };
    db.AddRange(program, carrier, fee);
    await db.SaveChangesAsync();

    var request = ValidRequest(fee.Id) with
    {
        ProgramConfigurationId = program.Id,
        CarrierId = carrier.Id,
        LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
        StateCode = "TX"
    };

    var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

    Assert.False(result.IsSuccess);
    Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
}
```

- [ ] **Step 4: Add a service test for all-state LOB canonical scope**

Add this test to `FeeAdminProgramScopeTests`:

```csharp
[Fact]
public async Task CreateVersionAsync_SavesCanonicalLobScopeForProgramCarrierLobAllStates()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = BuildCarrier();
    var fee = new FeeDefinition
    {
        Code = "MGA",
        DisplayName = "MGA Fee",
        FeeCategory = "PolicyFee",
        IsTaxable = false,
        CalculationOrder = 100,
        LedgerAccountId = 1,
    };
    db.AddRange(program, carrier, fee);
    await db.SaveChangesAsync();

    var programCarrier = BuildProgramCarrier(program, carrier);
    db.Add(programCarrier);
    await db.SaveChangesAsync();

    var programLob = BuildProgramLob(programCarrier, PolicyLineOfBusiness.GeneralLiability);
    db.Add(programLob);
    await db.SaveChangesAsync();

    var request = ValidRequest(fee.Id) with
    {
        ProgramConfigurationId = program.Id,
        CarrierId = carrier.Id,
        LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
        StateCode = null,
        CalcType = "Flat",
        FlatAmount = 50m,
        PercentRate = null
    };

    var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

    Assert.True(result.IsSuccess);
    var saved = await db.Set<FeeRuleVersion>().SingleAsync(v => v.Id == result.Value!.Id);
    Assert.Equal(programLob.Id, saved.ProgramCarrierLineOfBusinessId);
    Assert.Null(saved.ProgramCarrierId);
    Assert.Null(saved.ProgramCarrierLobStateId);
    Assert.Equal(program.Id, saved.ProgramConfigurationId);
    Assert.Equal(carrier.Id, saved.CarrierId);
    Assert.Equal(PolicyLineOfBusiness.GeneralLiability.ToString(), saved.LineOfBusiness);
    Assert.Null(saved.StateCode);
}
```

- [ ] **Step 5: Add a service test for state canonical scope**

Add this test to `FeeAdminProgramScopeTests`:

```csharp
[Fact]
public async Task CreateVersionAsync_SavesCanonicalStateScopeForProgramCarrierLobState()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = BuildCarrier();
    var fee = new FeeDefinition
    {
        Code = "SL_TAX",
        DisplayName = "Surplus Lines Tax",
        FeeCategory = "Tax",
        IsTaxable = false,
        CalculationOrder = 10,
        LedgerAccountId = 1,
    };
    db.AddRange(program, carrier, fee);
    await db.SaveChangesAsync();

    var programCarrier = BuildProgramCarrier(program, carrier);
    db.Add(programCarrier);
    await db.SaveChangesAsync();

    var programLob = BuildProgramLob(programCarrier, PolicyLineOfBusiness.GeneralLiability);
    db.Add(programLob);
    await db.SaveChangesAsync();

    var programState = BuildProgramState(programLob, "TX");
    db.Add(programState);
    await db.SaveChangesAsync();

    var request = ValidRequest(fee.Id) with
    {
        ProgramConfigurationId = program.Id,
        CarrierId = carrier.Id,
        LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
        StateCode = "tx"
    };

    var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

    Assert.True(result.IsSuccess);
    var saved = await db.Set<FeeRuleVersion>().SingleAsync(v => v.Id == result.Value!.Id);
    Assert.Equal(programState.Id, saved.ProgramCarrierLobStateId);
    Assert.Null(saved.ProgramCarrierId);
    Assert.Null(saved.ProgramCarrierLineOfBusinessId);
    Assert.Equal("TX", saved.StateCode);
}
```

- [ ] **Step 6: Add a service test for expired Program paths**

Add this test to `FeeAdminProgramScopeTests`:

```csharp
[Fact]
public async Task CreateVersionAsync_RejectsExpiredProgramPathForFeeEffectiveDate()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = BuildCarrier();
    var fee = new FeeDefinition
    {
        Code = "MGA",
        DisplayName = "MGA Fee",
        FeeCategory = "PolicyFee",
        IsTaxable = false,
        CalculationOrder = 100,
        LedgerAccountId = 1,
    };
    db.AddRange(program, carrier, fee);
    await db.SaveChangesAsync();

    var programCarrier = BuildProgramCarrier(program, carrier);
    programCarrier.ExpirationDate = new DateOnly(2026, 1, 31);
    db.Add(programCarrier);
    await db.SaveChangesAsync();

    var request = ValidRequest(fee.Id) with
    {
        ProgramConfigurationId = program.Id,
        CarrierId = carrier.Id,
        LineOfBusiness = null,
        StateCode = null,
        EffectiveDate = new DateOnly(2026, 2, 1),
        CalcType = "Flat",
        FlatAmount = 50m,
        PercentRate = null
    };

    var result = await new FeeAdminService(new TestServiceProvider(db)).CreateVersionAsync(Guid.NewGuid(), request);

    Assert.False(result.IsSuccess);
    Assert.Equal("PROGRAM_SCOPE_PATH_NOT_FOUND", result.ErrorCode);
}
```

- [ ] **Step 7: Add a SQLite check-constraint test**

Add this test to `FeeAdminProgramScopeTests`:

```csharp
[Fact]
public async Task SaveChangesAsync_RejectsProgramCarrierLobScopeWithoutCanonicalReference()
{
    await using var connection = new SqliteConnection("Filename=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var db = new ApplicationDbContext(options);
    await db.Database.EnsureCreatedAsync();

    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = BuildCarrier();
    var fee = new FeeDefinition
    {
        Code = "MGA",
        DisplayName = "MGA Fee",
        FeeCategory = "PolicyFee",
        IsTaxable = false,
        CalculationOrder = 100,
        LedgerAccountId = 1,
    };
    db.AddRange(program, carrier, fee);
    await db.SaveChangesAsync();

    db.Add(new FeeRuleVersion
    {
        FeeDefinitionId = fee.Id,
        ProgramConfigurationId = program.Id,
        CarrierId = carrier.Id,
        LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
        EffectiveDate = new DateOnly(2026, 1, 1),
        CalcType = "Flat",
        FlatAmount = 50m,
        SendToAccounting = true,
        ApplyAutomatically = true,
        InstallmentBehavior = "PerInstallment",
        RoundingMode = "NearestCent",
        PayableRouting = "NotPayable",
        CreatedBy = Guid.NewGuid(),
    });

    await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
}
```

- [ ] **Step 8: Add fee calculation override test**

In `backend/tests/SIMS.Application.Tests/Services/FeeCalculationServiceTests.cs`, add `using SIMS.Domain.Enums;` and this test:

```csharp
[Fact]
public async Task CalculateAsync_PrefersStateSpecificProgramFeeOverLobDefault()
{
    await using var db = CreateDb();
    var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
    var carrier = new Carrier { Name = "BRACE", IsActive = true };
    var fee = new FeeDefinition
    {
        Code = "SL_TAX",
        DisplayName = "Surplus Lines Tax",
        FeeCategory = "Tax",
        IsTaxable = false,
        CalculationOrder = 10,
        LedgerAccountId = 1
    };
    db.AddRange(program, carrier, fee);
    await db.SaveChangesAsync();

    var programCarrier = new ProgramCarrier
    {
        ProgramConfigurationId = program.Id,
        CarrierId = carrier.Id,
        IsActive = true,
        EffectiveDate = new DateOnly(2026, 1, 1)
    };
    db.Add(programCarrier);
    await db.SaveChangesAsync();

    var programLob = new ProgramCarrierLineOfBusiness
    {
        ProgramCarrierId = programCarrier.Id,
        LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
        IsActive = true,
        EffectiveDate = new DateOnly(2026, 1, 1)
    };
    db.Add(programLob);
    await db.SaveChangesAsync();

    var programState = new ProgramCarrierLobState
    {
        ProgramCarrierLineOfBusinessId = programLob.Id,
        StateCode = "TX",
        IsActive = true,
        EffectiveDate = new DateOnly(2026, 1, 1)
    };
    db.Add(programState);
    await db.SaveChangesAsync();

    db.AddRange(
        new FeeRuleVersion
        {
            FeeDefinitionId = fee.Id,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            ProgramCarrierLineOfBusinessId = programLob.Id,
            EffectiveDate = new DateOnly(2026, 1, 1),
            CalcType = "Flat",
            FlatAmount = 40m,
            SendToAccounting = true,
            ApplyAutomatically = true,
            InstallmentBehavior = "PerInstallment",
            RoundingMode = "NearestCent",
            PayableRouting = "NotPayable",
            CreatedBy = Guid.NewGuid()
        },
        new FeeRuleVersion
        {
            FeeDefinitionId = fee.Id,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            StateCode = "TX",
            ProgramCarrierLobStateId = programState.Id,
            EffectiveDate = new DateOnly(2026, 1, 1),
            CalcType = "Flat",
            FlatAmount = 100m,
            SendToAccounting = true,
            ApplyAutomatically = true,
            InstallmentBehavior = "PerInstallment",
            RoundingMode = "NearestCent",
            PayableRouting = "NotPayable",
            CreatedBy = Guid.NewGuid()
        });
    await db.SaveChangesAsync();

    var service = new FeeCalculationService(new TestServiceProvider(db));
    var result = await service.CalculateAsync(new PolicyContext(
        EffectiveDate: new DateOnly(2026, 1, 1),
        GrossPremium: 1000m,
        StateCode: "TX",
        IsEndorsement: false,
        IsFilingState: true,
        CarrierId: carrier.Id,
        CompanyId: null,
        ProducerId: null,
        LineOfBusiness: PolicyLineOfBusiness.GeneralLiability.ToString(),
        City: null,
        LicenseType: "Non-Admitted",
        ProgramConfigurationId: program.Id));

    var line = Assert.Single(result.Lines);
    Assert.Equal(100m, line.Amount);
}
```

- [ ] **Step 9: Run focused tests and confirm expected failures**

Run:

```powershell
dotnet test backend/tests/SIMS.Application.Tests/SIMS.Application.Tests.csproj --filter "FullyQualifiedName~FeeAdminProgramScopeTests|FullyQualifiedName~FeeCalculationServiceTests"
```

Expected: build fails because `FeeRuleVersion` does not yet have `ProgramCarrierId`, `ProgramCarrierLineOfBusinessId`, or `ProgramCarrierLobStateId`, or tests fail because validation is not implemented.

## Task 2: Add Canonical Fee Rule Scope Columns

**Files:**
- Modify: `backend/src/SIMS.Domain/Entities/Accounting/FeeRuleVersion.cs`
- Modify: `backend/src/SIMS.Infrastructure/Data/Configurations/Accounting/FeeRuleVersionConfiguration.cs`
- Create: `backend/src/SIMS.Infrastructure/Migrations/*_AddFeeRuleProgramScopeRefs.cs`
- Modify: `backend/src/SIMS.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Add entity properties**

In `FeeRuleVersion.cs`, insert this block after `public string? StateCode { get; set; }`:

```csharp
    public Guid? ProgramCarrierId { get; set; }
    public Guid? ProgramCarrierLineOfBusinessId { get; set; }
    public Guid? ProgramCarrierLobStateId { get; set; }
```

Insert this block after `public ProgramConfiguration? ProgramConfiguration { get; set; }`:

```csharp
    public ProgramCarrier? ProgramCarrier { get; set; }
    public ProgramCarrierLineOfBusiness? ProgramCarrierLineOfBusiness { get; set; }
    public ProgramCarrierLobState? ProgramCarrierLobState { get; set; }
```

- [ ] **Step 2: Configure relationships and indexes**

In `FeeRuleVersionConfiguration.cs`, insert this block after the existing `ProgramConfiguration` relationship:

```csharp
        b.HasOne(x => x.ProgramCarrier)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ProgramCarrierLineOfBusiness)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ProgramCarrierLobState)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierLobStateId)
            .OnDelete(DeleteBehavior.Restrict);
```

Insert this block after the existing fee lookup indexes:

```csharp
        b.HasIndex(x => x.ProgramCarrierId)
            .HasDatabaseName("ix_fee_rule_program_carrier_scope");
        b.HasIndex(x => x.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_fee_rule_program_lob_scope");
        b.HasIndex(x => x.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_fee_rule_program_state_scope");

        b.HasCheckConstraint(
            "ck_fee_rule_program_scope_canonical",
            """
            (
                "ProgramConfigurationId" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NULL
                AND "LineOfBusiness" IS NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NOT NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NOT NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "StateCode" IS NOT NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NOT NULL
            )
            """);
```

- [ ] **Step 3: Generate migration**

Run:

```powershell
dotnet ef migrations add AddFeeRuleProgramScopeRefs --project backend/src/SIMS.Infrastructure --startup-project backend/src/SIMS.API
```

Expected: EF creates a migration ending in `AddFeeRuleProgramScopeRefs.cs` and updates `ApplicationDbContextModelSnapshot.cs`.

- [ ] **Step 4: Edit the migration Up method**

In the generated migration file ending with `AddFeeRuleProgramScopeRefs.cs`, keep EF-generated `AddColumn`, `CreateIndex`, `AddForeignKey`, and `AddCheckConstraint` operations. Insert this SQL after columns and indexes are created and before the check constraint is added:

The SQL below must be date-aware and consistency-safe:

- Normalize Program-scoped `StateCode` before backfill.
- Reject unsupported Program-scoped `LineOfBusiness` strings before backfill.
- Add fee effective-date predicates to every Program setup join.
- Add a PostgreSQL trigger that rejects canonical IDs pointing to a different Program path than the denormalized columns.

```csharp
        migrationBuilder.Sql(
            """
            UPDATE fee_rule_versions
            SET "StateCode" = UPPER(TRIM("StateCode"))
            WHERE "ProgramConfigurationId" IS NOT NULL
              AND "StateCode" IS NOT NULL;

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM fee_rule_versions v
                    WHERE v."ProgramConfigurationId" IS NOT NULL
                      AND v."LineOfBusiness" IS NOT NULL
                      AND v."LineOfBusiness" NOT IN (
                          'GeneralLiability',
                          'InlandMarine',
                          'AutoLiability',
                          'AutoPhysicalDamage',
                          'Property',
                          'CommercialAuto',
                          'BusinessOwners',
                          'WorkersCompensation',
                          'ProfessionalLiability',
                          'Umbrella',
                          'Cyber',
                          'ExcessLiability',
                          'Other'
                      )
                ) THEN
                    RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program-scoped fee rule has an unsupported LineOfBusiness value.';
                END IF;
            END $$;

            UPDATE fee_rule_versions v
            SET "ProgramCarrierId" = pc."Id"
            FROM program_carriers pc
            WHERE v."ProgramConfigurationId" IS NOT NULL
              AND v."CarrierId" IS NOT NULL
              AND v."LineOfBusiness" IS NULL
              AND v."StateCode" IS NULL
              AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
              AND pc."CarrierId" = v."CarrierId"
              AND pc."IsActive" = TRUE
              AND pc."IsDeleted" = FALSE
              AND pc."EffectiveDate" <= v."EffectiveDate"
              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate");

            UPDATE fee_rule_versions v
            SET "ProgramCarrierLineOfBusinessId" = pcl."Id"
            FROM program_carrier_lines_of_business pcl
            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
            WHERE v."ProgramConfigurationId" IS NOT NULL
              AND v."CarrierId" IS NOT NULL
              AND v."LineOfBusiness" IS NOT NULL
              AND v."StateCode" IS NULL
              AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
              AND pc."CarrierId" = v."CarrierId"
              AND pcl."LineOfBusiness" = CASE v."LineOfBusiness"
                    WHEN 'GeneralLiability' THEN 1
                    WHEN 'InlandMarine' THEN 10
                    WHEN 'AutoLiability' THEN 11
                    WHEN 'AutoPhysicalDamage' THEN 12
                    WHEN 'Property' THEN 2
                    WHEN 'CommercialAuto' THEN 3
                    WHEN 'BusinessOwners' THEN 4
                    WHEN 'WorkersCompensation' THEN 5
                    WHEN 'ProfessionalLiability' THEN 6
                    WHEN 'Umbrella' THEN 7
                    WHEN 'Cyber' THEN 8
                    WHEN 'ExcessLiability' THEN 9
                    WHEN 'Other' THEN 99
                    ELSE -1
                  END
              AND pc."IsActive" = TRUE
              AND pc."IsDeleted" = FALSE
              AND pcl."IsActive" = TRUE
              AND pcl."IsDeleted" = FALSE
              AND pc."EffectiveDate" <= v."EffectiveDate"
              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate")
              AND pcl."EffectiveDate" <= v."EffectiveDate"
              AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= v."EffectiveDate");

            UPDATE fee_rule_versions v
            SET "ProgramCarrierLobStateId" = pcs."Id"
            FROM program_carrier_lob_states pcs
            INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
            INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
            WHERE v."ProgramConfigurationId" IS NOT NULL
              AND v."CarrierId" IS NOT NULL
              AND v."LineOfBusiness" IS NOT NULL
              AND v."StateCode" IS NOT NULL
              AND pc."ProgramConfigurationId" = v."ProgramConfigurationId"
              AND pc."CarrierId" = v."CarrierId"
              AND pcl."LineOfBusiness" = CASE v."LineOfBusiness"
                    WHEN 'GeneralLiability' THEN 1
                    WHEN 'InlandMarine' THEN 10
                    WHEN 'AutoLiability' THEN 11
                    WHEN 'AutoPhysicalDamage' THEN 12
                    WHEN 'Property' THEN 2
                    WHEN 'CommercialAuto' THEN 3
                    WHEN 'BusinessOwners' THEN 4
                    WHEN 'WorkersCompensation' THEN 5
                    WHEN 'ProfessionalLiability' THEN 6
                    WHEN 'Umbrella' THEN 7
                    WHEN 'Cyber' THEN 8
                    WHEN 'ExcessLiability' THEN 9
                    WHEN 'Other' THEN 99
                    ELSE -1
                  END
              AND pcs."StateCode" = UPPER(v."StateCode")
              AND pc."IsActive" = TRUE
              AND pc."IsDeleted" = FALSE
              AND pcl."IsActive" = TRUE
              AND pcl."IsDeleted" = FALSE
              AND pcs."IsActive" = TRUE
              AND pcs."IsDeleted" = FALSE
              AND pc."EffectiveDate" <= v."EffectiveDate"
              AND (pc."ExpirationDate" IS NULL OR pc."ExpirationDate" >= v."EffectiveDate")
              AND pcl."EffectiveDate" <= v."EffectiveDate"
              AND (pcl."ExpirationDate" IS NULL OR pcl."ExpirationDate" >= v."EffectiveDate")
              AND pcs."EffectiveDate" <= v."EffectiveDate"
              AND (pcs."ExpirationDate" IS NULL OR pcs."ExpirationDate" >= v."EffectiveDate");
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM fee_rule_versions v
                    WHERE v."ProgramConfigurationId" IS NOT NULL
                      AND v."CarrierId" IS NOT NULL
                      AND v."LineOfBusiness" IS NULL
                      AND v."StateCode" IS NULL
                      AND v."ProgramCarrierId" IS NULL
                ) THEN
                    RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program/Carrier fee rule has no matching active ProgramCarrier path.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM fee_rule_versions v
                    WHERE v."ProgramConfigurationId" IS NOT NULL
                      AND v."CarrierId" IS NOT NULL
                      AND v."LineOfBusiness" IS NOT NULL
                      AND v."StateCode" IS NULL
                      AND v."ProgramCarrierLineOfBusinessId" IS NULL
                ) THEN
                    RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program/Carrier/LOB fee rule has no matching active ProgramCarrierLineOfBusiness path.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM fee_rule_versions v
                    WHERE v."ProgramConfigurationId" IS NOT NULL
                      AND v."CarrierId" IS NOT NULL
                      AND v."LineOfBusiness" IS NOT NULL
                      AND v."StateCode" IS NOT NULL
                      AND v."ProgramCarrierLobStateId" IS NULL
                ) THEN
                    RAISE EXCEPTION 'Cannot add fee Program SOT constraint: at least one Program/Carrier/LOB/State fee rule has no matching active ProgramCarrierLobState path.';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM fee_rule_versions v
                    WHERE v."ProgramConfigurationId" IS NOT NULL
                      AND (
                          (v."CarrierId" IS NULL AND (v."LineOfBusiness" IS NOT NULL OR v."StateCode" IS NOT NULL))
                          OR (v."CarrierId" IS NOT NULL AND v."LineOfBusiness" IS NULL AND v."StateCode" IS NOT NULL)
                      )
                ) THEN
                    RAISE EXCEPTION 'Cannot add fee Program SOT constraint: Program-scoped fee rules cannot skip carrier or LOB levels before state.';
                END IF;
            END $$;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION validate_fee_rule_program_scope()
            RETURNS trigger AS $$
            DECLARE
                lob_value integer;
                mismatch_exists boolean;
            BEGIN
                lob_value := CASE NEW."LineOfBusiness"
                    WHEN 'GeneralLiability' THEN 1
                    WHEN 'InlandMarine' THEN 10
                    WHEN 'AutoLiability' THEN 11
                    WHEN 'AutoPhysicalDamage' THEN 12
                    WHEN 'Property' THEN 2
                    WHEN 'CommercialAuto' THEN 3
                    WHEN 'BusinessOwners' THEN 4
                    WHEN 'WorkersCompensation' THEN 5
                    WHEN 'ProfessionalLiability' THEN 6
                    WHEN 'Umbrella' THEN 7
                    WHEN 'Cyber' THEN 8
                    WHEN 'ExcessLiability' THEN 9
                    WHEN 'Other' THEN 99
                    ELSE NULL
                END;

                IF NEW."ProgramCarrierId" IS NOT NULL THEN
                    SELECT NOT EXISTS (
                        SELECT 1
                        FROM program_carriers pc
                        WHERE pc."Id" = NEW."ProgramCarrierId"
                          AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                          AND pc."CarrierId" = NEW."CarrierId"
                    ) INTO mismatch_exists;
                    IF mismatch_exists THEN
                        RAISE EXCEPTION 'Fee rule ProgramCarrierId does not match ProgramConfigurationId and CarrierId.';
                    END IF;
                END IF;

                IF NEW."ProgramCarrierLineOfBusinessId" IS NOT NULL THEN
                    SELECT NOT EXISTS (
                        SELECT 1
                        FROM program_carrier_lines_of_business pcl
                        INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                        WHERE pcl."Id" = NEW."ProgramCarrierLineOfBusinessId"
                          AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                          AND pc."CarrierId" = NEW."CarrierId"
                          AND pcl."LineOfBusiness" = lob_value
                    ) INTO mismatch_exists;
                    IF mismatch_exists THEN
                        RAISE EXCEPTION 'Fee rule ProgramCarrierLineOfBusinessId does not match Program, Carrier, and LineOfBusiness.';
                    END IF;
                END IF;

                IF NEW."ProgramCarrierLobStateId" IS NOT NULL THEN
                    SELECT NOT EXISTS (
                        SELECT 1
                        FROM program_carrier_lob_states pcs
                        INNER JOIN program_carrier_lines_of_business pcl ON pcl."Id" = pcs."ProgramCarrierLineOfBusinessId"
                        INNER JOIN program_carriers pc ON pc."Id" = pcl."ProgramCarrierId"
                        WHERE pcs."Id" = NEW."ProgramCarrierLobStateId"
                          AND pc."ProgramConfigurationId" = NEW."ProgramConfigurationId"
                          AND pc."CarrierId" = NEW."CarrierId"
                          AND pcl."LineOfBusiness" = lob_value
                          AND pcs."StateCode" = NEW."StateCode"
                    ) INTO mismatch_exists;
                    IF mismatch_exists THEN
                        RAISE EXCEPTION 'Fee rule ProgramCarrierLobStateId does not match Program, Carrier, LineOfBusiness, and StateCode.';
                    END IF;
                END IF;

                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER trg_validate_fee_rule_program_scope
            BEFORE INSERT OR UPDATE OF "ProgramConfigurationId", "CarrierId", "LineOfBusiness", "StateCode", "ProgramCarrierId", "ProgramCarrierLineOfBusinessId", "ProgramCarrierLobStateId"
            ON fee_rule_versions
            FOR EACH ROW
            EXECUTE FUNCTION validate_fee_rule_program_scope();
            """);
```

- [ ] **Step 5: Ensure Down removes added objects**

In the generated migration `Down` method, confirm it drops these foreign keys, indexes, check constraint, and columns:

```csharp
        migrationBuilder.DropCheckConstraint(
            name: "ck_fee_rule_program_scope_canonical",
            table: "fee_rule_versions");

        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS trg_validate_fee_rule_program_scope ON fee_rule_versions;
            DROP FUNCTION IF EXISTS validate_fee_rule_program_scope();
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_fee_rule_versions_program_carriers_ProgramCarrierId",
            table: "fee_rule_versions");

        migrationBuilder.DropForeignKey(
            name: "FK_fee_rule_versions_program_carrier_lines_of_business_ProgramCarrierLineOfBusinessId",
            table: "fee_rule_versions");

        migrationBuilder.DropForeignKey(
            name: "FK_fee_rule_versions_program_carrier_lob_states_ProgramCarrierLobStateId",
            table: "fee_rule_versions");

        migrationBuilder.DropIndex(
            name: "ix_fee_rule_program_carrier_scope",
            table: "fee_rule_versions");

        migrationBuilder.DropIndex(
            name: "ix_fee_rule_program_lob_scope",
            table: "fee_rule_versions");

        migrationBuilder.DropIndex(
            name: "ix_fee_rule_program_state_scope",
            table: "fee_rule_versions");

        migrationBuilder.DropColumn(
            name: "ProgramCarrierId",
            table: "fee_rule_versions");

        migrationBuilder.DropColumn(
            name: "ProgramCarrierLineOfBusinessId",
            table: "fee_rule_versions");

        migrationBuilder.DropColumn(
            name: "ProgramCarrierLobStateId",
            table: "fee_rule_versions");
```

- [ ] **Step 6: Run fee tests and confirm service tests still fail**

Run:

```powershell
dotnet test backend/tests/SIMS.Application.Tests/SIMS.Application.Tests.csproj --filter "FullyQualifiedName~FeeAdminProgramScopeTests|FullyQualifiedName~FeeCalculationServiceTests"
```

Expected: SQLite constraint and fee calculation tests can compile; service validation tests fail until `FeeAdminService` stamps canonical IDs.

## Task 3: Resolve Program Scope In Fee Admin Service

**Files:**
- Modify: `backend/src/SIMS.Application/Services/FeeAdminService.cs`

- [ ] **Step 1: Add service usings**

At the top of `FeeAdminService.cs`, add:

```csharp
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
```

- [ ] **Step 2: Add resolved scope record**

Inside `FeeAdminService`, after the `Db` property, add:

```csharp
    private sealed record ResolvedFeeProgramScope(
        Guid? ProgramCarrierId,
        Guid? ProgramCarrierLineOfBusinessId,
        Guid? ProgramCarrierLobStateId,
        string? LineOfBusiness,
        string? StateCode);
```

- [ ] **Step 3: Pass resolved scope into version creation**

In `CreateVersionAsync`, replace the validation and build section with:

```csharp
        var validation = await ValidateVersionRequestAsync(req, ct);
        if (!validation.IsSuccess)
            return Result<FeeRuleVersionDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var version = BuildVersion(req, userId, validation.Value!);
```

In `NewVersionFromExistingAsync`, replace the validation and build section with:

```csharp
        var validation = await ValidateVersionRequestAsync(req, ct);
        if (!validation.IsSuccess)
            return Result<FeeRuleVersionDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        // Stamp old version's disabled_date with the new version's effective_date in one transaction
        existing.DisabledDate = req.EffectiveDate;

        var newVersion = BuildVersion(req, userId, validation.Value!);
```

- [ ] **Step 4: Replace request validation**

Replace `ValidateVersionRequestAsync` with:

```csharp
    private async Task<Result<ResolvedFeeProgramScope>> ValidateVersionRequestAsync(
        CreateFeeRuleVersionRequest req, CancellationToken ct)
    {
        if (req.PayableRouting is not "NotPayable" and not "Company" and not "Entity")
            return Result<ResolvedFeeProgramScope>.Failure("PAYABLE_ROUTING_INVALID", "Payable routing must be NotPayable, Company, or Entity.");

        if (req.PayableRouting == "Entity")
        {
            if (!req.PayablePayeeId.HasValue)
                return Result<ResolvedFeeProgramScope>.Failure("PAYABLE_PAYEE_REQUIRED", "A third-party/vendor payee is required when payable routing is Entity.");

            var payeeExists = await Db.Set<Payee>()
                .AnyAsync(p => p.Id == req.PayablePayeeId.Value && p.IsActive, ct);
            if (!payeeExists)
                return Result<ResolvedFeeProgramScope>.Failure("PAYABLE_PAYEE_NOT_FOUND", "The selected third-party/vendor payee was not found or is inactive.");
        }

        return await ResolveProgramScopeAsync(req, ct);
    }
```

- [ ] **Step 5: Add Program scope resolver**

Add these methods below `ValidateVersionRequestAsync`:

```csharp
    private async Task<Result<ResolvedFeeProgramScope>> ResolveProgramScopeAsync(
        CreateFeeRuleVersionRequest req, CancellationToken ct)
    {
        var normalizedState = NormalizeStateCode(req.StateCode);
        if (!string.IsNullOrWhiteSpace(req.StateCode) && normalizedState is null)
            return Result<ResolvedFeeProgramScope>.Failure("STATE_CODE_INVALID", "State code must be two characters.");

        var normalizedLob = NormalizeLineOfBusiness(req.LineOfBusiness);
        PolicyLineOfBusiness? parsedLob = null;
        if (!string.IsNullOrWhiteSpace(normalizedLob))
        {
            if (!Enum.TryParse<PolicyLineOfBusiness>(normalizedLob, ignoreCase: true, out var lobValue))
                return Result<ResolvedFeeProgramScope>.Failure("LOB_INVALID", "Line of business is not valid.");

            parsedLob = lobValue;
            normalizedLob = lobValue.ToString();
        }

        if (!req.ProgramConfigurationId.HasValue)
            return Result<ResolvedFeeProgramScope>.Success(new(null, null, null, normalizedLob, normalizedState));

        var programExists = await Db.Set<ProgramConfiguration>()
            .AnyAsync(p => p.Id == req.ProgramConfigurationId.Value && p.IsActive, ct);
        if (!programExists)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_NOT_FOUND", "The selected Program was not found or is inactive.");

        if (!req.CarrierId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(normalizedLob) || normalizedState is not null)
                return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PARENT_REQUIRED", "Select a carrier before selecting a Program line of business or state.");

            return Result<ResolvedFeeProgramScope>.Success(new(null, null, null, null, null));
        }

        var programCarrier = await Db.Set<ProgramCarrier>()
            .FirstOrDefaultAsync(c =>
                c.ProgramConfigurationId == req.ProgramConfigurationId.Value &&
                c.CarrierId == req.CarrierId.Value &&
                c.IsActive &&
                c.EffectiveDate <= req.EffectiveDate &&
                (c.ExpirationDate == null || c.ExpirationDate >= req.EffectiveDate), ct);

        if (programCarrier is null)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PATH_NOT_FOUND", "The selected carrier is not active for this Program on the fee effective date.");

        if (string.IsNullOrWhiteSpace(normalizedLob))
        {
            if (normalizedState is not null)
                return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PARENT_REQUIRED", "Select a line of business before selecting a Program state.");

            return Result<ResolvedFeeProgramScope>.Success(new(programCarrier.Id, null, null, null, null));
        }

        var lob = parsedLob!.Value;

        var programLob = await Db.Set<ProgramCarrierLineOfBusiness>()
            .FirstOrDefaultAsync(l =>
                l.ProgramCarrierId == programCarrier.Id &&
                l.LineOfBusiness == lob &&
                l.IsActive &&
                l.EffectiveDate <= req.EffectiveDate &&
                (l.ExpirationDate == null || l.ExpirationDate >= req.EffectiveDate), ct);

        if (programLob is null)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PATH_NOT_FOUND", "The selected line of business is not active for this Program carrier on the fee effective date.");

        if (normalizedState is null)
            return Result<ResolvedFeeProgramScope>.Success(new(null, programLob.Id, null, normalizedLob, null));

        var programState = await Db.Set<ProgramCarrierLobState>()
            .FirstOrDefaultAsync(s =>
                s.ProgramCarrierLineOfBusinessId == programLob.Id &&
                s.StateCode == normalizedState &&
                s.IsActive &&
                s.EffectiveDate <= req.EffectiveDate &&
                (s.ExpirationDate == null || s.ExpirationDate >= req.EffectiveDate), ct);

        if (programState is null)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PATH_NOT_FOUND", "The selected state is not active for this Program carrier and line of business on the fee effective date.");

        return Result<ResolvedFeeProgramScope>.Success(new(null, null, programState.Id, normalizedLob, normalizedState));
    }

    private static string? NormalizeStateCode(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return null;

        var normalized = stateCode.Trim().ToUpperInvariant();
        return normalized.Length == 2 ? normalized : null;
    }

    private static string? NormalizeLineOfBusiness(string? lineOfBusiness)
    {
        if (string.IsNullOrWhiteSpace(lineOfBusiness))
            return null;

        return lineOfBusiness.Trim();
    }
```

- [ ] **Step 6: Stamp canonical IDs in BuildVersion**

Change the method signature:

```csharp
    private static FeeRuleVersion BuildVersion(CreateFeeRuleVersionRequest req, Guid userId, ResolvedFeeProgramScope scope)
```

Inside the initializer, replace these assignments:

```csharp
            LineOfBusiness = req.LineOfBusiness,
            StateCode = req.StateCode,
```

with:

```csharp
            LineOfBusiness = scope.LineOfBusiness,
            StateCode = scope.StateCode,
            ProgramCarrierId = scope.ProgramCarrierId,
            ProgramCarrierLineOfBusinessId = scope.ProgramCarrierLineOfBusinessId,
            ProgramCarrierLobStateId = scope.ProgramCarrierLobStateId,
```

- [ ] **Step 7: Load navigation data for mapped versions**

In both `CreateVersionAsync` and `NewVersionFromExistingAsync`, after loading `ProgramConfiguration`, add:

```csharp
        if (version.ProgramCarrierId.HasValue)
            await Db.Entry(version).Reference(v => v.ProgramCarrier).LoadAsync(ct);
        if (version.ProgramCarrierLineOfBusinessId.HasValue)
            await Db.Entry(version).Reference(v => v.ProgramCarrierLineOfBusiness).LoadAsync(ct);
        if (version.ProgramCarrierLobStateId.HasValue)
            await Db.Entry(version).Reference(v => v.ProgramCarrierLobState).LoadAsync(ct);
```

For `NewVersionFromExistingAsync`, use `newVersion` instead of `version`:

```csharp
        if (newVersion.ProgramCarrierId.HasValue)
            await Db.Entry(newVersion).Reference(v => v.ProgramCarrier).LoadAsync(ct);
        if (newVersion.ProgramCarrierLineOfBusinessId.HasValue)
            await Db.Entry(newVersion).Reference(v => v.ProgramCarrierLineOfBusiness).LoadAsync(ct);
        if (newVersion.ProgramCarrierLobStateId.HasValue)
            await Db.Entry(newVersion).Reference(v => v.ProgramCarrierLobState).LoadAsync(ct);
```

- [ ] **Step 8: Run focused backend tests**

Run:

```powershell
dotnet test backend/tests/SIMS.Application.Tests/SIMS.Application.Tests.csproj --filter "FullyQualifiedName~FeeAdminProgramScopeTests|FullyQualifiedName~FeeCalculationServiceTests"
```

Expected: all tests in `FeeAdminProgramScopeTests` and `FeeCalculationServiceTests` pass.

- [ ] **Step 9: Commit and push backend scope changes**

Run:

```powershell
git status --short
git add backend/src/SIMS.Domain/Entities/Accounting/FeeRuleVersion.cs backend/src/SIMS.Infrastructure/Data/Configurations/Accounting/FeeRuleVersionConfiguration.cs backend/src/SIMS.Infrastructure/Migrations backend/src/SIMS.Application/Services/FeeAdminService.cs backend/tests/SIMS.Application.Tests/Services/FeeAdminProgramScopeTests.cs backend/tests/SIMS.Application.Tests/Services/FeeCalculationServiceTests.cs
git commit -m "feat: enforce fee program setup scope"
git push origin main
```

Expected: only files from Tasks 1 through 3 are staged and pushed.

## Task 4: Cascade Fees Admin Scope Selectors

**Files:**
- Modify: `frontend/src/pages/admin/FeesAdminPage.tsx`

- [ ] **Step 1: Add computed Program scope values**

In `FeesAdminPage.tsx`, after `missingVendorPayee`, add:

```tsx
  const selectedProgram = programs.find(program => program.id === form.programConfigurationId)
  const programCarrierOptions = selectedProgram?.carriers.filter(carrier => carrier.isActive) ?? []
  const selectedProgramCarrier = programCarrierOptions.find(programCarrier => programCarrier.carrierId === form.carrierId)
  const programLobOptions = selectedProgramCarrier?.linesOfBusiness.filter(lob => lob.isActive) ?? []
  const selectedProgramLob = programLobOptions.find(lob => lob.lineOfBusiness === form.lineOfBusiness)
  const programStateOptions = selectedProgramLob?.states.filter(state => state.isActive) ?? []
  const carrierOptions = selectedProgram
    ? programCarrierOptions.map(programCarrier => ({
        id: programCarrier.carrierId,
        name: programCarrier.carrierName,
      }))
    : carriers
  const lobOptions = selectedProgram
    ? programLobOptions.map(lob => lob.lineOfBusiness)
    : ACTIVE_LOBS
  const stateOptions = selectedProgram
    ? programStateOptions.map(state => state.stateCode)
    : US_STATES
  const programScopeMissingCarrier = !!selectedProgram && (!!form.lineOfBusiness || !!form.stateCode) && !form.carrierId
  const programScopeMissingLob = !!selectedProgram && !!form.stateCode && !form.lineOfBusiness
  const incompleteProgramScope = programScopeMissingCarrier || programScopeMissingLob
```

- [ ] **Step 2: Add parent-changing setters**

Add these functions after the existing generic `set` helper:

```tsx
  function setProgramScope(programConfigurationId: string | null) {
    setForm(p => ({
      ...p,
      programConfigurationId,
      carrierId: null,
      lineOfBusiness: null,
      stateCode: null,
    }))
  }

  function setCarrierScope(carrierId: string | null) {
    setForm(p => ({
      ...p,
      carrierId,
      lineOfBusiness: null,
      stateCode: null,
    }))
  }

  function setLobScope(lineOfBusiness: string | null) {
    setForm(p => ({
      ...p,
      lineOfBusiness,
      stateCode: null,
    }))
  }
```

- [ ] **Step 3: Replace Scope fields**

Replace the existing Program, Carrier, State, and Line of Business fields in the `Scope` section with:

```tsx
          <Field label="Program">
            <select value={form.programConfigurationId ?? ''} onChange={e => setProgramScope(e.target.value || null)} className={selectCls}>
              <option value="">All Programs</option>
              {programs.map(program => <option key={program.id} value={program.id}>{program.name}</option>)}
            </select>
          </Field>
          <Field label="Carrier">
            <select value={form.carrierId ?? ''} onChange={e => setCarrierScope(e.target.value || null)} className={selectCls}>
              <option value="">{selectedProgram ? 'Program Carrier Default' : 'All Carriers'}</option>
              {carrierOptions.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
            {programScopeMissingCarrier && <p className="mt-1 text-xs text-red-600">Select a carrier for this Program scope.</p>}
          </Field>
          <Field label="State">
            <select
              value={form.stateCode ?? ''}
              onChange={e => set('stateCode', e.target.value || null)}
              disabled={!!selectedProgram && (!form.carrierId || !form.lineOfBusiness)}
              className={selectCls}
            >
              <option value="">{selectedProgram ? 'All States for LOB' : 'All States'}</option>
              {stateOptions.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
            {programScopeMissingLob && <p className="mt-1 text-xs text-red-600">Select a line of business before choosing a state.</p>}
          </Field>
```

Replace the existing Line of Business field with:

```tsx
          <Field label="Line of Business">
            <select
              value={form.lineOfBusiness ?? ''}
              onChange={e => setLobScope(e.target.value || null)}
              disabled={!!selectedProgram && !form.carrierId}
              className={selectCls}
            >
              <option value="">{selectedProgram ? 'All LOBs for Carrier' : 'All LOBs'}</option>
              {lobOptions.map(lob => <option key={lob} value={lob}>{LOB_LABELS[lob as PolicyLineOfBusiness] ?? lob}</option>)}
            </select>
          </Field>
```

- [ ] **Step 4: Block save when Program scope is incomplete**

Replace the Save button disabled expression:

```tsx
          <button onClick={() => saveVersion()} disabled={savingVersion || missingVendorPayee}
```

with:

```tsx
          <button onClick={() => saveVersion()} disabled={savingVersion || missingVendorPayee || incompleteProgramScope}
```

- [ ] **Step 5: Run frontend typecheck**

Run:

```powershell
npm --prefix frontend run build
```

Expected: production build succeeds.

- [ ] **Step 6: Commit and push frontend scope changes**

Run:

```powershell
git status --short
git add frontend/src/pages/admin/FeesAdminPage.tsx
git commit -m "fix: cascade fee program scope selectors"
git push origin main
```

Expected: only `FeesAdminPage.tsx` is staged and pushed.

## Task 5: Full Verification And Azure Smoke

**Files:**
- No new source files.

- [ ] **Step 1: Run backend build**

Run:

```powershell
dotnet build backend
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 2: Run focused backend regression tests**

Run:

```powershell
dotnet test backend/tests/SIMS.Application.Tests/SIMS.Application.Tests.csproj --filter "FullyQualifiedName~FeeAdminProgramScopeTests|FullyQualifiedName~FeeCalculationServiceTests"
```

Expected: focused fee tests pass.

- [ ] **Step 3: Run frontend production build**

Run:

```powershell
npm --prefix frontend run build
```

Expected: build succeeds.

- [ ] **Step 4: Verify no unrelated files are staged**

Run:

```powershell
git status --short
```

Expected: no tracked uncommitted files remain from the Fees Program SOT work. Existing unrelated untracked folders such as `.agents/`, `SIMS-UI-Guide/`, and `plugins/` remain untouched.

- [ ] **Step 5: Smoke test after push**

Use the Azure frontend and API after `main` deploys:

- Frontend app: `sims-frontend-test`
- API app: `sims-api-test`
- API host: `sims-api-test-f9htbma5aee5babz.eastus2-01.azurewebsites.net`

Smoke path:

1. Open the Azure frontend.
2. Sign in.
3. Go to Fees admin.
4. Open a fee type and create a new version.
5. Select Program Longleaf.
6. Confirm Carrier options are limited to carriers under Longleaf.
7. Select a carrier.
8. Confirm LOB options are limited to active LOBs under that Program carrier.
9. Select a LOB.
10. Confirm State options are limited to active states under that Program carrier LOB.
11. Save an all-state LOB fee rule.
12. Save a state-specific fee rule.
13. Attempt an invalid API save with Program plus a carrier/LOB/state outside Program setup and confirm the API rejects it.

Expected: valid Program setup paths save; invalid Program-scoped fee paths fail before they can create fee rules.

## Task 6: Record Next Plan Step

**Files:**
- Modify: `docs/superpowers/specs/2026-05-30-program-sot-database-contract-design.md`

- [ ] **Step 1: Add implementation status**

After the `First Implementation Recommendation` section, add:

```markdown
## Implementation Status

- Fees Program SOT: implemented for Program, Program/Carrier, Program/Carrier/LOB all-state, and Program/Carrier/LOB/State fee rule versions.
- Next target: Bordereaux profiles should move to canonical Program scope references using the same database contract pattern.
```

- [ ] **Step 2: Commit and push status doc**

Run:

```powershell
git status --short
git add docs/superpowers/specs/2026-05-30-program-sot-database-contract-design.md
git commit -m "docs: record fees program sot status"
git push origin main
```

Expected: only the spec status doc is staged and pushed.

## Final Success Criteria

- Program-scoped fee rules with Carrier, LOB, or State cannot be saved unless the matching Program setup path exists and is active on the fee effective date.
- The database rejects Program-scoped fee rows that skip canonical Program setup references.
- The database rejects Program-scoped fee rows whose canonical Program path ID does not match the denormalized Program, Carrier, LOB, or State columns.
- Migration backfill normalizes state codes, rejects unsupported LOB values, and only links fee rules to Program setup paths active on the fee rule effective date.
- Global fee rules continue working.
- Program-level fee rules continue working.
- Program/Carrier/LOB all-state fee rules work without duplicating every state.
- Program/Carrier/LOB/State fee rules override all-state LOB defaults.
- Fees admin UI only offers valid Program carriers, LOBs, and states after a Program is selected.
- Fees admin UI enables Save for valid Program-level, Program/Carrier, Program/Carrier/LOB all-state, and Program/Carrier/LOB/State scopes.
- Backend focused fee tests pass.
- Frontend build passes.
- Changes are committed and pushed to `origin/main` after each completed task group.
