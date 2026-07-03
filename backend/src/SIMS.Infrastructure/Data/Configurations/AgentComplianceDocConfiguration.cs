using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AgentComplianceDocConfiguration : IEntityTypeConfiguration<AgentComplianceDoc>
{
    public void Configure(EntityTypeBuilder<AgentComplianceDoc> b)
    {
        b.ToTable("agent_compliance_docs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.AgentId).HasColumnName("agent_id");
        b.Property(x => x.DocType).HasColumnName("doc_type").HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ExpirationDate).HasColumnName("expiration_date");
        b.Property(x => x.EoLimit).HasColumnName("eo_limit").HasColumnType("numeric(18,2)");
        b.Property(x => x.EoCarrierName).HasColumnName("eo_carrier_name").HasMaxLength(200);
        b.Property(x => x.LicenseState).HasColumnName("license_state").HasMaxLength(2);
        b.Property(x => x.ExecutedDate).HasColumnName("executed_date");
        b.Property(x => x.IsContinuous).HasColumnName("is_continuous").HasDefaultValue(false);
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        b.HasOne(x => x.Agent).WithMany(a => a.ComplianceDocs).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.AgentId, x.DocType });
    }
}
