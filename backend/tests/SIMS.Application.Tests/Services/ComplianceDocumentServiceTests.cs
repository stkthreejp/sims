using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using SIMS.Application.DTOs.Compliance;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class ComplianceDocumentServiceTests
{
    [Fact]
    public async Task PublishDraftAsync_RequiresSubmittedReviewBeforePublishing()
    {
        await using var db = CreateDb();
        var user = CreateUser("Pat", "Reviewer");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var document = await CreateDocumentAsync(service, user.Id, "Business Continuity Plan", "<p>Initial plan</p>");

        var blocked = await service.PublishDraftAsync(document.Id, new CompliancePublishDto(), user.Id);

        Assert.False(blocked.IsSuccess);
        Assert.Equal("VALIDATION", blocked.ErrorCode);

        var submitted = await service.SubmitForReviewAsync(document.Id, new ComplianceWorkflowActionDto { Notes = "Ready for approval" }, user.Id);
        Assert.True(submitted.IsSuccess);

        var published = await service.PublishDraftAsync(document.Id, new CompliancePublishDto { Notes = "Approved" }, user.Id);

        Assert.True(published.IsSuccess);
        Assert.Equal("Active", published.Value!.Status);
        Assert.NotNull(published.Value.CurrentPublishedVersion);
        Assert.Null(published.Value.CurrentDraftVersion);
        Assert.Equal("Published", published.Value.CurrentPublishedVersion!.Status);
        Assert.Contains(published.Value.Reviews, review => review.Status == "Approved" && review.Notes == "Approved");

        var actions = await db.ComplianceAuditLogs.Select(log => log.Action).ToListAsync();
        Assert.Contains("SubmittedForReview", actions);
        Assert.Contains("Published", actions);
    }

    [Fact]
    public async Task CompareVersionsAsync_ShowsAddedAndRemovedTextBetweenPublishedAndDraftVersions()
    {
        await using var db = CreateDb();
        var user = CreateUser("Casey", "Owner");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var document = await CreateDocumentAsync(service, user.Id, "IT Data Security Policy", "<p>Encrypt laptops annually.</p>");
        await service.SubmitForReviewAsync(document.Id, new ComplianceWorkflowActionDto(), user.Id);
        await service.PublishDraftAsync(document.Id, new CompliancePublishDto(), user.Id);

        var draft = await service.SaveDraftAsync(
            document.Id,
            new ComplianceDraftSaveDto
            {
                HtmlContent = "<p>Encrypt laptops quarterly and review access.</p>",
                ChangeSummary = "Tighten cadence"
            },
            user.Id);

        Assert.True(draft.IsSuccess);

        var compare = await service.CompareVersionsAsync(document.Id);

        Assert.True(compare.IsSuccess);
        Assert.Contains(compare.Value!.Parts, part => part.Kind == "Removed" && part.Text.Contains("annually"));
        Assert.Contains(compare.Value.Parts, part => part.Kind == "Added" && part.Text.Contains("quarterly"));
        Assert.Contains(compare.Value.Parts, part => part.Kind == "Added" && part.Text.Contains("access"));
    }

    [Fact]
    public async Task CreateAttestationCampaignAsync_RequiresPublishedVersion()
    {
        await using var db = CreateDb();
        var owner = CreateUser("Morgan", "Owner");
        var recipient = CreateUser("Jordan", "Staff");
        db.Users.AddRange(owner, recipient);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var document = await CreateDocumentAsync(service, owner.Id, "Incident Response Plan", "<p>Draft response plan</p>");

        var result = await service.CreateAttestationCampaignAsync(
            document.Id,
            new ComplianceAttestationCampaignCreateDto
            {
                VersionId = document.CurrentDraftVersion!.Id,
                Name = "Draft attestation",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
                UserIds = [recipient.Id]
            },
            owner.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION", result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAttestationAsync_RecordsRecipientStatusAndAuditLog()
    {
        await using var db = CreateDb();
        var owner = CreateUser("Alex", "Owner");
        var recipient = CreateUser("Taylor", "Staff", "taylor@example.com");
        db.Users.AddRange(owner, recipient);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var document = await CreateDocumentAsync(service, owner.Id, "Acceptable Use Policy", "<p>Use approved systems.</p>");
        await service.SubmitForReviewAsync(document.Id, new ComplianceWorkflowActionDto(), owner.Id);
        var published = await service.PublishDraftAsync(document.Id, new CompliancePublishDto(), owner.Id);
        var campaign = await service.CreateAttestationCampaignAsync(
            document.Id,
            new ComplianceAttestationCampaignCreateDto
            {
                VersionId = published.Value!.CurrentPublishedVersion!.Id,
                Name = "Annual acknowledgement",
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                UserIds = [recipient.Id]
            },
            owner.Id);

        var result = await service.SubmitAttestationAsync(
            campaign.Value!.Id,
            new ComplianceAttestationSubmitDto { Status = "Attested", Comment = "Reviewed" },
            recipient.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Attested", result.Value!.Status);
        Assert.Equal("Reviewed", result.Value.Comment);
        Assert.NotNull(result.Value.AttestedAt);

        var audit = await db.ComplianceAuditLogs.SingleAsync(log => log.Action == "AttestationSubmitted");
        Assert.Equal("Pending", audit.OldValue);
        Assert.Equal("Attested", audit.NewValue);
        Assert.Equal("Reviewed", audit.Comment);
    }

    private static async Task<ComplianceDocumentDetailDto> CreateDocumentAsync(
        ComplianceDocumentService service,
        Guid userId,
        string title,
        string htmlContent)
    {
        var result = await service.CreateDocumentAsync(
            new ComplianceDocumentCreateDto
            {
                Title = title,
                Category = "IT",
                DocumentType = "Policy",
                ReviewCadence = "Annual",
                HtmlContent = htmlContent
            },
            userId);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static ComplianceDocumentService CreateService(ComplianceTestDbContext db)
        => new(
            db,
            new FakeBlobStorageService(),
            new NoOpFileScanService(),
            new FakeHtmlToPdfService(),
            new ConfigurationBuilder().Build());

    private static ComplianceTestDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ComplianceTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ComplianceTestDbContext(options);
    }

    private static User CreateUser(string firstName, string lastName, string? email = null) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        UserName = email ?? $"{firstName}.{lastName}@example.com",
        Email = email ?? $"{firstName}.{lastName}@example.com",
    };

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string fileName, string contentType) =>
            Task.FromResult($"test/{fileName}");

        public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null) =>
            Task.FromResult($"https://example.test/{blobPath}");

        public Task<byte[]> DownloadAsync(string blobPath) =>
            Task.FromResult(Array.Empty<byte>());

        public Task DeleteAsync(string blobPath) =>
            Task.CompletedTask;
    }

    private sealed class FakeHtmlToPdfService : IHtmlToPdfService
    {
        public Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default) =>
            Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    private sealed class ComplianceTestDbContext : DbContext
    {
        public ComplianceTestDbContext(DbContextOptions<ComplianceTestDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();
        public DbSet<ComplianceDocumentVersion> ComplianceDocumentVersions => Set<ComplianceDocumentVersion>();
        public DbSet<ComplianceDocumentReview> ComplianceDocumentReviews => Set<ComplianceDocumentReview>();
        public DbSet<ComplianceEvidence> ComplianceEvidenceItems => Set<ComplianceEvidence>();
        public DbSet<ComplianceEvidenceAttachment> ComplianceEvidenceAttachments => Set<ComplianceEvidenceAttachment>();
        public DbSet<ComplianceAttestationCampaign> ComplianceAttestationCampaigns => Set<ComplianceAttestationCampaign>();
        public DbSet<ComplianceAttestationRecipient> ComplianceAttestationRecipients => Set<ComplianceAttestationRecipient>();
        public DbSet<ComplianceAuditLog> ComplianceAuditLogs => Set<ComplianceAuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().Ignore(u => u.RefreshTokens);

            modelBuilder.Entity<ComplianceDocument>()
                .HasOne(d => d.CurrentPublishedVersion)
                .WithMany()
                .HasForeignKey(d => d.CurrentPublishedVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceDocument>()
                .HasOne(d => d.CurrentDraftVersion)
                .WithMany()
                .HasForeignKey(d => d.CurrentDraftVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceDocument>()
                .HasOne(d => d.Owner)
                .WithMany()
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceDocument>()
                .HasOne(d => d.Approver)
                .WithMany()
                .HasForeignKey(d => d.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceDocumentVersion>()
                .HasOne(v => v.Document)
                .WithMany(d => d.Versions)
                .HasForeignKey(v => v.DocumentId);

            modelBuilder.Entity<ComplianceDocumentVersion>()
                .HasOne(v => v.CreatedBy)
                .WithMany()
                .HasForeignKey(v => v.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceDocumentVersion>()
                .HasOne(v => v.ApprovedBy)
                .WithMany()
                .HasForeignKey(v => v.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceDocumentReview>()
                .HasOne(r => r.Document)
                .WithMany(d => d.Reviews)
                .HasForeignKey(r => r.DocumentId);

            modelBuilder.Entity<ComplianceDocumentReview>()
                .HasOne(r => r.Version)
                .WithMany()
                .HasForeignKey(r => r.VersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceDocumentReview>()
                .HasOne(r => r.ReviewedBy)
                .WithMany()
                .HasForeignKey(r => r.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceEvidence>()
                .HasOne(e => e.Document)
                .WithMany(d => d.EvidenceItems)
                .HasForeignKey(e => e.DocumentId);

            modelBuilder.Entity<ComplianceEvidence>()
                .HasOne(e => e.Review)
                .WithMany()
                .HasForeignKey(e => e.ReviewId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceEvidence>()
                .HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceEvidenceAttachment>()
                .HasOne(a => a.Evidence)
                .WithMany(e => e.Attachments)
                .HasForeignKey(a => a.EvidenceId);

            modelBuilder.Entity<ComplianceEvidenceAttachment>()
                .HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceAttestationCampaign>()
                .HasOne(c => c.Document)
                .WithMany()
                .HasForeignKey(c => c.DocumentId);

            modelBuilder.Entity<ComplianceAttestationCampaign>()
                .HasOne(c => c.Version)
                .WithMany()
                .HasForeignKey(c => c.VersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceAttestationCampaign>()
                .HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceAttestationRecipient>()
                .HasOne(r => r.Campaign)
                .WithMany(c => c.Recipients)
                .HasForeignKey(r => r.CampaignId);

            modelBuilder.Entity<ComplianceAttestationRecipient>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceAuditLog>()
                .HasOne(l => l.Document)
                .WithMany()
                .HasForeignKey(l => l.DocumentId);

            modelBuilder.Entity<ComplianceAuditLog>()
                .HasOne(l => l.Version)
                .WithMany()
                .HasForeignKey(l => l.VersionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComplianceAuditLog>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
