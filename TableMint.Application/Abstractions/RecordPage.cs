using TableMint.Domain.Tables;

namespace TableMint.Application.Abstractions;

public sealed record RecordPage(
    IReadOnlyList<Record> Items,
    int TotalCount);
