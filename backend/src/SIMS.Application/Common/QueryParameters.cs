namespace SIMS.Application.Common;

public class QueryParameters
{
    private const int MaxPageSize = 100;
    private int _page = 1;
    private int _pageSize = 25;
    private string _sortDir = "desc";

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : value > MaxPageSize ? MaxPageSize : value;
    }
    public string? Search { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public string SortDir
    {
        get => _sortDir;
        set => _sortDir = value?.ToLowerInvariant() == "asc" ? "asc" : "desc";
    }
}
