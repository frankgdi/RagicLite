namespace TableMint.Domain.Tables;

public sealed record SelectOption(
    string Value,
    string Label,
    bool IsActive);
