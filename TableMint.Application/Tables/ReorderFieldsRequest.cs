namespace TableMint.Application.Tables;

public sealed record ReorderFieldsRequest(
    IReadOnlyList<Guid> FieldIds,
    int ExpectedVersion);
