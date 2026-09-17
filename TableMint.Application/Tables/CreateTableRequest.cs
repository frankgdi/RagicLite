namespace TableMint.Application.Tables;

public sealed record CreateTableRequest(
    string Name,
    IReadOnlyList<AddFieldRequest>? Fields = null);
