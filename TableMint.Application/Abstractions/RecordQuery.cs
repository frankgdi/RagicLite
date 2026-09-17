using System.Text.Json;

namespace TableMint.Application.Abstractions;

public enum RecordSortBy
{
    CreatedAt,
    UpdatedAt,
}

public enum SortDirection
{
    Ascending,
    Descending,
}

public sealed record RecordQuery(
    int Page,
    int PageSize,
    RecordSortBy SortBy = RecordSortBy.CreatedAt,
    SortDirection SortDirection = SortDirection.Descending,
    string? FilterFieldKey = null,
    JsonElement? FilterValue = null);
