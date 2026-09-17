namespace TableMint.WebApi.Contracts;

public sealed record CreateTableContract(
    string Name,
    IReadOnlyList<CreateFieldRequest>? Fields = null);
