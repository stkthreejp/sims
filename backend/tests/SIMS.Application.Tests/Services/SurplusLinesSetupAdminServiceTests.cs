using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.SurplusLines;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class SurplusLinesSetupAdminServiceTests
{
    [Fact]
    public async Task CreateAsync_SavesStateLicenseWordingAndFeeLinks()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var taxFee = new FeeDefinition { Code = "MS_SL_TAX", DisplayName = "MS Surplus Lines Tax", FeeCategory = "Tax", LedgerAccountId = 1 };
        var stampingFee = new FeeDefinition { Code = "MS_STAMP", DisplayName = "MS Stamping Fee", FeeCategory = "StampingFee", LedgerAccountId = 1 };
        db.AddRange(program, carrier, taxFee, stampingFee);
        await db.SaveChangesAsync();

        var service = new SurplusLinesSetupAdminService(db);

        var result = await service.CreateAsync(new UpsertSurplusLinesStateSetupRequest(
            StateCode: "ms",
            ProgramConfigurationId: program.Id,
            CarrierId: carrier.Id,
            LineOfBusiness: PolicyLineOfBusiness.GeneralLiability,
            EffectiveDate: new DateOnly(2026, 1, 1),
            ExpirationDate: null,
            IsActive: true,
            FilingRequired: true,
            LicenseHolderType: "SMM",
            FilingBrokerName: "Specialty Market Managers, LLC",
            LicenseNumber: "MS-SL-12345",
            LicenseState: "MS",
            BrokerAddressLine1: "123 Main",
            BrokerAddressLine2: null,
            BrokerCity: "Ridgeland",
            BrokerState: "MS",
            BrokerZipCode: "39157",
            BrokerCountry: "USA",
            StampingWording: "MS stamping wording",
            RequiredNoticeText: "MS notice",
            PaperworkNotes: "Upload state affidavit",
            FilingNotes: "Filed by vendor",
            SurplusLinesTaxFeeDefinitionId: taxFee.Id,
            StampingFeeDefinitionId: stampingFee.Id,
            FilingFeeDefinitionId: null));

        Assert.True(result.IsSuccess);
        Assert.Equal("MS", result.Value!.StateCode);
        Assert.Equal(program.Id, result.Value.ProgramConfigurationId);
        Assert.Equal("Longleaf", result.Value.ProgramName);
        Assert.Equal(carrier.Id, result.Value.CarrierId);
        Assert.Equal("BRACE", result.Value.CarrierName);
        Assert.Equal("MS Surplus Lines Tax", result.Value.SurplusLinesTaxFeeName);
        Assert.Equal("MS Stamping Fee", result.Value.StampingFeeName);
        Assert.Equal("MS notice", result.Value.RequiredNoticeText);
    }

    [Fact]
    public async Task CopyAsync_CopiesSetupToTargetState()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var fee = new FeeDefinition { Code = "SL_TAX", DisplayName = "Surplus Lines Tax", FeeCategory = "Tax", LedgerAccountId = 1 };
        db.AddRange(program, carrier, fee);
        await db.SaveChangesAsync();

        var service = new SurplusLinesSetupAdminService(db);
        var source = await service.CreateAsync(new UpsertSurplusLinesStateSetupRequest(
            "NC",
            program.Id,
            carrier.Id,
            PolicyLineOfBusiness.InlandMarine,
            new DateOnly(2026, 1, 1),
            null,
            true,
            true,
            "SMM",
            "Specialty Market Managers, LLC",
            "NC-SL-1",
            "NC",
            "123 Main",
            null,
            "Charlotte",
            "NC",
            "28202",
            "USA",
            "NC wording",
            "NC notice",
            "NC paperwork",
            "NC filing notes",
            fee.Id,
            null,
            null));

        var copy = await service.CopyAsync(source.Value!.Id, new CopySurplusLinesStateSetupRequest("SC"));

        Assert.True(copy.IsSuccess);
        Assert.Equal("SC", copy.Value!.StateCode);
        Assert.Equal("NC-SL-1", copy.Value.LicenseNumber);
        Assert.Equal("NC notice", copy.Value.RequiredNoticeText);
        Assert.Equal(fee.Id, copy.Value.SurplusLinesTaxFeeDefinitionId);
    }

    [Fact]
    public async Task CreateAsync_RejectsMissingFeeDefinitionLink()
    {
        await using var db = CreateDb();
        var service = new SurplusLinesSetupAdminService(db);

        var result = await service.CreateAsync(new UpsertSurplusLinesStateSetupRequest(
            "TX",
            null,
            null,
            null,
            new DateOnly(2026, 1, 1),
            null,
            true,
            true,
            "SMM",
            "Specialty Market Managers, LLC",
            "TX-SL-1",
            "TX",
            "123 Main",
            null,
            "Dallas",
            "TX",
            "75201",
            "USA",
            null,
            null,
            null,
            null,
            999,
            null,
            null));

        Assert.False(result.IsSuccess);
        Assert.Equal("FEE_DEFINITION_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_RejectsVendorFilingWithoutVendorPayee()
    {
        await using var db = CreateDb();
        var service = new SurplusLinesSetupAdminService(db);

        var result = await service.CreateAsync(new UpsertSurplusLinesStateSetupRequest(
            "TX",
            null,
            null,
            null,
            new DateOnly(2026, 1, 1),
            null,
            true,
            true,
            "SMM",
            "Specialty Market Managers, LLC",
            "TX-SL-1",
            "TX",
            "123 Main",
            null,
            "Dallas",
            "TX",
            "75201",
            "USA",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            CreateFilingPayable: true));

        Assert.False(result.IsSuccess);
        Assert.Equal("FILING_PAYEE_REQUIRED", result.ErrorCode);
        Assert.Contains("vendor", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_ReturnsValidationMessageWhenLinkedFeeHasNoMatchingRule()
    {
        await using var db = CreateDb();
        var fee = new FeeDefinition { Code = "TX_SL_TAX", DisplayName = "TX Surplus Lines Tax", FeeCategory = "Tax", LedgerAccountId = 1 };
        db.Add(fee);
        await db.SaveChangesAsync();

        var service = new SurplusLinesSetupAdminService(db);

        var result = await service.CreateAsync(new UpsertSurplusLinesStateSetupRequest(
            "TX",
            null,
            null,
            PolicyLineOfBusiness.GeneralLiability,
            new DateOnly(2026, 1, 1),
            null,
            true,
            true,
            "SMM",
            "Specialty Market Managers, LLC",
            "TX-SL-1",
            "TX",
            "123 Main",
            null,
            "Dallas",
            "TX",
            "75201",
            "USA",
            null,
            null,
            null,
            null,
            fee.Id,
            null,
            null));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.FeeValidationMessages, message => message.Contains("TX Surplus Lines Tax"));
    }

    [Fact]
    public async Task CreateAsync_DoesNotReturnValidationMessageWhenLinkedFeeHasMatchingRule()
    {
        await using var db = CreateDb();
        var fee = new FeeDefinition { Code = "TX_SL_TAX", DisplayName = "TX Surplus Lines Tax", FeeCategory = "Tax", LedgerAccountId = 1 };
        db.Add(fee);
        await db.SaveChangesAsync();
        db.Add(new FeeRuleVersion
        {
            FeeDefinitionId = fee.Id,
            StateCode = "TX",
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability.ToString(),
            EffectiveDate = new DateOnly(2026, 1, 1),
            CalcType = "Percent",
            PercentRate = 0.0485m,
            InstallmentBehavior = "PerInstallment",
            RoundingMode = "NearestCent",
            PayableRouting = "Entity",
            CreatedBy = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var service = new SurplusLinesSetupAdminService(db);

        var result = await service.CreateAsync(new UpsertSurplusLinesStateSetupRequest(
            "TX",
            null,
            null,
            PolicyLineOfBusiness.GeneralLiability,
            new DateOnly(2026, 1, 1),
            null,
            true,
            true,
            "SMM",
            "Specialty Market Managers, LLC",
            "TX-SL-1",
            "TX",
            "123 Main",
            null,
            "Dallas",
            "TX",
            "75201",
            "USA",
            null,
            null,
            null,
            null,
            fee.Id,
            null,
            null));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.FeeValidationMessages);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
