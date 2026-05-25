using Microsoft.EntityFrameworkCore;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class ProposalDocumentConfigurationServiceTests
{
    [Fact]
    public async Task CreateAsync_RejectsProgramSpecificConfigurationWhenCarrierLobIsNotConfiguredForProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var template = CreateTemplate("Longleaf proposal");
        db.AddRange(
            program,
            carrier,
            template,
            new ProgramCarrier
            {
                ProgramConfigurationId = program.Id,
                CarrierId = carrier.Id,
                IsActive = true,
                EffectiveDate = new DateOnly(2026, 1, 1),
            });
        await db.SaveChangesAsync();

        var result = await new ProposalDocumentConfigurationService(db).CreateAsync(new(
            program.Id,
            carrier.Id,
            PolicyLineOfBusiness.InlandMarine,
            null,
            ProposalDocumentRole.Proposal,
            template.Id,
            1,
            true,
            null,
            null,
            null));

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_PROGRAM_SETUP_PATH", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_AllowsProgramSpecificConfigurationWhenCarrierLobStateIsConfiguredForProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var template = CreateTemplate("Texas notice");
        db.AddRange(
            program,
            carrier,
            template,
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

        var result = await new ProposalDocumentConfigurationService(db).CreateAsync(new(
            program.Id,
            carrier.Id,
            PolicyLineOfBusiness.InlandMarine,
            "TX",
            ProposalDocumentRole.StateNotice,
            template.Id,
            1,
            true,
            null,
            null,
            null));

        Assert.True(result.IsSuccess);
        Assert.Equal(program.Id, result.Value!.ProgramConfigurationId);
        Assert.Equal(carrier.Id, result.Value.CarrierId);
        Assert.Equal("TX", result.Value.State);
    }

    [Fact]
    public async Task ResolveForQuoteAsync_PrefersProgramProposalAndIncludesMatchingStateNotice()
    {
        await using var db = CreateDb();
        var programId = Guid.NewGuid();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var genericProposal = CreateTemplate("Generic proposal");
        var programProposal = CreateTemplate("Longleaf proposal");
        var ncNotice = CreateTemplate("North Carolina notice");
        var scNotice = CreateTemplate("South Carolina notice");
        var quote = CreateQuote(programId, carrier, "NC");

        db.AddRange(
            carrier,
            quote.Submission.Insured,
            quote.Submission,
            quote,
            genericProposal,
            programProposal,
            ncNotice,
            scNotice,
            new ProposalDocumentConfiguration
            {
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                Role = ProposalDocumentRole.Proposal,
                DocumentTemplateId = genericProposal.Id,
                IsActive = true,
            },
            new ProposalDocumentConfiguration
            {
                ProgramConfigurationId = programId,
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                Role = ProposalDocumentRole.Proposal,
                DocumentTemplateId = programProposal.Id,
                IsActive = true,
            },
            new ProposalDocumentConfiguration
            {
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                State = "NC",
                Role = ProposalDocumentRole.StateNotice,
                DocumentTemplateId = ncNotice.Id,
                IsActive = true,
            },
            new ProposalDocumentConfiguration
            {
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                State = "SC",
                Role = ProposalDocumentRole.StateNotice,
                DocumentTemplateId = scNotice.Id,
                IsActive = true,
            });
        await db.SaveChangesAsync();

        var result = await new ProposalDocumentConfigurationService(db).ResolveForQuoteAsync(quote.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(programProposal.Id, result.Value!.Proposal.DocumentTemplateId);
        Assert.Collection(result.Value.Notices, notice => Assert.Equal(ncNotice.Id, notice.DocumentTemplateId));
    }

    [Fact]
    public async Task ResolveForQuoteAsync_FallsBackToGenericProposalWhenProgramProposalIsMissing()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Falls Lake", IsActive = true };
        var genericProposal = CreateTemplate("Generic proposal");
        var quote = CreateQuote(Guid.NewGuid(), carrier, "NC");

        db.AddRange(
            carrier,
            quote.Submission.Insured,
            quote.Submission,
            quote,
            genericProposal,
            new ProposalDocumentConfiguration
            {
                CarrierId = carrier.Id,
                LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
                Role = ProposalDocumentRole.Proposal,
                DocumentTemplateId = genericProposal.Id,
                IsActive = true,
            });
        await db.SaveChangesAsync();

        var result = await new ProposalDocumentConfigurationService(db).ResolveForQuoteAsync(quote.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(genericProposal.Id, result.Value!.Proposal.DocumentTemplateId);
        Assert.Empty(result.Value.Notices);
    }

    private static DocumentTemplate CreateTemplate(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        EntityType = TemplateEntityType.Quote,
        Kind = DocumentTemplateKind.Document,
        HtmlContent = "<p>{{Quote.QuoteNumber}}</p>",
        CreatedById = Guid.NewGuid(),
        IsActive = true,
    };

    private static Quote CreateQuote(Guid programId, Carrier carrier, string state)
    {
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            CompanyName = "Longleaf Logging",
            AddressLine1 = "1 Pine Rd",
            City = "Raleigh",
            State = state,
            ZipCode = "27601",
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            InsuredId = insured.Id,
            Insured = insured,
            SubmissionNumber = "SUB-001",
        };
        return new Quote
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            CarrierId = carrier.Id,
            Carrier = carrier,
            SubmissionId = submission.Id,
            Submission = submission,
            QuoteNumber = "Q-001",
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            EffectiveDate = new DateOnly(2026, 6, 1),
            ExpirationDate = new DateOnly(2027, 6, 1),
        };
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
