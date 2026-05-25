using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.PolicyNumbers;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class PolicyNumberAdminServiceTests
{
    [Fact]
    public async Task CreateAssignmentAsync_RejectsProgramSpecificAssignmentWhenCarrierLobIsNotConfiguredForProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var sequence = CreateSequence();

        db.AddRange(
            program,
            carrier,
            sequence,
            new ProgramCarrier
            {
                ProgramConfigurationId = program.Id,
                CarrierId = carrier.Id,
                IsActive = true,
                EffectiveDate = new DateOnly(2026, 1, 1),
            });
        await db.SaveChangesAsync();

        var result = await new PolicyNumberAdminService(db).CreateAssignmentAsync(new PolicyNumberAssignmentUpsertDto
        {
            PolicyNumberSequenceId = sequence.Id,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            IsActive = true,
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAssignmentAsync_AllowsProgramSpecificAssignmentWhenCarrierLobStateIsConfiguredForProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var sequence = CreateSequence();

        db.AddRange(
            program,
            carrier,
            sequence,
            new ProgramCarrier
            {
                ProgramConfigurationId = program.Id,
                CarrierId = carrier.Id,
                IsActive = true,
                EffectiveDate = new DateOnly(2026, 1, 1),
                LinesOfBusiness =
                {
                    new ProgramCarrierLineOfBusiness
                    {
                        LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                        IsActive = true,
                        EffectiveDate = new DateOnly(2026, 1, 1),
                        States =
                        {
                            new ProgramCarrierLobState
                            {
                                StateCode = "TX",
                                IsActive = true,
                                EffectiveDate = new DateOnly(2026, 1, 1),
                            },
                        },
                    },
                },
            });
        await db.SaveChangesAsync();

        var result = await new PolicyNumberAdminService(db).CreateAssignmentAsync(new PolicyNumberAssignmentUpsertDto
        {
            PolicyNumberSequenceId = sequence.Id,
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            State = "TX",
            IsActive = true,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(program.Id, result.Value!.ProgramConfigurationId);
        Assert.Equal(carrier.Id, result.Value.CarrierId);
        Assert.Equal(PolicyLineOfBusiness.InlandMarine, result.Value.LineOfBusiness);
        Assert.Equal("TX", result.Value.State);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static PolicyNumberSequence CreateSequence() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Longleaf IM",
        Format = "{CARRIER}-{LOB}-{SEQ:000}",
        TermSuffixFormat = "-{TERM:00}",
        NextNumber = 1,
        IsActive = true,
    };
}
