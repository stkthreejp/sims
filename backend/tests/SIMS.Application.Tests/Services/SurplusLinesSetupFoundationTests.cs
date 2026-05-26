using Microsoft.EntityFrameworkCore;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class SurplusLinesSetupFoundationTests
{
    [Fact]
    public async Task SurplusLinesStateSetup_PersistsStateLicenseWordingAndFeeLinks()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Name = "BRACE", IsActive = true };
        var taxFee = new FeeDefinition
        {
            Code = "MS_SL_TAX",
            DisplayName = "MS Surplus Lines Tax",
            FeeCategory = "Tax",
            IsTaxable = false,
            CalculationOrder = 10,
            LedgerAccountId = 2200,
        };
        var stampingFee = new FeeDefinition
        {
            Code = "MS_STAMP",
            DisplayName = "MS Stamping Fee",
            FeeCategory = "StampingFee",
            IsTaxable = false,
            CalculationOrder = 20,
            LedgerAccountId = 2200,
        };
        db.AddRange(program, carrier, taxFee, stampingFee);
        await db.SaveChangesAsync();

        db.Set<SurplusLinesStateSetup>().Add(new SurplusLinesStateSetup
        {
            StateCode = "MS",
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            EffectiveDate = new DateOnly(2026, 1, 1),
            FilingRequired = true,
            LicenseHolderType = "SMM",
            FilingBrokerName = "Specialty Market Managers, LLC",
            LicenseNumber = "MS-SL-12345",
            LicenseState = "MS",
            BrokerAddressLine1 = "100 Filing Way",
            BrokerCity = "Jackson",
            BrokerState = "MS",
            BrokerZipCode = "39201",
            BrokerCountry = "USA",
            StampingWording = "This policy is written through surplus lines.",
            RequiredNoticeText = "Attach Mississippi surplus lines notice.",
            PaperworkNotes = "File through surplus lines vendor monthly.",
            FilingNotes = "Use SMM license unless state setup says otherwise.",
            SurplusLinesTaxFeeDefinitionId = taxFee.Id,
            StampingFeeDefinitionId = stampingFee.Id,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var saved = await db.Set<SurplusLinesStateSetup>()
            .Include(s => s.ProgramConfiguration)
            .Include(s => s.Carrier)
            .Include(s => s.SurplusLinesTaxFeeDefinition)
            .Include(s => s.StampingFeeDefinition)
            .SingleAsync();

        Assert.Equal("MS", saved.StateCode);
        Assert.Equal("Longleaf", saved.ProgramConfiguration!.Name);
        Assert.Equal("BRACE", saved.Carrier!.Name);
        Assert.Equal(PolicyLineOfBusiness.GeneralLiability, saved.LineOfBusiness);
        Assert.True(saved.FilingRequired);
        Assert.Equal("SMM", saved.LicenseHolderType);
        Assert.Equal("MS-SL-12345", saved.LicenseNumber);
        Assert.Contains("surplus lines", saved.StampingWording);
        Assert.Equal("MS_SL_TAX", saved.SurplusLinesTaxFeeDefinition!.Code);
        Assert.Equal("MS_STAMP", saved.StampingFeeDefinition!.Code);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
