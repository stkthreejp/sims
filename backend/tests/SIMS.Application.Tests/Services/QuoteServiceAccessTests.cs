using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Common;
using SIMS.Application.DTOs;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Security;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class QuoteServiceAccessTests
{
    public static TheoryData<QuoteCreateDto, string> InvalidQuoteCreateRequests
    {
        get
        {
            var data = new TheoryData<QuoteCreateDto, string>();
            data.Add(ValidCreateRequest(dto => dto.PremiumAmount = -1m), "INVALID_QUOTE_AMOUNT");
            data.Add(ValidCreateRequest(dto => dto.TaxesAndFees = -1m), "INVALID_QUOTE_AMOUNT");
            data.Add(ValidCreateRequest(dto => dto.EffectiveDate = default), "INVALID_QUOTE_DATES");
            data.Add(ValidCreateRequest(dto =>
            {
                dto.EffectiveDate = new DateOnly(2026, 1, 2);
                dto.ExpirationDate = new DateOnly(2026, 1, 1);
            }), "INVALID_QUOTE_DATES");
            return data;
        }
    }

    [Fact]
    public async Task CreateAsync_DeniesSubmissionOutsideUserAccessScope()
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var carrier = new Carrier
        {
            Id = Guid.NewGuid(),
            Name = "Test Carrier",
            IsActive = true,
        };
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            CompanyName = "Outside Account",
            InsuredType = InsuredType.Commercial,
            State = "NC",
            CreatedById = otherUserId,
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = "SUB-OUTSIDE",
            InsuredId = insured.Id,
            Insured = insured,
            UnderwriterId = otherUserId,
            CreatedById = otherUserId,
        };
        db.AddRange(carrier, insured, submission);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var request = new QuoteCreateDto
        {
            SubmissionId = submission.Id,
            CarrierId = carrier.Id,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            PremiumAmount = 1000m,
            TaxesAndFees = 100m,
        };

        var result = await service.CreateAsync(request, currentUserId, new UserAccessScope(currentUserId, false));

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessDataAccess.AccessDeniedCode, result.ErrorCode);
        Assert.Empty(await db.Set<Quote>().ToListAsync());
    }

    [Theory]
    [MemberData(nameof(InvalidQuoteCreateRequests))]
    public async Task CreateAsync_RejectsInvalidMoneyAndDatesWithoutCreatingQuote(QuoteCreateDto request, string expectedCode)
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var submission = SeedSubmissionWithCarrier(db, request.CarrierId, currentUserId);
        request.SubmissionId = submission.Id;
        await db.SaveChangesAsync();

        var result = await CreateService(db).CreateAsync(request, currentUserId, new UserAccessScope(currentUserId, true));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(await db.Set<Quote>().ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_RejectsInvalidMoneyAndDatesWithoutChangingQuote()
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var carrierId = Guid.NewGuid();
        var submission = SeedSubmissionWithCarrier(db, carrierId, currentUserId);
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "QTE-2026-0001",
            SubmissionId = submission.Id,
            CarrierId = carrierId,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            PremiumAmount = 1000m,
            TaxesAndFees = 100m,
            TotalPremium = 1100m,
            CreatedById = currentUserId,
        };
        db.Add(quote);
        await db.SaveChangesAsync();

        var result = await CreateService(db).UpdateAsync(quote.Id, new QuoteUpdateDto
        {
            SubmissionId = submission.Id,
            CarrierId = carrierId,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2027, 1, 1),
            ExpirationDate = new DateOnly(2026, 1, 1),
            PremiumAmount = -100m,
            TaxesAndFees = 25m,
            Status = QuoteStatus.Quoted,
        }, new UserAccessScope(currentUserId, true));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_QUOTE_AMOUNT", result.ErrorCode);
        var saved = await db.Set<Quote>().SingleAsync(q => q.Id == quote.Id);
        Assert.Equal(1000m, saved.PremiumAmount);
        Assert.Equal(100m, saved.TaxesAndFees);
        Assert.Equal(1100m, saved.TotalPremium);
        Assert.Equal(new DateOnly(2026, 1, 1), saved.EffectiveDate);
        Assert.Equal(new DateOnly(2027, 1, 1), saved.ExpirationDate);
        Assert.Equal(QuoteStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task BindAsync_RejectsInvalidDatesBeforeMutatingQuote()
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var carrierId = Guid.NewGuid();
        var submission = SeedSubmissionWithCarrier(db, carrierId, currentUserId);
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "QTE-2026-0001",
            SubmissionId = submission.Id,
            CarrierId = carrierId,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            PremiumAmount = 1000m,
            TaxesAndFees = 100m,
            TotalPremium = 1100m,
            CreatedById = currentUserId,
        };
        db.Add(quote);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BindAsync(quote.Id, new QuoteBindDto
        {
            BoundDate = default,
            EffectiveDate = new DateOnly(2027, 1, 1),
            ExpirationDate = new DateOnly(2026, 1, 1),
        }, new UserAccessScope(currentUserId, true));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_QUOTE_DATES", result.ErrorCode);
        var saved = await db.Set<Quote>().SingleAsync(q => q.Id == quote.Id);
        Assert.Equal(QuoteStatus.Draft, saved.Status);
        Assert.Null(saved.PolicyNumber);
        Assert.Null(saved.BoundDate);
        Assert.Equal(new DateOnly(2026, 1, 1), saved.EffectiveDate);
        Assert.Equal(new DateOnly(2027, 1, 1), saved.ExpirationDate);
        Assert.Empty(await db.Set<Policy>().ToListAsync());
    }

    [Fact]
    public async Task BindAsync_RejectsInvalidExistingQuoteAmountsBeforeMutatingQuote()
    {
        await using var db = CreateDb();
        var currentUserId = Guid.NewGuid();
        var carrierId = Guid.NewGuid();
        var submission = SeedSubmissionWithCarrier(db, carrierId, currentUserId);
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "QTE-2026-0001",
            SubmissionId = submission.Id,
            CarrierId = carrierId,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            PremiumAmount = -1000m,
            TaxesAndFees = 100m,
            TotalPremium = -900m,
            CreatedById = currentUserId,
        };
        db.Add(quote);
        await db.SaveChangesAsync();

        var result = await CreateService(db).BindAsync(quote.Id, new QuoteBindDto
        {
            BoundDate = new DateOnly(2026, 1, 1),
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
        }, new UserAccessScope(currentUserId, true));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_QUOTE_AMOUNT", result.ErrorCode);
        var saved = await db.Set<Quote>().SingleAsync(q => q.Id == quote.Id);
        Assert.Equal(QuoteStatus.Draft, saved.Status);
        Assert.Null(saved.PolicyNumber);
        Assert.Null(saved.BoundDate);
        Assert.Empty(await db.Set<Policy>().ToListAsync());
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static QuoteCreateDto ValidCreateRequest(Action<QuoteCreateDto>? configure = null)
    {
        var request = new QuoteCreateDto
        {
            CarrierId = Guid.NewGuid(),
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            PremiumAmount = 1000m,
            TaxesAndFees = 100m,
        };

        configure?.Invoke(request);
        return request;
    }

    private static Submission SeedSubmissionWithCarrier(ApplicationDbContext db, Guid carrierId, Guid userId)
    {
        var carrier = new Carrier
        {
            Id = carrierId,
            Name = "Test Carrier",
            IsActive = true,
        };
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            CompanyName = "Test Account",
            InsuredType = InsuredType.Commercial,
            State = "NC",
            CreatedById = userId,
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = "SUB-VALID",
            InsuredId = insured.Id,
            Insured = insured,
            UnderwriterId = userId,
            CreatedById = userId,
        };
        db.AddRange(carrier, insured, submission);

        return submission;
    }

    private static QuoteService CreateService(ApplicationDbContext db)
    {
        var services = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();

        return new QuoteService(
            services,
            new NoOpWorkflowEngineService(),
            new NoOpCarrierCommissionService(),
            new NoOpAgentCommissionService(),
            new NoOpQuoteChecklistService(),
            new NoOpPolicyNumberService(),
            new NoOpPolicyTransactionLifecycleService(),
            new NoOpPolicyVersionService(),
            new NoOpUnderwritingClearanceService(),
            new NoOpUnderwritingReferralService(),
            new NoOpUnderwritingControlEnforcementService());
    }

    private sealed class NoOpWorkflowEngineService : IWorkflowEngineService
    {
        public Task FireEventAsync(string eventName, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
            => Task.CompletedTask;

        public Task FireStepCompletedAsync(Guid completedStepId, TaskEntityType entityType, Guid entityId, Dictionary<string, object> context)
            => Task.CompletedTask;
    }

    private sealed class NoOpCarrierCommissionService : ICarrierCommissionService
    {
        public Task<IReadOnlyList<CarrierCommissionDto>> GetAllAsync(Guid carrierId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CarrierCommissionDto>>([]);

        public Task<Result<CarrierCommissionDto>> CreateAsync(Guid carrierId, CreateCarrierCommissionRequest req, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<CarrierCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<CarrierCommissionRates?> GetActiveRatesAsync(Guid carrierId, string? lineOfBusiness, DateOnly asOfDate, Guid? programConfigurationId = null, CancellationToken ct = default)
            => Task.FromResult<CarrierCommissionRates?>(null);
    }

    private sealed class NoOpAgentCommissionService : IAgentCommissionService
    {
        public Task<IReadOnlyList<AgentCommissionDto>> GetAllAsync(Guid agentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AgentCommissionDto>>([]);

        public Task<Result<AgentCommissionDto>> CreateAsync(Guid agentId, CreateAgentCommissionRequest req, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Result<AgentCommissionDto>> DisableAsync(long id, DateOnly? disabledDate, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<decimal?> GetActiveRateAsync(Guid agentId, string? lineOfBusiness, DateOnly asOfDate, Guid? programConfigurationId = null, Guid? carrierId = null, string? stateCode = null, CancellationToken ct = default)
            => Task.FromResult<decimal?>(null);
    }

    private sealed class NoOpQuoteChecklistService : IQuoteChecklistService
    {
        public Task<Result<List<QuoteChecklistItemDto>>> GetForQuoteAsync(Guid quoteId, UserAccessScope access, IReadOnlyCollection<UnderwritingControlStage>? stages = null)
            => Task.FromResult(Result<List<QuoteChecklistItemDto>>.Success([]));

        public Task<Result<QuoteChecklistItemDto>> ToggleAsync(Guid itemId, bool completed, Guid userId, string userName)
            => throw new NotSupportedException();

        public Task SeedDefaultsAsync(Guid quoteId, PolicyLineOfBusiness lob)
            => Task.CompletedTask;
    }

    private sealed class NoOpPolicyNumberService : IPolicyNumberService
    {
        public Task<Result<PolicyNumberGenerationResult>> GenerateForBindAsync(Quote quote, Guid assignedById, DateOnly? effectiveDate = null)
            => throw new NotSupportedException();
    }

    private sealed class NoOpPolicyTransactionLifecycleService : IPolicyTransactionLifecycleService
    {
        public Task<Result> RecordCreatedAsync(PolicyTransaction transaction, Guid userId, string? notes = null)
            => Task.FromResult(Result.Success());

        public Task<Result> TransitionAsync(PolicyTransaction transaction, PolicyTransactionStatus toStatus, Guid userId, string? notes = null)
            => Task.FromResult(Result.Success());
    }

    private sealed class NoOpPolicyVersionService : IPolicyVersionService
    {
        public Task<PolicyVersion> EnsureCurrentVersionAsync(Policy policy, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<PolicyVersion> CreateVersionAsync(Policy policy, PolicyTransaction transaction, PolicyVersion? priorVersion, Guid userId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class NoOpUnderwritingClearanceService : IUnderwritingClearanceService
    {
        public Task<UnderwritingClearanceEvaluationDto?> GetLatestSubmissionAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult<UnderwritingClearanceEvaluationDto?>(null);

        public Task<UnderwritingClearanceEvaluationDto> EvaluateSubmissionAsync(Guid submissionId, Guid reviewerId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<UnderwritingClearanceEvaluationDto> OverrideSubmissionAsync(Guid submissionId, Guid overriddenById, string reason, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class NoOpUnderwritingReferralService : IUnderwritingReferralService
    {
        public Task SyncFromWriteupAsync(Guid quoteId, Guid userId, IMWriteupPayload payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> HasOpenRequiredReferralsAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<UnderwritingReferralSummaryDto> GetSubmissionSummaryAsync(Guid submissionId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<UnderwritingReferral> DecideAsync(Guid referralId, UnderwritingReferralStatus decision, Guid decisionById, string? notes, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class NoOpUnderwritingControlEnforcementService : IUnderwritingControlEnforcementService
    {
        public Task<UnderwritingControlEvaluationSummaryDto> EvaluateQuoteAsync(Guid quoteId, UnderwritingControlStage stage, Guid evaluatedByUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<UnderwritingControlEvaluationSummaryDto> EvaluatePolicyAsync(Guid policyId, UnderwritingControlStage stage, Guid evaluatedByUserId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<UnderwritingControlEnforcementResultDto>> GetForTargetAsync(UnderwritingControlTargetType targetType, Guid targetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UnderwritingControlEnforcementResultDto>>([]);

        public Task<Result<UnderwritingControlEnforcementResultDto>> OverrideAsync(Guid resultId, Guid userId, string reason, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
