using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class QuotePolicyFormSelectionServiceTests
{
    [Fact]
    public async Task ResetFromPackageAsync_PrefersProgramPackageOverGenericPackage()
    {
        await using var db = CreateDb();
        var programId = Guid.NewGuid();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Oden Specialty" };
        var quote = CreateQuote(carrier, "NC", PolicyLineOfBusiness.InlandMarine, programId);
        var genericForm = CreateTemplate("GEN-NC", "Generic NC notice");
        var programForm = CreateTemplate("LONG-NC", "Longleaf NC notice");
        var genericPackage = CreatePackage(carrier, PolicyLineOfBusiness.InlandMarine, "NC", "Generic NC package", null, genericForm);
        var programPackage = CreatePackage(carrier, PolicyLineOfBusiness.InlandMarine, "NC", "Longleaf NC package", programId, programForm);
        db.AddRange(carrier, quote.Submission.Insured, quote.Submission, quote, genericForm, programForm, genericPackage, programPackage);
        await db.SaveChangesAsync();

        var result = await CreateService(db).ResetFromPackageAsync(quote.Id);

        Assert.True(result.IsSuccess);
        var selection = Assert.Single(result.Value!);
        Assert.Equal(programForm.Id, selection.PolicyFormTemplateId);
        Assert.Equal("LONG-NC", selection.FormNumber);
    }

    [Fact]
    public async Task ResetFromPackageAsync_FallsBackToGenericPackageWhenProgramPackageIsMissing()
    {
        await using var db = CreateDb();
        var carrier = new Carrier { Id = Guid.NewGuid(), Name = "Oden Specialty" };
        var quote = CreateQuote(carrier, "SC", PolicyLineOfBusiness.GeneralLiability, Guid.NewGuid());
        var genericForm = CreateTemplate("GEN-SC", "Generic SC notice");
        var genericPackage = CreatePackage(carrier, PolicyLineOfBusiness.GeneralLiability, "SC", "Generic SC package", null, genericForm);
        db.AddRange(carrier, quote.Submission.Insured, quote.Submission, quote, genericForm, genericPackage);
        await db.SaveChangesAsync();

        var result = await CreateService(db).ResetFromPackageAsync(quote.Id);

        Assert.True(result.IsSuccess);
        var selection = Assert.Single(result.Value!);
        Assert.Equal(genericForm.Id, selection.PolicyFormTemplateId);
        Assert.Equal("GEN-SC", selection.FormNumber);
    }

    private static QuotePolicyFormSelectionService CreateService(PolicyFormsTestDbContext db)
    {
        var provider = new ServiceCollection()
            .AddSingleton<DbContext>(db)
            .BuildServiceProvider();

        return new QuotePolicyFormSelectionService(provider);
    }

    private static PolicyFormsTestDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PolicyFormsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PolicyFormsTestDbContext(options);
    }

    private static Quote CreateQuote(Carrier carrier, string state, PolicyLineOfBusiness lob, Guid? programId)
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
            ProgramId = programId,
            LineOfBusiness = lob,
            EffectiveDate = new DateOnly(2026, 6, 1),
            ExpirationDate = new DateOnly(2027, 6, 1),
        };
    }

    private static PolicyFormTemplate CreateTemplate(string formNumber, string name) => new()
    {
        Id = Guid.NewGuid(),
        FormNumber = formNumber,
        Name = name,
        DocumentType = DocumentType.PolicyForm,
        IsActive = true,
    };

    private static PolicyPackageConfiguration CreatePackage(
        Carrier carrier,
        PolicyLineOfBusiness lob,
        string state,
        string name,
        Guid? programId,
        PolicyFormTemplate form)
    {
        var package = new PolicyPackageConfiguration
        {
            Id = Guid.NewGuid(),
            CarrierId = carrier.Id,
            Carrier = carrier,
            ProgramConfigurationId = programId,
            LineOfBusiness = lob,
            State = state,
            Name = name,
            IsActive = true,
        };
        package.Forms.Add(new PolicyPackageForm
        {
            Id = Guid.NewGuid(),
            PolicyPackageConfigurationId = package.Id,
            PolicyPackageConfiguration = package,
            PolicyFormTemplateId = form.Id,
            PolicyFormTemplate = form,
            SequenceOrder = 1,
            FormType = PolicyFormType.Mandatory,
        });
        return package;
    }

    private sealed class PolicyFormsTestDbContext : DbContext
    {
        public PolicyFormsTestDbContext(DbContextOptions<PolicyFormsTestDbContext> options) : base(options)
        {
        }

        public DbSet<Carrier> Carriers => Set<Carrier>();
        public DbSet<Insured> Insureds => Set<Insured>();
        public DbSet<Submission> Submissions => Set<Submission>();
        public DbSet<Quote> Quotes => Set<Quote>();
        public DbSet<PolicyFormTemplate> PolicyFormTemplates => Set<PolicyFormTemplate>();
        public DbSet<PolicyPackageConfiguration> PolicyPackageConfigurations => Set<PolicyPackageConfiguration>();
        public DbSet<PolicyPackageForm> PolicyPackageForms => Set<PolicyPackageForm>();
        public DbSet<QuotePolicyFormSelection> QuotePolicyFormSelections => Set<QuotePolicyFormSelection>();
        public DbSet<SubmissionAdditionalInterest> SubmissionAdditionalInterests => Set<SubmissionAdditionalInterest>();
        public DbSet<SubmissionLocation> SubmissionLocations => Set<SubmissionLocation>();
        public DbSet<QuoteRatingSnapshot> QuoteRatingSnapshots => Set<QuoteRatingSnapshot>();

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
            modelBuilder.Entity<Submission>().Ignore(s => s.Drivers);
            modelBuilder.Entity<Submission>().Ignore(s => s.Vehicles);
            modelBuilder.Entity<Submission>().Ignore(s => s.PriorCarriers);
            modelBuilder.Entity<Submission>().Ignore(s => s.LossYears);
            modelBuilder.Entity<Submission>().Ignore(s => s.GLClassifications);
            modelBuilder.Entity<Submission>().Ignore(s => s.Equipment);
            modelBuilder.Entity<Submission>().Ignore(s => s.AdditionalInterestBlankets);
            modelBuilder.Entity<Submission>().Ignore(s => s.Supplemental);
            modelBuilder.Entity<Submission>().Ignore(s => s.GLCoverages);
            modelBuilder.Entity<Submission>().Ignore(s => s.IMCoverages);
            modelBuilder.Entity<Quote>().Ignore(q => q.CreatedBy);
            modelBuilder.Entity<Quote>().Ignore(q => q.Notes);
            modelBuilder.Entity<Quote>().Ignore(q => q.Attachments);
            modelBuilder.Entity<Quote>().Ignore(q => q.UWWriteup);
            modelBuilder.Entity<Quote>().Ignore(q => q.Program);
            modelBuilder.Entity<PolicyFormTemplate>().Ignore(f => f.FieldMappings);
            modelBuilder.Entity<PolicyPackageConfiguration>().Ignore(p => p.ProgramConfiguration);
            modelBuilder.Entity<PolicyPackageConfiguration>().HasMany(p => p.Forms).WithOne(f => f.PolicyPackageConfiguration);
            modelBuilder.Entity<PolicyPackageForm>().HasOne(f => f.PolicyFormTemplate).WithMany();
            modelBuilder.Entity<QuoteRatingSnapshot>().Ignore(s => s.Quote);
            modelBuilder.Entity<QuoteRatingSnapshot>().Ignore(s => s.PolicyTransaction);
            modelBuilder.Entity<QuoteRatingSnapshot>().Ignore(s => s.RatingPlanVersion);
            modelBuilder.Entity<QuoteRatingSnapshot>().Ignore(s => s.RatedBy);
            modelBuilder.Entity<QuoteRatingSnapshot>().Ignore(s => s.Lines);
        }
    }
}
