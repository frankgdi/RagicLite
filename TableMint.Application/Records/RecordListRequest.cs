using TableMint.Application.Abstractions;

namespace TableMint.Application.Records;

public sealed record RecordListRequest(
    int Page = 1,
    int PageSize = 50,
    RecordSortBy SortBy = RecordSortBy.CreatedAt,
    SortDirection SortDirection = SortDirection.Descending,
    string? FilterField = null,
    string? FilterValue = null);
