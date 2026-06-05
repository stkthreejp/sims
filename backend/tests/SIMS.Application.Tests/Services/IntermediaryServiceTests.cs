using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Intermediaries;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class IntermediaryServiceTests
{
    [Fact]
    public async Task CreateAsync_StoresContactAddressAndBankDetails()
    {
        await using var db = CreateDb();
        var service = new IntermediaryService(db);

        var result = await service.CreateAsync(new CreateIntermediaryRequest(
            Name: "  Bridge Specialty Brokers  ",
            ReferenceNumber: " BRG-001 ",
            Email: " ops@bridge.example ",
            Phone: " (555) 123-4567 ",
            AddressLine1: " 100 Market St ",
            AddressLine2: " Suite 400 ",
            City: " Dallas ",
            State: " TX ",
            ZipCode: " 75201 ",
            Country: " USA ",
            BankName: " First Bank ",
            BankAccountName: " Bridge Specialty Brokers Trust ",
            BankAccountLast4: "1234",
            BankRoutingNumber: "111000025",
            BankSwiftCode: "FBUS33",
            BankInstructions: " Monthly ACH ",
            IsActive: true,
            Notes: " London placement partner "));

        Assert.True(result.IsSuccess);
        Assert.Equal("Bridge Specialty Brokers", result.Value!.Name);
        Assert.Equal("BRG-001", result.Value.ReferenceNumber);
        Assert.Equal("ops@bridge.example", result.Value.Email);
        Assert.Equal("Dallas", result.Value.City);
        Assert.Equal("USA", result.Value.Country);
        Assert.Equal("First Bank", result.Value.BankName);
        Assert.Equal("1234", result.Value.BankAccountLast4);
        Assert.Empty(result.Value.BrokerageSetups);
    }

    [Fact]
    public async Task CreateBrokerageSetupAsync_RequiresPayeeWhenDirectPayableIsEnabled()
    {
        await using var db = CreateDb();
        var fixture = await SeedSetupFixtureAsync(db);
        var service = new IntermediaryService(db);

        var result = await service.CreateBrokerageSetupAsync(
            fixture.Intermediary.Id,
            new UpsertIntermediaryBrokerageSetupRequest(
                ProgramConfigurationId: fixture.Program.Id,
                CarrierId: fixture.Carrier.Id,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                EffectiveDate: new DateOnly(2026, 4, 1),
                ExpirationDate: null,
                BrokerageRate: 0.075m,
                CreatePayable: true,
                PayablePayeeId: null,
                IsActive: true,
                Notes: "Paid by SIMS"));

        Assert.False(result.IsSuccess);
        Assert.Equal("PAYABLE_PAYEE_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateBrokerageSetupAsync_StoresEffectiveDatedProgramCarrierLobSetup()
    {
        await using var db = CreateDb();
        var fixture = await SeedSetupFixtureAsync(db);
        var payee = new Payee { Id = 91, Name = "Bridge Specialty Brokers", PayeeType = "Broker", IsActive = true };
        db.Payees.Add(payee);
        await db.SaveChangesAsync();
        var service = new IntermediaryService(db);

        var result = await service.CreateBrokerageSetupAsync(
            fixture.Intermediary.Id,
            new UpsertIntermediaryBrokerageSetupRequest(
                ProgramConfigurationId: fixture.Program.Id,
                CarrierId: fixture.Carrier.Id,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                EffectiveDate: new DateOnly(2026, 4, 1),
                ExpirationDate: new DateOnly(2027, 3, 31),
                BrokerageRate: 0.075m,
                CreatePayable: true,
                PayablePayeeId: payee.Id,
                IsActive: true,
                Notes: "Paid by SIMS"));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.Program.Id, result.Value!.ProgramConfigurationId);
        Assert.Equal("Longleaf Inland Marine", result.Value.ProgramName);
        Assert.Equal(fixture.Carrier.Id, result.Value.CarrierId);
        Assert.Equal("Longleaf Casualty", result.Value.CarrierName);
        Assert.Equal(PolicyLineOfBusiness.InlandMarine, result.Value.LineOfBusiness);
        Assert.Equal("Inland Marine", result.Value.LineOfBusinessLabel);
        Assert.Null(result.Value.ProgramCarrierId);
        Assert.Equal(fixture.ProgramCarrierLineOfBusinessId, result.Value.ProgramCarrierLineOfBusinessId);
        Assert.Equal(0.075m, result.Value.BrokerageRate);
        Assert.True(result.Value.CreatePayable);
        Assert.Equal(payee.Id, result.Value.PayablePayeeId);
        Assert.Equal("Bridge Specialty Brokers", result.Value.PayablePayeeName);
    }

    [Fact]
    public async Task CreateBrokerageSetupAsync_StoresProgramCarrierForAllLinesSetup()
    {
        await using var db = CreateDb();
        var fixture = await SeedSetupFixtureAsync(db);
        var service = new IntermediaryService(db);

        var result = await service.CreateBrokerageSetupAsync(
            fixture.Intermediary.Id,
            new UpsertIntermediaryBrokerageSetupRequest(
                ProgramConfigurationId: fixture.Program.Id,
                CarrierId: fixture.Carrier.Id,
                LineOfBusiness: null,
                EffectiveDate: new DateOnly(2026, 4, 1),
                ExpirationDate: null,
                BrokerageRate: 0.075m,
                CreatePayable: false,
                PayablePayeeId: null,
                IsActive: true,
                Notes: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.ProgramCarrierId, result.Value!.ProgramCarrierId);
        Assert.Null(result.Value.ProgramCarrierLineOfBusinessId);
        Assert.Equal("All Lines", result.Value.LineOfBusinessLabel);
    }

    [Fact]
    public async Task CreateBrokerageSetupAsync_RejectsExpirationBeforeEffectiveDate()
    {
        await using var db = CreateDb();
        var fixture = await SeedSetupFixtureAsync(db);
        var service = new IntermediaryService(db);

        var result = await service.CreateBrokerageSetupAsync(
            fixture.Intermediary.Id,
            new UpsertIntermediaryBrokerageSetupRequest(
                ProgramConfigurationId: fixture.Program.Id,
                CarrierId: fixture.Carrier.Id,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                EffectiveDate: new DateOnly(2026, 4, 1),
                ExpirationDate: new DateOnly(2026, 3, 31),
                BrokerageRate: 0.075m,
                CreatePayable: false,
                PayablePayeeId: null,
                IsActive: true,
                Notes: null));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_DATE_RANGE", result.ErrorCode);
    }

    [Fact]
    public async Task CreateBrokerageSetupAsync_RejectsCarrierOutsideSelectedProgram()
    {
        await using var db = CreateDb();
        var fixture = await SeedSetupFixtureAsync(db);
        var outsideCarrier = new Carrier { Name = "Outside Casualty", IsActive = true };
        db.Carriers.Add(outsideCarrier);
        await db.SaveChangesAsync();
        var service = new IntermediaryService(db);

        var result = await service.CreateBrokerageSetupAsync(
            fixture.Intermediary.Id,
            new UpsertIntermediaryBrokerageSetupRequest(
                ProgramConfigurationId: fixture.Program.Id,
                CarrierId: outsideCarrier.Id,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                EffectiveDate: new DateOnly(2026, 4, 1),
                ExpirationDate: null,
                BrokerageRate: 0.075m,
                CreatePayable: false,
                PayablePayeeId: null,
                IsActive: true,
                Notes: null));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task CreateBrokerageSetupAsync_RejectsLobOutsideSelectedProgramCarrier()
    {
        await using var db = CreateDb();
        var fixture = await SeedSetupFixtureAsync(db, PolicyLineOfBusiness.GeneralLiability);
        var service = new IntermediaryService(db);

        var result = await service.CreateBrokerageSetupAsync(
            fixture.Intermediary.Id,
            new UpsertIntermediaryBrokerageSetupRequest(
                ProgramConfigurationId: fixture.Program.Id,
                CarrierId: fixture.Carrier.Id,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                EffectiveDate: new DateOnly(2026, 4, 1),
                ExpirationDate: null,
                BrokerageRate: 0.075m,
                CreatePayable: false,
                PayablePayeeId: null,
                IsActive: true,
                Notes: null));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task DeleteBrokerageSetupAsync_HidesDeletedSetupFromDetailsAndCounts()
    {
        await using var db = CreateDb();
        var fixture = await SeedSetupFixtureAsync(db);
        var service = new IntermediaryService(db);
        var setup = await service.CreateBrokerageSetupAsync(
            fixture.Intermediary.Id,
            new UpsertIntermediaryBrokerageSetupRequest(
                ProgramConfigurationId: fixture.Program.Id,
                CarrierId: fixture.Carrier.Id,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                EffectiveDate: new DateOnly(2026, 4, 1),
                ExpirationDate: null,
                BrokerageRate: 0.075m,
                CreatePayable: false,
                PayablePayeeId: null,
                IsActive: true,
                Notes: null));

        var delete = await service.DeleteBrokerageSetupAsync(fixture.Intermediary.Id, setup.Value!.Id);
        var detail = await service.GetByIdAsync(fixture.Intermediary.Id);
        var list = await service.GetAsync(includeInactive: true);
        var deletedRow = await db.Set<IntermediaryProgramCarrierLobSetup>()
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == setup.Value.Id);

        Assert.True(setup.IsSuccess);
        Assert.True(delete.IsSuccess);
        Assert.True(detail.IsSuccess);
        Assert.Empty(detail.Value!.BrokerageSetups);
        var item = Assert.Single(list);
        Assert.Equal(0, item.BrokerageSetupCount);
        Assert.Equal(0, item.ActiveBrokerageSetupCount);
        Assert.True(deletedRow.IsDeleted);
    }

    private static async Task<SetupFixture> SeedSetupFixtureAsync(ApplicationDbContext db, params PolicyLineOfBusiness[] configuredLines)
    {
        var intermediary = new Intermediary { Name = "Bridge Specialty Brokers", IsActive = true };
        var program = new ProgramConfiguration
        {
            Name = "Longleaf Inland Marine",
            Code = "LONGLEAF-IM",
            IsActive = true
        };
        var carrier = new Carrier { Name = "Longleaf Casualty", IsActive = true };
        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            ProgramConfiguration = program,
            Carrier = carrier,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1)
        };

        foreach (var line in configuredLines.Length == 0 ? [PolicyLineOfBusiness.InlandMarine] : configuredLines)
        {
            programCarrier.LinesOfBusiness.Add(new ProgramCarrierLineOfBusiness
            {
                LineOfBusiness = line,
                IsActive = true,
                EffectiveDate = new DateOnly(2026, 1, 1)
            });
        }

        db.AddRange(intermediary, program, carrier, programCarrier);
        await db.SaveChangesAsync();

        var programLob = programCarrier.LinesOfBusiness.FirstOrDefault(l => l.LineOfBusiness == PolicyLineOfBusiness.InlandMarine);

        return new SetupFixture(intermediary, program, carrier, programCarrier.Id, programLob?.Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed record SetupFixture(
        Intermediary Intermediary,
        ProgramConfiguration Program,
        Carrier Carrier,
        Guid ProgramCarrierId,
        Guid? ProgramCarrierLineOfBusinessId);
}
