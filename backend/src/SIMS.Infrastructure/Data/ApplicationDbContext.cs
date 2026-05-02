using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<User, Role, Guid,
    IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentLocation> AgentLocations => Set<AgentLocation>();
    public DbSet<AgentContact> AgentContacts => Set<AgentContact>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<CarrierContact> CarrierContacts => Set<CarrierContact>();
    public DbSet<CarrierLineOfBusiness> CarrierLinesOfBusiness => Set<CarrierLineOfBusiness>();
    public DbSet<Insured> Insureds => Set<Insured>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<PolicyTransaction> PolicyTransactions => Set<PolicyTransaction>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<InboundEmail> InboundEmails => Set<InboundEmail>();
    public DbSet<EmailAttachment> EmailAttachments => Set<EmailAttachment>();
    public DbSet<SubmissionLocation> SubmissionLocations => Set<SubmissionLocation>();
    public DbSet<SubmissionDriver> SubmissionDrivers => Set<SubmissionDriver>();
    public DbSet<SubmissionVehicle> SubmissionVehicles => Set<SubmissionVehicle>();
    public DbSet<SubmissionPriorCarrier> SubmissionPriorCarriers => Set<SubmissionPriorCarrier>();
    public DbSet<SubmissionSupplemental> SubmissionSupplementals => Set<SubmissionSupplemental>();
    public DbSet<SubmissionGLCoverages> SubmissionGLCoverages => Set<SubmissionGLCoverages>();
    public DbSet<SubmissionGLClassification> SubmissionGLClassifications => Set<SubmissionGLClassification>();
    public DbSet<SubmissionIMCoverages> SubmissionIMCoverages => Set<SubmissionIMCoverages>();
    public DbSet<SubmissionEquipment> SubmissionEquipment => Set<SubmissionEquipment>();
    public DbSet<TaskType> TaskTypes => Set<TaskType>();
    public DbSet<TaskInstance> TaskInstances => Set<TaskInstance>();
    public DbSet<SystemEvent> SystemEvents => Set<SystemEvent>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<HolidayCalendar> HolidayCalendar => Set<HolidayCalendar>();
    public DbSet<UserDelegation> UserDelegations => Set<UserDelegation>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<TaskAuditEntry> TaskAuditEntries => Set<TaskAuditEntry>();

    // Accounting
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<GlAccountMap> GlAccountMaps => Set<GlAccountMap>();
    public DbSet<Payee> Payees => Set<Payee>();
    public DbSet<JournalEntryRollup> JournalEntryRollups => Set<JournalEntryRollup>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<FeeDefinition> FeeDefinitions => Set<FeeDefinition>();
    public DbSet<FeeRuleVersion> FeeRuleVersions => Set<FeeRuleVersion>();
    public DbSet<FeeStateTaxability> FeeStateTaxabilities => Set<FeeStateTaxability>();
    public DbSet<FeePremiumBracket> FeePremiumBrackets => Set<FeePremiumBracket>();
    public DbSet<FeeAuditLog> FeeAuditLogs => Set<FeeAuditLog>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<CashApplication> CashApplications => Set<CashApplication>();
    public DbSet<CashMovementInstruction> CashMovementInstructions => Set<CashMovementInstruction>();
    public DbSet<CashDistributionBatch> CashDistributionBatches => Set<CashDistributionBatch>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables
        builder.Entity<User>().ToTable("users");
        builder.Entity<Role>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        // Apply all IEntityTypeConfiguration classes in this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global soft-delete query filters
        builder.Entity<Agent>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AgentLocation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<AgentContact>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Carrier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CarrierContact>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<CarrierLineOfBusiness>().HasQueryFilter(e => !e.Carrier.IsDeleted);
        builder.Entity<Insured>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Submission>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Quote>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<PolicyTransaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Note>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Attachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DocumentTemplate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<InboundEmail>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<EmailAttachment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionLocation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionDriver>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionVehicle>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionPriorCarrier>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionSupplemental>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionGLCoverages>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionGLClassification>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionIMCoverages>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SubmissionEquipment>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<TaskType>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<TaskInstance>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<SystemEvent>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<WorkflowTemplate>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<WorkflowStep>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<HolidayCalendar>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserDelegation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<EscalationRule>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<LedgerTransaction>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException(
                    "LedgerTransaction rows are immutable. Use a reversing entry.");
        }

        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
        }
    }
}
