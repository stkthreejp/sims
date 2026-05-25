using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class PolicyNumberServiceTests
{
    [Fact]
    public async Task GenerateForBindAsync_UsesMatchingSequenceAndRecordsUsage()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Oden Specialty" };
        var quote = CreateQuote(carrier, "NC", PolicyLineOfBusiness.InlandMarine, new DateOnly(2026, 6, 1));
        var sequence = new PolicyNumberSequence
        {
            Id = Guid.NewGuid(),
            Name = "Oden IM",
            Format = "{CARRIER}-{LOB}-{YY}-{STATE}-{SEQ:000}",
            TermSuffixFormat = "-T{TERM:00}",
            NextNumber = 7,
            IsActive = true,
        };
        var assignment = new PolicyNumberAssignment
        {
            Id = Guid.NewGuid(),
            PolicyNumberSequenceId = sequence.Id,
            PolicyNumberSequence = sequence,
            CarrierId = carrier.Id,
            Carrier = carrier,
            LineOfBusiness = PolicyLineOfBusiness.InlandMarine,
            State = "NC",
            IsActive = true,
        };
        db.AddRange(carrier, quote.Submission.Insured, quote.Submission, quote, sequence, assignment);
        await db.SaveChangesAsync();

        var result = await new PolicyNumberService(db).GenerateForBindAsync(quote, userId);

        Assert.True(result.IsSuccess);
        Assert.Equal("ODENSPECIALT-IM-26-NC-007-T01", result.Value!.PolicyNumber);
        Assert.Equal("ODENSPECIALT-IM-26-NC-007", result.Value.BasePolicyNumber);
        Assert.Equal(1, result.Value.TermNumber);
        Assert.Equal(assignment.Id, result.Value.AssignmentId);
        Assert.Equal(sequence.Id, result.Value.SequenceId);
        Assert.Equal(8, sequence.NextNumber);

        var usage = await db.Set<PolicyNumberSequenceUsage>().SingleAsync();
        Assert.Equal(quote.Id, usage.QuoteId);
        Assert.Equal("ODENSPECIALT-IM-26-NC-007-T01", usage.FullPolicyNumber);
        Assert.Equal(7, usage.SequenceValue);
        Assert.Equal(userId, usage.AssignedById);
    }

    [Fact]
    public async Task GenerateForBindAsync_PrefersStateSpecificAssignmentOverAllStateAssignment()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Longleaf" };
        var quote = CreateQuote(carrier, "NC", PolicyLineOfBusiness.GeneralLiability, new DateOnly(2026, 1, 15));
        var globalSequence = CreateSequence("Global", "GLOBAL-{SEQ:00}", 3);
        var stateSequence = CreateSequence("North Carolina", "NC-{SEQ:00}", 4);
        db.AddRange(
            carrier,
            quote.Submission.Insured,
            quote.Submission,
            quote,
            globalSequence,
            stateSequence,
            CreateAssignment(globalSequence, carrier, PolicyLineOfBusiness.GeneralLiability, null, priority: 0),
            CreateAssignment(stateSequence, carrier, PolicyLineOfBusiness.GeneralLiability, "NC", priority: 99));
        await db.SaveChangesAsync();

        var result = await new PolicyNumberService(db).GenerateForBindAsync(quote, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("NC-04-01", result.Value!.PolicyNumber);
    }

    [Fact]
    public async Task GenerateForBindAsync_UsesCarrierLobAssignmentWhenQuoteHasProgram()
    {
        await using var db = CreateDb();
        var program = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Oden Specialty" };
        var quote = CreateQuote(carrier, "SC", PolicyLineOfBusiness.InlandMarine, new DateOnly(2026, 5, 1));
        quote.ProgramId = program.Id;
        quote.Program = program;
        var sequence = CreateSequence("Carrier LOB", "CLOB-{LOB}-{SEQ:00}", 12);
        db.AddRange(
            program,
            carrier,
            quote.Submission.Insured,
            quote.Submission,
            quote,
            sequence,
            CreateAssignment(sequence, carrier, PolicyLineOfBusiness.InlandMarine, null));
        await db.SaveChangesAsync();

        var result = await new PolicyNumberService(db).GenerateForBindAsync(quote, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("CLOB-IM-12-01", result.Value!.PolicyNumber);
    }

    [Fact]
    public async Task GenerateForBindAsync_PrefersProgramSpecificAssignmentOverAllProgramAssignment()
    {
        await using var db = CreateDb();
        var longleaf = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Longleaf", Code = "LONGLEAF", IsActive = true };
        var shuttlebee = new ProgramConfiguration { Id = Guid.NewGuid(), Name = "Shuttlebee", Code = "SHUTTLEBEE", IsActive = true };
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Oden Specialty" };
        var quote = CreateQuote(carrier, "GA", PolicyLineOfBusiness.GeneralLiability, new DateOnly(2026, 7, 1));
        quote.ProgramId = shuttlebee.Id;
        quote.Program = shuttlebee;
        var allProgramSequence = CreateSequence("Carrier LOB", "CLOB-{LOB}-{SEQ:00}", 21);
        var shuttlebeeSequence = CreateSequence("Shuttlebee Carrier LOB", "SHUT-{LOB}-{SEQ:00}", 4);
        db.AddRange(
            longleaf,
            shuttlebee,
            carrier,
            quote.Submission.Insured,
            quote.Submission,
            quote,
            allProgramSequence,
            shuttlebeeSequence,
            CreateAssignment(allProgramSequence, carrier, PolicyLineOfBusiness.GeneralLiability, null),
            CreateAssignment(shuttlebeeSequence, carrier, PolicyLineOfBusiness.GeneralLiability, null, programConfigurationId: shuttlebee.Id));
        await db.SaveChangesAsync();

        var result = await new PolicyNumberService(db).GenerateForBindAsync(quote, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("SHUT-GL-04-01", result.Value!.PolicyNumber);
    }

    [Fact]
    public async Task GenerateForBindAsync_ResetsAnnualSequenceForPolicyEffectiveYear()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Reset Carrier" };
        var quote = CreateQuote(carrier, "SC", PolicyLineOfBusiness.AutoLiability, new DateOnly(2027, 3, 1));
        var sequence = CreateSequence("Annual", "{LOB}-{YYYY}-{SEQ:000}", 88);
        sequence.ResetAnnually = true;
        sequence.LastResetYear = 2026;
        var assignment = CreateAssignment(sequence, carrier, PolicyLineOfBusiness.AutoLiability, null);
        db.AddRange(carrier, quote.Submission.Insured, quote.Submission, quote, sequence, assignment);
        await db.SaveChangesAsync();

        var result = await new PolicyNumberService(db).GenerateForBindAsync(quote, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal("AL-2027-001-01", result.Value!.PolicyNumber);
        Assert.Equal(2, sequence.NextNumber);
        Assert.Equal(2027, sequence.LastResetYear);
    }

    [Fact]
    public async Task GenerateForBindAsync_UsesLegacyFallbackWhenNoAssignmentMatches()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "No Setup" };
        var quote = CreateQuote(carrier, "GA", PolicyLineOfBusiness.InlandMarine, new DateOnly(2026, 8, 1));
        var year = DateTime.UtcNow.Year;
        db.AddRange(
            carrier,
            quote.Submission.Insured,
            quote.Submission,
            quote,
            new Policy { Id = Guid.NewGuid(), PolicyNumber = $"POL-{year}-00001" },
            new Policy { Id = Guid.NewGuid(), PolicyNumber = $"POL-{year}-00002" });
        await db.SaveChangesAsync();

        var result = await new PolicyNumberService(db).GenerateForBindAsync(quote, Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal($"POL-{year}-00003", result.Value!.PolicyNumber);
        Assert.Null(result.Value.SequenceId);
        Assert.Empty(await db.Set<PolicyNumberSequenceUsage>().ToListAsync());
    }

    [Fact]
    public async Task GenerateForBindAsync_FailsWhenQuoteAlreadyHasPolicyNumber()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Already Bound" };
        var quote = CreateQuote(carrier, "NC", PolicyLineOfBusiness.InlandMarine, new DateOnly(2026, 6, 1));
        quote.PolicyNumber = "EXISTING";

        var result = await new PolicyNumberService(db).GenerateForBindAsync(quote, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("POLICY_NUMBER_EXISTS", result.ErrorCode);
    }

    private static PolicyNumberTestDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PolicyNumberTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PolicyNumberTestDbContext(options);
    }

    private static Quote CreateQuote(Carrier carrier, string state, PolicyLineOfBusiness lob, DateOnly effectiveDate)
    {
        var insured = new Insured
        {
            Id = Guid.NewGuid(),
            InsuredType = InsuredType.Commercial,
            CompanyName = "Test Insured",
            State = state,
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SubmissionNumber = "SUB-1",
            InsuredId = insured.Id,
            Insured = insured,
        };

        return new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = "Q-1",
            CarrierId = carrier.Id,
            Carrier = carrier,
            SubmissionId = submission.Id,
            Submission = submission,
            LineOfBusiness = lob,
            EffectiveDate = effectiveDate,
            ExpirationDate = effectiveDate.AddYears(1),
        };
    }

    private static PolicyNumberSequence CreateSequence(string name, string format, long nextNumber) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Format = format,
        TermSuffixFormat = "-{TERM:00}",
        NextNumber = nextNumber,
        IsActive = true,
    };

    private static PolicyNumberAssignment CreateAssignment(
        PolicyNumberSequence sequence,
        Carrier carrier,
        PolicyLineOfBusiness lob,
        string? state,
        int priority = 0,
        Guid? programConfigurationId = null) => new()
    {
        Id = Guid.NewGuid(),
        PolicyNumberSequenceId = sequence.Id,
        PolicyNumberSequence = sequence,
        ProgramConfigurationId = programConfigurationId,
        CarrierId = carrier.Id,
        Carrier = carrier,
        LineOfBusiness = lob,
        State = state,
        Priority = priority,
        IsActive = true,
    };

    private sealed class PolicyNumberTestDbContext : DbContext
    {
        public PolicyNumberTestDbContext(DbContextOptions<PolicyNumberTestDbContext> options) : base(options)
        {
        }

        public DbSet<Carrier> Carriers => Set<Carrier>();
        public DbSet<Insured> Insureds => Set<Insured>();
        public DbSet<Submission> Submissions => Set<Submission>();
        public DbSet<Quote> Quotes => Set<Quote>();
        public DbSet<Policy> Policies => Set<Policy>();
        public DbSet<PolicyNumberSequence> PolicyNumberSequences => Set<PolicyNumberSequence>();
        public DbSet<PolicyNumberAssignment> PolicyNumberAssignments => Set<PolicyNumberAssignment>();
        public DbSet<PolicyNumberSequenceUsage> PolicyNumberSequenceUsages => Set<PolicyNumberSequenceUsage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Carrier>().Ignore(c => c.LinesOfBusiness);
            modelBuilder.Entity<Carrier>().Ignore(c => c.Contacts);
            modelBuilder.Entity<Carrier>().Ignore(c => c.Quotes);
            modelBuilder.Entity<Insured>().Ignore(i => i.CreatedBy);
            modelBuilder.Entity<Insured>().Ignore(i => i.Submissions);
            modelBuilder.Entity<Submission>().Ignore(s => s.Agent);
            modelBuilder.Entity<Submission>().Ignore(s => s.Underwriter);
            modelBuilder.Entity<Submission>().Ignore(s => s.AssistantUW);
            modelBuilder.Entity<Submission>().Ignore(s => s.CreatedBy);
            modelBuilder.Entity<Submission>().Ignore(s => s.Quotes);
            modelBuilder.Entity<Submission>().Ignore(s => s.Locations);
            modelBuilder.Entity<Submission>().Ignore(s => s.Drivers);
            modelBuilder.Entity<Submission>().Ignore(s => s.Vehicles);
            modelBuilder.Entity<Submission>().Ignore(s => s.PriorCarriers);
            modelBuilder.Entity<Submission>().Ignore(s => s.LossYears);
            modelBuilder.Entity<Submission>().Ignore(s => s.GLClassifications);
            modelBuilder.Entity<Submission>().Ignore(s => s.Equipment);
            modelBuilder.Entity<Submission>().Ignore(s => s.AdditionalInterests);
            modelBuilder.Entity<Submission>().Ignore(s => s.AdditionalInterestBlankets);
            modelBuilder.Entity<Submission>().Ignore(s => s.Supplemental);
            modelBuilder.Entity<Submission>().Ignore(s => s.GLCoverages);
            modelBuilder.Entity<Submission>().Ignore(s => s.IMCoverages);
            modelBuilder.Entity<Quote>().Ignore(q => q.CreatedBy);
            modelBuilder.Entity<Quote>().Ignore(q => q.Notes);
            modelBuilder.Entity<Quote>().Ignore(q => q.Attachments);
            modelBuilder.Entity<Quote>().Ignore(q => q.UWWriteup);
            modelBuilder.Entity<ProgramConfiguration>().Ignore(p => p.ProgramCarriers);
            modelBuilder.Entity<ProgramConfiguration>().Ignore(p => p.GuidelineDocuments);
            modelBuilder.Entity<ProgramConfiguration>().Ignore(p => p.GuidelineControls);
            modelBuilder.Entity<Policy>().Ignore(p => p.Submission);
            modelBuilder.Entity<Policy>().Ignore(p => p.BoundQuote);
            modelBuilder.Entity<Policy>().Ignore(p => p.Carrier);
            modelBuilder.Entity<Policy>().Ignore(p => p.Transactions);
            modelBuilder.Entity<Policy>().Ignore(p => p.Versions);
            modelBuilder.Entity<PolicyNumberSequenceUsage>().Ignore(u => u.Quote);
            modelBuilder.Entity<PolicyNumberSequenceUsage>().Ignore(u => u.Policy);
            modelBuilder.Entity<PolicyNumberSequenceUsage>().Ignore(u => u.AssignedBy);
        }
    }
}
