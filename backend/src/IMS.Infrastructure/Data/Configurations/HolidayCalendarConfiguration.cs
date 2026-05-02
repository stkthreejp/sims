using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class HolidayCalendarConfiguration : IEntityTypeConfiguration<HolidayCalendar>
{
    public void Configure(EntityTypeBuilder<HolidayCalendar> builder)
    {
        builder.ToTable("holiday_calendar");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).IsRequired().HasMaxLength(200);

        builder.HasIndex(h => h.Date).IsUnique();
    }
}
