namespace WorkOps.Api.Contracts.Common;

public sealed class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value > 0 ? value : 1;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > 0 ? (value > 100 ? 100 : value) : 20;
    }
}
