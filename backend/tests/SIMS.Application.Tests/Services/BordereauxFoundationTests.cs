using Microsoft.EntityFrameworkCore;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Bordereaux;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class BordereauxFoundationTests
{
    [Fact]
    public async Task BordereauxProfile_PersistsLondonPremiumAndAccountCurrentTemplate()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        db.AddRange(program, carrier);
        await db.SaveChangesAsync();

        var profile = new BordereauxProfile
        {
            Name = "BRACE London Premium BDX",
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            ReportType = BordereauxReportType.Premium,
            Frequency = BordereauxFrequency.Monthly,
            OutputFormat = BordereauxOutputFormat.Xlsx,
            DateBasis = BordereauxDateBasis.EffectiveOrBoundDateGreater,
            RequiresAccountCurrent = true,
            RequiredTabsJson = """["General Liability (Section 1)","Acct Current"]""",
            RequiredColumnsJson = """["Certificate Ref","Gross premium paid this time","Net Premium to London in original currency"]""",
            MappingRulesJson = """{"commissionBasis":"commissionPlusBrokerage"}""",
            StaticValuesJson = """{"umr":"BRACE-SMM-2025-LOGGING","coverholderPin":"USA00060"}""",
            ValidationRulesJson = """{"requireReconciliation":true}""",
            IncludedTransactionTypesJson = """["NewBusiness","Endorsement"]""",
        };

        db.Add(profile);
        await db.SaveChangesAsync();

        var saved = await db.Set<BordereauxProfile>()
            .Include(p => p.ProgramConfiguration)
            .Include(p => p.Carrier)
            .SingleAsync();

        Assert.Equal("LONGLEAF", saved.ProgramConfiguration.Code);
        Assert.Equal("BRACE", saved.Carrier.Name);
        Assert.True(saved.RequiresAccountCurrent);
        Assert.Equal(BordereauxDateBasis.EffectiveOrBoundDateGreater, saved.DateBasis);
        Assert.Contains("Acct Current", saved.RequiredTabsJson);
    }

    [Fact]
    public async Task BordereauxRun_PersistsTandemFileAndReconciliationState()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var profile = new BordereauxProfile
        {
            Name = "BRACE Monthly",
            ProgramConfiguration = program,
            Carrier = carrier,
            ReportType = BordereauxReportType.Premium,
            Frequency = BordereauxFrequency.Monthly,
            OutputFormat = BordereauxOutputFormat.Xlsx,
            DateBasis = BordereauxDateBasis.EffectiveOrBoundDateGreater,
            RequiresAccountCurrent = true,
            RequiredTabsJson = """["General Liability (Section 1)","Inland Marine (Section 3)","Auto Veh Info","IM Unit Info","Acct Current"]""",
            RequiredColumnsJson = """["Policy Number","Gross Premium","Gross Commission","Net Due Carrier"]""",
            MappingRulesJson = "{}",
            StaticValuesJson = "{}",
            ValidationRulesJson = "{}",
            IncludedTransactionTypesJson = """["NewBusiness","Endorsement"]""",
        };
        db.Add(profile);
        await db.SaveChangesAsync();

        var run = new BordereauxRun
        {
            BordereauxProfileId = profile.Id,
            PeriodStart = new DateOnly(2026, 4, 1),
            PeriodEnd = new DateOnly(2026, 4, 30),
            Status = BordereauxRunStatus.Generated,
            ReconciliationStatus = BordereauxReconciliationStatus.Matched,
            GeneratedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            LondonBordereauxFileName = "Premium Bordx_Logging Template_April2026.xlsx",
            LondonBordereauxBlobPath = "bordereaux/brace/2026-04/london.xlsx",
            LondonBordereauxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            AccountCurrentFileName = "April 2026 AC.xlsx",
            AccountCurrentBlobPath = "bordereaux/brace/2026-04/account-current.xlsx",
            AccountCurrentContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            BordereauxRowCount = 14,
            AccountCurrentRowCount = 14,
            DetailRowCountsJson = """{"IM Unit Info":6,"Auto Veh Info":0}""",
            ValidationSummaryJson = """{"errors":0,"warnings":0}""",
            ReconciliationSummaryJson = """{"grossPremium":47272.00,"grossCommission":11454.88,"netDueCarrier":35817.12}""",
        };

        db.Add(run);
        await db.SaveChangesAsync();

        var saved = await db.Set<BordereauxRun>()
            .Include(r => r.Profile)
            .SingleAsync();

        Assert.Equal(BordereauxRunStatus.Generated, saved.Status);
        Assert.Equal(BordereauxReconciliationStatus.Matched, saved.ReconciliationStatus);
        Assert.Equal(14, saved.BordereauxRowCount);
        Assert.Equal(14, saved.AccountCurrentRowCount);
        Assert.Equal(profile.Id, saved.Profile.Id);
    }

    [Fact]
    public void BordereauxProfile_HasUniqueActiveScopeIndex()
    {
        using var db = CreateDb();

        var entity = db.Model.FindEntityType(typeof(BordereauxProfile));
        Assert.NotNull(entity);

        var index = entity!.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(BordereauxProfile.ProgramConfigurationId),
                nameof(BordereauxProfile.CarrierId),
                nameof(BordereauxProfile.ReportType),
                nameof(BordereauxProfile.LineOfBusiness),
                nameof(BordereauxProfile.StateCode),
                nameof(BordereauxProfile.IsActive),
            }));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
