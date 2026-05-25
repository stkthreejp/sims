using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.DTOs.PolicyForms;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class PolicyFormServiceTests
{
    [Fact]
    public async Task CreatePackageAsync_RejectsProgramSpecificPackageWhenCarrierLobIsNotConfiguredForProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        db.AddRange(
            program,
            carrier,
            new ProgramCarrier
            {
                ProgramConfigurationId = program.Id,
                CarrierId = carrier.Id,
                IsActive = true,
                EffectiveDate = new DateOnly(2026, 1, 1),
            });
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreatePackageAsync(new PolicyPackageConfigurationUpsertDto
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            State = "TX",
            Name = "Longleaf TX IM",
            IsActive = true,
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("Selected carrier, line of business, and state are not active for this program.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreatePackageAsync_AllowsProgramSpecificPackageWhenCarrierLobStateIsConfiguredForProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        db.AddRange(
            program,
            carrier,
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

        var result = await CreateService(db).CreatePackageAsync(new PolicyPackageConfigurationUpsertDto
        {
            ProgramConfigurationId = program.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            State = "TX",
            Name = "Longleaf TX IM",
            IsActive = true,
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(program.Id, result.Value!.ProgramConfigurationId);
        Assert.Equal(carrier.Id, result.Value.CarrierId);
        Assert.Equal("TX", result.Value.State);
    }

    private static PolicyFormService CreateService(ApplicationDbContext db)
    {
        var provider = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();
        var config = new ConfigurationBuilder().Build();
        return new PolicyFormService(provider, new NoOpBlobStorageService(), new CleanFileScanService(), config);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class NoOpBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string fileName, string contentType) => Task.FromResult(fileName);
        public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null) => Task.FromResult(blobPath);
        public Task<byte[]> DownloadAsync(string blobPath) => Task.FromResult(Array.Empty<byte>());
        public Task DeleteAsync(string blobPath) => Task.CompletedTask;
    }

    private sealed class CleanFileScanService : IFileScanService
    {
        public Task<FileScanResult> ScanAsync(IFormFile file, CancellationToken cancellationToken = default) => Task.FromResult(FileScanResult.Clean());
    }
}
