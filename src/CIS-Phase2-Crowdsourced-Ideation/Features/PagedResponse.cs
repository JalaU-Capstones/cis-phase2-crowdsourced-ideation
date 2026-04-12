namespace CIS.Phase2.CrowdsourcedIdeation.Features;

/// <summary>
/// Generic wrapper for paginated list responses.
/// </summary>
public sealed record PagedResponse<T>(
    IEnumerable<T> Data,
    int CurrentPage,
    int PageSize,
    int TotalItems,
    int TotalPages);