using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class CarrierRatingAssignmentProgramScopeTests
{
    [Fact]
    public async Task GetActiveAssignmentAsync_PrefersProgramSpecificAssignmentOverGenericAssignment()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var genericVersion = CreateVersion("Generic IM");
        var programVersion = CreateVersion("Longleaf IM");
        var programId = Guid.NewGuid();

        db.AddRange(
            carrier,
            genericVersion.RatingPlan,
            genericVersion,
            programVersion.RatingPlan,
            programVersion,
            new CarrierRatingAssignment
            {
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                RatingPlanVersionId = genericVersion.Id,
            },
            new CarrierRatingAssignment
            {
                ProgramConfigurationId = programId,
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                RatingPlanVersionId = programVersion.Id,
            });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetActiveAssignmentAsync(carrier.Id, PolicyLineOfBusiness.InlandMarine, programId);

        Assert.NotNull(result);
        Assert.Equal(programVersion.Id, result!.RatingPlanVersionId);
        Assert.Equal(programId, result.ProgramConfigurationId);
    }

    [Fact]
    public async Task GetActiveAssignmentAsync_FallsBackToGenericAssignmentWhenProgramSpecificAssignmentIsMissing()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var genericVersion = CreateVersion("Generic GL", PolicyLineOfBusiness.GeneralLiability);

        db.AddRange(
            carrier,
            genericVersion.RatingPlan,
            genericVersion,
            new CarrierRatingAssignment
            {
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
                RatingPlanVersionId = genericVersion.Id,
            });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetActiveAssignmentAsync(carrier.Id, PolicyLineOfBusiness.GeneralLiability, Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal(genericVersion.Id, result!.RatingPlanVersionId);
        Assert.Null(result.ProgramConfigurationId);
    }

    [Fact]
    public async Task CreateAsync_RejectsProgramSpecificAssignmentWhenCarrierLobIsNotConfiguredForProgram()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var version = CreateVersion("Longleaf GL", PolicyLineOfBusiness.GeneralLiability);

        db.AddRange(carrier, program, version.RatingPlan, version);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(new()
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            RatingPlanVersionId = version.Id,
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_AllowsProgramSpecificAssignmentWhenCarrierLobIsConfiguredForProgram()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var version = CreateVersion("Longleaf GL", PolicyLineOfBusiness.GeneralLiability);
        var programLob = new ProgramCarrierLineOfBusiness
        {
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1),
        };
        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 1, 1),
            LinesOfBusiness =
            {
                programLob,
            },
        };

        db.AddRange(carrier, program, version.RatingPlan, version, programCarrier);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(new()
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            RatingPlanVersionId = version.Id,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(program.Id, result.Value!.ProgramConfigurationId);
        Assert.Equal(programLob.Id, result.Value.ProgramCarrierLineOfBusinessId);
    }

    [Fact]
    public async Task CreateAsync_AllowsAssignmentWhenProgramPathStartsAfterVersionEffectiveDate()
    {
        // Real-world WS5 blocker: rate version effective 2026-01-01, program line
        // (binder) starts 2026-08-01. Ranges overlap, so the assignment must succeed.
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Lloyds of London - Dale", IsActive = true };
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var version = CreateVersion("Longleaf GL", PolicyLineOfBusiness.GeneralLiability);
        var programLob = new ProgramCarrierLineOfBusiness
        {
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 8, 1),
        };
        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2026, 8, 1),
            LinesOfBusiness = { programLob },
        };

        db.AddRange(carrier, program, version.RatingPlan, version, programCarrier);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(new()
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            RatingPlanVersionId = version.Id,
        });

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(programLob.Id, result.Value!.ProgramCarrierLineOfBusinessId);
    }

    [Fact]
    public async Task CreateAsync_RejectsAssignmentWhenProgramPathExpiredBeforeVersionEffectiveDate()
    {
        // Disjoint ranges: program line ended 2025-06-30, rates start 2026-01-01 —
        // the guard must still reject.
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Lloyds of London - Dale", IsActive = true };
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var version = CreateVersion("Longleaf GL", PolicyLineOfBusiness.GeneralLiability);
        var programLob = new ProgramCarrierLineOfBusiness
        {
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            IsActive = true,
            EffectiveDate = new DateOnly(2024, 1, 1),
            ExpirationDate = new DateOnly(2025, 6, 30),
        };
        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            IsActive = true,
            EffectiveDate = new DateOnly(2024, 1, 1),
            LinesOfBusiness = { programLob },
        };

        db.AddRange(carrier, program, version.RatingPlan, version, programCarrier);
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(new()
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.GeneralLiability,
            RatingPlanVersionId = version.Id,
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    private static RatingPlanVersion CreateVersion(string planName, PolicyLineOfBusiness lob = PolicyLineOfBusiness.InlandMarine)
    {
        var plan = new RatingPlan
        {
            Id = Guid.NewGuid(),
            LineOfBusiness = lob,
            Name = planName,
            FormulaKey = "IM_v1",
            Status = PlanStatus.Active,
        };

        return new RatingPlanVersion
        {
            Id = Guid.NewGuid(),
            RatingPlanId = plan.Id,
            RatingPlan = plan,
            VersionNumber = 1,
            EffectiveDate = new DateOnly(2026, 1, 1),
            Status = PlanStatus.Active,
        };
    }

    private static CarrierRatingAssignmentService CreateService(ApplicationDbContext db)
    {
        var provider = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();

        return new CarrierRatingAssignmentService(provider);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
