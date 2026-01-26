namespace WorkOps.Api.Contracts.Common;

public sealed class ListResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasNext => (Page * PageSize) < TotalCount;
}
