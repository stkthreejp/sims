using Microsoft.EntityFrameworkCore;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Bordereaux;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Infrastructure;

public class ApplicationDbContextModelTests
{
    public static TheoryData<Type, string[]> NullableFallbackUniqueIndexes => new()
    {
        {
            typeof(CarrierCommission),
            new[]
            {
                nameof(CarrierCommission.ProgramConfigurationId),
                nameof(CarrierCommission.CarrierId),
                nameof(CarrierCommission.LineOfBusiness),
                nameof(CarrierCommission.EffectiveDate),
            }
        },
        {
            typeof(AgentCommission),
            new[]
            {
                nameof(AgentCommission.ProgramConfigurationId),
                nameof(AgentCommission.CarrierId),
                nameof(AgentCommission.AgentId),
                nameof(AgentCommission.LineOfBusiness),
                nameof(AgentCommission.StateCode),
                nameof(AgentCommission.EffectiveDate),
            }
        },
        {
            typeof(BordereauxProfile),
            new[]
            {
                nameof(BordereauxProfile.ProgramConfigurationId),
                nameof(BordereauxProfile.CarrierId),
                nameof(BordereauxProfile.ReportType),
                nameof(BordereauxProfile.LineOfBusiness),
                nameof(BordereauxProfile.StateCode),
                nameof(BordereauxProfile.IsActive),
            }
        },
    };

    [Fact]
    public void BaseEntityTypes_WithSoftDeleteColumn_HaveQueryFilters()
    {
        using var db = CreateDb();

        var missingFilters = db.Model.GetEntityTypes()
            .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            .Where(entityType => entityType.FindProperty(nameof(BaseEntity.IsDeleted)) is not null)
            .Where(entityType => entityType.GetQueryFilter() is null)
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(missingFilters);
    }

    [Theory]
    [MemberData(nameof(NullableFallbackUniqueIndexes))]
    public void NullableFallbackUniqueIndexes_TreatNullScopeValuesAsNotDistinct(Type entityType, string[] propertyNames)
    {
        using var db = CreateDb();

        var modelEntityType = db.Model.FindEntityType(entityType);
        Assert.NotNull(modelEntityType);

        var index = modelEntityType.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.True(index.IsUnique);
        Assert.False(index.GetAreNullsDistinct());
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
