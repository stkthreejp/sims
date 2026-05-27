using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class AttachmentServiceAccessTests
{
    [Theory]
    [InlineData(DocumentEntityType.Carrier)]
    [InlineData(DocumentEntityType.Agent)]
    [InlineData(DocumentEntityType.Insured)]
    public async Task GetByEntityAsync_DeniesPartyAttachmentWhenUserHasNoScopedBusinessRecord(DocumentEntityType entityType)
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var entityId = await SeedPartyAsync(db, entityType, otherUserId);
        db.Add(CreateAttachment(entityType, entityId, otherUserId));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetByEntityAsync(entityType, entityId, currentUserId);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(DocumentEntityType.Carrier)]
    [InlineData(DocumentEntityType.Agent)]
    [InlineData(DocumentEntityType.Insured)]
    public async Task GetByEntityAsync_AllowsPartyAttachmentWhenUserHasScopedBusinessRecord(DocumentEntityType entityType)
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var entityId = await SeedScopedPartyAsync(db, entityType, currentUserId, otherUserId);
        db.Add(CreateAttachment(entityType, entityId, otherUserId));
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetByEntityAsync(entityType, entityId, currentUserId);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_DeniesPartyAttachmentWhenUserHasNoScopedBusinessRecord()
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var carrierId = await SeedPartyAsync(db, DocumentEntityType.Carrier, otherUserId);
        var attachment = CreateAttachment(DocumentEntityType.Carrier, carrierId, otherUserId);
        db.Add(attachment);
        await db.SaveChangesAsync();

        var blob = new RecordingBlobStorage();
        var result = await CreateService(db, blob).GetDownloadUrlAsync(attachment.Id, currentUserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ATTACHMENT_ACCESS_DENIED", result.ErrorCode);
        Assert.False(blob.DownloadUrlRequested);
    }

    [Fact]
    public async Task GetByEntityAsync_AllowsElevatedUserToAccessExistingPartyAttachment()
    {
        await using var db = CreateDb();
        var underwriterId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var carrierId = await SeedPartyAsync(db, DocumentEntityType.Carrier, otherUserId);
        db.Add(CreateAttachment(DocumentEntityType.Carrier, carrierId, otherUserId));
        await SeedRoleAsync(db, underwriterId, "Underwriter");
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetByEntityAsync(DocumentEntityType.Carrier, carrierId, underwriterId);

        Assert.Single(result);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static AttachmentService CreateService(ApplicationDbContext db, RecordingBlobStorage? blob = null)
        => new(
            db,
            blob ?? new RecordingBlobStorage(),
            new CleanFileScanService(),
            new ConfigurationBuilder().Build());

    private static async Task<Guid> SeedPartyAsync(ApplicationDbContext db, DocumentEntityType entityType, Guid userId)
    {
        await SeedUserAsync(db, userId);
        var entityId = Guid.NewGuid();

        switch (entityType)
        {
            case DocumentEntityType.Carrier:
                db.Add(new Carrier { Id = entityId, Name = "Outside Carrier" });
                break;
            case DocumentEntityType.Agent:
                db.Add(new Agent { Id = entityId, Name = "Outside Agent" });
                break;
            case DocumentEntityType.Insured:
                db.Add(new Insured
                {
                    Id = entityId,
                    InsuredType = InsuredType.Commercial,
                    CompanyName = "Outside Insured",
                    State = "NC",
                    CreatedById = userId,
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entityType));
        }

        await db.SaveChangesAsync();
        return entityId;
    }

    private static async Task<Guid> SeedScopedPartyAsync(
        ApplicationDbContext db,
        DocumentEntityType entityType,
        Guid currentUserId,
        Guid otherUserId)
    {
        await SeedUserAsync(db, currentUserId);
        await SeedUserAsync(db, otherUserId);

        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Scoped Carrier" };
        var agent = new Agent { Id = Guid.NewGuid(), Name = "Scoped Agent" };
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            InsuredType = InsuredType.Commercial,
            CompanyName = "Scoped Insured",
            State = "NC",
            CreatedById = otherUserId,
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = "SUB-SCOPED",
            InsuredId = insured.Id,
            Insured = insured,
            AgentId = agent.Id,
            Agent = agent,
            UnderwriterId = currentUserId,
            CreatedById = otherUserId,
        };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "Q-SCOPED",
            SubmissionId = submission.Id,
            Submission = submission,
            CarrierId = carrier.Id,
            Carrier = carrier,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            CreatedById = otherUserId,
        };

        db.AddRange(carrier, agent, insured, submission, quote);
        await db.SaveChangesAsync();

        return entityType switch
        {
            DocumentEntityType.Carrier => carrier.Id,
            DocumentEntityType.Agent => agent.Id,
            DocumentEntityType.Insured => insured.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(entityType)),
        };
    }

    private static async Task SeedRoleAsync(ApplicationDbContext db, Guid userId, string roleName)
    {
        await SeedUserAsync(db, userId);
        var role = new Role { Id = Guid.NewGuid(), Name = roleName, NormalizedName = roleName.ToUpperInvariant() };
        db.Add(role);
        db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = role.Id });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(ApplicationDbContext db, Guid userId)
    {
        if (await db.Set<User>().AnyAsync(u => u.Id == userId))
            return;

        db.Add(new User
        {
            Id = userId,
            UserName = $"{userId:N}@example.test",
            Email = $"{userId:N}@example.test",
            FirstName = "Test",
            LastName = "User",
        });
    }

    private static Attachment CreateAttachment(DocumentEntityType entityType, Guid entityId, Guid uploadedById)
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            DocumentType = DocumentType.Other,
            FileName = "evidence.pdf",
            BlobPath = $"attachments/{Guid.NewGuid():N}.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            UploadedById = uploadedById,
        };

        switch (entityType)
        {
            case DocumentEntityType.Carrier:
                attachment.CarrierId = entityId;
                break;
            case DocumentEntityType.Agent:
                attachment.AgentId = entityId;
                break;
            case DocumentEntityType.Insured:
                attachment.InsuredId = entityId;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entityType));
        }

        return attachment;
    }

    private sealed class RecordingBlobStorage : IBlobStorageService
    {
        public bool DownloadUrlRequested { get; private set; }

        public Task<string> UploadAsync(Stream content, string fileName, string contentType)
            => Task.FromResult("blob-path");

        public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null)
        {
            DownloadUrlRequested = true;
            return Task.FromResult($"https://blob.example/{blobPath}");
        }

        public Task<byte[]> DownloadAsync(string blobPath)
            => Task.FromResult(Array.Empty<byte>());

        public Task DeleteAsync(string blobPath)
            => Task.CompletedTask;
    }

    private sealed class CleanFileScanService : IFileScanService
    {
        public Task<FileScanResult> ScanAsync(IFormFile file, CancellationToken cancellationToken = default)
            => Task.FromResult(FileScanResult.Clean());
    }
}
