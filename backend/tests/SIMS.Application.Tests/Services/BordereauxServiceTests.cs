using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
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
    public async Task GetProfilesAsync_FiltersByProgramAndActiveStatus()
    {
        await using var db = CreateDb();
        var (program, carrier) = await SeedProgramCarrierAsync(db);
        var otherProgram = new ProgramConfiguration { Name = "Shuttlebee", Code = "SHUTTLEBEE", IsActive = true };
        db.Add(otherProgram);
        await db.SaveChangesAsync();
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
        savedInvoice.GrossPremium = 999m;
        await db.SaveChangesAsync();

        var savedRun = await db.Set<SIMS.Domain.Entities.Bordereaux.BordereauxRun>().SingleAsync();
        Assert.Contains("BRACE-SMM-2025-LOGGING", savedRun.ProfileSnapshotJson);
        Assert.DoesNotContain("CHANGED", savedRun.ProfileSnapshotJson);
        Assert.Contains("1451", savedRun.SourceRowsSnapshotJson);
        Assert.DoesNotContain("999", savedRun.SourceRowsSnapshotJson);
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
        return (program, carrier);
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
        string invoiceStatus = "Posted")
    {
        var insured = new Insured
        {
            InsuredType = InsuredType.Commercial,
            CompanyName = "Test Logging LLC",
            AddressLine1 = "100 Main",
            City = "Jackson",
            State = state,
            ZipCode = "39000",
            CreatedById = Guid.NewGuid(),
        };
        var submission = new Submission
        {
            SubmissionNumber = $"SUB-{Guid.NewGuid():N}",
            Insured = insured,
            UnderwriterId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
        };
        var quote = new Quote
        {
            QuoteNumber = $"Q-{Guid.NewGuid():N}",
            Submission = submission,
            ProgramId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
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
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            EffectiveDate = effectiveDate,
            ExpirationDate = effectiveDate.AddYears(1),
            PremiumAmount = grossPremium,
            TotalPremium = grossPremium,
            BoundDate = invoiceDate,
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
            TotalAmount = grossPremium,
            TotalFees = 0m,
            Status = invoiceStatus,
            LedgerTransactionId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
        };

        db.AddRange(transaction, invoice);
        await db.SaveChangesAsync();
        return transaction;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
