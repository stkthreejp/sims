namespace SIMS.Domain.Entities;

public class QuoteUWWriteupCondition : BaseEntity
{
    public Guid WriteupId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
    public bool Satisfied { get; set; } = false;
    public int SortOrder { get; set; }

    public QuoteUWWriteup Writeup { get; set; } = null!;
}
