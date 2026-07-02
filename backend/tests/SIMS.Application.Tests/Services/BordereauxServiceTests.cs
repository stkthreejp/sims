using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class BordereauxServiceTests
{
    [Fact]
    public async Task CreateProfileAsync_StoresLondonBdxAccountCurrentProfile()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobStateAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability, "MS");
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(new UpsertBordereauxProfileRequest(
            Name: " BRACE London BDX ",
            ProgramConfigurationId: program.Id,
            CarrierId: carrier.Id,
            LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
            StateCode: " ms ",
            ReportType: BordereauxReportType.Premium,
            Frequency: BordereauxFrequency.Monthly,
            OutputFormat: BordereauxOutputFormat.Xlsx,
            DateBasis: BordereauxDateBasis.EffectiveOrBoundDateGreater,
            RequiresAccountCurrent: true,
            IsActive: true,
            RequiredTabsJson: """["General Liability (Section 1)","Auto Veh Info","IM Unit Info","Acct Current"]""",
            RequiredColumnsJson: """["Certificate Ref","Gross premium paid this time","Net Premium to London in original currency"]""",
            MappingRulesJson: """{"commissionBasis":"commissionPlusBrokerage"}""",
            StaticValuesJson: """{"umr":"BRACE-SMM-2025-LOGGING","coverholderPin":"USA00060"}""",
            ValidationRulesJson: """{"requireReconciliation":true}""",
            IncludedTransactionTypesJson: """["NewBusiness","Endorsement"]""",
            Notes: " Monthly London package "));

        Assert.True(result.IsSuccess);
        Assert.Equal("BRACE London BDX", result.Value!.Name);
        Assert.Equal("MS", result.Value.StateCode);
        Assert.True(result.Value.RequiresAccountCurrent);
        Assert.Contains("Acct Current", result.Value.RequiredTabsJson);
        Assert.Equal("Monthly London package", result.Value.Notes);
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsInvalidJsonConfiguration()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            MappingRulesJson = "{not-json",
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_MAPPING_RULES_JSON", result.ErrorCode);
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsDuplicateActiveScope()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        var duplicate = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            Name = "Duplicate BRACE London BDX",
        });

        Assert.False(duplicate.IsSuccess);
        Assert.Equal("DUPLICATE_ACTIVE_PROFILE", duplicate.ErrorCode);
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsCarrierOutsideProgramSetupPath()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            LineOfBusiness = null,
            StateCode = null,
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task CreateProfileAsync_RejectsStateOutsideProgramSetupPath()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            StateCode = "TX",
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task CreateProfileAsync_SavesCanonicalLobScopeForAllStates()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));

        Assert.True(result.IsSuccess);
        var saved = await db.Set<SIMS.Domain.Entities.Bordereaux.BordereauxProfile>().SingleAsync(p => p.Id == result.Value!.Id);
        Assert.Equal(saved.ProgramCarrierLineOfBusinessId, result.Value!.ProgramCarrierLineOfBusinessId);
        Assert.Equal(program.Id, saved.ProgramConfigurationId);
        Assert.Equal(carrier.Id, saved.CarrierId);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability, saved.LineOfBusiness);
        Assert.Null(saved.StateCode);
        Assert.Null(saved.ProgramCarrierId);
        Assert.NotNull(saved.ProgramCarrierLineOfBusinessId);
        Assert.Null(saved.ProgramCarrierLobStateId);
    }

    [Fact]
    public async Task CreateProfileAsync_SavesCanonicalStateScopeForStateProfile()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var state = await SeedProgramCarrierLobStateAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability, "TX");
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            StateCode = "tx",
        });

        Assert.True(result.IsSuccess);
        var saved = await db.Set<SIMS.Domain.Entities.Bordereaux.BordereauxProfile>().SingleAsync(p => p.Id == result.Value!.Id);
        Assert.Equal(state.Id, saved.ProgramCarrierLobStateId);
        Assert.Equal(state.Id, result.Value!.ProgramCarrierLobStateId);
        Assert.Null(saved.ProgramCarrierId);
        Assert.Null(saved.ProgramCarrierLineOfBusinessId);
        Assert.Equal("TX", saved.StateCode);
    }

    [Fact]
    public async Task GetProfilesAsync_FiltersByProgramAndActiveStatus()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var otherProgram = new ProgramConfiguration { Name = "Shuttlebee", Code = "SHUTTLEBEE", IsActive = true };
        db.Add(otherProgram);
        await db.SaveChangesAsync();
        await SeedProgramCarrierLobSetupAsync(db, otherProgram, carrier, PolicyLineOfBusiness.GeneralLiability);
        var service = new BordereauxService(db);

        await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await service.CreateProfileAsync(ValidRequest(otherProgram.Id, carrier.Id) with
        {
            Name = "Other Program",
            IsActive = false,
        });

        var active = await service.GetProfilesAsync(programId: program.Id);
        var allForOtherProgram = await service.GetProfilesAsync(programId: otherProgram.Id, includeInactive: true);

        Assert.Single(active);
        Assert.Equal(program.Id, active[0].ProgramConfigurationId);
        Assert.Single(allForOtherProgram);
        Assert.False(allForOtherProgram[0].IsActive);
    }

    [Fact]
    public async Task UpdateProfileAsync_ChangesEditableFields()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobStateAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability, "AL");
        var service = new BordereauxService(db);
        var create = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));

        var update = await service.UpdateProfileAsync(create.Value!.Id, ValidRequest(program.Id, carrier.Id) with
        {
            Name = "BRACE Updated",
            StateCode = "AL",
            RequiredColumnsJson = """["Policy Number","Gross Premium","Gross Commission","Net Due Carrier"]""",
        });

        Assert.True(update.IsSuccess);
        Assert.Equal("BRACE Updated", update.Value!.Name);
        Assert.Equal("AL", update.Value.StateCode);
        Assert.Contains("Gross Commission", update.Value.RequiredColumnsJson);
    }

    [Fact]
    public async Task CreateProfileAsync_FlagsMissingBdxSetupItems()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            RequiredTabsJson = """["General Liability (Section 1)"]""",
            RequiredColumnsJson = """["Certificate Ref"]""",
            MappingRulesJson = "{}",
            StaticValuesJson = "{}",
        });

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.SetupStatus.IsReadyForExport);
        Assert.Contains(result.Value.SetupStatus.RequiredTabs, item => item.Key == "Acct Current" && item.Status == "Missing");
        Assert.Contains(result.Value.SetupStatus.RequiredColumns, item => item.Key == "Gross premium paid this time" && item.Status == "Missing");
        Assert.Contains(result.Value.SetupStatus.MappingRules, item => item.Key == "commissionBasis" && item.Status == "Missing");
        Assert.Contains(result.Value.SetupStatus.StaticValues, item => item.Key == "umr" && item.Status == "Missing");
        Assert.Contains(result.Value.SetupStatus.StaticValues, item => item.Key == "coverholderName" && item.Status == "Default");
    }

    [Fact]
    public async Task CreateProfileAsync_ReportsConfiguredBdxSetupItems()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);

        var result = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            StaticValuesJson = """{"umr":"BRACE-SMM-2025-LOGGING","yearOfAccount":"2025","coverholderPin":"USA00060"}""",
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.SetupStatus.IsReadyForExport);
        Assert.Equal(0, result.Value.SetupStatus.MissingItems);
        Assert.Contains(result.Value.SetupStatus.StaticValues, item => item.Key == "yearOfAccount" && item.Status == "Configured" && item.Value == "2025");
        Assert.Contains(result.Value.SetupStatus.StaticValues, item => item.Key == "coverholderName" && item.Status == "Default");
    }

    [Fact]
    public async Task GetPremiumPreviewAsync_IncludesLateProcessedEndorsementInBillingMonth()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        var transaction = await SeedPolicyTransactionWithInvoiceAsync(
            db,
            program,
            carrier,
            TransactionType.Endorsement,
            effectiveDate: new DateOnly(2026, 4, 15),
            invoiceDate: new DateOnly(2026, 5, 2),
            policyNumber: "LL-GL-000137-00",
            state: "MS",
            grossPremium: 50m,
            commissionAmount: 12.50m);

        var april = await service.GetPremiumPreviewAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        var may = await service.GetPremiumPreviewAsync(profile.Value.Id, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        Assert.True(april.IsSuccess);
        Assert.Empty(april.Value!.Rows);
        Assert.True(may.IsSuccess);
        var row = Assert.Single(may.Value!.Rows);
        Assert.Equal(transaction.Id, row.PolicyTransactionId);
        Assert.Equal(new DateOnly(2026, 5, 2), row.ReportingDate);
        Assert.Equal(new DateOnly(2026, 4, 15), row.TransactionEffectiveDate);
        Assert.Equal(50m, row.GrossPremium);
        Assert.Equal(12.50m, row.GrossCommission);
        Assert.Equal(37.50m, row.NetDueCarrier);
    }

    [Fact]
    public async Task GetPremiumPreviewAsync_FiltersByProfileScopeAndPostedInvoice()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobStateAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability, "MS");
        var otherCarrier = new Carrier { Name = "Other", IsActive = true };
        db.Add(otherCarrier);
        await db.SaveChangesAsync();
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            StateCode = "MS",
            IncludedTransactionTypesJson = """["NewBusiness"]""",
        });

        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.Endorsement, new DateOnly(2026, 4, 9), new DateOnly(2026, 4, 9), "LL-GL-000146-00", "MS", 100m, 25m);
        await SeedPolicyTransactionWithInvoiceAsync(db, program, otherCarrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 10), "OTHER-001", "MS", 200m, 50m);
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 11), new DateOnly(2026, 4, 11), "LL-GL-000147-00", "AL", 300m, 75m);
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 12), new DateOnly(2026, 4, 12), "LL-GL-000148-00", "MS", 400m, 100m, invoiceStatus: "Voided");

        var preview = await service.GetPremiumPreviewAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        Assert.True(preview.IsSuccess);
        var row = Assert.Single(preview.Value!.Rows);
        Assert.Equal("LL-GL-000145-00", row.PolicyNumber);
        Assert.Equal(1451m, preview.Value.GrossPremiumTotal);
        Assert.Equal(362.75m, preview.Value.GrossCommissionTotal);
        Assert.Equal(1088.25m, preview.Value.NetDueCarrierTotal);
    }

    [Fact]
    public async Task CreatePremiumRunSnapshotAsync_FreezesProfileAndPremiumRows()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            StaticValuesJson = """{"umr":"BRACE-SMM-2025-LOGGING"}""",
        });
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);

        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: Guid.NewGuid());

        Assert.True(run.IsSuccess);
        Assert.Equal(1, run.Value!.RunNumber);
        Assert.Equal(1, run.Value.BordereauxRowCount);
        Assert.Contains("BRACE-SMM-2025-LOGGING", run.Value.ProfileSnapshotJson);
        Assert.Contains("LL-GL-000145-00", run.Value.SourceRowsSnapshotJson);
        Assert.Contains("1451", run.Value.SourceRowsSnapshotJson);

        var savedProfile = await db.Set<SIMS.Domain.Entities.Bordereaux.BordereauxProfile>().SingleAsync();
        savedProfile.StaticValuesJson = """{"umr":"CHANGED"}""";
        var savedInvoice = await db.Set<Invoice>().SingleAsync();
        // Sentinel for the post-snapshot change. Must contain a '.' so it can never
        // false-match a hex substring of a random GUID in the snapshot JSON (a bare
        // "999" did, which made this assertion flaky).
        savedInvoice.GrossPremium = 987654.32m;
        await db.SaveChangesAsync();

        var savedRun = await db.Set<SIMS.Domain.Entities.Bordereaux.BordereauxRun>().SingleAsync();
        Assert.Contains("BRACE-SMM-2025-LOGGING", savedRun.ProfileSnapshotJson);
        Assert.DoesNotContain("CHANGED", savedRun.ProfileSnapshotJson);
        Assert.Contains("1451", savedRun.SourceRowsSnapshotJson);
        Assert.DoesNotContain("987654.32", savedRun.SourceRowsSnapshotJson);
    }

    [Fact]
    public async Task CreatePremiumRunSnapshotAsync_RecordsMissingSetupValidationWarnings()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);

        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        Assert.True(run.IsSuccess);
        Assert.Contains("\"status\":\"warnings\"", run.Value!.ValidationSummaryJson);
        Assert.Contains("\"missingLondonLobSetupRows\":1", run.Value.ValidationSummaryJson);
        Assert.Contains("\"missingSurplusLinesSetupRows\":1", run.Value.ValidationSummaryJson);
        Assert.Contains("LL-GL-000145-00", run.Value.ValidationSummaryJson);
    }

    [Fact]
    public async Task CreatePremiumRunSnapshotAsync_RecordsClearValidationWhenAllSetupsMatch()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability);
        await SeedSurplusLinesSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability, "MS");
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);

        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        Assert.True(run.IsSuccess);
        Assert.Contains("\"status\":\"clear\"", run.Value!.ValidationSummaryJson);
        Assert.Contains("\"warnings\":0", run.Value.ValidationSummaryJson);
        Assert.Contains("\"missingLondonLobSetupRows\":0", run.Value.ValidationSummaryJson);
        Assert.Contains("\"missingSurplusLinesSetupRows\":0", run.Value.ValidationSummaryJson);
        Assert.DoesNotContain("MISSING_LONDON_LOB_SETUP", run.Value.ValidationSummaryJson);
        Assert.DoesNotContain("MISSING_SURPLUS_LINES_SETUP", run.Value.ValidationSummaryJson);
    }

    [Fact]
    public async Task CreatePremiumRunSnapshotAsync_CreatesNextRunNumberWithoutOverwritingPriorRun()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);

        var firstRun = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);
        var invoice = await db.Set<Invoice>().SingleAsync();
        invoice.GrossPremium = 999m;
        await db.SaveChangesAsync();
        var secondRun = await service.CreatePremiumRunSnapshotAsync(profile.Value.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        Assert.True(firstRun.IsSuccess);
        Assert.True(secondRun.IsSuccess);
        Assert.Equal(1, firstRun.Value!.RunNumber);
        Assert.Equal(2, secondRun.Value!.RunNumber);
        Assert.Equal(2, await db.Set<SIMS.Domain.Entities.Bordereaux.BordereauxRun>().CountAsync());
        Assert.Contains("1451", firstRun.Value.SourceRowsSnapshotJson);
        Assert.Contains("999", secondRun.Value.SourceRowsSnapshotJson);
    }

    [Fact]
    public async Task ReconcilePremiumRunAsync_MarksMatchedWhenAccountCurrentTotalsAgree()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        var reconciled = await service.ReconcilePremiumRunAsync(run.Value!.Id, new ReconcileBordereauxRunRequest(
            AccountCurrentRowCount: 1,
            AccountCurrentGrossPremiumTotal: 1451m,
            AccountCurrentGrossCommissionTotal: 362.75m,
            AccountCurrentFeesTotal: 0m,
            AccountCurrentNetDueCarrierTotal: 1088.25m));

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(BordereauxReconciliationStatus.Matched, reconciled.Value!.ReconciliationStatus);
        Assert.Contains("\"status\":\"matched\"", reconciled.Value.ReconciliationSummaryJson);
        Assert.Contains("\"grossPremiumDifference\":0", reconciled.Value.ReconciliationSummaryJson);
    }

    [Fact]
    public async Task ReconcilePremiumRunAsync_MarksMismatchWhenAccountCurrentTotalsDiffer()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        var reconciled = await service.ReconcilePremiumRunAsync(run.Value!.Id, new ReconcileBordereauxRunRequest(
            AccountCurrentRowCount: 1,
            AccountCurrentGrossPremiumTotal: 1450m,
            AccountCurrentGrossCommissionTotal: 362.75m,
            AccountCurrentFeesTotal: 0m,
            AccountCurrentNetDueCarrierTotal: 1087.25m));

        Assert.True(reconciled.IsSuccess);
        Assert.Equal(BordereauxReconciliationStatus.Mismatch, reconciled.Value!.ReconciliationStatus);
        Assert.Contains("\"status\":\"mismatch\"", reconciled.Value.ReconciliationSummaryJson);
        Assert.Contains("\"grossPremiumDifference\":1", reconciled.Value.ReconciliationSummaryJson);
        Assert.Contains("\"netDueCarrierDifference\":1.00", reconciled.Value.ReconciliationSummaryJson);
    }

    [Fact]
    public async Task GetRunsAsync_ReturnsRunHistoryNewestVersionFirst()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);
        await service.CreatePremiumRunSnapshotAsync(profile.Value.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        var runs = await service.GetRunsAsync(profile.Value.Id);

        Assert.Equal(2, runs.Count);
        Assert.Equal(2, runs[0].RunNumber);
        Assert.Equal(1, runs[1].RunNumber);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsFrozenAuditEvidence()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        var savedRun = await service.GetRunAsync(run.Value!.Id);

        Assert.True(savedRun.IsSuccess);
        Assert.Contains("LL-GL-000145-00", savedRun.Value!.SourceRowsSnapshotJson);
        Assert.Contains("BRACE London BDX", savedRun.Value.ProfileSnapshotJson);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_StoresLondonAndAccountCurrentFilesOnRun()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        var generated = await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: Guid.NewGuid());

        Assert.True(generated.IsSuccess);
        Assert.Equal(BordereauxRunStatus.Generated, generated.Value!.Status);
        Assert.EndsWith(".xlsx", generated.Value.LondonBordereauxFileName);
        Assert.EndsWith(".xlsx", generated.Value.AccountCurrentFileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", generated.Value.LondonBordereauxContentType);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", generated.Value.AccountCurrentContentType);
        Assert.Equal(2, blob.Uploads.Count);
        Assert.Contains("londonBordereauxSha256", generated.Value.DetailRowCountsJson);
        Assert.Contains("accountCurrentSha256", generated.Value.DetailRowCountsJson);
        Assert.Contains("LL-GL-000145-00", blob.Uploads[0].Text);
        Assert.Contains("Account Current", blob.Uploads[1].Text);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_UsesFrozenRowsNotCurrentInvoiceValues()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);
        var invoice = await db.Set<Invoice>().SingleAsync();
        // Sentinel with a '.' so it can't false-match a GUID/structural substring in the export XML.
        invoice.GrossPremium = 987654.32m;
        await db.SaveChangesAsync();

        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        Assert.Contains("1451", blob.Uploads[0].Text);
        Assert.DoesNotContain("987654.32", blob.Uploads[0].Text);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_UsesInvoiceStampedCommissionForLondonRows()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability);
        db.Add(new CarrierCommission
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            CommissionRate = 0.24m,
            SMMRetentionRate = 0.05m,
            EffectiveDate = new DateOnly(2025, 1, 1),
            CreatedBy = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(
            db,
            program,
            carrier,
            TransactionType.NewBusiness,
            new DateOnly(2026, 4, 8),
            new DateOnly(2026, 4, 8),
            "LL-GL-000145-00",
            "MS",
            1451m,
            362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        var londonText = blob.Uploads[0].Text;
        Assert.Contains("0.25", londonText);
        Assert.Contains("362.75", londonText);
        Assert.Contains("1088.25", londonText);
        Assert.DoesNotContain("348.24", londonText);
        Assert.DoesNotContain("1102.76", londonText);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_UsesCarrierLobLondonSetupAndInvoiceCommission()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        carrier.DefaultCurrencyCode = "USD";
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability);
        db.Add(new SurplusLinesStateSetup
        {
            StateCode = "MS",
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            EffectiveDate = new DateOnly(2025, 1, 1),
            IsActive = true,
            FilingRequired = true,
            LicenseHolderType = "SMM",
            FilingBrokerName = "Specialty Market Managers, LLC",
            LicenseNumber = "MS-SL-12345",
            LicenseState = "MS",
            BrokerAddressLine1 = "456 Filing Ave",
            BrokerCity = "Ridgeland",
            BrokerState = "MS",
            BrokerZipCode = "39157",
            BrokerCountry = "USA",
        });
        db.Add(new CarrierCommission
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            CommissionRate = 0.24m,
            SMMRetentionRate = 0.05m,
            EffectiveDate = new DateOnly(2025, 1, 1),
            CreatedBy = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        var londonText = blob.Uploads[0].Text;
        Assert.Contains("BRACE-SMM-2025-LOGGING", londonText);
        Assert.Contains("FORESTRY GENERAL LIABILITY", londonText);
        Assert.Contains("LOGGING LUMBERING", londonText);
        Assert.Contains("DIRECT", londonText);
        Assert.Contains("USD", londonText);
        Assert.Contains("0.25", londonText);
        Assert.Contains("362.75", londonText);
        Assert.Contains("Specialty Market Managers, LLC", londonText);
        Assert.Contains("MS-SL-12345", londonText);
        Assert.Contains("456 Filing Ave", londonText);
        Assert.Contains("39157", londonText);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_WritesAutoAndImDetailTabsFromSubmissionSchedules()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.AutoPhysicalDamage);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.InlandMarine);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            LineOfBusiness = null,
        });
        var autoTransaction = await SeedPolicyTransactionWithInvoiceAsync(
            db,
            program,
            carrier,
            TransactionType.NewBusiness,
            new DateOnly(2026, 4, 8),
            new DateOnly(2026, 4, 8),
            "LL-APD-000145-00",
            "MS",
            1451m,
            348.24m,
            lineOfBusiness: PolicyLineOfBusiness.AutoPhysicalDamage);
        db.Add(new SubmissionVehicle
        {
            SubmissionId = autoTransaction.Policy.SubmissionId,
            UnitNumber = 7,
            Year = 2022,
            Make = "Kenworth",
            Model = "T880",
            Vin = "1XKZD49X9NJ123456",
            VehicleClass = VehicleClass.Truck,
            ApdStatedValue = 185000m,
            ApdCompDeductible = 5000m,
        });
        var equipmentType = new EquipmentType { TypeNumber = 12, Name = "Skidder" };
        db.Add(equipmentType);
        await db.SaveChangesAsync();
        var imTransaction = await SeedPolicyTransactionWithInvoiceAsync(
            db,
            program,
            carrier,
            TransactionType.NewBusiness,
            new DateOnly(2026, 4, 9),
            new DateOnly(2026, 4, 9),
            "LL-IM-000146-00",
            "MS",
            500m,
            120m,
            lineOfBusiness: PolicyLineOfBusiness.InlandMarine);
        db.Add(new SubmissionEquipment
        {
            SubmissionId = imTransaction.Policy.SubmissionId,
            ItemNumber = 3,
            Year = 2021,
            Make = "Tigercat",
            Model = "620H",
            SerialNumber = "SKD-620H-4455",
            Value = 275000m,
            Deductible = 10000m,
            EquipmentTypeId = equipmentType.Id,
        });
        await db.SaveChangesAsync();
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        var generated = await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        var londonText = blob.Uploads[0].Text;
        Assert.Contains("\"autoVehicleRows\":1", generated.Value!.DetailRowCountsJson);
        Assert.Contains("\"imUnitRows\":1", generated.Value.DetailRowCountsJson);
        Assert.Contains("1XKZD49X9NJ123456", londonText);
        Assert.Contains("Kenworth", londonText);
        Assert.Contains("185000", londonText);
        Assert.Contains("SKD-620H-4455", londonText);
        Assert.Contains("Tigercat", londonText);
        Assert.Contains("275000", londonText);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_WritesInsuredAndPolicyIssueColumns()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            IncludedTransactionTypesJson = """["NewBusiness","Endorsement","Renewal"]""",
        });
        await SeedPolicyTransactionWithInvoiceAsync(
            db,
            program,
            carrier,
            TransactionType.Renewal,
            new DateOnly(2026, 4, 8),
            new DateOnly(2026, 4, 10),
            "LL-GL-000145-01",
            "MS",
            1451m,
            362.75m,
            issuedDate: new DateOnly(2026, 4, 10),
            policyTermNumber: 2);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        var londonText = blob.Uploads[0].Text;
        Assert.Contains("100 Main", londonText);
        Assert.Contains("Hinds", londonText);
        Assert.Contains("39000", londonText);
        Assert.Contains("04/10/2026", londonText);
        Assert.Contains("Forestry Operations", londonText);
        Assert.Contains("Renewal", londonText);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_WritesClarifiedLondonMappings()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability);
        var intermediary = new Intermediary
        {
            Name = "London Broker Ltd",
            ReferenceNumber = "LON-001",
            AddressLine1 = "1 Lime Street",
            City = "London",
            State = "UK",
            ZipCode = "EC3M",
            Country = "GBR",
            IsActive = true,
        };
        db.Add(intermediary);
        db.Add(new IntermediaryProgramCarrierLobSetup
        {
            Intermediary = intermediary,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            EffectiveDate = new DateOnly(2025, 1, 1),
            BrokerageRate = 0.015m,
            CreatePayable = false,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        var agent = new Agent
        {
            Name = "Pine Producer",
            AgencyName = "Pine Agency",
            LicenseNumber = "AGT-100",
            IsActive = true,
            Locations =
            {
                new AgentLocation
                {
                    AddressLine1 = "12 Producer Rd",
                    City = "Birmingham",
                    State = "AL",
                    ZipCode = "35203",
                    IsPrimary = true,
                },
            },
        };
        var transaction = await SeedPolicyTransactionWithInvoiceAsync(
            db,
            program,
            carrier,
            TransactionType.Endorsement,
            new DateOnly(2026, 4, 8),
            new DateOnly(2026, 4, 10),
            "LL-GL-000145-00",
            "MS",
            1451m,
            362.75m,
            agent: agent,
            totalFees: 87.65m);
        db.Add(new SubmissionLocation
        {
            SubmissionId = transaction.Policy.SubmissionId,
            LocationNumber = 2,
            Address = "500 Risk Yard",
            City = "Mayersville",
            State = "MS",
            County = "Issaquena",
            ZipCode = "39113",
            Country = "USA",
            IsPrimary = true,
        });
        db.Add(new SubmissionGLCoverages
        {
            SubmissionId = transaction.Policy.SubmissionId,
            EachOccurrence = 1_000_000m,
            GeneralAggregate = 2_000_000m,
        });
        db.Add(new SubmissionGLClassification
        {
            SubmissionId = transaction.Policy.SubmissionId,
            LocationNumber = 2,
            ClassCode = "97111",
            Exposure = 1_250_000m,
        });
        await SeedRatingSnapshotAsync(
            db,
            transaction,
            scheduleModifier: 1.15m,
            lines:
            [
                new QuoteRatingLine
                {
                    ExposureRef = "GL-002-97111",
                    Inputs = """{"class_code":"97111","exposure":1250000,"occ_limit":1000000}""",
                    FactorsApplied = "{}",
                    LinePremium = 6200m,
                },
            ]);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            IncludedTransactionTypesJson = """["Endorsement"]""",
        });
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        var londonText = blob.Uploads[0].Text;
        Assert.Contains("500 Risk Yard", londonText);
        Assert.Contains("Issaquena", londonText);
        Assert.Contains("39113", londonText);
        Assert.Contains("State Taxes and Fees", londonText);
        Assert.Contains("87.65", londonText);
        Assert.Contains("Producing Agents and Brokers", londonText);
        Assert.Contains("Pine Agency", londonText);
        Assert.Contains("AGT-100", londonText);
        Assert.Contains("12 Producer Rd", londonText);
        Assert.Contains("35203", londonText);
        Assert.Contains("0.015", londonText);
        Assert.Contains("21.77", londonText);
        Assert.Contains("1250000", londonText);
        Assert.Contains("6200", londonText);
        Assert.Contains("1000000", londonText);
        Assert.Contains("1.15", londonText);
        Assert.Contains(">AP<", londonText);
    }

    [Fact]
    public async Task GeneratePremiumExportPackageAsync_IgnoresDeletedIntermediaryBrokerageSetups()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability);
        var intermediary = new Intermediary
        {
            Name = "Deleted Broker Ltd",
            IsActive = true,
        };
        db.Add(intermediary);
        db.Add(new IntermediaryProgramCarrierLobSetup
        {
            Intermediary = intermediary,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            EffectiveDate = new DateOnly(2025, 1, 1),
            BrokerageRate = 0.015m,
            CreatePayable = false,
            IsActive = true,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        var londonText = blob.Uploads[0].Text;
        Assert.Contains("1088.25", londonText);
        Assert.DoesNotContain("0.015", londonText);
        Assert.DoesNotContain("21.77", londonText);
        Assert.DoesNotContain("1066.48", londonText);
    }

    [Theory]
    [InlineData(TransactionType.Cancellation, "CP")]
    [InlineData(TransactionType.Reinstatement, "RN")]
    public async Task GeneratePremiumExportPackageAsync_UsesLondonTransactionCodes(TransactionType transactionType, string expectedCode)
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        await SeedProgramCarrierLobSetupAsync(db, program, carrier, PolicyLineOfBusiness.GeneralLiability);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id) with
        {
            IncludedTransactionTypesJson = $"""["{transactionType}"]""",
        });
        await SeedPolicyTransactionWithInvoiceAsync(
            db,
            program,
            carrier,
            transactionType,
            new DateOnly(2026, 4, 8),
            new DateOnly(2026, 4, 8),
            $"LL-GL-{expectedCode}",
            "MS",
            transactionType == TransactionType.Cancellation ? -100m : 100m,
            transactionType == TransactionType.Cancellation ? -25m : 25m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        Assert.Contains($">{expectedCode}<", blob.Uploads[0].Text);
    }

    [Fact]
    public async Task GetRunFileDownloadUrlAsync_ReturnsSignedUrlForGeneratedLondonFile()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        await SeedPolicyTransactionWithInvoiceAsync(db, program, carrier, TransactionType.NewBusiness, new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 8), "LL-GL-000145-00", "MS", 1451m, 362.75m);
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);
        await service.GeneratePremiumExportPackageAsync(run.Value!.Id, generatedById: null);

        var url = await service.GetRunFileDownloadUrlAsync(run.Value.Id, BordereauxRunFileKind.LondonBordereaux);

        Assert.True(url.IsSuccess);
        Assert.Contains("bordereaux-test/1/", url.Value);
        Assert.Contains("London-BDX", url.Value);
    }

    [Fact]
    public async Task GetRunFileDownloadUrlAsync_RejectsRunWithoutGeneratedFile()
    {
        await using var db = CreateDb();
        var blob = new FakeBlobStorageService();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var service = new BordereauxService(db, blob);
        var profile = await service.CreateProfileAsync(ValidRequest(program.Id, carrier.Id));
        var run = await service.CreatePremiumRunSnapshotAsync(profile.Value!.Id, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), generatedById: null);

        var url = await service.GetRunFileDownloadUrlAsync(run.Value!.Id, BordereauxRunFileKind.AccountCurrent);

        Assert.False(url.IsSuccess);
        Assert.Equal("FILE_NOT_GENERATED", url.ErrorCode);
    }

    private static UpsertBordereauxProfileRequest ValidRequest(Guid programId, Guid carrierId) => new(
        Name: "BRACE London BDX",
        ProgramConfigurationId: programId,
        CarrierId: carrierId,
        LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
        StateCode: null,
        ReportType: BordereauxReportType.Premium,
        Frequency: BordereauxFrequency.Monthly,
        OutputFormat: BordereauxOutputFormat.Xlsx,
        DateBasis: BordereauxDateBasis.EffectiveOrBoundDateGreater,
        RequiresAccountCurrent: true,
        IsActive: true,
        RequiredTabsJson: """["General Liability (Section 1)","Auto Veh Info","IM Unit Info","Acct Current"]""",
        RequiredColumnsJson: """["Certificate Ref","Gross premium paid this time","Net Premium to London in original currency"]""",
        MappingRulesJson: """{"commissionBasis":"commissionPlusBrokerage"}""",
        StaticValuesJson: """{"umr":"BRACE-SMM-2025-LOGGING","coverholderPin":"USA00060"}""",
        ValidationRulesJson: """{"requireReconciliation":true}""",
        IncludedTransactionTypesJson: """["NewBusiness","Endorsement"]""",
        Notes: null);

    private static async Task<(ProgramConfiguration Program, Carrier Carrier)> SeedProgramCarrierAsync(ApplicationDbContext db)
    {
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();
        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2025, 1, 1),
        };
        programCarrier.LinesOfBusiness.Add(new ProgramCarrierLineOfBusiness
        {
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            IsActive = true,
            EffectiveDate = new DateOnly(2025, 1, 1),
        });
        db.Add(programCarrier);
        await db.SaveChangesAsync();
        return (program, carrier);
    }

    private static async Task<ProgramCarrierLineOfBusiness> SeedProgramCarrierLobSetupAsync(
        ApplicationDbContext db,
        ProgramConfiguration program,
        Carrier carrier,
        PolicyLineOfBusiness lineOfBusiness)
    {
        var programCarrier = await db.Set<ProgramCarrier>()
            .Include(c => c.LinesOfBusiness)
            .SingleOrDefaultAsync(c => c.ProgramConfigurationId == program.Id && c.CarrierId == carrier.Id);

        if (programCarrier is null)
        {
            programCarrier = new ProgramCarrier
            {
                ProgramConfigurationId = program.Id,
                CarrierId = carrier.Id,
                IsActive = true,
                EffectiveDate = new DateOnly(2025, 1, 1),
            };
            db.Add(programCarrier);
        }

        var lob = programCarrier.LinesOfBusiness
            .FirstOrDefault(l => l.LineOfBusiness == lineOfBusiness);
        if (lob is null)
        {
            lob = new ProgramCarrierLineOfBusiness
            {
                LineOfBusiness = lineOfBusiness,
            };
            programCarrier.LinesOfBusiness.Add(lob);
            db.Set<ProgramCarrierLineOfBusiness>().Add(lob);
        }

        lob.IsActive = true;
        lob.EffectiveDate = new DateOnly(2025, 1, 1);
        lob.ExpirationDate = null;
        lob.LondonUmr = "BRACE-SMM-2025-LOGGING";
        lob.LondonSectionNumber = "Section No 1";
        lob.LondonClassOfBusiness = "FORESTRY GENERAL LIABILITY";
        lob.LondonRiskCode = "LOGGING LUMBERING";
        lob.LondonInsuranceType = "DIRECT";

        await db.SaveChangesAsync();
        return lob;
    }

    private static async Task<ProgramCarrierLobState> SeedProgramCarrierLobStateAsync(
        ApplicationDbContext db,
        ProgramConfiguration program,
        Carrier carrier,
        PolicyLineOfBusiness lineOfBusiness,
        string stateCode)
    {
        var lob = await db.Set<ProgramCarrierLineOfBusiness>()
            .Include(l => l.ProgramCarrier)
            .Include(l => l.States)
            .SingleAsync(l =>
                l.LineOfBusiness == lineOfBusiness &&
                l.ProgramCarrier.ProgramConfigurationId == program.Id &&
                l.ProgramCarrier.CarrierId == carrier.Id);

        var normalizedState = stateCode.Trim().ToUpperInvariant();
        var state = lob.States.FirstOrDefault(s => s.StateCode == normalizedState);
        if (state is null)
        {
            state = new ProgramCarrierLobState
            {
                StateCode = normalizedState,
            };
            lob.States.Add(state);
            db.Set<ProgramCarrierLobState>().Add(state);
        }

        state.IsActive = true;
        state.EffectiveDate = new DateOnly(2025, 1, 1);
        state.ExpirationDate = null;
        await db.SaveChangesAsync();
        return state;
    }

    private static async Task SeedSurplusLinesSetupAsync(
        ApplicationDbContext db,
        ProgramConfiguration program,
        Carrier carrier,
        PolicyLineOfBusiness lineOfBusiness,
        string stateCode)
    {
        db.Add(new SurplusLinesStateSetup
        {
            StateCode = stateCode,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = lineOfBusiness,
            EffectiveDate = new DateOnly(2025, 1, 1),
            IsActive = true,
            FilingRequired = true,
            LicenseHolderType = "Broker",
            FilingBrokerName = "SMM Filing",
            LicenseNumber = "SL-100",
            LicenseState = stateCode,
            BrokerAddressLine1 = "100 Filing St",
            BrokerCity = "Jackson",
            BrokerState = stateCode,
            BrokerZipCode = "39000",
            BrokerCountry = "USA",
        });
        await db.SaveChangesAsync();
    }

    private static async Task<PolicyTransaction> SeedPolicyTransactionWithInvoiceAsync(
        ApplicationDbContext db,
        ProgramConfiguration program,
        Carrier carrier,
        TransactionType transactionType,
        DateOnly effectiveDate,
        DateOnly invoiceDate,
        string policyNumber,
        string state,
        decimal grossPremium,
        decimal commissionAmount,
        string invoiceStatus = "Posted",
        PolicyLineOfBusiness lineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
        DateOnly? issuedDate = null,
        int policyTermNumber = 1,
        Agent? agent = null,
        decimal totalFees = 0m)
    {
        var insured = new Insured
        {
            InsuredType = InsuredType.Commercial,
            CompanyName = "Test Logging LLC",
            OperationType = "Forestry Operations",
            AddressLine1 = "100 Main",
            City = "Jackson",
            State = state,
            ZipCode = "39000",
            County = "Hinds",
            CreatedById = Guid.NewGuid(),
        };
        var submission = new Submission
        {
            SubmissionNumber = $"SUB-{Guid.NewGuid():N}",
            Insured = insured,
            Agent = agent,
            UnderwriterId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
        };
        var quote = new Quote
        {
            QuoteNumber = $"Q-{Guid.NewGuid():N}",
            Submission = submission,
            ProgramId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = lineOfBusiness,
            Status = QuoteStatus.Bound,
            EffectiveDate = effectiveDate,
            ExpirationDate = effectiveDate.AddYears(1),
            PremiumAmount = grossPremium,
            TotalPremium = grossPremium,
            CreatedById = Guid.NewGuid(),
        };
        var policy = new Policy
        {
            PolicyNumber = policyNumber,
            Submission = submission,
            BoundQuote = quote,
            ProgramId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = lineOfBusiness,
            EffectiveDate = effectiveDate,
            ExpirationDate = effectiveDate.AddYears(1),
            PolicyTermNumber = policyTermNumber,
            PremiumAmount = grossPremium,
            TotalPremium = grossPremium,
            BoundDate = invoiceDate,
            IssuedDate = issuedDate,
        };
        var transaction = new PolicyTransaction
        {
            Policy = policy,
            TransactionType = transactionType,
            Status = PolicyTransactionStatus.Completed,
            TransactionNumber = $"TXN-{Guid.NewGuid():N}",
            EffectiveDate = effectiveDate,
            ExpirationDate = effectiveDate.AddYears(1),
            PremiumChange = grossPremium,
            NewTotalPremium = grossPremium,
            ProcessedById = Guid.NewGuid(),
            ProcessedAt = invoiceDate.ToDateTime(new TimeOnly(12, 0)),
            CompletedAt = invoiceDate.ToDateTime(new TimeOnly(12, 0)),
        };
        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            PolicyTransactionId = transaction.Id,
            EffectiveDate = effectiveDate,
            InvoiceDate = invoiceDate,
            GrossPremium = grossPremium,
            CommissionAmount = commissionAmount,
            TotalAmount = grossPremium + totalFees,
            TotalFees = totalFees,
            Status = invoiceStatus,
            LedgerTransactionId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
        };

        db.AddRange(transaction, invoice);
        await db.SaveChangesAsync();
        return transaction;
    }

    private static async Task SeedRatingSnapshotAsync(
        ApplicationDbContext db,
        PolicyTransaction transaction,
        decimal scheduleModifier,
        IReadOnlyList<QuoteRatingLine> lines)
    {
        var plan = new RatingPlan
        {
            Name = "Test Rating Plan",
            LineOfBusiness = transaction.Policy.LineOfBusiness,
            FormulaKey = "TEST",
            Status = PlanStatus.Active,
        };
        var version = new RatingPlanVersion
        {
            RatingPlan = plan,
            VersionNumber = 1,
            Status = PlanStatus.Active,
            EffectiveDate = new DateOnly(2025, 1, 1),
            ScheduleMin = 0.8m,
            ScheduleMax = 1.2m,
        };
        var snapshot = new QuoteRatingSnapshot
        {
            QuoteId = transaction.Policy.BoundQuoteId,
            PolicyTransactionId = transaction.Id,
            RatingPlanVersion = version,
            RatedAt = DateTime.UtcNow,
            RatedById = Guid.NewGuid(),
            ManualPremium = lines.Sum(l => l.LinePremium),
            ScheduleModifier = scheduleModifier,
            GrandTotalPremium = lines.Sum(l => l.LinePremium),
            IsBoundSnapshot = true,
        };
        foreach (var line in lines)
            snapshot.Lines.Add(line);

        db.Add(snapshot);
        await db.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeBlobStorageService : SIMS.Application.Interfaces.Services.IBlobStorageService
    {
        public List<(string FileName, string ContentType, byte[] Bytes, string Text)> Uploads { get; } = [];

        public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory);
            var bytes = memory.ToArray();
            Uploads.Add((fileName, contentType, bytes, ExtractText(bytes)));
            return $"bordereaux-test/{Uploads.Count}/{fileName}";
        }

        public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null) => Task.FromResult($"https://blob.test/{blobPath}?download={Uri.EscapeDataString(fileName)}");
        public Task<byte[]> DownloadAsync(string blobPath) => Task.FromResult(Array.Empty<byte>());
        public Task DeleteAsync(string blobPath) => Task.CompletedTask;

        private static string ExtractText(byte[] bytes)
        {
            try
            {
                using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                var text = new StringBuilder();
                foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                {
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    text.AppendLine(reader.ReadToEnd());
                }

                return text.ToString();
            }
            catch (InvalidDataException)
            {
                return Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
