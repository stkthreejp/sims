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
