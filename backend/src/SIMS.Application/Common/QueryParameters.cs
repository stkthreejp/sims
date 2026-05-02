namespace SIMS.Application.Common;

public class QueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 25;

    public int Page { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }
    public string? Search { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public string SortDir { get; set; } = "desc";
}
