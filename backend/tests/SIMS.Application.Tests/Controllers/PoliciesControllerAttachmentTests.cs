using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIMS.API.Controllers;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Notes;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Enums;
using Xunit;

namespace SIMS.Application.Tests.Controllers;

public class PoliciesControllerAttachmentTests
{
    [Fact]
    public async Task GetAttachments_UsesBoundQuoteIdFromPolicy()
    {
        var policyId = Guid.NewGuid();
        var boundQuoteId = Guid.NewGuid();
        var attachments = new RecordingAttachmentService();
        var controller = CreateController(policyId, boundQuoteId, attachments);

        await controller.GetAttachments(policyId);

        Assert.Equal(DocumentEntityType.Policy, attachments.LastEntityType);
        Assert.Equal(boundQuoteId, attachments.LastEntityId);
    }

    [Fact]
    public async Task DownloadAttachment_ReturnsNotFoundWhenAttachmentIsNotOnPolicy()
    {
        var policyId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var attachments = new RecordingAttachmentService();
        var controller = CreateController(policyId, Guid.NewGuid(), attachments);

        var result = await controller.DownloadAttachment(policyId, attachmentId);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.False(attachments.DownloadUrlRequested);
    }

    [Fact]
    public async Task DeleteAttachment_ReturnsNotFoundWhenAttachmentIsNotOnPolicy()
    {
        var policyId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var attachments = new RecordingAttachmentService();
        var controller = CreateController(policyId, Guid.NewGuid(), attachments);

        var result = await controller.DeleteAttachment(policyId, attachmentId);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.False(attachments.DeleteRequested);
    }

    private static PoliciesController CreateController(
        Guid policyId,
        Guid boundQuoteId,
        RecordingAttachmentService attachments)
    {
        var userId = Guid.NewGuid();
        var policies = new StubPolicyService(policyId, boundQuoteId);
        var controller = new PoliciesController(policies, new ThrowingNoteService(), attachments)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                    ], "test"))
                }
            }
        };

        return controller;
    }

    private sealed class RecordingAttachmentService : IAttachmentService
    {
        public DocumentEntityType? LastEntityType { get; private set; }
        public Guid? LastEntityId { get; private set; }
        public bool DownloadUrlRequested { get; private set; }
        public bool DeleteRequested { get; private set; }
        public List<AttachmentDto> Attachments { get; } = [];

        public Task<IEnumerable<AttachmentDto>> GetByEntityAsync(DocumentEntityType entityType, Guid entityId, Guid userId)
        {
            LastEntityType = entityType;
            LastEntityId = entityId;
            return Task.FromResult<IEnumerable<AttachmentDto>>(Attachments);
        }

        public Task<Result<AttachmentDto>> UploadAsync(DocumentEntityType entityType, Guid entityId, IFormFile file, DocumentType documentType, string? description, Guid userId, Guid? policyTransactionId = null)
        {
            LastEntityType = entityType;
            LastEntityId = entityId;
            return Task.FromResult(Result<AttachmentDto>.Success(new AttachmentDto { Id = Guid.NewGuid() }));
        }

        public Task<Result<AttachmentDto>> CreateGeneratedAsync(DocumentEntityType entityType, Guid entityId, Stream content, string fileName, string contentType, long fileSizeBytes, DocumentType documentType, string? description, Guid userId, Guid? policyVersionId = null, Guid? policyTransactionId = null)
            => throw new NotImplementedException();

        public Task<Result<string>> GetDownloadUrlAsync(Guid id, Guid userId)
        {
            DownloadUrlRequested = true;
            return Task.FromResult(Result<string>.Success("https://blob.example/download"));
        }

        public Task<Result> DeleteAsync(Guid id, Guid userId)
        {
            DeleteRequested = true;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class StubPolicyService : IPolicyService
    {
        private readonly Guid _policyId;
        private readonly Guid _boundQuoteId;

        public StubPolicyService(Guid policyId, Guid boundQuoteId)
        {
            _policyId = policyId;
            _boundQuoteId = boundQuoteId;
        }

        public Task<Result<PolicyDto>> GetByIdAsync(Guid id, UserAccessScope access)
            => Task.FromResult(id == _policyId
                ? Result<PolicyDto>.Success(new PolicyDto { Id = id, BoundQuoteId = _boundQuoteId })
                : Result<PolicyDto>.Failure("NOT_FOUND", "Policy not found."));

        public Task<PagedResult<PolicyListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access) => throw new NotImplementedException();
        public Task<IEnumerable<PolicyListItemDto>> GetByInsuredAsync(Guid insuredId, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyTransactionArtifactsDto>> GetTransactionArtifactsAsync(Guid policyId, Guid transactionId, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyIssuancePacketDto>> GetIssuancePacketAsync(Guid policyId, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<GeneratedDocumentDto>> GenerateIssuancePacketPreviewAsync(Guid policyId, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyDto>> IssueAsync(Guid policyId, IssuePolicyDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<VoidTestBindResultDto>> VoidTestBindAsync(Guid policyId, VoidTestBindDto dto, UserAccessScope access, bool isAdmin) => throw new NotImplementedException();
        public Task<Result<PolicyTransactionDto>> AddEndorsementAsync(Guid policyId, CreateEndorsementDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyTransactionDto>> IssueEndorsementAsync(Guid policyId, Guid txnId, IssueEndorsementDto dto, UserAccessScope access, IReadOnlyCollection<string>? currentUserPermissions = null) => throw new NotImplementedException();
        public Task<Result<QuoteDto>> CreateRenewalQuoteAsync(Guid policyId, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyDto>> CancelAsync(Guid policyId, CancelPolicyDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyTransactionDto>> IssueCancellationNoticeAsync(Guid policyId, IssueCancellationNoticeDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyDto>> CompleteCancellationAsync(Guid policyId, Guid transactionId, CompleteCancellationDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyDto>> ReinstateAsync(Guid policyId, ReinstatePolicyDto dto, UserAccessScope access, IReadOnlyCollection<string>? currentUserPermissions = null) => throw new NotImplementedException();
        public Task<Result<PolicyTransactionDto>> StartRewriteAsync(Guid policyId, StartRewritePolicyDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<PolicyDto>> CompleteRewriteAsync(Guid policyId, Guid transactionId, CompleteRewritePolicyDto dto, UserAccessScope access, IReadOnlyCollection<string>? currentUserPermissions = null) => throw new NotImplementedException();
        public Task<Result<PolicyDto>> NonRenewAsync(Guid policyId, NonRenewPolicyDto dto, UserAccessScope access, IReadOnlyCollection<string>? currentUserPermissions = null) => throw new NotImplementedException();
        public Task<Result<PolicyTransactionDto>> MarkForNonRenewalAsync(Guid policyId, MarkNonRenewalDto dto, UserAccessScope access, IReadOnlyCollection<string>? currentUserPermissions = null) => throw new NotImplementedException();
        public Task<Result<PolicyDto>> CompleteNonRenewalAsync(Guid policyId, Guid transactionId, CompleteNonRenewalDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<LegalComplianceGuidanceDto>> GetCancellationGuidanceAsync(Guid policyId, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<LegalComplianceGuidanceDto>> GetNonRenewalGuidanceAsync(Guid policyId, UserAccessScope access) => throw new NotImplementedException();
    }

    private sealed class ThrowingNoteService : INoteService
    {
        public Task<IEnumerable<NoteDto>> GetByQuoteAsync(Guid quoteId, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<NoteDto>> GetByIdAsync(Guid quoteId, Guid id, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<NoteDto>> CreateAsync(Guid quoteId, NoteCreateDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<NoteDto>> UpdateAsync(Guid quoteId, Guid id, NoteUpdateDto dto, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result> DeleteAsync(Guid quoteId, Guid id, UserAccessScope access) => throw new NotImplementedException();
        public Task<Result<NoteDto>> TogglePinAsync(Guid quoteId, Guid id, UserAccessScope access) => throw new NotImplementedException();
    }
}
