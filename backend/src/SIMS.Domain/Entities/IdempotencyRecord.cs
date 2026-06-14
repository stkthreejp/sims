namespace SIMS.Domain.Entities;

public class IdempotencyRecord
{
    public long Id { get; set; }
    public string Key { get; set; } = null!;
    public string RequestPath { get; set; } = null!;
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
