namespace TableMint.Application.Records;

public sealed record RecordPageView(
    IReadOnlyList<RecordView> Items,
    int Page,
    int PageSize,
    int TotalCount);
