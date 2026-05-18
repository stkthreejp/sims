using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Policies;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;
using System.Text.Json;

namespace SIMS.Application.Services;

public class PolicyService : IPolicyService
{
    private readonly IServiceProvider _sp;
    private readonly IInvoicingService _invoicing;
    private readonly IVoidService _voids;
    private readonly IPolicyTransactionLifecycleService _transactionLifecycle;
    private readonly IPolicyVersionService _policyVersions;

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public PolicyService(
        IServiceProvider sp,
        IInvoicingService invoicing,
        IVoidService voids,
        IPolicyTransactionLifecycleService transactionLifecycle,
        IPolicyVersionService policyVersions)
    {
        _sp = sp;
        _invoicing = invoicing;
        _voids = voids;
        _transactionLifecycle = transactionLifecycle;
        _policyVersions = policyVersions;
    }

    public async Task<PagedResult<PolicyListItemDto>> GetAllAsync(QueryParameters query, UserAccessScope access)
    {
        var q = Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Where(p => !p.IsDeleted)
            .ForAccessScope(access)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(p =>
                p.PolicyNumber.ToLower().Contains(s) ||
                p.Submission.Insured.DisplayName.ToLower().Contains(s) ||
                p.Carrier.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync();

        q = query.SortDir.ToLower() == "asc"
            ? q.OrderBy(p => p.BoundDate)
            : q.OrderByDescending(p => p.BoundDate);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<PolicyListItemDto>
        {
            Items = items.Select(MapToListItemDto),
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<IEnumerable<PolicyListItemDto>> GetByInsuredAsync(Guid insuredId, UserAccessScope access)
    {
        var items = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Where(p => p.Submission.InsuredId == insuredId && !p.IsDeleted)
            .ForAccessScope(access)
            .OrderByDescending(p => p.BoundDate)
            .ToListAsync();

        return items.Select(MapToListItemDto);
    }

    public async Task<Result<PolicyDto>> GetByIdAsync(Guid id, UserAccessScope access)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Transactions).ThenInclude(t => t.ProcessedBy)
            .Include(p => p.Versions)
            .Where(p => p.Id == id && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        return policy == null
            ? Result<PolicyDto>.Failure("NOT_FOUND", "Policy not found.")
            : Result<PolicyDto>.Success(MapToDto(policy));
    }

    public async Task<Result<PolicyIssuancePacketDto>> GetIssuancePacketAsync(Guid policyId, UserAccessScope access)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.BoundQuote)
            .Include(p => p.Submission)
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        if (policy == null)
            return Result<PolicyIssuancePacketDto>.Failure("NOT_FOUND", "Policy not found.");

        var quoteForms = (IQuotePolicyFormSelectionService)_sp.GetService(typeof(IQuotePolicyFormSelectionService))!;
        await quoteForms.GetOrSeedAsync(policy.BoundQuoteId);

        var forms = await Db.Set<QuotePolicyFormSelection>()
            .Include(f => f.PolicyFormTemplate)
                .ThenInclude(t => t.FieldMappings)
            .Where(f => f.QuoteId == policy.BoundQuoteId)
            .OrderBy(f => f.SequenceOrder)
            .ToListAsync();

        var formDtos = forms.Select(MapIssuanceForm).ToList();
        var readinessMessages = formDtos
            .Where(f => f.IsIncluded && f.ReadinessStatus == "Blocked" && !string.IsNullOrWhiteSpace(f.ReadinessMessage))
            .Select(f => $"{f.FormNumber}: {f.ReadinessMessage!}")
            .ToList();

        return Result<PolicyIssuancePacketDto>.Success(new PolicyIssuancePacketDto
        {
            PolicyId = policy.Id,
            BoundQuoteId = policy.BoundQuoteId,
            IsIssued = policy.IssuedDate.HasValue,
            IssuedDate = policy.IssuedDate,
            IncludedFormCount = formDtos.Count(f => f.IsIncluded),
            IsReady = readinessMessages.Count == 0 && formDtos.Any(f => f.IsIncluded),
            ReadinessMessages = readinessMessages,
            Forms = formDtos,
        });
    }

    public async Task<Result<PolicyDto>> IssueAsync(Guid policyId, IssuePolicyDto dto, UserAccessScope access)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Transactions).ThenInclude(t => t.ProcessedBy)
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        if (policy == null)
            return Result<PolicyDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<PolicyDto>.Failure("INVALID_STATUS", "Only active policies can be issued.");
        if (policy.IssuedDate.HasValue)
            return Result<PolicyDto>.Failure("ALREADY_ISSUED", "Policy has already been issued.");

        var packet = await GetIssuancePacketAsync(policyId, access);
        if (!packet.IsSuccess)
            return Result<PolicyDto>.Failure(packet.ErrorCode ?? "ISSUANCE_PACKET_ERROR", packet.ErrorMessage ?? "Unable to load issuance packet.");
        if (packet.Value!.IncludedFormCount == 0)
            return Result<PolicyDto>.Failure("FORMS_REQUIRED", "Review and include at least one policy form before issuing.");
        if (!packet.Value.IsReady)
            return Result<PolicyDto>.Failure("PACKET_NOT_READY", string.Join(" ", packet.Value.ReadinessMessages));

        var newBusinessTxn = policy.Transactions
            .Where(t => t.TransactionType == TransactionType.NewBusiness)
            .OrderByDescending(t => t.ProcessedAt)
            .FirstOrDefault();

        var assembly = (IPolicyAssemblyService)_sp.GetService(typeof(IPolicyAssemblyService))!;
        var assemblyResult = await assembly.AssembleAndFileAsync(policyId, access.UserId, policyVersionId: newBusinessTxn?.ResultingPolicyVersionId);
        if (!assemblyResult.IsSuccess)
            return Result<PolicyDto>.Failure(assemblyResult.ErrorCode ?? "POLICY_PACKET_FAILED", assemblyResult.ErrorMessage ?? "Policy packet could not be assembled.");

        policy.IssuedDate = dto.IssuedDate;
        policy.UpdatedAt = DateTime.UtcNow;
        if (policy.BoundQuote != null)
        {
            policy.BoundQuote.IssuedDate = dto.IssuedDate;
            policy.BoundQuote.UpdatedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            if (newBusinessTxn != null)
                newBusinessTxn.Notes = string.IsNullOrWhiteSpace(newBusinessTxn.Notes)
                    ? dto.Notes.Trim()
                    : $"{newBusinessTxn.Notes}\n{dto.Notes.Trim()}";
        }

        if (newBusinessTxn != null)
        {
            var transitionResult = await _transactionLifecycle.TransitionAsync(newBusinessTxn, PolicyTransactionStatus.Completed, access.UserId, "Policy issued.");
            if (!transitionResult.IsSuccess)
                return Result<PolicyDto>.Failure(transitionResult.ErrorCode ?? "STATUS_TRANSITION_FAILED", transitionResult.ErrorMessage ?? "Policy transaction could not be completed.");
        }

        await Db.SaveChangesAsync();
        return Result<PolicyDto>.Success(MapToDto(policy));
    }

    public async Task<Result<GeneratedDocumentDto>> GenerateIssuancePacketPreviewAsync(Guid policyId, UserAccessScope access)
    {
        var packet = await GetIssuancePacketAsync(policyId, access);
        if (!packet.IsSuccess)
            return Result<GeneratedDocumentDto>.Failure(packet.ErrorCode ?? "ISSUANCE_PACKET_ERROR", packet.ErrorMessage ?? "Unable to load issuance packet.");
        if (packet.Value!.IncludedFormCount == 0)
            return Result<GeneratedDocumentDto>.Failure("FORMS_REQUIRED", "Review and include at least one policy form before generating the preview packet.");
        if (!packet.Value.IsReady)
            return Result<GeneratedDocumentDto>.Failure("PACKET_NOT_READY", string.Join(" ", packet.Value.ReadinessMessages));

        var assembly = (IPolicyAssemblyService)_sp.GetService(typeof(IPolicyAssemblyService))!;
        return await assembly.AssembleAndFileAsync(policyId, access.UserId, isPreview: true);
    }

    public async Task<Result<VoidTestBindResultDto>> VoidTestBindAsync(Guid policyId, VoidTestBindDto dto, UserAccessScope access, bool isAdmin)
    {
        if (!isAdmin)
            return Result<VoidTestBindResultDto>.Failure("ADMIN_REQUIRED", "Only admins can void a test bind.");

        var db = Db;
        var policy = await db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.BoundQuote)
            .Include(p => p.Transactions)
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();

        if (policy == null)
            return Result<VoidTestBindResultDto>.Failure("NOT_FOUND", "Policy not found.");
        if (!IsTestInsured(policy))
            return Result<VoidTestBindResultDto>.Failure("NOT_TEST_RECORD", "Only policies for test insureds can be voided this way.");
        if (policy.IssuedDate.HasValue)
            return Result<VoidTestBindResultDto>.Failure("POLICY_ISSUED", "Issued policies cannot be voided with the test bind cleanup.");
        if (policy.Status != PolicyStatus.Active)
            return Result<VoidTestBindResultDto>.Failure("INVALID_STATUS", "Only active, unissued test policies can be voided this way.");

        var activeTransactions = policy.Transactions.Where(t => !t.IsDeleted).ToList();
        if (activeTransactions.Count != 1 || activeTransactions[0].TransactionType != TransactionType.NewBusiness)
            return Result<VoidTestBindResultDto>.Failure("TRANSACTIONS_EXIST", "Only a simple new-business bind can be voided this way.");

        var transaction = activeTransactions[0];
        var invoice = await db.Set<Invoice>()
            .FirstOrDefaultAsync(i => i.PolicyTransactionId == transaction.Id && i.Status != "Voided");

        if (invoice != null && invoice.ClearedAmount > 0)
            return Result<VoidTestBindResultDto>.Failure("PAYMENTS_EXIST", "This invoice has payment activity. Void the cash activity first.");

        var payables = invoice == null
            ? new List<Payable>()
            : await db.Set<Payable>().Where(p => p.InvoiceId == invoice.Id).ToListAsync();

        if (payables.Any(p => p.PaidAmount > 0))
            return Result<VoidTestBindResultDto>.Failure("DISBURSEMENTS_EXIST", "This bind has payable payment activity and cannot be test-voided.");

        await using var dbTransaction = await db.Database.BeginTransactionAsync();

        Guid? reversalTransactionId = null;
        if (invoice != null)
        {
            var voidResult = await _voids.VoidInvoiceAsync(
                invoice.Id,
                string.IsNullOrWhiteSpace(dto.Reason) ? "Void test bind" : dto.Reason,
                access.UserId,
                isAdmin);

            if (!voidResult.Success)
                return Result<VoidTestBindResultDto>.Failure(voidResult.ErrorCode ?? "INVOICE_VOID_FAILED", voidResult.ErrorMessage ?? "Invoice could not be voided.");

            reversalTransactionId = voidResult.ReversalTransactionId;
            foreach (var payable in payables)
                payable.Status = "Voided";
        }

        var policyNumber = policy.PolicyNumber;
        var quoteId = policy.BoundQuoteId;

        policy.BoundQuote.Status = QuoteStatus.Quoted;
        policy.BoundQuote.PolicyNumber = null;
        policy.BoundQuote.BoundDate = null;
        policy.BoundQuote.IssuedDate = null;
        policy.BoundQuote.CancelledDate = null;

        transaction.IsDeleted = true;
        transaction.DeletedAt = DateTime.UtcNow;
        transaction.Notes = string.IsNullOrWhiteSpace(dto.Reason)
            ? "Voided as test bind cleanup."
            : $"Voided as test bind cleanup: {dto.Reason}";

        policy.IsDeleted = true;
        policy.DeletedAt = DateTime.UtcNow;

        var usage = await db.Set<PolicyNumberSequenceUsage>()
            .FirstOrDefaultAsync(u => u.PolicyId == policy.Id || (u.QuoteId == quoteId && u.FullPolicyNumber == policyNumber));
        if (usage != null)
        {
            usage.PolicyId = null;
            usage.IsDeleted = true;
            usage.DeletedAt = DateTime.UtcNow;
        }

        await RecalculateSubmissionStatusAsync(policy.SubmissionId);
        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Result<VoidTestBindResultDto>.Success(new VoidTestBindResultDto
        {
            PolicyId = policy.Id,
            QuoteId = quoteId,
            PolicyNumber = policyNumber,
            VoidedInvoiceId = invoice?.Id,
            ReversalTransactionId = reversalTransactionId,
        });
    }

    private static PolicyIssuanceFormDto MapIssuanceForm(QuotePolicyFormSelection form)
    {
        var readiness = GetFormReadiness(form.PolicyFormTemplate);
        return new PolicyIssuanceFormDto
        {
            Id = form.Id,
            PolicyFormTemplateId = form.PolicyFormTemplateId,
            FormNumber = form.PolicyFormTemplate.FormNumber,
            FormName = form.PolicyFormTemplate.Name,
            EditionDate = form.PolicyFormTemplate.EditionDate,
            SequenceOrder = form.SequenceOrder,
            FormType = form.FormType,
            IsIncluded = form.IsIncluded,
            IsSystemGenerated = form.IsSystemGenerated,
            FileName = form.PolicyFormTemplate.FileName,
            ReadinessStatus = readiness.Status,
            ReadinessMessage = readiness.Message,
        };
    }

    private static (string Status, string? Message) GetFormReadiness(PolicyFormTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.StoragePath) || string.IsNullOrWhiteSpace(template.FileName))
            return ("Blocked", "No file has been uploaded for this form.");

        var extension = Path.GetExtension(template.FileName).ToLowerInvariant();
        if (extension is not ".pdf" and not ".doc" and not ".docx" and not ".html" and not ".htm")
            return ("Blocked", "Only PDF, DOC, DOCX, and HTML forms can be assembled.");

        if (template.IsFillable && !template.FieldMappings.Any(m => !string.IsNullOrWhiteSpace(m.PdfFieldName) && !string.IsNullOrWhiteSpace(m.DataPath)))
            return ("Warning", "Fillable PDF has no mapped fields; it will be included as uploaded.");

        return ("Ready", null);
    }

    public async Task<Result<PolicyTransactionDto>> AddEndorsementAsync(
        Guid policyId, CreateEndorsementDto dto, UserAccessScope access)
    {
        var policy = await Db.Set<Policy>()
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (policy == null) return Result<PolicyTransactionDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<PolicyTransactionDto>.Failure("INVALID_STATUS", "Only active policies can be endorsed.");

        var txnNumber = await GenerateTransactionNumberAsync();
        var txn = new PolicyTransaction
        {
            PolicyId = policyId,
            TransactionType = TransactionType.Endorsement,
            Status = PolicyTransactionStatus.Submitted,
            TransactionNumber = txnNumber,
            EffectiveDate = dto.EffectiveDate,
            SourceQuoteId = policy.BoundQuoteId,
            RequestedById = access.UserId,
            RequestedAt = DateTime.UtcNow,
            PremiumBefore = policy.TotalPremium,
            PremiumChange = dto.PremiumChange,
            NewTotalPremium = policy.TotalPremium + dto.PremiumChange,
            PremiumAfter = policy.TotalPremium + dto.PremiumChange,
            EndorsementDescription = dto.EndorsementDescription,
            ReasonText = dto.EndorsementDescription,
            Notes = dto.Notes,
            ProcessedById = access.UserId,
            ProcessedAt = DateTime.UtcNow,
        };

        Db.Set<PolicyTransaction>().Add(txn);
        await Db.SaveChangesAsync();
        await _transactionLifecycle.RecordCreatedAsync(txn, access.UserId, "Endorsement transaction submitted.");
        await Db.Entry(txn).Reference(t => t.ProcessedBy).LoadAsync();

        return Result<PolicyTransactionDto>.Success(MapToTransactionDto(txn));
    }

    public async Task<Result<PolicyTransactionDto>> IssueEndorsementAsync(
        Guid policyId, Guid txnId, IssueEndorsementDto dto, UserAccessScope access)
    {
        var txn = await Db.Set<PolicyTransaction>()
            .Include(t => t.Policy).ThenInclude(p => p.BoundQuote)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Locations)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Vehicles)
            .Include(t => t.ProcessedBy)
            .Where(t => t.Id == txnId && t.PolicyId == policyId)
            .Where(t =>
                access.CanAccessAllBusinessData ||
                t.Policy.Submission.CreatedById == access.UserId ||
                t.Policy.Submission.UnderwriterId == access.UserId ||
                t.Policy.Submission.AssistantUWId == access.UserId ||
                t.Policy.BoundQuote.CreatedById == access.UserId)
            .FirstOrDefaultAsync();

        if (txn == null) return Result<PolicyTransactionDto>.Failure("NOT_FOUND", "Endorsement not found.");
        if (txn.TransactionType != TransactionType.Endorsement)
            return Result<PolicyTransactionDto>.Failure("INVALID_TYPE", "Transaction is not an endorsement.");

        if (dto.EffectiveDate.HasValue) txn.EffectiveDate = dto.EffectiveDate.Value;
        if (dto.PremiumChange.HasValue)
        {
            txn.PremiumBefore ??= txn.Policy.TotalPremium;
            txn.PremiumChange = dto.PremiumChange.Value;
            txn.NewTotalPremium = txn.Policy.TotalPremium + dto.PremiumChange.Value;
            txn.PremiumAfter = txn.NewTotalPremium;
        }
        txn.PremiumAfter ??= txn.NewTotalPremium;

        var priorVersion = await _policyVersions.EnsureCurrentVersionAsync(txn.Policy, access.UserId);
        var transitionResult = await _transactionLifecycle.TransitionAsync(txn, PolicyTransactionStatus.Issued, access.UserId, "Endorsement issued.");
        if (!transitionResult.IsSuccess)
            return Result<PolicyTransactionDto>.Failure(transitionResult.ErrorCode ?? "STATUS_TRANSITION_FAILED", transitionResult.ErrorMessage ?? "Endorsement status could not be updated.");

        txn.Policy.TotalPremium = txn.NewTotalPremium;
        await _policyVersions.CreateVersionAsync(txn.Policy, txn, priorVersion, access.UserId);
        await Db.SaveChangesAsync();

        // Auto-create invoice
        var quote = txn.Policy.BoundQuote;
        var submission = txn.Policy.Submission;
        if (quote != null && submission != null)
        {
            var req = new CreateInvoiceRequest(
                EffectiveDate: txn.EffectiveDate,
                GrossPremium: txn.PremiumChange,
                StateCode: submission.Insured?.State ?? "",
                IsEndorsement: true,
                IsFilingState: quote.IsFilingState,
                CarrierId: txn.Policy.CarrierId,
                CompanyId: quote.CompanyId,
                ProducerId: quote.ProducerId,
                LineOfBusiness: quote.LineOfBusiness.ToString(),
                City: null,
                LicenseType: null,
                LocationCount: submission.Locations?.Count(l => !l.IsDeleted) ?? 1,
                VehicleCount: submission.Vehicles?.Count(v => !v.IsDeleted) ?? 1,
                PolicyTransactionId: txn.Id
            );
            await _invoicing.BindAsync(req, access.UserId);
        }

        return Result<PolicyTransactionDto>.Success(MapToTransactionDto(txn));
    }

    public async Task<Result<QuoteDto>> CreateRenewalQuoteAsync(Guid policyId, UserAccessScope access)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.BoundQuote)
            .Include(p => p.Submission)
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (policy == null) return Result<QuoteDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<QuoteDto>.Failure("INVALID_STATUS", "Only active policies can be renewed.");

        var quoteService = (IQuoteService)_sp.GetService(typeof(IQuoteService))!;
        var source = policy.BoundQuote;

        var renewalDto = new QuoteCreateDto
        {
            SubmissionId = policy.SubmissionId,
            CarrierId = policy.CarrierId,
            LineOfBusiness = policy.LineOfBusiness,
            EffectiveDate = policy.ExpirationDate,
            ExpirationDate = policy.ExpirationDate.AddYears(1),
            PremiumAmount = policy.PremiumAmount,
            TaxesAndFees = policy.TaxesAndFees,
            CompanyId = source?.CompanyId,
            ProducerId = source?.ProducerId,
            IsFilingState = source?.IsFilingState ?? false,
            CoverageDescription = source?.CoverageDescription,
            Deductible = source?.Deductible,
            Limit = source?.Limit,
            UninsuredMotoristLimit = source?.UninsuredMotoristLimit,
            MedicalPaymentsLimit = source?.MedicalPaymentsLimit,
        };

        var renewalResult = await quoteService.CreateAsync(renewalDto, access.UserId);
        if (!renewalResult.IsSuccess || renewalResult.Value == null)
            return renewalResult;

        var transaction = new PolicyTransaction
        {
            PolicyId = policy.Id,
            TransactionType = TransactionType.Renewal,
            Status = PolicyTransactionStatus.Submitted,
            TransactionNumber = await GenerateTransactionNumberAsync(),
            EffectiveDate = policy.ExpirationDate,
            ExpirationDate = policy.ExpirationDate.AddYears(1),
            PriorPolicyId = policy.Id,
            SourceQuoteId = policy.BoundQuoteId,
            RenewalQuoteId = renewalResult.Value.Id,
            RequestedById = access.UserId,
            RequestedAt = DateTime.UtcNow,
            PremiumBefore = policy.TotalPremium,
            PremiumChange = 0m,
            NewTotalPremium = policy.TotalPremium,
            PremiumAfter = policy.TotalPremium,
            ProcessedById = access.UserId,
            ProcessedAt = DateTime.UtcNow,
        };
        Db.Set<PolicyTransaction>().Add(transaction);
        await Db.SaveChangesAsync();
        await _transactionLifecycle.RecordCreatedAsync(transaction, access.UserId, "Renewal transaction submitted.");

        return renewalResult;
    }

    public async Task<Result<PolicyDto>> CancelAsync(Guid policyId, CancelPolicyDto dto, UserAccessScope access)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Transactions).ThenInclude(t => t.ProcessedBy)
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (policy == null) return Result<PolicyDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<PolicyDto>.Failure("INVALID_STATUS", "Only active policies can be cancelled.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result<PolicyDto>.Failure("REASON_REQUIRED", "Cancellation reason is required.");
        if (dto.CancelledDate < policy.EffectiveDate || dto.CancelledDate > policy.ExpirationDate)
            return Result<PolicyDto>.Failure("INVALID_DATE", "Cancellation date must be within the policy term.");
        if (dto.ComplianceChecklist.Count == 0 || dto.ComplianceChecklist.Any(i => !i.IsCompleted))
            return Result<PolicyDto>.Failure("COMPLIANCE_REVIEW_REQUIRED", "Complete the cancellation compliance checklist before cancelling the policy.");

        var state = NormalizeState(policy.Submission?.Insured?.State);
        var legalRequirementIds = dto.LegalRequirementSectionIds
            .Concat(dto.ComplianceChecklist.SelectMany(i => i.RequirementSectionIds))
            .Distinct()
            .ToArray();
        var legalRequirements = legalRequirementIds.Length == 0
            ? await GetCancellationRequirementQuery(state).ToListAsync()
            : await GetCancellationRequirementQuery(state)
                .Where(r => legalRequirementIds.Contains(r.Id))
                .ToListAsync();
        var legalSnapshot = legalRequirements.Select(r => new
        {
            r.Id,
            r.State,
            r.Category,
            r.Topic,
            r.RequirementText,
            r.Citations,
            r.LastVerifiedAt
        }).ToList();

        var premiumBefore = policy.TotalPremium;
        var priorVersion = await _policyVersions.EnsureCurrentVersionAsync(policy, access.UserId);
        policy.Status = PolicyStatus.Cancelled;
        policy.CancelledDate = dto.CancelledDate;
        policy.TotalPremium += dto.PremiumChange;
        policy.UpdatedAt = DateTime.UtcNow;

        var cancellationTransaction = new PolicyTransaction
        {
            PolicyId = policy.Id,
            Policy = policy,
            TransactionType = TransactionType.Cancellation,
            Status = PolicyTransactionStatus.Issued,
            TransactionNumber = await GenerateTransactionNumberAsync(),
            EffectiveDate = dto.CancelledDate,
            SourceQuoteId = policy.BoundQuoteId,
            RequestedById = access.UserId,
            RequestedAt = DateTime.UtcNow,
            IssuedById = access.UserId,
            IssuedAt = DateTime.UtcNow,
            ReasonText = dto.Reason.Trim(),
            CancellationReason = dto.Reason.Trim(),
            CancellationMethod = string.IsNullOrWhiteSpace(dto.Method) ? "Written Notice" : dto.Method.Trim(),
            CancellationComplianceChecklistJson = JsonSerializer.Serialize(dto.ComplianceChecklist),
            CancellationLegalRequirementSnapshotJson = JsonSerializer.Serialize(legalSnapshot),
            PremiumBefore = premiumBefore,
            PremiumChange = dto.PremiumChange,
            NewTotalPremium = policy.TotalPremium,
            PremiumAfter = policy.TotalPremium,
            ProcessedById = access.UserId,
            ProcessedAt = DateTime.UtcNow,
            Notes = dto.Notes
        };
        Db.Set<PolicyTransaction>().Add(cancellationTransaction);

        await Db.SaveChangesAsync();
        await _policyVersions.CreateVersionAsync(policy, cancellationTransaction, priorVersion, access.UserId);
        await _transactionLifecycle.RecordCreatedAsync(cancellationTransaction, access.UserId, "Cancellation transaction issued.");
        return Result<PolicyDto>.Success(MapToDto(policy));
    }

    public async Task<Result<PolicyDto>> NonRenewAsync(Guid policyId, NonRenewPolicyDto dto, UserAccessScope access)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Carrier)
            .Include(p => p.BoundQuote)
            .Include(p => p.Transactions).ThenInclude(t => t.ProcessedBy)
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (policy == null) return Result<PolicyDto>.Failure("NOT_FOUND", "Policy not found.");
        if (policy.Status != PolicyStatus.Active)
            return Result<PolicyDto>.Failure("INVALID_STATUS", "Only active policies can be non-renewed.");

        var priorVersion = await _policyVersions.EnsureCurrentVersionAsync(policy, access.UserId);
        policy.Status = PolicyStatus.NonRenewed;
        policy.NonRenewedDate = dto.NonRenewedDate;
        policy.UpdatedAt = DateTime.UtcNow;
        var transaction = new PolicyTransaction
        {
            PolicyId = policy.Id,
            Policy = policy,
            TransactionType = TransactionType.NonRenewal,
            Status = PolicyTransactionStatus.Completed,
            TransactionNumber = await GenerateTransactionNumberAsync(),
            EffectiveDate = dto.NonRenewedDate,
            SourceQuoteId = policy.BoundQuoteId,
            RequestedById = access.UserId,
            RequestedAt = DateTime.UtcNow,
            CompletedById = access.UserId,
            CompletedAt = DateTime.UtcNow,
            ReasonText = dto.Reason?.Trim(),
            PremiumBefore = policy.TotalPremium,
            PremiumChange = 0m,
            NewTotalPremium = policy.TotalPremium,
            PremiumAfter = policy.TotalPremium,
            ProcessedById = access.UserId,
            ProcessedAt = DateTime.UtcNow,
            Notes = dto.Reason,
        };
        Db.Set<PolicyTransaction>().Add(transaction);
        await Db.SaveChangesAsync();
        await _policyVersions.CreateVersionAsync(policy, transaction, priorVersion, access.UserId);
        await _transactionLifecycle.RecordCreatedAsync(transaction, access.UserId, "Non-renewal transaction completed.");

        return Result<PolicyDto>.Success(MapToDto(policy));
    }

    public async Task<Result<LegalComplianceGuidanceDto>> GetCancellationGuidanceAsync(Guid policyId, UserAccessScope access)
    {
        return await GetLegalGuidanceAsync(policyId, access, "Cancellation");
    }

    public async Task<Result<LegalComplianceGuidanceDto>> GetNonRenewalGuidanceAsync(Guid policyId, UserAccessScope access)
    {
        return await GetLegalGuidanceAsync(policyId, access, "NonRenewal");
    }

    private async Task<Result<LegalComplianceGuidanceDto>> GetLegalGuidanceAsync(Guid policyId, UserAccessScope access, string action)
    {
        var policy = await Db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Where(p => p.Id == policyId && !p.IsDeleted)
            .ForAccessScope(access)
            .FirstOrDefaultAsync();
        if (policy == null) return Result<LegalComplianceGuidanceDto>.Failure("NOT_FOUND", "Policy not found.");

        var state = NormalizeState(policy.Submission?.Insured?.State);
        if (string.IsNullOrWhiteSpace(state))
            return Result<LegalComplianceGuidanceDto>.Failure("STATE_REQUIRED", "Insured state is required for legal guidance.");

        var rows = await GetRequirementQuery(state, action)
            .Select(r => new LegalComplianceRequirementDto
            {
                Id = r.Id,
                Category = r.Category,
                Topic = r.Topic,
                RequirementText = r.RequirementText,
                Citations = r.Citations,
                LastVerifiedAt = r.LastVerifiedAt
            })
            .ToListAsync();

        return Result<LegalComplianceGuidanceDto>.Success(new LegalComplianceGuidanceDto
        {
            State = state,
            LineOfBusiness = policy.LineOfBusiness.ToString(),
            Action = action,
            Requirements = rows,
            NoticeRequirements = rows.Where(r => r.Category == "NOTICE REQUIREMENTS").ToList(),
            ReasonRequirements = rows.Where(r => r.Category == "REASONS").ToList(),
            ProofOfNoticeRequirements = rows.Where(r => ContainsAny(r.Topic, "Proof")).ToList(),
            LienholderRequirements = rows.Where(r => ContainsAny(r.Topic, "Lienholder", "Mortgagee")).ToList(),
            StateAuthorityRequirements = rows.Where(r =>
                ContainsAny(r.Topic, "State Authority") ||
                ContainsAny(r.RequirementText, "Department", "DMV", "Motor Vehicle")).ToList(),
            ReturnPremiumRequirements = rows.Where(r =>
                ContainsAny(r.Topic, "Return of Unearned Premium") ||
                ContainsAny(r.RequirementText, "unearned premium")).ToList()
        });
    }

    private IQueryable<LegalRequirementSection> GetCancellationRequirementQuery(string state)
    {
        return GetRequirementQuery(state, "Cancellation");
    }

    private IQueryable<LegalRequirementSection> GetRequirementQuery(string state, string action)
    {
        return Db.Set<LegalRequirementSection>()
            .Where(r => r.State == state && r.Action == action)
            .OrderBy(r => r.Category == "NOTICE REQUIREMENTS" ? 0 :
                          r.Category == "REASONS" ? 1 :
                          r.Category == "INSURER REQUIREMENTS" ? 2 : 3)
            .ThenBy(r => r.SortOrder);
    }

    private async Task<string> GenerateTransactionNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"TXN-{year}-";
        var count = await Db.Set<PolicyTransaction>()
            .IgnoreQueryFilters()
            .CountAsync(t => t.TransactionNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D5}";
    }

    private async Task RecalculateSubmissionStatusAsync(Guid submissionId)
    {
        var submission = await Db.Set<Submission>()
            .Include(s => s.Quotes)
            .FirstOrDefaultAsync(s => s.Id == submissionId);

        if (submission == null)
            return;

        submission.Status = submission.Quotes.Any(q => !q.IsDeleted && q.Status == QuoteStatus.Bound)
            ? SubmissionStatus.Bound
            : SubmissionStatus.Quoted;
    }

    private static bool IsTestInsured(Policy policy)
    {
        var insured = policy.Submission?.Insured;
        if (insured == null)
            return false;

        var values = new[]
        {
            insured.DisplayName,
            insured.CompanyName,
            insured.FirstName,
            insured.LastName,
            insured.Email,
        };

        return values.Any(v => !string.IsNullOrWhiteSpace(v) && v.Contains("test", StringComparison.OrdinalIgnoreCase));
    }

    private static PolicyListItemDto MapToListItemDto(Policy p) => new()
    {
        Id = p.Id,
        PolicyNumber = p.PolicyNumber,
        SubmissionId = p.SubmissionId,
        InsuredName = p.Submission?.Insured?.DisplayName ?? "",
        CarrierName = p.Carrier?.Name ?? "",
        LineOfBusiness = p.LineOfBusiness,
        EffectiveDate = p.EffectiveDate,
        ExpirationDate = p.ExpirationDate,
        TotalPremium = p.TotalPremium,
        Status = p.Status,
        BoundDate = p.BoundDate,
        CreatedAt = p.CreatedAt,
    };

    private static PolicyDto MapToDto(Policy p)
    {
        var quote = p.BoundQuote;
        var premium = quote?.PremiumAmount ?? p.PremiumAmount;
        var effectiveCarrier = quote?.EffectiveCarrierRate ?? 0;
        var effectiveSMM = quote?.EffectiveSMMRate ?? 0;
        var effectiveAgent = quote?.EffectiveAgentRate ?? 0;

        return new PolicyDto
        {
            Id = p.Id,
            PolicyNumber = p.PolicyNumber,
            SubmissionId = p.SubmissionId,
            SubmissionNumber = p.Submission?.SubmissionNumber ?? "",
            InsuredId = p.Submission?.InsuredId ?? Guid.Empty,
            InsuredName = p.Submission?.Insured?.DisplayName ?? "",
            InsuredState = p.Submission?.Insured?.State ?? "",
            CarrierId = p.CarrierId,
            CarrierName = p.Carrier?.Name ?? "",
            LineOfBusiness = p.LineOfBusiness,
            EffectiveDate = p.EffectiveDate,
            ExpirationDate = p.ExpirationDate,
            PremiumAmount = p.PremiumAmount,
            TaxesAndFees = p.TaxesAndFees,
            TotalPremium = p.TotalPremium,
            Status = p.Status,
            BoundDate = p.BoundDate,
            IssuedDate = p.IssuedDate,
            CancelledDate = p.CancelledDate,
            NonRenewedDate = p.NonRenewedDate,
            BoundQuoteId = p.BoundQuoteId,
            CarrierCommissionRate = effectiveCarrier,
            SMMRetentionRate = effectiveSMM,
            AgentCommissionRate = effectiveAgent,
            CarrierCommissionAmount = Math.Round(premium * effectiveCarrier, 2),
            SMMRetentionAmount = Math.Round(premium * effectiveSMM, 2),
            AgentCommissionAmount = Math.Round(premium * effectiveAgent, 2),
            CoverageDescription = quote?.CoverageDescription,
            Deductible = quote?.Deductible,
            Limit = quote?.Limit,
            UninsuredMotoristLimit = quote?.UninsuredMotoristLimit,
            MedicalPaymentsLimit = quote?.MedicalPaymentsLimit,
            Transactions = MapTransactionDtos(p),
            CreatedAt = p.CreatedAt,
        };
    }

    private static string NormalizeState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length != 2) return trimmed;

        return trimmed.ToUpperInvariant() switch
        {
            "AL" => "Alabama",
            "AR" => "Arkansas",
            "FL" => "Florida",
            "GA" => "Georgia",
            "LA" => "Louisiana",
            "MD" => "Maryland",
            "MS" => "Mississippi",
            "NC" => "North Carolina",
            "OK" => "Oklahoma",
            "PA" => "Pennsylvania",
            "SC" => "South Carolina",
            "TN" => "Tennessee",
            "TX" => "Texas",
            "VA" => "Virginia",
            _ => trimmed
        };
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private static List<PolicyTransactionDto> MapTransactionDtos(Policy policy)
    {
        var versions = policy.Versions.ToDictionary(v => v.Id);
        return policy.Transactions
            .OrderBy(t => t.ProcessedAt)
            .Select(t => MapToTransactionDto(t, versions))
            .ToList();
    }

    private static PolicyTransactionDto MapToTransactionDto(PolicyTransaction t)
        => MapToTransactionDto(t, new Dictionary<Guid, PolicyVersion>());

    private static PolicyTransactionDto MapToTransactionDto(PolicyTransaction t, IReadOnlyDictionary<Guid, PolicyVersion> versions) => new()
    {
        Id = t.Id,
        PolicyId = t.PolicyId,
        TransactionType = t.TransactionType,
        Status = t.Status,
        TransactionNumber = t.TransactionNumber,
        EffectiveDate = t.EffectiveDate,
        ExpirationDate = t.ExpirationDate,
        SourceQuoteId = t.SourceQuoteId,
        RenewalQuoteId = t.RenewalQuoteId,
        PriorPolicyVersionId = t.PriorPolicyVersionId,
        ResultingPolicyVersionId = t.ResultingPolicyVersionId,
        PriorVersion = t.PriorPolicyVersionId.HasValue && versions.TryGetValue(t.PriorPolicyVersionId.Value, out var priorVersion)
            ? MapToVersionSummaryDto(priorVersion)
            : null,
        ResultingVersion = t.ResultingPolicyVersionId.HasValue && versions.TryGetValue(t.ResultingPolicyVersionId.Value, out var resultingVersion)
            ? MapToVersionSummaryDto(resultingVersion)
            : null,
        RequestedById = t.RequestedById,
        RequestedAt = t.RequestedAt,
        ReviewedById = t.ReviewedById,
        ReviewedAt = t.ReviewedAt,
        ApprovedById = t.ApprovedById,
        ApprovedAt = t.ApprovedAt,
        IssuedById = t.IssuedById,
        IssuedAt = t.IssuedAt,
        CompletedById = t.CompletedById,
        CompletedAt = t.CompletedAt,
        ReasonCode = t.ReasonCode,
        ReasonText = t.ReasonText,
        EndorsementDescription = t.EndorsementDescription,
        PriorPolicyId = t.PriorPolicyId,
        CancellationReason = t.CancellationReason,
        CancellationMethod = t.CancellationMethod,
        CancellationComplianceChecklist = DeserializeChecklist(t.CancellationComplianceChecklistJson),
        CancellationLegalRequirementSnapshotJson = t.CancellationLegalRequirementSnapshotJson,
        PremiumBefore = t.PremiumBefore,
        PremiumChange = t.PremiumChange,
        NewTotalPremium = t.NewTotalPremium,
        PremiumAfter = t.PremiumAfter,
        TaxesAndFeesDelta = t.TaxesAndFeesDelta,
        CommissionDelta = t.CommissionDelta,
        BillingModeSnapshot = t.BillingModeSnapshot,
        ExternalReference = t.ExternalReference,
        VoidsPolicyTransactionId = t.VoidsPolicyTransactionId,
        ReversesPolicyTransactionId = t.ReversesPolicyTransactionId,
        ProcessedByName = t.ProcessedBy != null
            ? $"{t.ProcessedBy.FirstName} {t.ProcessedBy.LastName}".Trim()
            : "",
        ProcessedAt = t.ProcessedAt,
        Notes = t.Notes,
    };

    private static PolicyVersionSummaryDto MapToVersionSummaryDto(PolicyVersion version) => new()
    {
        Id = version.Id,
        VersionNumber = version.VersionNumber,
        EffectiveDate = version.EffectiveDate,
        ExpirationDate = version.ExpirationDate,
        Status = version.Status,
        PremiumAmount = version.PremiumAmount,
        TaxesAndFees = version.TaxesAndFees,
        TotalPremium = version.TotalPremium,
        RatingSnapshotId = version.RatingSnapshotId,
        CreatedAt = version.CreatedAt,
    };

    private static IReadOnlyList<CancellationComplianceChecklistItemDto> DeserializeChecklist(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<CancellationComplianceChecklistItemDto>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
