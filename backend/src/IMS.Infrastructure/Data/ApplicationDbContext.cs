using IMS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IMS.Infrastructure.Data;

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
