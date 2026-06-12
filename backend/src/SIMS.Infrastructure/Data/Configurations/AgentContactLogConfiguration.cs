using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AgentContactLogConfiguration : IEntityTypeConfiguration<AgentContactLog>
{
    public void Configure(EntityTypeBuilder<AgentContactLog> b)
    {
        b.ToTable("agent_contact_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.AgentId).HasColumnName("agent_id");
        b.Property(x => x.LogDate).HasColumnName("log_date");
        b.Property(x => x.LogType).HasColumnName("log_type").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ContactName).HasColumnName("contact_name").HasMaxLength(200);
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasOne(x => x.Agent).WithMany(a => a.ContactLogs).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.AgentId);
    }
}
