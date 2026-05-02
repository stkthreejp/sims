namespace IMS.Domain.Entities;

public class HolidayCalendar : BaseEntity
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}
