using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.OutboundCommunications;
using SIMS.Application.DTOs.ProposalDocuments;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using SIMS.Infrastructure.Services;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class ProposalGenerationServiceSelectionTests
{
    [Fact]
    public async Task GenerateInlandMarineHtmlAsync_UsesConfiguredProposalAndAppendsStateNotice()
    {
        await using var db = CreateDb();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Uma",
            LastName = "Underwriter",
            Email = "uma@example.com",
            UserName = "uma@example.com",
        };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            InsuredType = InsuredType.Commercial,
            CompanyName = "Longleaf Logging",
            AddressLine1 = "1 Pine Rd",
            City = "Raleigh",
            State = "NC",
            ZipCode = "27601",
            CreatedById = user.Id,
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = "SUB-001",
            InsuredId = insured.Id,
            Insured = insured,
            UnderwriterId = user.Id,
            Underwriter = user,
            CreatedById = user.Id,
        };
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
            CarrierId = carrier.Id,
            Carrier = carrier,
            SubmissionId = submission.Id,
            Submission = submission,
            QuoteNumber = "Q-001",
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 6, 1),
            ExpirationDate = new DateOnly(2027, 6, 1),
            PremiumAmount = 1200m,
            TotalPremium = 1300m,
            CreatedById = user.Id,
        };
        var proposalTemplate = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Falls Lake NC Proposal",
            EntityType = TemplateEntityType.Quote,
            Kind = DocumentTemplateKind.Document,
            HtmlContent = "<main>Configured proposal {{Quote.QuoteNumber}} for {{Insured.DisplayName}}</main>",
            CreatedById = user.Id,
            IsActive = true,
        };
        var noticeTemplate = new DocumentTemplate
        {
            Id = Guid.NewGuid(),
            Name = "NC Notice",
            EntityType = TemplateEntityType.Quote,
            Kind = DocumentTemplateKind.Document,
            HtmlContent = "<section>North Carolina notice for {{Insured.DisplayName}}</section>",
            CreatedById = user.Id,
            IsActive = true,
        };
        var formTemplate = new PolicyFormTemplate
        {
            Id = Guid.NewGuid(),
            FormNumber = "LL IM 001",
            Name = "Inland Marine Form",
            IsActive = true,
        };
        db.AddRange(
            user,
            carrier,
            insured,
            submission,
            quote,
            proposalTemplate,
            noticeTemplate,
            formTemplate,
            new QuotePolicyFormSelection
            {
                Id = Guid.NewGuid(),
                QuoteId = quote.Id,
                Quote = quote,
                PolicyFormTemplateId = formTemplate.Id,
                PolicyFormTemplate = formTemplate,
                IsIncluded = true,
                SequenceOrder = 1,
            });
        await db.SaveChangesAsync();

        var configurationService = new RecordingProposalDocumentConfigurationService(new ProposalDocumentSelectionDto(
            quote.Id,
            "NC",
            new ProposalDocumentSelectionItemDto(Guid.NewGuid(), proposalTemplate.Id, proposalTemplate.Name, ProposalDocumentRole.Proposal, null, 0),
            [
                new ProposalDocumentSelectionItemDto(Guid.NewGuid(), noticeTemplate.Id, noticeTemplate.Name, ProposalDocumentRole.StateNotice, "NC", 1),
            ]));
        var service = CreateService(db, configurationService);

        var result = await service.GenerateInlandMarineHtmlAsync(quote.Id);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(quote.Id, configurationService.ResolvedQuoteId);
        Assert.Contains("Configured proposal Q-001 for Longleaf Logging", result.Value);
        Assert.Contains("North Carolina notice for Longleaf Logging", result.Value);
    }

    private static ProposalGenerationService CreateService(
        ApplicationDbContext db,
        IProposalDocumentConfigurationService configurationService)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new ProposalGenerationService(
            db,
            new ThrowingAttachmentService(),
            new ThrowingHtmlToPdfService(),
            new ThrowingOutboundCommunicationService(),
            configurationService,
            new DocumentMergeService(),
            config);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class RecordingProposalDocumentConfigurationService(ProposalDocumentSelectionDto selection)
        : IProposalDocumentConfigurationService
    {
        public Guid? ResolvedQuoteId { get; private set; }

        public Task<IReadOnlyList<ProposalDocumentConfigurationDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ProposalDocumentConfigurationDto>> CreateAsync(UpsertProposalDocumentConfigurationRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ProposalDocumentConfigurationDto>> UpdateAsync(Guid id, UpsertProposalDocumentConfigurationRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<ProposalDocumentSelectionDto>> ResolveForQuoteAsync(Guid quoteId, CancellationToken ct = default)
        {
            ResolvedQuoteId = quoteId;
            return Task.FromResult(Result<ProposalDocumentSelectionDto>.Success(selection));
        }
    }

    private sealed class ThrowingAttachmentService : IAttachmentService
    {
        public Task<IEnumerable<AttachmentDto>> GetByEntityAsync(DocumentEntityType entityType, Guid entityId, Guid userId)
            => throw new NotSupportedException();

        public Task<Result<AttachmentDto>> UploadAsync(DocumentEntityType entityType, Guid entityId, IFormFile file, DocumentType documentType, string? description, Guid userId, Guid? policyTransactionId = null)
            => throw new NotSupportedException();

        public Task<Result<AttachmentDto>> CreateGeneratedAsync(DocumentEntityType entityType, Guid entityId, Stream content, string fileName, string contentType, long fileSizeBytes, DocumentType documentType, string? description, Guid userId, Guid? policyVersionId = null, Guid? policyTransactionId = null)
            => throw new NotSupportedException();

        public Task<Result<string>> GetDownloadUrlAsync(Guid id, Guid userId)
            => throw new NotSupportedException();

        public Task<Result> DeleteAsync(Guid id, Guid userId)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingHtmlToPdfService : IHtmlToPdfService
    {
        public Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingOutboundCommunicationService : IOutboundCommunicationService
    {
        public Task<IEnumerable<OutboundCommunicationListItemDto>> GetForEntityAsync(OutboundCommunicationEntityType entityType, Guid entityId, Guid? policyTransactionId = null)
            => throw new NotSupportedException();

        public Task<Result<OutboundCommunicationDto>> GetByIdAsync(Guid id)
            => throw new NotSupportedException();

        public Task<Result<OutboundCommunicationDto>> CreateDraftAsync(OutboundCommunicationCreateDto dto, Guid createdById)
            => throw new NotSupportedException();

        public Task<Result<OutboundCommunicationDto>> UpdateDraftAsync(Guid id, OutboundCommunicationUpdateDto dto)
            => throw new NotSupportedException();

        public Task<Result<OutboundCommunicationDto>> UpdateStatusAsync(Guid id, OutboundCommunicationStatusUpdateDto dto, Guid userId)
            => throw new NotSupportedException();

        public Task<Result<OutboundCommunicationDto>> SendAsync(Guid id, Guid userId)
            => throw new NotSupportedException();
    }
}
