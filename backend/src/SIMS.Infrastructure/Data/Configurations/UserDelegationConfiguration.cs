using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class UserDelegationConfiguration : IEntityTypeConfiguration<UserDelegation>
{
    public void Configure(EntityTypeBuilder<UserDelegation> builder)
    {
        builder.ToTable("user_delegations");
        builder.HasKey(d => d.Id);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.DelegateToUser)
            .WithMany()
            .HasForeignKey(d => d.DelegateToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.UserId, d.IsActive });
    }
}
